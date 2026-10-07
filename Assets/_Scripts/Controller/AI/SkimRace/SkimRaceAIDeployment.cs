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
    /// Skipped while <c>GameDataSO.IsTraining</c> is set (the genetic trainer owns the seats then)
    /// and when <see cref="SkimRaceAIConfigSO.DeployInNormalPlay"/> is off, in which case the seat
    /// keeps the platform <see cref="AIPilot"/>.
    ///
    /// <para>The policy is the intensity's own tuning file when one exists AND it was tuned on the map
    /// in the scene (<see cref="PolicyFor"/>); otherwise the general policy, which is tuned for every
    /// track - so a new intensity, or a map edited since its tuning, still gets an AI that finishes.</para>
    /// </summary>
    public static class SkimRaceAIDeployment
    {
        /// <summary>The scene load and intensity last warned about, so a stale tuning file warns once
        /// per race rather than once per AI seat.</summary>
        static (int scene, int intensity) _warnedFor;

        /// <summary>True when this match's Squirrel AI seats belong to <see cref="SkimRacePilot"/>:
        /// a mode with a racing objective (<see cref="SkimRaceObjective.For"/>), outside training.</summary>
        public static bool Claims(GameDataSO gameData)
        {
            if (gameData == null || gameData.IsTraining) return false;
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
            // No upper clamp: an intensity with no tuning file of its own flies the general policy.
            int intensity = gameData.SelectedIntensity != null ? Mathf.Max(1, gameData.SelectedIntensity.Value) : 1;
            var config = PolicyFor(intensity);
            // The host's lobby pick, independent of intensity (intensity is the map, difficulty is
            // the opponent): the same per-intensity policy, plus the difficulty's deliberate mistakes
            // (none for Hard). Every seat - backfill and adopted hull alike - is installed under it,
            // racing this mode's objective (Skim Race's crystals, Regatta's rings).
            var difficulty = AIDifficultyRules.Resolve(gameData.RequestedAIDifficulty);
            var handicap = SkimRaceDifficultySO.Load().For(difficulty);
            pilot.Bind(vessel, gameData, config, handicap, SkimRaceObjective.For(gameData));
            CSDebug.LogVerbose(CSLogChannel.AITraining,
                $"[SkimRaceAI] {vessel.VesselStatus.PlayerName} flies the Skim Race pilot " +
                $"({config.PolicyVersion}, I{intensity}, {pilot.Objective?.GetType().Name}, {difficulty}" +
                (handicap.IsNone ? ")." : $": reaction {handicap.ReactionSeconds:0.##} s, mistake chance {handicap.MistakeChance:0.##})."));
            return pilot;
        }

        /// <summary>
        /// The policy for <paramref name="intensity"/>: its own tuning file (<see cref="SkimRaceAIConfigSO.LoadFor"/>)
        /// when that file fits the map in the scene (<see cref="SkimRaceAIConfigSO.FitsTrack"/>), otherwise
        /// the general policy - with one console warning per race naming the file and the command that
        /// retunes it. A file tuned on another map is not trusted: its numbers were found for corners and
        /// crystals that are no longer there, while the general policy is tuned to finish any track.
        /// </summary>
        public static SkimRaceAIConfigSO PolicyFor(int intensity)
        {
            var tuned = SkimRaceAIConfigSO.LoadFor(intensity);
            if (string.IsNullOrEmpty(tuned.TrackFingerprint)) return tuned; // the general policy: any map
            if (!SkimRaceCourseSource.TryFingerprintFromScene(intensity, out string live, out int scene) || tuned.FitsTrack(live))
                return tuned;

            var general = SkimRaceAIConfigSO.LoadDefault();
            if (_warnedFor != (scene, intensity))
            {
                _warnedFor = (scene, intensity);
                CSDebug.LogWarning(
                    $"[SkimRaceAI] Intensity {intensity}: the AI tuning file {tuned.name} ({tuned.PolicyVersion}) " +
                    $"was tuned on a different map (fingerprint {tuned.TrackFingerprint}; this scene is {live}), so " +
                    $"the AI flies the general settings ({general.PolicyVersion}) instead. To retune it for this map: " +
                    $"python3 Tools/Build/skimrace_retune.py {intensity}");
            }
            return general;
        }
    }
}
