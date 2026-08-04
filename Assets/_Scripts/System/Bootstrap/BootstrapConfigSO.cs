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

        [Header("Shader Warmup")]
        [SerializeField, Tooltip("Shader variant collections compiled during the splash screen so first-use " +
             "effects (crystal pickups, explosions, trails) don't hitch on in-game shader compilation. " +
             "Record via Project Settings > Graphics > Shader Loading > 'Save to asset...' after a " +
             "representative play session in the editor.")]
        ShaderVariantCollection[] _shaderWarmupCollections;

        [Header("Debug")]
        [SerializeField, Tooltip("Log detailed bootstrap timing to the console.")]
        bool _verboseLogging;

        public float ServiceInitTimeoutSeconds => _serviceInitTimeoutSeconds;
        public float MinimumSplashDuration => _minimumSplashDuration;
        public int TargetFrameRate => _targetFrameRate;
        public bool PreventScreenSleep => _preventScreenSleep;
        public int VSyncCount => _vSyncCount;
        public ShaderVariantCollection[] ShaderWarmupCollections => _shaderWarmupCollections;
        public bool VerboseLogging => _verboseLogging;
    }
}
