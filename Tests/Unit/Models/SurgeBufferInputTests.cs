using System.Collections.Generic;
using KrissJourney.Kriss.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Unit.Models;

/// <summary>
/// <see cref="SurgeChallenge.BufferInput"/>: what a press that lands on one of Saberinne's
/// glyphs is worth. Left off it is swallowed exactly as before; turned on it is held in a
/// single slot (latest press wins) and spent the instant the caret comes back to Kriss,
/// through the same <c>Resolve</c> path a live press takes - so a buffered wrong arrow costs
/// the ordinary penalty and buffering is never a free correct answer.
///
/// Beat-boundary caution: DuetBeat is a float widened to double, so 0.3f is ~0.300000011920929,
/// strictly greater than the literal 0.3 and than 0.1 + 0.2. Every Drain below is written to
/// clear or miss the beat by a margin (0.31, or 0.29 then 0.02) and never to sit on it.
/// </summary>
[TestClass]
public class SurgeBufferInputTests
{
    static SurgeChallenge Challenge(
        float drainRate = 0f,
        float restore = 6f,
        float penalty = 8f,
        float startingRage = 50f,
        float maxRage = 100f,
        string duetPattern = ".S",
        float duetBeat = 0.3f,
        bool bufferInput = true) => new()
        {
            DrainRate = drainRate,
            Restore = restore,
            Penalty = penalty,
            StartingRage = startingRage,
            MaxRage = maxRage,
            DuetPattern = duetPattern,
            DuetBeat = duetBeat,
            BufferInput = bufferInput
        };

    static List<SurgeDirection> Row(params SurgeDirection[] directions) => [.. directions];

    /// <summary>The ".S" row every buffering case below is built on: his, hers, his.</summary>
    static List<SurgeDirection> HisHersHis() => Row(SurgeDirection.Left, SurgeDirection.Up, SurgeDirection.Right);

    [TestMethod]
    public void BufferInput_DefaultsToFalse()
    {
        // Swallowing is the behaviour every already-authored Surge was tuned against, so the
        // new option has to be opt-in or it would silently change all of them.
        Assert.IsFalse(new SurgeChallenge().BufferInput);
    }

    [TestMethod]
    public void PressOnHerGlyphIsStillSwallowedWhenBufferingIsOff()
    {
        // Regression guard for the PlaySaberinnesShare restructure: with the flag absent,
        // Position and Rage must match the pre-change numbers exactly.
        SurgeState state = new(Challenge(bufferInput: false), HisHersHis());

        state.ApplyInput(SurgeDirection.Left);                // his glyph: Position 0 -> 1, Rage 50 -> 56

        bool wrongArrow = state.ApplyInput(SurgeDirection.Down);  // wrong, and hers
        bool herOwnArrow = state.ApplyInput(SurgeDirection.Up);   // the row's own glyph, but still hers
        bool nextGlyph = state.ApplyInput(SurgeDirection.Right);  // the glyph AFTER hers - would buffer if the flag were on

        Assert.IsFalse(wrongArrow);
        Assert.IsFalse(herOwnArrow);
        Assert.IsFalse(nextGlyph);
        Assert.AreEqual(1, state.Position);
        Assert.AreEqual(56, state.Rage, "Nothing pressed on her glyph may spend or restore anything.");

        state.Drain(0.31); // her glyph resolves; nothing may come out of a slot that was never filled

        Assert.AreEqual(2, state.Position, "Only her own glyph should have resolved.");
        Assert.AreEqual(62, state.Rage);
        Assert.AreEqual(SurgeOutcome.InProgress, state.Outcome, "Glyph 2 is still owed - no buffered press may have taken it.");
    }

    [TestMethod]
    public void BufferedCorrectPressResolvesWhenCaretReturns()
    {
        SurgeState state = new(Challenge(), HisHersHis());

        state.ApplyInput(SurgeDirection.Left);   // his glyph: Position 0 -> 1, Rage 50 -> 56
        state.ApplyInput(SurgeDirection.Right);  // pressed on hers: glyph 2's own direction, held

        state.Drain(0.31); // her glyph resolves (Position 2), and the slot is spent on the way out

        Assert.AreEqual(3, state.Position, "The buffered press should take glyph 2 the instant the caret returns.");
        Assert.AreEqual(SurgeOutcome.Won, state.Outcome);
        Assert.AreEqual(68, state.Rage, "Two restores after the live press - hers, then the buffered one - and no penalty.");
    }

    [TestMethod]
    public void BufferedWrongPressTakesTheOrdinaryPenalty()
    {
        SurgeState state = new(Challenge(), HisHersHis());

        state.ApplyInput(SurgeDirection.Left);  // his glyph: Position 0 -> 1, Rage 50 -> 56
        state.ApplyInput(SurgeDirection.Left);  // pressed on hers: wrong for glyph 2, held anyway

        state.Drain(0.31);

        Assert.AreEqual(2, state.Position, "A wrong buffered press advances nothing, exactly like a wrong live press.");
        Assert.AreEqual(54, state.Rage, "Her restore, then the ordinary penalty: 56 + 6 - 8.");
        Assert.AreEqual(SurgeOutcome.InProgress, state.Outcome);
    }

    [TestMethod]
    public void LatestBufferedPressWins()
    {
        // Wrong then corrected: a player fixing themselves mid-run meant the second one.
        SurgeState corrected = new(Challenge(), HisHersHis());
        corrected.ApplyInput(SurgeDirection.Left);   // his glyph
        corrected.ApplyInput(SurgeDirection.Down);   // hers: wrong for glyph 2
        corrected.ApplyInput(SurgeDirection.Right);  // hers: corrected to glyph 2's own direction
        corrected.Drain(0.31);

        Assert.AreEqual(3, corrected.Position);
        Assert.AreEqual(SurgeOutcome.Won, corrected.Outcome);
        Assert.AreEqual(68, corrected.Rage, "The discarded wrong press must cost nothing.");

        // And the mirror: right then wrong is judged on the wrong one, with no free pass for
        // having been right a moment earlier.
        SurgeState spoiled = new(Challenge(), HisHersHis());
        spoiled.ApplyInput(SurgeDirection.Left);
        spoiled.ApplyInput(SurgeDirection.Right);    // hers: correct for glyph 2
        spoiled.ApplyInput(SurgeDirection.Down);     // hers: overwritten with a wrong one
        spoiled.Drain(0.31);

        Assert.AreEqual(2, spoiled.Position);
        Assert.AreEqual(54, spoiled.Rage, "The overwritten correct press must not restore anything.");
        Assert.AreEqual(SurgeOutcome.InProgress, spoiled.Outcome);
    }

    [TestMethod]
    public void BufferedPressIsSpentOnlyOnce()
    {
        // ".S" over five glyphs: his, hers, his, hers, his. One press held on her first glyph
        // must take glyph 2 and then be gone - it must not still be sitting there when her
        // second glyph hands the caret back at glyph 4.
        SurgeState state = new(Challenge(), Row(
            SurgeDirection.Left, SurgeDirection.Up, SurgeDirection.Right, SurgeDirection.Down, SurgeDirection.Left));

        state.ApplyInput(SurgeDirection.Left);   // glyph 0: Position 0 -> 1, Rage 50 -> 56
        state.ApplyInput(SurgeDirection.Right);  // held on her glyph 1: glyph 2's direction

        state.Drain(0.31); // her glyph 1 resolves, then the slot is spent on glyph 2 -> Position 3 (hers again)

        Assert.AreEqual(3, state.Position);

        state.Drain(0.31); // her glyph 3 resolves -> Position 4, and the empty slot must do nothing

        Assert.AreEqual(4, state.Position, "Glyph 4 is still owed: a spent press must not resolve a second time.");
        Assert.AreEqual(SurgeOutcome.InProgress, state.Outcome);
        Assert.AreEqual(74, state.Rage, "Four takes since the start: 50 + 6 * 4.");
    }

    [TestMethod]
    public void BufferedPressIsDroppedWhenHerGlyphEndsTheRow()
    {
        // Row "LU" under ".S": her glyph is the last one, so the take that consumes it also wins
        // the row. Nothing may resolve after the outcome - the held press is simply dropped.
        SurgeState state = new(Challenge(), Row(SurgeDirection.Left, SurgeDirection.Up));

        state.ApplyInput(SurgeDirection.Left);   // glyph 0: Position 0 -> 1, Rage 50 -> 56
        state.ApplyInput(SurgeDirection.Down);   // held on her final glyph, and wrong for anything

        state.Drain(0.31);

        Assert.AreEqual(SurgeOutcome.Won, state.Outcome);
        Assert.AreEqual(2, state.Position);
        Assert.AreEqual(62, state.Rage,
            "Exactly one restore after the live press - hers - and no penalty from the dropped one.");
    }

    [TestMethod]
    public void BufferedPressIsNotResolvedAfterALoss()
    {
        // Drain spends first and PlaySaberinnesShare runs second, so a tick that empties the bar
        // must end the row before the slot is looked at: no press resolves after a loss.
        SurgeState state = new(Challenge(drainRate: 100), HisHersHis());

        state.ApplyInput(SurgeDirection.Left);   // glyph 0: Position 0 -> 1, Rage 50 -> 56
        state.ApplyInput(SurgeDirection.Right);  // held on her glyph 1: correct for glyph 2

        state.Drain(0.6); // 60 rage of drain against 56 in the bar

        Assert.AreEqual(SurgeOutcome.Lost, state.Outcome);
        Assert.AreEqual(0, state.Rage);
        Assert.AreEqual(1, state.Position,
            "Neither her glyph nor the buffered press may resolve on the tick that lost the row.");
    }

    [TestMethod]
    public void BufferInputIsInertWithoutADuetPattern()
    {
        // Only her glyphs ever buffer, so with no duet at all the flag must change nothing.
        SurgeState buffered = new(Challenge(duetPattern: null, bufferInput: true), HisHersHis());
        SurgeState swallowed = new(Challenge(duetPattern: null, bufferInput: false), HisHersHis());

        foreach (SurgeState state in new[] { buffered, swallowed })
        {
            state.ApplyInput(SurgeDirection.Left);   // correct
            state.ApplyInput(SurgeDirection.Left);   // wrong for glyph 1
            state.ApplyInput(SurgeDirection.Up);     // correct
            state.Drain(0.31);
            state.ApplyInput(SurgeDirection.Right);  // correct, wins
        }

        Assert.AreEqual(swallowed.Position, buffered.Position);
        Assert.AreEqual(swallowed.Rage, buffered.Rage);
        Assert.AreEqual(swallowed.Outcome, buffered.Outcome);
        Assert.AreEqual(SurgeOutcome.Won, buffered.Outcome, "Both runs should have walked the whole row.");
    }

    [TestMethod]
    public void BufferedPressLandingBackOnHerGlyphDoesNotDisturbHerTempo()
    {
        // ".SS" over five glyphs: his, hers, hers, his, hers. The buffered press is spent on
        // glyph 3 and lands the caret straight back onto her glyph 4, and her clock has to start
        // that glyph from zero - the 0.05s left over from the run that just ended must not be
        // carried into it.
        SurgeState state = new(Challenge(duetPattern: ".SS"), Row(
            SurgeDirection.Left, SurgeDirection.Up, SurgeDirection.Right, SurgeDirection.Down, SurgeDirection.Left));

        state.ApplyInput(SurgeDirection.Left);   // glyph 0: Position 0 -> 1, Rage 50 -> 56
        state.ApplyInput(SurgeDirection.Down);   // held on her glyphs: glyph 3's own direction

        state.Drain(0.65); // two of her beats (0.6) with 0.05 to spare, then the slot takes glyph 3

        Assert.AreEqual(4, state.Position, "Her run of two, then the buffered press, should stop on her glyph 4.");

        state.Drain(0.29); // had the 0.05 remainder leaked through, 0.05 + 0.29 would already clear the beat

        Assert.AreEqual(4, state.Position, "Her next glyph must still need a full beat of its own.");
        Assert.AreEqual(SurgeOutcome.InProgress, state.Outcome);

        state.Drain(0.02); // 0.31 on this glyph alone

        Assert.AreEqual(5, state.Position);
        Assert.AreEqual(SurgeOutcome.Won, state.Outcome);
        Assert.AreEqual(80, state.Rage, "Five takes in all: 50 + 6 * 5.");
    }
}
