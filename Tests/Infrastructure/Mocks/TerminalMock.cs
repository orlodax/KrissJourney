using System;
using System.Collections.Generic;
using System.Text;
using KrissJourney.Kriss.Services.Terminal;

namespace KrissJourney.Tests.Infrastructure.Mocks;

public class TerminalMock : ITerminal
{
    private readonly StringBuilder outputBuilder = new();
    private readonly Queue<ConsoleKeyInfo> keyQueue = new();

    // Window properties
    public int WindowWidth { get; set; } = 80;
    public int WindowHeight { get; set; } = 25;
    public int CursorLeft { get; set; } = 0;
    public int CursorTop { get; set; } = 0;
    public int WindowLeft { get; set; } = 0;
    public int WindowTop { get; set; } = 0;

    // Color properties
    public ConsoleColor ForegroundColor { get; set; } = ConsoleColor.Gray;
    public ConsoleColor BackgroundColor { get; set; } = ConsoleColor.Black;

    // Input simulation
    public bool KeyAvailable => keyQueue.Count > 0;

    // For testing
    public string GetOutput() => outputBuilder.ToString();
    public int KeyQueueCount => keyQueue.Count;
    public bool CursorVisible
    {
        get => true;
        set { /* do nothing */ }
    }

    public void Clear()
    {
        outputBuilder.AppendLine("[CONSOLE CLEARED]");
        CursorLeft = 0;
        CursorTop = 0;
    }

    public void ResetOutput()
    {
        outputBuilder.Clear();
        keyQueue.Clear();
        CursorLeft = 0;
        CursorTop = 0;
    }

    public void WriteLine(string message = null)
    {
        outputBuilder.AppendLine(message ?? string.Empty);
        CursorLeft = 0;
        CursorTop++;
    }

    public void Write(string message)
    {
        outputBuilder.Append(message);
        // Update cursor position based on content
        if (message != null)
        {
            string[] lines = message.Split('\n');
            if (lines.Length > 1)
            {
                CursorTop += lines.Length - 1;
                CursorLeft = lines[^1].Length;
            }
            else
            {
                CursorLeft += message.Length;
            }
        }
    }

    public void WriteLine(string message, ConsoleColor color)
    {
        outputBuilder.AppendLine($"[{color}]{message ?? string.Empty}[/{color}]");
        CursorLeft = 0;
        CursorTop++;
    }

    public void Write(string message, ConsoleColor color)
    {
        outputBuilder.Append($"[{color}]{message}[/{color}]");
        // Update cursor
        if (message != null)
        {
            string[] lines = message.Split('\n');
            if (lines.Length > 1)
            {
                CursorTop += lines.Length - 1;
                CursorLeft = lines[^1].Length;
            }
            else
            {
                CursorLeft += message.Length;
            }
        }
    }

    public void SetCursorPosition(int left, int top)
    {
        CursorLeft = left;
        CursorTop = top;
    }

    public string ReadLine()
    {
        return string.Empty; // Default implementation for tests
    }

    public ConsoleKeyInfo ReadKey(bool intercept = false)
    {
        if (keyQueue.Count > 0)
            return keyQueue.Dequeue();

        throw new InvalidOperationException("No keys available in mock terminal");
    }

    public void ResetColor()
    {
        ForegroundColor = ConsoleColor.Gray;
        BackgroundColor = ConsoleColor.Black;
    }

    public void EnqueueKeys(params ConsoleKey[] keys)
    {
        foreach (ConsoleKey key in keys)
        {
            char keyChar = key switch
            {
                ConsoleKey.Spacebar => ' ',
                _ => (char)key // Basic mapping for letters
            };
            keyQueue.Enqueue(new ConsoleKeyInfo(char.ToLower(keyChar), key, false, false, false));
        }
    }

    public void EnqueueText(string text)
    {
        foreach (char c in text)
        {
            ConsoleKey key;
            if (c == ' ')
                key = ConsoleKey.Spacebar;
            else if (char.IsLetter(c))
                key = (ConsoleKey)char.ToUpper(c);
            else
                continue; // Skip characters we can't easily map

            keyQueue.Enqueue(new ConsoleKeyInfo(c, key, false, false, false));
        }
    }
}
