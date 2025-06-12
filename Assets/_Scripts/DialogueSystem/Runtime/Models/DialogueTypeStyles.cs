using UnityEngine;
using UnityEngine.UIElements;
using CosmicShore.DialogueSystem.Models;

namespace CosmicShore.DialogueSystem.Editor
{
    public static class DialogueTypeStyles
    {
        public static Color GetColor(DialogueType type)
        {
            return type switch
            {
                DialogueType.CaptainIntroduction => new Color(0.3f, 0.7f, 1f),   // Cyan
                DialogueType.EventIntroduction => new Color(0.8f, 0.5f, 0.2f), // Orange
                DialogueType.NarrativeMoment => new Color(1f, 0.9f, 0.3f),   // Yellow
                DialogueType.CombatWarning => new Color(1f, 0.3f, 0.3f),   // Red
                DialogueType.AmbientLore => new Color(0.5f, 1f, 0.5f),   // Green
                DialogueType.SystemMessage => new Color(0.7f, 0.7f, 0.7f), // Gray
                _ => Color.white
            };
        }
    }
}
