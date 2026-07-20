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
    /// The scene runs on the networked single-host model like every mode
    /// (<see cref="SandboxBenchmarkController"/> + <c>ServerPlayerVesselInitializerWithAI</c>):
    /// the host's Squirrel plus an AI crowd sized by the graphics settings.
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
            gameData.GameMode = GameModes.WildlifeBlitz; // ecosystem mode → AI seeks crystals
            gameData.selectedVesselClass.Value = VesselClassType.Squirrel;
            gameData.SelectedIntensity.Value = intensity;
            gameData.RequestedDomainCount = 3; // deterministic Jade/Ruby/Gold AI spread (don't inherit the last game's)

            // 1 human + aiCount AI Squirrels via ServerPlayerVesselInitializerWithAI backfill.
            gameData.ConfigurePlayerCounts(1 + Mathf.Max(0, aiCount), 1);

            gameData.InvokeGameLaunch();
        }
    }
}
