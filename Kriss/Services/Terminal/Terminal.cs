using System;

namespace KrissJourney.Kriss.Services.Terminal;

/// <summary>
/// Forwards all calls to the Console class (production code).
/// </summary>
public class Terminal : ITerminal
{
    public int WindowWidth => Console.WindowWidth;
    public int WindowHeight => Console.WindowHeight;
    public int CursorLeft
    {
        get => Console.CursorLeft;
        set => Console.CursorLeft = value;
    }
    public int CursorTop
    {
        get => Console.CursorTop;
        set => Console.CursorTop = value;
    }
    public int WindowLeft => Console.WindowLeft;
    public int WindowTop => Console.WindowTop;
    public ConsoleColor ForegroundColor
    {
        get => Console.ForegroundColor;
        set => Console.ForegroundColor = value;
    }
    public ConsoleColor BackgroundColor
    {
        get => Console.BackgroundColor;
        set => Console.BackgroundColor = value;
    }
    public string ReadLine()
    {
        return Console.ReadLine();
    }
    public void WriteLine(string message = null)
    {
        if (string.IsNullOrEmpty(message))
            Console.WriteLine();
        else
            Console.WriteLine(message);
    }
    public void Write(string message)
    {
        Console.Write(message);
    }
    public void WriteLine(string message, ConsoleColor color)
    {
        ConsoleColor original = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = original;
    }
    public void Write(string message, ConsoleColor color)
    {
        ConsoleColor original = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(message);
        Console.ForegroundColor = original;
    }
    public void Clear()
    {
        Console.Clear();
    }
    public void SetCursorPosition(int left, int top)
    {
        Console.SetCursorPosition(left, top);
    }
    public ConsoleKeyInfo ReadKey(bool intercept = false)
    {
        return Console.ReadKey(intercept);
    }
    public void ResetColor()
    {
        Console.ResetColor();
    }
    public bool KeyAvailable => Console.KeyAvailable;
    public bool CursorVisible
    {
        get => true;
        set
        {
            if (value)
                Console.Write("\x1b[?25h");
            else
                Console.Write("\x1b[?25l");
        }
    }
}
