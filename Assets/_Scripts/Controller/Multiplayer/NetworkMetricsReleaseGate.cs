using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Switchback perf pass (2026-11-02): NGO's NetworkMetrics.DispatchFrame cost ~1.7 KB of GC per frame because
    /// com.unity.multiplayer.tools is installed, so MULTIPLAYER_TOOLS is defined in every build and message metrics
    /// are hooked by default. Outside the editor and development builds nobody reads them, so turn message and
    /// profiling metrics off on each NetworkManager as it is created (before StartHost/StartClient hooks them).
    /// Editor and dev builds are unchanged so the Network Profiler / RNSM keep working.
    /// </summary>
    static class NetworkMetricsReleaseGate
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (Debug.isDebugBuild) return;
            NetworkManager.OnInstantiated -= Disable;
            NetworkManager.OnInstantiated += Disable;
            if (NetworkManager.Singleton) Disable(NetworkManager.Singleton);
        }

        static void Disable(NetworkManager nm)
        {
            var cfg = nm ? nm.NetworkConfig : null;
            if (cfg == null) return;
            cfg.NetworkMessageMetrics = false; // exists because com.unity.multiplayer.tools is in the manifest
            cfg.NetworkProfilingMetrics = false;
        }
    }
}
