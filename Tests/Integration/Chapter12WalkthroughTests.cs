using System;
using System.Linq;
using System.Threading.Tasks;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Integration;

/// <summary>
/// Drives c12 ("THE CELLS") through a real <see cref="GameEngine"/>/TerminalMock pair, on the
/// harness described in <see cref="ChapterWalkthroughTestBase"/>. The chapter had no walkthrough
/// of its own until issue 19 rewrote it from 22 nodes and one decision into 30 nodes and ten, so
/// everything here is new coverage rather than a repair.
///
/// Two walks, because the chapter's two hardest pieces cannot be covered by one: node 8's
/// cellblock Action has a required chain (find the panel, pull the lever, only then free anyone),
/// and node 26's free-night hub is five choices where the exit stays hidden until all four
/// vignettes have been played. The first walk takes the chain and the hub the long way round; the
/// second proves the chain cannot be short-circuited.
/// </summary>
[TestClass]
public class Chapter12WalkthroughTests : ChapterWalkthroughTestBase
{
    [TestMethod]
    [Timeout(120000)]
    public void Chapter12_WalksFromNode1ThroughNode24_SolvesTheCellblockAndPlaysEveryFreeNightVignette()
    {
        GameEngine engine = BuildScopedEngine(12);
        SetCurrentChapter(engine, 12);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                          // node 1 -> node 12
            idx = await ContinueAsync(idx);                                          // node 12 -> node 2

            idx = await ContinueAsync(idx);                                          // node 2: Corolla's "when I get my hands free" break
            idx = await ChooseAsync(idx, "\"What happened out there? At the shield?\""); // node 2: reply -> linename "shield"
            idx = await ContinueAsync(idx);                                          // node 2: Kriss's childid line -> node 3

            idx = await ContinueAsync(idx);                                          // node 3 -> node 32

            // Node 32's three replies all converge on "silence", which carries the childid; the
            // "band" one narrates its own beat on the way past.
            idx = await ChooseAsync(idx, "Watch the band on her head instead.", ConsoleKey.DownArrow); // node 32 -> "band"
            idx = await ContinueAsync(idx);                                          // node 32: "silence" childid line -> node 13

            idx = await ContinueAsync(idx);                                          // node 13 -> node 14
            idx = await ContinueAsync(idx);                                          // node 14 -> node 25

            idx = await ChooseAsync(idx, "Ask Math and Efeliah to bring the guard back here.", ConsoleKey.DownArrow); // node 25 -> "lure"
            idx = await ContinueAsync(idx);                                          // node 25: "watch" childid line -> node 4

            idx = await ContinueAsync(idx);                                          // node 4 -> node 6
            idx = await ContinueAsync(idx);                                          // node 6 -> node 15
            idx = await ContinueAsync(idx);                                          // node 15 -> node 16
            idx = await ContinueAsync(idx);                                          // node 16 -> node 17
            idx = await ContinueAsync(idx);                                          // node 17 -> node 7
            idx = await ContinueAsync(idx);                                          // node 7 -> node 18
            idx = await ContinueAsync(idx);                                          // node 18 -> node 19
            idx = await ContinueAsync(idx);                                          // node 19 -> node 8 (Action)

            // The cellblock's required chain. Every step but the last has no childid, so it prints
            // its answer and hands the prompt straight back - ActAsync, never DoActionAsync.
            idx = await ActAsync(idx, "search corridor");                            // grants foundPanel
            idx = await ActAsync(idx, "use panel");                                  // gated on foundPanel -> grants fieldsDown
            idx = await ActAsync(idx, "search alcove");                              // grants medkit
            idx = await ActAsync(idx, "treat theo");                                 // gated on medkit, flavour only
            idx = await ActAsync(idx, "open prisoners");                             // gated on fieldsDown; no answer, so it advances
                                                                                     // straight to node 9 with no pause of its own

            idx = await ChooseAsync(idx, "\"That was mostly him.\"");                // node 9: reply -> linename "respect"
            idx = await ContinueAsync(idx);                                          // node 9: Riff's "My name is Riff" break
            idx = await ContinueAsync(idx);                                          // node 9: Riff's childid line -> node 10

            idx = await ContinueAsync(idx);                                          // node 10: Riff's "ancient machine" break
            idx = await ContinueAsync(idx);                                          // node 10: Math's "The Memories speak of it" break
            idx = await ChooseAsync(idx, "\"Then we finish what they started.\"", ConsoleKey.DownArrow); // node 10: reply -> "door"
            idx = await ContinueAsync(idx);                                          // node 10: Corolla's "About that door" break
            idx = await ContinueAsync(idx);                                          // node 10: Riff's childid line -> node 11

            idx = await ContinueAsync(idx);                                          // node 11 -> node 21
            idx = await ContinueAsync(idx);                                          // node 21 -> node 22
            idx = await ContinueAsync(idx);                                          // node 22 -> node 26 (Choice hub)

            // The hub, four vignettes then the exit. ChoiceNode.selectedRow is an instance field
            // that survives every return trip, so each pick moves down exactly one row from where
            // the previous one left the highlight; the exit only joins the list on the fifth
            // visit, once all four isNodeVisited conditions in its "all" group hold.
            idx = await ChooseAsync(idx, "Sit with Efeliah while she works on Theo's arm."); // node 26 -> node 27
            idx = await ContinueAsync(idx);                                          // node 27 -> back to node 26

            idx = await ChooseAsync(idx, "Watch Smiurl in the corner with the rebel children.", ConsoleKey.DownArrow);
            idx = await ContinueAsync(idx);                                          // node 33 -> back to node 26

            idx = await ChooseAsync(idx, "Go over to the map table, where Corolla and Riff are.", ConsoleKey.DownArrow);
            idx = await ChooseAsync(idx, "\"You are still fighting. That is not nothing.\""); // node 28: reply -> "answer"
            idx = await ContinueAsync(idx);                                          // node 28: Riff's childid line -> back to node 26

            idx = await ChooseAsync(idx, "Look for Math.", ConsoleKey.DownArrow);    // node 26 -> node 29
            idx = await ContinueAsync(idx);                                          // node 29 -> back to node 26

            idx = await ChooseAsync(idx, "Say the thing you have been turning over since the cells.", ConsoleKey.DownArrow);
                                                                                     // node 26 -> node 30, now that all four are visited

            idx = await ContinueAsync(idx);                                          // node 30: Kriss's "surprisingly simple" break
            idx = await ChooseAsync(idx, "\"I know that we will manage it.\"");      // node 30: reply -> "conviction"
            idx = await ContinueAsync(idx);                                          // node 30: Kriss's childid line -> node 31

            idx = await ChooseAsync(idx, "\"I want to see the Projector. I want to know what it is.\""); // node 31: reply -> "answers"
            idx = await ContinueAsync(idx);                                          // node 31: Efeliah's "vague presentiment" break
            idx = await ContinueAsync(idx);                                          // node 31: Riff's childid line -> node 24

            idx = await ContinueAsync(idx);                                          // node 24: islast -> StartNextChapter(13)
        });

        RunWalkToChapterEnd(engine, script, startNodeId: 1, expectedNextChapterId: 13);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("CHAPTER 12"), "Should have rendered c12's own header at node 1.");
        Assert.IsTrue(output.Contains("You went through it."), "Node 2's 'shield' reply branch should have played.");
        Assert.IsTrue(output.Contains("I would want a longer look at that thing"), "Node 32's 'band' reply branch should have played.");
        Assert.IsTrue(output.Contains("a hypnotized mind is not easy to speak to"), "Node 25's 'lure' reply branch should have played.");
        Assert.IsTrue(output.Contains("a control panel mounted beside the sealed door"), "'search corridor' should have found the panel.");
        Assert.IsTrue(output.Contains("The effect is immediate."), "'use panel' should have dropped every barrier.");
        Assert.IsTrue(output.Contains("You missed a spot."), "'treat theo' should have paid off the medkit found in the alcove.");
        Assert.IsTrue(output.Contains("Then he has my respect."), "Node 9's reply should have reached Riff's answer.");
        Assert.IsTrue(output.Contains("A psi-lock."), "Node 10 should have set up the psychic door c13 opens.");
        Assert.IsTrue(output.Contains("you do not interrupt"), "The hub should have played Efeliah's vignette (node 27).");
        Assert.IsTrue(output.Contains("cluster of rebel children"), "The hub should have played Smiurl's vignette (node 33).");
        Assert.IsTrue(output.Contains("Hope.") && output.Contains("I had forgotten I was allowed to want it."),
            "The hub should have played the map-table dialogue and its reply (node 28).");
        Assert.IsTrue(output.Contains("Master's Memories"), "The hub should have played Math's vignette (node 29).");
        Assert.IsTrue(output.Contains("But tonight, just for tonight, you are free."), "Should have flowed all the way to node 24's closing beat.");

        Assert.IsTrue(engine.Evaluate(new Condition { Item = "foundPanel" }), "'search corridor' should have banked foundPanel.");
        Assert.IsTrue(engine.Evaluate(new Condition { Item = "fieldsDown" }), "'use panel' should have banked fieldsDown.");
        Assert.IsTrue(engine.Evaluate(new Condition { Item = "medkit" }), "'search alcove' should have banked the medkit.");
    }

    /// <summary>
    /// The cellblock's negative space. Node 8 is the one place in c12 where a player can strand
    /// themselves, so both of its exits (freeing the prisoners, and walking on down the corridor)
    /// are gated on fieldsDown, and pulling the lever is itself gated on having found the panel.
    /// Each unmet gate must answer with its own refusal and hand the prompt back - not advance,
    /// and not fall through to the generic "I don't know about that".
    /// </summary>
    [TestMethod]
    [Timeout(60000)]
    public void Chapter12Node8_TheCellblockChainCannotBeSkipped()
    {
        GameEngine engine = BuildScopedEngine(12);
        SetCurrentChapter(engine, 12);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ActAsync(idx, "open prisoners");                             // no fieldsDown -> refusal
            idx = await ActAsync(idx, "go corridor");                                // the other exit, refused for the same reason
            idx = await ActAsync(idx, "use panel");                                  // no foundPanel yet -> its own, different refusal
            idx = await ActAsync(idx, "treat theo");                                 // no medkit -> its own refusal

            await WaitForOutputIndexAsync(ActionPrompt, idx);                        // proves the prompt came back a fifth time
        });

        RunWalkToUnansweredPrompt(engine, script, startNodeId: 8);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("The barriers are still active."),
            "Freeing the prisoners before the fields are down must play its own refusal.");
        Assert.IsTrue(output.Contains("I am not leaving these people behind those barriers."),
            "Walking on down the corridor must be refused too, or the cellblock can be abandoned.");
        Assert.IsTrue(output.Contains("I need to find whatever controls these barriers first."),
            "Pulling the lever before finding the panel must play the lever's own refusal.");
        Assert.IsTrue(output.Contains("I would need to find a kit first."),
            "Treating Theo without the medkit must play the kit's refusal.");

        Assert.IsFalse(output.Contains("The effect is immediate."), "No barrier may drop on this walk.");
        Assert.IsFalse(engine.Evaluate(new Condition { Item = "fieldsDown" }), "A refused lever must not bank fieldsDown.");
    }

    /// <summary>
    /// Cheapest-first structural pass over the rewritten chapter: ids unique, every childid and
    /// nextline resolves, and exactly one reachable node ends it. Then the two shapes issue 19
    /// added, which a walkthrough proves work but not that they are built the way they must be:
    /// the free-night hub's exit is gated on all four vignettes at once, and every vignette
    /// returns to it rather than running on.
    /// </summary>
    [TestMethod]
    public void Chapter12Structure_TheFreeNightHubIsClosedUntilAllFourVignettesArePlayed()
    {
        Chapter chapter = GetScopedChapters(BuildScopedEngine(12)).Single();

        Assert.AreEqual(12, chapter.Id, "The Id field inside c12.json must match its filename.");
        ChapterStructureAssertions.AssertReferencesResolve(chapter);
        ChapterStructureAssertions.AssertChapterEndsAt(chapter, endsAtNodeId: 24);

        ChoiceNode hub = (ChoiceNode)chapter.Nodes.Single(n => n.Id == 26);
        Assert.AreEqual(5, hub.Choices.Count, "Four vignettes plus the way out.");

        foreach (Choice vignette in hub.Choices.Take(4))
            Assert.IsNull(vignette.Condition, "Every vignette is open from the start; only leaving is earned.");

        Choice leave = hub.Choices[4];
        Assert.IsNull(leave.Condition.Item, "Leaving is gated on a group, not on any single thing.");
        CollectionAssert.AreEqual(new[] { "27", "33", "28", "29" }, leave.Condition.All.ConvertAll(c => c.Item),
            "The exit must stay out of the list until all four vignettes have been visited.");
        Assert.IsTrue(leave.Condition.All.TrueForAll(c => c.Type == "isNodeVisited"));

        // An unmet isNodeVisited hides its choice outright rather than refusing it, so a refusal
        // line on the exit would be dead text the player can never see.
        Assert.IsTrue(string.IsNullOrEmpty(leave.Refusal),
            "The exit is hidden until it is earned, so it must not also carry a refusal.");

        foreach (int vignetteId in new[] { 27, 33, 29 })
            Assert.AreEqual(26, chapter.Nodes.Single(n => n.Id == vignetteId).ChildId,
                $"Vignette {vignetteId} must return to the hub, or the hub can be left early.");

        DialogueNode mapTable = (DialogueNode)chapter.Nodes.Single(n => n.Id == 28);
        Assert.AreEqual(26, mapTable.Dialogues.Last().ChildId,
            "The map-table vignette is a dialogue, and it has to come back to the hub too.");
    }
}
