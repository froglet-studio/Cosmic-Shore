using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Which end-game stats the scoreboard tracks, per game mode - the ONE genuinely per-mode
    /// value the in-game canvas used to carry as a scene override (`Docs/GAMECANVAS.md` §3).
    ///
    /// The shared <c>GameCanvas.prefab</c> ships with an EMPTY
    /// <c>EventDrivenStatsProvider.statsToTrack</c>, and the provider consults this asset
    /// (<c>Resources/GameModeStatsProfile</c>) keyed by <c>GameDataSO.GameMode</c> before it
    /// falls back to vessel-telemetry discovery. So a mode's stat list is a one-line edit here,
    /// and no scene needs to override the canvas to change it.
    ///
    /// The shipped values are the MEASURED ones: every scene tracked
    /// {Longest Drift, MaxBoost, PrismsDamaged} except Skim Race (five) and Joust (leads with
    /// Jousts Won). Reproduce with <c>Tools/Build/gamecanvas_unification_report.py</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "GameModeStatsProfile",
        menuName = "ScriptableObjects/Scoring/Game Mode Stats Profile")]
    public sealed class GameModeStatsProfileSO : ScriptableObject
    {
        public const string ResourcePath = "GameModeStatsProfile";

        [Serializable]
        public sealed class Entry
        {
            public GameModes Mode;
            public List<VesselStatEventSO> Stats = new();
        }

        [Tooltip("Stats tracked by every mode that has no entry below.")]
        [SerializeField] List<VesselStatEventSO> defaultStats = new();

        [Tooltip("Per-mode lists. The first entry whose Mode matches wins; an empty list means 'use the default'.")]
        [SerializeField] List<Entry> perMode = new();

        public IReadOnlyList<VesselStatEventSO> DefaultStats => defaultStats;
        public IReadOnlyList<Entry> PerMode => perMode;

        /// <summary>The list for <paramref name="mode"/>, or the default list when it has none.</summary>
        public IReadOnlyList<VesselStatEventSO> StatsFor(GameModes mode)
        {
            for (int i = 0; i < perMode.Count; i++)
            {
                var e = perMode[i];
                if (e != null && e.Mode == mode && e.Stats != null && e.Stats.Count > 0)
                    return e.Stats;
            }
            return defaultStats;
        }

        static GameModeStatsProfileSO _cached;
        static bool _searched;

        /// <summary>The shipped profile, or null when the asset is absent (loaded once).</summary>
        public static GameModeStatsProfileSO Load()
        {
            if (_searched) return _cached;
            _searched = true;
            _cached = Resources.Load<GameModeStatsProfileSO>(ResourcePath);
            return _cached;
        }
    }
}
