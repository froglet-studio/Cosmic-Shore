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
        [FormerlySerializedAs("SelectedIcon")]
        public Sprite IconActive;
        [FormerlySerializedAs("Icon")]
        public Sprite IconInactive;
        public Sprite CardBackground;
        public bool GolfScoring;
        public string SceneName;
    }
}