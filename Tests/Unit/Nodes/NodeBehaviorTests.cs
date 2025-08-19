using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Tests.Infrastructure;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Unit.Nodes;

/// <summary>
/// Example tests showing how to test node behavior using the NodeTestRunner
/// </summary>
[TestClass]
public class NodeBehaviorTests
{
    private NodeTestRunner testRunner;

    [TestInitialize]
    public void TestInitialize()
    {
        // Create a test runner without terminal mocking (we don't need it for these tests)
        testRunner = new NodeTestRunner(setupTerminalMock: false);
    }

    [TestMethod]
    public void StoryNode_CanBeCreatedAndTested()
    {
        // Create and test a story node
        StoryNode storyNode = testRunner.CreateNode<StoryNode>(nodeId: 1, node =>
        {
            node.Text = "Custom story text";
            node.ChildId = 2;
        });

        // Verify node properties
        Assert.AreEqual(1, storyNode.Id);
        Assert.AreEqual("Custom story text", storyNode.Text);
        Assert.AreEqual(2, storyNode.ChildId);

        // You can also use the TestNode method for more concise tests
        testRunner.TestNode<StoryNode>(node =>
        {
            Assert.IsNotNull(node);
            // Test node behavior here
        });
    }

    [TestMethod]
    public void DialogueNode_CanBeCreatedAndTested()
    {
        // Create and test a dialogue node with custom dialogues
        DialogueNode dialogueNode = testRunner.CreateNode<DialogueNode>(nodeId: 1, node =>
        {
            node.Dialogues = [
                new DialogueLine { Actor = EnCharacter.Kriss, Line = "Hello world", Break = true },
                new DialogueLine { Actor = EnCharacter.Chief, Line = "Goodbye world" }
            ];
        });

        // Verify node properties
        Assert.AreEqual(1, dialogueNode.Id);
        Assert.AreEqual(2, dialogueNode.Dialogues.Count);
        Assert.AreEqual(EnCharacter.Kriss, dialogueNode.Dialogues[0].Actor);
    }

    [TestMethod]
    public void ChoiceNode_CanBeCreatedAndTested()
    {
        // Create and test a choice node with custom choices
        testRunner.TestNode<ChoiceNode>(node =>
        {
            // Verify the node was created with default properties
            Assert.IsNotNull(node);
            Assert.IsNotNull(node.Choices);
            Assert.AreEqual(1, node.Choices.Count);

            // You can modify the node and test its behavior
            node.Choices.Add(new Choice { Desc = "Another option", ChildId = 3 });
            Assert.AreEqual(2, node.Choices.Count);
        });
    }

    [TestMethod]
    public void ActionNode_CanBeCreatedAndTested()
    {
        // Create and test an action node
        testRunner.TestNode<ActionNode>(node =>
        {
            // Test action node behavior
            Assert.IsNotNull(node);
            Assert.IsNotNull(node.Actions);
        }, configure: node =>
        {
            // Configure the action node with custom actions
            node.Actions = [
                new()
                {
                    Verbs = ["look"],
                    Effect = new Effect { GainItem = "sword" }
                }
            ];
        });
    }
}

