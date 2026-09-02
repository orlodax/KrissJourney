using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KrissJourney.Kriss;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Integration;

/// <summary>
/// Drives c16 ("THE ROAD SOUTH") through a real <see cref="GameEngine"/>/<see cref="TerminalMock"/>
/// pair, following the same pattern as <see cref="Chapter14And15WalkthroughTests"/>: the engine's own
/// blocking, recursive node.Load() -> AdvanceToNext -> GameEngine.LoadNode chain runs on the test
/// thread, while a background Task plays the part of the player, waiting for each prompt to actually
/// appear in the mock's output before answering it.
///
/// Node 16 is islast with no childid, so <see cref="KrissJourney.Kriss.Nodes.NodeBase.AdvanceToNext"/>
/// calls <see cref="GameEngine.StartNextChapter"/> unconditionally once its own "press a key" prompt is
/// answered. Since this engine's chapter list has been reflectively narrowed to just chapter 16 (c17
/// does not exist as adapted content yet), that surfaces as a clean, deterministic
/// ArgumentNullException from StartChapter ("Chapter with ID 17 not found.") - proof the walk reached
/// node 16 and nothing past it.
/// </summary>
[TestClass]
public class Chapter16WalkthroughTests
{
    const string ContinuePrompt = "Press a key to continue...";

    TerminalMock terminal;

    [TestInitialize]
    public void TestInitialize()
    {
        CommandLineOptions.IsDebug = true; // strips Typist's flow/pause delays
        terminal = new TerminalMock();
        TerminalFacade.SetTestTerminal(terminal);
    }

    /// <summary>
    /// Walks the whole 16-node chapter down the "push" branch of node 5's choice: Kriss presses
    /// Corolla on how she knows the distance to the coast, she explodes ("Leave me alone!"), and the
    /// evening camp opens the whole Øder backstory dialogue chain (nodes 9-11), Corolla's morning
    /// "Don't" exchange (node 13), and Efeliah's puzzlement over the pull south (node 15), ending at
    /// node 16's sighting of the coastal town.
    /// </summary>
    [TestMethod]
    public void Chapter16PushBranch_WalksFromNode1ThroughNode16_CorollaExplodesAndTellsTheOderStory()
    {
        GameEngine engine = BuildScopedEngine(16);
        SetCurrentChapter(engine, 16);

        ArgumentNullException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                              // node 1 -> node 2
            idx = await ContinueAsync(idx);                                              // node 2 -> node 3

            idx = await ContinueAsync(idx);                                              // node 3: Efeliah's "I can hardly argue with that" break
            idx = await ContinueAsync(idx);                                              // node 3: Kriss's childid line -> node 4

            idx = await ContinueAsync(idx);                                              // node 4 -> node 5

            idx = await ChooseAsync(idx, "It's obvious you're hiding something.");        // node 5 -> node 6 (push branch)

            idx = await ContinueAsync(idx);                                              // node 6 -> node 8

            idx = await ContinueAsync(idx);                                              // node 8 -> node 9

            idx = await ChooseAsync(idx, "\"What happened?\"");                           // node 9: reply on "she is a noble" -> linename "attack"
            idx = await ChooseAsync(idx, "\"What changed it?\"");                         // node 9: reply on "attack" -> linename "silence"
            idx = await ContinueAsync(idx);                                              // node 9: Corolla's childid line -> node 10

            idx = await ChooseAsync(idx, "\"And your father?\"");                         // node 10: reply on first line -> linename "father"
            idx = await ContinueAsync(idx);                                              // node 10: "father" line's own break
            idx = await ChooseAsync(idx, "\"Is that why you helped me?");                 // node 10: reply -> linename "helped"
            idx = await ContinueAsync(idx);                                              // node 10: Corolla's childid line -> node 11

            idx = await ChooseAsync(idx, "\"I'm sorry.");                                 // node 11: reply -> linename "forgiven"
            idx = await ContinueAsync(idx);                                              // node 11: Corolla's childid line -> node 12

            idx = await ContinueAsync(idx);                                              // node 12 -> node 13

            idx = await ChooseAsync(idx, "\"Don't what?\"");                              // node 13: reply -> linename "dontwhat"
            idx = await ContinueAsync(idx);                                              // node 13: Corolla's childid line -> node 14

            idx = await ContinueAsync(idx);                                              // node 14 -> node 15

            idx = await ChooseAsync(idx, "\"Neither do I.\"");                            // node 15: reply -> linename "resign"
            idx = await ChooseAsync(idx, "\"I think it has to do with the call.\"");      // node 15: reply -> linename "callAsked"
            idx = await ContinueAsync(idx);                                              // node 15: Kriss's childid line -> node 16

            idx = await ContinueAsync(idx);                                              // node 16 -> StartNextChapter(17), out of scope
        });

        try
        {
            engine.LoadNode(1);
            Assert.Fail("Expected the walk to end trying to start chapter 17, which this scoped engine does not have loaded.");
        }
        catch (ArgumentNullException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(30)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual("Chapter with ID 17 not found.", stoppedAt.ParamName,
            "The walk should stop only because chapter 17 is outside this test's scoped chapter list, not for any earlier reason.");
        Assert.AreEqual(0, terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");

        string output = terminal.GetOutput();
        Assert.IsTrue(output.Contains("CHAPTER 16"), "Should have rendered c16's own header at node 1.");
        Assert.IsTrue(output.Contains("Leave me alone!"), "Push branch should reach Corolla's outburst (node 6).");
        Assert.IsTrue(output.Contains("We helped you because it was right."), "Should have reached Theo's line in node 10's Øder exchange.");
        Assert.IsTrue(output.Contains("part of the planet"), "Should have reached node 14's hilltop beat.");
        Assert.IsTrue(output.Contains("There must be."), "Should have reached node 15's closing line.");
        Assert.IsTrue(output.Contains("Then, at last, dawn comes."), "Should have flowed all the way to node 16's closing beat.");
    }

    /// <summary>
    /// Node 5's other branch: letting it go instead of pushing. Node 7 is a short, distinct beat from
    /// node 6, but both converge on node 8's evening camp and the same Theo-opens-the-story beat at
    /// node 9 - proving the convergence, and that the outburst is unique to the push branch, is enough
    /// without re-walking the whole Øder dialogue chain a second time.
    /// </summary>
    [TestMethod]
    public void Chapter16BackOffBranch_ConvergesAtNode8AndStillReachesTheosOpening()
    {
        GameEngine engine = BuildScopedEngine(16);
        SetCurrentChapter(engine, 16);

        InvalidOperationException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                              // node 1 -> node 2
            idx = await ContinueAsync(idx);                                              // node 2 -> node 3

            idx = await ContinueAsync(idx);                                              // node 3: Efeliah's "I can hardly argue with that" break
            idx = await ContinueAsync(idx);                                              // node 3: Kriss's childid line -> node 4

            idx = await ContinueAsync(idx);                                              // node 4 -> node 5

            idx = await ChooseAsync(idx, "Let it go, and keep walking in silence.", ConsoleKey.DownArrow); // node 5 -> node 7 (back-off branch, choice index 1)

            idx = await ContinueAsync(idx);                                              // node 7 -> node 8

            idx = await ContinueAsync(idx);                                              // node 8 -> node 9

            // Theo's opening line is deliberately left unanswered beyond this: node 9's first reply
            // prompt has not rendered yet, and answering it is Chapter16PushBranch's job, not this
            // convergence check's. Its text having rendered is proof enough the two branches meet.
            await WaitForOutputIndexAsync("It's time we told you our story.", idx);
        });

        try
        {
            engine.LoadNode(1);
            Assert.Fail("Expected node 9's own reply prompt to time out the mock's ReadKey once the walk reached it unanswered.");
        }
        catch (InvalidOperationException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(30)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual("No keys available in mock terminal", stoppedAt.Message,
            "Expected exception message in testing environment only: System.Console.ReadKey never throws this way.");

        string output = terminal.GetOutput();
        Assert.IsTrue(output.Contains("You let the words die before they reach your mouth."), "Should have taken node 7 (the back-off branch).");
        Assert.IsTrue(output.Contains("It's time we told you our story."), "Should still converge on node 9 (Theo opens the story) via node 8.");
        Assert.IsFalse(output.Contains("Leave me alone!"), "The back-off branch should never reach node 6's outburst.");
    }

    // ---- helpers -------------------------------------------------------------------

    /// <summary>
    /// Loads every real chapter through the production embedded-resource pipeline
    /// (<see cref="GameEngine.Run"/>), then reflectively narrows the engine's private
    /// chapter list down to just the requested ids. The chapters themselves are the exact
    /// objects the real deserializer produced - only which ones the engine can see is scoped.
    /// </summary>
    static GameEngine BuildScopedEngine(params int[] chapterIds)
    {
        GameEngine engine = new(new TestStatusManager());
        engine.Run();

        FieldInfo chaptersField = typeof(GameEngine).GetField("chapters", BindingFlags.NonPublic | BindingFlags.Instance);
        List<Chapter> allChapters = (List<Chapter>)chaptersField.GetValue(engine);
        List<Chapter> scoped = [.. chapterIds.Select(id => allChapters.Single(c => c.Id == id))];
        chaptersField.SetValue(engine, scoped);

        return engine;
    }

    static void SetCurrentChapter(GameEngine engine, int chapterId)
    {
        FieldInfo chaptersField = typeof(GameEngine).GetField("chapters", BindingFlags.NonPublic | BindingFlags.Instance);
        List<Chapter> chapters = (List<Chapter>)chaptersField.GetValue(engine);
        Chapter chapter = chapters.Single(c => c.Id == chapterId);

        PropertyInfo currentChapterProperty = typeof(GameEngine).GetProperty(nameof(GameEngine.CurrentChapter));
        currentChapterProperty.SetValue(engine, chapter);
    }

    /// <summary>Waits for the next generic "press a key" prompt and answers it with Enter.</summary>
    async Task<int> ContinueAsync(int startIndex, int timeoutMs = 5000)
    {
        int idx = await WaitForOutputIndexAsync(ContinuePrompt, startIndex, timeoutMs);
        terminal.EnqueueKeys(ConsoleKey.Enter);
        return idx;
    }

    /// <summary>
    /// Waits for a marker unique to a Choice/Dialogue-reply prompt (which, unlike
    /// <see cref="KrissJourney.Kriss.Helpers.Typist.WaitForKey"/>, prints no "press a key" banner
    /// before its raw ReadKey), then plays the given navigation keys followed by Enter.
    /// </summary>
    async Task<int> ChooseAsync(int startIndex, string marker, params ConsoleKey[] navigation)
    {
        int idx = await WaitForOutputIndexAsync(marker, startIndex);
        if (navigation.Length > 0)
            terminal.EnqueueKeys(navigation);
        terminal.EnqueueKeys(ConsoleKey.Enter);
        return idx;
    }

    async Task<int> WaitForOutputIndexAsync(string marker, int startIndex, int timeoutMs = 5000)
    {
        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            string output = terminal.GetOutput();
            int idx = output.IndexOf(marker, startIndex, StringComparison.Ordinal);
            if (idx >= 0)
                return idx + marker.Length;
            await Task.Delay(10);
        }

        throw new TimeoutException(
            $"Marker not found in output within {timeoutMs}ms: '{marker}'.\n---- output so far ----\n{terminal.GetOutput()}");
    }
}
