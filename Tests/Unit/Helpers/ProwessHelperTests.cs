using System.Linq;
using KrissJourney.Kriss.Helpers;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Unit.Helpers;

[TestClass]
public class ProwessHelperTests
{
    /// <summary>
    /// Regression test for issue 3. This must not merely call GetProwess (which never throws
    /// thanks to the graceful fallback) - it must assert IsDefined, so that a Fight node added
    /// to a chapter without an explicit ProwessHelper entry fails this test instead of silently
    /// degrading in production. Deliberately chapter-id agnostic: it walks whatever chapters and
    /// Fight nodes exist, so it keeps working across the issue 5 renumbering.
    /// </summary>
    [TestMethod]
    public void EveryChapterWithFightNode_HasExplicitProwessEntry()
    {
        GameEngine gameEngine = GameEngineTestExtensions.Setup();

        foreach (Chapter chapter in gameEngine.GetChapters())
        {
            bool hasFightNode = chapter.Nodes.Any(n => n is FightNode);

            if (hasFightNode)
                Assert.IsTrue(ProwessHelper.IsDefined(chapter.Id),
                    $"Chapter {chapter.Id} contains a Fight node but has no explicit Prowess entry in ProwessHelper.");
        }
    }

    [TestMethod]
    public void GetProwess_Chapter10_MatchesAuthorCalibration()
    {
        Prowess prowess = ProwessHelper.GetProwess(10);

        Assert.AreEqual(30, prowess.MaxHealth);
        Assert.AreEqual(10, prowess.BaseDamage);
        Assert.AreEqual(1, prowess.RageBonus);
        Assert.AreEqual(5, prowess.FuryBonus);
        Assert.AreEqual(1, prowess.AttacksPerRound);
    }

    [TestMethod]
    public void GetProwess_Chapter13_MatchesAuthorCalibration()
    {
        Prowess prowess = ProwessHelper.GetProwess(13);

        Assert.AreEqual(40, prowess.MaxHealth);
        Assert.AreEqual(15, prowess.BaseDamage);
        Assert.AreEqual(2, prowess.RageBonus);
        Assert.AreEqual(10, prowess.FuryBonus);
        Assert.AreEqual(2, prowess.AttacksPerRound);
    }

    [TestMethod]
    public void GetProwess_UndefinedChapter_FallsBackWithoutThrowing()
    {
        Prowess prowess = ProwessHelper.GetProwess(99);

        // Should degrade gracefully instead of crashing, and should not be a blank/default struct.
        Assert.IsFalse(ProwessHelper.IsDefined(99));
        Assert.AreNotEqual(default, prowess);
    }

    [TestMethod]
    public void GetProwess_UndefinedChapter_FallsBackToNearestCalibratedChapter()
    {
        // 11 and 12 sit between the calibrated 10 and 13, and each must resolve to
        // whichever is closer - not to the highest entry in the table.
        Assert.AreEqual(ProwessHelper.GetProwess(10), ProwessHelper.GetProwess(11));
        Assert.AreEqual(ProwessHelper.GetProwess(13), ProwessHelper.GetProwess(12));

        // Below and above the calibrated range, the nearest is the first and last entry.
        Assert.AreEqual(ProwessHelper.GetProwess(10), ProwessHelper.GetProwess(1));
        Assert.AreEqual(ProwessHelper.GetProwess(20), ProwessHelper.GetProwess(99));
    }

    [TestMethod]
    public void GetProwess_EquidistantFromTwoChapters_PrefersTheEarlier()
    {
        // 17 is one away from both 16 and 18. The earlier wins, so an uncalibrated
        // chapter inherits difficulty already proven in play rather than difficulty
        // tuned for later, stronger enemies.
        Assert.AreEqual(ProwessHelper.GetProwess(16), ProwessHelper.GetProwess(17));
    }

    [TestMethod]
    public void IsDefined_UndefinedChapter_ReturnsFalse()
    {
        Assert.IsFalse(ProwessHelper.IsDefined(99));
    }

    [TestMethod]
    public void TryGetProwess_UndefinedChapter_ReturnsFalse()
    {
        bool found = ProwessHelper.TryGetProwess(99, out Prowess prowess);

        Assert.IsFalse(found);
        Assert.AreEqual(default, prowess);
    }

    [TestMethod]
    public void TryGetProwess_DefinedChapter_ReturnsTrueWithValue()
    {
        bool found = ProwessHelper.TryGetProwess(10, out Prowess prowess);

        Assert.IsTrue(found);
        Assert.AreEqual(30, prowess.MaxHealth);
    }
}
