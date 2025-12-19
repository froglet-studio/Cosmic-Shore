using System;
using UnityEngine;

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
                Debug.LogWarning("DialogueEventChannel: Raised with empty setId.");
                return;
            }

            OnDialogueRequested?.Invoke(setId);
        }
    }
}