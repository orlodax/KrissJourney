using System;
using System.Collections.Generic;
using System.Text;
using KrissJourney.Kriss.Services.Terminal;

namespace KrissJourney.Tests.Infrastructure.Mocks;

public class TerminalMock : ITerminal
{
    private readonly StringBuilder outputBuilder = new();
    private readonly Queue<ConsoleKeyInfo> keyQueue = new();
    private readonly object keyQueueLock = new();
    private readonly object outputLock = new();

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
    public bool KeyAvailable
    {
        get
        {
            lock (keyQueueLock)
                return keyQueue.Count > 0;
        }
    }

    // For testing
    public string GetOutput()
    {
        lock (outputLock)
            return outputBuilder.ToString();
    }
    public int KeyQueueCount
    {
        get
        {
            lock (keyQueueLock)
                return keyQueue.Count;
        }
    }
    public bool CursorVisible
    {
        get => true;
        set { /* do nothing */ }
    }

    public void Clear()
    {
        lock (outputLock)
            outputBuilder.AppendLine("[CONSOLE CLEARED]");
        CursorLeft = 0;
        CursorTop = 0;
    }

    public void ResetOutput()
    {
        lock (outputLock)
            outputBuilder.Clear();
        lock (keyQueueLock)
            keyQueue.Clear();
        CursorLeft = 0;
        CursorTop = 0;
    }

    public void WriteLine(string message = null)
    {
        lock (outputLock)
            outputBuilder.AppendLine(message ?? string.Empty);
        CursorLeft = 0;
        CursorTop++;
    }

    public void Write(string message)
    {
        lock (outputLock)
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
        lock (outputLock)
            outputBuilder.AppendLine($"[{color}]{message ?? string.Empty}[/{color}]");
        CursorLeft = 0;
        CursorTop++;
    }

    public void Write(string message, ConsoleColor color)
    {
        lock (outputLock)
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
        // Block briefly to allow tests to enqueue keys after prompts appear
        int waited = 0;
        while (waited < 1000)
        {
            lock (keyQueueLock)
            {
                if (keyQueue.Count > 0)
                    return keyQueue.Dequeue();
            }
            System.Threading.Thread.Sleep(5);
            waited += 5;
        }

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
            lock (keyQueueLock)
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

            lock (keyQueueLock)
                keyQueue.Enqueue(new ConsoleKeyInfo(c, key, false, false, false));
        }
    }
}
