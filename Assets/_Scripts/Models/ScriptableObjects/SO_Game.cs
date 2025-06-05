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

        [Tooltip("In multiplayer mode, the maximum number of players allowed to play this game. This is only applicable if IsMultiplayer is true.")]
        public int MaxPlayersForMultiplayer = 0;

        // TODO: Refactor later to support multiple multiplayer game modes. We can add a bool isMultiplayer paramter to SO_Game later if needed.
        public static bool IsMultiplayerModes(GameModes gameMode)
        {
            return gameMode == GameModes.MultiplayerFreestyle || 
                gameMode == GameModes.MultiplayerCellularDuel;
        }
    }
}