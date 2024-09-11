using UnityEngine;
using UnityEngine.Video;

namespace CosmicShore
{
    [System.Serializable]
    public class SO_Game : ScriptableObject
    {
        public GameModes Mode;
        public string DisplayName;
        public string Description;
        public Sprite Icon;
        public Sprite SelectedIcon;
        public Sprite CardBackground;
        public VideoPlayer PreviewClip;
        public bool GolfScoring;
        public string SceneName;
    }
}
