using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure.Helpers;
using KrissJourney.Tests.Infrastructure.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Unit.Infrastructure;

/// <summary>
/// The suite must never read or write the player's own status.json under LocalApplicationData.
/// Isolation hangs on one subtle thread: StatusManager's constructor reads AppDataPath through
/// the virtual getter, so TestStatusManager's override redirects the save file before any I/O.
/// Details in Tests/README.md, "Save file isolation".
/// </summary>
[TestClass]
public class SaveFileIsolationTests
{
    private static string realSaveFileHash;
    private static DateTime realSaveFileWrittenUtc;
    private static bool realSaveFileExisted;

    private static string RealSaveDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KrissJourney");

    private static string RealSaveFile => Path.Combine(RealSaveDirectory, "default", "status.json");

    [AssemblyInitialize]
    public static void SnapshotRealSaveFile(TestContext _)
    {
        realSaveFileExisted = File.Exists(RealSaveFile);
        if (!realSaveFileExisted)
            return;

        realSaveFileHash = HashOf(RealSaveFile);
        realSaveFileWrittenUtc = File.GetLastWriteTimeUtc(RealSaveFile);
    }

    [AssemblyCleanup]
    public static void AssertRealSaveFileUntouched()
    {
        if (!realSaveFileExisted)
        {
            if (File.Exists(RealSaveFile))
                throw new InvalidOperationException(
                    $"A test created the player's save file at '{RealSaveFile}'. " +
                    "Use TestStatusManager, never the production StatusManager.");

            return;
        }

        if (!File.Exists(RealSaveFile))
            throw new InvalidOperationException($"A test deleted the player's save file at '{RealSaveFile}'.");

        if (HashOf(RealSaveFile) != realSaveFileHash || File.GetLastWriteTimeUtc(RealSaveFile) != realSaveFileWrittenUtc)
            throw new InvalidOperationException(
                $"A test rewrote the player's save file at '{RealSaveFile}'. " +
                "Use TestStatusManager, never the production StatusManager.");
    }

    [TestMethod]
    public void TestStatusManager_KeepsItsSaveFileOutOfTheRealAppDataFolder()
    {
        TestStatusManager manager = new();

        FieldInfo pathField = typeof(StatusManager).GetField("_localStatusFilePath",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(pathField, "StatusManager no longer keeps its save path in _localStatusFilePath; update this guard.");

        string resolved = Path.GetFullPath((string)pathField.GetValue(manager));

        Assert.IsFalse(
            resolved.StartsWith(Path.GetFullPath(RealSaveDirectory), StringComparison.OrdinalIgnoreCase),
            $"TestStatusManager resolved its save file to '{resolved}', inside the player's own save folder. " +
            "StatusManager's constructor must keep reading AppDataPath through the virtual getter.");
    }

    [TestMethod]
    public void Setup_BuildsTheEngineOnTheTestStatusManager()
    {
        GameEngine engine = GameEngineTestExtensions.Setup();

        FieldInfo managerField = typeof(GameEngine).GetField("statusManager",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(managerField, "GameEngine no longer keeps its status manager in 'statusManager'; update this guard.");

        Assert.IsInstanceOfType(managerField.GetValue(engine), typeof(TestStatusManager),
            "The shared test setup must build on TestStatusManager, not the production StatusManager.");
    }

    private static string HashOf(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
}
