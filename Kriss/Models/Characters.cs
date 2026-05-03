using System;

namespace KrissJourney.Kriss.Models;

public enum EnCharacter
{
    Narrator,
    Kriss,
    Corolla,
    Smiurl,
    Theo,
    Efeliah,
    Math,
    Elder,
    Jeorghe,
    Chief,
    Person,
    Saberinne,
    Riff
}

public static class CharacterExtensions
{
    public static ConsoleColor Color(this EnCharacter character)
    {
        return character switch
        {
            EnCharacter.Narrator => ConsoleColor.DarkCyan,
            EnCharacter.Kriss => ConsoleColor.Cyan,
            EnCharacter.Corolla => ConsoleColor.Red,
            EnCharacter.Smiurl => ConsoleColor.Yellow,
            EnCharacter.Theo => ConsoleColor.Blue,
            EnCharacter.Efeliah => ConsoleColor.DarkGreen,
            EnCharacter.Math => ConsoleColor.DarkMagenta,
            EnCharacter.Elder => ConsoleColor.Magenta,
            EnCharacter.Jeorghe => ConsoleColor.DarkMagenta,
            EnCharacter.Chief => ConsoleColor.Magenta,
            EnCharacter.Person => ConsoleColor.DarkYellow,
            EnCharacter.Saberinne => ConsoleColor.Green,
            EnCharacter.Riff => ConsoleColor.DarkYellow,
            _ => ConsoleColor.DarkCyan, // default color
        };
    }
}