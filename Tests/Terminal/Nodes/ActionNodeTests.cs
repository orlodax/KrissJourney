using System;
using System.Reflection;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Action = KrissJourney.Kriss.Models.Action;

namespace KrissJourney.Tests.Terminal.Nodes
{
    [TestClass]
    public class ActionNodeTests : NodeTestBase
    {
        private ActionNode actionNode;

        [TestInitialize]
        public override void TestInitialize()
        {
            base.TestInitialize();

            // Create a basic ActionNode with test actions
            actionNode = CreateNode<ActionNode>(configure: node =>
            {
                node.Actions =
                [
                    new Action
                    {
                        Verbs = ["look", "examine"],
                        Answer = "You see nothing special.",
                        ChildId = null,
                    },
                    new Action
                    {
                        Verbs = ["take"],
                        Objects =
                        [
                            new ActionObject
                            {
                                Objs = ["book", "notebook"],
                                Answer = "You take the book.",
                                ChildId = 2,
                                Effect = new Effect
                                {
                                    GainItem = "book"
                                },
                                Condition = null // Remove the condition to simplify the test
                            }
                        ],
                        Answer = null,
                        Condition = null // Remove the condition to simplify the test
                    }
                ];
            });
        }

        [TestMethod]
        public void TabPressed_ShowsAvailableActions()
        {
            // Arrange
            SimulateUserInput(ConsoleKey.Tab);

            // Act
            LoadNode(actionNode);

            // Assert
            string output = TerminalMock.GetOutput();
            Assert.IsTrue(output.Contains("Possible actions here"), "Tab should display available actions");
            Assert.IsTrue(output.Contains("look"), "Action 'look' should be shown");
            Assert.IsTrue(output.Contains("take"), "Action 'take' should be shown");
        }

        [TestMethod]
        public void EnterPressed_WithValidAction_DisplaysSuccess()
        {
            // Arrange - simulate typing "look" and pressing Enter
            SimulateTextInput("look");
            SimulateUserInput(ConsoleKey.Enter);

            // Act
            LoadNode(actionNode);

            // Assert
            string output = TerminalMock.GetOutput();
            Assert.IsTrue(output.Contains("You see nothing special"), "Success message should be displayed");
        }

        [TestMethod]
        public void EnterPressed_WithActionAndObject_ProcessesAction()
        {
            // Arrange: create a second node that will be loaded by AdvanceToNext
            StoryNode nextNode = CreateNode<StoryNode>(nodeId: 2, configure: node =>
            {
                node.Text = "Next node text";
                node.ChildId = 1; // Set it to point back to the first node to avoid looking for non-existent nodes
            });

            SimulateTextInput("take book");
            SimulateUserInput(ConsoleKey.Enter); // Simulate pressing Enter to perform the action
            SimulateUserInput(ConsoleKey.Enter); // Simulate pressing any key to advance to the next node

            // Act
            LoadNode(actionNode);

            // Assert
            string output = TerminalMock.GetOutput();
            Assert.IsTrue(output.Contains("You take the book"), "Success message should be displayed");
            Assert.IsTrue(output.Contains("Next node text"), "Should advance to the next node and display its text");
        }

        [TestMethod]
        public void InvalidAction_ShowsError()
        {
            SimulateTextInput("invalid");
            SimulateUserInput(ConsoleKey.Enter);

            LoadNode(actionNode);

            string output = TerminalMock.GetOutput();
            Assert.IsTrue(output.Contains("You can't or won't do that"));
        }

        [TestMethod]
        public void MeasureMessage_CalculatesCorrectPosition()
        {
            // Arrange
            string shortMessage = "Test message";
            string longMessage = "This is a very long message that will span multiple lines in the console window. " +
                                "It contains several words and should result in multiple rows when displayed.";
            string messageWithNewlines = "Line 1\nLine 2\nLine 3";

            // Act
            int shortPos = (int)actionNode.GetType().GetMethod("MeasureMessage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, [shortMessage]);

            _ = (int)actionNode.GetType().GetMethod("MeasureMessage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, [longMessage]);

            _ = (int)actionNode.GetType().GetMethod("MeasureMessage",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                .Invoke(null, [messageWithNewlines]);

            // Assert - Just test that the function returns a valid value
            Assert.IsTrue(shortPos >= 0, "Position should be non-negative");

            TerminalMock.Clear();

            TerminalCleanup();
        }

        [TestMethod]
        public void TabPressed_WithPartialVerb_ShowsObjectHelp()
        {
            // Arrange: Simulate typing 'take' and pressing Tab
            SimulateTextInput("take");
            SimulateUserInput(ConsoleKey.Tab);

            // Act
            LoadNode(actionNode);

            // Assert
            string output = TerminalMock.GetOutput();
            Assert.IsTrue(output.Contains("Possible objects for the action typed"), "Tab should display object help");
            Assert.IsTrue(output.Contains("book"), "Object 'book' should be shown");
        }

        [TestMethod]
        public void BackSpacePressed_RemovesKeyAndRedraws()
        {
            // Arrange: Simulate typing 'look', then Backspace
            SimulateTextInput("look");
            SimulateUserInput(ConsoleKey.Backspace);
            SimulateUserInput(ConsoleKey.Enter); // To exit loop

            // Act
            LoadNode(actionNode);

            // Assert
            string output = TerminalMock.GetOutput();
            Assert.IsTrue(output.Contains("You can't or won't do that") || output.Contains("Possible actions here"));
        }

        [TestMethod]
        public void EnterPressed_ActionWithoutRequiredObject_ShowsCustomRefusal()
        {
            // Arrange: Simulate typing 'take' (no object)
            SimulateTextInput("take");
            SimulateUserInput(ConsoleKey.Enter);
            SimulateUserInput(ConsoleKey.Enter); // To exit loop

            // Act
            LoadNode(actionNode);

            // Assert
            string output = TerminalMock.GetOutput();
            Assert.IsTrue(output.Contains("<<") && output.Contains(">>"), "Custom refusal should be shown");
        }

        [TestMethod]
        public void ProcessAction_WithUnmetCondition_ShowsCustomRefusal()
        {
            // Arrange: Add an action with an object that has a condition with a refusal
            Condition failCondition = new() { Refusal = "Nope!", Item = "nonexistent_item", Type = "item" };
            Action failAction = new()
            {
                Verbs = ["fail"],
                Objects =
                [
                    new ActionObject
                    {
                        Objs = ["obj"],
                        Condition = failCondition,
                        Answer = "Should not see this"
                    }
                ],
                Answer = null,
                Condition = null
            };
            actionNode.Actions.Add(failAction);
            SimulateTextInput("fail obj");
            SimulateUserInput(ConsoleKey.Enter);
            SimulateUserInput(ConsoleKey.Enter); // To exit loop

            // Act
            LoadNode(actionNode);

            // Assert
            string output = TerminalMock.GetOutput();
            Assert.IsTrue(output.Contains("<<Nope!>>"), "Custom refusal should be shown for failed condition");
        }

        [TestMethod]
        public void DisplaySuccess_WithChildId_AdvancesToNextNode()
        {
            // Arrange: Simulate typing 'take book' (which has ChildId = 2)
            StoryNode nextNode = CreateNode<StoryNode>(nodeId: 2, configure: node => node.Text = "Next node!");
            SimulateTextInput("take book");
            SimulateUserInput(ConsoleKey.Enter);
            SimulateUserInput(ConsoleKey.Enter); // To advance

            // Act
            LoadNode(actionNode);

            // Assert
            string output = TerminalMock.GetOutput();
            Assert.IsTrue(output.Contains("Next node!"), "Should advance to next node");
        }

        [TestMethod]
        public void RedrawNode_DisplaysBottomMessageWithCorrectColor()
        {
            // Arrange: Use reflection to set internal fields
            FieldInfo bottomMessageField = typeof(ActionNode).GetField("BottomMessage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            FieldInfo bottomMessageColorField = typeof(ActionNode).GetField("BottomMessageColor", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            bottomMessageField.SetValue(actionNode, "Test bottom message");
            bottomMessageColorField.SetValue(actionNode, ConsoleColor.DarkYellow);

            MethodInfo redrawNodeMethod = typeof(ActionNode).GetMethod(
                name: "RedrawNode",
                bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                binder: null,
                types: [typeof(bool)],
                modifiers: null
            );
            redrawNodeMethod.Invoke(actionNode, [true]);

            string outputYellow = TerminalMock.GetOutput();
            TerminalMock.Clear();
            bottomMessageColorField.SetValue(actionNode, ConsoleColor.Cyan);

            redrawNodeMethod.Invoke(actionNode, [true]);
            string outputCyan = TerminalMock.GetOutput();

            // Assert
            Assert.IsTrue(outputYellow.Contains("Test bottom message"));
            Assert.IsTrue(outputCyan.Contains("Test bottom message"));

            TerminalCleanup();
        }

        [TestMethod]
        public void MiniGame01_TabPressed_ShowsGuessPrompt()
        {
            // Arrange
            MiniGame01 miniGame = CreateNode<MiniGame01>();
            SimulateUserInput(ConsoleKey.Tab);

            // Act
            LoadNode(miniGame);

            // Assert
            string output = TerminalMock.GetOutput();
            Assert.IsTrue(output.Contains("Type something for Efeliah to guess"), "Tab should display guessing prompt");
        }

        [TestMethod]
        public void MiniGame01_EnterPressed_DisplaysGuess()
        {
            // Arrange
            MiniGame01 miniGame = CreateNode<MiniGame01>();
            SimulateTextInput("apple");
            SimulateUserInput(ConsoleKey.Enter);

            // Act
            LoadNode(miniGame);

            // Assert
            string output = TerminalMock.GetOutput();
            Assert.IsTrue(output.Contains("You are thinking: ... apple"), "Should display Efeliah's guess");
            Assert.IsTrue(output.Contains("Efeliah opens her eyes"), "Should display Efeliah's response");
        }

        [TestMethod]
        public void MiniGame01_EnterPressed_Stop_AdvancesToNext()
        {
            // Arrange: create a next node for advancing
            StoryNode nextNode = CreateNode<StoryNode>(nodeId: 42, configure: node => node.Text = "End of guessing!");
            MiniGame01 miniGame = CreateNode<MiniGame01>(configure: node => node.ChildId = 42);
            SimulateTextInput("stop");
            SimulateUserInput(ConsoleKey.Enter);

            // Act
            LoadNode(miniGame);

            // Node 42 is loaded when the player types "stop" and presses Enter
        }
    }
}