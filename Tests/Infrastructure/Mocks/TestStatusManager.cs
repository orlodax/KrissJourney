using System.Collections.Generic;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Services;

namespace KrissJourney.Tests.Infrastructure.Mocks;

/// <summary>
/// A test implementation of StatusManager that doesn't rely on the filesystem
/// </summary>
public class TestStatusManager : StatusManager
{
    // Fields to use instead of relying on base class properties
    private readonly string testPath = "test_path";
    private readonly Status testStatus = new()
    {
        VisitedNodes = [],
        Inventory = []
    };

    // Override the protected properties
    protected override string AppDataPath => testPath;
    protected override Status Status => testStatus;

    // Constructor with base() call to ensure initialization
    public TestStatusManager() : base()
    {
        // Override the initialization behavior from the base class
        // We'll use our _testStatus and _testPath instead
    }

    // Override methods to avoid file system operations
    public override void SaveProgress(int chapterId, int nodeId)
    {
        if (testStatus.VisitedNodes.TryGetValue(chapterId, out List<int> visitedNodes) && !visitedNodes.Contains(nodeId))
            visitedNodes.Add(nodeId);
        else
            testStatus.VisitedNodes[chapterId] = [nodeId];

        // No file system operations needed
    }

    public override bool HasVisitedNodes()
    {
        return testStatus.VisitedNodes.Count != 0;
    }

    public override int GetLastChapterId()
    {
        if (testStatus.VisitedNodes.Count == 0)
            return 1;

        int max = 1;
        foreach (int key in testStatus.VisitedNodes.Keys)
            if (key > max)
                max = key;

        return max;
    }

    public override bool IsNodeVisited(int chapterId, int nodeId)
    {
        if (testStatus.VisitedNodes.TryGetValue(chapterId, out List<int> visitedNodes))
            return visitedNodes.Contains(nodeId);

        return false;
    }

    public override void AddItemToInventory(string item)
    {
        if (!testStatus.Inventory.Contains(item))
            testStatus.Inventory.Add(item);
    }

    public override bool IsItemInInventory(string item)
    {
        return testStatus.Inventory.Contains(item);
    }
}