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
/// Drives c14 ("THE PROJECTOR") and c15 ("THE LIBERATION") through a real
/// <see cref="GameEngine"/>/<see cref="TerminalMock"/> pair, the same way
/// <see cref="Terminal.Nodes.SurgeTests"/> drives a single Surge node: the engine's own
/// blocking, recursive node.Load() -> AdvanceToNext -> GameEngine.LoadNode chain runs on the
/// test thread, while a background Task plays the part of the player, waiting for each
/// prompt to actually appear in the mock's output before answering it.
///
/// One production quirk this file leans on deliberately: <see cref="NodeBase.AdvanceToNext"/>
/// calls <see cref="GameEngine.StartNextChapter"/> synchronously for an islast node, and that
/// call runs the WHOLE next chapter before returning - so reaching c14's node 95 does not
/// stop the walk, it hands straight into c15's node 1 in the same call stack. Rather than
/// chase that cascade into c16 (which is out of this issue's scope and, per c16/c18/c20's
/// known ProwessHelper gap, would crash on an unrelated Fight node), every walk here uses a
/// GameEngine whose chapter list has been reflectively narrowed to just the chapter(s) under
/// test. Reaching the far edge of that scope then surfaces as a clean, deterministic
/// ArgumentNullException from StartChapter ("Chapter with ID N not found.") - proof the walk
/// reached the intended islast node and nothing past it.
/// </summary>
[TestClass]
public class Chapter14And15WalkthroughTests
{
    const string ContinuePrompt = "Press a key to continue...";

    TerminalMock terminal;

    [TestInitialize]
    public void TestInitialize()
    {
        CommandLineOptions.IsDebug = true; // strips Typist's flow/pause delays; SurgeNode's real-time drain is unaffected
        terminal = new TerminalMock();
        TerminalFacade.SetTestTerminal(terminal);
    }

    [TestMethod]
    public void Chapter14HappyPath_WalksThroughNode95AndHandsOffIntoChapter15sRiffArrival()
    {
        GameEngine engine = BuildScopedEngine(14, 15);
        SetCurrentChapter(engine, 14);

        InvalidOperationException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                              // node 1

            idx = await ContinueAsync(idx);                                              // node 2: Efeliah's break line
            idx = await ContinueAsync(idx);                                              // node 2: Kriss's childid line -> node 20

            idx = await ContinueAsync(idx);                                              // node 20: "Let it out!"
            idx = await PlaySurgeToVictoryAsync(idx, "LURDLURD");
            idx = await WaitForOutputIndexAsync("The pattern locks into place all at once", idx);
            idx = await ContinueAsync(idx);                                              // node 20 success -> node 3

            idx = await ContinueAsync(idx);                                              // node 3 -> node 4
            idx = await ContinueAsync(idx);                                              // node 4 -> node 40
            idx = await ContinueAsync(idx);                                              // node 40 -> node 5

            idx = await ChooseAsync(idx, "\"What happened to it?\"");                     // node 5: Efeliah's reply prompt
            // "joeExplains" (the reply target) has no break of its own in c14.json - it chains
            // straight through Efeliah's "terminals" line into Math's childid line, whose own
            // transition is the only "press a key" prompt between the reply and node 6.
            idx = await ContinueAsync(idx);                                              // node 5: Math's childid line -> node 6

            idx = await ContinueAsync(idx);                                              // node 6 -> node 7
            idx = await ContinueAsync(idx);                                              // node 7 -> node 8

            idx = await ChooseAsync(idx, "\"Well...\"");                                  // node 8: Efeliah's reply prompt
            idx = await ContinueAsync(idx);                                              // node 8: krissRises childid -> node 90

            idx = await ContinueAsync(idx);                                              // node 90 -> node 900

            idx = await ChooseAsync(idx, "Let the ascent carry you.");                    // node 900 -> 901
            idx = await ContinueAsync(idx);                                              // node 901 -> node 902
            idx = await ChooseAsync(idx, "Look to the Rock.");                            // node 902 -> 9021
            idx = await ContinueAsync(idx);                                              // node 9021 -> node 903
            idx = await ContinueAsync(idx);                                              // node 903 -> node 904
            idx = await ChooseAsync(idx, "Try to reach toward Earth.");                   // node 904 -> 905
            idx = await ContinueAsync(idx);                                              // node 905 -> node 906
            idx = await ContinueAsync(idx);                                              // node 906 -> node 908
            idx = await ContinueAsync(idx);                                              // node 908 -> node 940

            idx = await ChooseAsync(idx, "Follow it, patient.");                          // node 940 -> 941 (correct)
            idx = await ChooseAsync(idx, "Keep moving toward it.");                       // node 941 -> 943 (correct)
            idx = await ChooseAsync(idx, "Go all the way in.");                           // node 943 -> 945 (correct)

            idx = await ContinueAsync(idx);                                              // node 945 -> node 946
            idx = await ContinueAsync(idx);                                              // node 946 -> node 95
            idx = await ContinueAsync(idx);                                              // node 95 -> StartNextChapter(15)

            // ---- c14's islast hands off into c15's node 1, same call stack ----

            idx = await ContinueAsync(idx);                                              // c15 node 1 -> node 2
            idx = await ChooseAsync(idx, "Let the sentence trail off, unfinished.");      // c15 node 2 -> node 28 (choice 0)
            idx = await ContinueAsync(idx);                                              // c15 node 28 -> node 3

            // Node 3's opening line (Riff's greeting) flows immediately, before its first
            // "press a key" prompt; finding it is proof enough the cascade landed on c15's own
            // content. That next prompt is deliberately left unanswered - walking the whole new
            // 31-node chapter is Chapter15Standalone's job below, not this cascade test's.
            await WaitForOutputIndexAsync("Hail to the saviors!", idx);
        });

        try
        {
            engine.LoadNode(1);
            Assert.Fail("Expected node 3's own break prompt to time out the mock's ReadKey once the walk reached c15's node 1 cascade.");
        }
        catch (InvalidOperationException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(30)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual("No keys available in mock terminal", stoppedAt.Message,
            "Expected exception message in testing environment only: System.Console.ReadKey never throws this way.");
        Assert.AreEqual(0, terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");

        string output = terminal.GetOutput();
        Assert.IsTrue(output.Contains("The pattern locks into place all at once"), "Should have won the first Surge attempt (node 20).");
        Assert.IsTrue(output.Contains("I think I know what to do."), "Should have reached c14's resolution beat at node 95.");
        Assert.IsTrue(output.Contains("CHAPTER 15"), "islast on node 95 should have hung off into c15's own header (node 1).");
        Assert.IsTrue(output.Contains("Hail to the saviors!"), "Should have reached c15 node 3 (Riff's arrival).");
    }

    [TestMethod]
    public void Chapter14_Surge20FailsWithNoInput_RoutesThroughNode21ToNode22AndSucceedsAtNode3()
    {
        GameEngine engine = BuildScopedEngine(14);
        SetCurrentChapter(engine, 14);

        InvalidOperationException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                              // node 1
            idx = await ContinueAsync(idx);                                              // node 2: Efeliah's break line
            idx = await ContinueAsync(idx);                                              // node 2: Kriss's childid line -> node 20

            idx = await ContinueAsync(idx);                                              // node 20: "Let it out!"
            // Deliberately no arrow keys: node 20's drainrate (18/s against startingrage 100)
            // guarantees a loss on its own, exactly like SurgeTests' HighDrainRate test.
            idx = await WaitForOutputIndexAsync(
                "The pattern slips through your grip at the last glyph", idx, timeoutMs: 10_000);
            idx = await ContinueAsync(idx);                                              // node 20 failure -> node 21 (failurechildid)

            idx = await ContinueAsync(idx);                                              // node 21 -> node 22

            idx = await ContinueAsync(idx);                                              // node 22: "Let it out!"
            idx = await PlaySurgeToVictoryAsync(idx, "DRULDRUL");
            idx = await WaitForOutputIndexAsync("This time the pattern holds.", idx);
            idx = await ContinueAsync(idx);                                              // node 22 success -> node 3

            // node 3's own trailing "press a key" prompt is deliberately left unanswered:
            // its text having rendered is the proof the walk reached node 3.
            await WaitForOutputIndexAsync("The door does not glow", idx);
        });

        try
        {
            engine.LoadNode(1);
            Assert.Fail("Expected node 3's own unanswered prompt to time out the mock's ReadKey.");
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
        Assert.IsTrue(output.Contains("The pattern slips through your grip at the last glyph"), "Node 20 should have failed.");
        Assert.IsTrue(output.Contains("Somewhere back down the corridor"), "Should have reached node 21 (guards approaching).");
        Assert.IsTrue(output.Contains("This time the pattern holds."), "Node 22 should have succeeded.");
        Assert.IsTrue(output.Contains("The door does not glow"), "Should have converged on node 3, same target as a first-attempt win.");
    }

    [TestMethod]
    public void Chapter14_SearchSoftFail_WrongTurnsLoopBackToHub1AndTheRunStillReachesNode95()
    {
        GameEngine engine = BuildScopedEngine(14);
        SetCurrentChapter(engine, 14);

        ArgumentNullException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                              // node 1
            idx = await ContinueAsync(idx);                                              // node 2: Efeliah's break line
            idx = await ContinueAsync(idx);                                              // node 2: Kriss's childid line -> node 20

            idx = await ContinueAsync(idx);                                              // node 20: "Let it out!"
            idx = await PlaySurgeToVictoryAsync(idx, "LURDLURD");
            idx = await WaitForOutputIndexAsync("The pattern locks into place all at once", idx);
            idx = await ContinueAsync(idx);                                              // node 20 success -> node 3

            idx = await ContinueAsync(idx);                                              // node 3 -> node 4
            idx = await ContinueAsync(idx);                                              // node 4 -> node 40
            idx = await ContinueAsync(idx);                                              // node 40 -> node 5

            idx = await ChooseAsync(idx, "\"What happened to it?\"");
            // "joeExplains" (the reply target) has no break of its own in c14.json - it chains
            // straight through Efeliah's "terminals" line into Math's childid line, whose own
            // transition is the only "press a key" prompt between the reply and node 6.
            idx = await ContinueAsync(idx);                                              // node 5: Math's childid line -> node 6

            idx = await ContinueAsync(idx);                                              // node 6 -> node 7
            idx = await ContinueAsync(idx);                                              // node 7 -> node 8

            idx = await ChooseAsync(idx, "\"Well...\"");
            idx = await ContinueAsync(idx);                                              // node 8: krissRises childid -> node 90

            idx = await ContinueAsync(idx);                                              // node 90 -> node 900

            idx = await ChooseAsync(idx, "Let the ascent carry you.");                    // node 900 -> 901
            idx = await ContinueAsync(idx);                                              // node 901 -> node 902
            idx = await ChooseAsync(idx, "Look to the Rock.");                            // node 902 -> 9021
            idx = await ContinueAsync(idx);                                              // node 9021 -> node 903
            idx = await ContinueAsync(idx);                                              // node 903 -> node 904
            idx = await ChooseAsync(idx, "Try to reach toward Earth.");                   // node 904 -> 905
            idx = await ContinueAsync(idx);                                              // node 905 -> node 906
            idx = await ContinueAsync(idx);                                              // node 906 -> node 908
            idx = await ContinueAsync(idx);                                              // node 908 -> node 940

            // Hub 1, first visit (selectedRow starts at 0): deliberately pick the WRONG turn
            // (index 1, "Turn toward something sharper...") -> node 9401.
            idx = await ChooseAsync(idx, "Follow it, patient.", ConsoleKey.DownArrow);

            // From the wrong turn, push further rather than backing out -> shared deep wander.
            idx = await ChooseAsync(idx, "Keep going anyway.", ConsoleKey.DownArrow);      // node 9401 -> 9491 (index 1)
            idx = await ChooseAsync(idx, "Keep searching, blind.", ConsoleKey.DownArrow);  // node 9491 -> 9490 (index 1)

            idx = await ContinueAsync(idx);                                              // node 9490 (soft pull-out) -> loops back to 940

            // Hub 1, second visit: ChoiceNode.selectedRow is an instance field that is never
            // reset between Load() calls on the same node object, so it is still sitting on
            // index 1 (the wrong turn) from the first visit here. UpArrow moves it back to the
            // correct index 0 ("Follow it, patient.") before confirming with Enter.
            idx = await ChooseAsync(idx, "Follow it, patient.", ConsoleKey.UpArrow);        // node 940 -> 941 (index 0, correct)

            idx = await ChooseAsync(idx, "Keep moving toward it.");                        // node 941 -> 943 (index 0, correct, first visit)
            idx = await ChooseAsync(idx, "Go all the way in.");                            // node 943 -> 945 (index 0, correct, first visit)

            idx = await ContinueAsync(idx);                                              // node 945 -> node 946
            idx = await ContinueAsync(idx);                                              // node 946 -> node 95
            idx = await ContinueAsync(idx);                                              // node 95 -> StartNextChapter(15), out of scope
        });

        try
        {
            engine.LoadNode(1);
            Assert.Fail("Expected the walk to end trying to start chapter 15, which this scoped engine does not have loaded.");
        }
        catch (ArgumentNullException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(30)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual("Chapter with ID 15 not found.", stoppedAt.ParamName,
            "The walk should stop only because chapter 15 is outside this test's scoped chapter list, not for any earlier reason.");
        Assert.AreEqual(0, terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");

        string output = terminal.GetOutput();
        Assert.IsTrue(output.Contains("It is only static"), "Should have taken the wrong turn at hub 1 (node 9401).");
        Assert.IsTrue(output.Contains("you cannot find the thread at all"), "Should have reached the shared deep wander (node 9491).");
        Assert.IsTrue(output.Contains("I'm here."), "Should have reached the soft pull-out (node 9490).");
        Assert.IsTrue(output.Contains("I think I know what to do."), "Re-entering hub 1 and taking the correct path should still finish the chapter.");
    }

    /// <summary>
    /// Walks the whole 31-node chapter issue 8 authored: both flavor choices (nodes 2/4), the
    /// "Friends? For life." handshake and both revelation dialogues' replies (nodes 6, 14, 15),
    /// the first node-8 poll (branch: Math should stay), node 17's gated table-talk Action (ask
    /// math before wait is allowed to advance), node 19's award-ceremony Action (all four items,
    /// then leave), the dawn-departure reply (node 21), and node 24's isNodeVisited-gated
    /// farewell choice - which, since this walk took node 8's "Math should stay" branch (-> node
    /// 9), should show only that choice's callback text. Ends at node 27 (islast).
    /// </summary>
    [TestMethod]
    public void Chapter15Standalone_WalksFromNode1ThroughNode27_CollectsAllFourAwardItems()
    {
        GameEngine engine = BuildScopedEngine(15);
        SetCurrentChapter(engine, 15);
        TestStatusManager statusManager = GetStatusManager(engine);

        ArgumentNullException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                              // node 1 -> node 2

            idx = await ChooseAsync(idx, "Let the sentence trail off, unfinished.");      // node 2 -> node 28 (choice 0)
            idx = await ContinueAsync(idx);                                              // node 28 -> node 3

            idx = await ContinueAsync(idx);                                              // node 3: Riff's "still catching his breath" break
            idx = await ContinueAsync(idx);                                              // node 3: Riff's childid line -> node 4

            idx = await ChooseAsync(idx, "Wait, and let him say what he needs to say.");  // node 4 -> node 30 (choice 0)
            idx = await ContinueAsync(idx);                                              // node 30 -> node 5

            idx = await ContinueAsync(idx);                                              // node 5: Riff's childid line -> node 6

            idx = await ContinueAsync(idx);                                              // node 6: Corolla's "did they hit me" break
            idx = await ContinueAsync(idx);                                              // node 6: Corolla's "that's what counts" break
            idx = await ChooseAsync(idx, "\"Friends. For life.\" You repeat it, unsure what exactly you've just agreed to.");
                                                                                           // node 6: "Friends? For life." reply 0 -> handshakeDone
            idx = await ContinueAsync(idx);                                              // node 6: handshakeDone childid -> node 7

            idx = await ContinueAsync(idx);                                              // node 7: Theo's childid line -> node 8

            idx = await ChooseAsync(idx, "Tell her you think Math should stay, if that's what he wants.");
                                                                                           // node 8 -> node 9 (choice 0)
            idx = await ContinueAsync(idx);                                              // node 9 -> node 12

            idx = await ContinueAsync(idx);                                              // node 12: Math's childid line -> node 13
            idx = await ContinueAsync(idx);                                              // node 13 -> node 14

            idx = await ChooseAsync(idx, "\"Actually… I remember. You didn't notice, but I was right behind you. I was listening.\"");
                                                                                           // node 14: reply 0 on Efeliah's line -> efShocked
            idx = await ContinueAsync(idx);                                              // node 14: Corolla's childid line -> node 15

            idx = await ChooseAsync(idx, "\"Sure...\"");                                  // node 15: reply 0 on Efeliah's Oxengutter line -> efSword
            idx = await ContinueAsync(idx);                                              // node 15: Efeliah's "back in shape now" break
            idx = await ContinueAsync(idx);                                              // node 15: Efeliah's childid line -> node 16

            idx = await ContinueAsync(idx);                                              // node 16 -> node 32
            idx = await ContinueAsync(idx);                                              // node 32 -> node 33
            idx = await ContinueAsync(idx);                                              // node 33 -> node 17 (Action)

            idx = await ActAsync(idx, "ask math");                                       // node 17: grants heardMathMotive
            idx = await ActAsync(idx, "wait");                                           // node 17: condition met -> node 18 (no extra prompt)

            idx = await ContinueAsync(idx);                                              // node 18 -> node 19 (Choice hub)

            // Node 19's hub keeps its selectedRow between visits (it is the same node instance
            // the engine re-enters), so every return needs exactly one DownArrow to step onto
            // the next unplayed choice. The hub also opens up as it goes: each branch below is
            // only listed at all once the one gating it has played out, so this walk takes them
            // in listing order and the exit appears only after the last companion prize.
            idx = await ChooseAsync(idx, "Hold out your hands for the first prize.");     // choice 0 -> node 34, grants lightSphere
            idx = await ContinueAsync(idx);                                              // node 34 -> back to node 19
            idx = await ChooseAsync(idx, "Take the second prize", ConsoleKey.DownArrow); // choice 1 -> node 35, grants daggerReplica
            idx = await ContinueAsync(idx);                                              // node 35 -> back to node 19
            idx = await ChooseAsync(idx, "Watch what they bring Corolla.", ConsoleKey.DownArrow);
            idx = await ContinueAsync(idx);                                              // node 36 -> back to node 19
            idx = await ChooseAsync(idx, "Watch the four men wheeling something out for Theo.", ConsoleKey.DownArrow);
            idx = await ContinueAsync(idx);                                              // node 37: Theo's childid line -> back to node 19
            idx = await ChooseAsync(idx, "Watch what they bring Smiurl.", ConsoleKey.DownArrow);
            idx = await ContinueAsync(idx);                                              // node 38 -> back to node 19
            idx = await ChooseAsync(idx, "Watch what they bring Efeliah and Math.", ConsoleKey.DownArrow);
            idx = await ContinueAsync(idx);                                              // node 39: Math's childid line -> back to node 19
            idx = await ChooseAsync(idx, "Bow to the crowd, and let them lead you off the stage.", ConsoleKey.DownArrow);

            idx = await ContinueAsync(idx);                                              // node 20 -> node 21

            // Node 21 carries no replies: "Sure. At dawn." is a line Kriss SPEAKS, not one the
            // player picks, so all three of its breaks are plain "press a key" prompts. Waiting
            // on the text of a spoken line instead would deadlock - that line only renders once
            // the third break has been answered, and answering it is what this step is for.
            idx = await ContinueAsync(idx);                                              // node 21: the councillor's break
            idx = await ContinueAsync(idx);                                              // node 21: Efeliah's "That's not everything, is it?" break
            idx = await ContinueAsync(idx);                                              // node 21: "You're right, as always!" break
            idx = await ContinueAsync(idx);                                              // node 21: Smiurl's childid line -> node 22

            idx = await ContinueAsync(idx);                                              // node 22 -> node 23

            idx = await ContinueAsync(idx);                                              // node 23 -> node 40

            idx = await ContinueAsync(idx);                                              // node 40: Efeliah's childid line -> node 24

            idx = await ChooseAsync(idx, "something in his glance thanks you for it.");   // node 24: only visible choice (gated on node 9)

            idx = await ContinueAsync(idx);                                              // node 25: Kriss's childid line -> node 26

            idx = await ContinueAsync(idx);                                              // node 26 -> node 27

            idx = await ContinueAsync(idx);                                              // node 27 -> StartNextChapter(16), out of scope
        });

        try
        {
            engine.LoadNode(1);
            Assert.Fail("Expected the walk to end trying to start chapter 16, which this scoped engine does not have loaded.");
        }
        catch (ArgumentNullException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(30)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual("Chapter with ID 16 not found.", stoppedAt.ParamName,
            "The walk should stop only because chapter 16 is outside this test's scoped chapter list, not for any earlier reason.");
        Assert.AreEqual(0, terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");

        string output = terminal.GetOutput();
        Assert.IsTrue(output.Contains("CHAPTER 15"), "Should have rendered c15's own header at node 1.");
        Assert.IsTrue(output.Contains("Hail to the saviors!"), "Should have reached node 3 (Riff's arrival).");
        Assert.IsTrue(output.Contains("So THIS is what you were hiding from me!"), "Should have reached node 7 (Math's announcement).");
        Assert.IsTrue(output.Contains("Ayonn is changed."), "Should have reached node 22 (the atrium gathering).");
        Assert.IsTrue(output.Contains("something in his glance thanks you for it."),
            "Node 24 should show the callback tied to node 9 (the branch this walk took at node 8).");
        Assert.IsFalse(output.Contains("doesn't seem to hold it against you."),
            "Node 24's node-10 callback should not be visible: this walk never visited node 10.");
        Assert.IsFalse(output.Contains("seems to understand it."),
            "Node 24's node-11 callback should not be visible: this walk never visited node 11.");
        Assert.IsTrue(output.Contains("Ahead: the road, and whatever comes next."), "Should have flowed all the way to node 27's closing beat.");

        Assert.IsTrue(statusManager.IsItemInInventory("heardMathMotive"), "Node 17's 'ask math' should have granted heardMathMotive.");
        Assert.IsTrue(statusManager.IsItemInInventory("lightSphere"), "Node 19's first prize should have granted lightSphere.");
        Assert.IsTrue(statusManager.IsItemInInventory("daggerReplica"), "Node 19's second prize should have granted daggerReplica.");
        Assert.IsFalse(statusManager.IsItemInInventory("laserRifle"), "Corolla's rifle is hers; watching her receive it grants Kriss nothing.");
        Assert.IsFalse(statusManager.IsItemInInventory("amplifier"), "The amplifier is Math's; watching him receive it grants Kriss nothing.");
    }

    /// <summary>
    /// Node 24's gating is the payoff of the node-8 poll: proving a SECOND branch shows a
    /// DIFFERENT callback would otherwise mean re-running the whole 31-node walk above just to
    /// change one earlier choice. Since GameEngine.LoadNode can jump straight to any node id in
    /// the current chapter, and the gate itself only reads VisitedNodes through the real
    /// TestStatusManager (the same one AllChapters... /ChoiceNode.DisplayChoices reads in
    /// production), pre-seeding node 10 as visited and loading node 24 directly exercises the
    /// identical isNodeVisited gate far more cheaply, without needing a second full walk.
    /// </summary>
    [TestMethod]
    public void Chapter15Node24_WithNodeTenVisitedInstead_ShowsTheNodeTenCallback()
    {
        GameEngine engine = BuildScopedEngine(15);
        SetCurrentChapter(engine, 15);
        TestStatusManager statusManager = GetStatusManager(engine);
        statusManager.SaveProgress(15, 10); // stand-in for having taken node 8's "doubt" branch

        ArgumentNullException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ChooseAsync(idx, "doesn't seem to hold it against you.");         // node 24's only visible choice (gated on node 10)

            idx = await ContinueAsync(idx);                                              // node 25: Kriss's childid line -> node 26

            idx = await ContinueAsync(idx);                                              // node 26 -> node 27

            idx = await ContinueAsync(idx);                                              // node 27 -> StartNextChapter(16), out of scope
        });

        try
        {
            engine.LoadNode(24);
            Assert.Fail("Expected the walk to end trying to start chapter 16, which this scoped engine does not have loaded.");
        }
        catch (ArgumentNullException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(30)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual("Chapter with ID 16 not found.", stoppedAt.ParamName,
            "The walk should stop only because chapter 16 is outside this test's scoped chapter list, not for any earlier reason.");
        Assert.AreEqual(0, terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");

        string output = terminal.GetOutput();
        Assert.IsTrue(output.Contains("doesn't seem to hold it against you."),
            "Node 24 should show the callback tied to node 10 (the node pre-seeded as visited).");
        Assert.IsFalse(output.Contains("something in his glance thanks you for it."),
            "Node 24's node-9 callback should not be visible: node 9 was never visited.");
        Assert.IsFalse(output.Contains("seems to understand it."),
            "Node 24's node-11 callback should not be visible: node 11 was never visited.");
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
    /// <see cref="Typist.WaitForKey"/>, prints no "press a key" banner before its raw
    /// ReadKey), then plays the given navigation keys followed by Enter.
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
    /// Plays a whole authored Surge row in one go. Waits for "RAGE" (the first drawn frame,
    /// which only appears after SurgeNode.PlayRow has discarded any stale queued keys and
    /// started its Stopwatch) before queuing anything, exactly as SurgeTests does for its
    /// first key - see PlayRow's own comment on why keys sent before that would be dropped.
    /// Queuing the whole correct row at once (rather than one key per glyph, waiting on the
    /// caret between each) is still "ahead of the drain": SurgeState.ApplyInput drains its
    /// input queue every ~10ms tick, and every queued key here is correct, so the row
    /// resolves within a tick or two regardless of exactly when this task is scheduled.
    /// </summary>
    async Task<int> PlaySurgeToVictoryAsync(int startIndex, string sequence)
    {
        int idx = await WaitForOutputIndexAsync("RAGE", startIndex);

        foreach (char c in sequence)
            terminal.EnqueueKeys(DirectionKey(c));

        return idx;
    }

    const string ActionPrompt = "\\>";

    /// <summary>
    /// Waits for an ActionNode's own command prompt (Typist.RenderPrompt's "\>" - like a
    /// Choice/reply prompt, it prints no "press a key" banner before blocking on raw input),
    /// then types the given command followed by Enter, the same text-parser interaction
    /// ActionNodeTests drives through SimulateTextInput/NodeTestBase, but here across the real
    /// engine/TerminalMock pair.
    /// </summary>
    async Task<int> ActAsync(int startIndex, string command)
    {
        int idx = await WaitForOutputIndexAsync(ActionPrompt, startIndex);
        terminal.EnqueueText(command);
        terminal.EnqueueKeys(ConsoleKey.Enter);
        return idx;
    }

    static ConsoleKey DirectionKey(char c) => char.ToUpperInvariant(c) switch
    {
        'L' => ConsoleKey.LeftArrow,
        'U' => ConsoleKey.UpArrow,
        'R' => ConsoleKey.RightArrow,
        'D' => ConsoleKey.DownArrow,
        _ => throw new ArgumentException($"'{c}' is not a Surge direction letter (L/U/R/D)."),
    };

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
