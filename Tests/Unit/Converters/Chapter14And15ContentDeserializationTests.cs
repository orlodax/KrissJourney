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
    public void Chapter15_HasExactlyTheFiveDocumentedNodes()
    {
        List<int> ids = [.. c15.Nodes.Select(n => n.Id).OrderBy(i => i)];
        CollectionAssert.AreEqual(new List<int> { 1, 10, 11, 12, 110 }, ids);
    }

    [TestMethod]
    public void Chapter15_Node10_CorollaLine_LeadsToNode11()
    {
        DialogueNode node10 = (DialogueNode)c15.Nodes.Single(n => n.Id == 10);
        DialogueLine last = node10.Dialogues.Last();

        Assert.AreEqual(EnCharacter.Corolla, last.Actor);
        Assert.AreEqual(11, last.ChildId);
    }

    [TestMethod]
    public void Chapter15_Node110_KrissLine_LeadsToNode12()
    {
        DialogueNode node110 = (DialogueNode)c15.Nodes.Single(n => n.Id == 110);
        DialogueLine last = node110.Dialogues.Last();

        Assert.AreEqual(EnCharacter.Kriss, last.Actor);
        Assert.AreEqual(12, last.ChildId);
    }

    [TestMethod]
    public void Chapter15_Node12_IsTheOnlyIsLastNode()
    {
        List<NodeBase> lastNodes = [.. c15.Nodes.Where(n => n.IsLast)];
        Assert.AreEqual(1, lastNodes.Count);
        Assert.AreEqual(12, lastNodes[0].Id);
    }
}
