using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KrissJourney.Kriss.Helpers;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Terminal.Nodes;

[TestClass]
public class FightNodeTests : NodeTestBase
{
    private FightNode _fightNode;
    private Prowess _prowess;

    // Helper class for predictable random numbers
    public class PredictableRandom(params int[] values) : Random
    {
        private readonly Queue<int> _values = new(values);

        public override int Next()
        {
            if (_values.Count > 0)
                return _values.Dequeue();

            throw new InvalidOperationException("PredictableRandom queue is empty.");
        }

        public override int Next(int maxValue)
        {
            if (_values.Count > 0)
                return _values.Dequeue();

            throw new InvalidOperationException("PredictableRandom queue is empty.");
        }

        public override int Next(int minValue, int maxValue)
        {
            if (_values.Count > 0)
                return _values.Dequeue();

            throw new InvalidOperationException("PredictableRandom queue is empty.");
        }
    }

    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();

        _prowess = ProwessHelper.GetProwess(10); // Assuming chapter 1 prowess

        _fightNode = CreateNode<FightNode>(configure: node =>
        {
            node.Encounter = new Encounter
            {
                Foes = [new Foe { Name = "Goblin", Health = 20, Damage = 5 }],
                VictoryMessage = "You are victorious!",
                DefeatMessage = "You have been defeated."
            };
            node.ChildId = 2;
        });

        // Set chapter for predictable prowess
        TestRunner.TestChapter = new Chapter
        {
            Id = 10,
            Title = "Test Chapter",
            Nodes = []
        };

        CreateNode<StoryNode>(nodeId: 2); // Dummy next node
    }

    private void SetPredictableRandom(params int[] values)
    {
        var predictableRandom = new PredictableRandom(values);
        FieldInfo randomField = typeof(FightNode).GetField("random", BindingFlags.Instance | BindingFlags.NonPublic);
        randomField.SetValue(_fightNode, predictableRandom);
    }

    [TestMethod]
    public void FailDodgeAndPerfectAttack_DefeatsFoe()
    {
        // Arrange
        // Foe Attack: Key=Up(0), Target=10 (fail, cursor is at 1)
        // Player Attack: Key=Down(1), Target=1 (perfect)
        // PerfectTimingBonus: returns prowess.BaseDamage / 3 - 1
        int perfectBonus = _prowess.BaseDamage / 3 - 1;
        if (perfectBonus < _prowess.BaseDamage / 10) perfectBonus = _prowess.BaseDamage / 10;

        SetPredictableRandom(0, 10, 1, 1, perfectBonus);

        SimulateUserInput(ConsoleKey.UpArrow);   // Fail dodge
        SimulateUserInput(ConsoleKey.DownArrow); // Perfect attack
        SimulateUserInput(ConsoleKey.Enter);     // Advance after victory

        // Act
        LoadNode(_fightNode);

        // Assert
        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("You were hit, taking 5 damage!"), "Should confirm being hit.");

        int expectedDamage = _prowess.BaseDamage + _prowess.RageBonus + perfectBonus;
        Assert.IsTrue(output.Contains($"Critical hit! You deal {expectedDamage} damage to Goblin!"), "Should confirm critical hit.");
        Assert.IsTrue(output.Contains("Goblin is defeated!"), "Should confirm foe is defeated.");
        Assert.IsTrue(output.Contains("You are victorious!"), "Should show victory message.");
    }

    [TestMethod]
    public void HealthDepleted_LeadsToGameOver()
    {
        // Arrange
        _fightNode.Encounter.Foes.First().Damage = _prowess.MaxHealth; // Ensure one-hit defeat

        // Foe Attack: Key=Up(0), Target=10 (fail)
        SetPredictableRandom(0, 10);

        SimulateUserInput(ConsoleKey.UpArrow); // Fail dodge
        SimulateUserInput(ConsoleKey.Enter);   // Advance after defeat

        // Act
        LoadNode(_fightNode);

        // Assert
        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("Your health has been depleted!"), "Should show health depleted message.");
        Assert.IsTrue(output.Contains("You have been defeated."), "Should show defeat message.");
    }

    [TestMethod]
    public void PerfectDodgeAndMissedAttack_FoeSurvives()
    {
        // Arrange
        _fightNode.Encounter.Foes.First().Health = 999; // Make foe beefy

        // Foe Attack: Key=Up(0), Target=1 (perfect)
        // Player Attack: Key=Down(1), Target=10 (fail)
        // PerfectTimingBonus: returns prowess.BaseDamage / 10
        int perfectBonus = _prowess.BaseDamage / 10;
        SetPredictableRandom(0, 1, 1, 10, perfectBonus);

        SimulateUserInput(ConsoleKey.UpArrow);   // Perfect dodge
        SimulateUserInput(ConsoleKey.DownArrow); // Fail attack

        // Act
        // This will loop, so we expect an exception from the test framework
        // about unconsumed input, after we add some dummy input.
        SimulateUserInput(ConsoleKey.Escape); // Dummy input to be left in queue
        try
        {
            LoadNode(_fightNode);
            Assert.Fail("Node loop should have prevented this from being reached without more input.");
        }
        catch (InvalidOperationException ex)
        {
            // The predictable random runs out of numbers
            Assert.IsTrue(ex.Message.Contains("PredictableRandom queue is empty"));
        }

        // Assert
        string output = TerminalMock.GetOutput();
        Assert.IsTrue(output.Contains("Perfect parry! You gained an edge."), "Should confirm perfect parry.");
        Assert.IsTrue(output.Contains("You missed!"), "Should confirm missed attack.");
        Assert.IsFalse(output.Contains("Goblin is defeated!"), "Foe should not be defeated.");

        // Cleanup the dummy input
        TerminalCleanup();
    }
}
