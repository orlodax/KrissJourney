using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Integration;

/// <summary>
/// Drives c20 ("THE LONG CROSSING") - the chapter added by this batch - through a real
/// <see cref="GameEngine"/>/TerminalMock pair, on the harness described in
/// <see cref="ChapterWalkthroughTestBase"/>.
///
/// The first two walks take both options of every one of the chapter's decision points, and
/// differ in one more thing: the first is played by a Kriss who watched Corolla take the rifle
/// in c15 and burned a day on the wrong bearing in c19, the second by one who did neither. Both
/// flags are inventory conditions rather than isNodeVisited ones, so the gated scenes are still
/// offered either way and answer with their refusals instead - which is exactly what those two
/// walks check.
///
/// They cannot between them cover the chapter, because the forest crossing is a 2x2 grid rather
/// than a fork: junction 1 routes to a different copy of junction 2 (105 clean, 115 adrift), and
/// the four leaves land on three arrival tiers. Those two walks are the outer diagonal - nothing
/// wrong, and everything wrong - so the two mixed walks below take the middle, which is the only
/// way nodes 107, 109, 116, 118 and the shared late-afternoon arrival 112 are ever seen.
/// </summary>
[TestClass]
public class Chapter20WalkthroughTests : ChapterWalkthroughTestBase
{
    [TestMethod]
    [Timeout(120000)]
    public void CarriedFlagsAndCleanCrossing_ReachesTheMonolithWithTheAfternoonToSpare()
    {
        GameEngine engine = BuildScopedEngine(20);
        SetCurrentChapter(engine, 20);

        // What c15's award ceremony and c19's bearing puzzle would have left in the bag by now.
        engine.AddItemToInventory(new Effect { GainItem = "corollaArmed" });
        engine.AddItemToInventory(new Effect { GainItem = "lostDayAtSea" });

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                          // node 1 -> node 2

            idx = await ActAsync(idx, "count days");                                 // node 2: gated flavour, no childid - hands the prompt back
            idx = await DoActionAsync(idx, "hold compass");                          // node 2 -> node 3

            idx = await ContinueAsync(idx);                                          // node 3 -> node 4
            idx = await ContinueAsync(idx);                                          // node 4 -> node 5
            idx = await ContinueAsync(idx);                                          // node 5: Corolla's childid line -> node 6

            idx = await ActAsync(idx, "ask rifle");                                  // node 6: the c15 payoff, no childid
            idx = await DoActionAsync(idx, "eat");                                   // node 6 -> node 7

            idx = await ContinueAsync(idx);                                          // node 7 -> node 8

            idx = await ChooseAsync(idx, "Watch the flames and let your mind empty out."); // node 8 -> node 81
            idx = await ContinueAsync(idx);                                          // node 81 -> node 9

            idx = await ContinueAsync(idx);                                          // node 9: Corolla's "By the way," break
            idx = await ChooseAsync(idx, "After everything I've been through, a few trees won't stop me."); // -> "confident"
            idx = await ContinueAsync(idx);                                          // node 9: Corolla's verdict break
            idx = await ContinueAsync(idx);                                          // node 9: Kriss's childid line -> node 10

            idx = await ContinueAsync(idx);                                          // node 10 -> node 101

            idx = await ChooseAsync(idx, "Send Smiurl scrambling up the tallest tree in reach."); // node 101 -> node 102
            idx = await ContinueAsync(idx);                                          // node 102 -> node 105

            idx = await ChooseAsync(idx, "Send Smiurl up once more to fix the bearing.");         // node 105 -> node 106
            idx = await ContinueAsync(idx);                                          // node 106 -> node 108
            idx = await ContinueAsync(idx);                                          // node 108 -> node 110
            idx = await ContinueAsync(idx);                                          // node 110 -> node 12

            idx = await ContinueAsync(idx);                                          // node 12 -> node 13
            idx = await ContinueAsync(idx);                                          // node 13 -> node 14
            idx = await ContinueAsync(idx);                                          // node 14 -> node 15

            idx = await ChooseAsync(idx, "Offer to go alone, plainly.");             // node 15 -> "offerplain"
            idx = await ContinueAsync(idx);                                          // node 15: islast final line -> StartNextChapter(21)
        });

        RunWalkToChapterEnd(engine, script, startNodeId: 1, expectedNextChapterId: 21);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("CHAPTER 20"), "Should have rendered c20's own header at node 1.");
        Assert.IsTrue(output.Contains("You keep forgetting the one you spent chasing the wrong"),
            "With lostDayAtSea in the bag, node 2's day-counting flavour should play its answer, not its refusal.");
        Assert.IsTrue(output.Contains("This laser rifle isn't just an honorific"),
            "With corollaArmed in the bag, node 6 should pay off c15's award ceremony.");
        Assert.IsTrue(output.Contains("Fear."), "Node 3's reveal - the call is afraid - must survive.");
        Assert.IsTrue(output.Contains("You watch the flames for a long time"), "Should take node 81, the first campfire branch.");
        Assert.IsTrue(output.Contains("a few trees aren't"), "Should speak the confident answer at node 9.");
        Assert.IsTrue(output.Contains("it's the right ground"),
            "Two climbs should reach node 106, the on-track branch.");
        Assert.IsTrue(output.Contains("with hours of good light still ahead of you"),
            "The clean crossing should arrive mid-afternoon (node 110).");
        Assert.IsTrue(output.Contains("You don't need to come with me"), "Should offer to go alone at node 15.");
        Assert.IsTrue(output.Contains("We won't leave you, boy."), "Should reach Theo's refusal, the chapter's last beat.");

        Assert.IsFalse(output.Contains("It stopped mattering a while ago"), "The day-counting refusal must not play with the flag set.");
        Assert.IsFalse(output.Contains("you're too hungry to insist"), "The rifle refusal must not play with the flag set.");
        Assert.IsFalse(output.Contains("You've walked in a slow, complete circle"), "Never the full circle on this walk (node 117).");
        Assert.IsFalse(output.Contains("the light is already turning gold and low"), "Never the dusk arrival on this walk (node 111).");
        Assert.IsFalse(output.Contains("the light already going long and yellow"), "Nor the late-afternoon one (node 112).");
    }

    [TestMethod]
    [Timeout(120000)]
    public void NoFlagsAndLostTwice_TakesEveryOtherBranchAndArrivesAtDusk()
    {
        GameEngine engine = BuildScopedEngine(20);
        SetCurrentChapter(engine, 20);

        // Nothing seeded: both gated scenes must still be offered, and must answer with a refusal.

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                          // node 1 -> node 2

            idx = await ActAsync(idx, "count days");                                 // node 2: refused, prompt handed back
            idx = await DoActionAsync(idx, "steer needle");                          // node 2 -> node 3 (a different verb/object pair for the same object)

            idx = await ContinueAsync(idx);                                          // node 3 -> node 4
            idx = await ContinueAsync(idx);                                          // node 4 -> node 5
            idx = await ContinueAsync(idx);                                          // node 5 -> node 6

            idx = await ActAsync(idx, "ask rifle");                                  // node 6: refused, prompt handed back
            idx = await DoActionAsync(idx, "drink");                                 // node 6 -> node 7

            idx = await ContinueAsync(idx);                                          // node 7 -> node 8

            idx = await ChooseAsync(idx, "Try to place the animal calls coming from the hills.", ConsoleKey.DownArrow); // node 8 -> node 82
            idx = await ContinueAsync(idx);                                          // node 82 -> node 9

            idx = await ContinueAsync(idx);                                          // node 9: "By the way," break
            idx = await ChooseAsync(idx, "I'm not thrilled either, but I'm not turning back now.", ConsoleKey.DownArrow); // -> "reluctant"
            idx = await ContinueAsync(idx);                                          // node 9: Corolla's verdict break
            idx = await ContinueAsync(idx);                                          // node 9 -> node 10

            idx = await ContinueAsync(idx);                                          // node 10 -> node 101

            idx = await ChooseAsync(idx, "Keep pushing forward and trust your bearings.", ConsoleKey.DownArrow); // node 101 -> node 103
            idx = await ContinueAsync(idx);                                          // node 103 -> node 115, junction 2's adrift copy

            idx = await ChooseAsync(idx, "Keep moving; there's no time to keep stopping.", ConsoleKey.DownArrow); // node 115 -> node 117
            idx = await ContinueAsync(idx);                                          // node 117 -> node 119
            idx = await ContinueAsync(idx);                                          // node 119 -> node 111
            idx = await ContinueAsync(idx);                                          // node 111 -> node 12

            idx = await ContinueAsync(idx);                                          // node 12 -> node 13
            idx = await ContinueAsync(idx);                                          // node 13 -> node 14
            idx = await ContinueAsync(idx);                                          // node 14 -> node 15

            idx = await ChooseAsync(idx, "Offer, but frame it as repaying a debt.", ConsoleKey.DownArrow); // node 15 -> "offerdebt"
            idx = await ContinueAsync(idx);                                          // node 15: islast final line -> StartNextChapter(21)
        });

        RunWalkToChapterEnd(engine, script, startNodeId: 1, expectedNextChapterId: 21);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("It stopped mattering a while ago"),
            "Without lostDayAtSea the day-counting object must still be offered and must refuse.");
        Assert.IsTrue(output.Contains("you're too hungry to insist"),
            "Without corollaArmed the rifle object must still be offered and must refuse.");
        Assert.IsTrue(output.Contains("You spend a while trying to guess which of the sounds"), "Should take node 82 this time.");
        Assert.IsTrue(output.Contains("I'm not thrilled about it, if I'm honest"), "Should speak the reluctant answer at node 9.");
        Assert.IsTrue(output.Contains("You've walked in a slow, complete circle"), "Guessing twice should reach node 117.");
        Assert.IsTrue(output.Contains("the light is already turning gold and low"),
            "Only the doubly-lost crossing arrives at dusk, and it gets there through node 119 (node 111).");
        Assert.IsTrue(output.Contains("I can never repay what I already owe"), "Should frame the offer as a debt at node 15.");

        Assert.IsFalse(output.Contains("This laser rifle isn't just an honorific"), "The rifle payoff must stay behind its flag.");
        Assert.IsFalse(output.Contains("You keep forgetting the one you spent chasing the wrong"), "The day-count payoff must stay behind its flag.");
        Assert.IsFalse(output.Contains("with hours of good light still ahead of you"), "Never the on-time arrival on this walk.");
        Assert.IsFalse(output.Contains("the light already going long and yellow"), "Nor the late-afternoon one (node 112).");
    }

    /// <summary>
    /// Climb at junction 1, guess at junction 2: the clean copy of junction 2 (105) and the only
    /// walk that sees 107 and 109. Starts at the forest rather than at node 1 - everything before
    /// node 101 is already walked twice above, and repeating it would only buy the same coverage
    /// a third time.
    /// </summary>
    [TestMethod]
    [Timeout(60000)]
    public void ClimbedThenGuessed_LosesTheAfternoonAtTheSecondJunctionAndArrivesLate()
    {
        GameEngine engine = BuildScopedEngine(20);
        SetCurrentChapter(engine, 20);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ChooseAsync(idx, "Send Smiurl scrambling up the tallest tree in reach."); // node 101 -> node 102
            idx = await ContinueAsync(idx);                                          // node 102 -> node 105

            idx = await ChooseAsync(idx, "Keep moving; there's no time to keep stopping.", ConsoleKey.DownArrow); // node 105 -> node 107
            idx = await ContinueAsync(idx);                                          // node 107 -> node 109
            idx = await ContinueAsync(idx);                                          // node 109 -> node 112
            idx = await ContinueAsync(idx);                                          // node 112 -> node 12

            await WaitForOutputIndexAsync("formation of grey rock juts up from the highest point of the hill,", idx);
        });

        RunWalkToUnansweredPrompt(engine, script, startNodeId: 101);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("comes back down a minute later utterly certain"), "Junction 1 was climbed, so node 102 confirms the bearing.");
        Assert.IsTrue(output.Contains("the forest quietly takes back"), "Guessing at junction 2 should reach node 107.");
        Assert.IsTrue(output.Contains("how much of the morning went on ground you never"), "Node 109 is the clearing that follows it.");
        Assert.IsTrue(output.Contains("the light already going long and yellow"),
            "One mistake, wherever it was made, arrives late afternoon at node 112.");

        Assert.IsFalse(output.Contains("with hours of good light still ahead of you"), "Not the clean arrival: a junction was guessed.");
        Assert.IsFalse(output.Contains("the light is already turning gold and low"), "Not dusk either: only one junction was guessed.");
        Assert.IsFalse(output.Contains("You've walked in a slow, complete circle"), "The full circle needs both guesses.");
    }

    /// <summary>
    /// The mirror: guess at junction 1, climb at junction 2. That routes through 115, the adrift
    /// copy of the junction, and is the only walk that sees 116 and 118 - and it has to land on
    /// the same node 112 as the walk above, because the chapter tracks how many mistakes were
    /// made, not which one.
    /// </summary>
    [TestMethod]
    [Timeout(60000)]
    public void GuessedThenClimbed_RecoversLateAndArrivesOnTheSameTierAsTheOtherSingleMistake()
    {
        GameEngine engine = BuildScopedEngine(20);
        SetCurrentChapter(engine, 20);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ChooseAsync(idx, "Keep pushing forward and trust your bearings.", ConsoleKey.DownArrow); // node 101 -> node 103
            idx = await ContinueAsync(idx);                                          // node 103 -> node 115

            idx = await ChooseAsync(idx, "Send Smiurl up once more to fix the bearing."); // node 115 -> node 116
            idx = await ContinueAsync(idx);                                          // node 116 -> node 118
            idx = await ContinueAsync(idx);                                          // node 118 -> node 112
            idx = await ContinueAsync(idx);                                          // node 112 -> node 12

            await WaitForOutputIndexAsync("formation of grey rock juts up from the highest point of the hill,", idx);
        });

        RunWalkToUnansweredPrompt(engine, script, startNodeId: 101);

        // No chapter reads forestLost yet, so this is the only thing that would notice if
        // junction 1's Effect stopped firing - or if 115's climb somehow cleared it.
        Assert.IsTrue(engine.Evaluate(new Condition { Item = "forestLost" }),
            "Junction 1's guess should have banked forestLost on the way through, and nothing at 115 takes it back.");

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("sense that a particular root or fallen log has already gone by"),
            "Guessing at junction 1 should play node 103, the walk on instinct.");
        Assert.IsTrue(output.Contains("he tells it to you straight"), "Climbing at 115 should reach node 116, the late correction.");
        Assert.IsTrue(output.Contains("just as certain the certainty"), "Node 118 is the clearing that follows it.");
        Assert.IsTrue(output.Contains("the light already going long and yellow"),
            "Both single-mistake branches converge on node 112's late-afternoon arrival.");

        Assert.IsFalse(output.Contains("comes back down a minute later utterly certain"), "Node 102 belongs to the other junction-1 branch.");
        Assert.IsFalse(output.Contains("it's the right ground"), "Node 106 is junction 2's clean copy, which this walk never reaches.");
        Assert.IsFalse(output.Contains("the forest quietly takes back"), "Node 107 belongs to the other single-mistake branch.");
        Assert.IsFalse(output.Contains("You've walked in a slow, complete circle"), "The full circle needs both guesses.");
    }

    /// <summary>
    /// Cheapest-first structural pass over the chapter this batch adds from scratch: ids unique,
    /// every childid resolves, every nextline resolves to a linename in its own node, node 1
    /// exists (it is both the chapter's entry point and where a lost fight would restart it),
    /// and exactly one reachable node carries islast.
    /// </summary>
    [TestMethod]
    public void Chapter20Structure_IdsAreUniqueEveryReferenceResolvesAndOneNodeEndsIt()
    {
        Chapter chapter = GetScopedChapters(BuildScopedEngine(20)).Single();

        Assert.AreEqual(20, chapter.Id, "The Id field inside c20.json must match its filename.");
        ChapterStructureAssertions.AssertReferencesResolve(chapter);
        ChapterStructureAssertions.AssertChapterEndsAt(chapter, endsAtNodeId: 15);

        // Three junction nodes, not two: 105 and 115 are the same decision reached from a clean
        // morning and from a lost one. All three must be real climb-or-guess forks, not corridors.
        foreach (int junctionId in new[] { 101, 105, 115 })
        {
            ChoiceNode junction = (ChoiceNode)chapter.Nodes.Single(n => n.Id == junctionId);
            Assert.AreEqual(2, junction.Choices.Count, $"Junction {junctionId} should be a straight climb-or-guess fork.");
            Assert.IsNull(junction.Choices[0].Effect, $"Junction {junctionId}'s climb costs only time.");
        }

        // Where the flag is set. Nothing reads forestLost today - the arrival tier is routed by
        // node id instead, and StoryFlowTests lists it as knowingly unspent - so this is only
        // asserting the record is kept. It is a flag rather than a counter: 115 is reached only by
        // a player who already holds it, which is why the loop above cannot assert on choices[1].
        Assert.AreEqual("forestLost", ((ChoiceNode)chapter.Nodes.Single(n => n.Id == 101)).Choices[1].Effect?.GainItem,
            "Junction 1's guess is where the flag is earned.");
        Assert.AreEqual("forestLost", ((ChoiceNode)chapter.Nodes.Single(n => n.Id == 105)).Choices[1].Effect?.GainItem,
            "Junction 2's guess earns it too, for the player who got junction 1 right.");
        Assert.IsTrue(((ChoiceNode)chapter.Nodes.Single(n => n.Id == 115)).Choices.All(c => c.Effect is null),
            "115 is only reachable from 103, which already awarded forestLost: awarding it again would be dead JSON.");

        // The 2x2 grid. Junction 1 picks WHICH copy of junction 2 you get; the four leaves then
        // collapse onto three arrival tiers by how many guesses were made, not by which ones.
        Assert.AreEqual(105, chapter.Nodes.Single(n => n.Id == 102).ChildId, "Climbing at junction 1 reaches the clean copy of junction 2.");
        Assert.AreEqual(115, chapter.Nodes.Single(n => n.Id == 103).ChildId, "Guessing at junction 1 reaches the adrift copy of junction 2.");

        foreach ((int leaf, int clearing, int arrival) in new[]
        {
            (106, 108, 110), // no mistakes    -> mid-afternoon
            (107, 109, 112), // guessed at 105 -> late afternoon
            (116, 118, 112), // guessed at 101 -> late afternoon, the same tier
            (117, 119, 111), // guessed twice  -> dusk
        })
        {
            Assert.AreEqual(clearing, chapter.Nodes.Single(n => n.Id == leaf).ChildId, $"Leaf {leaf} should lead to clearing {clearing}.");
            Assert.AreEqual(arrival, chapter.Nodes.Single(n => n.Id == clearing).ChildId, $"Clearing {clearing} should arrive at {arrival}.");
        }

        // Every arrival rejoins at 12, so no branch of the grid can strand the player.
        foreach (int arrival in new[] { 110, 111, 112 })
            Assert.AreEqual(12, chapter.Nodes.Single(n => n.Id == arrival).ChildId, $"Arrival {arrival} should rejoin the spine at node 12.");
    }

    /// <summary>
    /// Node 6 is the one place in the chapter where a gate could strand the player: its only
    /// interesting object is behind corollaArmed. The fallback verbs must therefore carry the
    /// childid unconditionally, so a player without the flag still gets out of the scene.
    /// </summary>
    [TestMethod]
    public void Chapter20Node6_HasAnUnconditionalWayOutOfTheRiflePayoff()
    {
        Chapter chapter = GetScopedChapters(BuildScopedEngine(20)).Single();
        ActionNode node6 = (ActionNode)chapter.Nodes.Single(n => n.Id == 6);

        List<Kriss.Models.Action> advancing = [.. node6.Actions.Where(a => a.ChildId.HasValue)];

        Assert.AreEqual(1, advancing.Count, "Exactly one action should leave node 6.");
        Assert.AreEqual(7, advancing[0].ChildId);
        Assert.AreEqual(0, advancing[0].Objects.Count, "The way out must not need an object to be guessed by name.");
        Assert.IsTrue(advancing[0].Verbs.Count > 1, "Several plain verbs should reach it.");

        ActionObject rifle = node6.Actions.SelectMany(a => a.Objects).Single(o => o.Objs.Contains("rifle"));
        Assert.AreEqual("corollaArmed", rifle.Condition.Item);
        Assert.IsFalse(string.IsNullOrWhiteSpace(rifle.Condition.Refusal),
            "An item condition still lists its object, so it needs a refusal to play when it is not met.");
        Assert.IsFalse(rifle.ChildId.HasValue, "The payoff is flavour: it must hand the prompt back, not advance.");
    }
}
