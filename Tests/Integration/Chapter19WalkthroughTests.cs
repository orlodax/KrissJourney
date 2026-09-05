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
/// Drives c19 ("THE SEA MUTANTS"), substantially rewritten by this batch, through a real
/// <see cref="GameEngine"/>/TerminalMock pair on the <see cref="ChapterWalkthroughTestBase"/>
/// harness. It is the heaviest chapter in the back half to walk: it carries a Surge (the
/// door-punch at node 5, a random 11-glyph row), a Fight (the grotto at node 19), a text-parser
/// search that reads back c18's storm-prep choices, the apnea swim, and the bearing puzzle that
/// hands c20 its lostDayAtSea flag.
///
/// The full walk plays all of them; two shorter walks start mid-chapter to cover the apnea
/// branches the full one does not take, without paying for a second Fight.
/// </summary>
[TestClass]
public class Chapter19WalkthroughTests : ChapterWalkthroughTestBase
{
    [TestMethod]
    [Timeout(300000)]
    public void FullEscape_PunchesTheDoorSurfacesCleanWinsTheGrottoAndCallsWest()
    {
        GameEngine engine = BuildScopedEngine(19);
        SetCurrentChapter(engine, 19);

        // What c18's storm prep would have left behind on the "medbay + Corolla" line.
        engine.AddItemToInventory(new Effect { GainItem = "securedMedbay" });
        engine.AddItemToInventory(new Effect { GainItem = "corollaSecuredBelow" });

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                          // node 1 -> node 2
            idx = await ContinueAsync(idx);                                          // node 2 -> node 3
            idx = await ContinueAsync(idx);                                          // node 3: childid line -> node 4

            idx = await ChooseAsync(idx, "Test-tube crossbreeds, then?");             // node 4 -> "blunt"
            idx = await ContinueAsync(idx);                                          // node 4: Smiurl's childid line -> node 5

            idx = await ContinueAsync(idx);                                          // node 5: "Let it out!"
            idx = await PlaySurgeToVictoryAsync(idx, glyphCount: 11);
            idx = await WaitForOutputIndexAsync("you begin to scream, and your hand snaps forward", idx, timeoutMs: 15000);
            idx = await ContinueAsync(idx);                                          // node 5: success message -> node 6

            idx = await ContinueAsync(idx);                                          // node 6 -> node 7
            idx = await ContinueAsync(idx);                                          // node 7: Kriss's childid line -> node 8
            idx = await ContinueAsync(idx);                                          // node 8 -> node 9
            idx = await ContinueAsync(idx);                                          // node 9 -> node 10
            idx = await ContinueAsync(idx);                                          // node 10 -> node 11
            idx = await ContinueAsync(idx);                                          // node 11 -> node 12

            idx = await ChooseAsync(idx, "Long, steady strokes. Pace yourself.");     // node 12 -> node 1201
            idx = await ChooseAsync(idx, "Keep the same steady rhythm.");             // node 1201 -> node 1211
            idx = await ContinueAsync(idx);                                          // node 1211 -> node 1220
            idx = await ContinueAsync(idx);                                          // node 1220 -> node 1221
            idx = await ContinueAsync(idx);                                          // node 1221: childid line -> node 15

            idx = await ContinueAsync(idx);                                          // node 15 -> node 16
            idx = await ContinueAsync(idx);                                          // node 16: Efeliah's childid line -> node 17

            idx = await ActAsync(idx, "search medbay");                              // node 17: c18 payoff, no childid
            idx = await ActAsync(idx, "search galley");                              // node 17: the partner flag was never taken - refusal
            idx = await DoActionAsync(idx, "run");                                   // node 17 -> node 18

            idx = await ContinueAsync(idx);                                          // node 18 -> node 19

            idx = await ContinueAsync(idx);                                          // node 19: "Prepare to fight!"
            idx = await DriveFightAsync(idx, "Theo drops from the upper platform at full height", qteLength: 38);
            idx = await ContinueAsync(idx);                                          // node 19: victory message -> node 20

            idx = await ContinueAsync(idx);                                          // node 20: Kriss's "Can't be that complicated." break
            idx = await ContinueAsync(idx);                                          // node 20: childid line -> node 21

            idx = await ContinueAsync(idx);                                          // node 21: Corolla's break
            idx = await ContinueAsync(idx);                                          // node 21: childid line -> node 210

            idx = await ChooseAsync(idx, "West. The sun's low enough to be setting."); // node 210 -> node 211
            idx = await ContinueAsync(idx);                                          // node 211: Smiurl's childid line -> node 22

            idx = await ChooseAsync(idx, "That doesn't sound good.");                 // node 22: reply index 0, "Go on."
            idx = await ContinueAsync(idx);                                          // node 22: Smiurl's childid line -> node 23

            idx = await ContinueAsync(idx);                                          // node 23 (islast) -> StartNextChapter(20)
        });

        RunWalkToChapterEnd(engine, script, startNodeId: 1, expectedNextChapterId: 20, scriptTimeoutSeconds: 240);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("CHAPTER 19"), "Should have rendered c19's own header at node 1.");
        Assert.IsTrue(output.Contains("You mean these monsters were grown in a test tube?"), "Should take node 4's blunt reply.");
        Assert.IsFalse(output.Contains("So he's kin to your own people"), "Should not take node 4's gentle reply.");
        Assert.IsTrue(output.Contains("you begin to scream, and your hand snaps forward"), "The Surge at node 5 should have been won.");
        Assert.IsTrue(output.Contains("The medical case you lashed down is still intact"),
            "With securedMedbay in the bag, node 17's search should pay off c18's storm prep.");
        Assert.IsTrue(output.Contains("You dig for the galley locker anyway"),
            "Without securedGalley the same search must refuse - a storm-prep pair is exclusive by design.");
        Assert.IsTrue(output.Contains("Theo drops from the upper platform at full height"), "The grotto fight should have been won.");
        Assert.IsTrue(output.Contains("It has to be west!"), "Smiurl should confirm the correct bearing at node 211.");
        Assert.IsTrue(output.Contains("It was not a natural phenomenon"), "Should reach Efeliah's storm revelation at node 22.");
        Assert.IsTrue(output.Contains("no less human than we are"), "Should reach the closing line at node 23.");

        Assert.IsFalse(output.Contains("A full day gone"), "Calling west correctly must skip the wrong-bearing node (212).");
        Assert.IsFalse(output.Contains("only means dying slower"), "The steady swim must not touch the drowning branches.");
    }

    /// <summary>
    /// The other way to survive the swim: bank air early, then spend it in one push. Starts at
    /// node 12 so the apnea puzzle can be covered without replaying the Surge and the Fight.
    /// </summary>
    [TestMethod]
    [Timeout(120000)]
    public void ApneaSwim_SteadyThenHard_StillSurfaces()
    {
        GameEngine engine = BuildScopedEngine(19);
        SetCurrentChapter(engine, 19);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ChooseAsync(idx, "Long, steady strokes. Pace yourself.");     // node 12 -> node 1201
            idx = await ChooseAsync(idx, "The curve's close. Push harder now.", ConsoleKey.DownArrow); // node 1201 -> node 1212
            idx = await ContinueAsync(idx);                                          // node 1212 -> node 1220
            idx = await ContinueAsync(idx);                                          // node 1220 -> node 1221
            idx = await ContinueAsync(idx);                                          // node 1221 -> node 15

            await WaitForOutputIndexAsync("You make for the portal at the top of the tunnel", idx);
        });

        RunWalkToUnansweredPrompt(engine, script, startNodeId: 12);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("the reserve you banked"), "Should take node 1212, the banked-then-spent branch.");
        Assert.IsTrue(output.Contains("entirely, gloriously alive"), "Should surface clean at node 1220.");
        Assert.IsFalse(output.Contains("Theo resuscitates") || output.Contains("If I'm alive, it's because of you"),
            "Surviving the swim must skip the drowning node entirely.");
    }

    /// <summary>
    /// Both hard-first branches drown, and drowning is not a Game Over: Theo brings him back and
    /// the chapter goes on through the same node 15. Walked here for node 1214; node 1213 is the
    /// other half of the same fork and is asserted structurally below.
    /// </summary>
    [TestMethod]
    [Timeout(120000)]
    public void ApneaSwim_HardThenHard_DrownsAndIsBroughtBack()
    {
        GameEngine engine = BuildScopedEngine(19);
        SetCurrentChapter(engine, 19);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ChooseAsync(idx, "Kick hard, straight for the light.", ConsoleKey.DownArrow); // node 12 -> node 1202
            idx = await ChooseAsync(idx, "Keep kicking. No turning back now.", ConsoleKey.DownArrow); // node 1202 -> node 1214
            idx = await ContinueAsync(idx);                                          // node 1214 -> node 13
            idx = await ContinueAsync(idx);                                          // node 13 -> node 14
            idx = await ContinueAsync(idx);                                          // node 14: childid line -> node 15

            await WaitForOutputIndexAsync("You make for the portal at the top of the tunnel", idx);
        });

        RunWalkToUnansweredPrompt(engine, script, startNodeId: 12);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("your lungs stop"), "Should take node 1214, the all-out branch.");
        Assert.IsTrue(output.Contains("If I'm alive, it's because of you."), "Drowning must be survivable: Theo brings him back at node 14.");
        Assert.IsFalse(output.Contains("GAME OVER"), "The apnea swim is a branch, not a death.");
        Assert.IsFalse(output.Contains("entirely, gloriously alive"), "The clean-exit node must not play on a drowning run.");
    }

    /// <summary>
    /// Structural pass: ids unique, references resolve, one reachable islast, the apnea fork's
    /// four leaves land where the chapter's own recaps say they do, and the chapter's Fight node
    /// has a real calibrated Prowess rather than one inherited through the fallback.
    /// </summary>
    [TestMethod]
    public void Chapter19Structure_ApneaForkConvergesAndTheGrottoFightIsCalibrated()
    {
        Chapter chapter = GetScopedChapters(BuildScopedEngine(19)).Single();

        Assert.AreEqual(19, chapter.Id, "The Id field inside c19.json must match its filename.");
        ChapterStructureAssertions.AssertReferencesResolve(chapter);
        ChapterStructureAssertions.AssertChapterEndsAt(chapter, endsAtNodeId: 23);

        // Two survive, two drown, and both drownings rejoin the story rather than ending it.
        Assert.AreEqual(1220, chapter.Nodes.Single(n => n.Id == 1211).ChildId);
        Assert.AreEqual(1220, chapter.Nodes.Single(n => n.Id == 1212).ChildId);
        Assert.AreEqual(13, chapter.Nodes.Single(n => n.Id == 1213).ChildId);
        Assert.AreEqual(13, chapter.Nodes.Single(n => n.Id == 1214).ChildId);
        Assert.IsFalse(chapter.Nodes.Single(n => n.Id == 13).IsClosing, "Drowning here is a branch, not a Game Over.");

        Assert.IsTrue(chapter.Nodes.Any(n => n is FightNode), "c19 still holds the grotto fight.");
        Assert.IsTrue(ProwessHelper.IsDefined(19),
            "A chapter with a Fight node needs its own Prowess entry, not the nearest-chapter fallback.");

        // The bearing puzzle: exactly one right answer, and every wrong one costs the same day.
        ChoiceNode bearing = (ChoiceNode)chapter.Nodes.Single(n => n.Id == 210);
        Assert.AreEqual(1, bearing.Choices.Count(c => c.ChildId == 211), "Exactly one heading is correct.");
        Assert.IsTrue(bearing.Choices.Where(c => c.ChildId == 212).All(c => c.Effect?.GainItem == "lostDayAtSea"),
            "Every wrong heading must set the flag c20 reads back.");
        Assert.IsTrue(bearing.Choices.Where(c => c.ChildId == 211).All(c => c.Effect == null),
            "The correct heading must not set it.");
    }
}
