using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Puts the Skim Race pilot on an AI seat - in Skim Race (crystals on the waypoint track) and
    /// in Regatta (rings along the domain's rail): the same pilot, a different
    /// <see cref="SkimRaceObjective"/>. Called by
    /// <c>ServerPlayerVesselInitializerWithAI.ConfigureAIPilot</c> — the one place every backfill
    /// bot (and every hull an AI inherits mid-match) is configured — so the pilot is present in
    /// the normal arcade flow with no scene wiring, and an AI seat in any other mode is untouched.
    ///
    /// Skipped while <c>GameDataSO.IsGeneticTrainingSession</c> is set (the genetic trainer owns
    /// the seats then; a Hangar PRACTICE game, <c>IsTraining</c>, is a normal match and gets the pilot)
    /// and when <see cref="SkimRaceAIConfigSO.DeployInNormalPlay"/> is off, in which case the seat
    /// keeps the platform <see cref="AIPilot"/>.
    /// </summary>
    public static class SkimRaceAIDeployment
    {
        /// <summary>True when this match's Squirrel AI seats belong to <see cref="SkimRacePilot"/>:
        /// a mode with a racing objective (<see cref="SkimRaceObjective.For"/>), outside a genetic-training session.</summary>
        public static bool Claims(GameDataSO gameData)
        {
            if (gameData == null || gameData.IsGeneticTrainingSession) return false;
            if (gameData.GameMode is not (GameModes.SkimRace or GameModes.Regatta)) return false;
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
            pilot.Bind(vessel, gameData, config, SkimRaceObjective.For(gameData));
            CSDebug.LogVerbose(CSLogChannel.AITraining,
                $"[SkimRaceAI] {vessel.VesselStatus.PlayerName} flies the Skim Race pilot " +
                $"({config.PolicyVersion}, I{intensity}, {pilot.Objective?.GetType().Name}).");
            return pilot;
        }
    }
}
