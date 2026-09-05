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
    static SurgeChallenge Challenge(float drainRate = 25f, float restore = 6f, float penalty = 8f, float startingRage = 100f, float maxRage = 100f, string duetPattern = null, float duetBeat = 0.3f) => new()
    {
        DrainRate = drainRate,
        Restore = restore,
        Penalty = penalty,
        StartingRage = startingRage,
        MaxRage = maxRage,
        DuetPattern = duetPattern,
        DuetBeat = duetBeat
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

/// <summary>
/// The duet mechanic: <see cref="SurgeState.IsSaberinnes"/> marks part of the row as hers, and
/// her share plays itself on a beat clock rather than through player input. Pins that her
/// glyphs restore rage like a correct press, that pressing one of hers is swallowed rather
/// than charged, and that her beat clock does not drift or leak across a turn boundary.
/// </summary>
[TestClass]
public class SurgeDuetPatternTests
{
    static SurgeChallenge Challenge(float drainRate = 0f, float restore = 6f, float penalty = 8f, float startingRage = 100f, float maxRage = 100f, string duetPattern = ".S", float duetBeat = 0.3f) => new()
    {
        DrainRate = drainRate,
        Restore = restore,
        Penalty = penalty,
        StartingRage = startingRage,
        MaxRage = maxRage,
        DuetPattern = duetPattern,
        DuetBeat = duetBeat
    };

    static List<SurgeDirection> Row(params SurgeDirection[] directions) => [.. directions];

    [TestMethod]
    public void SaberinnesGlyph_ResolvesAfterTheBeat_RestoringRageLikeACorrectInput()
    {
        SurgeState state = new(Challenge(restore: 6, startingRage: 50), Row(SurgeDirection.Left, SurgeDirection.Up));

        state.ApplyInput(SurgeDirection.Left); // Kriss's glyph: Position 0 -> 1, Rage 50 -> 56
        state.Drain(0.29); // short of the 0.3s beat

        Assert.AreEqual(1, state.Position, "Her glyph should not resolve before the beat elapses.");
        Assert.AreEqual(56, state.Rage);

        state.Drain(0.02); // crosses the beat

        Assert.AreEqual(2, state.Position);
        Assert.AreEqual(62, state.Rage, "Her glyph should restore rage exactly like a correct input.");
        Assert.AreEqual(SurgeOutcome.Won, state.Outcome);
    }

    [TestMethod]
    public void InputOnSaberinnesGlyph_IsSwallowed_AndCostsNothing()
    {
        SurgeState state = new(Challenge(startingRage: 50), Row(SurgeDirection.Left, SurgeDirection.Up));
        state.ApplyInput(SurgeDirection.Left); // Position now 1, which is hers

        double rageBefore = state.Rage;

        bool wrongArrow = state.ApplyInput(SurgeDirection.Down); // wrong, and also hers
        bool herOwnArrow = state.ApplyInput(SurgeDirection.Up);  // the row's own glyph, but still hers

        Assert.IsFalse(wrongArrow);
        Assert.IsFalse(herOwnArrow);
        Assert.AreEqual(rageBefore, state.Rage, "Neither call should spend a penalty on her glyph.");
        Assert.AreEqual(1, state.Position, "Neither call should advance the caret.");
        Assert.AreNotEqual(SurgeOutcome.Lost, state.Outcome);
    }

    [TestMethod]
    public void RunOfHerGlyphs_ResolvesOnePerBeat()
    {
        SurgeState state = new(Challenge(duetPattern: "S"), Row(SurgeDirection.Left, SurgeDirection.Up, SurgeDirection.Right));

        state.Drain(0.31); // just past one beat (DuetBeat is a float widened to double, so exact 0.3 undershoots)

        Assert.AreEqual(1, state.Position, "One beat should resolve exactly one of her glyphs, never more.");
    }

    [TestMethod]
    public void WithoutADuetPattern_NothingResolvesItself()
    {
        SurgeState state = new(Challenge(duetPattern: null), Row(SurgeDirection.Left, SurgeDirection.Up));

        state.Drain(100); // zero drain rate, huge elapsed time

        Assert.AreEqual(0, state.Position);
        Assert.AreEqual(SurgeOutcome.InProgress, state.Outcome);
    }

    [TestMethod]
    public void ZeroDrainZeroPenalty_CannotBeLost()
    {
        SurgeState state = new(Challenge(drainRate: 0, penalty: 0, duetPattern: null, startingRage: 100), Row(SurgeDirection.Left, SurgeDirection.Up));

        for (int i = 0; i < 20; i++)
        {
            state.ApplyInput(SurgeDirection.Right); // always wrong for Position 0/1
            state.Drain(1_000_000);
            Assert.AreNotEqual(SurgeOutcome.Lost, state.Outcome);
        }

        state.ApplyInput(SurgeDirection.Left);
        state.ApplyInput(SurgeDirection.Up);

        Assert.AreEqual(SurgeOutcome.Won, state.Outcome);
    }

    [TestMethod]
    public void RunOfHerGlyphs_TakesExactlyAsManyBeatsAsGlyphs_WithoutDrifting()
    {
        // A run of hers should cost precisely count * beat, whether the elapsed time arrives in
        // one lump or dribbled across several Drain calls - the remainder must carry between them.
        SurgeState state = new(Challenge(duetPattern: "S"), Row(SurgeDirection.Left, SurgeDirection.Up));

        state.Drain(0.29); // banked, short of the beat
        Assert.AreEqual(0, state.Position);

        state.Drain(0.02); // 0.31 total: first glyph resolves, no remainder left over
        Assert.AreEqual(1, state.Position);

        state.Drain(0.29); // banked again on the second glyph
        Assert.AreEqual(1, state.Position);

        state.Drain(0.02); // another 0.31 total: second glyph resolves
        Assert.AreEqual(2, state.Position);
        Assert.AreEqual(SurgeOutcome.Won, state.Outcome, "Two beats of ~0.3s should clear a two-glyph run - no drift.");
    }

    [TestMethod]
    public void DuetPending_ResetsWhenCaretLeavesHerShare_SoBankedTimeDoesNotSpillToALaterGlyph()
    {
        // Pattern "S.S": her glyph, then Kriss's, then hers again. Banking 0.1s of overflow on
        // the first glyph must not shave 0.1s off the beat the second one needs.
        SurgeState state = new(Challenge(duetPattern: "S."), Row(SurgeDirection.Left, SurgeDirection.Up, SurgeDirection.Right));

        state.Drain(0.4); // resolves glyph 0 (needs 0.3) with 0.1s that would otherwise overflow
        Assert.AreEqual(1, state.Position);

        state.ApplyInput(SurgeDirection.Up); // Kriss's glyph, correct: Position 1 -> 2 (hers again)
        Assert.AreEqual(2, state.Position);

        state.Drain(0.2); // if the 0.1s overflow had leaked through, 0.1 + 0.2 would already clear the beat
        Assert.AreEqual(2, state.Position, "Banked time from an earlier glyph must not spill into this one.");
        Assert.AreEqual(SurgeOutcome.InProgress, state.Outcome);

        state.Drain(0.11); // now a full fresh beat has elapsed on this glyph
        Assert.AreEqual(3, state.Position);
        Assert.AreEqual(SurgeOutcome.Won, state.Outcome);
    }

    [TestMethod]
    public void SaberinnesGlyph_RestoresRage_ButNeverAboveMaxRage()
    {
        SurgeState state = new(Challenge(restore: 6, startingRage: 98, maxRage: 100, duetPattern: "S"), Row(SurgeDirection.Left));

        state.Drain(0.31); // just past one beat

        Assert.AreEqual(100, state.Rage, "98 + 6 clamps to 100, exactly like a normal correct input.");
    }

    [TestMethod]
    public void SaberinnesWin_CannotLaterBeDrainedIntoALoss()
    {
        SurgeState state = new(Challenge(drainRate: 25, duetPattern: "S", startingRage: 50), Row(SurgeDirection.Left));

        state.Drain(0.31); // her only glyph resolves, winning the row (small drain along the way, survivable)

        Assert.AreEqual(SurgeOutcome.Won, state.Outcome);

        state.Drain(1000); // an absurd amount of drain that would have emptied the bar had the row still been live

        Assert.AreEqual(SurgeOutcome.Won, state.Outcome, "A row Saberinne wins must never be dragged into a loss by later drain.");
    }
}
