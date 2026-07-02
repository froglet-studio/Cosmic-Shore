using System; // Required for Action
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Engine;
using CosmicShore.Utility;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class CrystalCollisionTurnMonitor : TurnMonitor
    {
        // The RESOLVED crystal target for this turn — set in StartMonitor from
        // EndConditionOverridesSO (Tools > Cosmic Shore > End Game Conditions) → waypoints → 39.
        // Intentionally NOT a [SerializeField]: end-game counts are authored only via the tool,
        // never per-scene. Do not re-add [SerializeField] here (see /EndGameConditions skill).
        protected int CrystalCollisions;
        protected IRoundStats ownStats;

        [Header("Optional Configuration")]
        [SerializeField] SpawnableWaypointTrack optionalEnvironment;
        [SerializeField] int optionalLaps = 4;

        public override void StartMonitor()
        {
            CrystalCollisions = GetCrystalCollisionCount();

            InitializeOwnStats();
            if (ownStats != null)
            {
                // Idempotent: StartMonitor can be invoked more than once per turn
                // (e.g. duplicated SOAP subscriptions) — never double-attach.
                ownStats.OnCrystalsCollectedChanged -= UpdateCrystals;
                ownStats.OnCrystalsCollectedChanged += UpdateCrystals;
            }
            UpdateCrystals(ownStats);
            base.StartMonitor();
        }

        public override void StopMonitor()
        {
            base.StopMonitor();
            if (ownStats != null) ownStats.OnCrystalsCollectedChanged -= UpdateCrystals;
        }

        public override bool CheckForEndOfTurn()
        {
            if (ownStats == null) return false;
            return ownStats.CrystalsCollected >= CrystalCollisions;
        }

        protected virtual void UpdateCrystals(IRoundStats stats) => UpdateCrystalsRemainingUI();

        protected virtual void UpdateCrystalsRemainingUI()
        {
            string message = GetRemainingCrystalsCountToCollect();
            if (onUpdateTurnMonitorDisplay) onUpdateTurnMonitorDisplay.Raise(message);
        }

        public string GetRemainingCrystalsCountToCollect()
        {
            InitializeOwnStats();
            if (ownStats == null) return CrystalCollisions.ToString();
            int remaining = CrystalCollisions - ownStats.CrystalsCollected;
            return Mathf.Max(0, remaining).ToString();
        }

        protected virtual void InitializeOwnStats()
        {
            if (ownStats != null) return;
            if (gameData.TryGetLocalPlayerStats(out IPlayer _, out ownStats)) return;
        }

        protected int GetCrystalCollisionCount()
        {
            // End-game counts are the tool's authority: Tools > Cosmic Shore > End Game Conditions
            // (Resources/EndConditionOverrides). A 0 there means "auto" → the autoCalc below.
            // Keyed by GameMode so HexRace and Crystal Capture (same monitor class) stay independent.
            var overrides = EndConditionOverridesSO.Instance;

            // PORT Deviation (drift-sync shim): with no EndConditionOverrides asset registered
            // (the headless client and tests have no editor tool to author the Resources asset,
            // and the engine Resources registry starts empty), a pre-seeded non-zero
            // CrystalCollisions keeps the legacy explicit-override semantic (SkimRaceSim /
            // TurnMonitorTests seed it before StartMonitor). Remove once the content phase
            // registers a real EndConditionOverrides asset.
            if (overrides == null && CrystalCollisions > 0) return CrystalCollisions;

            int autoCalc = ComputeAutoCalcCount();
            return overrides != null ? overrides.GetCrystalCount(gameData.GameMode, autoCalc) : autoCalc;
        }

        int ComputeAutoCalcCount()
        {
            if (optionalEnvironment)
                return optionalEnvironment.waypoints[optionalEnvironment.intensityLevel - 1].positions.Count * optionalLaps;

            CSDebug.LogWarning($"[CrystalCollisionTurnMonitor] No crystal count configured for {gameObject.name} and no waypoints. Defaulting to 39.");
            return 39;
        }
    }
}
