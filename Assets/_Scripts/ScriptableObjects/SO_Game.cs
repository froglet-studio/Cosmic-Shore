using UnityEngine;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using System;
namespace CosmicShore.ScriptableObjects
{
    [System.Serializable]
    public class SO_Game : ScriptableObject
    {
        public GameModes Mode;
        public bool IsMultiplayer;
        public string DisplayName;
        public string Description;
        [Tooltip("One short line for the game card's reveal panel (under 48 characters). Empty shows nothing. " +
                 "Written for the arcade cards by Tools/Build/author_card_short_lines.py from card_short_lines.json.")]
        public string ShortDescription;
        [FormerlySerializedAs("SelectedIcon")]
        public Sprite IconActive;
        [FormerlySerializedAs("Icon")]
        public Sprite IconInactive;
        public Sprite CardBackground;
        public bool GolfScoring;
        public string SceneName;
    }
}