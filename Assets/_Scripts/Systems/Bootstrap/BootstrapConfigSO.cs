using UnityEngine;

namespace CosmicShore.Systems.Bootstrap
{
    [CreateAssetMenu(
        fileName = "BootstrapConfig",
        menuName = "ScriptableObjects/Core/BootstrapConfig")]
    public class BootstrapConfigSO : ScriptableObject
    {
        [Header("Scene Flow")]
        [SerializeField, Tooltip("Scene to load after bootstrap completes. Typically the Authentication scene.")]
        string _firstSceneName = "Authentication";

        [SerializeField, Tooltip("Scene to load after authentication succeeds.")]
        string _mainMenuSceneName = "Menu_Main";

        [Header("Timeouts")]
        [SerializeField, Tooltip("Max seconds to wait for all services to initialize before giving up.")]
        float _serviceInitTimeoutSeconds = 15f;

        [SerializeField, Tooltip("Minimum seconds to show the splash/loading screen.")]
        float _minimumSplashDuration = 1f;

        [Header("Platform Settings")]
        [SerializeField, Tooltip("Target framerate. 0 = platform default.")]
        int _targetFrameRate = 60;

        [SerializeField, Tooltip("Prevent the screen from dimming during gameplay.")]
        bool _preventScreenSleep = true;

        [SerializeField, Tooltip("VSync count. 0 = off, 1 = every VBlank, 2 = every other VBlank.")]
        int _vSyncCount = 0;

        [Header("Debug")]
        [SerializeField, Tooltip("Log detailed bootstrap timing to the console.")]
        bool _verboseLogging;

        public string FirstSceneName => _firstSceneName;
        public string MainMenuSceneName => _mainMenuSceneName;
        public float ServiceInitTimeoutSeconds => _serviceInitTimeoutSeconds;
        public float MinimumSplashDuration => _minimumSplashDuration;
        public int TargetFrameRate => _targetFrameRate;
        public bool PreventScreenSleep => _preventScreenSleep;
        public int VSyncCount => _vSyncCount;
        public bool VerboseLogging => _verboseLogging;
    }
}
