// Shared interface
using System;
namespace CosmicShore.DialogueSystem.Runtime.Models
{
    public interface IDialogueView
    {
        void ShowDialogueSet(DialogueSet set);
        void ShowLine(DialogueSet set, DialogueLine line, Action onLineComplete);
        void Hide(Action onHidden);
    }
}
