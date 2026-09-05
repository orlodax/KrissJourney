using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KrissJourney.Kriss;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Integration;

/// <summary>
/// Drives c24 ("HOME"), the game's final chapter, through a real <see cref="GameEngine"/>/
/// <see cref="TerminalMock"/> pair, following the same pattern as
/// <see cref="Chapter16WalkthroughTests"/>: the engine's own blocking, recursive
/// node.Load() -> AdvanceToNext -> GameEngine.LoadNode chain runs on the test thread, while a
/// background Task plays the part of the player, waiting for each prompt to actually appear
/// in the mock's output before answering it.
///
/// Node 15 is both islast and isclosing: once its final line's own "press a key" prompt is
/// answered, <see cref="NodeBase.AdvanceToNext"/> calls <see cref="GameEngine.DisplayMenu"/>,
/// which - since this test's <see cref="TestStatusManager"/> reports visited nodes - drops into
/// a `do { ... } while (!isValid)` chapter-picker loop reading <see cref="TerminalMock.ReadLine"/>,
/// which always returns an empty string in the mock. That loop never becomes valid and never
/// calls ReadKey again, so it cannot be walked through in a test at all. Every walk below
/// therefore stops one prompt short of the very end: it waits for node 15's closing line to
/// appear in the output and then, deliberately, supplies no further key. The mock's ReadKey
/// blocks for a second and then throws - proof the walk reached the intended final line and
/// nothing past it, without ever entering the unwalkable menu loop.
/// </summary>
[TestClass]
public class Chapter24WalkthroughTests
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
    /// Walks the "tell her the truth" reply (which converges on the same lie), the "reach for
    /// her" window choice, and the "hold the name back" ending choice (which still says it).
    /// Exercises one option of each of the chapter's three decision points and every node the
    /// happy path touches, in order: 1 -> 2 (dialogue, reply) -> 3 -> 4 (dialogue) -> 5 -> 6
    /// (choice) -> 7 -> 9 -> 10 (dialogue, no pause) -> 11 -> 12 (choice) -> 13 -> 15 (dialogue).
    /// Node 11 ends with its own "press a key" prompt just like any other Story node, separate
    /// from node 12's choice prompt right after it.
    /// </summary>
    [TestMethod]
    public void Chapter24TruthReachHoldPath_WalksToTheClosingLineAndStopsBeforeTheMenu()
    {
        GameEngine engine = BuildScopedEngine(24);
        SetCurrentChapter(engine, 24);

        InvalidOperationException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                                  // node 1 -> node 2

            idx = await ChooseAsync(idx, "Tell her the truth.");                              // node 2: mother's question -> "truth" -> falls through to "spare"
            idx = await ContinueAsync(idx);                                                  // node 2: the break after "Shh. Silence. Rest now." -> node 3

            idx = await ContinueAsync(idx);                                                  // node 3 -> node 4

            idx = await ContinueAsync(idx);                                                  // node 4: the break after "Believe me. It's better this way." -> node 5

            idx = await ContinueAsync(idx);                                                  // node 5 -> node 6

            idx = await ChooseAsync(idx, "Reach for her, one more time.");                    // node 6: window choice -> node 7

            idx = await ContinueAsync(idx);                                                  // node 7 -> node 9 (node 8 is the untaken branch)

            idx = await ContinueAsync(idx);                                                  // node 9 -> node 10 (no pause) -> node 11

            idx = await ContinueAsync(idx);                                                  // node 11 -> node 12

            idx = await ChooseAsync(idx, "Hold the name back.");                              // node 12: ending choice -> node 13

            idx = await ContinueAsync(idx);                                                  // node 13 -> node 15 (dialogue runs straight through to the closing line)

            await WaitForOutputIndexAsync("I have the impression I've seen you somewhere before.", idx);
        });

        try
        {
            engine.LoadNode(1);
            Assert.Fail("Expected node 15's own closing-line prompt to time out the mock's ReadKey, since answering it would fall into GameEngine.DisplayMenu()'s unwalkable ReadLine loop.");
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
        Assert.IsTrue(output.Contains("CHAPTER 24"), "Should have rendered c24's own header at node 1.");
        Assert.IsTrue(output.Contains("your first thought is of Saberinne"), "Should render the restored waking beat.");
        Assert.IsTrue(output.Contains("worse than going suddenly blind and deaf"), "Should render the mutilation beat.");
        Assert.IsTrue(output.Contains("The truth is right there"), "Truth reply should render its own precomment before falling through.");
        Assert.IsTrue(output.Contains("Fine..."), "Should converge on the lie regardless of which reply was picked.");
        Assert.IsTrue(output.Contains("Come on, mom"), "Should render the restored comforting-his-mother beat.");
        Assert.IsTrue(output.Contains("The storm..."), "Should render the interrupted beat.");
        Assert.IsTrue(output.Contains("Shh"), "Should render the restored \"rest now\" beat.");
        Assert.IsTrue(output.Contains("Believe me"), "Should render the dinner argument's closing line.");
        Assert.IsTrue(output.Contains("you dream of her"), "Should render the transition into the dream.");
        Assert.IsTrue(output.Contains("with no trace of intelligent life on it"), "Should render the failed call toward Noi-Hert.");
        Assert.IsTrue(output.Contains("Your mind has gone mute again"), "The Surge never appears here - the mind stays mute both times."); ;
        Assert.IsTrue(output.Contains("The alarm rings earlier"), "Should render the restored morning-routine beat.");
        Assert.IsTrue(output.Contains("carry the scar of this"), "Should render the scarred tree.");
        Assert.IsTrue(output.Contains("come in and greet you warmly"), "Should render the restored classmates-greeting beat.");
        Assert.IsTrue(output.Contains("There's a new student today"), "Should render the classmate's whisper.");
        Assert.IsTrue(output.Contains("Her eyes are green"), "The final scene's green eyes must survive intact.");
        Assert.IsTrue(output.Contains("Saberinne!"), "The final scene's exclamation must survive intact.");
        Assert.IsTrue(output.Contains("my name is Sabrina"), "The final scene's correction must survive intact.");
        Assert.IsTrue(output.Contains("Do you mind if I sit here?"), "Should render the restored empty-seat beat.");
        Assert.IsTrue(output.Contains("I have the impression I've seen you somewhere before"), "The final scene's closing line must survive intact.");
        Assert.IsFalse(output.Contains("You tell yourself it is pointless"), "Should never take the untaken window-choice branch (node 8).");
        Assert.IsFalse(output.Contains("You do not even try to stop it"), "Should never take the untaken ending-choice branch (node 14).");
        Assert.AreEqual(0, terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");
    }

    /// <summary>
    /// The mirror path: "spare her" (which skips the truth precomment entirely but still
    /// converges on the same lie), "tell yourself it's pointless" (which still reaches for her
    /// anyway), and "let it out" (which still says the name). Proves the other option of every
    /// decision point, and that nodes 8/14 - the branches Chapter24TruthReachHoldPath never
    /// takes - render their own distinct text before converging.
    /// </summary>
    [TestMethod]
    public void Chapter24SparePointlessLetItOutPath_TakesTheOtherBranchOfEveryDecision()
    {
        GameEngine engine = BuildScopedEngine(24);
        SetCurrentChapter(engine, 24);

        InvalidOperationException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                                  // node 1 -> node 2

            idx = await ChooseAsync(idx, "Tell her the truth.", ConsoleKey.DownArrow);        // node 2: mother's question -> "spare" directly
            idx = await ContinueAsync(idx);                                                  // node 2: the break -> node 3

            idx = await ContinueAsync(idx);                                                  // node 3 -> node 4
            idx = await ContinueAsync(idx);                                                  // node 4: the break -> node 5
            idx = await ContinueAsync(idx);                                                  // node 5 -> node 6

            idx = await ChooseAsync(idx, "Reach for her, one more time.", ConsoleKey.DownArrow); // node 6: window choice -> node 8

            idx = await ContinueAsync(idx);                                                  // node 8 -> node 9
            idx = await ContinueAsync(idx);                                                  // node 9 -> node 10 (no pause) -> node 11
            idx = await ContinueAsync(idx);                                                  // node 11 -> node 12

            idx = await ChooseAsync(idx, "Hold the name back.", ConsoleKey.DownArrow);        // node 12: ending choice -> node 14

            idx = await ContinueAsync(idx);                                                  // node 14 -> node 15

            await WaitForOutputIndexAsync("I have the impression I've seen you somewhere before.", idx);
        });

        try
        {
            engine.LoadNode(1);
            Assert.Fail("Expected node 15's own closing-line prompt to time out the mock's ReadKey.");
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
        Assert.IsFalse(output.Contains("The truth is right there"), "Sparing her should skip the truth reply's own precomment entirely.");
        Assert.IsTrue(output.Contains("Fine..."), "Should still converge on the same lie.");
        Assert.IsTrue(output.Contains("You tell yourself it is pointless"), "Should take the resist branch this time (node 8).");
        Assert.IsTrue(output.Contains("your thought is already moving before you have finished deciding"), "Node 8's own distinct text should render.");
        Assert.IsTrue(output.Contains("Your mind has gone mute again"), "Should still fail either way.");
        Assert.IsTrue(output.Contains("You do not even try to stop it"), "Should take the let-it-out branch this time (node 14).");
        Assert.IsTrue(output.Contains("Saberinne!"), "Should still say the name either way.");
        Assert.IsTrue(output.Contains("I have the impression I've seen you somewhere before"), "The closing line must still survive.");
        Assert.IsFalse(output.Contains("You throw your thought outward, with an immense"), "Should never take the untaken window-choice branch (node 7).");
        Assert.IsFalse(output.Contains("You try.# You clamp down on it") || output.Contains("You try. You clamp down on it"), "Should never take the untaken ending-choice branch (node 13).");
        Assert.AreEqual(0, terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");
    }

    /// <summary>
    /// Cheapest-first structural check, independent of the terminal walk: every childid (node,
    /// choice, dialogue-line, and reply) resolves to something real in the chapter, every
    /// nextline resolves to a linename declared in the same dialogue node, and every node id is
    /// unique. Most content bugs die here before they'd ever reach a walkthrough test.
    /// </summary>
    [TestMethod]
    public void Chapter24Structure_NodeIdsAreUniqueAndEveryReferenceResolves()
    {
        GameEngine engine = BuildScopedEngine(24);
        Chapter chapter = GetScopedChapters(engine).Single(c => c.Id == 24);

        List<int> ids = [.. chapter.Nodes.Select(n => n.Id)];
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "Node ids must be unique within c24.");

        HashSet<int> idSet = [.. ids];

        foreach (NodeBase node in chapter.Nodes)
        {
            if (node.ChildId != 0)
                Assert.IsTrue(idSet.Contains(node.ChildId), $"Node {node.Id}'s childid {node.ChildId} does not resolve.");

            if (node is ChoiceNode choiceNode)
                foreach (Choice choice in choiceNode.Choices)
                    Assert.IsTrue(idSet.Contains(choice.ChildId), $"Node {node.Id}'s choice childid {choice.ChildId} does not resolve.");

            if (node is DialogueNode dialogueNode)
            {
                HashSet<string> lineNames = [.. dialogueNode.Dialogues.Where(l => l.LineName != null).Select(l => l.LineName)];

                foreach (DialogueLine line in dialogueNode.Dialogues)
                {
                    if (line.ChildId.HasValue)
                        Assert.IsTrue(idSet.Contains(line.ChildId.Value), $"Node {node.Id}'s dialogue line childid {line.ChildId} does not resolve.");

                    if (!string.IsNullOrWhiteSpace(line.NextLine))
                        Assert.IsTrue(lineNames.Contains(line.NextLine), $"Node {node.Id}'s nextline '{line.NextLine}' has no matching linename.");

                    if (line.Replies != null)
                        foreach (Reply reply in line.Replies)
                        {
                            if (reply.ChildId.HasValue)
                                Assert.IsTrue(idSet.Contains(reply.ChildId.Value), $"Node {node.Id}'s reply childid {reply.ChildId} does not resolve.");

                            if (!string.IsNullOrWhiteSpace(reply.NextLine))
                                Assert.IsTrue(lineNames.Contains(reply.NextLine), $"Node {node.Id}'s reply nextline '{reply.NextLine}' has no matching linename.");
                        }
                }
            }
        }

        // node 24-15 is the game's actual ending: last chapter, last node.
        NodeBase closingNode = chapter.Nodes.Single(n => n.IsClosing);
        Assert.AreEqual(15, closingNode.Id, "The closing node should still be the final dialogue node.");
        Assert.IsTrue(closingNode.IsLast, "The closing node must also be islast.");
    }

    // ---- helpers -------------------------------------------------------------------

    /// <summary>
    /// Loads every real chapter through the production embedded-resource pipeline
    /// (<see cref="GameEngine.Run"/>), then reflectively narrows the engine's private
    /// chapter list down to just the requested ids.
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

    static List<Chapter> GetScopedChapters(GameEngine engine)
    {
        FieldInfo chaptersField = typeof(GameEngine).GetField("chapters", BindingFlags.NonPublic | BindingFlags.Instance);
        return (List<Chapter>)chaptersField.GetValue(engine);
    }

    static void SetCurrentChapter(GameEngine engine, int chapterId)
    {
        List<Chapter> chapters = GetScopedChapters(engine);
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
