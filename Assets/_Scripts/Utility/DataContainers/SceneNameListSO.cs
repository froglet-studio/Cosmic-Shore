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

        [SerializeField, Tooltip("Offline WebGL main-menu scene, loaded straight from Bootstrap when " +
            "OfflineMenuShell is active (no auth/Relay). Typically 'Menu_Main_WebGL'.")]
        string _mainMenuWebGLScene = "Menu_Main_WebGL";

        [Header("Gameplay Scenes")]
        [SerializeField, Tooltip("Multiplayer gameplay scene.")]
        string _multiplayerScene = "MinigameFreestyleMultiplayer_Gameplay";

        public string BootstrapScene => _bootstrapScene;
        public string AuthenticationScene => _authenticationScene;
        public string MainMenuScene => _mainMenuScene;
        public string MainMenuWebGLScene => _mainMenuWebGLScene;
        public string MultiplayerScene => _multiplayerScene;
    }
}
