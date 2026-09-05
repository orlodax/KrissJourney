using System.Collections.Generic;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure.Mocks;

namespace KrissJourney.Tests.Infrastructure.Helpers;

/// <summary>
/// Extensions and utilities for testing with GameEngine
/// </summary>
public static class GameEngineTestExtensions
{
    /// <summary>
    /// Get the chapters from a GameEngine instance using reflection
    /// </summary>
    public static List<Chapter> GetChapters(this GameEngine gameEngine)
    {
        System.Reflection.FieldInfo chaptersField = typeof(GameEngine).GetField("chapters",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        return chaptersField?.GetValue(gameEngine) as List<Chapter>;
    }

    /// <summary>
    /// Setup a GameEngine instance backed by the in-memory TestStatusManager.
    /// Never the production StatusManager: that one loads (and saves to) the author's own
    /// status.json under LocalApplicationData, so tests would read - and eventually rewrite -
    /// a real playthrough. See SaveFileIsolationTests, which guards this.
    /// </summary>
    public static GameEngine Setup()
    {
        GameEngine gameEngine = new(new TestStatusManager());
        gameEngine.Run();
        return gameEngine;
    }
}
