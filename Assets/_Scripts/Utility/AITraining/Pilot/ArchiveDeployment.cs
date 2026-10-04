using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Utility.AITraining
{
    /// <summary>
    /// Ships one trained genome into a normal match. Intensity 4 is that genome
    /// verbatim. Intensities 1–3 are the same genes flown through
    /// <see cref="IntensityDitherer"/> — dropout, steering noise, reaction delay,
    /// ability skip, throttle scale. The archive entry is cloned and never written
    /// back, so a lower tier cannot mutate the stored god-tier genome.
    ///
    /// Opt out with <see cref="TrainingControlSO.UseStoredGenomeForLowerIntensity"/>:
    /// the seat then flies the genome stored for that exact intensity, raw.
    /// A vessel-locked mode keys the archive on the mode's hull. An unlocked mode
    /// keys it on the seat's <see cref="VesselClassType"/>.
    /// </summary>
    public static class ArchiveDeployment
    {
        public const int TrainedIntensity = 4;

        public static VesselClassType ResolveVessel(GameModes mode, VesselClassType liveVessel)
        {
            if (TrainingModeCatalog.TryGet(mode, out var row) && row.VesselLocked)
                return row.Vessel;
            return liveVessel;
        }

        /// <summary>
        /// The genome to fly, or null when the archive has no entry for that key.
        /// Registry defaults are not an entry. The returned genome is a clone.
        /// </summary>
        public static TrainingGenome ResolveGenome(
            TrainingArchiveSO archive,
            GameModes mode,
            VesselClassType liveVessel,
            int playIntensity,
            bool useStoredGenomeForLowerIntensity)
        {
            if (archive == null) return null;
            playIntensity = Mathf.Clamp(playIntensity, 1, 4);
            VesselClassType keyed = ResolveVessel(mode, liveVessel);
            int lookup = useStoredGenomeForLowerIntensity ? playIntensity : TrainedIntensity;
            var entry = archive.Find(keyed, mode, lookup);
            if (entry?.Genome == null) return null;
            return entry.Genome.Clone();
        }

        /// <summary>
        /// Intensity the pilot should declare. Dithering keeps the match intensity
        /// so 1–3 degrade the decision. An explicit lower-tier genome is flown raw
        /// (intensity 4 through the ditherer) because that genome is already the tier.
        /// </summary>
        public static int FlownIntensity(int playIntensity, bool useStoredGenomeForLowerIntensity)
        {
            playIntensity = Mathf.Clamp(playIntensity, 1, 4);
            if (useStoredGenomeForLowerIntensity && playIntensity < TrainedIntensity)
                return TrainedIntensity;
            return playIntensity;
        }

        public static bool TryInstall(
            GameObject host,
            IVessel vessel,
            GameDataSO gameData,
            CellRuntimeDataSO cellData,
            TrainingArchiveSO archive,
            GameModes mode,
            VesselClassType liveVessel,
            int playIntensity,
            bool useStoredGenomeForLowerIntensity)
        {
            var genome = ResolveGenome(archive, mode, liveVessel, playIntensity, useStoredGenomeForLowerIntensity);
            if (genome == null || host == null) return false;
            Install(host, vessel, gameData, cellData, genome,
                FlownIntensity(playIntensity, useStoredGenomeForLowerIntensity));
            return true;
        }

        public static void Install(
            GameObject host,
            IVessel vessel,
            GameDataSO gameData,
            CellRuntimeDataSO cellData,
            TrainingGenome genome,
            int flownIntensity)
        {
            // Stop, then disable. A stopped AIPilot still runs ability coroutines
            // until the component is disabled, and those coroutines write the same
            // sticks TrainingPilot writes.
            var aiPilot = host.GetComponentInChildren<AIPilot>(true);
            if (aiPilot != null)
            {
                if (aiPilot.AutoPilotEnabled) aiPilot.StopAIPilot();
                aiPilot.enabled = false;
            }

            var pilot = host.GetComponent<TrainingPilot>();
            if (pilot == null) pilot = host.AddComponent<TrainingPilot>();
            pilot.BindVessel(vessel, gameData, cellData);
            pilot.Intensity = Mathf.Clamp(flownIntensity, 1, 4);
            pilot.LoadGenome(genome);
            pilot.BeginEpisode();
            CSDebug.LogVerbose(CSLogChannel.AITraining,
                $"[Deploy] Installed trained pilot at intensity {pilot.Intensity}.");
        }
    }
}
