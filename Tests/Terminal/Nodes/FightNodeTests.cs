using System;
using System.Threading.Tasks;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Terminal.Nodes;

[TestClass]
public class FightNodeTests : NodeTestBase
{
    private FightNode _fightNode;

    [TestInitialize]
    public override void TestInitialize()
    {
        base.TestInitialize();

        // Set chapter for predictable prowess
        TestRunner.TestChapter = new Chapter
        {
            Id = 10,
            Title = "Test Chapter",
            Nodes = []
        };

        CreateNode<StoryNode>(nodeId: 2); // Dummy next node
    }

    [TestMethod]
    public void FailDodgeAndPerfectAttack_DefeatsFoe()
    {
        _fightNode = CreateNode<FightNode>(configure: node =>
        {
            node.Encounter = new Encounter
            {
                Foes = [new Foe { Name = "Goblin", Health = 1, Damage = 5 }],
                VictoryMessage = "You are victorious!",
                DefeatMessage = "You have been defeated.",
                QteSpeedFactor = 2.0f,
                QteCycles = 1,
                QteLength = 5,
                QteWidth = 0
            };
            node.ChildId = 2;
        });

        // Arrange: we won't rely on predictable random here. We'll read the prompted direction from output.

        // Drive a precise script relative to output positions
        Task script = Task.Run(async () =>
        {
            int idx = 0;
            idx = await WaitForOutputIndexAsync("Press a key to continue...", idx); // pre-fight prompt
            SimulateUserInput(ConsoleKey.Enter);

            // Foe dodge prompt (any direction) — deduce by scanning before first QTE frame
            (idx, ConsoleKey foeKey, _) = await WaitForDirectionBeforeQteAsync(idx, isDodge: true);
            // do nothing to guarantee hit

            idx = await WaitForOutputIndexAsync("You were hit, taking", idx); // result printed
            idx = await WaitForOutputIndexAsync("Press a key to continue...", idx);
            SimulateUserInput(ConsoleKey.Enter);

            // Player strike prompt (any direction) — deduce by scanning before QTE frame
            (idx, ConsoleKey playerKey, _) = await WaitForDirectionBeforeQteAsync(idx, isDodge: false);
            await Task.Delay(50);
            SimulateUserInput(playerKey); // success or perfect depending on target

            idx = await WaitForOutputIndexAsync("Press a key to continue...", idx);
            SimulateUserInput(ConsoleKey.Enter);
            idx = await WaitForOutputIndexAsync("You are victorious!", idx);
        });

        // Act
        LoadNode(_fightNode);
        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(6)), "Input script timed out.");

        // Assert
        string out1 = TerminalMock.GetOutput();
        Assert.IsTrue(WaitForOutputContains("You were hit, taking 5 damage!"), $"Should confirm being hit.\nOUTPUT:\n{out1}");

        // We accept either a normal or critical hit here, since timing isn't controlled
        string out2 = TerminalMock.GetOutput();
        bool hasNormal = out2.Contains("You deal ") && out2.Contains("damage to Goblin.");
        bool hasCritical = out2.Contains("Critical hit! You deal ");
        Assert.IsTrue(hasNormal || hasCritical, $"Should confirm damage dealt.\nOUTPUT:\n{out2}");
        string out3 = TerminalMock.GetOutput();
        Assert.IsTrue(WaitForOutputContains("Goblin is defeated!"), $"Should confirm foe is defeated.\nOUTPUT:\n{out3}");
        string out4 = TerminalMock.GetOutput();
        Assert.IsTrue(WaitForOutputContains("You are victorious!"), $"Should show victory message.\nOUTPUT:\n{out4}");
    }

    [TestMethod]
    public void HealthDepleted_LeadsToGameOver()
    {
        // Arrange
        _fightNode = CreateNode<FightNode>(configure: node =>
        {
            node.Encounter = new Encounter
            {
                Foes = [new Foe { Name = "Goblin", Health = 1, Damage = 30 }],
                VictoryMessage = "You are victorious!",
                DefeatMessage = "You have been defeated.",
                QteSpeedFactor = 2.0f,
                QteCycles = 1,
                QteLength = 5,
                QteWidth = 0
            };
            node.ChildId = 2;
        });

        // No reliance on predictable random; we'll detect direction and just not press any key

        Task script2 = Task.Run(async () =>
        {
            int idx = 0;
            idx = await WaitForOutputIndexAsync("Press a key to continue...", idx);
            SimulateUserInput(ConsoleKey.Enter);
            (idx, _, _) = await WaitForDirectionBeforeQteAsync(idx, isDodge: true); // QTE started (idx returned at '<')
            // no key -> hit and defeat
            idx = await WaitForOutputIndexAsync("You have been defeated.", idx);
        });

        // Act
        LoadNode(_fightNode);
        Assert.IsTrue(script2.Wait(TimeSpan.FromSeconds(5)), "Input script #2 timed out.");

        // Assert
        // Production now shows defeat text without a specific 'depleted' line
        Assert.IsTrue(WaitForOutputContains("You have been defeated."), "Should show defeat message.");
    }

    [TestMethod]
    public void PerfectDodgeAndMissedAttack_FoeSurvives()
    {
        // Arrange
        _fightNode = CreateNode<FightNode>(configure: node =>
        {
            node.Encounter = new Encounter
            {
                Foes = [new Foe { Name = "Goblin", Health = 100, Damage = 5 }],
                VictoryMessage = "You are victorious!",
                DefeatMessage = "You have been defeated.",
                QteSpeedFactor = 2.0f,
                QteCycles = 1,
                QteLength = 5,
                QteWidth = 0
            };
            node.ChildId = 2;
        });

        // We'll react to whatever direction is prompted rather than overriding Random

        Task script3 = Task.Run(async () =>
        {
            int idx = 0;
            idx = await WaitForOutputIndexAsync("Press a key to continue...", idx);
            SimulateUserInput(ConsoleKey.Enter);
            (idx, ConsoleKey dodgeKey, _) = await WaitForDirectionBeforeQteAsync(idx, isDodge: true);
            await Task.Delay(30);
            SimulateUserInput(dodgeKey); // attempt a successful dodge (perfect not guaranteed)
            idx = await WaitForOutputIndexAsync("Press a key to continue...", idx);
            SimulateUserInput(ConsoleKey.Enter);
            (idx, ConsoleKey strikeKey, _) = await WaitForDirectionBeforeQteAsync(idx, isDodge: false);
            // no key -> miss
        });

        // Act
        LoadNode(_fightNode);
        Assert.IsTrue(script3.Wait(TimeSpan.FromSeconds(5)), "Input script #3 timed out.");

        // Assert
        // Accept either a perfect or a good dodge
        string defendOut = TerminalMock.GetOutput();
        bool perfectOrGood = defendOut.Contains("Perfect parry!") || defendOut.Contains("Good dodge!");
        Assert.IsTrue(perfectOrGood, "Should confirm at least a successful dodge.");
        Assert.IsTrue(WaitForOutputContains("You missed!"), "Should confirm missed attack.");
        string output = TerminalMock.GetOutput();
        Assert.IsFalse(output.Contains("Goblin is defeated!"), "Foe should not be defeated.");

        // Cleanup any leftover input for this test only
        TerminalCleanup();
    }

    // Wait for the QTE frame '<', then deduce the preceding direction prompt between startIdx and that point
    private async Task<(int nextIdx, ConsoleKey key, string direction)> WaitForDirectionBeforeQteAsync(int startIdx, bool isDodge)
    {
        int qteIdx = await WaitForOutputIndexAsync("<", startIdx);
        string output = TerminalMock.GetOutput();
        int pressIdx = output.LastIndexOf("Press ", qteIdx, StringComparison.Ordinal);
        string suffix = isDodge ? " to dodge!" : " to strike!";
        if (pressIdx >= 0)
        {
            int toIdx = output.IndexOf(suffix, pressIdx, StringComparison.Ordinal);
            if (toIdx > pressIdx)
            {
                int dirStart = pressIdx + "Press ".Length;
                string dir = output[dirStart..toIdx].Trim();
                ConsoleKey key = dir switch
                {
                    "UP" => ConsoleKey.UpArrow,
                    "DOWN" => ConsoleKey.DownArrow,
                    "LEFT" => ConsoleKey.LeftArrow,
                    "RIGHT" => ConsoleKey.RightArrow,
                    _ => ConsoleKey.NoName
                };
                return (qteIdx, key, dir);
            }
        }
        throw new TimeoutException($"Direction prompt not found before QTE frame: '{(isDodge ? "dodge" : "strike")}'");
    }
}
