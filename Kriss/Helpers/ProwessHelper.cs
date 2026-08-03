using System;
using System.Collections.Generic;
using System.Linq;
using KrissJourney.Kriss.Models;

namespace KrissJourney.Kriss.Helpers;

// I am not actually giving you XP ≽^•⩊•^≼
public static class ProwessHelper
{
    // Every chapter containing a Fight node needs an entry here.
    static readonly Dictionary<int, Prowess> ProwessByChapter = new()
    {
        // c10 - oxengutters and croeggs: Kriss swings a dead chief's sword and hopes.
        [10] = new Prowess()
        {
            MaxHealth = 30,
            BaseDamage = 10,
            RageBonus = 1,
            FuryBonus = 5,
            AttacksPerRound = 1
        },
        // c13 - Ayonn's mind-controlled guards: he has learned to fight back twice a round.
        [13] = new Prowess()
        {
            MaxHealth = 40,
            BaseDamage = 15,
            RageBonus = 2,
            FuryBonus = 10,
            AttacksPerRound = 2
        },
        // c17 - the Edzzen bar brawl: tougher and hitting harder, but still two swings,
        // because the mob's three attacks per round make this a fight about not being hit.
        [17] = new Prowess()
        {
            MaxHealth = 50,
            BaseDamage = 20,
            RageBonus = 3,
            FuryBonus = 15,
            AttacksPerRound = 2
        },
        // c19 - the sea mutants: the underwater rage that tore a door out of its frame
        // shows up as a third attack per round.
        [19] = new Prowess()
        {
            MaxHealth = 60,
            BaseDamage = 25,
            RageBonus = 4,
            FuryBonus = 20,
            AttacksPerRound = 3
        },
        // c22 - the duel with Saberinne: Kriss at his peak. She is a wall because of her
        // own stats in c22.json, not because he is weakened here.
        [22] = new Prowess()
        {
            MaxHealth = 70,
            BaseDamage = 30,
            RageBonus = 5,
            FuryBonus = 25,
            AttacksPerRound = 3
        }
    };

    /// <summary>
    /// Kriss' stats for the given chapter. A chapter that gained a Fight node without
    /// gaining an entry above falls back to the nearest calibrated chapter, keeping the
    /// game playable instead of crashing mid-combat. Never throws; use IsDefined to
    /// detect the missing calibration.
    /// </summary>
    public static Prowess GetProwess(int chapterId)
    {
        if (ProwessByChapter.TryGetValue(chapterId, out Prowess prowess))
            return prowess;

        // Nearest by chapter distance. On a tie the earlier chapter wins, so an
        // uncalibrated chapter inherits difficulty already proven in play rather than
        // difficulty tuned for later, stronger enemies.
        int nearest = ProwessByChapter.Keys
            .OrderBy(id => Math.Abs(id - chapterId))
            .ThenBy(id => id)
            .First();

        return ProwessByChapter[nearest];
    }

    /// <summary>
    /// Whether the chapter has an explicitly calibrated Prowess, as opposed to resolving
    /// through the fallback.
    /// </summary>
    public static bool IsDefined(int chapterId) => ProwessByChapter.ContainsKey(chapterId);

    /// <summary>
    /// Gets the explicitly calibrated Prowess for the chapter, if there is one.
    /// </summary>
    public static bool TryGetProwess(int chapterId, out Prowess prowess) =>
        ProwessByChapter.TryGetValue(chapterId, out prowess);
}
