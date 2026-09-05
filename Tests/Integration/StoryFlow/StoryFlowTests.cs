using System;
using System.Collections.Generic;
using System.Linq;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Integration.StoryFlow;

[TestClass]
public class StoryFlowTests
{
    GameEngine gameEngine;

    [TestInitialize]
    public void TestInitialize()
    {
        gameEngine = GameEngineTestExtensions.Setup();
    }

    [TestMethod]
    public void AllChapters_HaveValidStructure()
    {
        // Check all chapters have at least one node
        foreach (Chapter chapter in gameEngine.GetChapters())
        {
            Assert.IsTrue(chapter.Nodes.Count > 0, $"Chapter {chapter.Id} has no nodes");

            // Check all nodes have valid IDs
            foreach (NodeBase node in chapter.Nodes)
                Assert.IsTrue(node.Id > 0, $"Node in chapter {chapter.Id} has invalid ID: {node.Id}");
        }
    }

    [TestMethod]
    public void AllChapters_ValidateChildNodes()
    {
        // Check that all referenced child nodes exist
        foreach (Chapter chapter in gameEngine.GetChapters())
        {
            foreach (NodeBase node in chapter.Nodes)
            {
                // If childId is not 0, it should point to a valid node
                if (node.ChildId > 0)
                {
                    bool childExists = chapter.Nodes.Any(n => n.Id == node.ChildId);
                    Assert.IsTrue(childExists,
                        $"Node {node.Id} in chapter {chapter.Id} references non-existent child node {node.ChildId}");
                }

                // Check specific node types for their child references
                if (node is ChoiceNode choiceNode)
                {
                    foreach (Choice choice in choiceNode.Choices)
                    {
                        if (choice.ChildId > 0)
                        {
                            bool childExists = chapter.Nodes.Any(n => n.Id == choice.ChildId);
                            Assert.IsTrue(childExists,
                                $"Choice in node {node.Id}, chapter {chapter.Id} references non-existent child node {choice.ChildId}");
                        }
                    }
                }
                else if (node is ActionNode actionNode && node is not MiniGame01)
                {
                    foreach (Kriss.Models.Action action in actionNode.Actions)
                    {
                        if (action.ChildId.HasValue)
                        {
                            bool childExists = chapter.Nodes.Any(n => n.Id == action.ChildId.Value);
                            Assert.IsTrue(childExists,
                                $"Action in node {node.Id}, chapter {chapter.Id} references non-existent child node {action.ChildId}");
                        }

                        // Check action objects for child references
                        foreach (ActionObject obj in action.Objects)
                        {
                            if (obj.ChildId.HasValue)
                            {
                                bool childExists = chapter.Nodes.Any(n => n.Id == obj.ChildId.Value);
                                Assert.IsTrue(childExists,
                                    $"Action object in node {node.Id}, chapter {chapter.Id} references non-existent child node {obj.ChildId}");
                            }
                        }
                    }
                }
                else if (node is DialogueNode dialogueNode)
                {
                    foreach (DialogueLine dialogue in dialogueNode.Dialogues)
                    {
                        if (dialogue.ChildId.HasValue)
                        {
                            bool childExists = chapter.Nodes.Any(n => n.Id == dialogue.ChildId.Value);
                            Assert.IsTrue(childExists,
                                $"Dialogue in node {node.Id}, chapter {chapter.Id} references non-existent child node {dialogue.ChildId}");
                        }

                        // Check replies for child references
                        if (dialogue.Replies != null)
                        {
                            foreach (Reply reply in dialogue.Replies)
                            {
                                if (reply.ChildId.HasValue)
                                {
                                    bool childExists = chapter.Nodes.Any(n => n.Id == reply.ChildId.Value);
                                    Assert.IsTrue(childExists,
                                        $"Dialogue reply in node {node.Id}, chapter {chapter.Id} references non-existent child node {reply.ChildId}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    [TestMethod]
    public void AllChapters_HaveNoUnreachableOrMissingNodes()
    {
        // Load all chapters using the real loader
        foreach (Chapter chapter in GameEngineTestExtensions.Setup().GetChapters())
        {
            Dictionary<int, NodeBase> nodeMap = chapter.Nodes.ToDictionary(n => n.Id);
            HashSet<int> visited = [];
            Queue<NodeBase> queue = new();
            if (chapter.Nodes.Count == 0)
                continue;
            // Always start traversal from node 1 (canonical entry point)
            NodeBase entryNode = chapter.Nodes.FirstOrDefault(n => n.Id == 1);
            if (entryNode == null)
                continue;
            queue.Enqueue(entryNode); // Assume first node is entry

            while (queue.Count > 0)
            {
                NodeBase node = queue.Dequeue();
                if (!visited.Add(node.Id))
                    continue;

                foreach (int nextId in GetAllOutgoingLinks(node))
                {
                    if (!nodeMap.TryGetValue(nextId, out NodeBase value))
                        Assert.Fail($"Node {node.Id} in chapter {chapter.Id} references missing node {nextId}");
                    else
                        queue.Enqueue(value);
                }
            }

            // Check for unreachable nodes
            List<NodeBase> unreachable = [.. chapter.Nodes.Where(n => !visited.Contains(n.Id))];
            Assert.IsTrue(unreachable.Count == 0, $"Unreachable nodes in chapter {chapter.Id}: {string.Join(", ", unreachable.Select(n => n.Id))}");
        }
    }


    [TestMethod]
    public void AllChapters_LoadExactlyTwentyFourInContiguousOrder()
    {
        // GameEngine.Run loads embedded resource "KrissJourney.Kriss.Chapters.c{id}.json" in a
        // loop starting at id 1 and breaks at the first missing resource (see GameEngine.Run), so
        // any gap below 24 would silently truncate the back half of the story instead of failing
        // loudly. GameEngine.StartChapter also finds a chapter by its Id FIELD, not by list
        // position (chapters.Find(c => c.Id == chapterId)), so the loaded list must both have the
        // right count and have each chapter's Id field match the c{N}.json it was loaded from.
        List<Chapter> chapters = gameEngine.GetChapters();

        Assert.AreEqual(24, chapters.Count, "Expected all 24 chapters (see issue 5's renumber) to load.");

        for (int i = 0; i < chapters.Count; i++)
        {
            int expectedId = i + 1;
            Assert.AreEqual(expectedId, chapters[i].Id,
                $"Chapter loaded at position {i} should have Id {expectedId} to match its c{expectedId}.json filename.");
        }
    }

    [TestMethod]
    public void AllChapters_HaveANodeWithId1()
    {
        // Node 1 is load-bearing twice over: GameEngine.StartChapter enters a chapter through
        // LoadNode(nodeId: 1), and FightNode.GameOver restarts one the same way. A chapter
        // without it would throw ArgumentNullException on entry, and any chapter holding a Fight
        // would throw it again on every defeat.
        foreach (Chapter chapter in gameEngine.GetChapters())
            Assert.IsTrue(chapter.Nodes.Any(n => n.Id == 1), $"Chapter {chapter.Id} has no node with Id 1.");
    }

    [TestMethod]
    public void AllChapters_NodeIdsAreUniqueWithinTheChapter()
    {
        foreach (Chapter chapter in gameEngine.GetChapters())
        {
            List<int> duplicateIds = [.. chapter.Nodes
                .GroupBy(n => n.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)];

            Assert.IsTrue(duplicateIds.Count == 0,
                $"Chapter {chapter.Id} has duplicate node ids: {string.Join(", ", duplicateIds)}");
        }
    }

    [TestMethod]
    public void Chapter1_FirstNodeIsAccessible()
    {
        // Assuming chapter 1 exists and is the start of the game
        if (gameEngine.GetChapters().Count > 0)
        {
            Chapter chapter1 = gameEngine.GetChapters()[0];
            Assert.IsNotNull(chapter1, "Chapter 1 should exist");

            NodeBase firstNode = chapter1.Nodes.OrderBy(n => n.Id).FirstOrDefault();
            Assert.IsNotNull(firstNode, "Chapter 1 should have at least one node");
        }
    }

    [TestMethod]
    public void AllChapters_ValidateTextContent()
    {
        // Check that all nodes have some text content
        foreach (Chapter chapter in gameEngine.GetChapters())
        {
            foreach (NodeBase node in chapter.Nodes)
            {
                // Node-specific content validation
                if (node is ChoiceNode choiceNode)
                {
                    Assert.IsTrue(choiceNode.Choices.Count > 0,
                        $"ChoiceNode {node.Id} in chapter {chapter.Id} has no choices");
                }
                else if (node is DialogueNode dialogueNode)
                {
                    Assert.IsTrue(dialogueNode.Dialogues.Count > 0,
                        $"DialogueNode {node.Id} in chapter {chapter.Id} has no dialogues");

                    foreach (DialogueLine dialogue in dialogueNode.Dialogues)
                        if (!string.IsNullOrWhiteSpace(dialogue.Line))
                        {
                            // If it has replies, they should have text
                            if (dialogue.Replies != null && dialogue.Replies.Count > 0)
                            {
                                foreach (Reply reply in dialogue.Replies)
                                    Assert.IsFalse(string.IsNullOrWhiteSpace(reply.Line),
                                        $"Dialogue reply in node {node.Id}, chapter {chapter.Id} has no text");
                            }
                        }
                }
                else if (node is ActionNode actionNode && node is not MiniGame01)
                {
                    Assert.IsTrue(actionNode.Actions.Count > 0,
                        $"ActionNode {node.Id} in chapter {chapter.Id} has no actions");

                    foreach (Kriss.Models.Action action in actionNode.Actions)
                    {
                        Assert.IsTrue(action.Verbs.Count > 0,
                            $"Action in node {node.Id}, chapter {chapter.Id} has no objects");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Every Dialogue reply's/line's "nextline" jump must resolve to a "linename" within the SAME
    /// DialogueNode. This is not caught by <see cref="AllChapters_HaveNoUnreachableOrMissingNodes"/>,
    /// whose BFS only follows inter-node "childid" links: <see cref="GetAllOutgoingLinks"/>
    /// deliberately does not yield NextLine jumps, because they are not node ids at all.
    /// Was scoped to chapters 14/15 when only those used nextline heavily; the c16-c24 pass moved
    /// most speech into Dialogue nodes with reply jumps, so it is now the repo-wide invariant it
    /// always should have been - together with id uniqueness and childid resolution, which
    /// <see cref="ChapterStructureAssertions.AssertReferencesResolve"/> checks in the same walk.
    /// </summary>
    [TestMethod]
    public void AllChapters_NodeReferencesAndDialogueNextLineJumpsAllResolve()
    {
        foreach (Chapter chapter in gameEngine.GetChapters())
            ChapterStructureAssertions.AssertReferencesResolve(chapter);
    }

    /// <summary>
    /// Every chapter from c11 on ends in exactly one place. Game-over nodes are excluded - an
    /// isclosing Story node returns to the menu instead of starting the next chapter, so c18's
    /// drowning (node 181) legitimately sits alongside node 19, the real ending.
    /// c1-c10 are canon and are not held to this: c1 deliberately ends on either node 7 or node 8
    /// depending on where the player was standing when the voice called.
    /// </summary>
    [TestMethod]
    public void ChaptersElevenToTwentyFour_EachEndAtExactlyOneIsLastNode()
    {
        (int chapterId, int endsAt)[] endings =
        [
            (11, 10), (12, 24), (13, 13), (14, 95), (15, 27), (16, 16), (17, 15),
            (18, 19), (19, 23), (20, 15), (21, 13), (22, 24), (23, 16), (24, 15),
        ];

        foreach ((int chapterId, int endsAt) in endings)
            ChapterStructureAssertions.AssertChapterEndsAt(
                gameEngine.GetChapters().Single(c => c.Id == chapterId), endsAt);
    }

    /// <summary>
    /// Every "gainitem" a chapter awards is either read back by a later Condition, or is on the
    /// list below of flags that are knowingly awarded and never gated. This is not a style rule:
    /// an ungated flag is usually the visible half of a setup whose payoff was cut, or a payoff
    /// whose gate was written against the wrong name, and both look identical in the JSON.
    /// The list is deliberately explicit so that a NEW ungated flag fails here and has to be
    /// argued for, rather than joining the pile silently.
    /// </summary>
    [TestMethod]
    public void AllChapters_EveryAwardedFlagIsEitherGatedOrKnowinglyUnspent()
    {
        // Each of these was checked by hand; the reason it is here is beside it.
        HashSet<string> knowinglyUnspent =
        [
            "securedRigging",      // c18: the partner of corollaSecuredBelow. c19 node 17 reads the
                                   // pair by checking only corollaSecuredBelow, so this flag's whole
                                   // meaning is carried by the OTHER one's absence. Not dead.
            "lightSphere",         // c15 award: Kriss's prize, never gated by later content
            "daggerReplica",       // c15 award: the same
            "heardRockHistory",    // c15: a knowledge flag nothing asks about later
            "edzzenclose",         // c17
            "foundLocker",         // c13
            "guardBaton",          // c12 and c13 both award it; nothing checks for it
            "forestLost",          // c20: both forest junctions set it, and the dusk arrival is
                                   // routed by node id instead, so nothing reads the flag
        ];

        Dictionary<string, List<int>> awarded = [];
        HashSet<string> gated = [];

        foreach (Chapter chapter in gameEngine.GetChapters())
        {
            foreach (Effect effect in EffectsIn(chapter))
                if (!string.IsNullOrEmpty(effect.GainItem))
                {
                    if (!awarded.TryGetValue(effect.GainItem, out List<int> chapters))
                        awarded[effect.GainItem] = chapters = [];

                    if (!chapters.Contains(chapter.Id))
                        chapters.Add(chapter.Id);
                }

            foreach (Condition condition in ConditionsIn(chapter))
                CollectGatedItems(condition, gated);
        }

        List<string> unexpectedlyUnspent = [.. awarded.Keys
            .Where(item => !gated.Contains(item) && !knowinglyUnspent.Contains(item))
            .OrderBy(item => item)];

        Assert.AreEqual(0, unexpectedlyUnspent.Count,
            "These flags are awarded but never gated on, and are not on the known-unspent list: "
            + string.Join(", ", unexpectedlyUnspent.Select(i => $"{i} (from c{string.Join("/c", awarded[i])})"))
            + ". Either something later should read them, or they should be added to the list with a reason.");

        List<string> gatedButNeverAwarded = [.. gated.Where(item => !awarded.ContainsKey(item)).OrderBy(item => item)];

        Assert.AreEqual(0, gatedButNeverAwarded.Count,
            "These items gate content but no chapter ever awards them, so the content behind them is unreachable: "
            + string.Join(", ", gatedButNeverAwarded));
    }

    static IEnumerable<Effect> EffectsIn(Chapter chapter)
    {
        foreach (NodeBase node in chapter.Nodes)
        {
            if (node is ChoiceNode choiceNode)
                foreach (Choice choice in choiceNode.Choices)
                    if (choice.Effect is not null)
                        yield return choice.Effect;

            if (node is ActionNode actionNode and not MiniGame01)
                foreach (Kriss.Models.Action action in actionNode.Actions)
                {
                    if (action.Effect is not null)
                        yield return action.Effect;

                    foreach (ActionObject obj in action.Objects ?? [])
                        if (obj.Effect is not null)
                            yield return obj.Effect;
                }
        }
    }

    static IEnumerable<Condition> ConditionsIn(Chapter chapter)
    {
        foreach (NodeBase node in chapter.Nodes)
        {
            if (node is ChoiceNode choiceNode)
                foreach (Choice choice in choiceNode.Choices)
                    if (choice.Condition is not null)
                        yield return choice.Condition;

            if (node is ActionNode actionNode and not MiniGame01)
                foreach (Kriss.Models.Action action in actionNode.Actions)
                {
                    if (action.Condition is not null)
                        yield return action.Condition;

                    foreach (ActionObject obj in action.Objects ?? [])
                        if (obj.Condition is not null)
                            yield return obj.Condition;
                }
        }
    }

    /// <summary>Inventory items only: an "isNodeVisited" condition's Item is a node id, not a flag.</summary>
    static void CollectGatedItems(Condition condition, HashSet<string> gated)
    {
        if (condition.All is { Count: > 0 })
            foreach (Condition nested in condition.All)
                CollectGatedItems(nested, gated);

        if (!string.IsNullOrEmpty(condition.Item) && condition.Type != "isNodeVisited")
            gated.Add(condition.Item);
    }

    /// <summary>
    /// Issue 7 explicitly calls for zero Game-Over-style dead ends anywhere in c14 (unlike
    /// c1-c10, which use exactly that pattern - a Story node with no forward link that loops
    /// back to node 1 - deliberately, e.g. c2 node 21). Every node must either be the
    /// chapter's terminal (islast) or have at least one way forward that
    /// <see cref="GetAllOutgoingLinks"/> can see (childid, a choice, a dialogue
    /// line/reply chain, or a Surge's success/failure route).
    /// </summary>
    [TestMethod]
    public void Chapter14_HasNoDeadEndNodes()
    {
        Chapter chapter = gameEngine.GetChapters().Single(c => c.Id == 14);

        foreach (NodeBase node in chapter.Nodes)
        {
            if (node.IsLast)
                continue;

            bool hasWayForward = GetAllOutgoingLinks(node).Any();
            Assert.IsTrue(hasWayForward, $"Chapter 14 node {node.Id} is a dead end: no childid/choice/reply/surge route and not islast.");
        }
    }

    static IEnumerable<int> GetAllOutgoingLinks(NodeBase node)
    {
        if (node is StoryNode s && s.ChildId > 0)
            yield return s.ChildId;

        if (node is ChoiceNode c && c.Choices != null)
            foreach (Choice choice in c.Choices)
                if (choice.ChildId > 0)
                    yield return choice.ChildId;

        if (node is DialogueNode d)
        {
            // DialogueNode.Load falls through to AdvanceToNext(ChildId) once the lines run out,
            // so a Dialogue node's own childid is a real link and not only its lines' ones.
            if (d.ChildId > 0)
                yield return d.ChildId;
        }

        if (node is DialogueNode dn && dn.Dialogues != null)
        {
            foreach (DialogueLine dlg in dn.Dialogues)
            {
                if (dlg.ChildId.HasValue && dlg.ChildId.Value > 0)
                    yield return dlg.ChildId.Value;
                if (dlg.Replies != null)
                    foreach (Kriss.Models.Reply reply in dlg.Replies)
                        if (reply.ChildId.HasValue && reply.ChildId.Value > 0)
                            yield return reply.ChildId.Value;
            }
        }

        if (node is ActionNode a && a.Actions != null)
        {
            foreach (Kriss.Models.Action action in a.Actions)
            {
                if (action.ChildId.HasValue && action.ChildId.Value > 0)
                    yield return action.ChildId.Value;
                if (action.Objects != null)
                    foreach (Kriss.Models.ActionObject obj in action.Objects)
                        if (obj.ChildId.HasValue && obj.ChildId.Value > 0)
                            yield return obj.ChildId.Value;
            }
        }

        if (node is FightNode f)
            yield return f.ChildId;

        if (node is SurgeNode surge)
        {
            yield return surge.ChildId;

            if (surge.Challenge?.FailureChildId is int failureChildId)
                yield return failureChildId;
        }

        if (node is MiniGame01 miniGame)
        {
            if (miniGame.ChildId > 0)
                yield return miniGame.ChildId;
        }
    }
}

