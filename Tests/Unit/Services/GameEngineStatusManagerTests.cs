using System.Collections.Generic;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure.Helpers;
using KrissJourney.Tests.Infrastructure.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Unit.Services;

[TestClass]
public class GameEngineStatusManagerTests
{
    TestStatusManager statusManager;
    GameEngine gameEngine;

    [TestInitialize]
    public void TestInitialize()
    {
        statusManager = new TestStatusManager();
        gameEngine = new GameEngine(statusManager);
        gameEngine.Run();
    }

    [TestMethod]
    public void Init_LoadsChapters()
    {
        List<Chapter> chapters = gameEngine.GetChapters();
        Assert.IsNotNull(chapters, "Chapters field should not be null");
        Assert.IsTrue(chapters.Count > 0, "Chapters should be loaded");
    }

    [TestMethod]
    public void Evaluate_WithNullCondition_ReturnsTrue()
    {
        Assert.IsTrue(gameEngine.Evaluate(null), "Null condition should evaluate to true");
    }

    [TestMethod]
    public void Evaluate_WithConditionAndNoRequiredItem_ReturnsTrue()
    {
        Condition condition = new()
        {
            Item = null,
            Refusal = "You can't do that"
        };

        Assert.IsTrue(gameEngine.Evaluate(condition), "Condition with no required item should evaluate to true");
    }

    [TestMethod]
    public void Evaluate_WithEmptyItemCondition_ReturnsTrue()
    {
        // Arrange
        Condition condition = new()
        {
            Type = "default",
            Item = "", // Empty string
            Refusal = "You can't do that"
        };

        // Act
        bool result = gameEngine.Evaluate(condition);

        // Assert
        Assert.IsTrue(result, "Empty item string should evaluate to true");
    }

    [TestMethod]
    public void AddItemToInventory_WithGainItem_AddsItemToInventory()
    {
        // Arrange
        Effect effect = new()
        {
            GainItem = "test_sword"
        };

        // Act
        gameEngine.AddItemToInventory(effect);

        // Assert
        Assert.IsTrue(statusManager.IsItemInInventory("test_sword"), "Item should be added to inventory");
    }

    [TestMethod]
    public void StatusManager_AddItemToInventory_And_IsItemInInventory_WorksCorrectly()
    {
        // Arrange
        TestStatusManager testStatusManager = new();
        string item = "test_item";

        // Act
        testStatusManager.AddItemToInventory(item);
        bool hasItem = testStatusManager.IsItemInInventory(item);

        // Assert
        Assert.IsTrue(hasItem, "Item should be in inventory after adding");
    }

    [TestMethod]
    public void SaveProgress_AddsNodeToVisitedNodes_And_Persists()
    {
        // Arrange
        TestStatusManager manager = new();
        int chapterId = 99;
        int nodeId = 123;

        // Act
        manager.SaveProgress(chapterId, nodeId);

        // Assert
        Assert.IsTrue(manager.HasVisitedNodes(), "Should have visited nodes after saving progress");
        Assert.IsTrue(manager.IsNodeVisited(chapterId, nodeId), "Node should be marked as visited");
    }

    [TestMethod]
    public void GetLastChapterId_ReturnsCorrectId()
    {
        // Arrange
        TestStatusManager manager = new();
        int chapterId = 77;
        int nodeId = 1;
        manager.SaveProgress(chapterId, nodeId);

        // Act
        int lastId = manager.GetLastChapterId();

        // Assert
        Assert.AreEqual(chapterId, lastId, "GetLastChapterId should return the highest chapter id");
    }

    [TestMethod]
    public void AddItemToInventory_DoesNotAddDuplicates()
    {
        // Arrange
        TestStatusManager manager = new();
        string item = "unique_item";

        // Act
        manager.AddItemToInventory(item);
        manager.AddItemToInventory(item);

        // Assert
        int count = 0;
        foreach (object i in typeof(TestStatusManager).BaseType.GetProperty("Status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(manager).GetType().GetProperty("Inventory").GetValue(typeof(TestStatusManager).BaseType.GetProperty("Status", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(manager)) as System.Collections.IEnumerable)
            if ((string)i == item) count++;

        Assert.AreEqual(1, count, "Item should only be added once");
    }
}


