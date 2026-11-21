using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Drop this component on any vessel that has both AIPilot AND should use a
    /// trained genome from the archive instead of inspector parameters.
    ///
    /// At Start the bridge looks up the archive for this seat. Intensity 4 is the
    /// stored genome. Intensities 1–3 dither that genome unless
    /// useDitheringForLowerIntensities is off, in which case the exact intensity
    /// entry is flown raw. AIPilot is stopped and disabled before TrainingPilot
    /// starts, and a seat that already has a TrainingPilot is left alone.
    ///
    /// This is the prefab deployment path. The session service does the same
    /// hand-off for ordinary AI seats. Both share TrainingPilot.
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

        TrainingPilot _pilot;

        void Start()
        {
            // The session runner owns genomes while a GA match is in progress.
            // An archive bridge on the vessel prefab would install a second pilot.
            if (gameData != null && gameData.IsTraining) return;
            if (archive == null) return;
            // The deployment service may already have taken this seat.
            if (GetComponent<TrainingPilot>() != null) return;

            int intensity = forceIntensity > 0
                ? forceIntensity
                : (gameData?.SelectedIntensity != null ? Mathf.Clamp(gameData.SelectedIntensity.Value, 1, 4) : 4);

            VesselClassType live = lookupVessel != VesselClassType.Any
                ? lookupVessel
                : ResolveVesselFromContext();
            GameModes mode = lookupMode != GameModes.Random
                ? lookupMode
                : (gameData != null ? gameData.GameMode : GameModes.Random);

            // useDitheringForLowerIntensities false is the prefab opt-out: fly the
            // genome stored for this intensity, raw. True loads intensity 4 and dithers.
            bool useStored = !useDitheringForLowerIntensities;
            var genome = ArchiveDeployment.ResolveGenome(archive, mode, live, intensity, useStored);
            if (genome == null)
            {
                int lookup = useStored ? intensity : ArchiveDeployment.TrainedIntensity;
                Debug.LogWarning($"[Deploy] No archive entry for " +
                                 $"{ArchiveDeployment.ResolveVessel(mode, live)}/{mode}/I{lookup} on {name}. " +
                                 "AIPilot stays in control.");
                return;
            }

            if (!replaceLegacyAI)
            {
                CSDebug.LogVerbose(CSLogChannel.AITraining,
                    $"[Deploy] Dry-run only (replaceLegacyAI=false): " +
                    $"{ArchiveDeployment.ResolveVessel(mode, live)}/{mode}/play I{intensity}");
                return;
            }

            var vesselComp = GetComponent<IVessel>() ?? GetComponentInParent<IVessel>();
            ArchiveDeployment.Install(gameObject, vesselComp, gameData, cellData, genome,
                ArchiveDeployment.FlownIntensity(intensity, useStored));
            _pilot = GetComponent<TrainingPilot>();
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
