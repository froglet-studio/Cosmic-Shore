// NetworkCrystalCollisionTurnMonitor.cs
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The crystal collection turn monitor (SkimRace, Crystal Capture). <see cref="StartMonitor"/> resolves the
    /// crystal target (the End Game Conditions tool, else the track's crystals per lap x laps, else 39), syncs it to
    /// all clients via NetworkVariable and publishes it to <see cref="GameDataSO.CrystalTargetCount"/> so any system
    /// can read it. It used to sit on a local-only base, <c>CrystalCollisionTurnMonitor</c>, which no scene placed;
    /// that base was folded in here on 2026-10-25 (Docs/REFACTOR_BACKLOG.md M7), its steps kept in their order.
    /// </summary>
    public class NetworkCrystalCollisionTurnMonitor : TurnMonitor
    {
        // The RESOLVED crystal target for this turn - set in StartMonitor from
        // EndConditionOverridesSO (FrogletTools > Game Modes > End Game Conditions) → the track's
        // crystals per lap x laps → 39.
        // Intentionally NOT a [SerializeField]: end-game counts are authored only via the tool,
        // never per-scene. Do not re-add [SerializeField] here (see /EndGameConditions skill).
        int CrystalCollisions;
        IRoundStats ownStats;

        [Header("Optional Configuration")]
        [SerializeField] SpawnableWaypointTrack optionalEnvironment;

        [Tooltip("Laps per intensity level (index 0 = intensity 1), matching the track's waypoint " +
                 "list by index. Lets a long track run fewer laps than a short one. An entry <= 0, " +
                 "or a missing entry for the current intensity, falls back to Optional Laps.")]
        [SerializeField] List<int> lapsPerIntensity = new();

        [Tooltip("Fallback lap count, used for any intensity Laps Per Intensity does not cover.")]
        [SerializeField] int optionalLaps = 4;

        private readonly NetworkVariable<int> _netCrystalCollisions = new NetworkVariable<int>(0);

        // Stats this monitor actually subscribed to. Unsubscription must run off THIS
        // list, never gameData.RoundStatsList: on a mid-turn scene exit, SceneLoader's
        // ResetRuntimeData clears the roster BEFORE the old scene's objects are
        // destroyed, so a list-based unsubscribe loop detaches nothing and the handler
        // leaks onto the persistent human RoundStats (Docs/ScoringSystem/BUGS.md B15).
        readonly List<IRoundStats> _subscribedStats = new();

        void OnEnable()
        {
            _netCrystalCollisions.OnValueChanged += OnCrystalTargetSynced;
        }

        void OnDisable()
        {
            _netCrystalCollisions.OnValueChanged -= OnCrystalTargetSynced;
        }

        /// <summary>
        /// Fires on all clients when the server writes to <c>_netCrystalCollisions</c>.
        /// Keeps <see cref="GameDataSO.CrystalTargetCount"/> in sync across all machines.
        /// </summary>
        void OnCrystalTargetSynced(int previousValue, int newValue)
        {
            if (newValue > 0)
                gameData.CrystalTargetCount = newValue;
        }

        public override void StartMonitor()
        {
            // Resolve the target: the End Game Conditions tool > waypoints > 39
            CrystalCollisions = GetCrystalCollisionCount();

            InitializeOwnStats();
            if (ownStats != null)
            {
                // Idempotent: StartMonitor can be invoked more than once per turn
                // (e.g. duplicated SOAP subscriptions) - never double-attach.
                ownStats.OnCrystalsCollectedChanged -= UpdateCrystals;
                ownStats.OnCrystalsCollectedChanged += UpdateCrystals;
            }
            UpdateCrystals(ownStats);
            base.StartMonitor();

            // Subscribe to every player's crystal-changed event so the HUD displays the
            // up-to-date DOMAIN sum (not just the local player's count). The local player was
            // wired above; teammates' collections would otherwise stay invisible.
            foreach (var stats in gameData.RoundStatsList)
            {
                if (stats == null || _subscribedStats.Contains(stats)) continue;
                stats.OnCrystalsCollectedChanged += OnAnyCrystalChanged;
                _subscribedStats.Add(stats);
            }

            if (!IsServer) return;

            _netCrystalCollisions.Value = CrystalCollisions;
            gameData.CrystalTargetCount = CrystalCollisions;

            CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[NetworkCrystalMonitor] Server set crystal target: {CrystalCollisions} " +
                      $"(intensity={gameData.SelectedIntensity.Value})");
        }

        public override void StopMonitor()
        {
            foreach (var stats in _subscribedStats)
            {
                if (stats == null) continue;
                stats.OnCrystalsCollectedChanged -= OnAnyCrystalChanged;
            }
            _subscribedStats.Clear();
            base.StopMonitor();
            if (ownStats != null) ownStats.OnCrystalsCollectedChanged -= UpdateCrystals;
        }

        public override void OnDestroy()
        {
            // Safety net for destruction paths that bypass StopMonitor - detaching
            // from the persistent RoundStats must never depend on the turn ending.
            StopMonitor();
            base.OnDestroy();
        }

        void UpdateCrystals(IRoundStats stats) => UpdateCrystalsRemainingUI();

        void OnAnyCrystalChanged(IRoundStats _) => UpdateCrystalsRemainingUI();

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // End condition delegated to the mode's ScoringRule. SkimRace and Crystal Capture
            // both end when an active domain's summed metric reaches the target; the trigger is
            // per-team so AI and human teammates finish the objective together.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        void UpdateCrystalsRemainingUI()
        {
            // Remaining is the LOCAL PLAYER'S DOMAIN deficit (the rule owns target + sum), so
            // the HUD reflects the team objective rather than a single ship. A null local
            // player resolves to Domains.Blue (sum 0 → full target remaining), matching the
            // previous behavior.
            int remaining = gameData.ScoringRule.Remaining(
                gameData, gameData.LocalPlayer?.Domain ?? Domains.Blue);

            if (onUpdateTurnMonitorDisplay)
                onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }

        public string GetRemainingCrystalsCountToCollect()
        {
            InitializeOwnStats();
            if (ownStats == null) return CrystalCollisions.ToString();
            int remaining = CrystalCollisions - ownStats.CrystalsCollected;
            return Mathf.Max(0, remaining).ToString();
        }

        void InitializeOwnStats()
        {
            if (ownStats != null) return;
            if (gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats)) return;
        }

        int GetCrystalCollisionCount()
        {
            int autoCalc = ComputeAutoCalcCount();

            // End-game counts are the tool's authority: FrogletTools > Game Modes > End Game Conditions
            // (Resources/EndConditionOverrides). A 0 there means "auto" → this autoCalc.
            // Keyed by GameMode so SkimRace and Crystal Capture (same monitor class) stay independent.
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null ? overrides.GetCrystalCount(gameData.GameMode, autoCalc) : autoCalc;
        }

        int ComputeAutoCalcCount()
        {
            if (optionalEnvironment)
            {
                // Crystals per lap is the track's authored count when it has one (a densely sampled
                // spline carries far more waypoints than crystal anchors), else its waypoint count.
                int intensity = optionalEnvironment.intensityLevel;
                int perLap = optionalEnvironment.CrystalsPerLap(intensity);
                if (perLap > 0) return perLap * ResolveLaps(intensity);
            }

            CSDebug.LogWarning($"[NetworkCrystalCollisionTurnMonitor] No crystal count configured for {gameObject.name} and no waypoints. Defaulting to 39.");
            return 39;
        }

        /// <summary>
        /// Laps for the given 1-based intensity: the per-intensity entry when one is
        /// authored and positive, otherwise the flat <see cref="optionalLaps"/>. Keeps
        /// scenes that predate <see cref="lapsPerIntensity"/> (empty list) on their
        /// original single-value behavior. Public read-only: the Skim Race AI's map fingerprint
        /// counts the laps too.
        /// </summary>
        public int ResolveLaps(int intensity)
        {
            int index = intensity - 1;
            if (lapsPerIntensity != null && index >= 0 && index < lapsPerIntensity.Count && lapsPerIntensity[index] > 0)
                return lapsPerIntensity[index];

            return Mathf.Max(1, optionalLaps);
        }
    }
}
