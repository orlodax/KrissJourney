using System;
using System.Linq;
using System.Threading.Tasks;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Integration;

/// <summary>
/// Drives c21 ("THE WHITE CITY"), reshaped by this batch, through a real
/// <see cref="GameEngine"/>/TerminalMock pair on the <see cref="ChapterWalkthroughTestBase"/>
/// harness.
///
/// The chapter's spine is the dream-flight: five Choice nodes (5, 6, 7, 8, 9), each a hub whose
/// spokes are one-shot looks that return to it, plus one advance. Only the first hub gates its
/// advance - "Follow the call" stays out of the list until node 51 has shown Kriss where the
/// call comes from - so the first two walks below are the two shapes the flight can take: look
/// at everything, or take only what the chapter insists on. The third walk is not about the
/// flight at all: it covers node 4's shrinking reply blocks, the one place in the game where a
/// Dialogue node's highlight can be left pointing past the block being drawn.
/// </summary>
[TestClass]
public class Chapter21WalkthroughTests : ChapterWalkthroughTestBase
{
    [TestMethod]
    [Timeout(120000)]
    public void FullDreamFlight_VisitsEverySpokeAndWakesInTheGardens()
    {
        GameEngine engine = BuildScopedEngine(21);
        SetCurrentChapter(engine, 21);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                                   // node 1 -> node 4

            idx = await ChooseAsync(idx, "Put it off. You'll think about it when you're there.");  // node 4: 1st reply block, index 0
            idx = await ChooseAsync(idx, "Then that settles it: she guides.");                 // node 4: 2nd reply block, index 0 (selectedRow is still 0)
            idx = await ContinueAsync(idx);                                                   // node 4: Kriss's childid line -> node 5

            // Hub 1. The advance is hidden until node 51 has been seen, so the call has to come first.
            idx = await ChooseAsync(idx, "Look out toward the city, far off in the dark.", ConsoleKey.DownArrow); // -> node 51
            idx = await ContinueAsync(idx);                                                   // node 51 -> node 5
            idx = await ChooseAsync(idx, "Look at what is moving in the forest around you.", ConsoleKey.UpArrow); // -> node 50
            idx = await ContinueAsync(idx);                                                   // node 50 -> node 5
            idx = await ChooseAsync(idx, "Follow the call.", ConsoleKey.DownArrow, ConsoleKey.DownArrow); // -> node 6

            // Hub 2.
            idx = await ChooseAsync(idx, "Look at what the army has done to the ground.");     // -> node 52
            idx = await ContinueAsync(idx);                                                   // node 52 -> node 6
            idx = await ChooseAsync(idx, "Look behind you, without turning.", ConsoleKey.DownArrow); // -> node 53
            idx = await ContinueAsync(idx);                                                   // node 53 -> node 6
            idx = await ChooseAsync(idx, "Sail on toward the walls.", ConsoleKey.DownArrow);   // -> node 7

            // Hub 3.
            idx = await ChooseAsync(idx, "Look up at the walls.");                             // -> node 54
            idx = await ContinueAsync(idx);                                                   // node 54 -> node 7
            idx = await ChooseAsync(idx, "Go up.", ConsoleKey.DownArrow);                      // -> node 8

            // Hub 4.
            idx = await ChooseAsync(idx, "Look at the lit window in the palace.");             // -> node 55
            idx = await ContinueAsync(idx);                                                   // node 55 -> node 8
            idx = await ChooseAsync(idx, "Go down into the gardens.", ConsoleKey.DownArrow);   // -> node 9

            // Hub 5.
            idx = await ChooseAsync(idx, "Stay a moment among the trees and the fountains.");  // -> node 56
            idx = await ContinueAsync(idx);                                                   // node 56 -> node 9
            idx = await ChooseAsync(idx, "Go up the stair.", ConsoleKey.DownArrow);            // -> node 10

            idx = await ContinueAsync(idx);                                                   // node 10 -> node 11
            idx = await ContinueAsync(idx);                                                   // node 11 -> node 12

            idx = await ChooseAsync(idx, "Tell them about the staircase.");                    // node 12 -> "krisswake1"
            idx = await ContinueAsync(idx);                                                   // node 12: "climb" childid line -> node 13

            idx = await ContinueAsync(idx);                                                   // node 13 (islast) -> StartNextChapter(22)
        });

        RunWalkToChapterEnd(engine, script, startNodeId: 1, expectedNextChapterId: 22);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("CHAPTER 21"), "Should have rendered c21's own header at node 1.");
        Assert.IsTrue(output.Contains("We'll think about that when we're there..."), "Should speak the first reply block's first option.");
        Assert.IsTrue(output.Contains("Then you'll guide us."), "Should speak the second reply block's first option.");

        foreach ((int spoke, string marker) in new[]
        {
            (50, "Not one of them is able to tell that anything"),
            (51, "The city is a pale shape in the dark"),
            (52, "You sail over it like some spectral ship"),
            (53, "kind that runs ahead of the day-star"),
            (54, "mighty bulwarks, immense and old"),
            (55, "It trembles as though it can feel you coming."),
            (56, "The trees here are enchanting and still fast asleep"),
        })
            Assert.IsTrue(output.Contains(marker), $"Spoke {spoke} should have been visited on the full flight.");

        Assert.IsTrue(output.Contains("a figure at the top of the staircase"), "Should reach node 10, the figure on the stair.");
        Assert.IsTrue(output.Contains("I saw that staircase last night."), "Should take node 12's first reply.");
        Assert.IsTrue(output.Contains("a vast atrium at the head"), "Should reach node 13, the veiled atrium that ends the chapter.");

        // The gate: the advance out of hub 1 must not be listed before node 51 has been seen.
        int callHeard = output.IndexOf("The city is a pale shape in the dark", StringComparison.Ordinal);
        int advanceOffered = output.IndexOf("Follow the call.", StringComparison.Ordinal);
        Assert.IsTrue(callHeard >= 0 && advanceOffered > callHeard,
            "\"Follow the call\" is gated on isNodeVisited 51, so it must not appear in the list before node 51 has played.");
    }

    [TestMethod]
    [Timeout(120000)]
    public void MinimalDreamFlight_SkipsEverySpokeItCanAndStillReachesTheAtrium()
    {
        GameEngine engine = BuildScopedEngine(21);
        SetCurrentChapter(engine, 21);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                                   // node 1 -> node 4

            idx = await ChooseAsync(idx, "Tell her the gates will not be what stops you.", ConsoleKey.DownArrow); // node 4: 1st block, index 1
            idx = await ChooseAsync(idx, "Say nothing, and let it stand.");                    // node 4: 2nd block, index 1 (selectedRow carried over)
            idx = await ContinueAsync(idx);                                                   // node 4 -> node 5

            // Node 51 is the one spoke the chapter will not let anyone skip: it unlocks the advance.
            idx = await ChooseAsync(idx, "Look out toward the city, far off in the dark.", ConsoleKey.DownArrow);
            idx = await ContinueAsync(idx);                                                   // node 51 -> node 5
            idx = await ChooseAsync(idx, "Follow the call.", ConsoleKey.DownArrow);            // -> node 6

            idx = await ChooseAsync(idx, "Sail on toward the walls.", ConsoleKey.DownArrow, ConsoleKey.DownArrow); // node 6 -> node 7
            idx = await ChooseAsync(idx, "Go up.", ConsoleKey.DownArrow);                      // node 7 -> node 8
            idx = await ChooseAsync(idx, "Go down into the gardens.", ConsoleKey.DownArrow);   // node 8 -> node 9
            idx = await ChooseAsync(idx, "Go up the stair.", ConsoleKey.DownArrow);            // node 9 -> node 10

            idx = await ContinueAsync(idx);                                                   // node 10 -> node 11
            idx = await ContinueAsync(idx);                                                   // node 11 -> node 12

            idx = await ChooseAsync(idx, "Say nothing at all.", ConsoleKey.DownArrow, ConsoleKey.DownArrow); // node 12 -> "krisswake3"
            idx = await ContinueAsync(idx);                                                   // node 12 -> node 13

            idx = await ContinueAsync(idx);                                                   // node 13 (islast) -> StartNextChapter(22)
        });

        RunWalkToChapterEnd(engine, script, startNodeId: 1, expectedNextChapterId: 22);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("The gates will not be what stops us."), "Should speak the first block's second option.");
        Assert.IsTrue(output.Contains("You nod, once, and leave it there."), "Should take the second block's silent option.");
        Assert.IsTrue(output.Contains("The city is a pale shape in the dark"), "The unlocking spoke is not skippable.");
        Assert.IsTrue(output.Contains("You do not answer him."), "Should take node 12's silent reply.");
        Assert.IsTrue(output.Contains("a vast atrium at the head"), "The short flight must still reach node 13.");

        foreach ((int spoke, string marker) in new[]
        {
            (50, "Not one of them is able to tell that anything"),
            (52, "You sail over it like some spectral ship"),
            (53, "kind that runs ahead of the day-star"),
            (54, "mighty bulwarks, immense and old"),
            (55, "It trembles as though it can feel you coming."),
            (56, "The trees here are enchanting and still fast asleep"),
        })
            Assert.IsFalse(output.Contains(marker), $"Spoke {spoke} is optional and must not play on the short flight.");
    }

    /// <summary>
    /// Node 4 is the only place in the game where one Dialogue node's second reply block is
    /// smaller than its first (3 options, then 2), and selectedRow is node-level, so the second
    /// block is drawn with a highlight the first block put out of range. Replaces the static
    /// lint that used to forbid the shape outright: it could only read JSON, so it could not
    /// see the clamp that makes the shape safe, and it would have forced this content to pad.
    /// </summary>
    [TestMethod]
    [Timeout(60000)]
    public void HighReplyRowThenAShorterBlock_ClampsTheHighlightInsteadOfCrashing()
    {
        GameEngine engine = BuildScopedEngine(21);
        SetCurrentChapter(engine, 21);

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                                   // node 1 -> node 4

            // 3-option block: walk the highlight all the way down to row 2, then take it.
            idx = await ChooseAsync(idx, "Say nothing, and keep looking into the dark.", ConsoleKey.DownArrow, ConsoleKey.DownArrow);

            // 2-option block, entered with selectedRow still at 2 and no navigation of its own.
            // An unclamped Enter here indexes Replies[2] on a 2-element list; that would leave
            // ChapterWalkthroughTestBase with an ArgumentOutOfRangeException it does not catch,
            // so reaching the assertions below at all is half of what this test proves.
            idx = await ChooseAsync(idx, "Say nothing, and let it stand.");

            // Stops at node 4's own exit pause rather than walking the rest of the chapter.
            await WaitForOutputIndexAsync("Then it's decided.", idx);
        });

        RunWalkToUnansweredPrompt(engine, script, startNodeId: 1);

        string output = Terminal.GetOutput();
        Assert.IsTrue(output.Contains("You do not answer her."),
            "The first block's third option should have been taken, which is what puts selectedRow out of the second block's range.");
        Assert.IsTrue(output.Contains("You nod, once, and leave it there."),
            "The clamp lands on the shorter block's LAST row: it pins the highlight to what exists rather than resetting it to the top.");
        Assert.IsFalse(output.Contains("Then you'll guide us."),
            "Resetting to row 0 instead of clamping would have taken the second block's first option.");
    }

    /// <summary>
    /// Structural pass over the reshaped chapter: ids unique, every reference resolves, exactly
    /// one reachable islast, and - the shape the reshape is actually about - every spoke returns
    /// to its own hub, so no look can strand the dream.
    /// </summary>
    [TestMethod]
    public void Chapter21Structure_EverySpokeReturnsToItsHubAndOneNodeEndsIt()
    {
        Chapter chapter = GetScopedChapters(BuildScopedEngine(21)).Single();

        Assert.AreEqual(21, chapter.Id, "The Id field inside c21.json must match its filename.");
        ChapterStructureAssertions.AssertReferencesResolve(chapter);
        ChapterStructureAssertions.AssertChapterEndsAt(chapter, endsAtNodeId: 13);

        foreach ((int hub, int[] spokes) in new[]
        {
            (5, new[] { 50, 51 }),
            (6, new[] { 52, 53 }),
            (7, new[] { 54 }),
            (8, new[] { 55 }),
            (9, new[] { 56 }),
        })
        {
            ChoiceNode hubNode = (ChoiceNode)chapter.Nodes.Single(n => n.Id == hub);

            foreach (int spoke in spokes)
            {
                Choice look = hubNode.Choices.Single(c => c.ChildId == spoke);
                Assert.IsTrue(look.IsNotRepeatable, $"Hub {hub}'s look at {spoke} is a one-shot: it must be isnotrepeatable.");
                Assert.AreEqual(hub, chapter.Nodes.Single(n => n.Id == spoke).ChildId,
                    $"Spoke {spoke} must return to hub {hub}.");
            }

            Assert.AreEqual(1, hubNode.Choices.Count(c => !spokes.Contains(c.ChildId)),
                $"Hub {hub} should have exactly one way onward besides its looks.");
        }

        // Only the first hub gates its advance, and it gates on the spoke that supplies the reason.
        Choice followTheCall = ((ChoiceNode)chapter.Nodes.Single(n => n.Id == 5)).Choices.Single(c => c.ChildId == 6);
        Assert.AreEqual("isNodeVisited", followTheCall.Condition.Type,
            "isNodeVisited keeps an unmet choice out of the list entirely, which is what an unheard call should do.");
        Assert.AreEqual("51", followTheCall.Condition.Item);
        Assert.IsTrue(string.IsNullOrEmpty(followTheCall.Refusal),
            "A hidden choice never plays a refusal, so one here would be dead text.");

        foreach (int hub in new[] { 6, 7, 8, 9 })
            Assert.IsTrue(((ChoiceNode)chapter.Nodes.Single(n => n.Id == hub)).Choices.All(c => c.Condition == null),
                $"Hub {hub}'s advance is ungated: the flight only ever waits on the call.");
    }
}
