using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using KrissJourney.Kriss.Helpers;
using KrissJourney.Kriss.Models;

namespace KrissJourney.Kriss.Nodes;

/// <summary>
/// Kriss' telekinesis: a row of direction glyphs the player walks with the arrow keys
/// while the rage bar drains in real time.
/// Deliberately the mirror image of <see cref="FightNode"/>'s QTE. The fight rewards
/// patience - wait for the oscillating cursor, strike on the beat. The Surge rewards
/// momentum: only a correct arrow puts rage back, so hesitating is fatal and a couple of
/// mistakes are cheaper than slowing down. Fast and sloppy wins where slow and careful
/// dies.
/// The arithmetic all lives in <see cref="SurgeState"/>, which knows nothing about the
/// terminal. What is left here is the loop that draws it and reads keys.
/// </summary>
public class SurgeNode : NodeBase
{
    /// <summary>
    /// The first Surge in play order is Ayonn's shield in chapter XI, and the tutorial
    /// fires there - the same precedent as the fight tutorial keyed to c10 node 701 in
    /// <see cref="FightNode"/>.
    /// c11 currently narrates the breach across nodes 8, 9, 16 and 17, and issue 6
    /// restructures those around the Surge. If the first attempt does not end up on node 8,
    /// this constant has to move with it or the tutorial silently never shows.
    /// </summary>
    public const int TutorialChapterId = 11;
    public const int TutorialNodeId = 8;

    const string DefaultSuccessMessage = "The rage goes out of you all at once, and something gives.";
    const string DefaultFailureMessage = "The rage gutters out before it can break anything.";

    const char RageBlock = '\u2588'; // the same block the FightNode cursor is drawn with
    const int RageBarWidth = 20;
    const int RageBarTotalWidth = RageBarWidth + 8; // "RAGE  [" ... "]"
    const int FrameDelayMs = 10;                    // same tick as the FightNode QTE
    const int DisplayLines = 4;                     // glyph row, caret, blank, rage bar
    const int OutcomeHoldMs = 350;                  // let the finished row be seen before it goes

    readonly Random random = new();

    int rowIndent;
    int glyphGap;
    int caretRowWidth;
    int barIndent;

    public SurgeChallenge Challenge { get; set; }

    public override void Load()
    {
        Challenge ??= new SurgeChallenge();

        Init();
        WriteLine();
        WriteLine();

        if (IsThisNode(TutorialChapterId, TutorialNodeId)) // To display the tutorial the first time the player surges
            ShowTutorial();

        // A Surge is never a Game Over and is never skipped for having been visited: the
        // scene it belongs to routes both outcomes itself, and at Ayonn's shield the node
        // is entered more than once on purpose.
        while (true)
        {
            Typist.InstantText("Let it out!", ConsoleColor.Red);
            Typist.WaitForKey(2);
            RedrawNode();

            if (PlayRow() is SurgeOutcome.Won)
            {
                Typist.RenderText(isFlowing: true, Challenge.SuccessMessage ?? DefaultSuccessMessage, ConsoleColor.Red);
                Typist.WaitForKey(3);

                AdvanceToNext(ChildId);
                return;
            }

            Typist.RenderText(isFlowing: true, Challenge.FailureMessage ?? DefaultFailureMessage, ConsoleColor.DarkRed);
            Typist.WaitForKey(3);

            // Either the story has somewhere to put a failure - at Ayonn's shield every
            // failed push costs a companion - or the player simply goes again on the spot.
            if (Challenge.FailureChildId.HasValue)
            {
                AdvanceToNext(Challenge.FailureChildId.Value);
                return;
            }

            RedrawNode();
        }
    }

    static void ShowTutorial()
    {
        WriteLine();
        WriteLine();
        Typist.InstantText(
            "Follow the sequence with the arrow keys. Speed matters more than precision; hesitation is what kills you.",
            ConsoleColor.DarkYellow);
        Thread.Sleep(1000);
        WriteLine();
        WriteLine();
    }

    /// <summary>
    /// Runs one attempt: draws the row and the bar, feeds real elapsed time and arrow keys
    /// into the state, and returns as soon as the row is finished or the rage is spent.
    /// </summary>
    SurgeOutcome PlayRow()
    {
        SurgeState state = new(Challenge, BuildRow());
        SetLayout(state.Length);

        // Reserve the display area before anchoring to it, so that any scrolling the
        // reservation causes happens now rather than under the frames.
        for (int i = 0; i < DisplayLines; i++)
            WriteLine();

        int top = CursorTop - DisplayLines;

        CursorVisible = false;

        // Whatever was hammered while reading the node must not count as input.
        while (KeyAvailable)
            ReadKey(intercept: true);

        Stopwatch clock = Stopwatch.StartNew();
        double previousElapsed = 0;
        string lastFrame = null;

        while (true)
        {
            double elapsed = clock.Elapsed.TotalSeconds;
            state.Drain(elapsed - previousElapsed);
            previousElapsed = elapsed;

            while (!state.IsOver && KeyAvailable)
                if (SurgeDirectionExtensions.TryFromKey(ReadKey(intercept: true).Key, out SurgeDirection direction))
                    state.ApplyInput(direction);

            lastFrame = DrawFrame(top, state, lastFrame);

            if (state.IsOver)
                break;

            Thread.Sleep(FrameDelayMs);
        }

        Thread.Sleep(OutcomeHoldMs);

        // Whatever was still being hammered as the row ended must not go on to skip the
        // message that reports how it ended.
        while (KeyAvailable)
            ReadKey(intercept: true);

        ClearDisplay(top);
        CursorVisible = true;

        return state.Outcome;
    }

    List<SurgeDirection> BuildRow()
    {
        List<SurgeDirection> authored = SurgeDirectionExtensions.Parse(Challenge.Sequence);

        return authored.Count > 0 ? authored : SurgeDirectionExtensions.Generate(Challenge.SequenceLength, random);
    }

    /// <summary>
    /// Works out where the row and the bar sit. The gap between glyphs shrinks rather than
    /// letting a long row wrap, and nothing is ever written in the last column, because a
    /// wrap would scroll the frames out from under their anchor.
    /// </summary>
    void SetLayout(int glyphCount)
    {
        int usableWidth = Math.Max(1, WindowWidth - 1);

        glyphGap = 2;
        while (glyphGap > 0 && RowWidth(glyphCount, glyphGap) > usableWidth)
            glyphGap--;

        int rowWidth = Math.Min(usableWidth, RowWidth(glyphCount, glyphGap));

        rowIndent = Math.Max(0, (usableWidth - rowWidth) / 2);
        caretRowWidth = Math.Min(usableWidth, rowIndent + rowWidth);
        barIndent = Math.Max(0, (usableWidth - RageBarTotalWidth) / 2);
    }

    static int RowWidth(int glyphCount, int gap) => glyphCount + (Math.Max(0, glyphCount - 1) * gap);

    /// <summary>
    /// Redraws the three lines, but only when something visible has actually changed:
    /// at a 10ms tick a real terminal would otherwise flicker, and the test terminal would
    /// collect thousands of identical frames.
    /// Returns the signature of what is now on screen.
    /// </summary>
    string DrawFrame(int top, SurgeState state, string lastFrame)
    {
        int filled = FilledSegments(state);
        string frame = $"{state.Position}|{filled}|{state.Outcome}";

        if (frame == lastFrame)
            return frame;

        SetCursorPosition(0, top);
        WriteGlyphRow(state);

        SetCursorPosition(0, top + 1);
        Write(CaretRow(state), CaretColor(state));

        SetCursorPosition(0, top + 3);
        WriteRageBar(filled);

        return frame;
    }

    void WriteGlyphRow(SurgeState state)
    {
        Write(new string(' ', rowIndent));

        for (int i = 0; i < state.Length; i++)
        {
            if (i > 0)
                Write(new string(' ', glyphGap));

            Write(state.Glyphs[i].Glyph().ToString(), GlyphColor(state, i));
        }
    }

    /// <summary>
    /// Spent glyphs go grey, the one owed right now is white, and the rest are the
    /// narrator's dark cyan - except in the mind-merge, where Saberinne's share of the row
    /// is hers in her own green from first sight to the moment she takes it. It never turns
    /// white, because white means "press this" and pressing hers does nothing.
    /// </summary>
    static ConsoleColor GlyphColor(SurgeState state, int index)
    {
        if (index < state.Position)
            return ConsoleColor.DarkGray;

        if (state.IsSaberinnes(index))
            return EnCharacter.Saberinne.Color();

        return index == state.Position ? ConsoleColor.White : ConsoleColor.DarkCyan;
    }

    /// <summary>
    /// The caret carries whoever is playing, so a glance at it says whether to press.
    /// </summary>
    static ConsoleColor CaretColor(SurgeState state) =>
        state.IsSaberinnes(state.Position) ? EnCharacter.Saberinne.Color() : ConsoleColor.White;

    string CaretRow(SurgeState state)
    {
        if (state.Position >= state.Length)
            return new string(' ', caretRowWidth);

        int column = rowIndent + (state.Position * (1 + glyphGap));

        return (new string(' ', column) + '^').PadRight(caretRowWidth);
    }

    void WriteRageBar(int filled)
    {
        Write(new string(' ', barIndent));
        Write("RAGE  ", ConsoleColor.Red);
        Write("[", ConsoleColor.DarkGray);

        if (filled > 0)
            Write(new string(RageBlock, filled), ConsoleColor.Red);

        if (filled < RageBarWidth)
            Write(new string('-', RageBarWidth - filled), ConsoleColor.DarkGray);

        Write("]", ConsoleColor.DarkGray);
    }

    /// <summary>
    /// Any rage left at all keeps one block lit, so an empty bar always means a lost Surge
    /// and never a rounding artefact.
    /// </summary>
    static int FilledSegments(SurgeState state)
    {
        if (state.Rage <= 0)
            return 0;

        int filled = (int)Math.Round(state.RageFraction * RageBarWidth, MidpointRounding.AwayFromZero);

        return Math.Clamp(filled, 1, RageBarWidth);
    }

    static void ClearDisplay(int top)
    {
        string blank = new(' ', Math.Max(1, WindowWidth - 1));

        for (int i = 0; i < DisplayLines; i++)
        {
            SetCursorPosition(0, top + i);
            Write(blank);
        }

        SetCursorPosition(0, top);
    }
}
