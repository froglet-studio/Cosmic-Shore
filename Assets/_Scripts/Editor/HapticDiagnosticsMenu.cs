using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Diagnostic menu items for haptics. Use these when the device is buzzing nothing
    /// — Test Haptic Now fires a known-good preset and the Console dumps the full
    /// gating state (settings, platform, Lofelt init, device capabilities).
    /// </summary>
    public static class HapticDiagnosticsMenu
    {
        [MenuItem("Tools/Cosmic Shore/Test Haptic (Play Mode)", false, 110)]
        static void TestHapticNow()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[HapticDiagnostics] Enter Play Mode first — Lofelt's runtime is only active during play.");
                return;
            }
            HapticController.ForceTestPlay();
        }

        [MenuItem("Tools/Cosmic Shore/Dump Haptic Diagnostics", false, 111)]
        static void DumpDiagnostics()
        {
            HapticController.LogDiagnostics();
        }
    }
}
