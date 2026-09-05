using System;
using System.Linq;
using System.Threading.Tasks;
using KrissJourney.Kriss.Helpers;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Integration;

/// <summary>
/// Drives c22 ("SABERINNE"), substantially rewritten by this batch, on the
/// <see cref="ChapterWalkthroughTestBase"/> harness.
///
/// The duel is deliberately three-phased and only the first phase is winnable: node 3 is a real
/// Fight, nodes 4-9 are two forks whose branches all converge because losing ground is the point,
/// and node 11 is the Surge that ends it. Everything after node 12 is the long revelation, whose
/// decisions are all reply prompts. The full walk plays the whole chapter with the first option
/// at every prompt; two shorter walks start mid-chapter and take the other option of every one.
/// </summary>
[TestClass]
public class Chapter22WalkthroughTests : ChapterWalkthroughTestBase
{
    const string SurgeSuccessMarker = "The sword loses its weight in your hand";
    const string FightVictoryMarker = "the warrior meets your lunge with lightning speed";

    [TestMethod]
    [Timeout(300000)]
    public void FullDuel_WinsPhaseOneLosesGroundReleasesTheSurgeAndHearsTheWholeRevelation()
    {
        GameEngine engine = BuildScopedEngine(22);
        SetCurrentChapter(engine, 22);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                          // node 1 -> node 2
            idx = await ContinueAsync(idx);                                          // node 2: her name, childid line -> node 3

            idx = await ContinueAsync(idx);                                          // node 3: "Prepare to fight!"
            idx = await DriveFightAsync(idx, FightVictoryMarker, qteLength: 42);
            idx = await ContinueAsync(idx);                                          // node 3: victory message -> node 4

            idx = await ChooseAsync(idx, "Meet the sabers blade for blade, force against force."); // node 4 -> node 5
            idx = await ContinueAsync(idx);                                          // node 5 -> node 7
            idx = await ChooseAsync(idx, "Feint low, then reverse the blade at the last instant."); // node 7 -> node 8
            idx = await ContinueAsync(idx);                                          // node 8 -> node 10
            idx = await ContinueAsync(idx);                                          // node 10 -> node 11

            idx = await ContinueAsync(idx);                                          // node 11: "Let it out!"
            idx = await PlaySurgeToVictoryAsync(idx, glyphCount: 16);
            idx = await WaitForOutputIndexAsync(SurgeSuccessMarker, idx, timeoutMs: 15000);
            idx = await ContinueAsync(idx);                                          // node 11: success message -> node 12

            idx = await ContinueAsync(idx);                                          // node 12: the unmasking -> node 13
            idx = await ContinueAsync(idx);                                          // node 13 -> node 14
            idx = await ContinueAsync(idx);                                          // node 14 -> node 15

            idx = await ChooseAsync(idx, "How could you possibly know that?");        // node 15 -> node 16 (reply carries a childid)
            idx = await ChooseAsync(idx, "What does that even mean, bend matter?");   // node 16 -> node 17
            idx = await ChooseAsync(idx, "No. I don't believe that.");                // node 17 -> "stops"
            idx = await ContinueAsync(idx);                                          // node 17: childid line -> node 18

            idx = await ContinueAsync(idx);                                          // node 18 -> node 19
            idx = await ChooseAsync(idx, "How is that possible?");                    // node 19 -> node 20
            idx = await ChooseAsync(idx, "Wait. The legend Elder Long told us.");     // node 20 -> "legend"
            idx = await ContinueAsync(idx);                                          // node 20: childid line -> node 21

            idx = await ContinueAsync(idx);                                          // node 21: the hedge maze -> node 22
            idx = await ChooseAsync(idx, "\"I heard you.\"");                         // node 22 -> "howdid"
            idx = await ContinueAsync(idx);                                          // node 22: childid line -> node 23

            idx = await ChooseAsync(idx, "\"Nothing bad. I promise you that.\"");     // node 23 -> "bound"
            idx = await ContinueAsync(idx);                                          // node 23: childid line -> node 24

            idx = await ContinueAsync(idx);                                          // node 24 (islast) -> StartNextChapter(23)
        });

        RunWalkToChapterEnd(engine, script, startNodeId: 1, expectedNextChapterId: 23, scriptTimeoutSeconds: 240);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("CHAPTER 22"), "Should have rendered c22's own header at node 1.");
        Assert.IsTrue(output.Contains("I am Saberinne."), "She names herself before the duel, and again after the helm comes off.");
        Assert.IsTrue(output.Contains(FightVictoryMarker), "Phase one is winnable and should have been won.");
        Assert.IsTrue(output.Contains("It buys you nothing."), "Should take node 5, the brute-force tactic.");
        Assert.IsTrue(output.Contains("the wall is closer now than"), "Should take node 8, the feint-reversal tactic.");
        Assert.IsTrue(output.Contains("could claim they had, and were still alive"), "The faceless-helm beat at node 10 must survive.");
        Assert.IsTrue(output.Contains(SurgeSuccessMarker), "The Surge at node 11 should have been won.");
        Assert.IsTrue(output.Contains("It is a woman."), "Node 12 is the unmasking.");
        Assert.IsTrue(output.Contains("You would call it, a little imprecisely, telekinesis"),
            "c22 is where the word telekinesis is finally allowed to be said.");
        Assert.IsTrue(output.Contains("So far, only Saberinne has ever"), "Should reach Efeliah's shielded-thought payoff at node 22.");
        Assert.IsTrue(output.Contains("is bound to Saberinne's"), "Should reach the binding at node 23.");
        Assert.IsTrue(output.Contains("Sleep takes you before you finish the thought."), "Should reach the chapter's last line at node 24.");

        Assert.IsFalse(output.Contains("You veer wide, aiming for the gap"), "Never node 6 on this walk.");
        Assert.IsFalse(output.Contains("You abandon caution entirely"), "Never node 9 on this walk.");
    }

    /// <summary>
    /// The other tactic at each of phase two's forks. Starts at node 4 so the losing-ground
    /// branches can be covered without replaying the Fight, and stops at the Surge's own prompt.
    /// </summary>
    [TestMethod]
    [Timeout(120000)]
    public void PhaseTwoAlternateTactics_LoseTheSameGroundAndReachTheSameWall()
    {
        GameEngine engine = BuildScopedEngine(22);
        SetCurrentChapter(engine, 22);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ChooseAsync(idx, "Break the rhythm, cut in from an angle left unguarded.", ConsoleKey.DownArrow); // node 4 -> node 6
            idx = await ContinueAsync(idx);                                          // node 6 -> node 7
            idx = await ChooseAsync(idx, "Abandon finesse. Throw everything into one overhead cut.", ConsoleKey.DownArrow); // node 7 -> node 9
            idx = await ContinueAsync(idx);                                          // node 9 -> node 10
            idx = await ContinueAsync(idx);                                          // node 10 -> node 11 (Surge), whose own prompt is left unanswered

            await WaitForOutputIndexAsync("The blade rests, patient, against your throat.", idx);
        });

        RunWalkToUnansweredPrompt(engine, script, startNodeId: 4);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("You veer wide, aiming for the gap"), "Should take node 6 this time.");
        Assert.IsTrue(output.Contains("You abandon caution entirely"), "Should take node 9 this time.");
        Assert.IsTrue(output.Contains("Suddenly you find the wall at your back"), "Both forks must converge on node 10 all the same.");
        Assert.IsFalse(output.Contains("You throw your whole weight behind the next exchange"), "Never node 5 on this walk.");
        Assert.IsFalse(output.Contains("You drop low, and at the very last instant"), "Never node 8 on this walk.");
    }

    /// <summary>
    /// The revelation's other reply at every prompt. Starts at node 12 - the unmasking, straight
    /// after the Surge - so the whole conversation can be walked a second time cheaply.
    /// </summary>
    [TestMethod]
    [Timeout(120000)]
    public void RevelationAlternateReplies_ReachTheSameEndingByTheOtherRoute()
    {
        GameEngine engine = BuildScopedEngine(22);
        SetCurrentChapter(engine, 22);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                          // node 12 -> node 13
            idx = await ContinueAsync(idx);                                          // node 13 -> node 14
            idx = await ContinueAsync(idx);                                          // node 14 -> node 15

            idx = await ChooseAsync(idx, "Prophesied by whom?", ConsoleKey.DownArrow);            // node 15 -> node 16
            idx = await ChooseAsync(idx, "That's not possible.", ConsoleKey.DownArrow);           // node 16 -> node 17
            idx = await ChooseAsync(idx, "Try me.", ConsoleKey.DownArrow);                        // node 17 -> "stops"
            idx = await ContinueAsync(idx);                                          // node 17 -> node 18

            idx = await ContinueAsync(idx);                                          // node 18 -> node 19
            idx = await ChooseAsync(idx, "Say nothing, and let her go on.", ConsoleKey.DownArrow); // node 19 -> node 20
            idx = await ChooseAsync(idx, "Ask her about Long's dream of the goddess.", ConsoleKey.DownArrow); // node 20 -> "legend"
            idx = await ContinueAsync(idx);                                          // node 20 -> node 21

            idx = await ContinueAsync(idx);                                          // node 21 -> node 22
            idx = await ChooseAsync(idx, "Hold her gaze, and let her work it out.", ConsoleKey.DownArrow); // node 22 -> "howdid"
            idx = await ContinueAsync(idx);                                          // node 22 -> node 23

            idx = await ChooseAsync(idx, "Put a hand on her shoulder, and say nothing.", ConsoleKey.DownArrow); // node 23 -> "bound"
            idx = await ContinueAsync(idx);                                          // node 23 -> node 24

            idx = await ContinueAsync(idx);                                          // node 24 (islast) -> StartNextChapter(23)
        });

        RunWalkToChapterEnd(engine, script, startNodeId: 12, expectedNextChapterId: 23);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("You would call it, a little imprecisely, telekinesis"),
            "The lore is carried by the lines, not the replies, so it must land either way.");
        Assert.IsTrue(output.Contains("is what keeps you from actually managing it"), "Node 17's \"stops\" line closes the beat either way.");
        Assert.IsTrue(output.Contains("Sleep takes you before you finish the thought."), "This route must reach the same ending.");
    }

    /// <summary>
    /// Structural pass. The interesting shape here is that phase two is deliberately unwinnable:
    /// both forks converge, so no tactic can be "the right one". That is worth pinning, because
    /// a later edit giving one branch its own childid would quietly turn a scripted defeat into
    /// a puzzle the player can fail at.
    /// </summary>
    [TestMethod]
    public void Chapter22Structure_PhaseTwoIsUnwinnableByConstructionAndTheDuelIsCalibrated()
    {
        Chapter chapter = GetScopedChapters(BuildScopedEngine(22)).Single();

        Assert.AreEqual(22, chapter.Id, "The Id field inside c22.json must match its filename.");
        ChapterStructureAssertions.AssertReferencesResolve(chapter);
        ChapterStructureAssertions.AssertChapterEndsAt(chapter, endsAtNodeId: 24);

        foreach ((int fork, int[] branches, int join) in new[]
        {
            (4, new[] { 5, 6 }, 7),
            (7, new[] { 8, 9 }, 10),
        })
        {
            ChoiceNode forkNode = (ChoiceNode)chapter.Nodes.Single(n => n.Id == fork);
            CollectionAssert.AreEquivalent(branches, forkNode.Choices.Select(c => c.ChildId).ToArray(),
                $"Node {fork} should offer exactly its two tactics.");

            foreach (int branch in branches)
                Assert.AreEqual(join, chapter.Nodes.Single(n => n.Id == branch).ChildId,
                    $"Node {branch} must converge on {join}: phase two is a scripted loss, not a puzzle.");
        }

        // The Surge that ends the duel is never a Game Over: no failurechildid means the player
        // simply gathers the rage again on the spot.
        SurgeNode release = (SurgeNode)chapter.Nodes.Single(n => n.Id == 11);
        Assert.IsNull(release.Challenge.FailureChildId, "Failing the release must retry in place, not route anywhere.");
        Assert.AreEqual(12, release.ChildId);

        Assert.IsTrue(chapter.Nodes.Any(n => n is FightNode), "Phase one is a real Fight node.");
        Assert.IsTrue(ProwessHelper.IsDefined(22),
            "A chapter with a Fight node needs its own Prowess entry, not the nearest-chapter fallback.");
    }
}
