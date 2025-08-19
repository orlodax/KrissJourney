using System;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;

namespace KrissJourney.Tests.Infrastructure.Helpers;

/// <summary>
/// Helpers for testing nodes without requiring actual chapters to be loaded
/// </summary>
public static class NodeTestHelper
{
    /// <summary>
    /// Create and configure a test node without requiring an actual chapter
    /// </summary>
    /// <typeparam name="T">The type of node to create</typeparam>
    /// <param name="gameEngine">The game engine instance</param>
    /// <param name="nodeId">The ID to assign to the node (defaults to 1)</param>
    /// <param name="configure">Optional action to configure the node</param>
    /// <returns>The configured node</returns>
    public static T CreateTestNode<T>(this GameEngine gameEngine, int nodeId = 1, Action<T> configure = null) where T : NodeBase, new()
    {
        T node = new() { Id = nodeId };
        // Apply default configuration
        if (typeof(T) == typeof(StoryNode) || node is StoryNode)
        {
            node.Text = "This is a test story node";
        }
        else if (typeof(T) == typeof(DialogueNode) || node is DialogueNode)
        {
            node.Text = "This is a test dialogue node";
            if (node is DialogueNode dialogueNode)
            {
                dialogueNode.Dialogues =
                [
                    new DialogueLine { Actor = EnCharacter.Narrator, Line = "Test dialogue" }
                ];
            }
        }
        else if (typeof(T) == typeof(ChoiceNode) || node is ChoiceNode)
        {
            node.Text = "This is a test choice node";
            if (node is ChoiceNode choiceNode)
            {
                choiceNode.Choices =
                [
                    new Choice { Desc = "Test choice", ChildId = 2 }
                ];
            }
        }
        else if (typeof(T) == typeof(ActionNode) || node is ActionNode)
        {
            node.Text = "This is a test action node";
            if (node is ActionNode actionNode)
            {
                actionNode.Actions =
                [
                    new Kriss.Models.Action { Verbs = ["Test action"] }
                ];
            }
        }

        // Apply custom configuration if provided
        configure?.Invoke(node);

        // Set up test environment for the node
        SetupTestEnvironmentForNode(gameEngine, node);

        return node;
    }

    /// <summary>
    /// Sets up the test environment for a node
    /// </summary>
    /// <param name="gameEngine">The game engine instance</param>
    /// <param name="node">The node to set up</param>
    private static void SetupTestEnvironmentForNode(GameEngine gameEngine, NodeBase node)
    {
        // Create a test chapter if needed
        int testChapterId = 999;

        // Create a test chapter
        Chapter testChapter = new()
        {
            Id = testChapterId,
            Title = "Test Chapter",
            Nodes = [node]
        };

        // Set up the node
        node.SetGameEngine(gameEngine);

        // Use reflection to set the private setters for CurrentChapter and CurrentNode
        System.Reflection.PropertyInfo currentChapterProperty = typeof(GameEngine).GetProperty(nameof(GameEngine.CurrentChapter));
        System.Reflection.PropertyInfo currentNodeProperty = typeof(GameEngine).GetProperty(nameof(GameEngine.CurrentNode));

        currentChapterProperty?.SetValue(gameEngine, testChapter);
        currentNodeProperty?.SetValue(gameEngine, node);
    }

    /// <summary>
    /// Runs a test on a node without requiring an actual chapter
    /// </summary>
    /// <typeparam name="T">The type of node to test</typeparam>
    /// <param name="gameEngine">The game engine instance</param>
    /// <param name="testAction">The action to perform on the node</param>
    /// <param name="nodeId">The ID to assign to the node (defaults to 1)</param>
    /// <param name="configure">Optional action to configure the node</param>
    public static void TestNode<T>(this GameEngine gameEngine, Action<T> testAction, int nodeId = 1, Action<T> configure = null) where T : NodeBase, new()
    {
        T node = gameEngine.CreateTestNode(nodeId, configure);
        testAction(node);
    }
}
