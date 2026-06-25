using UnityEngine;

namespace CosmicShore.Core
{
    [CreateAssetMenu(
        fileName = "BootstrapConfig",
        menuName = "ScriptableObjects/Core/BootstrapConfig")]
    public class BootstrapConfigSO : ScriptableObject
    {
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

        [Header("WebGL / Offline")]
        [SerializeField, Tooltip("Boot straight into the offline main-menu shell: no auth, no Relay, " +
            "no party/presence, no analytics. WebGL is always offline regardless of this flag because it " +
            "cannot be a Netcode host. Toggle on to force the offline shell in the Editor for testing.")]
        bool _offlineMenuShell;

        public float ServiceInitTimeoutSeconds => _serviceInitTimeoutSeconds;
        public float MinimumSplashDuration => _minimumSplashDuration;
        public int TargetFrameRate => _targetFrameRate;
        public bool PreventScreenSleep => _preventScreenSleep;
        public int VSyncCount => _vSyncCount;
        public bool VerboseLogging => _verboseLogging;

        /// <summary>
        /// True when the app should boot the offline main-menu shell (no auth/Relay/party/analytics).
        /// Always true on WebGL — it cannot host Netcode — regardless of the serialized flag.
        /// </summary>
        public bool OfflineMenuShell => _offlineMenuShell || Application.platform == RuntimePlatform.WebGLPlayer;
    }
}
