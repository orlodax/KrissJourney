using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using KrissJourney.Kriss;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;
using KrissJourney.Kriss.Services;
using KrissJourney.Tests.Infrastructure.Mocks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KrissJourney.Tests.Integration;

/// <summary>
/// The scaffolding every chapter walkthrough needs, factored out of the pattern
/// <see cref="Chapter17WalkthroughTests"/> and <see cref="Chapter24WalkthroughTests"/> each
/// carry their own copy of: the engine's blocking, recursive node.Load() -> AdvanceToNext ->
/// GameEngine.LoadNode chain runs on the test thread while a background Task plays the player,
/// waiting for each prompt to actually appear in the mock's output before answering it.
/// Those two files are left as they are; this base exists so the chapters added after them do
/// not each grow a sixth copy of the same helpers.
/// See Tests/README.md, "Chapter walkthroughs", for how a walk is written and why the engine
/// has to be driven this way rather than by calling Load() directly.
/// </summary>
public abstract class ChapterWalkthroughTestBase
{
    protected const string ContinuePrompt = "Press a key to continue...";

    /// <summary>Typist.RenderPrompt's command prompt, which prints no banner before blocking.</summary>
    protected const string ActionPrompt = "\\>";

    protected TerminalMock Terminal { get; private set; }

    [TestInitialize]
    public virtual void TestInitialize()
    {
        CommandLineOptions.IsDebug = true; // strips Typist's flow/pause delays; real-time Fight/Surge clocks are unaffected
        Terminal = new TerminalMock();
        TerminalFacade.SetTestTerminal(Terminal);
    }

    /// <summary>
    /// Loads every real chapter through the production embedded-resource pipeline
    /// (<see cref="GameEngine.Run"/>), then reflectively narrows the engine's private chapter
    /// list down to the requested ids, so an islast node's StartNextChapter lands outside the
    /// list and stops the walk deterministically instead of running on into the next chapter.
    /// </summary>
    protected static GameEngine BuildScopedEngine(params int[] chapterIds)
    {
        GameEngine engine = new(new TestStatusManager());
        engine.Run();

        FieldInfo chaptersField = typeof(GameEngine).GetField("chapters", BindingFlags.NonPublic | BindingFlags.Instance);
        List<Chapter> allChapters = (List<Chapter>)chaptersField.GetValue(engine);
        List<Chapter> scoped = [.. chapterIds.Select(id => allChapters.Single(c => c.Id == id))];
        chaptersField.SetValue(engine, scoped);

        return engine;
    }

    protected static List<Chapter> GetScopedChapters(GameEngine engine)
    {
        FieldInfo chaptersField = typeof(GameEngine).GetField("chapters", BindingFlags.NonPublic | BindingFlags.Instance);
        return (List<Chapter>)chaptersField.GetValue(engine);
    }

    protected static void SetCurrentChapter(GameEngine engine, int chapterId)
    {
        Chapter chapter = GetScopedChapters(engine).Single(c => c.Id == chapterId);

        PropertyInfo currentChapterProperty = typeof(GameEngine).GetProperty(nameof(GameEngine.CurrentChapter));
        currentChapterProperty.SetValue(engine, chapter);
    }

    /// <summary>
    /// Runs the walk: <paramref name="script"/> is already playing the player in the background,
    /// and this drives the engine on the test thread until it stops trying to start the chapter
    /// after <paramref name="expectedNextChapterId"/> - which this scoped engine does not have,
    /// so it surfaces as a clean ArgumentNullException. That exception IS the assertion that the
    /// walk reached the chapter's islast node and nothing past it.
    /// </summary>
    protected void RunWalkToChapterEnd(GameEngine engine, Task script, int startNodeId, int expectedNextChapterId, int scriptTimeoutSeconds = 60)
    {
        ArgumentNullException stoppedAt = null;

        try
        {
            engine.LoadNode(startNodeId);
            Assert.Fail($"Expected the walk to end trying to start chapter {expectedNextChapterId}, which this scoped engine does not have loaded.");
        }
        catch (ArgumentNullException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(scriptTimeoutSeconds)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual($"Chapter with ID {expectedNextChapterId} not found.", stoppedAt.ParamName,
            "The walk should stop only because the next chapter is outside this test's scoped chapter list, not for any earlier reason.");
        Assert.AreEqual(0, Terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");
    }

    /// <summary>
    /// Runs a walk that deliberately stops short of the chapter's end: the script leaves the
    /// last prompt unanswered, so the mock's ReadKey blocks for a second and then throws, which
    /// is the proof the walk got exactly that far and no further.
    /// </summary>
    protected void RunWalkToUnansweredPrompt(GameEngine engine, Task script, int startNodeId, int scriptTimeoutSeconds = 60)
    {
        InvalidOperationException stoppedAt = null;

        try
        {
            engine.LoadNode(startNodeId);
            Assert.Fail("Expected the walk's last unanswered prompt to time out the mock's ReadKey.");
        }
        catch (InvalidOperationException ex)
        {
            stoppedAt = ex;
        }

        Assert.IsTrue(script.Wait(TimeSpan.FromSeconds(scriptTimeoutSeconds)), "Input script timed out.");
        Assert.IsNotNull(stoppedAt);
        Assert.AreEqual("No keys available in mock terminal", stoppedAt.Message,
            "Expected exception message in testing environment only: System.Console.ReadKey never throws this way.");
        Assert.AreEqual(0, Terminal.KeyQueueCount, "Every queued key should have been consumed exactly once.");
    }

    // ---- prompt helpers ------------------------------------------------------------

    protected async Task<int> WaitForOutputIndexAsync(string marker, int startIndex, int timeoutMs = 5000)
    {
        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            string output = Terminal.GetOutput();
            int idx = output.IndexOf(marker, startIndex, StringComparison.Ordinal);
            if (idx >= 0)
                return idx + marker.Length;
            await Task.Delay(10);
        }

        throw new TimeoutException(
            $"Marker not found in output within {timeoutMs}ms: '{marker}'.\n---- output so far ----\n{Terminal.GetOutput()}");
    }

    /// <summary>Waits for the next generic "press a key" prompt and answers it with Enter.</summary>
    protected async Task<int> ContinueAsync(int startIndex, int timeoutMs = 5000)
    {
        int idx = await WaitForOutputIndexAsync(ContinuePrompt, startIndex, timeoutMs);
        Terminal.EnqueueKeys(ConsoleKey.Enter);
        return idx;
    }

    /// <summary>
    /// Waits for a marker unique to a Choice/Dialogue-reply prompt (which, unlike
    /// Typist.WaitForKey, prints no "press a key" banner before its raw ReadKey), then plays the
    /// given navigation keys followed by Enter. Both nodes keep their selectedRow between
    /// prompts, so navigation is relative to wherever the previous pick left the highlight.
    /// </summary>
    protected async Task<int> ChooseAsync(int startIndex, string marker, params ConsoleKey[] navigation)
    {
        int idx = await WaitForOutputIndexAsync(marker, startIndex);
        if (navigation.Length > 0)
            Terminal.EnqueueKeys(navigation);
        Terminal.EnqueueKeys(ConsoleKey.Enter);
        return idx;
    }

    /// <summary>Types a command at an ActionNode's prompt. Does not answer whatever the answer prints.</summary>
    protected async Task<int> ActAsync(int startIndex, string command)
    {
        int idx = await WaitForOutputIndexAsync(ActionPrompt, startIndex);
        Terminal.EnqueueText(command);
        Terminal.EnqueueKeys(ConsoleKey.Enter);
        return idx;
    }

    /// <summary>
    /// Types a command and answers the "press a key to continue" prompt that follows it. Use
    /// this for a command that carries a ChildId (ActionNode.DisplaySuccess always pauses once
    /// before advancing); use <see cref="ActAsync"/> for one that only prints an answer and
    /// hands the prompt straight back.
    /// </summary>
    protected async Task<int> DoActionAsync(int startIndex, string command)
    {
        int idx = await ActAsync(startIndex, command);
        return await ContinueAsync(idx);
    }

    // ---- Surge helpers -------------------------------------------------------------

    /// <summary>
    /// Plays a Surge row the chapter generates at random. SurgeNode draws the row before it
    /// draws the bar, so the arrow glyphs between <paramref name="startIndex"/> and the FIRST
    /// "RAGE" are exactly one frame of the row - no chapter's prose contains U+2190..U+2193, so
    /// nothing else can be mistaken for them. Waiting for that first frame also guarantees
    /// PlayRow has already discarded stale queued keys and started its Stopwatch, which is why
    /// no key may be queued before it.
    /// Queuing the whole correct row at once stays ahead of the drain: PlayRow drains its input
    /// queue every ~10ms tick and every key here is correct, so the row resolves in a tick or two.
    /// Only for rows that are entirely Kriss's - see <see cref="PlayAlternatingDuetSurgeAsync"/>.
    /// </summary>
    protected async Task<int> PlaySurgeToVictoryAsync(int startIndex, int glyphCount)
    {
        int rageIdx = await WaitForOutputIndexAsync("RAGE", startIndex);
        List<SurgeDirection> row = ReadDrawnRow(startIndex, rageIdx, glyphCount);

        foreach (SurgeDirection direction in row)
            Terminal.EnqueueKeys(direction.Key());

        return rageIdx;
    }

    /// <summary>
    /// Plays a random row under a strictly alternating ".S" duet, where the whole row cannot be
    /// queued at once: everything pressed while the caret sits on one of Saberinne's glyphs is
    /// swallowed, so a batch would spend itself against her share and deadlock a Surge that
    /// (drain 0, penalty 0) can never end on its own.
    /// Instead this presses one glyph per turn, using the caret's own colour as the signal:
    /// SurgeNode.CaretColor paints it white on Kriss's glyphs and Saberinne's green on hers, so
    /// each fresh white caret frame is one press owed.
    /// </summary>
    protected async Task<int> PlayAlternatingDuetSurgeAsync(int startIndex, int glyphCount, int timeoutMs = 30000)
    {
        int rageIdx = await WaitForOutputIndexAsync("RAGE", startIndex);
        List<SurgeDirection> row = ReadDrawnRow(startIndex, rageIdx, glyphCount);

        // The caret row is drawn BEFORE the rage bar in every frame, so a caret belonging to this
        // Surge sits at an index below rageIdx: anchoring the search there would never see the
        // opening frame, and with drain at zero nothing would ever redraw to produce another.
        int searchFrom = startIndex;
        bool caretWasHers = false;

        // Kriss owns the even indices under ".S"; hers resolve themselves one per DuetBeat.
        for (int glyph = 0; glyph < glyphCount; glyph += 2)
        {
            Stopwatch sw = Stopwatch.StartNew();
            bool pressed = false;

            while (!pressed)
            {
                if (sw.ElapsedMilliseconds > timeoutMs)
                    throw new TimeoutException($"Duet Surge never handed the caret back for glyph {glyph}.\n{Terminal.GetOutput()}");

                bool? caretIsHers = LatestCaretIsSaberinnes(searchFrom);

                if (caretIsHers is false && (glyph == 0 || caretWasHers))
                {
                    Terminal.EnqueueKeys(row[glyph].Key());
                    caretWasHers = false;
                    pressed = true;
                }
                else
                {
                    if (caretIsHers is true)
                        caretWasHers = true;

                    await Task.Delay(10);
                }
            }
        }

        return searchFrom;
    }

    /// <summary>
    /// Pulls one drawn glyph row out of the captured output. Deliberately strict about the
    /// count: a short read means the frame was captured mid-write and the row would be wrong.
    /// </summary>
    List<SurgeDirection> ReadDrawnRow(int startIndex, int endIndex, int glyphCount)
    {
        string window = Terminal.GetOutput()[startIndex..endIndex];

        List<SurgeDirection> row = [.. window
            .Where(c => c is SurgeDirectionExtensions.LeftGlyph
                          or SurgeDirectionExtensions.UpGlyph
                          or SurgeDirectionExtensions.RightGlyph
                          or SurgeDirectionExtensions.DownGlyph)
            .Select(DirectionOf)];

        Assert.AreEqual(glyphCount, row.Count,
            $"Expected exactly one drawn Surge row of {glyphCount} glyphs before the first rage bar.");

        return row;
    }

    static SurgeDirection DirectionOf(char glyph) => glyph switch
    {
        SurgeDirectionExtensions.LeftGlyph => SurgeDirection.Left,
        SurgeDirectionExtensions.UpGlyph => SurgeDirection.Up,
        SurgeDirectionExtensions.RightGlyph => SurgeDirection.Right,
        _ => SurgeDirection.Down,
    };

    /// <summary>
    /// Whose glyph the caret is resting on in the most recently drawn frame, read off the colour
    /// SurgeNode writes the caret row in. Null while no caret row has been drawn yet.
    /// </summary>
    bool? LatestCaretIsSaberinnes(int searchFrom)
    {
        string output = Terminal.GetOutput();
        int caret = output.LastIndexOf('^');

        if (caret < searchFrom)
            return null;

        int open = output.LastIndexOf('[', caret);
        if (open < 0)
            return null;

        int close = output.IndexOf(']', open);
        if (close < 0 || close > caret)
            return null;

        string color = output[(open + 1)..close];

        if (color == nameof(ConsoleColor.White))
            return false;

        return color == EnCharacter.Saberinne.Color().ToString() ? true : null;
    }

    // ---- Fight helpers -------------------------------------------------------------

    /// <summary>
    /// Drives a whole <see cref="FightNode"/> encounter to victory: answers whichever comes
    /// first out of the next dodge/strike QTE prompt, a between-rounds pause, or the victory
    /// text, until the victory marker is reached. Lifted from
    /// <see cref="Chapter17WalkthroughTests"/>, whose own comment records why the QTE is
    /// resolved by polling the drawn frames rather than by pre-computing a sleep.
    /// </summary>
    protected async Task<int> DriveFightAsync(int startIndex, string victoryMarker, int qteLength, int overallTimeoutMs = 120000)
    {
        int idx = startIndex;

        Stopwatch sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < overallTimeoutMs)
        {
            string output = Terminal.GetOutput();

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
                Terminal.EnqueueKeys(ConsoleKey.Enter);
                continue;
            }

            idx = await ResolveQteAsync(best, isDodge: best == dodgeIdx, qteLength);
        }

        throw new TimeoutException($"Fight did not reach victory marker '{victoryMarker}' in time.\n{Terminal.GetOutput()}");
    }

    /// <summary>
    /// Resolves a single dodge/strike QTE by reacting to the frames as they are drawn: reads the
    /// required arrow off the prompt, finds the 'X' target in the first frame, then presses the
    /// moment any later frame's cursor lands within the game's own +/-2 success window.
    /// </summary>
    async Task<int> ResolveQteAsync(int suffixIdx, bool isDodge, int qteLength)
    {
        string suffix = isDodge ? " to dodge!" : " to strike!";

        string output = Terminal.GetOutput();
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
            output = Terminal.GetOutput();
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
            output = Terminal.GetOutput();
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
                        Terminal.EnqueueKeys(key);
                        return lastKnownFrameEnd;
                    }
                }
            }
            await Task.Delay(8);
        }

        // Fell through every frame this QTE will draw: press anyway so the node's own timeout-Fail
        // logic resolves it rather than hanging the whole walk.
        Terminal.EnqueueKeys(key);
        return lastKnownFrameEnd;
    }
}
