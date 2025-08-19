using System.Collections.Generic;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Services;

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
    /// Setup a GameEngine instance with the default StatusManager
    /// </summary>
    public static GameEngine Setup()
    {
        GameEngine gameEngine = new(new StatusManager());
        gameEngine.Run();
        return gameEngine;
    }
}
