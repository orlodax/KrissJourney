using System.Collections.Generic;
using KrissJourney.Kriss.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Unit.Models;

/// <summary>
/// Boundary tests for the rage arithmetic in <see cref="SurgeState"/>. These pin the rules
/// documented on the type itself: rage clamps to [0, MaxRage]; exactly 0 is a loss; a wrong
/// input spends the penalty without moving the caret; a correct input advances the caret and
/// restores rage; reaching the last glyph wins; the outcome is sticky; and the row is inert
/// once it is over. The behavioural property that makes fast-and-sloppy beat slow-and-careful
/// is covered separately in <see cref="SurgeMomentumOverPatienceTests"/>.
/// </summary>
[TestClass]
public class SurgeStateTests
{
    static SurgeChallenge Challenge(float drainRate = 25f, float restore = 6f, float penalty = 8f, float startingRage = 100f, float maxRage = 100f) => new()
    {
        DrainRate = drainRate,
        Restore = restore,
        Penalty = penalty,
        StartingRage = startingRage,
        MaxRage = maxRage
    };

    static List<SurgeDirection> Row(params SurgeDirection[] directions) => [.. directions];

    [TestMethod]
    public void Drain_ToExactlyZero_Loses()
    {
        SurgeState state = new(Challenge(drainRate: 10, startingRage: 10), Row(SurgeDirection.Left, SurgeDirection.Up));

        state.Drain(1.0); // 10 rage/sec for 1 second empties the bar exactly

        Assert.AreEqual(0, state.Rage);
        Assert.AreEqual(SurgeOutcome.Lost, state.Outcome);
        Assert.IsTrue(state.IsOver);
    }

    [TestMethod]
    public void Drain_PastZero_ClampsAtZero_NeverGoesNegative()
    {
        SurgeState state = new(Challenge(drainRate: 100, startingRage: 10), Row(SurgeDirection.Left));

        state.Drain(5.0); // far more drain than there is rage to spend

        Assert.AreEqual(0, state.Rage);
        Assert.AreEqual(SurgeOutcome.Lost, state.Outcome);
    }

    [TestMethod]
    public void Construction_StartingRageAboveMax_ClampsToMaxRage()
    {
        SurgeState state = new(Challenge(startingRage: 500, maxRage: 100), Row(SurgeDirection.Left));

        Assert.AreEqual(100, state.Rage);
        Assert.AreEqual(1.0, state.RageFraction);
    }

    [TestMethod]
    public void ApplyInput_Correct_RestoresRage_ButNeverAboveMaxRage()
    {
        SurgeState state = new(Challenge(restore: 6, startingRage: 98, maxRage: 100), Row(SurgeDirection.Left, SurgeDirection.Up));

        bool correct = state.ApplyInput(SurgeDirection.Left);

        Assert.IsTrue(correct);
        Assert.AreEqual(100, state.Rage); // 98 + 6 = 104, clamped to 100
        Assert.AreEqual(1, state.Position);
    }

    [TestMethod]
    public void ApplyInput_Wrong_SpendsPenalty_AndDoesNotAdvanceTheCaret()
    {
        SurgeState state = new(Challenge(penalty: 8, startingRage: 50), Row(SurgeDirection.Left, SurgeDirection.Up));

        bool correct = state.ApplyInput(SurgeDirection.Up); // the row wants Left first

        Assert.IsFalse(correct);
        Assert.AreEqual(42, state.Rage);
        Assert.AreEqual(0, state.Position); // untouched - a miss never advances, but never resets either
        Assert.AreEqual(SurgeOutcome.InProgress, state.Outcome);
    }

    [TestMethod]
    public void ApplyInput_Wrong_NeverEndsTheRowOnItsOwn_UnlessItAlsoEmptiesTheRage()
    {
        // A single wrong input costing less than the remaining rage must leave the attempt
        // very much alive: mashing is punished, not instantly fatal.
        SurgeState state = new(Challenge(penalty: 8, startingRage: 50), Row(SurgeDirection.Left));

        state.ApplyInput(SurgeDirection.Up);

        Assert.IsFalse(state.IsOver);
    }

    [TestMethod]
    public void ApplyInput_OnLastGlyph_Wins()
    {
        SurgeState state = new(Challenge(startingRage: 50), Row(SurgeDirection.Left));

        bool correct = state.ApplyInput(SurgeDirection.Left);

        Assert.IsTrue(correct);
        Assert.AreEqual(SurgeOutcome.Won, state.Outcome);
        Assert.IsTrue(state.IsOver);
        Assert.AreEqual(state.Length, state.Position);
    }

    [TestMethod]
    public void Outcome_IsSticky_AWonRowCannotLaterDrainIntoALoss()
    {
        SurgeState state = new(Challenge(drainRate: 1000, startingRage: 50), Row(SurgeDirection.Left));

        state.ApplyInput(SurgeDirection.Left); // wins immediately, row is empty

        Assert.AreEqual(SurgeOutcome.Won, state.Outcome);

        state.Drain(1000); // an absurd amount of drain that would obliterate rage if it still mattered

        Assert.AreEqual(SurgeOutcome.Won, state.Outcome, "A won row must never be dragged into a loss by later drain.");
    }

    [TestMethod]
    public void ApplyInput_AfterTheRowIsOver_ReturnsFalseAndChangesNothing()
    {
        SurgeState state = new(Challenge(startingRage: 50), Row(SurgeDirection.Left));
        state.ApplyInput(SurgeDirection.Left); // wins, nothing left to press

        double rageBefore = state.Rage;
        int positionBefore = state.Position;

        bool result = state.ApplyInput(SurgeDirection.Left);

        Assert.IsFalse(result);
        Assert.AreEqual(rageBefore, state.Rage);
        Assert.AreEqual(positionBefore, state.Position);
        Assert.AreEqual(SurgeOutcome.Won, state.Outcome);
    }

    [TestMethod]
    public void Drain_AfterTheRowIsOver_DoesNothing()
    {
        SurgeState state = new(Challenge(drainRate: 25, startingRage: 0), Row(SurgeDirection.Left)); // Lost at construction

        state.Drain(1);

        Assert.AreEqual(0, state.Rage);
        Assert.AreEqual(SurgeOutcome.Lost, state.Outcome);
    }

    [TestMethod]
    public void EmptyRow_WinsAtConstruction()
    {
        SurgeState state = new(Challenge(), []);

        Assert.AreEqual(SurgeOutcome.Won, state.Outcome);
        Assert.IsTrue(state.IsOver);
        Assert.AreEqual(0, state.Length);
    }

    [TestMethod]
    public void ZeroStartingRage_LosesAtConstruction()
    {
        SurgeState state = new(Challenge(startingRage: 0), Row(SurgeDirection.Left, SurgeDirection.Up));

        Assert.AreEqual(SurgeOutcome.Lost, state.Outcome);
        Assert.IsTrue(state.IsOver);
    }
}
