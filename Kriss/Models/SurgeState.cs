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
/// <c>starting - drain * elapsed + restore * taken - penalty * wrong</c>,
/// so for the same keys in the same order, pressing them sooner is always worth strictly
/// more rage. That is the "fast and sloppy beats slow and careful" property: a mistake
/// costs a fixed amount once, while hesitation costs for as long as it lasts.
/// In a duet part of the row is Saberinne's and she takes it herself - <see cref="IsSaberinnes"/>.
/// </summary>
public sealed class SurgeState
{
    readonly double drainRate;
    readonly double restore;
    readonly double penalty;
    readonly double duetBeat;
    readonly bool bufferInput; // inert without a duet: only her glyphs ever buffer
    readonly bool[] duet; // null unless the challenge names a duet pattern
    readonly List<SurgeDirection> glyphs;

    double duetPending; // seconds Saberinne has spent on the glyph the caret is on
    SurgeDirection? buffered; // a press made on her glyph, waiting for the caret to come back

    public SurgeState(SurgeChallenge challenge, IEnumerable<SurgeDirection> row)
    {
        ArgumentNullException.ThrowIfNull(challenge);

        glyphs = row is null ? [] : [.. row];

        drainRate = Math.Max(0, challenge.DrainRate);
        restore = Math.Max(0, challenge.Restore);
        penalty = Math.Max(0, challenge.Penalty);
        duetBeat = Math.Max(0, challenge.DuetBeat);
        bufferInput = challenge.BufferInput;
        duet = BuildDuet(challenge.DuetPattern, glyphs.Count);
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
    /// Whether the glyph at <paramref name="index"/> is Saberinne's rather than Kriss's.
    /// The one source of truth for the split: the renderer takes its colour from this and
    /// the arithmetic resolves from it, so what shows as hers is exactly what she plays.
    /// </summary>
    public bool IsSaberinnes(int index) => duet is not null && index >= 0 && index < duet.Length && duet[index];

    /// <summary>
    /// Time passing, in seconds: the bar drains, then Saberinne takes whatever of her share
    /// the caret is resting on. Rage that reaches zero has emptied and the Surge is lost;
    /// an outcome, once reached, is final, so a won row cannot be drained into a loss.
    /// </summary>
    public void Drain(double elapsedSeconds)
    {
        if (IsOver || elapsedSeconds <= 0)
            return;

        Spend(drainRate * elapsedSeconds);
        PlaySaberinnesShare(elapsedSeconds);
    }

    /// <summary>
    /// One arrow. The right one advances the caret and restores rage; a wrong one costs
    /// rage and leaves the caret exactly where it was, so mashing never walks the row -
    /// and never sends the player back to the start either. Saberinne's glyphs take no
    /// input at all: pressing ahead into her share is swallowed rather than charged, since
    /// it was never his to get wrong - unless <see cref="SurgeChallenge.BufferInput"/> holds
    /// it for the caret's return instead.
    /// Returns whether the input was the one the row wanted of the player; a buffered press
    /// has not been answered yet, so it returns false and is judged on the way out.
    /// </summary>
    public bool ApplyInput(SurgeDirection input)
    {
        if (IsOver)
            return false;

        if (IsSaberinnes(Position))
        {
            // Latest press wins: a player correcting themselves mid-run meant the last one.
            if (bufferInput)
                buffered = input;

            return false;
        }

        return Resolve(input);
    }

    /// <summary>Judges one arrow against the glyph the caret is actually on.</summary>
    bool Resolve(SurgeDirection input)
    {
        if (input != glyphs[Position])
        {
            Spend(penalty);
            return false;
        }

        Take();

        return true;
    }

    /// <summary>
    /// Saberinne's half plays itself: the caret rests on one of her glyphs for a beat, then
    /// she takes it and puts back the rage a correct input would have. A run of hers goes
    /// one glyph per beat rather than all in one frame, so being carried keeps a tempo. Her
    /// clock only runs while the caret is actually on her glyph.
    /// </summary>
    void PlaySaberinnesShare(double elapsedSeconds)
    {
        if (!IsOver && IsSaberinnes(Position))
        {
            duetPending += elapsedSeconds;

            // The remainder carries over inside a run of hers, so a run takes exactly as many
            // beats as it has glyphs instead of drifting by a frame each time.
            while (duetPending >= duetBeat && !IsOver && IsSaberinnes(Position))
            {
                duetPending -= duetBeat;
                Take();
            }
        }

        if (IsOver || !IsSaberinnes(Position))
        {
            duetPending = 0;
            ConsumeBuffered();
        }
    }

    /// <summary>
    /// Hands a buffered press to the row the moment her share is done with it, so it costs
    /// and pays exactly what pressing at that instant would - buffering is never a free
    /// correct answer. A finished row drops it instead: nothing resolves after the outcome.
    /// </summary>
    void ConsumeBuffered()
    {
        if (buffered is not SurgeDirection input)
            return;

        buffered = null;

        if (!IsOver)
            Resolve(input);
    }

    /// <summary>Consumes the glyph under the caret, whichever of the two it belonged to.</summary>
    void Take()
    {
        Position++;
        Rage = Math.Min(MaxRage, Rage + restore);

        if (Position >= glyphs.Count)
            Outcome = SurgeOutcome.Won;
    }

    /// <summary>
    /// Expands the cycled "S" mask over the actual row once, so ownership is a lookup and
    /// cannot be computed two different ways in two different places.
    /// </summary>
    static bool[] BuildDuet(string pattern, int length)
    {
        if (string.IsNullOrEmpty(pattern) || length == 0)
            return null;

        bool[] mask = new bool[length];

        for (int i = 0; i < length; i++)
            mask[i] = char.ToUpperInvariant(pattern[i % pattern.Length]) == 'S';

        return mask;
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
