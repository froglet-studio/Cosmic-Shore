using UnityEngine;

namespace CosmicShore.Utility
{
    [CreateAssetMenu(fileName = "SceneNameListSO", menuName = "ScriptableObjects/SceneNameListSO")]
    public class SceneNameListSO : ScriptableObject
    {
        [Header("Core Flow Scenes")]
        [SerializeField, Tooltip("Bootstrap scene (build index 0). Typically 'Bootstrap'.")]
        string _bootstrapScene = "Bootstrap";

        [SerializeField, Tooltip("Scene to load after bootstrap completes. Typically 'Authentication'.")]
        string _authenticationScene = "Authentication";

        [SerializeField, Tooltip("Main menu scene loaded after authentication. Typically 'Menu_Main'.")]
        string _mainMenuScene = "Menu_Main";

        // Gameplay scenes are launched by name from each SO_ArcadeGame card's SceneName -
        // there is no single generic "multiplayer scene" (the retired MultiplayerScene
        // member defaulted to the deleted standalone freestyle scene and had no runtime
        // consumers).

        public string BootstrapScene => _bootstrapScene;
        public string AuthenticationScene => _authenticationScene;
        public string MainMenuScene => _mainMenuScene;
    }
}
