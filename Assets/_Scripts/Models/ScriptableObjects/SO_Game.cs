using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Video;

namespace CosmicShore
{
    [System.Serializable]
    public class SO_Game : ScriptableObject
    {
        public GameModes Mode;
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