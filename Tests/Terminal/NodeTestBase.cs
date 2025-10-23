using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using KrissJourney.Kriss; // For CommandLineOptions
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure;
using KrissJourney.Tests.Infrastructure.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Terminal;

/// <summary>
/// Base class for node terminal-based tests
/// </summary>
public abstract class NodeTestBase
{
    /// <summary>
    /// The unified test runner for node testing
    /// </summary>
    protected NodeTestRunner TestRunner { get; set; }

    /// <summary>
    /// Shorthand access to the GameEngine from the TestRunner
    /// </summary>
    protected GameEngine GameEngine => TestRunner.GameEngine;

    /// <summary>
    /// Shorthand access to the TerminalMock from the TestRunner
    /// </summary>
    protected TerminalMock TerminalMock => TestRunner.Terminal;

    [TestInitialize]
    public virtual void TestInitialize()
    {
        // Force fast rendering in tests (disable Typist delays)
        CommandLineOptions.IsDebug = true;

        // Create the test runner with terminal mock
        TestRunner = new NodeTestRunner(setupTerminalMock: true);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        // Assert that all simulated input has been consumed
        if (TerminalMock is TerminalMock mock)
            if (mock.KeyQueueCount > 0)
                Assert.Fail($"Test did not consume all simulated input. {mock.KeyQueueCount} key(s) left in queue.");
    }

    /// <summary>
    /// Creates a node of the specified type with default settings
    /// </summary>
    protected T CreateNode<T>(int nodeId = 1, Action<T> configure = null) where T : NodeBase, new()
    {
        return TestRunner.CreateNode(nodeId, configure);
    }

    /// <summary>
    /// Simulates user input by providing a sequence of keys or text
    /// </summary>
    protected void SimulateUserInput(params ConsoleKey[] keys)
    {
        TestRunner.SimulateUserInput(keys);
    }

    /// <summary>
    /// Simulates typing text
    /// </summary>
    protected void SimulateTextInput(string text)
    {
        TestRunner.SimulateTextInput(text);
    }

    /// <summary>
    /// Waits until the output contains a substring or times out.
    /// Returns true if found, false if timeout.
    /// </summary>
    protected bool WaitForOutputContains(string marker, int timeoutMs = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            string output = TerminalMock.GetOutput();
            if (output.Contains(marker, StringComparison.Ordinal))
                return true;
            Thread.Sleep(10);
        }
        return false;
    }

    /// <summary>
    /// Asynchronously waits until the output contains marker at or after startIndex.
    /// Returns the index just after the matched marker, or throws on timeout.
    /// </summary>
    protected async Task<int> WaitForOutputIndexAsync(string marker, int startIndex, int timeoutMs = 3000)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            string output = TerminalMock.GetOutput();
            int idx = output.IndexOf(marker, startIndex, StringComparison.Ordinal);
            if (idx >= 0)
                return idx + marker.Length;
            await Task.Delay(10);
        }
        throw new TimeoutException($"Marker not found in output within {timeoutMs}ms: '{marker}'");
    }


    /// <summary>
    /// Asserts that the exception message for a ReadKey operation is as expected.
    /// An exception is expected to be thrown in the testing environment only.
    /// This is a workaround for the fact that System.Console.ReadKey never throws an exception in a real terminal.
    /// </summary>
    /// <param name="exceptionMessage"></param>
    protected static void AssertReadKeyException(string exceptionMessage)
    {
        Assert.AreEqual(
            "No keys available in mock terminal",
            exceptionMessage,
            message: "Expected exception message in testing environment only: System.Console.ReadKey never throws this way.");
    }

    /// <summary>
    /// Clear key queue to avoid leftover input error for tests that don't consume all input
    /// </summary>
    protected void TerminalCleanup()
    {
        while (TerminalMock.KeyQueueCount > 0)
            TerminalMock.ReadKey();
    }

    protected static void LoadNode(NodeBase node)
    {
        try
        {
            node.Load();
        }
        catch (InvalidOperationException ex)
        {
            AssertReadKeyException(ex.Message);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Unexpected exception during node load: {ex.Message}");
        }
    }
}
