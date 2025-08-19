using System;

namespace KrissJourney.Kriss.Services.Terminal;

public interface ITerminal
{
    int WindowWidth { get; }
    int WindowHeight { get; }
    int CursorLeft { get; set; }
    int CursorTop { get; set; }
    int WindowLeft { get; }
    int WindowTop { get; }
    ConsoleColor ForegroundColor { get; set; }
    ConsoleColor BackgroundColor { get; set; }
    string ReadLine();
    void WriteLine(string message = null);
    void Write(string message);
    void WriteLine(string message, ConsoleColor color);
    void Write(string message, ConsoleColor color);
    void Clear();
    void SetCursorPosition(int left, int top);
    ConsoleKeyInfo ReadKey(bool intercept = false);
    void ResetColor();
    bool KeyAvailable { get; }
    bool CursorVisible { get; set; }
}
