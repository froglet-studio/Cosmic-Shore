using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Drop this component on any vessel that has both AIPilot AND should use a
    /// trained genome from the archive instead of inspector parameters.
    ///
    /// At Awake the bridge:
    ///   1. Looks up the best-known genome for (vessel × game mode × intensity).
    ///   2. Disables the legacy AIPilot.
    ///   3. Adds a TrainingPilot, gives it the genome, and binds it to the vessel.
    ///
    /// This is the deployment path: ship a trained TrainingArchiveSO with the
    /// game and the bridge delivers it into live play. Training and deployment
    /// share the same TrainingPilot — what you trained is exactly what runs.
    /// </summary>
    [RequireComponent(typeof(AIPilot))]
    public class TrainingAIDeploymentBridge : MonoBehaviour
    {
        [Header("Archive")]
        [SerializeField] TrainingArchiveSO archive;
        [SerializeField] GameDataSO gameData;
        [SerializeField] CellRuntimeDataSO cellData;

        [Header("Override")]
        [Tooltip("If non-zero, overrides the intensity read from gameData.SelectedIntensity.")]
        [SerializeField, Range(0, 4)] int forceIntensity = 0;
        [Tooltip("Vessel class for archive lookup. Set to Any to read from VesselStatus at runtime.")]
        [SerializeField] VesselClassType lookupVessel = VesselClassType.Any;
        [Tooltip("Game mode for archive lookup. Set to Random to read from gameData at runtime.")]
        [SerializeField] GameModes lookupMode = GameModes.Random;

        [Header("Behavior")]
        [Tooltip("If false, the bridge keeps AIPilot enabled and only logs what the deployment would have done. Useful for A/B testing.")]
        [SerializeField] bool replaceLegacyAI = true;
        [Tooltip("If true, applies the archive's intensity-4 genome with runtime intensity dithering. " +
                 "If false, looks up the explicit genome for the requested intensity (use this when an intensity has its own trained pilot rather than a dither).")]
        [SerializeField] bool useDitheringForLowerIntensities = true;

        AIPilot _aiPilot;
        TrainingPilot _pilot;

        void Awake()
        {
            _aiPilot = GetComponent<AIPilot>();
        }

        void Start()
        {
            if (archive == null) return;

            int intensity = forceIntensity > 0
                ? forceIntensity
                : (gameData?.SelectedIntensity != null ? Mathf.Max(1, gameData.SelectedIntensity.Value) : 4);

            VesselClassType vessel = lookupVessel != VesselClassType.Any
                ? lookupVessel
                : ResolveVesselFromContext();
            GameModes mode = lookupMode != GameModes.Random
                ? lookupMode
                : (gameData != null ? gameData.GameMode : GameModes.Random);

            int lookupIntensity = useDitheringForLowerIntensities ? 4 : intensity;
            var genome = archive.FindBestAvailable(vessel, mode, lookupIntensity, out int matchScore);
            if (genome == null)
            {
                Debug.LogWarning($"[Deploy] No trained genome for {vessel}/{mode}/I{lookupIntensity}; AI will use inspector defaults.");
                return;
            }

            if (matchScore < 4)
                Debug.Log($"[Deploy] No exact archive match for {vessel}/{mode}/I{lookupIntensity}; using best partial match (score {matchScore}).");

            if (!replaceLegacyAI)
            {
                Debug.Log($"[Deploy] Dry-run only (replaceLegacyAI=false): {vessel}/{mode}/I{lookupIntensity}");
                return;
            }

            // Disable legacy AIPilot so we don't fight over inputs.
            _aiPilot.StopAIPilot();
            _aiPilot.enabled = false;

            _pilot = gameObject.GetComponent<TrainingPilot>();
            if (_pilot == null) _pilot = gameObject.AddComponent<TrainingPilot>();

            var vesselComp = GetComponent<IVessel>() ?? GetComponentInParent<IVessel>();
            _pilot.BindVessel(vesselComp, gameData, cellData);
            _pilot.Intensity = intensity;
            _pilot.LoadGenome(genome);
            _pilot.BeginEpisode();
        }

        VesselClassType ResolveVesselFromContext()
        {
            var v = GetComponent<IVessel>() ?? GetComponentInParent<IVessel>();
            if (v?.VesselStatus != null) return v.VesselStatus.VesselType;
            return VesselClassType.Any;
        }

        void OnDestroy()
        {
            if (_pilot != null) _pilot.EndEpisode();
        }
    }
}
