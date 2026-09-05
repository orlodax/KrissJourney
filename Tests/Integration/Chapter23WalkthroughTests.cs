using System;
using System.Linq;
using System.Threading.Tasks;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Integration;

/// <summary>
/// Drives c23 ("THE FAREWELL"), substantially rewritten by this batch, on the
/// <see cref="ChapterWalkthroughTestBase"/> harness.
///
/// Its centrepiece is the one duet Surge in the game: node 9 carries duetpattern ".S", so half
/// the row is Saberinne's and resolves on her own beat, and with drain and penalty both at zero
/// it cannot be lost - only stalled. That makes it the one Surge a test cannot play by queuing
/// the whole row at once, since everything pressed on her glyphs is swallowed; see
/// PlayAlternatingDuetSurgeAsync, which presses one glyph per turn off the caret's colour.
/// </summary>
[TestClass]
public class Chapter23WalkthroughTests : ChapterWalkthroughTestBase
{
    const string MergeSuccessMarker = "That enormous power lifts toward the sky";

    [TestMethod]
    [Timeout(300000)]
    public void FullMerge_SaysEveryFarewellPlaysTheDuetAndSweepsTheArmyAway()
    {
        GameEngine engine = BuildScopedEngine(23);
        SetCurrentChapter(engine, 23);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                          // node 1 -> node 2

            idx = await ChooseAsync(idx, "Ask what the two of you would become.");    // node 2 -> "askbecome" -> "entity"
            idx = await ContinueAsync(idx);                                          // node 2: Kriss's "Tomorrow, at dawn" break
            idx = await ContinueAsync(idx);                                          // node 2: Smiurl's childid line -> node 3

            idx = await ChooseAsync(idx, "Answer at once, before the presentiment can land."); // node 3: Theo's farewell
            idx = await ContinueAsync(idx);                                          // node 3 -> node 4

            idx = await ChooseAsync(idx, "Hold her gaze and say nothing.");           // node 4: Corolla's farewell
            idx = await ContinueAsync(idx);                                          // node 4 -> node 5

            idx = await ChooseAsync(idx, "Meet her searching look.");                 // node 5: Efeliah's farewell
            idx = await ContinueAsync(idx);                                          // node 5 -> node 6

            idx = await ChooseAsync(idx, "Hug him back before he lets go.");          // node 6: Smiurl's farewell
            idx = await ContinueAsync(idx);                                          // node 6: "farewellclose" childid line -> node 7

            idx = await ContinueAsync(idx);                                          // node 7: Kriss's opening break
            idx = await ChooseAsync(idx, "Tell her she could not keep it from you for long."); // node 7 -> "presssecret" -> "tomorrow"
            idx = await ContinueAsync(idx);                                          // node 7: Saberinne's childid line -> node 8

            idx = await ContinueAsync(idx);                                          // node 8 -> node 9

            idx = await ContinueAsync(idx);                                          // node 9: "Let it out!"
            idx = await PlayAlternatingDuetSurgeAsync(idx, glyphCount: 20);
            idx = await WaitForOutputIndexAsync(MergeSuccessMarker, idx, timeoutMs: 30000);
            idx = await ContinueAsync(idx);                                          // node 9: success message -> node 10

            // The army sequence is two real decisions with Story nodes between them, not five
            // hollow ones: 11, 12 and 14 stopped being Choice nodes when this batch collapsed them.
            idx = await ChooseAsync(idx, "Keep hold of them singly, and know what each one feels."); // node 10 -> node 101
            idx = await ContinueAsync(idx);                                          // node 101 -> node 11
            idx = await ContinueAsync(idx);                                          // node 11 -> node 12
            idx = await ContinueAsync(idx);                                          // node 12 -> node 13

            idx = await ChooseAsync(idx, "Hold your attention on them while it happens."); // node 13 -> node 131
            idx = await ContinueAsync(idx);                                          // node 131 -> node 14
            idx = await ContinueAsync(idx);                                          // node 14 -> node 15
            idx = await ContinueAsync(idx);                                          // node 15 -> node 16

            idx = await ChooseAsync(idx, "Go looking in her mind for the reason.");   // node 16 -> "whyreach" -> "risksrun"
            idx = await ContinueAsync(idx);                                          // node 16: islast final line -> StartNextChapter(24)
        });

        RunWalkToChapterEnd(engine, script, startNodeId: 1, expectedNextChapterId: 24, scriptTimeoutSeconds: 240);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("CHAPTER 23"), "Should have rendered c23's own header at node 1.");
        Assert.IsTrue(output.Contains("It would keep what makes each of us who we are"),
            "Node 2's two replies converge on 'entity', so this proves the convergence, NOT which reply was taken.");
        Assert.IsTrue(output.Contains("and find that you do not much care that they hear you ask it."),
            "The askbecome branch's own line is the only thing that tells node 2's two replies apart.");
        Assert.IsTrue(output.Contains("This is my farewell."), "Theo's farewell must land.");
        Assert.IsTrue(output.Contains("the way a hand comes back from something hot."), "Should take Theo's brush-it-off branch.");
        Assert.IsTrue(output.Contains("She smiles at that, and does not deny it,"), "Should press Saberinne about the secret at node 7.");
        Assert.IsTrue(output.Contains("the composure cracks completely"), "Should take Corolla's held-gaze branch.");
        Assert.IsTrue(output.Contains("until she is the one who glances away"), "Should take Efeliah's met-look branch.");
        Assert.IsTrue(output.Contains("You catch him properly this time"), "Should take Smiurl's returned-hug branch.");
        Assert.IsTrue(output.Contains("Neither will I."), "Both Smiurl branches converge on the same closing line.");
        Assert.IsTrue(output.Contains("Time will bring you answers"), "Node 7's \"tomorrow\" line closes the last night.");
        Assert.IsTrue(output.Contains(MergeSuccessMarker), "The duet Surge at node 9 should have been played out to its end.");
        Assert.IsTrue(output.Contains("An enormous black vortex descends"), "Should reach the vortex, now Story node 14.");
        Assert.IsTrue(output.Contains("Elder Long perceives the pulse"), "The c8 callback at node 15 must survive.");
        Assert.IsTrue(output.Contains("The process is now irreversible."), "Should reach the separation at node 16.");
        Assert.IsTrue(output.Contains("You go looking, and she does not stop you,"), "Should take node 16's first reply.");
        Assert.IsTrue(output.Contains("Never."), "Should reach the chapter's last exchange.");

        Assert.IsFalse(output.Contains("You ask the practical question"), "Never node 2's other reply on this walk.");
        Assert.IsFalse(output.Contains("because refusing it is the last thing you have left to give him."), "Never Theo's other branch.");
        Assert.IsFalse(output.Contains("You let her go at her own pace"), "Never Corolla's other branch on this walk.");
        Assert.IsFalse(output.Contains("You say nothing, and she seems to prefer it"), "Never Efeliah's other branch on this walk.");
        Assert.IsFalse(output.Contains("You let him step back and will not look at him"), "Never Smiurl's other branch on this walk.");
        Assert.IsFalse(output.Contains("You leave her the one thing she has asked to keep,"), "Never node 7's other branch.");
        Assert.IsFalse(output.Contains("You ask nothing further."), "Never node 16's other branch.");
    }

    /// <summary>
    /// The other option of every farewell. Stops at the Surge's own prompt rather than replaying
    /// the duet, which the walk above already covers.
    /// </summary>
    [TestMethod]
    [Timeout(120000)]
    public void AlternateFarewells_TakeTheOtherHalfOfEveryGoodbye()
    {
        GameEngine engine = BuildScopedEngine(23);
        SetCurrentChapter(engine, 23);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                          // node 1 -> node 2

            // Both of node 2's replies converge on "entity", but each now plays its own line first.
            idx = await ChooseAsync(idx, "Ask what it would let you do.", ConsoleKey.DownArrow); // node 2 -> "askdo" -> "entity"
            idx = await ContinueAsync(idx);                                          // node 2: break
            idx = await ContinueAsync(idx);                                          // node 2 -> node 3

            idx = await ChooseAsync(idx, "Let it land, and refuse it anyway.", ConsoleKey.DownArrow); // node 3 -> "theohold"
            idx = await ContinueAsync(idx);                                          // node 3 -> node 4

            idx = await ChooseAsync(idx, "Let her be the one to step back.", ConsoleKey.DownArrow);
            idx = await ContinueAsync(idx);                                          // node 4 -> node 5

            idx = await ChooseAsync(idx, "Let the silence answer for you.", ConsoleKey.DownArrow);
            idx = await ContinueAsync(idx);                                          // node 5 -> node 6

            idx = await ChooseAsync(idx, "Let him have this on his own terms.", ConsoleKey.DownArrow);
            idx = await ContinueAsync(idx);                                          // node 6 -> node 7

            idx = await ContinueAsync(idx);                                          // node 7: break
            idx = await ChooseAsync(idx, "Tell her not to say it. Not tonight.", ConsoleKey.DownArrow); // node 7 -> "leavesecret"
            idx = await ContinueAsync(idx);                                          // node 7 -> node 8

            idx = await ContinueAsync(idx);                                          // node 8 -> node 9 (Surge), prompt left unanswered

            await WaitForOutputIndexAsync("Space and time lose part of their meaning.", idx);
        });

        RunWalkToUnansweredPrompt(engine, script, startNodeId: 1);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("You ask the practical question"), "Should take node 2's other reply.");
        Assert.IsTrue(output.Contains("because refusing it is the last thing you have left to give him."), "Should take Theo's hold-it branch.");
        Assert.IsTrue(output.Contains("You let her go at her own pace"), "Should take Corolla's other branch.");
        Assert.IsTrue(output.Contains("You say nothing, and she seems to prefer it"), "Should take Efeliah's other branch.");
        Assert.IsTrue(output.Contains("You let him step back and will not look at him"), "Should take Smiurl's other branch.");
        Assert.IsTrue(output.Contains("You leave her the one thing she has asked to keep,"), "Should take node 7's leave-it branch.");
        Assert.IsTrue(output.Contains("Neither will I."), "The farewells still converge on the same closing line.");
        Assert.IsTrue(output.Contains("It would keep what makes each of us who we are"),
            "Node 2's other reply still reaches 'entity': the branches differ in their own line, not in where they land.");

        Assert.IsFalse(output.Contains("and find that you do not much care that they hear you ask it."), "Never node 2's first reply here.");
        Assert.IsFalse(output.Contains("the way a hand comes back from something hot."), "Never Theo's first branch on this walk.");
        Assert.IsFalse(output.Contains("the composure cracks completely"), "Never Corolla's first branch on this walk.");
        Assert.IsFalse(output.Contains("until she is the one who glances away"), "Never Efeliah's first branch on this walk.");
        Assert.IsFalse(output.Contains("You catch him properly this time"), "Never Smiurl's first branch on this walk.");
        Assert.IsFalse(output.Contains("She smiles at that, and does not deny it,"), "Never node 7's first branch on this walk.");
    }

    /// <summary>
    /// Structural pass, plus the two things about node 9 that make it the game's odd Surge out:
    /// it is a duet, and it is unfailable by tuning rather than by routing.
    /// </summary>
    [TestMethod]
    public void Chapter23Structure_TheMergeIsADuetThatCannotBeLost()
    {
        Chapter chapter = GetScopedChapters(BuildScopedEngine(23)).Single();

        Assert.AreEqual(23, chapter.Id, "The Id field inside c23.json must match its filename.");
        ChapterStructureAssertions.AssertReferencesResolve(chapter);
        ChapterStructureAssertions.AssertChapterEndsAt(chapter, endsAtNodeId: 16);

        SurgeNode merge = (SurgeNode)chapter.Nodes.Single(n => n.Id == 9);
        Assert.AreEqual(".S", merge.Challenge.DuetPattern, "The merge is the one Surge Saberinne plays half of.");
        Assert.AreEqual(0f, merge.Challenge.DrainRate, "Zero drain: the merge cannot be lost by hesitating.");
        Assert.AreEqual(0f, merge.Challenge.Penalty, "Zero penalty: nor by pressing the wrong arrow.");
        Assert.IsNull(merge.Challenge.FailureChildId, "With nothing to fail, there is nowhere for a failure to route.");
        Assert.IsFalse(merge.Challenge.BufferInput,
            "This instance has not opted into buffering: a press on one of her glyphs is still swallowed here.");

        // The army sequence used to be five Choice nodes in a row, each offering two framings of a
        // beat the player could not change. This batch kept the two that give the mind something to
        // do - how the panic is held, and whether the vortex is watched - and made the rest Story.
        foreach ((int step, int[] branches, int rejoin) in new[]
        {
            (10, new[] { 101, 102 }, 11),
            (13, new[] { 131, 132 }, 14),
        })
        {
            ChoiceNode node = (ChoiceNode)chapter.Nodes.Single(n => n.Id == step);
            Assert.AreEqual(2, node.Choices.Count, $"Army step {step} offers two framings.");
            CollectionAssert.AreEquivalent(branches, node.Choices.Select(c => c.ChildId).ToArray(),
                $"Army step {step} should branch to {string.Join(" and ", branches)}.");

            foreach (int branch in branches)
                Assert.AreEqual(rejoin, chapter.Nodes.Single(n => n.Id == branch).ChildId,
                    $"Branch {branch} must rejoin at {rejoin}: a framing changes the telling, never the outcome.");
        }

        foreach (int narrated in new[] { 11, 12, 14, 15 })
            Assert.IsInstanceOfType<StoryNode>(chapter.Nodes.Single(n => n.Id == narrated),
                $"Node {narrated} was a Choice with nothing to decide; this batch made it narration and it must stay that way.");
    }

    /// <summary>
    /// The last decision in the game, taken the other way. The alternate-farewells walk stops at
    /// the Surge, so nothing else reaches node 16, and its second reply needs a DownArrow to be
    /// picked at all. Enters at node 16 directly rather than replaying the duet a second time.
    /// </summary>
    [TestMethod]
    [Timeout(120000)]
    public void AskingNothingAtTheEnd_StillGetsTheReasonAndStillEndsTheChapter()
    {
        GameEngine engine = BuildScopedEngine(23);
        SetCurrentChapter(engine, 23);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ChooseAsync(idx, "Ask nothing more, and let it come or not.", ConsoleKey.DownArrow); // -> "whywait" -> "risksrun"
            idx = await ContinueAsync(idx);                                          // node 16: islast final line -> StartNextChapter(24)
        });

        RunWalkToChapterEnd(engine, script, startNodeId: 16, expectedNextChapterId: 24);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("You ask nothing further."), "Should take node 16's second reply.");
        Assert.IsTrue(output.Contains("the way her pain reaches you:"),
            "Refusing to ask must not cost the revelation: it arrives unoffered instead.");
        Assert.IsTrue(output.Contains("The same risks you run, I run."), "Both replies converge on 'risksrun'.");
        Assert.IsTrue(output.Contains("Never."), "The chapter still ends on the last exchange.");

        Assert.IsFalse(output.Contains("You go looking, and she does not stop you,"), "Never the reaching branch on this walk.");
    }
}
