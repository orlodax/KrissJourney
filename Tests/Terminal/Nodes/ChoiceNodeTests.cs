using System;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Terminal.Nodes;

[TestClass]
public class ChoiceNodeTests : NodeTestBase
{
    private ChoiceNode choiceNode;

    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
        choiceNode = CreateNode<ChoiceNode>(configure: node =>
        {
            node.Choices =
            [
                new() { Desc = "First", ChildId = 2, IsHidden = false, IsPlayed = false, UnHide = 2 },
                new() { Desc = "Second", ChildId = 3, IsHidden = false, IsPlayed = false, IsNotRepeatable = true },
                new() { Desc = "Hidden", ChildId = 4, IsHidden = true }
            ];
        });
    }

    [TestMethod]
    public void DisplaysOnlyVisibleChoices()
    {
        LoadNode(choiceNode);

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("First"));
        Assert.IsTrue(output.Contains("Second"));
        Assert.IsFalse(output.Contains("Hidden"));
    }

    [TestMethod]
    [DataRow(ConsoleKey.DownArrow)]
    [DataRow(ConsoleKey.RightArrow)]
    public void Navigation_DownOrRight_SelectsCorrectChoice(ConsoleKey consoleKey)
    {
        SimulateUserInput(consoleKey, ConsoleKey.Enter);
        _ = CreateNode<StoryNode>(nodeId: 3, configure: n => n.Text = "Next node loaded!");

        LoadNode(choiceNode);

        // Should select "Second" choice
        Assert.IsTrue(choiceNode.Choices[1].IsPlayed);
    }

    [TestMethod]
    public void Navigation_RightLeft_SelectsCorrectChoice()
    {
        SimulateUserInput(ConsoleKey.RightArrow, ConsoleKey.LeftArrow, ConsoleKey.Enter);
        _ = CreateNode<StoryNode>(nodeId: 2, configure: n => n.Text = "Next node loaded!");

        LoadNode(choiceNode);

        Assert.IsTrue(choiceNode.Choices[0].IsPlayed);
    }

    [TestMethod]
    public void Navigation_DownUp_SelectsCorrectChoice()
    {
        SimulateUserInput(ConsoleKey.DownArrow, ConsoleKey.UpArrow, ConsoleKey.Enter);
        _ = CreateNode<StoryNode>(nodeId: 2, configure: n => n.Text = "Next node loaded!");

        LoadNode(choiceNode);

        Assert.IsTrue(choiceNode.Choices[0].IsPlayed);
    }

    [TestMethod]
    public void NonRepeatableChoice_CannotBeSelectedAgain()
    {
        // Play the second choice
        choiceNode.Choices[1].IsPlayed = true;
        choiceNode.Choices[1].IsNotRepeatable = true;
        // Simulate: DownArrow to select second, Enter (should refuse), Enter (should allow first)
        SimulateUserInput(ConsoleKey.DownArrow, ConsoleKey.Enter, ConsoleKey.UpArrow, ConsoleKey.Enter);
        _ = CreateNode<StoryNode>(nodeId: 2, configure: n => n.Text = "Next node loaded!");

        LoadNode(choiceNode);

        string output = TerminalMock.GetOutput();
        // Should redraw and allow only repeatable (first) choice
        Assert.IsTrue(output.Contains("First"));
        // Also, after retry, first choice should be played
        Assert.IsTrue(choiceNode.Choices[0].IsPlayed);
    }

    [TestMethod]
    public void UnHide_MakesChoiceVisible()
    {
        // the first choice in this test setup should unhide the third choice
        SimulateUserInput(ConsoleKey.Enter);
        _ = CreateNode<StoryNode>(nodeId: 2, configure: n => n.Text = "Next node loaded!");

        LoadNode(choiceNode); ;

        SimulateUserInput(ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter);
        _ = CreateNode<StoryNode>(nodeId: 4, configure: n => n.Text = "Next node loaded!");

        LoadNode(choiceNode);

        // After selection, UnHide should make third choice visible
        Assert.IsFalse(choiceNode.Choices[2].IsHidden);
    }

    [TestMethod]
    public void Refusal_ShowsRefusalText()
    {
        // Set up a condition that will fail (item not in inventory)
        choiceNode.Choices[0].Condition = new Condition { Type = "item", Item = "not_in_inventory" };
        choiceNode.Choices[0].Refusal = "Cannot pick";
        SimulateUserInput(ConsoleKey.Enter, ConsoleKey.Enter); // Retry after refusal

        LoadNode(choiceNode);

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("Cannot pick"));
    }

    [TestMethod]
    public void DisplayChoices_FiltersByIsNodeVisitedCondition_AddsCorrectly()
    {
        // Arrange: Add a choice with isNodeVisited condition
        // Mark node 42 as visited in the test status manager
        GameEngine.SaveProgress(42);
        choiceNode.Choices.Add(new Choice
        {
            Desc = "NodeVisited",
            ChildId = 5,
            IsHidden = false,
            Condition = new Condition { Type = "isNodeVisited", Item = "42" }
        });
        _ = CreateNode<StoryNode>(nodeId: 5, configure: n => n.Text = "Next node loaded if 42 was visited!");

        SimulateUserInput(ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter);

        LoadNode(choiceNode);

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("NodeVisited"));
    }

    [TestMethod]
    public void DisplayChoices_ThrowsOnInvalidNodeId()
    {
        // Arrange: Add a choice with isNodeVisited and invalid node id
        choiceNode.Choices.Add(new Choice
        {
            Desc = "InvalidNodeId",
            ChildId = 6,
            IsHidden = false,
            Condition = new Condition { Type = "isNodeVisited", Item = "notAnInt" }
        });
        SimulateUserInput(ConsoleKey.Enter);
        Assert.ThrowsException<Exception>(() => choiceNode.Load());

        TerminalCleanup();
    }

    [TestMethod]
    public void RepeatableChoice_CanBeSelectedMultipleTimes()
    {
        choiceNode.Choices[0].IsNotRepeatable = false;
        _ = CreateNode<StoryNode>(nodeId: 2, configure: n => n.Text = "Next node loaded if 42 was visited!");
        SimulateUserInput(ConsoleKey.Enter, ConsoleKey.Enter);

        LoadNode(choiceNode);

        Assert.IsTrue(choiceNode.Choices[0].IsPlayed);
        // Try again
        choiceNode.Choices[0].IsPlayed = false;
        SimulateUserInput(ConsoleKey.Enter);

        LoadNode(choiceNode);

        Assert.IsTrue(choiceNode.Choices[0].IsPlayed);
    }

    [TestMethod]
    public void ChoiceWithEffect_AddsItemToInventory()
    {
        choiceNode.Choices[0].Effect = new Effect { GainItem = "sword" };
        _ = CreateNode<StoryNode>(nodeId: 2, configure: n => n.Text = "This node will give you a sword!");

        // This will call GameEngine.AddItemToInventory
        SimulateUserInput(ConsoleKey.Enter);

        LoadNode(choiceNode);

        choiceNode = CreateNode<ChoiceNode>(configure: node =>
        {
            node.Choices =
            [
                new()
                {
                    Desc = "Sword-enabled choice",
                    ChildId = 6,
                    IsHidden = false,
                    Condition = new Condition { Item = "sword" }
                }
            ];
        });

        _ = CreateNode<StoryNode>(nodeId: 6, configure: n => n.Text = "Next node loaded if sword was in inventory!");

        // Will call GameEngine.Evaluate to check if the item is in inventory
        SimulateUserInput(ConsoleKey.Enter);

        LoadNode(choiceNode);
    }

    [TestMethod]
    public void RedrawNode_ClearsAndRedraws()
    {
        // Should not throw and should output the node text and spacing
        choiceNode.GetType().GetMethod("RedrawNode", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(choiceNode, null);
        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("[CONSOLE CLEARED]"));
    }

    [TestMethod]
    public void NonRepeatableChoice_AdvancesAndCannotBeSelectedAgain()
    {
        // Arrange: Make second choice non-repeatable
        choiceNode.Choices[1].IsNotRepeatable = true;
        // Simulate: DownArrow to select second, Enter to select
        SimulateUserInput(ConsoleKey.DownArrow, ConsoleKey.Enter);
        // Add a dummy next node to simulate game loading
        StoryNode nextNode = CreateNode<StoryNode>(nodeId: 3, configure: n => n.Text = "Next node loaded!");

        LoadNode(choiceNode);

        string output = TerminalMock.GetOutput();
        // Should advance to next node and display its text
        Assert.IsTrue(output.Contains("Next node loaded!"));
        // The choice should be marked as played
        Assert.IsTrue(choiceNode.Choices[1].IsPlayed);

        TerminalMock.ResetOutput();
        // Now try to select it again (simulate a new node, as in real gameplay)
        ChoiceNode newChoiceNode = CreateNode<ChoiceNode>(configure: node =>
        {
            node.Choices = choiceNode.Choices;
        });
        SimulateUserInput(ConsoleKey.DownArrow, ConsoleKey.Enter);

        LoadNode(newChoiceNode);

        string output2 = TerminalMock.GetOutput();
        // Should not advance again, should not see next node text again
        Assert.IsFalse(output2.Contains("Next node loaded!"));
    }

    [TestMethod]
    public void RepeatableChoice_AdvancesEachTime()
    {
        // Arrange: Make first choice repeatable
        choiceNode.Choices[0].IsNotRepeatable = false;
        // Add a dummy next node to simulate game loading
        StoryNode nextNode = CreateNode<StoryNode>(nodeId: 2, configure: n => n.Text = "Repeat node loaded!");
        // Simulate: Enter to select, then Enter to select again (simulate new node each time)
        SimulateUserInput(ConsoleKey.Enter, ConsoleKey.Enter);

        LoadNode(choiceNode);

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("Repeat node loaded!"));
        // Simulate a new node for the repeat
        ChoiceNode newChoiceNode = CreateNode<ChoiceNode>(configure: node =>
        {
            node.Choices = choiceNode.Choices;
        });
        SimulateUserInput(ConsoleKey.Enter);

        LoadNode(newChoiceNode);
        string output2 = TerminalMock.GetOutput();

        Assert.IsTrue(output2.Contains("Repeat node loaded!"));
    }
}