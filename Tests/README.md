# KrissJourney Testing Framework

This document explains the testing infrastructure for the KrissJourney game engine.

## Folder Structure

The testing project is organized into the following structure:

```text
Tests/
├── Infrastructure/           # Testing infrastructure & utilities
│   ├── Mocks/                # Mock implementations for testing
│   │   ├── TerminalMock.cs   # Mock implementation of ITerminal
│   │   └── TestStatusManager.cs # Mock implementation of StatusManager
│   ├── Helpers/              # Test helper classes
│   │   ├── NodeTestHelper.cs # Helper for testing nodes
│   │   └── TestUtils.cs      # General test utilities
│   └── NodeTestRunner.cs     # Unified test runner for nodes
├── Unit/                     # Unit tests for specific components
│   ├── Infrastructure/       # Tests that guard the test harness itself
│   ├── Models/               # Tests for model classes
│   ├── Nodes/                # Tests for node behaviors (non-UI)
│   ├── Services/             # Tests for services
│   └── Converters/           # Tests for converters and serialization
├── Integration/              # Tests that span multiple components
│   └── StoryFlow/            # Tests for story flow and progression
└── Terminal/                 # Tests that interact with the terminal UI
    └── Nodes/                # Terminal-based node interaction tests
```

## Key Components

### 1. NodeTestRunner (Infrastructure)

`NodeTestRunner` is the core class that handles all the setup and management for testing nodes. It:

- Creates a test environment with a TestStatusManager that doesn't rely on the file system
- Sets up a mock terminal for testing terminal interactions (optional)
- Creates and configures test nodes with default or custom properties
- Manages the test chapter and node relationships
- Provides methods for simulating user input and verifying output

```csharp
// Example of creating and using a NodeTestRunner
var testRunner = new NodeTestRunner(setupTerminalMock: true);
var storyNode = testRunner.CreateNode<StoryNode>(configure: node => {
    node.Text = "Custom story text";
});
```

### 2. NodeTestBase (Terminal)

`NodeTestBase` is an abstract base class for terminal-based node tests that leverages the NodeTestRunner. It:

- Initializes a NodeTestRunner with terminal mock enabled
- Provides easy access to common test operations
- Simplifies the creation of terminal-based tests

```csharp
// Example of a test class that inherits from NodeTestBase
[TestClass]
public class MyNodeTests : NodeTestBase
{
    [TestMethod]
    public void MyTest()
    {
        var node = CreateNode<StoryNode>();
        SimulateUserInput(ConsoleKey.Enter);
        // ... test code ...
    }
}
```

### 3. TestStatusManager (Infrastructure/Mocks)

`TestStatusManager` is a special implementation of StatusManager that doesn't use the file system, making it ideal for unit tests. It:

- Overrides file system operations to use in-memory data structures
- Provides the same interface as the real StatusManager
- Enables testing without file system dependencies

### Save file isolation

**No test may construct the production `StatusManager`.** Its constructor loads - and, when no
save exists yet, immediately writes - `status.json` under `LocalApplicationData/KrissJourney/`,
which on a developer machine is the author's own playthrough. Two things follow from that: a
test can silently mutate a real save, and a test's outcome can depend on how far the machine's
owner has played. Both have already happened once. The author's save carries an inventory item
`magic_key` that appears nowhere in any chapter JSON; it is the fixture item from
`GameEngineTests.AddItemToInventory_And_EvaluateWithItemCondition_ReturnsTrue`, written there by
a past test run through the real manager. A `VisitedNodes` entry for chapter `99`, which does not
exist either, is residue of the same kind.

`GameEngineTestExtensions.Setup()` therefore builds on `TestStatusManager`, and so does every
other engine the suite constructs.

The isolation used to rest on a virtual call from `StatusManager`'s constructor: `AppDataPath`
was `protected virtual` with a private setter, and `TestStatusManager` overrode the getter to
`test_path`, so rewriting the constructor to use a local variable instead of the property would
have silently pointed the whole suite at the author's save. Issue 26 (2026-09-05) removed that
trap. The save folder is now a constructor parameter: `protected StatusManager(string
appDataPath)` sets the non-virtual `AppDataPath` and derives `_localStatusFilePath` from the
argument, the public parameterless constructor resolves the real path and chains to it, and
`TestStatusManager` passes `test_path` with `: base(TestPath)`. Overriding `AppDataPath` no
longer redirects anything, because nothing reads it back.

`Unit/Infrastructure/SaveFileIsolationTests.cs` guards both halves:

- an `[AssemblyInitialize]`/`[AssemblyCleanup]` pair hashes the real save file before the first
  test and re-checks hash and mtime after the last, so any write anywhere in the run fails it
  (verified: an `AssemblyCleanup` throw is reported as a failed test and `dotnet test` exits 1);
- two test methods assert that `TestStatusManager` resolves its save file outside the real
  save folder, and that `Setup()` hands back an engine built on `TestStatusManager`.

## Usage Examples

### Basic Node Testing (Unit Tests)

```csharp
// Location: Unit/Nodes/MyNodeTests.cs
namespace KrissJourney.Tests.Unit.Nodes;

[TestMethod]
public void BasicNodeTest()
{
    var testRunner = new NodeTestRunner(); // No terminal mock needed
    
    testRunner.TestNode<StoryNode>(node => {
        // Test code goes here
        Assert.AreEqual("Test StoryNode node", node.Text);
    });
}
```

### Testing Terminal Interactions

```csharp
// Location: Terminal/Nodes/MyTerminalTests.cs
namespace KrissJourney.Tests.Terminal.Nodes;

[TestMethod]
public void TerminalInteractionTest()
{
    var testRunner = new NodeTestRunner(setupTerminalMock: true);
    
    var actionNode = testRunner.CreateNode<ActionNode>();
    
    // Simulate user pressing Tab
    testRunner.SimulateUserInput(ConsoleKey.Tab);
    
    // Process the input
    actionNode.Load();
    
    // Verify the output
    var output = testRunner.GetTerminalOutput();
    Assert.IsTrue(output.Contains("Possible actions here"));
}
```

## Chapter walkthroughs (Integration)

`Tests/Integration/Chapter*WalkthroughTests.cs` drive a real chapter, through a real
`GameEngine`, against `TerminalMock`. They exist because most content bugs are not visible in
the JSON: a missing `break`, a reply block that renumbers the highlight, a gate that hides the
only way forward. Cheap structural checks come first (`ChapterStructureAssertions`,
`Integration/StoryFlow/StoryFlowTests.cs`); a walkthrough is for what only playing can show.

### How a walk is shaped

The engine's node loop is blocking and recursive - `node.Load()` -> `AdvanceToNext` ->
`GameEngine.LoadNode` -> the next `node.Load()` - and it never returns until the chapter runs
out. So the test thread runs the engine, and a background `Task` plays the player: it polls
`TerminalMock.GetOutput()` for the next prompt and only then enqueues the key that answers it.
Queuing keys blindly ahead of time does not work, because several nodes flush pending input
before they start reading.

`ChapterWalkthroughTestBase` holds the whole harness:

- `BuildScopedEngine(ids)` loads every real chapter through the production embedded-resource
  path, then narrows the engine's list to the ids given. A chapter's `islast` node then runs
  off the end of the list and throws a clean `ArgumentNullException`, which is how a walk
  proves it reached the end and went no further (`RunWalkToChapterEnd`). A walk that stops
  mid-chapter instead leaves the last prompt unanswered and lets the mock's `ReadKey` time out
  (`RunWalkToUnansweredPrompt`).
- `ContinueAsync` answers a `Typist.WaitForKey` banner; `ChooseAsync` answers a Choice or
  reply prompt, which prints no banner, so it waits on the option's own text instead;
  `ActAsync` / `DoActionAsync` type at an Action node's `\>` prompt.
- `DriveFightAsync` wins a `FightNode` by reacting to the oscillating-cursor frames as they are
  drawn rather than by predicting them.
- `PlaySurgeToVictoryAsync` reads the row a `SurgeNode` generated straight out of the drawn
  frame and plays it back. `PlayAlternatingDuetSurgeAsync` is its duet counterpart: under a
  `.S` pattern half the row is Saberinne's and presses on her glyphs are swallowed, so it
  presses one glyph per turn, reading whose turn it is from the caret's colour.

### Writing one

Walk the chapter JSON first and count the prompts: a Story node is one, a Dialogue line is one
only if it has `break` or a `childid`, a reply or choice is one, and an Action command that
carries a `childid` produces one after its answer. Then pick markers from the rendered text -
short, and never spanning one of the content's own `\n` line breaks, because the mock captures
the newlines exactly as authored.

Two more things bite. `ChoiceNode.selectedRow` and `DialogueNode.selectedRow` persist across
prompts, so navigation keys are relative to wherever the last pick left the highlight, and node
objects are shared for the life of the engine, so a hub revisited later remembers what was
already played. When a Dialogue node's next reply block is shorter than the one that last moved
the highlight, `DialogueNode` clamps the row down to that block's last option rather than
resetting it to the top, so a script that navigated in an earlier block may need no navigation
in the later one (c21 node 4, blocks of 3 then 2). And every walkthrough must consume every key it queues: the base class asserts
`KeyQueueCount` is zero at the end, which is what catches a walk that silently answered the
wrong prompt.

## Best Practices

1. Place tests in the appropriate folder:
   - Put unit tests in the `Unit` folder
   - Put integration tests in the `Integration` folder
   - Put terminal-based tests in the `Terminal` folder

2. Follow the namespace convention:
   - `KrissJourney.Tests.Unit.X` for unit tests
   - `KrissJourney.Tests.Integration.X` for integration tests
   - `KrissJourney.Tests.Terminal.X` for terminal tests
   - `KrissJourney.Tests.Infrastructure.X` for infrastructure

3. Use the appropriate base classes:
   - Use `NodeTestBase` for terminal-based tests
   - Use `NodeTestRunner` directly for non-terminal tests or complex scenarios

4. Keep test logic focused on specific behaviors

5. Use the configure parameter to set up complex node structures

6. Skip terminal tests that are difficult to mock with `Assert.Inconclusive`
