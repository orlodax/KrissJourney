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

    static IEnumerable<int> GetAllOutgoingLinks(NodeBase node)
    {
        if (node is StoryNode s && s.ChildId > 0)
            yield return s.ChildId;

        if (node is ChoiceNode c && c.Choices != null)
            foreach (Choice choice in c.Choices)
                if (choice.ChildId > 0)
                    yield return choice.ChildId;

        if (node is DialogueNode d && d.Dialogues != null)
        {
            foreach (DialogueLine dlg in d.Dialogues)
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

        if (node is MiniGame01 miniGame)
        {
            if (miniGame.ChildId > 0)
                yield return miniGame.ChildId;
        }
    }
}

