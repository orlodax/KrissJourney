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
    private const string TestPath = "test_path";
    private readonly Status testStatus = new()
    {
        VisitedNodes = [],
        Inventory = []
    };

    // Override the protected property
    protected override Status Status => testStatus;

    // The save folder is passed to the base constructor (issue 26). Overriding AppDataPath no
    // longer redirects anything: the base no longer reads it back. Tests/README.md, "Save file
    // isolation".
    public TestStatusManager() : base(TestPath)
    { }

    // Override methods to avoid file system operations
    public override void SaveProgress(int chapterId, int nodeId)
    {
        // mirrors the production StatusManager: re-entering a node already on the list is a
        // no-op, NOT a reason to replace the chapter's whole list with just that node - which
        // is what the old flattened && did, silently wiping every earlier visit the moment any
        // hub node (c10's Mazerock crossroads, c15's award ceremony) was entered twice
        if (testStatus.VisitedNodes.TryGetValue(chapterId, out List<int> visitedNodes))
        {
            if (!visitedNodes.Contains(nodeId))
                visitedNodes.Add(nodeId);
        }
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