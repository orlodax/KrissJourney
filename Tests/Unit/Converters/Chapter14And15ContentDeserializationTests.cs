using System.Collections.Generic;
using System.Linq;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Unit.Converters;

/// <summary>
/// Spot-checks that issue 7's real c14.json / c15.json content deserializes into the
/// expected C# fields through the production <see cref="NodeJsonConverter"/> pipeline
/// (<see cref="GameEngine.Run"/>), rather than a hand-written fixture. Unlike
/// <see cref="SurgeDeserializationTests"/> (which proves the converter handles the "surge"
/// type shape in the abstract) this proves the two new node types actually AUTHORED for
/// this issue - the Surge door challenges and the Dialogue reply/nextline jumps - carry the
/// exact values the content report describes, and that nothing was silently dropped by
/// System.Text.Json's default "ignore unknown/mismatched property" behaviour.
/// </summary>
[TestClass]
public class Chapter14And15ContentDeserializationTests
{
    Chapter c14;
    Chapter c15;

    [TestInitialize]
    public void TestInitialize()
    {
        GameEngine gameEngine = GameEngineTestExtensions.Setup();
        List<Chapter> chapters = gameEngine.GetChapters();

        c14 = chapters.Single(c => c.Id == 14);
        c15 = chapters.Single(c => c.Id == 15);
    }

    [TestMethod]
    public void Chapter14_Node20_IsTheTutorialGradeFirstSurgeAttempt()
    {
        SurgeNode node20 = (SurgeNode)c14.Nodes.Single(n => n.Id == 20);

        Assert.AreEqual(3, node20.ChildId, "Node 20 should advance to node 3 (the door opening) on success.");
        Assert.IsNotNull(node20.Challenge, "Node 20 must carry a challenge object.");
        Assert.AreEqual("LURDLURD", node20.Challenge.Sequence);
        Assert.AreEqual(8, node20.Challenge.SequenceLength);
        Assert.AreEqual(18f, node20.Challenge.DrainRate);
        Assert.AreEqual(8f, node20.Challenge.Restore);
        Assert.AreEqual(5f, node20.Challenge.Penalty);
        Assert.AreEqual(100f, node20.Challenge.StartingRage);
        Assert.AreEqual(100f, node20.Challenge.MaxRage);
        Assert.AreEqual(21, node20.Challenge.FailureChildId,
            "Node 20's failed attempt must route to node 21 (guards approach), not retry in place.");
    }

    [TestMethod]
    public void Chapter14_Node22_IsTheEscalatedSecondSurgeAttemptWithInPlaceRetry()
    {
        SurgeNode node22 = (SurgeNode)c14.Nodes.Single(n => n.Id == 22);

        Assert.AreEqual(3, node22.ChildId, "Node 22's success must converge on node 3, same as node 20's.");
        Assert.IsNotNull(node22.Challenge);
        Assert.AreEqual("DRULDRUL", node22.Challenge.Sequence);
        Assert.AreEqual(25f, node22.Challenge.DrainRate);
        Assert.AreEqual(6f, node22.Challenge.Restore);
        Assert.AreEqual(8f, node22.Challenge.Penalty);
        Assert.IsNull(node22.Challenge.FailureChildId,
            "Node 22 must have no failurechildid: a failed second attempt retries the row in place, per SurgeNode.Load.");
    }

    [TestMethod]
    public void Chapter14_Node5_EfeliahReplies_BothJumpToJoeExplains()
    {
        DialogueNode node5 = (DialogueNode)c14.Nodes.Single(n => n.Id == 5);

        DialogueLine withReplies = node5.Dialogues.Single(d => d.Replies is { Count: > 0 });
        Assert.AreEqual(2, withReplies.Replies.Count);
        Assert.AreEqual("\"What happened to it?\"", withReplies.Replies[0].Line);
        Assert.AreEqual("\"How did Joe get his hands on something like this?\"", withReplies.Replies[1].Line);

        foreach (Reply reply in withReplies.Replies)
        {
            Assert.AreEqual("joeExplains", reply.NextLine, "Both replies on node 5 should jump to the same explanation.");
            Assert.IsNull(reply.ChildId, "These replies jump within the node (nextline), not to another node (childid).");
        }

        Assert.IsTrue(node5.Dialogues.Any(d => d.LineName == "joeExplains"),
            "Node 5 must contain a line named 'joeExplains' for the replies to resolve to.");
    }

    [TestMethod]
    public void Chapter14_Node8_KrissRisesReply_LeadsToNode90()
    {
        DialogueNode node8 = (DialogueNode)c14.Nodes.Single(n => n.Id == 8);

        DialogueLine withReplies = node8.Dialogues.Single(d => d.Replies is { Count: > 0 });
        Assert.AreEqual(3, withReplies.Replies.Count);
        foreach (Reply reply in withReplies.Replies)
            Assert.AreEqual("krissRises", reply.NextLine);

        DialogueLine krissRises = node8.Dialogues.Single(d => d.LineName == "krissRises");
        Assert.AreEqual(90, krissRises.ChildId, "krissRises should hand off to node 90 (Kriss takes the seat).");
    }

    [TestMethod]
    public void Chapter14_HasNoNodeNumberedNineOrTenOrElevenOrOneTenOrTwelve()
    {
        // Issue 7 moves the old nodes 9, 10, 11, 110 and 12 out of c14 and into c15.
        int[] movedIds = [9, 10, 11, 110, 12];
        foreach (int id in movedIds)
            Assert.IsFalse(c14.Nodes.Any(n => n.Id == id),
                $"Node {id} should have moved to c15, but is still present in c14.");
    }

    [TestMethod]
    public void Chapter15_HasFortyFourNodesInContiguousOrder()
    {
        // Issue 8 replaced c15's 6-node stub (1, 10, 11, 110, 12, 13 moved from c14 under
        // issue 7) with 31 authored nodes; splitting the banquet setup (old node 16) into
        // three Story beats added 32 and 33, and turning the award ceremony (node 19) from one
        // Action node into a Choice hub gave each prize its own branch, 34-39. Splitting the
        // departure (node 23) so the travel clothes and the councillors' send-off narrate as
        // one Story beat before Riff speaks added 40. Issue 23 then gave node 24's send-off
        // poll a spoken beat per answer instead of routing every branch straight at node 25,
        // adding 41-44, so ids 1-44 are contiguous.
        List<int> ids = [.. c15.Nodes.Select(n => n.Id).OrderBy(i => i)];
        CollectionAssert.AreEqual(Enumerable.Range(1, 44).ToList(), ids);
    }

    [TestMethod]
    public void Chapter15_Node27_IsTheOnlyIsLastNode()
    {
        List<NodeBase> lastNodes = [.. c15.Nodes.Where(n => n.IsLast)];
        Assert.AreEqual(1, lastNodes.Count);
        Assert.AreEqual(27, lastNodes[0].Id);
    }

    /// <summary>
    /// Node 17's "ask/talk/say" verb group is the setup half of issue 8's gating: matching the
    /// "math"/"projector" object must silently grant heardMathMotive (no childid of its own -
    /// the player stays in the Action prompt), and the "wait/continue/listen" verb group must
    /// carry the gate itself: a conditionless-typed Condition (defaults to an inventory check
    /// in GameEngine.Evaluate) on that same item, with its own refusal text, advancing to
    /// node 18 only once satisfied.
    /// </summary>
    [TestMethod]
    public void Chapter15_Node17_AskMathObject_GrantsHeardMathMotive_AndGatesTheWaitVerb()
    {
        ActionNode node17 = (ActionNode)c15.Nodes.Single(n => n.Id == 17);

        Kriss.Models.Action askAction = node17.Actions.Single(a => a.Verbs.Contains("ask"));
        CollectionAssert.AreEquivalent(new List<string> { "ask", "talk", "say" }, askAction.Verbs);

        ActionObject mathObject = askAction.Objects.Single(o => o.Objs.Contains("math"));
        CollectionAssert.AreEquivalent(new List<string> { "math", "projector" }, mathObject.Objs);
        Assert.IsNotNull(mathObject.Effect, "Asking about the Projector/Math must carry an effect.");
        Assert.AreEqual("heardMathMotive", mathObject.Effect.GainItem);
        Assert.IsNull(mathObject.ChildId, "Matching an object mid-conversation should not advance the node on its own.");

        Kriss.Models.Action waitAction = node17.Actions.Single(a => a.Verbs.Contains("wait"));
        CollectionAssert.AreEquivalent(new List<string> { "wait", "continue", "listen" }, waitAction.Verbs);
        Assert.IsNotNull(waitAction.Condition, "The wait verb must be gated.");
        Assert.AreEqual("heardMathMotive", waitAction.Condition.Item);
        Assert.IsFalse(string.IsNullOrEmpty(waitAction.Condition.Refusal));
        Assert.AreEqual(18, waitAction.ChildId);
    }

    [TestMethod]
    public void Chapter15_Node17_AskEfeliahObject_GrantsHeardRockHistory()
    {
        ActionNode node17 = (ActionNode)c15.Nodes.Single(n => n.Id == 17);
        Kriss.Models.Action askAction = node17.Actions.Single(a => a.Verbs.Contains("ask"));

        ActionObject rockObject = askAction.Objects.Single(o => o.Objs.Contains("rock"));
        CollectionAssert.AreEquivalent(new List<string> { "efeliah", "rock", "founder", "master" }, rockObject.Objs);
        Assert.IsNotNull(rockObject.Effect);
        Assert.AreEqual("heardRockHistory", rockObject.Effect.GainItem);
    }

    /// <summary>
    /// Node 19's award ceremony is a Choice hub, not a text parser: every prize on that stage
    /// is a listed choice, so nothing has to be guessed by name. It unlocks progressively, and
    /// entirely through isNodeVisited so that a locked prize is not listed at all: Kriss's first
    /// prize is the only thing on offer, claiming it opens his second, that one opens the four
    /// companion branches in any order, and only all four together open the exit. Every branch
    /// loops back to the hub, which together make it impossible to leave the stage early.
    /// </summary>
    [TestMethod]
    public void Chapter15_Node19_IsAnAwardHubNobodyCanLeaveEmptyHanded()
    {
        ChoiceNode node19 = (ChoiceNode)c15.Nodes.Single(n => n.Id == 19);
        Assert.AreEqual(7, node19.Choices.Count, "Two prizes for Kriss, one per companion group, plus the exit.");

        Choice sphere = node19.Choices[0];
        Assert.IsNull(sphere.Condition, "Kriss's first prize is the only choice that is open from the start.");
        Assert.AreEqual("lightSphere", sphere.Effect.GainItem,
            "The sphere is Kriss's own prize, and c20 node 16 gates the camp-light beat on it.");
        Assert.AreEqual(34, sphere.ChildId);

        Choice dagger = node19.Choices[1];
        Assert.AreEqual("isNodeVisited", dagger.Condition.Type);
        Assert.AreEqual("34", dagger.Condition.Item, "The second prize only appears once the first has been played out.");
        Assert.AreEqual("daggerReplica", dagger.Effect.GainItem);
        Assert.AreEqual(35, dagger.ChildId);

        List<Choice> companions = [.. node19.Choices.Skip(2).Take(4)];
        CollectionAssert.AreEqual(new List<int> { 36, 37, 38, 39 }, companions.ConvertAll(c => c.ChildId),
            "One branch each for Corolla, Theo, Smiurl, and Efeliah with Math.");
        foreach (Choice companion in companions)
        {
            Assert.AreEqual("isNodeVisited", companion.Condition.Type);
            Assert.AreEqual("35", companion.Condition.Item,
                "A companion's prize only appears once Kriss has taken both of his own; the four are then free in any order.");
        }

        // Two of the companion prizes are read back by later content, and both gates are item
        // conditions, so the scene is still offered without the flag and answers with a refusal:
        // c20 node 6 gates "ask about the rifle" on corollaArmed, and c16 node 17 gates asking
        // Efeliah what she was listening to on mathAmplifier. Watching those two prizes handed
        // over here is what earns those scenes.
        Assert.AreEqual("corollaArmed", companions[0].Effect?.GainItem,
            "Corolla's branch must set the flag c20's rifle payoff gates on.");
        Assert.AreEqual("mathAmplifier", companions[3].Effect?.GainItem,
            "Efeliah and Math's branch must set the flag c16 node 17's canyon-camp payoff gates on.");
        foreach (Choice companion in companions.Skip(1).Take(2))
            Assert.IsNull(companion.Effect, "Theo's and Smiurl's prizes are theirs, and nothing later reads them back.");

        Choice leave = node19.Choices[6];
        Assert.AreEqual(20, leave.ChildId);
        Assert.IsNull(leave.Condition.Item, "Leaving is gated on a group, not on any single thing.");
        CollectionAssert.AreEqual(new List<string> { "36", "37", "38", "39" }, leave.Condition.All.ConvertAll(c => c.Item),
            "The exit stays out of the list until all four companion prizes have been watched.");
        Assert.IsTrue(leave.Condition.All.TrueForAll(c => c.Type == "isNodeVisited"));

        foreach (Choice gated in node19.Choices.Where(c => c.Condition != null))
            Assert.IsTrue(string.IsNullOrEmpty(gated.Refusal),
                "Nothing here is refused: an unmet isNodeVisited hides its choice, so a refusal line would be dead text.");

        foreach (int storyBranchId in new[] { 34, 35, 36, 38 })
            Assert.AreEqual(19, c15.Nodes.Single(n => n.Id == storyBranchId).ChildId,
                "Every prize branch returns to the ceremony hub.");

        foreach (int dialogueBranchId in new[] { 37, 39 })
        {
            DialogueNode branch = (DialogueNode)c15.Nodes.Single(n => n.Id == dialogueBranchId);
            Assert.AreEqual(19, branch.Dialogues.Last().ChildId.Value,
                "Theo's and Math's prize scenes are spoken dialogue, and they return to the hub too.");
        }
    }

    /// <summary>
    /// Node 24 is the payoff half of issue 8's Kriss/Math poll: three choices, each gated on
    /// isNodeVisited against a different one of nodes 9/10/11 (the branches of node 8), all
    /// converging on node 25. ChoiceNode.DisplayChoices only shows the choice whose condition
    /// currently passes, so exactly one is visible at a time - proven at the engine level by
    /// Chapter14And15WalkthroughTests, not here.
    /// </summary>
    [TestMethod]
    public void Chapter15_Node24_ChoicesAreGatedOnNodesNineTenAndEleven_PlusAnUngatedFallback()
    {
        ChoiceNode node24 = (ChoiceNode)c15.Nodes.Single(n => n.Id == 24);

        Assert.AreEqual(4, node24.Choices.Count, "Three callbacks plus the ungated fallback.");

        List<Choice> gated = [.. node24.Choices.Where(c => c.Condition is not null)];
        Assert.AreEqual(3, gated.Count);

        List<string> expectedItems = ["9", "10", "11"];
        foreach (Choice choice in gated)
        {
            Assert.AreEqual("isNodeVisited", choice.Condition.Type);
            Assert.IsTrue(expectedItems.Remove(choice.Condition.Item),
                $"Unexpected or duplicate isNodeVisited target: {choice.Condition.Item}");
        }

        Assert.AreEqual(0, expectedItems.Count, $"Missing isNodeVisited targets: {string.Join(", ", expectedItems)}.");

        // An unmet isNodeVisited keeps its choice OUT of the list entirely, so the poll needs one
        // choice nobody's history can hide, or a player who somehow reached node 24 having visited
        // none of 9/10/11 would face an empty hub. 2026-09-05
        Assert.AreEqual(1, node24.Choices.Count(c => c.Condition is null),
            "Exactly one choice must be ungated, so the hub can never render empty.");

        // Issue 23 gave each answer its own spoken beat instead of routing all of them at node 25
        // unspoken; the four Story nodes then converge on 25, so the poll still costs one node.
        CollectionAssert.AreEqual(new List<int> { 41, 42, 43, 44 }, node24.Choices.ConvertAll(c => c.ChildId),
            "Each answer gets its own Story beat, in the order the choices are listed.");

        foreach (int beatId in new[] { 41, 42, 43, 44 })
            Assert.AreEqual(25, c15.Nodes.Single(n => n.Id == beatId).ChildId,
                $"Node {beatId} should rejoin the spine at node 25.");
    }
}
