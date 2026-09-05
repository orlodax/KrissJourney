using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KrissJourney.Kriss;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Integration;

/// <summary>
/// Drives c17 ("THE EDZZEN") through a real <see cref="GameEngine"/>/<see cref="TerminalMock"/> pair,
/// following the same pattern as <see cref="Chapter16WalkthroughTests"/>. Two things this chapter adds
/// over c16's walk:
///
/// 1. The tribunal at node 5 carries two reply prompts, each offering the player a chance to keep
///    heckling or to cut straight to the fight - exercised here by picking the heckle once (index 0,
///    default selection) and then drawing steel on the second prompt (index 1, via DownArrow).
///
/// 2. Two real <see cref="KrissJourney.Kriss.Nodes.FightNode"/> encounters (the bar brawl at node 6,
///    shared by both paths, and the roof duel at node 140, slow-path only) have to be won through the
///    QTE's actual random timing, not just a scripted key press. <see cref="DriveFightAsync"/> and
///    <see cref="ResolveQteAsync"/> solve this deterministically rather than gambling on luck: the mock
///    terminal's Write appends literally with no real cursor overwrite, so every oscillating-cursor
///    frame the node draws survives in the captured output. The helper locates the 'X' target in the
///    first frame, then polls each subsequent frame as it is actually drawn and presses the moment any
///    frame's cursor lands within the game's own +/-2 success window - reacting to what is on screen
///    rather than pre-computing a single sleep from QteSpeedFactor, which turned out to drift too far
///    off on long waits (node 6's QteLength of 36) to be reliable and once even ran the fight long
///    enough to blow the stack through FightNode's recursive round-by-round self-calls.
///
/// Node 15 is islast with no childid, so <see cref="KrissJourney.Kriss.Nodes.NodeBase.AdvanceToNext"/>
/// calls <see cref="GameEngine.StartNextChapter"/> unconditionally once its own "press a key" prompt is
/// answered. Since this engine's chapter list has been reflectively narrowed to just chapter 17 (c18
/// does not exist as adapted content yet), that surfaces as a clean, deterministic
/// ArgumentNullException from StartChapter ("Chapter with ID 18 not found.") - proof the walk reached
/// node 15 and nothing past it.
/// </summary>
[TestClass]
public class Chapter17WalkthroughTests
{
    const string ContinuePrompt = "Press a key to continue...";

    TerminalMock terminal;

    [TestInitialize]
    public void TestInitialize()
    {
        CommandLineOptions.IsDebug = true; // strips Typist's flow/pause delays; FightNode's own 600ms beats are unaffected
        terminal = new TerminalMock();
        TerminalFacade.SetTestTerminal(terminal);
    }

    /// <summary>
    /// The efficient run: no fumble at any lighthouse stage, so Kriss jumps clean, lands on the ship,
    /// and Corolla fights her own rooftop duel exactly as narrated in the source - node 14's Dialogue,
    /// no second Fight node involved.
    /// </summary>
    [TestMethod]
    [Timeout(180000)]
    public void FastPath_NoFumbles_KrissJumpsCleanAndCorollaWinsHerOwnNarratedDuel()
    {
        GameEngine engine = BuildScopedEngine(17);
        SetCurrentChapter(engine, 17);

        ArgumentNullException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                              // node 1 -> 2
            idx = await ContinueAsync(idx);                                              // node 2 -> 3
            idx = await ContinueAsync(idx);                                              // node 3 -> 4
            idx = await ContinueAsync(idx);                                              // node 4 -> 5

            idx = await ChooseAsync(idx, "condemned to die.");                            // node 5: 1st reply prompt, index 0 (heckle) -> "theobit"
            idx = await ContinueAsync(idx);                                              // Theo's break line -> 2nd Efeliah translation
            idx = await ChooseAsync(idx, "putrid remains.", ConsoleKey.DownArrow);        // node 5: 2nd reply prompt, index 1 (draw sword) -> childid 6

            idx = await ContinueAsync(idx);                                              // Fight: "Prepare to fight!" -> rounds begin
            idx = await DriveFightAsync(idx, "You push through.", qteLength: 36);
            idx = await ContinueAsync(idx);                                              // victory message's own continue -> node 7

            idx = await ContinueAsync(idx);                                              // node 7 -> node 8

            idx = await DoActionAsync(idx, "climbs into the dark.", "bar");               // node 8 (shared): bar the door -> node 9
            idx = await DoActionAsync(idx, "might be encouragement.", "climb");           // node 9 (fast): climb -> node 10
            idx = await DoActionAsync(idx, "This way!", "open");                          // node 10 (fast): through the hatch -> node 11
            idx = await DoActionAsync(idx, "waiting on you.", "wedge");                   // node 11 (fast): wedge hatch -> node 12

            idx = await WaitForOutputIndexAsync("entirely indifferent to all of this.", idx); // node 12 (fast) has loaded
            terminal.EnqueueText("look");
            terminal.EnqueueKeys(ConsoleKey.Enter);
            idx = await WaitForOutputIndexAsync("tilted up toward the sun like a ramp.", idx); // look has no childid: back to the prompt directly
            terminal.EnqueueText("jump");
            terminal.EnqueueKeys(ConsoleKey.Enter);
            idx = await ContinueAsync(idx);                                              // jump's own continue -> node 13

            idx = await ContinueAsync(idx);                                              // node 13 -> node 14
            idx = await ContinueAsync(idx);                                              // node 14 (duel narrated, no replies) -> node 15

            idx = await ContinueAsync(idx);                                              // node 15 -> StartNextChapter(18), out of scope
        });

        try
        {
            engine.LoadNode(1);
            Assert.Fail("Expected the walk to end trying to start chapter 18, which this scoped engine does not have loaded.");
        }
        catch (ArgumentNullException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(170)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual("Chapter with ID 18 not found.", stoppedAt.ParamName,
            "The walk should stop only because chapter 18 is outside this test's scoped chapter list, not for any earlier reason.");
        Assert.AreEqual(0, terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");

        string output = terminal.GetOutput();
        Assert.IsTrue(output.Contains("CHAPTER 17"), "Should have rendered c17's own header at node 1.");
        Assert.IsTrue(output.Contains("Has he even seen himself?"), "Push-heckle branch should have reached the second reply's own line.");
        Assert.IsTrue(output.Contains("You go off the edge of the lighthouse"), "Fast path should reach the clean jump (node 12).");
        Assert.IsTrue(output.Contains("A figure has pulled itself over the edge of the roof."), "Should reach the fast-path duel setup (node 13).");
        Assert.IsFalse(output.Contains("burn scar from Ayonn."), "Fast path must not touch the slow-path Fight variant's own victory text.");
        Assert.IsTrue(output.Contains("Corolla is always fine."), "Should reach the shared ending (node 15).");
    }

    /// <summary>
    /// The fumbling run: hesitating at the very first lighthouse stage (node 8's "wait" trap) routes
    /// the rest of the escape down the slow track, where the bigger Edzzen reaches the roof before
    /// anyone can jump and Kriss has to fight it directly - a real, tougher-tuned Fight node (140)
    /// rather than Corolla's narrated duel, exactly as the issue asks: failure escalates the encounter,
    /// it does not end the game.
    /// </summary>
    [TestMethod]
    [Timeout(180000)]
    public void SlowPath_FumbleAtGroundFloor_RoutesToTheHarderRoofFightInsteadOfGameOver()
    {
        GameEngine engine = BuildScopedEngine(17);
        SetCurrentChapter(engine, 17);

        ArgumentNullException stoppedAt = null;

        Task script = Task.Run(async () =>
        {
            int idx = 0;

            idx = await ContinueAsync(idx);                                              // node 1 -> 2
            idx = await ContinueAsync(idx);                                              // node 2 -> 3
            idx = await ContinueAsync(idx);                                              // node 3 -> 4
            idx = await ContinueAsync(idx);                                              // node 4 -> 5

            idx = await ChooseAsync(idx, "condemned to die.", ConsoleKey.DownArrow);       // node 5: 1st reply prompt, index 1 (draw sword now) -> childid 6

            idx = await ContinueAsync(idx);                                              // Fight: "Prepare to fight!" -> rounds begin
            idx = await DriveFightAsync(idx, "You push through.", qteLength: 36);
            idx = await ContinueAsync(idx);                                              // victory message's own continue -> node 7

            idx = await ContinueAsync(idx);                                              // node 7 -> node 8

            idx = await DoActionAsync(idx, "climbs into the dark.", "wait");              // node 8 (shared): fumble -> node 90 (slow)

            idx = await DoActionAsync(idx, "thin and urgent.", "climb");                  // node 90 (slow): climb -> node 100
            idx = await DoActionAsync(idx, "you would call reassuring.", "open");         // node 100 (slow): through the hatch -> node 110
            idx = await DoActionAsync(idx, "Corolla has her stiletto out.", "wedge");      // node 110 (slow): wedge hatch -> node 130

            idx = await ContinueAsync(idx);                                              // node 130 (Story, auto) -> node 140

            idx = await ContinueAsync(idx);                                              // Fight: "Prepare to fight!" -> rounds begin
            idx = await DriveFightAsync(idx, "does not get up.", qteLength: 6);
            idx = await ContinueAsync(idx);                                              // victory message's own continue -> node 150

            idx = await ContinueAsync(idx);                                              // node 150 -> node 15

            idx = await ContinueAsync(idx);                                              // node 15 -> StartNextChapter(18), out of scope
        });

        try
        {
            engine.LoadNode(1);
            Assert.Fail("Expected the walk to end trying to start chapter 18, which this scoped engine does not have loaded.");
        }
        catch (ArgumentNullException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(170)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual("Chapter with ID 18 not found.", stoppedAt.ParamName,
            "The walk should stop only because chapter 18 is outside this test's scoped chapter list, not for any earlier reason.");
        Assert.AreEqual(0, terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");

        string output = terminal.GetOutput();
        Assert.IsTrue(output.Contains("shove Smiurl toward the stair instead."), "The fumble at node 8 should have fired.");
        Assert.IsTrue(output.Contains("There is no jumping past this one."), "Should reach the slow-path confrontation (node 130).");
        Assert.IsTrue(output.Contains("does not get up."), "Should win the harder roof Fight (node 140), not a Game Over.");
        Assert.IsFalse(output.Contains("GAME OVER"), "Failure at the lighthouse stage must not itself produce a Game Over.");
        Assert.IsTrue(output.Contains("burn scar from Ayonn."), "The wound should still land on the Ayonn scar in the slow variant.");
        Assert.IsTrue(output.Contains("Corolla is always fine."), "Should converge on the shared ending (node 15).");
    }

    // ---- helpers -------------------------------------------------------------------

    static GameEngine BuildScopedEngine(params int[] chapterIds)
    {
        GameEngine engine = new(new TestStatusManager());
        engine.Run();

        FieldInfo chaptersField = typeof(GameEngine).GetField("chapters", BindingFlags.NonPublic | BindingFlags.Instance);
        List<Chapter> allChapters = (List<Chapter>)chaptersField.GetValue(engine);
        List<Chapter> scoped = [.. chapterIds.Select(id => allChapters.Single(c => c.Id == id))];
        chaptersField.SetValue(engine, scoped);

        return engine;
    }

    static void SetCurrentChapter(GameEngine engine, int chapterId)
    {
        FieldInfo chaptersField = typeof(GameEngine).GetField("chapters", BindingFlags.NonPublic | BindingFlags.Instance);
        List<Chapter> chapters = (List<Chapter>)chaptersField.GetValue(engine);
        Chapter chapter = chapters.Single(c => c.Id == chapterId);

        PropertyInfo currentChapterProperty = typeof(GameEngine).GetProperty(nameof(GameEngine.CurrentChapter));
        currentChapterProperty.SetValue(engine, chapter);
    }

    /// <summary>Waits for the next generic "press a key" prompt and answers it with Enter.</summary>
    async Task<int> ContinueAsync(int startIndex, int timeoutMs = 5000)
    {
        int idx = await WaitForOutputIndexAsync(ContinuePrompt, startIndex, timeoutMs);
        terminal.EnqueueKeys(ConsoleKey.Enter);
        return idx;
    }

    /// <summary>
    /// Waits for a marker unique to a Dialogue-reply prompt (which, unlike
    /// <see cref="KrissJourney.Kriss.Helpers.Typist.WaitForKey"/>, prints no "press a key" banner
    /// before its raw ReadKey), then plays the given navigation keys followed by Enter.
    /// </summary>
    async Task<int> ChooseAsync(int startIndex, string marker, params ConsoleKey[] navigation)
    {
        int idx = await WaitForOutputIndexAsync(marker, startIndex);
        if (navigation.Length > 0)
            terminal.EnqueueKeys(navigation);
        terminal.EnqueueKeys(ConsoleKey.Enter);
        return idx;
    }

    /// <summary>
    /// Waits for a marker proving the target Action node has actually loaded (its own scene text),
    /// then types the given single-word command and Enter. A successful command in this chapter always
    /// carries both an Answer and a ChildId, so <c>ActionNode.DisplaySuccess</c> follows with its own
    /// "press a key to continue" prompt before advancing - callers still need a following
    /// <see cref="ContinueAsync"/> for that prompt; this helper only submits the command itself.
    /// </summary>
    async Task<int> TypeCommandAsync(int startIndex, string sceneMarker, string command)
    {
        int idx = await WaitForOutputIndexAsync(sceneMarker, startIndex);
        terminal.EnqueueText(command);
        terminal.EnqueueKeys(ConsoleKey.Enter);
        return idx;
    }

    /// <summary>
    /// Submits an Action-node command and answers the "press a key to continue" prompt that follows
    /// it (every successful command in this chapter's lighthouse sequence carries both an Answer and a
    /// ChildId, so <c>ActionNode.DisplaySuccess</c> always produces exactly one such prompt before
    /// advancing to the next node).
    /// </summary>
    async Task<int> DoActionAsync(int startIndex, string sceneMarker, string command)
    {
        int idx = await TypeCommandAsync(startIndex, sceneMarker, command);
        return await ContinueAsync(idx);
    }

    async Task<int> WaitForOutputIndexAsync(string marker, int startIndex, int timeoutMs = 5000)
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            string output = terminal.GetOutput();
            int idx = output.IndexOf(marker, startIndex, StringComparison.Ordinal);
            if (idx >= 0)
                return idx + marker.Length;
            await Task.Delay(10);
        }

        throw new TimeoutException(
            $"Marker not found in output within {timeoutMs}ms: '{marker}'.\n---- output so far ----\n{terminal.GetOutput()}");
    }

    /// <summary>
    /// Drives a whole <see cref="KrissJourney.Kriss.Nodes.FightNode"/> encounter to victory: repeatedly
    /// finds whichever comes first out of the next dodge/strike QTE prompt, a "press a key to continue"
    /// pause between rounds, or the encounter's own victory text, and answers it, until the victory
    /// marker is reached. Throws if a "GAME OVER" is printed first, since none of this chapter's
    /// intended walkthroughs should lose either fight.
    /// </summary>
    async Task<int> DriveFightAsync(int startIndex, string victoryMarker, int qteLength, int overallTimeoutMs = 120000)
    {
        int idx = startIndex;

        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < overallTimeoutMs)
        {
            string output = terminal.GetOutput();

            int victoryIdx = output.IndexOf(victoryMarker, idx, StringComparison.Ordinal);
            if (victoryIdx >= 0)
                return victoryIdx + victoryMarker.Length;

            int gameOverIdx = output.IndexOf("GAME OVER", idx, StringComparison.Ordinal);
            if (gameOverIdx >= 0)
                throw new InvalidOperationException(
                    $"Fight resolved in defeat instead of victory ('{victoryMarker}' never appeared):\n{output[idx..]}");

            int dodgeIdx = output.IndexOf(" to dodge!", idx, StringComparison.Ordinal);
            int strikeIdx = output.IndexOf(" to strike!", idx, StringComparison.Ordinal);
            int continueIdx = output.IndexOf(ContinuePrompt, idx, StringComparison.Ordinal);

            int best = new[] { dodgeIdx, strikeIdx, continueIdx }.Where(i => i >= 0).DefaultIfEmpty(-1).Min();

            if (best < 0)
            {
                await Task.Delay(15);
                continue;
            }

            if (best == continueIdx)
            {
                idx = continueIdx + ContinuePrompt.Length;
                terminal.EnqueueKeys(ConsoleKey.Enter);
                continue;
            }

            bool isDodge = best == dodgeIdx;
            idx = await ResolveQteAsync(best, isDodge, qteLength);
        }

        throw new TimeoutException($"Fight did not reach victory marker '{victoryMarker}' in time.\n{terminal.GetOutput()}");
    }

    /// <summary>
    /// Resolves a single dodge/strike QTE. Parses the required arrow key from the "Press X to
    /// dodge/strike!" line, reads the very first oscillating-cursor frame drawn afterward to find the
    /// 'X' target's position, then <b>polls</b> each subsequent frame as it is drawn (rather than
    /// pre-computing a single sleep) and presses the moment any frame's cursor comes within the game's
    /// own +/-2 success window. Reacting to what is actually on screen, instead of predicting when it
    /// will be, avoids compounding scheduler jitter over a multi-second wait - an earlier version of
    /// this helper pre-computed a delay from the node's QteSpeedFactor and slept for it, which worked
    /// for small targets but landed outside the window often enough on far targets (large QteLength,
    /// e.g. node 6's 36) to cost real health, and once even ran the fight long enough to blow the
    /// stack through FightNode's recursive round-by-round self-calls.
    /// </summary>
    async Task<int> ResolveQteAsync(int suffixIdx, bool isDodge, int qteLength)
    {
        string suffix = isDodge ? " to dodge!" : " to strike!";

        string output = terminal.GetOutput();
        int pressIdx = output.LastIndexOf("Press ", suffixIdx, StringComparison.Ordinal);
        int dirStart = pressIdx + "Press ".Length;
        int dirEnd = output.IndexOf(suffix, pressIdx, StringComparison.Ordinal);
        string dir = output[dirStart..dirEnd].Trim();

        ConsoleKey key = dir switch
        {
            "UP" => ConsoleKey.UpArrow,
            "DOWN" => ConsoleKey.DownArrow,
            "LEFT" => ConsoleKey.LeftArrow,
            "RIGHT" => ConsoleKey.RightArrow,
            _ => throw new InvalidOperationException($"Unrecognized QTE direction '{dir}'."),
        };

        int searchFrom = suffixIdx + suffix.Length;

        int firstFrameStart = await WaitForOutputIndexAsync("<", searchFrom);
        string firstFrame = null;
        Stopwatch captureSw = Stopwatch.StartNew();
        while (captureSw.ElapsedMilliseconds < 2000)
        {
            output = terminal.GetOutput();
            if (output.Length >= firstFrameStart + qteLength)
            {
                firstFrame = output.Substring(firstFrameStart, qteLength);
                break;
            }
            await Task.Delay(5);
        }

        if (firstFrame == null)
            throw new TimeoutException("QTE frame was not fully captured in time.");

        int targetPos = firstFrame.IndexOf('X');
        if (targetPos < 0)
            targetPos = 1; // the cursor already covers the target in this very frame: any press now is a Perfect

        int lastKnownFrameEnd = firstFrameStart + qteLength;

        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 8000)
        {
            output = terminal.GetOutput();
            int latestFrameMarker = output.LastIndexOf('<');
            if (latestFrameMarker >= searchFrom)
            {
                int contentStart = latestFrameMarker + 1;
                if (output.Length >= contentStart + qteLength)
                {
                    string frame = output.Substring(contentStart, qteLength);
                    int cursorPos = frame.IndexOf('█');
                    if (cursorPos < 0)
                        cursorPos = targetPos; // this frame's cursor is exactly on the target

                    lastKnownFrameEnd = contentStart + qteLength;

                    if (Math.Abs(cursorPos - targetPos) <= 2)
                    {
                        terminal.EnqueueKeys(key);
                        return lastKnownFrameEnd;
                    }
                }
            }
            await Task.Delay(8);
        }

        // Fell through every frame this QTE will draw (its own QteCycles ran out): press anyway so the
        // node's own timeout-Fail logic resolves it rather than hanging the whole walk.
        terminal.EnqueueKeys(key);
        return lastKnownFrameEnd;
    }
}
