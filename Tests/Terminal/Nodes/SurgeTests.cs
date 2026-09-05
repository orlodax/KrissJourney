using System;
using System.Threading.Tasks;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Terminal.Nodes;

[TestClass]
public class SurgeTests : NodeTestBase
{
    // The mock's Write(message, color) formats as "[Color]message[/Color]" - see TerminalMock.
    static string Tag(ConsoleColor color, char glyph) => $"[{color}]{glyph}[/{color}]";
    static string White(char glyph) => Tag(ConsoleColor.White, glyph);
    static string DarkCyan(char glyph) => Tag(ConsoleColor.DarkCyan, glyph);
    static string DarkGray(char glyph) => Tag(ConsoleColor.DarkGray, glyph);
    static string Green(char glyph) => Tag(ConsoleColor.Green, glyph); // Saberinne's colour, EnCharacter.Saberinne.Color()

    SurgeNode _surge;

    [TestMethod]
    public void CorrectRow_RendersDocumentedColoursAndRageBar_ThenAdvancesOnSuccess()
    {
        // The dummy target must be created before the node under test: NodeTestRunner points
        // GameEngine.CurrentNode at whichever CreateNode call happens last, and Surge.Load()
        // reads GameEngine.CurrentNode/CurrentChapter for its tutorial gate (see below).
        CreateNode<StoryNode>(nodeId: 2); // dummy target for the success route

        // DrainRate/Restore both 0 so rage sits fixed at half the bar for the whole attempt:
        // this is what lets one frame carry both the "-" empty segments and the "█" filled
        // ones. The four glyphs are all exercised, both as pending (DarkCyan), current
        // (White) and consumed (DarkGray).
        _surge = CreateNode<SurgeNode>(configure: node =>
        {
            node.Challenge = new SurgeChallenge
            {
                Sequence = "L U R D",
                DrainRate = 0,
                Restore = 0,
                StartingRage = 50,
                MaxRage = 100,
                SuccessMessage = "The door gives.",
            };
            node.ChildId = 2;
        });

        Task script = Task.Run(async () =>
        {
            int idx = await WaitForOutputIndexAsync("Press a key to continue...", 0); // the "Let it out!" prompt
            SimulateUserInput(ConsoleKey.Enter);

            // The loop flushes any buffered keys once before its first frame, so keys sent
            // before "RAGE" is on screen would be silently discarded. Wait for it first.
            idx = await WaitForOutputIndexAsync("RAGE", idx);
            SimulateUserInput(ConsoleKey.LeftArrow); // correct: L

            idx = await WaitForOutputIndexAsync(White('↑'), idx); // U becomes current -> confirms L was consumed
            SimulateUserInput(ConsoleKey.UpArrow); // correct: U

            idx = await WaitForOutputIndexAsync(White('→'), idx); // R becomes current
            SimulateUserInput(ConsoleKey.RightArrow); // correct: R

            idx = await WaitForOutputIndexAsync(White('↓'), idx); // D becomes current
            SimulateUserInput(ConsoleKey.DownArrow); // correct: D, wins the row

            idx = await WaitForOutputIndexAsync("The door gives.", idx); // success message flown
            idx = await WaitForOutputIndexAsync("Press a key to continue...", idx);
            SimulateUserInput(ConsoleKey.Enter); // past the Surge's own WaitForKey

            // What follows is the dummy StoryNode's own WaitForKey, deliberately left
            // unanswered: it times out and throws, which LoadNode()'s wrapper expects.
        });

        LoadNode(_surge);
        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(8)), "Input script timed out.");

        string output = TerminalMock.GetOutput();

        // Glyphs: the four direction characters, U+2190..U+2193.
        // Pending (not yet reached): DarkCyan.
        Assert.IsTrue(output.Contains(DarkCyan('↑')), "Pending Up glyph should render DarkCyan.");
        Assert.IsTrue(output.Contains(DarkCyan('→')), "Pending Right glyph should render DarkCyan.");
        Assert.IsTrue(output.Contains(DarkCyan('↓')), "Pending Down glyph should render DarkCyan.");

        // Current (owed right now): White, for every glyph in turn.
        Assert.IsTrue(output.Contains(White('←')), "Left glyph should render White while it is the current one.");
        Assert.IsTrue(output.Contains(White('↑')), "Up glyph should render White while it is the current one.");
        Assert.IsTrue(output.Contains(White('→')), "Right glyph should render White while it is the current one.");
        Assert.IsTrue(output.Contains(White('↓')), "Down glyph should render White while it is the current one.");

        // Consumed: DarkGray, for every glyph once it has been played.
        Assert.IsTrue(output.Contains(DarkGray('←')), "Left glyph should render DarkGray once consumed.");
        Assert.IsTrue(output.Contains(DarkGray('↑')), "Up glyph should render DarkGray once consumed.");
        Assert.IsTrue(output.Contains(DarkGray('→')), "Right glyph should render DarkGray once consumed.");
        Assert.IsTrue(output.Contains(DarkGray('↓')), "Down glyph should render DarkGray once consumed.");

        // Rage bar: red label, red block fill, dark grey dash emptiness.
        Assert.IsTrue(output.Contains("[Red]RAGE  [/Red]"), "Rage label should render Red.");
        Assert.IsTrue(output.Contains($"[Red]{new string('█', 10)}[/Red]"), "Filled rage should render as solid Red blocks.");
        Assert.IsTrue(output.Contains($"[DarkGray]{new string('-', 10)}[/DarkGray]"), "Empty rage should render as DarkGray dashes.");

        // Success route: advances via childid to the dummy node.
        Assert.IsTrue(output.Contains("Test StoryNode node"), "Winning the row should advance to the node's childid.");
    }

    [TestMethod]
    public void HighDrainRate_WithNoInput_ProducesADeterministicLossAndTheFailureMessage()
    {
        CreateNode<StoryNode>(nodeId: 2); // dummy target, created first - see the render test above for why

        _surge = CreateNode<SurgeNode>(configure: node =>
        {
            node.Challenge = new SurgeChallenge
            {
                Sequence = "L U R D",
                DrainRate = 400, // empties a full 100 rage bar in 0.25s of real time - no ambiguity
                StartingRage = 100,
                MaxRage = 100,
                FailureMessage = "The rage gutters out before it can break anything.",
            };
            node.ChildId = 2;
        });

        Task script = Task.Run(async () =>
        {
            int idx = await WaitForOutputIndexAsync("Press a key to continue...", 0); // "Let it out!" prompt
            SimulateUserInput(ConsoleKey.Enter);

            // Deliberately no arrow keys at all: the row must be lost purely to the drain.
            await WaitForOutputIndexAsync("The rage gutters out before it can break anything.", idx);

            // What follows is the Surge's own post-failure WaitForKey. FailureChildId is
            // unset on this Challenge, so a real Surge would retry in place - but here we
            // leave the queue empty on purpose and let LoadNode()'s wrapper catch the
            // resulting timeout, exactly like FightNodeTests does for its own trailing prompts.
        });

        LoadNode(_surge);
        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(8)), "Input script timed out.");

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("The rage gutters out before it can break anything."),
            "Failure message should show when the rage bar empties with no input at all.");
    }

    [TestMethod]
    public void DuetPattern_RendersTheMarkedPendingGlyphInSaberinnesGreen()
    {
        CreateNode<StoryNode>(nodeId: 2); // dummy target, created first - see the render test above for why

        _surge = CreateNode<SurgeNode>(configure: node =>
        {
            node.Challenge = new SurgeChallenge
            {
                Sequence = "L U", // two glyphs: Kriss's (index 0) and Saberinne's (index 1)
                DrainRate = 0,
                StartingRage = 100,
                MaxRage = 100,
                DuetPattern = ".S",
            };
            node.ChildId = 2;
        });

        Task script = Task.Run(async () =>
        {
            int idx = await WaitForOutputIndexAsync("Press a key to continue...", 0);
            SimulateUserInput(ConsoleKey.Enter);

            // Up is Saberinne's now and resolves itself on the beat; pressing it anyway is
            // harmless since it lands on her glyph and is swallowed rather than consumed.
            idx = await WaitForOutputIndexAsync("RAGE", idx);
            SimulateUserInput(ConsoleKey.LeftArrow, ConsoleKey.UpArrow);
        });

        LoadNode(_surge);
        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(8)), "Input script timed out.");

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains(Green('↑')),
            "The duet-marked pending glyph (Up, index 1, pattern \".S\") should render in Saberinne's green while pending.");
    }

    [TestMethod]
    public void DuetPattern_HerShareResolvesOnItsOwn_PlayerNeedOnlyPressHisGlyphs()
    {
        CreateNode<StoryNode>(nodeId: 2); // dummy target, created first - see the render test above for why

        _surge = CreateNode<SurgeNode>(configure: node =>
        {
            node.Challenge = new SurgeChallenge
            {
                Sequence = "L U", // Kriss's Left, then Saberinne's Up
                DrainRate = 0,
                StartingRage = 100,
                MaxRage = 100,
                DuetPattern = ".S",
                SuccessMessage = "The door gives.",
            };
            node.ChildId = 2;
        });

        Task script = Task.Run(async () =>
        {
            int idx = await WaitForOutputIndexAsync("Press a key to continue...", 0);
            SimulateUserInput(ConsoleKey.Enter);

            idx = await WaitForOutputIndexAsync("RAGE", idx);
            SimulateUserInput(ConsoleKey.LeftArrow); // only his glyph; her Up resolves after the beat with no key at all

            idx = await WaitForOutputIndexAsync("The door gives.", idx);
            idx = await WaitForOutputIndexAsync("Press a key to continue...", idx);
            SimulateUserInput(ConsoleKey.Enter);
        });

        LoadNode(_surge);
        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(8)), "Input script timed out.");

        Assert.IsTrue(TerminalMock.GetOutput().Contains("Test StoryNode node"),
            "Winning a duet row by pressing only Kriss's glyph should still advance to childid.");
    }

    [TestMethod]
    public void Tutorial_FiresOnFirstInstance_ChapterAndNodeMatchTutorialConstants()
    {
        TestRunner.TestChapter = new Chapter { Id = SurgeNode.TutorialChapterId, Title = "Test Chapter", Nodes = [] };

        CreateNode<StoryNode>(nodeId: 999); // dummy target, created first - GameEngine.CurrentNode must end up on _surge

        _surge = CreateNode<SurgeNode>(nodeId: SurgeNode.TutorialNodeId, configure: node =>
        {
            node.Challenge = new SurgeChallenge { Sequence = "L", DrainRate = 0, StartingRage = 100, MaxRage = 100 };
            node.ChildId = 999;
        });

        Task script = Task.Run(async () =>
        {
            int idx = await WaitForOutputIndexAsync("Follow the sequence with the arrow keys", 0);
            idx = await WaitForOutputIndexAsync("Press a key to continue...", idx);
            SimulateUserInput(ConsoleKey.Enter);

            idx = await WaitForOutputIndexAsync("RAGE", idx);
            SimulateUserInput(ConsoleKey.LeftArrow); // completes the (length-1) row so the loop can end
        });

        LoadNode(_surge);
        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(8)), "Input script timed out.");

        Assert.IsTrue(TerminalMock.GetOutput().Contains("Follow the sequence with the arrow keys"),
            "The tutorial should fire on Surge.TutorialChapterId / Surge.TutorialNodeId.");
    }

    [TestMethod]
    public void Tutorial_DoesNotFire_OutsideItsFirstInstance()
    {
        // The default test chapter id from NodeTestRunner is neither Surge.TutorialChapterId
        // nor Surge.TutorialNodeId, so this is "any other Surge" as far as the gate is concerned.
        Assert.AreNotEqual(SurgeNode.TutorialChapterId, TestRunner.TestChapter.Id);

        CreateNode<StoryNode>(nodeId: 2); // dummy target, created first - see the render test above for why

        _surge = CreateNode<SurgeNode>(nodeId: 1, configure: node =>
        {
            node.Challenge = new SurgeChallenge { Sequence = "L", DrainRate = 0, StartingRage = 100, MaxRage = 100 };
            node.ChildId = 2;
        });

        Task script = Task.Run(async () =>
        {
            int idx = await WaitForOutputIndexAsync("Press a key to continue...", 0);
            SimulateUserInput(ConsoleKey.Enter);

            idx = await WaitForOutputIndexAsync("RAGE", idx);
            SimulateUserInput(ConsoleKey.LeftArrow);
        });

        LoadNode(_surge);
        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(8)), "Input script timed out.");

        Assert.IsFalse(TerminalMock.GetOutput().Contains("Follow the sequence with the arrow keys"),
            "The tutorial must not fire outside its first instance.");
    }
}
