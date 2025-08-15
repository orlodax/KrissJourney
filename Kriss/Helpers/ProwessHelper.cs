using System;
using KrissJourney.Kriss.Models;

namespace KrissJourney.Kriss.Helpers;

// I am not actually giving you XP ≽^•⩊•^≼
public static class ProwessHelper
{
    public static Prowess GetProwess(int chapterId)
    {
        return chapterId switch
        {
            10 => new Prowess()
            {
                MaxHealth = 30,
                BaseDamage = 10,
                RageBonus = 1,
                FuryBonus = 5,
                QteSpeedFactor = 1f,
                QteWidth = 2
            },
            _ => throw new NotImplementedException($"Prowess for chapter {chapterId} is not implemented.")
        };
    }
}
