using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using KrissJourney.Kriss.Models;
using KrissJourney.Kriss.Nodes;

namespace KrissJourney.Kriss.Services;

public class GameEngine(StatusManager statusManager)
{
    public Chapter CurrentChapter { get; private set; }
    public NodeBase CurrentNode { get; private set; }

    readonly List<Chapter> chapters = [];

    readonly StatusManager statusManager = statusManager;

    public void Run()
    {
        // Dump any exception
        AppDomain.CurrentDomain.UnhandledException += LogError;

        // Load all Chapters
        int id = 1;
        do
        {
            string jChapter = LoadResource($"KrissJourney.Kriss.Chapters.c{id}.json");

            if (string.IsNullOrEmpty(jChapter))
                break;

            chapters.Add(JsonSerializer.Deserialize<Chapter>(jChapter, JsonHelper.Options));
            id++;
        }
        while (true);

        //debug: start from. Comment for default start
        CurrentChapter = chapters[9];
        LoadNode(1);
        //debug

        if (!Console.IsOutputRedirected)
            DisplayMenu();
    }

    /// <summary>
    /// First screen in game. Comes back to this after completing the story or a section
    /// </summary>
    public void DisplayMenu()
    {
        Clear();

        ForegroundColor = ConsoleColor.Green;
        WriteLine("-------------------------------------------------------------");
        WriteLine();
        WriteLine("            ██╗  ██╗██████╗ ██╗███████╗███████╗              ");
        WriteLine("            ██║ ██╔╝██╔══██╗██║██╔════╝██╔════╝              ");
        WriteLine("            █████╔╝ ██████╔╝██║███████╗███████╗              ");
        WriteLine("            ██╔═██╗ ██╔══██╗██║╚════██║╚════██║              ");
        WriteLine("            ██║  ██╗██║  ██║██║███████║███████║              ");
        WriteLine("            ╚═╝  ╚═╝╚═╝  ╚═╝╚═╝╚══════╝╚══════╝              ");
        WriteLine("     ██╗ ██████╗ ██╗   ██╗██████╗ ███╗   ██╗███████╗██╗   ██╗");
        WriteLine("     ██║██╔═══██╗██║   ██║██╔══██╗████╗  ██║██╔════╝╚██╗ ██╔╝");
        WriteLine("     ██║██║   ██║██║   ██║██████╔╝██╔██╗ ██║█████╗   ╚████╔╝ ");
        WriteLine("██   ██║██║   ██║██║   ██║██╔══██╗██║╚██╗██║██╔══╝    ╚██╔╝  ");
        WriteLine("╚█████╔╝╚██████╔╝╚██████╔╝██║  ██║██║ ╚████║███████╗   ██║   ");
        WriteLine(" ╚════╝  ╚═════╝  ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═══╝╚══════╝   ╚═╝   ");
        WriteLine();
        WriteLine("              />_________________________________");
        WriteLine("     [########[]_________________________________>");
        WriteLine("              \\>");
        WriteLine();
        WriteLine("-------------------------------------------------------------");

        ForegroundColor = ConsoleColor.DarkCyan;
        WriteLine();

        int chapterId = 1;

        if (statusManager.HasVisitedNodes())
        {
            WriteLine("Welcome back, traveler. This is your journey so far.");
            WriteLine("This game still features autosave, at least for now.");
            WriteLine("Type a number and press enter to select a chapter.");
            WriteLine();

            int lastChapter = statusManager.GetLastChapterId();

            for (int i = 0; i < lastChapter; i++)
                WriteLine(i + 1 + ". " + chapters[i].Title);

            WriteLine();

            bool isValid = false;

            do
            {
                if (int.TryParse(ReadLine(), out int digit))
                    if (isValid = digit <= lastChapter)
                        chapterId = digit;
            }
            while (!isValid);
        }
        else
        {
            WriteLine("Welcome traveler. Your journey is yet to be started.");
            WriteLine("This game features autosave. You just won't know when.");
            WriteLine();
            WriteLine("Press any key.");
            ReadKey(true);
        }

        // load first node of selected chapter
        StartChapter(chapterId);
    }

    public void StartNextChapter()
    {
        StartChapter((CurrentChapter?.Id ?? 0) + 1);
    }

    /// <summary>
    /// Find next node and uses node factory to build the proper type
    /// </summary>
    /// <param name="nodeId"></param>
    public void LoadNode(int? nodeId)
    {
        if (nodeId.HasValue)
        {
            ArgumentNullException.ThrowIfNull(CurrentChapter, "No chapter is loaded.");

            CurrentNode = CurrentChapter.Nodes.Find(n => n.Id == nodeId);

            ArgumentNullException.ThrowIfNull(CurrentNode, $"Node with ID {nodeId} not found in the current chapter.");

            CurrentNode.SetGameEngine(this);
            CurrentNode.IsVisited = IsNodeVisited(CurrentNode.Id);

            CurrentNode.Load();
        }
        else
            throw new Exception("Id was null and/or node wasn't the last in the chapter!");
    }

    /// <summary>
    /// Marks nodes as done, and if they are last of chapter, also chapter as done
    /// </summary>
    public void SaveProgress(int nodeId)
    {
        ArgumentNullException.ThrowIfNull(CurrentChapter, "Cannot save progress: no chapter is loaded.");

        statusManager.SaveProgress(CurrentChapter.Id, nodeId);
    }

    /// <summary>
    /// Check if selected node was visited in the past
    /// </summary>
    /// <param name="nodeId"></param>
    /// <returns></returns>
    public bool IsNodeVisited(int nodeId)
    {
        ArgumentNullException.ThrowIfNull(CurrentChapter, "Cannot check if node was visited: no chapter is loaded.");

        return statusManager.IsNodeVisited(CurrentChapter.Id, nodeId);
    }

    /// <summary>
    /// Decides upon the condition of a choice, action, object etc
    /// </summary>
    /// <param name="condition"></param>
    /// <returns></returns>
    public bool Evaluate(Condition condition)
    {
        if (condition is null || string.IsNullOrEmpty(condition.Item))
            return true; // no condition, or empty item means always true

        return condition.Type switch
        {
            "isNodeVisited" => IsNodeVisited(Convert.ToInt32(condition.Item)),
            _ => statusManager.IsItemInInventory(condition.Item),
        };
    }

    /// <summary>
    /// You picked something up
    /// </summary>
    /// <param name="effect"></param>
    public void AddItemToInventory(Effect effect)
    {
        statusManager.AddItemToInventory(effect.GainItem);
    }

    /// <summary>
    /// Starts the n-th chapter at the beginning (first node)
    /// </summary>
    /// <param name="chapterId" the chapter number></param>
    /// <returns></returns>
    void StartChapter(int chapterId)
    {
        CurrentChapter = chapters.Find(c => c.Id == chapterId);

        ArgumentNullException.ThrowIfNull(CurrentChapter, $"Chapter with ID {chapterId} not found.");

        LoadNode(nodeId: 1);
    }

    /// <summary>
    /// Extracts resources (chapters) from the compiled DLL
    /// </summary>
    /// <param name="resourceName" name-path of the resource></param>
    /// <returns></returns>
    static string LoadResource(string resourceName)
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            using StreamReader reader = new(stream);
            return reader.ReadToEnd();
        }
        return string.Empty;
    }

    /// <summary>
    /// Dump any unhandled exception to txt file
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    void LogError(object sender, UnhandledExceptionEventArgs e)
    {
        string path = Path.Combine(AppContext.BaseDirectory, $"errorLog.txt");

        System.Text.StringBuilder sb = new();
        sb.AppendLine(e.ExceptionObject.ToString());

        if (CurrentChapter != null)
            sb.AppendLine("Chapter: " + CurrentChapter.Id);

        if (CurrentNode != null)
            sb.AppendLine("Node: " + CurrentNode.Id);

        File.WriteAllText(path, sb.ToString());
    }
}
