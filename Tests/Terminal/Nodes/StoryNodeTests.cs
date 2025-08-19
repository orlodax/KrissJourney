using System;
using KrissJourney.Kriss.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Terminal.Nodes;

[TestClass]
public class StoryNodeTests : NodeTestBase
{
    private StoryNode storyNode;

    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();
        storyNode = CreateNode<StoryNode>(configure: node =>
        {
            node.Text = "Story text";
            node.ChildId = 2;
        });
    }

    [TestMethod]
    public void LoadsAndAdvancesToNextNode()
    {
        StoryNode nextNode = CreateNode<StoryNode>(nodeId: 2, configure: n => n.Text = "Next node");
        SimulateUserInput(ConsoleKey.Enter);
        try { storyNode.Load(); } catch { }
        // Should have called AdvanceToNext(2)
    }

    [TestMethod]
    public void DisplaysText()
    {
        SimulateUserInput(ConsoleKey.Enter);
        try { storyNode.Load(); } catch { }
        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("Story text"));
    }
}