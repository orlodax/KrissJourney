using System;
using System.Collections.Generic;
using KrissJourney.Kriss.Helpers;
using KrissJourney.Kriss.Models;

namespace KrissJourney.Kriss.Nodes;

public class DialogueNode : NodeBase
{
    ConsoleKeyInfo key;
    int selectedRow = 0;

    // all the lines (thus paths) of this node's dialogues
    public List<DialogueLine> Dialogues { get; set; }

    public override void Load()
    {
        Clear();
        RecursiveDialogues(0, isLineFlowing: true);
    }

    // lineid iterates over elements of dialogues[] 
    void RecursiveDialogues(int lineId, bool isLineFlowing)
    {
        if (IsVisited)
            isLineFlowing = false;

        // if we are at the beginning of the dialogue, render the initial text
        if (lineId == 0)
        {
            ForegroundColor = ConsoleColor.DarkCyan; // narrator, default color

            if (IsVisited && AltText != null)
                Typist.RenderText(isLineFlowing, AltText);
            else
                Typist.RenderText(isLineFlowing, Text);
        }

        // current line object selected in the iteration
        DialogueLine currentLine = Dialogues[lineId];

        if (currentLine.PreComment != null)
        {
            Typist.RenderNonSpeechPart(isLineFlowing, currentLine.PreComment);

            WriteLine();
            WriteLine();
        }

        if (currentLine.Line != null)
            Typist.RenderLine(isLineFlowing, currentLine);

        if (currentLine.Comment != null)
            Typist.RenderNonSpeechPart(isLineFlowing, currentLine.Comment);

        WriteLine();
        WriteLine();

        // pause if it's marked as break or if it's last line of chapter
        if (currentLine.Break || IsLast && Dialogues.Count == Dialogues.IndexOf(currentLine) + 1)
        {
            Typist.WaitForKey(2);
            Clear();
        }

        // if it encounters a link, jump to the node
        if (currentLine.ChildId.HasValue)
        {
            Typist.WaitForKey(2);
            AdvanceToNext(currentLine.ChildId.Value);
        }

        // if there are replies available, display choice
        if (currentLine.Replies != null && currentLine.Replies.Count != 0)
        {
            // selectedRow is node-level, and a later block can be shorter than the one that last
            // moved the highlight (c21 node 4 offers 3 options, then 2): without this clamp the
            // highlight lands on a row nobody draws and Enter indexes past the list. Clamped, not
            // reset, so the row the player last stood on still means something. 2026-09-05
            if (selectedRow > currentLine.Replies.Count - 1)
                selectedRow = currentLine.Replies.Count - 1;

            for (int i = 0; i < Dialogues[lineId].Replies.Count; i++)
            {
                // set both colors explicitly on every row: the last rendered speech part leaves
                // the actor's color behind, and an unselected reply must read as narrator text
                ForegroundColor = i == selectedRow
                    ? ConsoleColor.White
                    : ConsoleColor.DarkCyan;
                BackgroundColor = i == selectedRow
                    ? ConsoleColor.DarkCyan
                    : ConsoleColor.Black;

                Write("\t");
                Write(i + 1 + ". " + Dialogues[lineId].Replies[i].Line);

                ResetColor();
                ForegroundColor = ConsoleColor.DarkCyan;
                WriteLine();
                CursorLeft = WindowLeft;
            }

            key = ReadKey(true);

            // on selection, either advance to the next node specified in the reply, or jump to the next line
            if (key.Key == ConsoleKey.Enter)
            {
                Clear();

                if (currentLine.Replies[selectedRow].ChildId.HasValue)
                    AdvanceToNext(currentLine.Replies[selectedRow].ChildId.Value);
                else
                    RecursiveDialogues(
                        Dialogues.FindIndex(l => l.LineName == currentLine.Replies[selectedRow].NextLine),
                        isLineFlowing: true);
            }

            if ((key.Key == ConsoleKey.UpArrow || key.Key == ConsoleKey.LeftArrow) && selectedRow > 0)
                selectedRow--;
            if ((key.Key == ConsoleKey.DownArrow || key.Key == ConsoleKey.RightArrow) && selectedRow < Dialogues[lineId].Replies.Count - 1)
                selectedRow++;

            // rewind to the start of the current beat. A break ends a beat, and so does an
            // earlier line's own reply prompt: redraw past one and it asks its question again,
            // eating the keypress meant for this one, which makes adjacent reply blocks unusable.
            int redrawFrom = lineId;
            while (redrawFrom > 0)
            {
                redrawFrom--;

                if (Dialogues[redrawFrom].Break || Dialogues[redrawFrom].Replies is { Count: > 0 })
                {
                    redrawFrom++;
                    break;
                }
            }

            // a break on the very first line has already been answered on the way in, so a
            // redraw from the top would ask for that key a second time
            if (redrawFrom == 0 && Dialogues[0].Break)
                redrawFrom = 1;

            // redraw the node to allow the selection effect
            Clear();
            RecursiveDialogues(redrawFrom, isLineFlowing: false);
        }
        else // if there are no replies available, either continue to the next line or jump to the line specified in the current line
        {
            if (!string.IsNullOrWhiteSpace(currentLine.NextLine))
            {
                int nextLineId = Dialogues.FindIndex(l => l.LineName == currentLine.NextLine);
                RecursiveDialogues(nextLineId, isLineFlowing);
            }
            else if (Dialogues.Count > lineId + 1)
                RecursiveDialogues(lineId + 1, isLineFlowing);

            AdvanceToNext(ChildId);
        }
    }
}