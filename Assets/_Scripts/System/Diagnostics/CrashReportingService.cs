using System;
using UnityEngine;
using UnityEngine.CrashReportHandler;

namespace CosmicShore.Core
{
    /// <summary>
    /// Consent-gated crash reporting (Unity Cloud Diagnostics).
    ///
    /// Privacy posture: capture is forced OFF before the first scene loads and only turns on when
    /// the player has actively granted analytics consent. Unity's default is capture-on, so the
    /// <see cref="Disarm"/> hook below is load-bearing - without it a player who never answered the
    /// consent prompt (or answered "no") would still have crash payloads uploaded.
    ///
    /// Single writer: <see cref="AnalyticsServiceFacade"/> owns the consent decision and calls
    /// <see cref="ApplyConsent"/>. Nothing else should touch <see cref="CrashReportHandler"/>.
    ///
    /// The service itself is enabled per-project in Project Settings > Services > Cloud Diagnostics
    /// (ProjectSettings: <c>enableCrashReportAPI</c> + <c>CrashReportingSettings.m_Enabled</c>).
    /// With the service disabled these calls are harmless no-ops, so this code is safe to ship
    /// either way.
    /// </summary>
    public static class CrashReportingService
    {
        static bool _metadataStamped;

        /// <summary>True when crash capture is currently armed. Read-only mirror for diagnostics UI.</summary>
        public static bool IsCapturing { get; private set; }

        /// <summary>
        /// Forces capture off before any scene loads, so the window between process start and the
        /// consent decision is never collected. Consent (if already stored) re-arms it moments later
        /// when <see cref="AnalyticsServiceFacade"/> initialises.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Disarm()
        {
            try
            {
                CrashReportHandler.enableCaptureExceptions = false;
                IsCapturing = false;
                // Re-stamp metadata each play session — the once-latch otherwise survives a
                // domain-reload-free play exit and StampMetadataOnce never re-runs.
                _metadataStamped = false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CrashReporting] Could not disarm capture: {ex.Message}");
            }
        }

        /// <summary>
        /// Arms or disarms crash capture. Called by <see cref="AnalyticsServiceFacade"/> whenever the
        /// consent decision changes and once at startup with the stored decision.
        /// </summary>
        public static void ApplyConsent(bool granted)
        {
            try
            {
                CrashReportHandler.enableCaptureExceptions = granted;
                IsCapturing = granted;

                if (granted) StampMetadataOnce();

                Debug.Log($"[CrashReporting] Capture {(granted ? "ENABLED" : "disabled")} by consent.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CrashReporting] Could not apply consent: {ex.Message}");
            }
        }

        /// <summary>
        /// Attaches the context that makes a crash report actionable: which build, which platform,
        /// which graphics device. Deliberately excludes anything that identifies a person - the
        /// player id is NOT attached, so a crash report cannot be joined back to an individual.
        /// </summary>
        static void StampMetadataOnce()
        {
            if (_metadataStamped) return;
            _metadataStamped = true;

            try
            {
                CrashReportHandler.SetUserMetadata("build_version", Application.version);
                CrashReportHandler.SetUserMetadata("unity_version", Application.unityVersion);
                CrashReportHandler.SetUserMetadata("platform", Application.platform.ToString());
                CrashReportHandler.SetUserMetadata("gpu", SystemInfo.graphicsDeviceName);
                CrashReportHandler.SetUserMetadata("gpu_api", SystemInfo.graphicsDeviceType.ToString());
                CrashReportHandler.SetUserMetadata("cpu_threads", SystemInfo.processorCount.ToString());
                CrashReportHandler.SetUserMetadata("system_memory_mb", SystemInfo.systemMemorySize.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CrashReporting] Could not stamp metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds the current game mode so a crash report says which mode was running. Safe to call
        /// often; ignored while capture is disarmed.
        /// </summary>
        public static void SetGameModeContext(string gameMode)
        {
            if (!IsCapturing || string.IsNullOrEmpty(gameMode)) return;

            try { CrashReportHandler.SetUserMetadata("game_mode", gameMode); }
            catch (Exception ex) { Debug.LogWarning($"[CrashReporting] metadata: {ex.Message}"); }
        }
    }
}
