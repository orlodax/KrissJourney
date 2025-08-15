using System;
using KrissJourney.Kriss.Services.Terminal;

namespace KrissJourney.Kriss.Services;

/// <summary>
/// Facade for the terminal service.
/// This class is used to abstract the terminal operations and allows for easy testing.
/// </summary>
public static class TerminalFacade
{
    private static ITerminal terminal = new Terminal.Terminal();

    public static void SetTestTerminal(ITerminal testTerminal)
    {
        terminal = testTerminal;
    }

    public static ITerminal GetCurrentTerminal()
    {
        return terminal;
    }

    public static int WindowWidth => terminal.WindowWidth;
    public static int WindowHeight => terminal.WindowHeight;
    public static int CursorLeft
    {
        get => terminal.CursorLeft;
        set => terminal.CursorLeft = value;
    }
    public static int CursorTop
    {
        get => terminal.CursorTop;
        set => terminal.CursorTop = value;
    }
    public static int WindowLeft => terminal.WindowLeft;
    public static int WindowTop => terminal.WindowTop;
    public static ConsoleColor ForegroundColor
    {
        get => terminal.ForegroundColor;
        set => terminal.ForegroundColor = value;
    }
    public static ConsoleColor BackgroundColor
    {
        get => terminal.BackgroundColor;
        set => terminal.BackgroundColor = value;
    }
    public static string ReadLine() => terminal.ReadLine();
    public static void WriteLine(string message = null) => terminal.WriteLine(message);
    public static void Write(string message) => terminal.Write(message);
    public static void WriteLine(string message, ConsoleColor color) => terminal.WriteLine(message, color);
    public static void Write(string message, ConsoleColor color) => terminal.Write(message, color);
    public static void Clear() => terminal.Clear();
    public static void SetCursorPosition(int left, int top) => terminal.SetCursorPosition(left, top);
    public static ConsoleKeyInfo ReadKey(bool intercept = false) => terminal.ReadKey(intercept);
    public static void ResetColor() => terminal.ResetColor();
    public static bool KeyAvailable => terminal.KeyAvailable;
    public static bool CursorVisible
    {
        get => terminal.CursorVisible;
        set => terminal.CursorVisible = value;
    }
}
