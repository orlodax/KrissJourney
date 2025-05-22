using System;
using System.Collections.Generic;
using System.Linq;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure.Mocks;

namespace KrissJourney.Tests.Infrastructure;

/// <summary>
/// A unified class for testing nodes with or without requiring chapters to be loaded
/// </summary>
public class NodeTestRunner
{
    /// <summary>
    /// The game engine instance used for testing
    /// </summary>
    public GameEngine GameEngine { get; }

    /// <summary>
    /// The mock terminal for testing terminal-based interactions
    /// </summary>
    public TerminalMock Terminal { get; private set; }

    /// <summary>
    /// The test chapter that is automatically created for testing nodes
    /// </summary>
    public Chapter TestChapter { get; private set; }

    /// <summary>
    /// Creates a new NodeTestRunner with a test environment
    /// </summary>
    /// <param name="setupTerminalMock">Whether to set up a terminal mock</param>
    public NodeTestRunner(bool setupTerminalMock = false)
    {
        // Create a test status manager that doesn't use the file system
        var statusManager = new TestStatusManager();

        // Initialize the game engine with our test status manager
        GameEngine = new GameEngine(statusManager);

        // Call Run to initialize the game engine (loads chapters, etc.)
        GameEngine.Run();

        // Create test chapter for node testing
        SetupTestChapter();

        // Set up terminal mock if requested
        if (setupTerminalMock)
        {
            SetupTerminalMock();
        }
    }

    /// <summary>
    /// Creates and configures a test chapter
    /// </summary>
    private void SetupTestChapter()
    {
        // Get chapters field using reflection
        var chaptersField = typeof(GameEngine).GetField("chapters",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Create a test chapter
        TestChapter = new Chapter
        {
            Id = 999,
            Title = "Test Chapter",
            Nodes = []
        };

        // Add it to the chapters list if it doesn't already exist
        if (chaptersField?.GetValue(GameEngine) is List<Chapter> chapters && !chapters.Any(c => c.Id == TestChapter.Id))
        {
            chapters.Add(TestChapter);
        }
    }

    /// <summary>
    /// Sets up the terminal mock for testing terminal interactions
    /// </summary>
    public void SetupTerminalMock()
    {
        // Create mock terminal
        Terminal = new TerminalMock();

        // Store original terminal and set our mock
        TerminalFacade.SetTestTerminal(Terminal);
    }

    /// <summary>
    /// Creates a node of the specified type and configures it for testing
    /// </summary>
    /// <typeparam name="T">The type of node to create</typeparam>
    /// <param name="nodeId">The ID to assign to the node (defaults to 1)</param>
    /// <param name="configure">Optional action to configure the node</param>
    /// <returns>The configured node</returns>
    public T CreateNode<T>(int nodeId = 1, Action<T> configure = null) where T : NodeBase, new()
    {
        // Create the node with the specified ID
        var node = new T { Id = nodeId };

        // Apply default configuration
        SetDefaultNodeProperties(node);

        // Apply custom configuration if provided
        configure?.Invoke(node);

        // Set up the node in the test environment
        SetupNodeInTestEnvironment(node);

        return node;
    }

    /// <summary>
    /// Sets default properties on a node based on its type
    /// </summary>
    private static void SetDefaultNodeProperties<T>(T node) where T : NodeBase
    {
        // Set default text for all node types
        node.Text = $"Test {typeof(T).Name} node";

        // Apply type-specific defaults
        if (node is StoryNode storyNode)
        {
            storyNode.ChildId = 2;
        }
        else if (node is DialogueNode dialogueNode)
        {
            dialogueNode.Dialogues =
            [
                new Dialogue { Actor = "Tester", Line = "Test dialogue" }
            ];
        }
        else if (node is ChoiceNode choiceNode)
        {
            choiceNode.Choices =
            [
                new Choice { Desc = "Test choice", ChildId = 2 }
            ];
        }
        else if (node is ActionNode actionNode)
        {
            actionNode.Actions =
            [
                new Kriss.Models.Action
                {
                    Verbs = ["test", "examine"],
                    Answer = "This is a test response."
                }
            ];
        }
    }

    /// <summary>
    /// Sets up a node in the test environment
    /// </summary>
    private void SetupNodeInTestEnvironment(NodeBase node)
    {
        // Add the node to the test chapter if it's not already there
        if (!TestChapter.Nodes.Any(n => n.Id == node.Id))
        {
            TestChapter.Nodes.Add(node);
        }

        // Set the game engine on the node
        node.SetGameEngine(GameEngine);

        // Set the current chapter and node in the game engine
        SetCurrentChapterAndNode(TestChapter, node);
    }

    /// <summary>
    /// Sets the current chapter and node in the game engine using reflection
    /// </summary>
    private void SetCurrentChapterAndNode(Chapter chapter, NodeBase node)
    {
        // Set current chapter field
        var currentChapterField = typeof(GameEngine).GetField("currentChapter",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        currentChapterField?.SetValue(GameEngine, chapter);

        // Set current node field
        var currentNodeField = typeof(GameEngine).GetField("currentNode",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        currentNodeField?.SetValue(GameEngine, node);
    }

    /// <summary>
    /// Runs a test on a node
    /// </summary>
    /// <typeparam name="T">The type of node to test</typeparam>
    /// <param name="testAction">The action to perform on the node</param>
    /// <param name="nodeId">The ID to assign to the node (defaults to 1)</param>
    /// <param name="configure">Optional action to configure the node</param>
    public void TestNode<T>(Action<T> testAction, int nodeId = 1, Action<T> configure = null) where T : NodeBase, new()
    {
        var node = CreateNode(nodeId, configure);
        testAction(node);
    }

    /// <summary>
    /// Simulates user input by providing a sequence of keys
    /// </summary>
    public void SimulateUserInput(params ConsoleKey[] keys)
    {
        if (Terminal == null)
        {
            throw new InvalidOperationException("Terminal mock is not set up. Call SetupTerminalMock() first.");
        }

        Terminal.EnqueueKeys(keys);
    }

    /// <summary>
    /// Simulates typing text
    /// </summary>
    public void SimulateTextInput(string text)
    {
        if (Terminal == null)
        {
            throw new InvalidOperationException("Terminal mock is not set up. Call SetupTerminalMock() first.");
        }

        Terminal.EnqueueText(text);
    }

    /// <summary>
    /// Gets the terminal output
    /// </summary>
    public string GetTerminalOutput()
    {
        if (Terminal == null)
        {
            throw new InvalidOperationException("Terminal mock is not set up. Call SetupTerminalMock() first.");
        }

        return Terminal.GetOutput();
    }
}
