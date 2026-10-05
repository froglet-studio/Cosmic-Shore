using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puts the Skim Race pilot on an AI seat. Called by
    /// <c>ServerPlayerVesselInitializerWithAI.ConfigureAIPilot</c> — the one place every backfill
    /// bot (and every hull an AI inherits mid-match) is configured — so the pilot is present in
    /// the normal arcade flow with no scene wiring, and an AI seat in any other mode is untouched.
    ///
    /// Skipped while <c>GameDataSO.IsTraining</c> is set (the genetic trainer owns the seats then)
    /// and when <see cref="SkimRaceAIConfigSO.DeployInNormalPlay"/> is off, in which case the seat
    /// keeps the platform <see cref="AIPilot"/>.
    /// </summary>
    public static class SkimRaceAIDeployment
    {
        /// <summary>True when Skim Race AI seats in this match belong to <see cref="SkimRacePilot"/>.</summary>
        public static bool Claims(GameDataSO gameData)
        {
            if (gameData == null || gameData.IsTraining) return false;
            if (gameData.GameMode != GameModes.SkimRace) return false;
            var cfg = SkimRaceAIConfigSO.LoadDefault();
            return cfg != null && cfg.DeployInNormalPlay;
        }

        /// <summary>Installs (or re-binds) the pilot on <paramref name="vesselObject"/>. Returns the pilot, or null.</summary>
        public static SkimRacePilot TryInstall(GameObject vesselObject, IVessel vessel, GameDataSO gameData)
        {
            if (vesselObject == null || vessel == null || !Claims(gameData)) return null;
            if (vessel.VesselStatus == null || vessel.VesselStatus.VesselType != VesselClassType.Squirrel)
                return null; // tuned for the Squirrel; other hulls keep AIPilot

            var pilot = vesselObject.GetComponent<SkimRacePilot>();
            if (pilot == null) pilot = vesselObject.AddComponent<SkimRacePilot>();
            int intensity = gameData.SelectedIntensity != null ? Mathf.Clamp(gameData.SelectedIntensity.Value, 1, 4) : 1;
            var config = SkimRaceAIConfigSO.LoadFor(intensity);
            // The host's lobby pick, independent of intensity (intensity is the map, difficulty is
            // the opponent). Read here so every seat - backfill and adopted hull alike - reports
            // the difficulty it was installed under.
            var difficulty = AIDifficultyRules.Resolve(gameData.RequestedAIDifficulty);
            pilot.Bind(vessel, gameData, config);
            CSDebug.LogVerbose(CSLogChannel.AITraining,
                $"[SkimRaceAI] {vessel.VesselStatus.PlayerName} flies the Skim Race pilot " +
                $"({config.PolicyVersion}, I{intensity}, {difficulty}).");
            return pilot;
        }
    }
}
