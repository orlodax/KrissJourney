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
/// Drives c13 ("THE MUNICIPAL BUILDING") through a real <see cref="GameEngine"/>/TerminalMock
/// pair, on the harness described in <see cref="ChapterWalkthroughTestBase"/>. Like c12's, this
/// file is new coverage: the chapter had no walkthrough of its own before issue 19 reworked it.
///
/// Its two <see cref="FightNode"/> encounters are driven with the base's DriveFightAsync, which
/// resolves each QTE by reacting to the frames as they are drawn rather than by gambling on a
/// pre-computed sleep. Neither encounter sets qtelength, so both run at Encounter's default 20.
/// </summary>
[TestClass]
public class Chapter13WalkthroughTests : ChapterWalkthroughTestBase
{
    const int DefaultQteLength = 20; // Encounter.QteLength's default; neither c13 fight overrides it

    [TestMethod]
    [Timeout(300000)]
    public void Chapter13_WalksFromNode1ThroughNode13_WinsBothLandingsAndUnlocksTheWeaponsLocker()
    {
        GameEngine engine = BuildScopedEngine(13);
        SetCurrentChapter(engine, 13);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                          // node 1 -> node 2

            idx = await ContinueAsync(idx);                                          // node 2: Riff's "Seven floors" break
            idx = await ChooseAsync(idx, "\"And once we are inside? Give me the route.\""); // node 2: reply -> linename "service"
            idx = await ContinueAsync(idx);                                          // node 2: the route's own break
            idx = await ContinueAsync(idx);                                          // node 2: Riff's "Everything Joe's people confiscate" break
            idx = await ContinueAsync(idx);                                          // node 2: Efeliah's "I am ready." break
            idx = await ContinueAsync(idx);                                          // node 2: Theo's "We go." break
            idx = await ContinueAsync(idx);                                          // node 2: Smiurl's childid line -> node 3

            idx = await ContinueAsync(idx);                                          // node 3 -> node 14

            idx = await ContinueAsync(idx);                                          // node 14: Riff's "Good luck." break
            idx = await ChooseAsync(idx, "\"Wait. What does 'we cannot hold for long' mean?\""); // node 14: reply -> "maximum"
            idx = await ContinueAsync(idx);                                          // node 14: "Maximum risk." break
            idx = await ContinueAsync(idx);                                          // node 14: Kriss's childid line -> node 4

            idx = await ContinueAsync(idx);                                          // node 4 -> node 5
            idx = await ContinueAsync(idx);                                          // node 5 -> node 6 (Fight)

            idx = await ContinueAsync(idx);                                          // node 6: "Prepare to fight!" -> rounds begin
            idx = await DriveFightAsync(idx, "like machines unplugged", DefaultQteLength);
            idx = await ContinueAsync(idx);                                          // node 6: victory message's own continue -> node 7

            // Floor 2's required chain: the keys come off the guards, the keys open the locker,
            // and only the locker's gear unlocks the stairs. None of the first three carry a
            // childid, so each hands the prompt straight back.
            idx = await ActAsync(idx, "search guard");                               // grants foundKeys
            idx = await ActAsync(idx, "open locker");                                // gated on foundKeys -> grants weaponsFound
            idx = await ActAsync(idx, "take baton");                                 // flavour, ungated
            idx = await ActAsync(idx, "go up");                                      // gated on weaponsFound; no answer, so it advances
                                                                                     // straight to node 8 with no pause of its own

            idx = await ContinueAsync(idx);                                          // node 8 -> node 9

            idx = await ContinueAsync(idx);                                          // node 9: Efeliah's "I am fine." break
            idx = await ContinueAsync(idx);                                          // node 9: Corolla's childid line -> node 10 (Fight)

            idx = await ContinueAsync(idx);                                          // node 10: "Prepare to fight!" -> rounds begin
            idx = await DriveFightAsync(idx, "The officer is the last to fall.", DefaultQteLength);
            idx = await ContinueAsync(idx);                                          // node 10: victory message's own continue -> node 11

            idx = await ContinueAsync(idx);                                          // node 11 -> node 12
            idx = await ContinueAsync(idx);                                          // node 12 -> node 13

            idx = await ContinueAsync(idx);                                          // node 13: islast -> StartNextChapter(14)
        });

        RunWalkToChapterEnd(engine, script, startNodeId: 1, expectedNextChapterId: 14, scriptTimeoutSeconds: 240);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("CHAPTER 13"), "Should have rendered c13's own header at node 1.");
        Assert.IsTrue(output.Contains("Service entrance, maintenance corridor"), "Node 2's reply should have produced Riff's route.");
        Assert.IsTrue(output.Contains("Maximum risk."), "Node 14's reply should have got the cost of the diversion said out loud.");
        Assert.IsTrue(output.Contains("small ring of keys"), "'search guard' should have found the keys.");
        Assert.IsTrue(output.Contains("the locker swings open on your confiscated gear"), "'open locker' should have paid off the keys.");
        Assert.IsTrue(output.Contains("like machines unplugged"), "Should have won the floor-2 landing (node 6).");
        Assert.IsTrue(output.Contains("The officer is the last to fall."), "Should have won the floor-6 landing (node 10).");
        Assert.IsFalse(output.Contains("GAME OVER"), "Neither fight should have been lost.");
        Assert.IsTrue(output.Contains("Riff's fighters, still buying time."), "Should have flowed all the way to node 13's closing beat.");

        Assert.IsTrue(engine.Evaluate(new Condition { Item = "foundKeys" }), "'search guard' should have banked foundKeys.");
        Assert.IsTrue(engine.Evaluate(new Condition { Item = "weaponsFound" }), "'open locker' should have banked weaponsFound.");
    }

    /// <summary>
    /// Floor 2's negative space. The stairs are the node's only exit and they are gated on
    /// weaponsFound, which is only reachable through the locker, which is only reachable through
    /// the keys. Each unmet gate must play its own refusal and hand the prompt back, so a player
    /// who climbs early is told why rather than either advancing bare-handed or getting stuck.
    /// </summary>
    [TestMethod]
    [Timeout(60000)]
    public void Chapter13Node7_TheStairsStayShutUntilTheLockerIsOpened()
    {
        GameEngine engine = BuildScopedEngine(13);
        SetCurrentChapter(engine, 13);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ActAsync(idx, "go up");                                      // no weaponsFound -> refusal
            idx = await ActAsync(idx, "open locker");                                // no keys yet -> the locker's own refusal
            idx = await ActAsync(idx, "search locker");                              // ungated flavour: says the guard must carry the key

            await WaitForOutputIndexAsync(ActionPrompt, idx);                        // proves the prompt came back a fourth time
        });

        RunWalkToUnansweredPrompt(engine, script, startNodeId: 7);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("Not without our weapons."),
            "Climbing before the locker is open must play the stairs' refusal.");
        Assert.IsTrue(output.Contains("The locker is locked. I need a key."),
            "Opening the locker without the keys must play the locker's refusal.");
        Assert.IsTrue(output.Contains("The guard must carry the key."),
            "Examining the locker must point at where the key is, so the chain is discoverable.");

        Assert.IsFalse(engine.Evaluate(new Condition { Item = "weaponsFound" }), "A refused locker must not bank weaponsFound.");
    }

    /// <summary>
    /// Cheapest-first structural pass, plus the one thing about c13 that crashes the game rather
    /// than merely reading wrong: a Fight node in a chapter with no calibrated Prowess entry used
    /// to throw mid-combat, and c13 has two of them.
    /// </summary>
    [TestMethod]
    public void Chapter13Structure_BothFightsAreCalibratedAndOneNodeEndsIt()
    {
        Chapter chapter = GetScopedChapters(BuildScopedEngine(13)).Single();

        Assert.AreEqual(13, chapter.Id, "The Id field inside c13.json must match its filename.");
        ChapterStructureAssertions.AssertReferencesResolve(chapter);
        ChapterStructureAssertions.AssertChapterEndsAt(chapter, endsAtNodeId: 13);

        Assert.IsTrue(ProwessHelper.IsDefined(13),
            "c13 holds Fight nodes, so it needs its own Prowess entry rather than the nearest-chapter fallback.");

        FightNode[] fights = [.. chapter.Nodes.OfType<FightNode>()];
        Assert.AreEqual(2, fights.Length, "The two stairwell landings are the chapter's fights.");

        foreach (FightNode fight in fights)
        {
            Assert.IsTrue(fight.Encounter.Foes.Any(), $"Fight node {fight.Id} has no foes.");
            Assert.IsTrue(fight.Encounter.Foes.All(f => f.Health > 0 && f.Damage > 0),
                $"Fight node {fight.Id} has a foe that cannot fight or cannot be beaten.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(fight.Encounter.VictoryMessage), $"Fight node {fight.Id} has no victory message.");
            Assert.IsFalse(string.IsNullOrWhiteSpace(fight.Encounter.DefeatMessage),
                $"Fight node {fight.Id} has no defeat message, so a loss would fall back to the engine's generic line.");
            Assert.IsTrue(chapter.Nodes.Any(n => n.Id == fight.ChildId), $"Fight node {fight.Id}'s childid does not resolve.");
        }

        // FightNode.GameOver restarts the chapter through LoadNode(1), so a defeat is only
        // survivable if node 1 exists.
        Assert.IsTrue(chapter.Nodes.Any(n => n.Id == 1), "A lost fight restarts the chapter at node 1, which must exist.");
    }
}
