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
/// Drives c18 ("THE SOLAR SHIP") through a real <see cref="GameEngine"/>/<see cref="TerminalMock"/>
/// pair, following the same pattern as <see cref="Chapter16WalkthroughTests"/> for Dialogue/Choice
/// and <see cref="Chapter14And15WalkthroughTests"/> for the Action node (node 2's console puzzle,
/// driven the same way node 17's table-talk Action is there: <c>ActAsync</c> waits on the "\>"
/// prompt and types a command).
///
/// Node 19 is islast with no childid, so <see cref="KrissJourney.Kriss.Nodes.NodeBase.AdvanceToNext"/>
/// calls <see cref="GameEngine.StartNextChapter"/> unconditionally once its own "press a key" prompt
/// is answered. Since this engine's chapter list has been reflectively narrowed to just chapter 18,
/// that surfaces as a clean, deterministic ArgumentNullException from StartChapter ("Chapter with ID
/// 19 not found.") - proof the walk reached node 19 and nothing past it.
/// </summary>
[TestClass]
public class Chapter18WalkthroughTests
{
    const string ContinuePrompt = "Press a key to continue...";
    const string ActionPrompt = "\\>";

    TerminalMock terminal;

    [TestInitialize]
    public void TestInitialize()
    {
        CommandLineOptions.IsDebug = true; // strips Typist's flow/pause delays
        terminal = new TerminalMock();
        TerminalFacade.SetTestTerminal(terminal);
    }

    /// <summary>
    /// Every node in the real chapter 18 JSON deserializes, every childid (node-level, Choice,
    /// Action/ActionObject, and Dialogue reply/childid) resolves to a node that actually exists in
    /// the chapter, every Dialogue reply's nextline resolves to a linename that exists in the same
    /// node, and node ids are unique. Cheapest possible check, and the one most likely to catch a
    /// typo before a full walkthrough would.
    /// </summary>
    [TestMethod]
    public void Chapter18_EveryChildIdAndNextLineResolves_AndIdsAreUnique()
    {
        GameEngine engine = BuildScopedEngine(18);
        Chapter chapter = GetChapter(engine, 18);

        List<int> ids = [.. chapter.Nodes.Select(n => n.Id)];
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "Node ids should be unique within c18.");

        HashSet<int> idSet = [.. ids];

        foreach (KrissJourney.Kriss.Nodes.NodeBase node in chapter.Nodes)
        {
            if (node.ChildId != 0)
                Assert.IsTrue(idSet.Contains(node.ChildId), $"Node {node.Id}'s own childid {node.ChildId} does not resolve.");

            switch (node)
            {
                case KrissJourney.Kriss.Nodes.ChoiceNode choiceNode:
                    foreach (Choice choice in choiceNode.Choices)
                        Assert.IsTrue(idSet.Contains(choice.ChildId), $"Node {node.Id}'s choice '{choice.Desc}' childid {choice.ChildId} does not resolve.");
                    break;

                case KrissJourney.Kriss.Nodes.ActionNode actionNode:
                    foreach (KrissJourney.Kriss.Models.Action action in actionNode.Actions)
                    {
                        if (action.ChildId.HasValue)
                            Assert.IsTrue(idSet.Contains(action.ChildId.Value), $"Node {node.Id}'s action childid {action.ChildId} does not resolve.");

                        foreach (ActionObject obj in action.Objects)
                            if (obj.ChildId.HasValue)
                                Assert.IsTrue(idSet.Contains(obj.ChildId.Value), $"Node {node.Id}'s action-object childid {obj.ChildId} does not resolve.");
                    }
                    break;

                case KrissJourney.Kriss.Nodes.DialogueNode dialogueNode:
                    List<string> lineNames = [.. dialogueNode.Dialogues.Where(d => d.LineName != null).Select(d => d.LineName)];

                    foreach (DialogueLine line in dialogueNode.Dialogues)
                    {
                        if (line.ChildId.HasValue)
                            Assert.IsTrue(idSet.Contains(line.ChildId.Value), $"Node {node.Id}'s dialogue childid {line.ChildId} does not resolve.");

                        if (line.Replies == null)
                            continue;

                        foreach (Reply reply in line.Replies)
                        {
                            if (reply.ChildId.HasValue)
                                Assert.IsTrue(idSet.Contains(reply.ChildId.Value), $"Node {node.Id}'s reply '{reply.Line}' childid {reply.ChildId} does not resolve.");

                            if (!string.IsNullOrEmpty(reply.NextLine))
                                Assert.IsTrue(lineNames.Contains(reply.NextLine), $"Node {node.Id}'s reply '{reply.Line}' nextline '{reply.NextLine}' does not resolve to a linename in the same node.");
                        }
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Walks the whole chapter: Theo's console puzzle (node 2, examining the sails, the helm and
    /// the observation screens before entering the route succeeds), the ocean-meditation and
    /// homesickness dialogues (nodes 5-6, taking the "share"/"voice" reply branch), the evening
    /// cabin scene's own reply (node 7), the storm preparation choices (nodes 10 and 13, taking the
    /// medbay and rigging branches), and the underwater choice (node 18, the correct "go still"
    /// branch taken first time), ending at node 19's closing beat. The wrong branch is walked by
    /// <see cref="Chapter18Node18_KickingHard_CostsTheAirAndHandsTheChoiceBack"/>.
    /// </summary>
    [TestMethod]
    public void Chapter18_WalksFromNode1ThroughNode19_SolvesConsoleAndSurvivesTheStorm()
    {
        GameEngine engine = BuildScopedEngine(18);
        SetCurrentChapter(engine, 18);
        TestStatusManager statusManager = GetStatusManager(engine);

        ArgumentNullException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            // Node 1 is a single Dialogue node with no breaks or replies until Kriss's own childid
            // line at the very end, so the engine renders every line back to back with no
            // intermediate "press a key" prompt - only one continue for the whole node.
            idx = await ContinueAsync(idx);                                              // node 1: Kriss's childid line -> node 2 (Action)

            idx = await ActAsync(idx, "examine sails");                                  // node 2: grants learnedSolarGlyph
            idx = await ActAsync(idx, "examine helm");                                   // node 2: grants learnedHelmBearing
            idx = await ActAsync(idx, "enter route");                                    // node 2: condition not yet met -> harmless refusal
            idx = await ActAsync(idx, "examine screens");                                // node 2: grants learnedSeabedFix
            idx = await ActAsync(idx, "enter route");                                    // node 2: condition met -> success text (has childid+answer)
            idx = await ContinueAsync(idx);                                              // node 2: success text's own "press a key" -> node 3

            idx = await ContinueAsync(idx);                                              // node 3 -> node 4
            idx = await ContinueAsync(idx);                                              // node 4 -> node 5

            idx = await ChooseAsync(idx, "Ask her to share it with you.");               // node 5: reply -> linename "share"
            idx = await ContinueAsync(idx);                                              // node 5: "share" line's childid -> node 6

            idx = await ChooseAsync(idx, "Admit you would rather just go home.");        // node 6: reply -> linename "voice"
            idx = await ContinueAsync(idx);                                              // node 6: Efeliah's childid line -> node 7

            idx = await ContinueAsync(idx);                                              // node 7: Corolla's "stop torturing the dwarf" break
            idx = await ChooseAsync(idx, "Tell her you didn't expect it either.");        // node 7: reply -> linename "keepgoing"
            idx = await ContinueAsync(idx);                                              // node 7: Corolla's childid line -> node 8

            idx = await ContinueAsync(idx);                                              // node 8 -> node 9
            idx = await ContinueAsync(idx);                                              // node 9 -> node 10 (Choice)

            idx = await ChooseAsync(idx, "Get to the medical bay, and lash down the medicines before they shatter.");
                                                                                           // node 10 -> node 11, grants securedMedbay
            idx = await ContinueAsync(idx);                                              // node 11 -> node 13 (Choice)

            idx = await ChooseAsync(idx, "Stay topside, and help Theo finish the rigging instead.", ConsoleKey.DownArrow);
                                                                                           // node 13 -> node 15; grants nothing, and the
                                                                                           // absence of corollaSecuredBelow IS the record
            idx = await ContinueAsync(idx);                                              // node 15 -> node 16

            idx = await ContinueAsync(idx);                                              // node 16 -> node 17
            idx = await ContinueAsync(idx);                                              // node 17 -> node 18 (Choice)

            idx = await ChooseAsync(idx, "Go still, and let your body find its own buoyancy.");
                                                                                           // node 18 -> node 19

            idx = await ContinueAsync(idx);                                              // node 19 -> StartNextChapter(19), out of scope
        });

        try
        {
            engine.LoadNode(1);
            Assert.Fail("Expected the walk to end trying to start chapter 19, which this scoped engine does not have loaded.");
        }
        catch (ArgumentNullException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(30)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual("Chapter with ID 19 not found.", stoppedAt.ParamName,
            "The walk should stop only because chapter 19 is outside this test's scoped chapter list, not for any earlier reason.");
        Assert.AreEqual(0, terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");

        string output = terminal.GetOutput();
        Assert.IsTrue(output.Contains("CHAPTER 18"), "Should have rendered c18's own header at node 1.");
        Assert.IsTrue(output.Contains("You had it."), "Should have solved the console puzzle at node 2.");
        Assert.IsTrue(output.Contains("I just want to go home"), "Should have reached Kriss's homesickness reveal (node 6).");
        Assert.IsTrue(output.Contains("We keep going."), "Should have reached node 7's evening scene via the new reply.");
        Assert.IsTrue(output.Contains("Good"), "Should have taken the rigging branch at node 13 (node 15's Theo line).");
        Assert.IsTrue(output.Contains("Down."), "Should have flowed all the way to node 19's closing beat.");

        Assert.IsTrue(statusManager.IsItemInInventory("learnedSolarGlyph"), "Examining the sails should have granted learnedSolarGlyph.");
        Assert.IsTrue(statusManager.IsItemInInventory("learnedHelmBearing"), "Examining the helm should have granted learnedHelmBearing.");
        Assert.IsTrue(statusManager.IsItemInInventory("learnedSeabedFix"), "Examining the screens should have granted learnedSeabedFix.");
        Assert.IsTrue(statusManager.IsItemInInventory("securedMedbay"), "Node 10's medbay branch should have granted securedMedbay.");
        Assert.IsFalse(statusManager.IsItemInInventory("securedGalley"), "The galley branch was not taken, so securedGalley should not be granted.");
        Assert.IsFalse(statusManager.IsItemInInventory("corollaSecuredBelow"),
            "The rigging branch was taken instead, and it grants nothing: c19 node 17 reads this pair by "
            + "checking corollaSecuredBelow alone, so the rigging half is recorded by that flag's absence.");
    }

    /// <summary>
    /// The console puzzle must not be skippable: entering the route before all three clues are
    /// found gives a harmless, visible failure (the ship yaws) and leaves the player back at the
    /// action prompt to keep exploring, rather than crashing or silently advancing. This is the
    /// negative-space check for the positive walk above, which only ever calls "enter route" after
    /// all three clues are already in hand.
    /// </summary>
    [TestMethod]
    public void Chapter18Node2_EnteringRouteWithoutAllThreeClues_GivesHarmlessRefusalAndDoesNotAdvance()
    {
        GameEngine engine = BuildScopedEngine(18);
        SetCurrentChapter(engine, 18);
        TestStatusManager statusManager = GetStatusManager(engine);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ActAsync(idx, "examine sails");                                  // grants learnedSolarGlyph only
            idx = await ActAsync(idx, "enter route");                                    // still missing two clues -> refusal, stays at node 2
            await WaitForOutputIndexAsync(ActionPrompt, idx);                            // proves the prompt reappeared instead of advancing
        });

        // Loading node 2 directly (rather than node 1) keeps this check scoped to the puzzle
        // itself; ActionNode.PrepareForAction blocks on ReadKey forever once the script above has
        // finished queuing its keys, which is exactly the state this test wants to inspect.
        Task loadTask = Task.Run(() => engine.LoadNode(2));

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(30)), "Input script timed out.");

        string output = terminal.GetOutput();
        Assert.IsTrue(output.Contains("entirely\nunconvinced") || output.Contains("entirely unconvinced"),
            "An incomplete attempt should give the harmless, visible refusal text (the ship yaws and settles).");
        Assert.IsFalse(output.Contains("You had it."), "An incomplete attempt must not reach the success text.");

        Assert.IsTrue(statusManager.IsItemInInventory("learnedSolarGlyph"));
        Assert.IsFalse(statusManager.IsItemInInventory("learnedHelmBearing"));
        Assert.IsFalse(statusManager.IsItemInInventory("learnedSeabedFix"));
    }

    /// <summary>
    /// Node 18's wrong branch. Issue 21 turned node 181 from a game over that ejected to the menu
    /// into a survivable near-drowning that routes back into node 18, so kicking hard costs the
    /// last of Kriss's air and hands the same choice back rather than ending the run. The second
    /// pass sees node 18's alttext, and "Kick hard" - now isnotrepeatable - is greyed out but
    /// still listed, which is why the recovery needs an UpArrow: ChoiceNode.selectedRow is an
    /// instance field and is still sitting on the row the first pass spent.
    /// </summary>
    [TestMethod]
    public void Chapter18Node18_KickingHard_CostsTheAirAndHandsTheChoiceBack()
    {
        GameEngine engine = BuildScopedEngine(18);
        SetCurrentChapter(engine, 18);

        ArgumentNullException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ChooseAsync(idx, "Kick hard, any direction. Up is probably one of them.", ConsoleKey.DownArrow);
                                                                                           // node 18 -> node 181
            idx = await ContinueAsync(idx);                                              // node 181 -> back to node 18

            idx = await ChooseAsync(idx, "Go still, and let your body find its own buoyancy.", ConsoleKey.UpArrow);
                                                                                           // node 18 (second pass) -> node 19

            idx = await ContinueAsync(idx);                                              // node 19 -> StartNextChapter(19), out of scope
        });

        try
        {
            engine.LoadNode(18);
            Assert.Fail("Expected the walk to end trying to start chapter 19, which this scoped engine does not have loaded.");
        }
        catch (ArgumentNullException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(30)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual("Chapter with ID 19 not found.", stoppedAt.ParamName,
            "The wrong branch must still reach node 19: it costs air, not the run.");
        Assert.AreEqual(0, terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");

        string output = terminal.GetOutput();
        Assert.IsTrue(output.Contains("you have been swimming down."), "Kicking hard should play node 181's mistake.");
        Assert.IsTrue(output.Contains("there is a good deal less of you"),
            "Coming back to node 18 a second time should render its alttext, not the first-visit text.");
        Assert.IsTrue(output.Contains("Down."), "The recovery should still flow all the way to node 19's closing beat.");
    }

    /// <summary>
    /// The structural half of the test above, and the guard against the ejection coming back:
    /// node 181 must carry a childid home and neither islast nor isclosing. With either flag set,
    /// NodeBase.AdvanceToNext takes that branch before LoadNode and the player is thrown out to
    /// the menu (isclosing) or into c19 (islast) for making one wrong guess in the dark.
    /// </summary>
    [TestMethod]
    public void Chapter18Node181_RoutesBackIntoTheChoiceAndIsNotAnEnding()
    {
        Chapter chapter = GetChapter(BuildScopedEngine(18), 18);

        KrissJourney.Kriss.Nodes.NodeBase node181 = chapter.Nodes.Single(n => n.Id == 181);

        Assert.AreEqual(18, node181.ChildId, "The near-drowning must hand the player back to the choice it came from.");
        Assert.IsFalse(node181.IsLast, "Node 181 is not an ending: islast here would skip the player into c19.");
        Assert.IsFalse(node181.IsClosing, "Node 181 is not a game over: isclosing here would eject the player to the menu.");

        KrissJourney.Kriss.Nodes.ChoiceNode node18 = (KrissJourney.Kriss.Nodes.ChoiceNode)chapter.Nodes.Single(n => n.Id == 18);

        Assert.IsFalse(string.IsNullOrWhiteSpace(node18.AltText),
            "Node 18 is now re-entered, so it needs alttext: without it the second pass replays the first-visit prose.");

        Choice kick = node18.Choices.Single(c => c.ChildId == 181);
        Assert.IsTrue(kick.IsNotRepeatable,
            "The wrong branch must be spendable only once, or the player can loop through the same mistake forever.");

        Choice goStill = node18.Choices.Single(c => c.ChildId == 19);
        Assert.IsNull(goStill.Condition, "The way out must never be gated: it is the only route to the chapter's ending.");
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

    static Chapter GetChapter(GameEngine engine, int chapterId)
    {
        FieldInfo chaptersField = typeof(GameEngine).GetField("chapters", BindingFlags.NonPublic | BindingFlags.Instance);
        List<Chapter> chapters = (List<Chapter>)chaptersField.GetValue(engine);
        return chapters.Single(c => c.Id == chapterId);
    }

    /// <summary>
    /// Reflectively grabs the <see cref="TestStatusManager"/> BuildScopedEngine constructed the
    /// engine with, so a script can assert on inventory/visited-node state through the exact
    /// same StatusManager the engine itself reads and writes - not a second, disconnected one.
    /// </summary>
    static TestStatusManager GetStatusManager(GameEngine engine)
    {
        FieldInfo statusManagerField = typeof(GameEngine).GetField("statusManager", BindingFlags.NonPublic | BindingFlags.Instance);
        return (TestStatusManager)statusManagerField.GetValue(engine);
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

    /// <summary>
    /// Waits for an ActionNode's own command prompt (Typist.RenderPrompt's "\>" - like a
    /// Choice/reply prompt, it prints no "press a key" banner before blocking on raw input),
    /// then types the given command followed by Enter.
    /// </summary>
    async Task<int> ActAsync(int startIndex, string command)
    {
        int idx = await WaitForOutputIndexAsync(ActionPrompt, startIndex);
        terminal.EnqueueText(command);
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
