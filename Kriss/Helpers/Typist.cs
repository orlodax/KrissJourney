using System;
using System.Collections.Generic;
using System.Threading;
using KrissJourney.Kriss.Models;

namespace KrissJourney.Kriss.Helpers;

public static class Typist
{
    static readonly int FlowDelay = 10; // fine-tunes the speed of TextFlow
    static readonly int ParagraphBreak = 1000; // "#" arbitrary pause
    static readonly int ShortPause = 700; // comma pause
    static readonly int LongPause = 1200; // dot pause
    static readonly List<string> ToSkipPause = [".", "!", "?", "\"", ">", ")", "]", "}", ":"]; // symbols after short pause that must not trigger another pause
    static readonly List<string> ToShortPause = [":", ";", ",", "!", "?"]; // symbols after which trigger a short pause

    /// <summary>
    /// Main method to render different kinds of text
    /// </summary>
    /// <param name="isFlowing"></param>
    /// <param name="text"></param>
    /// <param name="color"></param>
    public static void RenderText(bool isFlowing, string text, ConsoleColor color = ConsoleColor.DarkCyan)
    {
        ForegroundColor = color;

        if (text != null)
        {
            int flow = FlowDelay;
            int paragraph = ParagraphBreak;
            int shortPause = ShortPause;
            int longPause = LongPause;

            if (CommandLineOptions.IsDebug)
                isFlowing = false;

            if (!isFlowing)
                flow = paragraph = shortPause = longPause = 0;

            string prevChar = string.Empty;

            for (int i = 0; i < text.Length; i++)
            {
                string c = text[i].ToString(); ;

                if (prevChar.Equals("."))
                {
                    if (c.Equals(" ") || c.Equals("\n"))
                        Thread.Sleep(longPause);
                }
                else
                {
                    if (ToShortPause.Contains(prevChar) && !ToSkipPause.Contains(c))
                        Thread.Sleep(shortPause);
                }

                if (prevChar.Equals("$"))
                    color = c switch
                    {
                        "R" => EnCharacter.Corolla.Color(),
                        "r" => ConsoleColor.DarkRed,
                        "G" => EnCharacter.Saberinne.Color(),
                        "g" => EnCharacter.Efeliah.Color(),
                        "B" => EnCharacter.Theo.Color(),
                        "C" => EnCharacter.Narrator.Color(),
                        "c" => EnCharacter.Kriss.Color(),
                        "M" => ConsoleColor.Magenta,
                        "m" => EnCharacter.Math.Color(),
                        "Y" => EnCharacter.Smiurl.Color(),
                        "y" => ConsoleColor.DarkYellow,
                        "K" => ConsoleColor.Black,
                        "W" => ConsoleColor.White,
                        "D" => ConsoleColor.DarkGray,
                        "d" => ConsoleColor.Gray,
                        _ => color,
                    };
                else
                {
                    if (!c.Equals("#") && !c.Equals("$"))
                    {
                        ForegroundColor = color;
                        Write(c);
                        Thread.Sleep(flow);
                    }
                    else if (c.Equals("#"))
                        Thread.Sleep(paragraph);
                }
                prevChar = c;
            }
        }
    }

    public static void FlowingText(string text, ConsoleColor color = ConsoleColor.DarkCyan)
    {
        RenderText(true, text, color);
    }

    public static void InstantText(string text, ConsoleColor color = ConsoleColor.DarkCyan)
    {
        RenderText(false, text, color);
    }

    /// <summary>
    /// To display characters speech parts
    /// </summary>
    public static void RenderLine(bool isFlowing, DialogueLine dialogue)
    {
        if (dialogue.IsTelepathy)
            RenderText(isFlowing, "<<" + dialogue.Line + ">> ", dialogue.Actor.Color());
        else
            RenderText(isFlowing, "\"" + dialogue.Line + "\" ", dialogue.Actor.Color());

        if (!CommandLineOptions.IsDebug && isFlowing)
            Thread.Sleep(ParagraphBreak);
    }

    public static void RenderNonSpeechPart(bool isFlowing, string part)
    {
        RenderText(isFlowing, part);

        if (!CommandLineOptions.IsDebug && isFlowing)
            Thread.Sleep(ParagraphBreak);
    }

    public static void RenderPrompt(List<ConsoleKeyInfo> keysPressed)
    {
        ForegroundColor = ConsoleColor.Gray;
        Write("\\>");
        CursorLeft += 1;

        //if redrawing after backspacing, rewrite stack
        if (keysPressed.Count != 0)
            for (int i = 0; i < keysPressed.Count; i++)
                Write(keysPressed[i].KeyChar.ToString());
    }

    public static void WaitForKey(int numberOfNewLines)
    {
        for (int i = 0; i < numberOfNewLines; i++)
            WriteLine();

        // Use light gray instead of dark gray on terminals with poor contrast
        ForegroundColor = ConsoleColor.DarkGray;

        Write("Press a key to continue...");
        ReadKey(true);
    }
}
