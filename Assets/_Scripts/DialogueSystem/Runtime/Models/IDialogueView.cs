// Shared interface
using System;
using CosmicShore.DialogueSystem.Models;

public interface IDialogueView
{
    void ShowDialogueSet(DialogueSet set);
    void ShowLine(DialogueSet set, DialogueLine line, Action onLineComplete);
    void Hide(Action onHidden);
}