using System.Collections.Generic;
using System.Linq;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Integration;

/// <summary>
/// The cheap structural checks a chapter has to survive before a walkthrough is worth running:
/// unique ids, every reference resolving, and exactly one way out of the chapter. Shared by the
/// per-chapter walkthrough classes and by the repo-wide invariants in
/// <see cref="StoryFlow.StoryFlowTests"/>, so a rule is written once and cannot drift into two
/// slightly different versions of itself.
/// </summary>
public static class ChapterStructureAssertions
{
    /// <summary>
    /// Node ids are unique; every childid (node, choice, action, action object, dialogue line
    /// and reply) resolves to a node in the same chapter; every nextline resolves to a linename
    /// declared in the same dialogue node.
    /// </summary>
    public static void AssertReferencesResolve(Chapter chapter)
    {
        List<int> ids = [.. chapter.Nodes.Select(n => n.Id)];
        Assert.AreEqual(ids.Count, ids.Distinct().Count(),
            $"Chapter {chapter.Id} has duplicate node ids: {string.Join(", ", ids.GroupBy(i => i).Where(g => g.Count() > 1).Select(g => g.Key))}");

        HashSet<int> idSet = [.. ids];

        foreach (NodeBase node in chapter.Nodes)
        {
            void Resolves(int? childId, string what)
            {
                if (childId is int id && id > 0)
                    Assert.IsTrue(idSet.Contains(id), $"Chapter {chapter.Id} node {node.Id}: {what} childid {id} does not resolve.");
            }

            Resolves(node.ChildId, "the node's own");

            switch (node)
            {
                case ChoiceNode choiceNode:
                    foreach (Choice choice in choiceNode.Choices)
                        Resolves(choice.ChildId, "a choice's");
                    break;

                case MiniGame01:
                    break; // its actions are generated, not authored

                case ActionNode actionNode:
                    foreach (Kriss.Models.Action action in actionNode.Actions)
                    {
                        Resolves(action.ChildId, "an action's");
                        foreach (ActionObject obj in action.Objects ?? [])
                            Resolves(obj.ChildId, "an action object's");
                    }
                    break;

                case DialogueNode dialogueNode:
                    HashSet<string> lineNames = [.. dialogueNode.Dialogues
                        .Where(l => !string.IsNullOrEmpty(l.LineName))
                        .Select(l => l.LineName)];

                    foreach (DialogueLine line in dialogueNode.Dialogues)
                    {
                        Resolves(line.ChildId, "a dialogue line's");
                        AssertNextLineResolves(chapter, node, lineNames, line.NextLine, "line");

                        foreach (Reply reply in line.Replies ?? [])
                        {
                            Resolves(reply.ChildId, "a reply's");
                            AssertNextLineResolves(chapter, node, lineNames, reply.NextLine, "reply");
                        }
                    }
                    break;

                case SurgeNode surgeNode:
                    Resolves(surgeNode.Challenge?.FailureChildId, "a surge failure's");
                    break;
            }
        }
    }

    static void AssertNextLineResolves(Chapter chapter, NodeBase node, HashSet<string> lineNames, string nextLine, string what)
    {
        if (!string.IsNullOrWhiteSpace(nextLine))
            Assert.IsTrue(lineNames.Contains(nextLine),
                $"Chapter {chapter.Id} node {node.Id}: {what} nextline '{nextLine}' has no matching linename in the same node.");
    }

    /// <summary>
    /// Walking forward from node 1, the chapter hands over to the next one at exactly the node
    /// the caller names, and nowhere else. A second reachable islast node is only tolerated when
    /// it is also isclosing - <see cref="NodeBase.AdvanceToNext"/> takes that branch first and
    /// goes back to the menu, so it is a game over rather than a way onward. No chapter uses that
    /// tolerance today: c18's node 181 was the one that did, and issue 21 turned the drowning into
    /// a survivable near-drowning that routes back into node 18. The named ending may itself be
    /// isclosing: c24 node 15 both ends the chapter and ends the game.
    /// </summary>
    public static void AssertChapterEndsAt(Chapter chapter, int endsAtNodeId)
    {
        HashSet<int> reachable = Reachable(chapter);

        List<NodeBase> endings = [.. reachable
            .Select(id => chapter.Nodes.Single(n => n.Id == id))
            .Where(n => n.IsLast)
            .OrderBy(n => n.Id)];

        Assert.IsTrue(endings.Any(n => n.Id == endsAtNodeId),
            $"Chapter {chapter.Id} should end at node {endsAtNodeId}, but that node is not a reachable islast node. "
            + $"Reachable islast nodes: [{string.Join(", ", endings.Select(n => n.Id))}].");

        List<int> rivals = [.. endings.Where(n => n.Id != endsAtNodeId && !n.IsClosing).Select(n => n.Id)];

        Assert.AreEqual(0, rivals.Count,
            $"Chapter {chapter.Id} has more than one way into the next chapter: besides node {endsAtNodeId}, "
            + $"nodes [{string.Join(", ", rivals)}] are islast without being a game over.");
    }

    /// <summary>Every node id reachable by following links forward from node 1.</summary>
    public static HashSet<int> Reachable(Chapter chapter)
    {
        Dictionary<int, NodeBase> nodeMap = chapter.Nodes.ToDictionary(n => n.Id);

        Assert.IsTrue(nodeMap.ContainsKey(1),
            $"Chapter {chapter.Id} has no node with id 1: GameEngine.StartChapter enters a chapter there, and a lost fight restarts it there.");

        HashSet<int> visited = [];
        Stack<int> pending = new();
        pending.Push(1);

        while (pending.Count > 0)
        {
            int id = pending.Pop();
            if (!visited.Add(id))
                continue;

            foreach (int next in OutgoingLinks(nodeMap[id]))
            {
                Assert.IsTrue(nodeMap.ContainsKey(next),
                    $"Chapter {chapter.Id} node {id} references missing node {next}.");
                pending.Push(next);
            }
        }

        return visited;
    }

    /// <summary>
    /// Every forward link a node can take, whatever its type. Deliberately does NOT yield
    /// nextline jumps: those are line labels inside one node, not node ids.
    /// </summary>
    public static IEnumerable<int> OutgoingLinks(NodeBase node)
    {
        if (node.ChildId > 0 && node is not ChoiceNode)
            yield return node.ChildId;

        switch (node)
        {
            case ChoiceNode choiceNode:
                foreach (Choice choice in choiceNode.Choices ?? [])
                    if (choice.ChildId > 0)
                        yield return choice.ChildId;
                break;

            case MiniGame01:
                break;

            case ActionNode actionNode:
                foreach (Kriss.Models.Action action in actionNode.Actions ?? [])
                {
                    if (action.ChildId is int actionChild && actionChild > 0)
                        yield return actionChild;

                    foreach (ActionObject obj in action.Objects ?? [])
                        if (obj.ChildId is int objChild && objChild > 0)
                            yield return objChild;
                }
                break;

            case DialogueNode dialogueNode:
                foreach (DialogueLine line in dialogueNode.Dialogues ?? [])
                {
                    if (line.ChildId is int lineChild && lineChild > 0)
                        yield return lineChild;

                    foreach (Reply reply in line.Replies ?? [])
                        if (reply.ChildId is int replyChild && replyChild > 0)
                            yield return replyChild;
                }
                break;

            case SurgeNode surgeNode:
                if (surgeNode.Challenge?.FailureChildId is int failureChild && failureChild > 0)
                    yield return failureChild;
                break;
        }
    }
}
