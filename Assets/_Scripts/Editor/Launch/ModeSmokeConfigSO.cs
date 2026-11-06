using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// What the per-mode smoke test (<see cref="ModeSmokeLauncher"/>, Docs/LAUNCH/SMOKE_TEST.md) plays and how long
    /// it waits. Editor-tool config, so it lives in an asset rather than a list in code
    /// (<c>Assets/_SO_Assets/Launch/ModeSmokeConfig.asset</c>).
    ///
    /// <para>The modes come from the shipped ROSTERS (the arcade and arena card lists a player launches from), so a new
    /// card is smoked the day it is listed, with nothing to register. A mode the harness cannot drive is named in
    /// <see cref="Excluded"/> with its reason, and every run reports it as skipped with that reason, never as passed.</para>
    ///
    /// <para>The end conditions are the shipped ones: the smoke never shortens a match, because the only override path
    /// (<c>EndConditionOverridesSO</c>) is a shipped asset and a per-run edit would write into it. It plays at
    /// <see cref="TimeScale"/> instead.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ModeSmokeConfig", menuName = "Cosmic Shore/Launch/Mode Smoke Config")]
    public class ModeSmokeConfigSO : ScriptableObject
    {
        public const string AssetPath = "Assets/_SO_Assets/Launch/ModeSmokeConfig.asset";

        [Serializable]
        public class Exclusion
        {
            [Tooltip("The mode the smoke does not play.")]
            public GameModes Mode;
            [Tooltip("Why, in one plain line. Shown in every run's summary and as the skipped test's message.")]
            public string Reason;
        }

        [Serializable]
        public class ModeOverride
        {
            [Tooltip("The mode these values replace the defaults for.")]
            public GameModes Mode;
            [Tooltip("Intensity to launch at (1-4, clamped to the card's range). 0 = the default.")]
            [Range(0, 4)] public int Intensity;
            [Tooltip("Seats, every one flown by the AI (clamped to the card's seat range). 0 = the default.")]
            [Min(0)] public int Players;
            [Tooltip("Game seconds the match may run before the smoke calls it a timeout. 0 = the default.")]
            [Min(0f)] public float MatchBudgetSeconds;
        }

        [Header("Which modes")]
        [Tooltip("The card lists players launch from (ArcadeGames, ArenaGames). Every card on them is smoked unless excluded.")]
        [SerializeField] List<SO_GameList> rosters = new();
        [Tooltip("Modes the smoke cannot drive, each with its reason. Reported as skipped in every run.")]
        [SerializeField] List<Exclusion> excluded = new();
        [Tooltip("The short set the 'quick' menu item and -smokeModes quick run: a fast check before a full run.")]
        [SerializeField] List<GameModes> quickModes = new();

        [Header("How each match is played")]
        [Tooltip("Intensity every mode launches at unless overridden (clamped to the card's range).")]
        [Range(1, 4)] [SerializeField] int defaultIntensity = 1;
        [Tooltip("Seats per match unless overridden, every one flown by the AI (clamped to the card's seat range).")]
        [Min(2)] [SerializeField] int defaultPlayers = 3;
        [Tooltip("The lobby AI difficulty every AI seat flies. Legendary is the unhandicapped pilot, the quickest to finish.")]
        [SerializeField] AIDifficulty aiDifficulty = AIDifficulty.Legendary;
        [Tooltip("Time.timeScale while a turn runs. The AI trainer's headless runs use up to 4.")]
        [Range(1f, 8f)] [SerializeField] float timeScale = 4f;
        [Tooltip("Per-mode intensity, seats or budget, where the defaults do not suit a mode.")]
        [SerializeField] List<ModeOverride> overrides = new();

        [Header("Budgets (seconds)")]
        [Tooltip("Game seconds a match may run before it counts as a timeout. Matches are designed around 90 s.")]
        [Min(30f)] [SerializeField] float matchBudgetSeconds = 600f;
        [Tooltip("Real seconds from entering Play to the menu holding a local pilot.")]
        [Min(30f)] [SerializeField] float bootTimeoutSeconds = 180f;
        [Tooltip("Real seconds from the launch to the mode's scene holding a controller and every seat.")]
        [Min(30f)] [SerializeField] float sceneTimeoutSeconds = 180f;
        [Tooltip("Real seconds from the scene being ready to the first turn running (Ready pressed).")]
        [Min(15f)] [SerializeField] float startTimeoutSeconds = 120f;
        [Tooltip("Real seconds leaving Play may take before the run gives up on the remaining modes.")]
        [Min(15f)] [SerializeField] float exitTimeoutSeconds = 60f;

        [Header("Checks")]
        [Tooltip("Path length (world units) a seat must fly during the match to count as moving.")]
        [Min(0.1f)] [SerializeField] float movingDistance = 10f;

        public IReadOnlyList<SO_GameList> Rosters => rosters;
        public IReadOnlyList<Exclusion> Excluded => excluded;
        public IReadOnlyList<GameModes> QuickModes => quickModes;
        public AIDifficulty AIDifficulty => aiDifficulty;
        public float TimeScale => timeScale;
        public float BootTimeoutSeconds => bootTimeoutSeconds;
        public float SceneTimeoutSeconds => sceneTimeoutSeconds;
        public float StartTimeoutSeconds => startTimeoutSeconds;
        public float ExitTimeoutSeconds => exitTimeoutSeconds;
        public float MovingDistance => movingDistance;

        /// <summary>The reason <paramref name="mode"/> is excluded, or null when it is played.</summary>
        public string ExclusionReason(GameModes mode)
        {
            foreach (var e in excluded)
                if (e != null && e.Mode == mode)
                    return string.IsNullOrWhiteSpace(e.Reason) ? "excluded in ModeSmokeConfig (no reason given)" : e.Reason;
            return null;
        }

        public int IntensityFor(SO_ArcadeGame card)
        {
            var o = Override(card.Mode);
            int wanted = o != null && o.Intensity > 0 ? o.Intensity : defaultIntensity;
            return Mathf.Clamp(wanted, Mathf.Max(1, card.MinIntensity), Mathf.Max(card.MinIntensity, card.MaxIntensity));
        }

        public int PlayersFor(SO_ArcadeGame card)
        {
            var o = Override(card.Mode);
            int wanted = o != null && o.Players > 0 ? o.Players : defaultPlayers;
            int max = Mathf.Max(1, card.MaxSeats);
            return Mathf.Clamp(wanted, Mathf.Clamp(card.MinPlayersAllowed, 1, max), max);
        }

        public float MatchBudgetFor(GameModes mode)
        {
            var o = Override(mode);
            return o != null && o.MatchBudgetSeconds > 0f ? o.MatchBudgetSeconds : matchBudgetSeconds;
        }

        ModeOverride Override(GameModes mode)
        {
            foreach (var o in overrides)
                if (o != null && o.Mode == mode) return o;
            return null;
        }
    }
}
