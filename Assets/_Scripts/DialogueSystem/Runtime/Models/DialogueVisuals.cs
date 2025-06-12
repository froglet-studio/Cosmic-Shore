using CosmicShore.DialogueSystem.Models;
using UnityEngine;

namespace CosmicShore.DialogueSystem.Editor
{
    public static class DialogueVisuals
    {
        public static Color GetColorForSpeaker(DialogueSpeaker speaker)
        {
            return speaker switch
            {
                DialogueSpeaker.Speaker1 => new Color(1f, 0.92f, 0.3f),  // Yellow
                DialogueSpeaker.Speaker2 => new Color(0.3f, 1f, 0.5f),   // Green
                _ => Color.gray
            };
        }

        public static Color GetModeColor(DialogueModeType mode)
        {
            return mode switch
            {
                DialogueModeType.Monologue => new Color(0.3f, 0.6f, 1f),  // Blue
                DialogueModeType.Dialogue => new Color(0.8f, 0.5f, 0.2f), // Orange/neutral
                _ => Color.gray
            };
        }
    }
}
