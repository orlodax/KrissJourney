using System;
using System.Text.Json.Serialization;
using KrissJourney.Kriss.Helpers;
using KrissJourney.Kriss.Services;

namespace KrissJourney.Kriss.Nodes;

[JsonConverter(typeof(NodeJsonConverter))]
public abstract class NodeBase
{
    public int Id { get; set; } // unique id primary key
    public string Recap { get; set; } // just to sum it up
    public string Type { get; set; } // story, choice, action...
    public string Text { get; set; } // text to be flown
    public string AltText { get; set; } // other text to be flown/displayed (i.e. if the node is already visited)
    public int ChildId { get; set; } // possible id (if single-next)
    public bool IsVisited { get; set; } // have we ever played this node?
    public bool IsLast { get; set; } // more than one node per chapter could be its last
    public bool IsClosing { get; set; } // close game or return to menu if last node of last chapter/section

    public abstract void Load();

    protected GameEngine GameEngine { get; private set; } // reference to the game engine instance

    public void SetGameEngine(GameEngine gameEngine)
    {
        GameEngine = gameEngine;
    }

    protected void Init()
    {
        // start text
        Clear();
        ForegroundColor = ConsoleColor.DarkCyan; // narrator, default color

        if (IsVisited && AltText != null)
            Typist.RenderText(!IsVisited, AltText);
        else
            Typist.RenderText(!IsVisited, Text);
    }

    protected void AdvanceToNext(int childId)
    {
        // mark caller as visited
        GameEngine.SaveProgress(Id);

        // if it closes story or section, go back to menu
        if (IsClosing)
            GameEngine.DisplayMenu();

        // if it closes chapter load the next chapter, else load next node
        if (IsLast)
            GameEngine.StartNextChapter();

        GameEngine.LoadNode(childId);
    }

    /// <summary>
    /// Check if the current node is the one with given chapter and node id.
    /// </summary>
    protected bool IsThisNode(int chapterId, int nodeId) => GameEngine.CurrentChapter?.Id == chapterId && GameEngine.CurrentNode?.Id == nodeId;

    protected void RedrawNode()
    {
        Clear();

        Typist.InstantText(Text);

        WriteLine();
        WriteLine();
        WriteLine();
    }
}