using System;
using System.Collections.Generic;

namespace KrissJourney.Kriss.Models;

public enum SurgeOutcome
{
    InProgress,
    Won,
    Lost
}

/// <summary>
/// All of a Surge's arithmetic, and none of its terminal: the caller says how much time
/// went by and which arrow was pressed. Keeping the clock outside is what makes the
/// minigame testable, and it is also where the mechanic's whole intent is enforced.
/// Rage at any moment is
/// <c>starting - drain * elapsed + restore * correct - penalty * wrong</c>,
/// so for the same keys in the same order, pressing them sooner is always worth strictly
/// more rage. That is the "fast and sloppy beats slow and careful" property: a mistake
/// costs a fixed amount once, while hesitation costs for as long as it lasts.
/// </summary>
public sealed class SurgeState
{
    readonly double drainRate;
    readonly double restore;
    readonly double penalty;
    readonly List<SurgeDirection> glyphs;

    public SurgeState(SurgeChallenge challenge, IEnumerable<SurgeDirection> row)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        glyphs = row is null ? [] : [.. row];

        drainRate = Math.Max(0, challenge.DrainRate);
        restore = Math.Max(0, challenge.Restore);
        penalty = Math.Max(0, challenge.Penalty);
        MaxRage = Math.Max(1, challenge.MaxRage);
        Rage = Math.Clamp(challenge.StartingRage, 0, MaxRage);

        // An empty row has nothing to break, and a bar that starts empty is already spent.
        // Settling both here keeps every other method free of special cases.
        if (glyphs.Count == 0)
            Outcome = SurgeOutcome.Won;
        else if (Rage <= 0)
            Outcome = SurgeOutcome.Lost;
    }

    /// <summary>The row, left to right.</summary>
    public IReadOnlyList<SurgeDirection> Glyphs => glyphs;

    /// <summary>How many glyphs the player has already taken. Also the index of the current one.</summary>
    public int Position { get; private set; }

    public int Length => glyphs.Count;

    public double Rage { get; private set; }

    public double MaxRage { get; }

    /// <summary>How full the bar is, 0 to 1. What the renderer draws.</summary>
    public double RageFraction => Math.Clamp(Rage / MaxRage, 0, 1);

    public SurgeOutcome Outcome { get; private set; } = SurgeOutcome.InProgress;

    public bool IsOver => Outcome != SurgeOutcome.InProgress;

    /// <summary>
    /// Time passing, in seconds. Rage that reaches zero has emptied and the Surge is lost;
    /// an outcome, once reached, is final, so a won row cannot be drained into a loss.
    /// </summary>
    public void Drain(double elapsedSeconds)
    {
        if (IsOver || elapsedSeconds <= 0)
            return;

        Spend(drainRate * elapsedSeconds);
    }

    /// <summary>
    /// One arrow. The right one advances the caret and restores rage; a wrong one costs
    /// rage and leaves the caret exactly where it was, so mashing never walks the row -
    /// and never sends the player back to the start either.
    /// Returns whether the input was the one the row wanted.
    /// </summary>
    public bool ApplyInput(SurgeDirection input)
    {
        if (IsOver)
            return false;

        if (input != glyphs[Position])
        {
            Spend(penalty);
            return false;
        }

        Position++;
        Rage = Math.Min(MaxRage, Rage + restore);

        if (Position >= glyphs.Count)
            Outcome = SurgeOutcome.Won;

        return true;
    }

    void Spend(double amount)
    {
        Rage -= amount;

        if (Rage > 0)
            return;

        Rage = 0;
        Outcome = SurgeOutcome.Lost;
    }
}
