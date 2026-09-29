using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Wired to the Settings panel's "Run Benchmark" button. Loads the benchmark stress-test scene
    /// through the SAME launch path every game uses (set <see cref="GameDataSO"/> → raise
    /// <c>OnLaunchGame</c> → <c>SceneLoader.LaunchGame</c>), so the always-on Relay host loads it
    /// correctly via Netcode scene management and nothing special-cases it.
    ///
    /// The scene runs as a single-player sandbox (<see cref="SandboxBenchmarkController"/> +
    /// <c>MiniGamePlayerSpawnerAdapter</c>) - the host loads it, the Relay simply idles.
    /// </summary>
    public class BenchmarkSceneLauncher : MonoBehaviour
    {
        /// <summary>Must match the committed benchmark scene's file name (there is exactly one; it is never re-created).</summary>
        public const string BenchmarkSceneName = "BenchmarkStressTest";

        [Inject] GameDataSO gameData;

        [SerializeField, Min(1), Tooltip("AI skill / spawn intensity. Higher = harder AI and denser spawns.")]
        int intensity = 2;

        /// <summary>Hook this to the Settings → Benchmark button's onClick.</summary>
        public void LaunchBenchmark()
        {
            if (gameData == null)
            {
                Debug.LogError("[BenchmarkSceneLauncher] GameDataSO not injected - needs a ContainerScope in the scene.");
                return;
            }

            int aiCount = DisplayGraphicsSettings.Instance != null
                ? DisplayGraphicsSettings.Instance.Current.AiCrowdSize
                : 3;

            gameData.SceneName = BenchmarkSceneName;
            gameData.GameMode = GameModes.WildlifeBlitz; // single-player ecosystem mode → AI seeks crystals
            gameData.IsMultiplayerMode = false;
            gameData.selectedVesselClass.Value = VesselClassType.Squirrel;
            gameData.SelectedIntensity.Value = intensity;

            // Drives AI backfill where supported; the cloned single-player scene also carries a fixed
            // AI list in its spawner. 1 human + aiCount AI.
            gameData.ConfigurePlayerCounts(1 + Mathf.Max(0, aiCount), 1);

            gameData.InvokeGameLaunch();
        }
    }
}
