﻿using System.Collections.Generic;

namespace Lybra;

public class NodeBase
{
    public int Id { get; set; } //unique id primary key
    public string Recap { get; set; } //just to sum it up
    public string Type { get; set; } //story, choice, action...
    public string Text { get; set; } //text to be flown
    public string AltText { get; set; } //other text to be flown/displayed (i.e. if the node is already visited)
    public int ChildId { get; set; } //possible id (if single-next)
    public List<Choice> Choices { get; set; } //list of possible choices
    public List<Action> Actions { get; set; } //list of possible actions
    public List<Dialogue> Dialogues { get; set; } //all the lines (thus paths) of the node's dialogues
    public bool IsVisited { get; set; } //have we ever played this node?
    public bool IsLast { get; set; } //more than one node per chapter could be its last
    public bool IsClosing { get; set; } //close game or return to menu if last node of last chapter/section

    public NodeBase(NodeBase n)
    {
        Id = n.Id;
        Recap = n.Recap;
        Type = n.Type;
        Text = n.Text;
        AltText = n.AltText;
        ChildId = n.ChildId;
        Choices = n.Choices;
        Actions = n.Actions;
        Dialogues = n.Dialogues;
        IsVisited = n.IsVisited;
        IsLast = n.IsLast;
        IsClosing = n.IsClosing;
    }
    public NodeBase()
    {

    }
}