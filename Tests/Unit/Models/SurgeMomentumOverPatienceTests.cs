using System.Collections.Generic;
using KrissJourney.Kriss.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Unit.Models;

/// <summary>
/// Pins the one property issue 4 exists to deliver: at identical tuning, Surge must reward
/// momentum over patience - the mirror image of the FightNode QTE, which rewards waiting for
/// the right beat. Fast, sloppy play should win where slow, careful play loses.
///
/// If a future refactor makes "careful" beat "fast" again, it has quietly turned Surge back
/// into a Fight, and this test - and only this test - is what catches it. Do not soften or
/// remove this assertion to make a change pass; if it goes red, the mechanic changed, and the
/// mechanic is the acceptance criterion (see .issues/issue 4.md).
/// </summary>
[TestClass]
public class SurgeMomentumOverPatienceTests
{
    [TestMethod]
    public void FastAndSloppy_BeatsSlowAndAccurate_AtIdenticalShippedDefaultTuning()
    {
        SurgeChallenge challenge = new(); // shipped defaults, untouched: DrainRate 25, Restore 6, Penalty 8, StartingRage/MaxRage 100
        List<SurgeDirection> row =
        [
            SurgeDirection.Left, SurgeDirection.Up, SurgeDirection.Right, SurgeDirection.Down,
            SurgeDirection.Left, SurgeDirection.Up, SurgeDirection.Right, SurgeDirection.Down,
        ];

        // Quick and dirty: 0.2s between presses, and every third press is deliberately wrong.
        SurgeOutcome fastAndSloppy = Drive(challenge, row, secondsPerKey: 0.2, wrongEveryNth: 3);

        // Slow and careful: a full second to line up every press, and never a single mistake.
        SurgeOutcome slowAndAccurate = Drive(challenge, row, secondsPerKey: 1.0, wrongEveryNth: 0);

        Assert.AreEqual(SurgeOutcome.Won, fastAndSloppy,
            "Fast play with one mistake in three should still win a Surge - that is the whole point of the mechanic.");
        Assert.AreEqual(SurgeOutcome.Lost, slowAndAccurate,
            "Slow, mistake-free play should still lose to the drain - patience must not be rewarded here.");
    }

    /// <summary>
    /// Drives a <see cref="SurgeState"/> the way real play would: elapsed time passes before
    /// every keypress (that is the continuous drain), and every <paramref name="wrongEveryNth"/>
    /// attempt (0 disables this entirely) presses a glyph other than the one the row wants -
    /// which spends the penalty without ever advancing the caret, exactly like a real miss.
    /// </summary>
    static SurgeOutcome Drive(SurgeChallenge challenge, IReadOnlyList<SurgeDirection> row, double secondsPerKey, int wrongEveryNth)
    {
        SurgeState state = new(challenge, row);
        int attempt = 0;
        int guard = row.Count * 100; // generous safety net against ever looping forever on a regression

        while (!state.IsOver && attempt < guard)
        {
            state.Drain(secondsPerKey);

            if (state.IsOver)
                break;

            SurgeDirection wanted = state.Glyphs[state.Position];
            bool playWrong = wrongEveryNth > 0 && attempt % wrongEveryNth == wrongEveryNth - 1;

            state.ApplyInput(playWrong ? WrongDirection(wanted) : wanted);
            attempt++;
        }

        Assert.IsTrue(state.IsOver, "Drive loop guard hit - the Surge never resolved.");

        return state.Outcome;
    }

    static SurgeDirection WrongDirection(SurgeDirection correct) =>
        correct == SurgeDirection.Left ? SurgeDirection.Right : SurgeDirection.Left;
}
