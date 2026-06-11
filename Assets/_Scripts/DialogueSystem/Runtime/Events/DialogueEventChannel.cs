using System;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.DialogueSystem.Events
{
    [CreateAssetMenu(
        fileName = "DialogueEventChannel",
        menuName = "CosmicShore/Dialogue/Dialogue Event Channel")]
    public class DialogueEventChannel : ScriptableObject
    {
        public event Action<string> OnDialogueRequested;

        public void Raise(string setId)
        {
            if (string.IsNullOrEmpty(setId))
            {
                CSDebug.LogWarning("DialogueEventChannel: Raised with empty setId.");
                return;
            }

            OnDialogueRequested?.Invoke(setId);
        }
    }
}