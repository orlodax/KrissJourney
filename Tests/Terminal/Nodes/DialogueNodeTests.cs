using System;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Terminal.Nodes;

[TestClass]
public class DialogueNodeTests : NodeTestBase
{
    private DialogueNode dialogueNode;

    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
        dialogueNode = CreateNode<DialogueNode>(configure: node =>
        {
            node.Id = 1;
            node.Dialogues =
            [
                new DialogueLine
                {
                    Actor = EnCharacter.Corolla, Line = "Hello", Break = true, Replies =
                    [
                        new() { Line = "Reply1", ChildId = 2 },
                        new() { Line = "Reply2", NextLine = "L2" }
                    ]
                },
                new DialogueLine { Actor = EnCharacter.Kriss, Line = "World", LineName = "L2", ChildId = 3 },
            ];
        });
    }

    [TestMethod]
    public void DisplaysDialogueAndReplies()
    {
        SimulateUserInput(ConsoleKey.Enter);

        LoadNode(dialogueNode);

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("Hello"));
        Assert.IsTrue(output.Contains("Reply1"));
        Assert.IsTrue(output.Contains("Reply2"));
    }

    [TestMethod]
    public void SelectingReplyWithChildId_AdvancesToNextNode()
    {
        SimulateUserInput(ConsoleKey.Enter, ConsoleKey.Enter); // one for wait for key, one to select first reply
        _ = CreateNode<StoryNode>(nodeId: 2, configure: n => n.Text = "Next node loaded!");

        LoadNode(dialogueNode);
        // Should have called AdvanceToNext with 2 (would throw if not found)
    }

    [TestMethod]
    public void SelectingReplyWithNextLine_ContinuesDialogue()
    {
        SimulateUserInput(ConsoleKey.Enter, ConsoleKey.DownArrow, ConsoleKey.Enter); // select second reply
        _ = CreateNode<StoryNode>(nodeId: 3, configure: n => n.Text = "Next node loaded!");

        LoadNode(dialogueNode);

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("World"));
    }

    [TestMethod]
    public void SelectingReplyWithInvalidChildId_Throws()
    {
        // Add a reply with a ChildId that does not exist
        dialogueNode.Dialogues[0].Replies.Add(new Reply { Line = "Ghost", ChildId = 99 });
        SimulateUserInput(ConsoleKey.Enter, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter); // select "Ghost"

        Assert.ThrowsException<ArgumentNullException>(dialogueNode.Load);

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("Hello"));
        Assert.IsTrue(output.Contains("Ghost"));
        TerminalMock.ResetOutput();
    }

    [TestMethod]
    public void DialogueWithNoReplies_ContinuesToNextDialogue()
    {
        // Add a dialogue with no replies, should auto-continue to next
        dialogueNode.Dialogues =
        [
            new DialogueLine { Actor = EnCharacter.Smiurl, Line = "No replies here" },
            new DialogueLine { Actor = EnCharacter.Theo, Line = "World", LineName = "L2", ChildId = 3 }
        ];
        SimulateUserInput(ConsoleKey.Enter, ConsoleKey.Enter); // Just advance
        _ = CreateNode<StoryNode>(nodeId: 3, configure: n => { n.Text = "Next node loaded!"; n.ChildId = 1; });

        LoadNode(dialogueNode);

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("World"));
        Assert.IsTrue(output.Contains("No replies here"));
    }

    [TestMethod]
    public void DialogueWithBreak_WaitsForEnter()
    {
        // Add a dialogue with Break = false, should wait for Enter
        dialogueNode.Dialogues.Insert(0, new DialogueLine { Actor = EnCharacter.Efeliah, Line = "Keep going", Break = true, Replies = [] });
        SimulateUserInput(ConsoleKey.Enter); // Should advance to the next line

        LoadNode(dialogueNode);

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("Keep going"));
        Assert.IsTrue(output.Contains("Hello"));
    }

    [TestMethod]
    public void SelectingReplyWithNextLineAndChildId_RendersNextLineThenAdvancesToChildId()
    {
        dialogueNode.Dialogues[0].Replies.Add(new Reply { Line = "Both", NextLine = "L2", ChildId = 3 });
        _ = CreateNode<StoryNode>(nodeId: 3, configure: n => { n.Text = "Advanced to 3!"; n.ChildId = 1; });
        SimulateUserInput(ConsoleKey.Enter, ConsoleKey.DownArrow, ConsoleKey.DownArrow, ConsoleKey.Enter); // select "Both"

        LoadNode(dialogueNode);

        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("World"));
        Assert.IsTrue(output.Contains("Advanced to 3!"));
    }

    [TestMethod]
    public void UnselectedReplies_AreAllNarratorColored()
    {
        // The speaker is Corolla (Red): if a reply row inherits the color left over by
        // Typist.RenderLine, the first row paints red instead of the narrator's DarkCyan.
        dialogueNode.Dialogues[0].Break = false;
        _ = CreateNode<StoryNode>(nodeId: 3, configure: n => { n.Text = "Next node loaded!"; n.ChildId = 1; });
        SimulateUserInput(ConsoleKey.DownArrow, ConsoleKey.Enter); // move off row 1, then pick row 2

        LoadNode(dialogueNode);

        (ConsoleColor Foreground, ConsoleColor Background, string Text) firstRow =
            TerminalMock.ColoredWrites.FindLast(w => w.Text == "1. Reply1");

        Assert.IsNotNull(firstRow.Text, "The first reply row was never written.");
        Assert.AreEqual(ConsoleColor.DarkCyan, firstRow.Foreground,
            "An unselected reply must render in the narrator color, not the speaking actor's.");
        Assert.AreEqual(ConsoleColor.Black, firstRow.Background,
            "An unselected reply must render on the default background.");
    }

    /// <summary>
    /// Two reply blocks with no break between them (c16's Øder story, c22's soulmate reveal).
    /// The arrow-key redraw rewinds to the start of the current beat: if it rewinds past the
    /// earlier block instead, that block asks its question again and swallows the Enter meant
    /// for this one, leaving the second choice unreachable.
    /// </summary>
    [TestMethod]
    public void ArrowKeyOnSecondReplyBlock_RedrawsThatBlockAndNotTheFirst()
    {
        dialogueNode.Dialogues =
        [
            new DialogueLine
            {
                Actor = EnCharacter.Corolla, Line = "FirstQuestion", Replies =
                [
                    new() { Line = "A1", NextLine = "L2" },
                    new() { Line = "A2", NextLine = "L2" }
                ]
            },
            new DialogueLine
            {
                Actor = EnCharacter.Theo, Line = "SecondQuestion", LineName = "L2", Replies =
                [
                    new() { Line = "B1", NextLine = "L3" },
                    new() { Line = "B2", NextLine = "L3" }
                ]
            },
            new DialogueLine { Actor = EnCharacter.Kriss, Line = "TheEnd", LineName = "L3", ChildId = 3 }
        ];
        _ = CreateNode<StoryNode>(nodeId: 3, configure: n => n.Text = "Next node loaded!");
        SimulateUserInput(ConsoleKey.Enter, ConsoleKey.DownArrow, ConsoleKey.Enter); // A1, then move to B2 and take it

        LoadNode(dialogueNode);

        string output = TerminalMock.GetOutput();
        Assert.AreEqual(1, CountOf(output, "FirstQuestion"),
            "The redraw must not replay the first block's question, or its prompt eats the next keypress.");
        Assert.AreEqual(2, CountOf(output, "SecondQuestion"),
            "The second block should render once on arrival and once on the arrow-key redraw.");
        Assert.IsTrue(output.Contains("TheEnd"), "The second block's reply should have been reachable.");
    }

    static int CountOf(string haystack, string needle)
    {
        int count = 0;
        for (int i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;

        return count;
    }
}
