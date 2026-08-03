using System;
using System.Collections.Generic;

namespace KrissJourney.Kriss.Models;

/// <summary>
/// The four glyphs a Surge row is built from.
/// </summary>
public enum SurgeDirection
{
    Left,
    Up,
    Right,
    Down
}

public static class SurgeDirectionExtensions
{
    // The row is drawn from U+2190..U+2193, the same Code Page 437 set the FightNode
    // cursor block comes from. Written as escapes rather than literal arrows so that no
    // source encoding accident can quietly turn the game's glyphs into question marks.
    public const char LeftGlyph = '\u2190';
    public const char UpGlyph = '\u2191';
    public const char RightGlyph = '\u2192';
    public const char DownGlyph = '\u2193';

    static readonly SurgeDirection[] All = [SurgeDirection.Left, SurgeDirection.Up, SurgeDirection.Right, SurgeDirection.Down];

    /// <summary>The glyph drawn in the row.</summary>
    public static char Glyph(this SurgeDirection direction) => direction switch
    {
        SurgeDirection.Left => LeftGlyph,
        SurgeDirection.Up => UpGlyph,
        SurgeDirection.Right => RightGlyph,
        _ => DownGlyph,
    };

    /// <summary>The key that plays this glyph. Arrow keys only, as in FightNode and ChoiceNode.</summary>
    public static ConsoleKey Key(this SurgeDirection direction) => direction switch
    {
        SurgeDirection.Left => ConsoleKey.LeftArrow,
        SurgeDirection.Up => ConsoleKey.UpArrow,
        SurgeDirection.Right => ConsoleKey.RightArrow,
        _ => ConsoleKey.DownArrow,
    };

    /// <summary>
    /// Reads a key press as a direction. Anything that is not an arrow key is not input at
    /// all: it costs nothing and does nothing, so leaning on the keyboard while reading
    /// cannot bleed the rage bar.
    /// </summary>
    public static bool TryFromKey(ConsoleKey key, out SurgeDirection direction)
    {
        switch (key)
        {
            case ConsoleKey.LeftArrow:
                direction = SurgeDirection.Left;
                return true;
            case ConsoleKey.UpArrow:
                direction = SurgeDirection.Up;
                return true;
            case ConsoleKey.RightArrow:
                direction = SurgeDirection.Right;
                return true;
            case ConsoleKey.DownArrow:
                direction = SurgeDirection.Down;
                return true;
            default:
                direction = SurgeDirection.Left;
                return false;
        }
    }

    /// <summary>
    /// Parses an authored row. Letters or arrow glyphs, in any case; every other character
    /// is treated as spacing and skipped, so "L U R D" reads the same as "lurd".
    /// </summary>
    public static List<SurgeDirection> Parse(string sequence)
    {
        List<SurgeDirection> row = [];

        if (string.IsNullOrWhiteSpace(sequence))
            return row;

        foreach (char c in sequence)
        {
            switch (char.ToUpperInvariant(c))
            {
                case 'L':
                case LeftGlyph:
                    row.Add(SurgeDirection.Left);
                    break;
                case 'U':
                case UpGlyph:
                    row.Add(SurgeDirection.Up);
                    break;
                case 'R':
                case RightGlyph:
                    row.Add(SurgeDirection.Right);
                    break;
                case 'D':
                case DownGlyph:
                    row.Add(SurgeDirection.Down);
                    break;
            }
        }

        return row;
    }

    /// <summary>
    /// Builds a random row of the given length. The same glyph never appears twice in a
    /// row: holding an arrow key down auto-repeats, and that must never be worth more than
    /// a single press.
    /// </summary>
    public static List<SurgeDirection> Generate(int length, Random random)
    {
        ArgumentNullException.ThrowIfNull(random);

        List<SurgeDirection> row = [];

        for (int i = 0; i < Math.Max(1, length); i++)
        {
            SurgeDirection next;

            do
                next = All[random.Next(All.Length)];
            while (i > 0 && next == row[i - 1]);

            row.Add(next);
        }

        return row;
    }
}
