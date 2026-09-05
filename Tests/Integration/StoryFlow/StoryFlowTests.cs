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
        // GameEngine.StartChapter always calls LoadNode(nodeId: 1) as a chapter's entry point.
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
    /// Issue 7 (c14/c15 restructure): every Dialogue reply's/line's "nextline" jump must
    /// resolve to a "linename" within the SAME DialogueNode. This is not caught by
    /// <see cref="AllChapters_HaveNoUnreachableOrMissingNodes"/>, whose BFS only follows
    /// inter-node "childid" links: <see cref="GetAllOutgoingLinks"/> deliberately does not
    /// yield NextLine jumps, because they are not node ids at all. Scoped to chapters 14/15
    /// (this issue's changed files) rather than made a repo-wide invariant, since other
    /// chapters (c5, c8, c11) also use nextline/linename and are out of this issue's scope.
    /// </summary>
    [TestMethod]
    public void Chapter14And15_DialogueNextLineReferencesResolveWithinTheSameNode()
    {
        foreach (int chapterId in new[] { 14, 15 })
        {
            Chapter chapter = gameEngine.GetChapters().Single(c => c.Id == chapterId);

            foreach (DialogueNode dialogueNode in chapter.Nodes.OfType<DialogueNode>())
            {
                HashSet<string> lineNames = [.. dialogueNode.Dialogues
                    .Where(d => !string.IsNullOrEmpty(d.LineName))
                    .Select(d => d.LineName)];

                foreach (DialogueLine line in dialogueNode.Dialogues)
                {
                    if (!string.IsNullOrEmpty(line.NextLine))
                        Assert.IsTrue(lineNames.Contains(line.NextLine),
                            $"Chapter {chapterId} node {dialogueNode.Id}: line nextline '{line.NextLine}' has no matching linename.");

                    if (line.Replies == null)
                        continue;

                    foreach (Reply reply in line.Replies)
                        if (!string.IsNullOrEmpty(reply.NextLine))
                            Assert.IsTrue(lineNames.Contains(reply.NextLine),
                                $"Chapter {chapterId} node {dialogueNode.Id}: reply nextline '{reply.NextLine}' has no matching linename.");
                }
            }
        }
    }

    /// <summary>
    /// Issue 7 ended c14 at node 95; issue 8 rewrote c15 into 31 nodes (1-31) ending at node 27.
    /// Confirms exactly one reachable node per chapter carries islast, and that it is the
    /// specific node the content report names - not just "any one node", which would pass
    /// even if the wrong node were marked.
    /// Scoped to 14/15: c1, c2, c18 and c19 do not currently satisfy "exactly one" (0 or 2
    /// islast nodes respectively), so this is not yet safe as a repo-wide invariant.
    /// </summary>
    [TestMethod]
    public void Chapter14And15_ExactlyOneReachableIsLastNode()
    {
        AssertSingleReachableIsLast(chapterId: 14, expectedIsLastNodeId: 95);
        AssertSingleReachableIsLast(chapterId: 15, expectedIsLastNodeId: 27);
    }

    void AssertSingleReachableIsLast(int chapterId, int expectedIsLastNodeId)
    {
        Chapter chapter = gameEngine.GetChapters().Single(c => c.Id == chapterId);
        Dictionary<int, NodeBase> nodeMap = chapter.Nodes.ToDictionary(n => n.Id);

        HashSet<int> visited = [];
        Queue<NodeBase> queue = new();
        queue.Enqueue(nodeMap[1]);

        while (queue.Count > 0)
        {
            NodeBase node = queue.Dequeue();
            if (!visited.Add(node.Id))
                continue;

            foreach (int nextId in GetAllOutgoingLinks(node))
                queue.Enqueue(nodeMap[nextId]);
        }

        List<int> reachableIsLastIds = [.. visited.Where(id => nodeMap[id].IsLast)];

        Assert.AreEqual(1, reachableIsLastIds.Count,
            $"Chapter {chapterId}: expected exactly one reachable islast node, found [{string.Join(", ", reachableIsLastIds)}].");
        Assert.AreEqual(expectedIsLastNodeId, reachableIsLastIds[0],
            $"Chapter {chapterId}: the reachable islast node should be {expectedIsLastNodeId}.");
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

