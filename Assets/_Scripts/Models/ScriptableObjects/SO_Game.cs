using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;
using CosmicShore.Models.Enums;
using CosmicShore.Game.Ship;
namespace CosmicShore.Models.ScriptableObjects
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
        public VideoPlayer PreviewClip;
        public bool GolfScoring;
        public string SceneName;
    }
}