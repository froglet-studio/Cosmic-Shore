// NetworkCrystalCollisionTurnMonitor.cs
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Crystal objective monitor (HexRace + Crystal Capture). Resolves the crystal target
    /// at <see cref="StartMonitor"/> from <see cref="EndConditionOverridesSO"/>
    /// (Tools &gt; Cosmic Shore &gt; End Game Conditions; 0 = auto from waypoints, else 39 -
    /// never a per-scene field), syncs it to all clients via the base target leg into
    /// <see cref="GameDataSO.CrystalTargetCount"/>, and shows the local player's DOMAIN
    /// crystal deficit. End condition, remaining display, and the B15 subscription
    /// lifecycle come from <see cref="ObjectiveTurnMonitor"/>.
    /// </summary>
    public class NetworkCrystalCollisionTurnMonitor : ObjectiveTurnMonitor
    {
        // The RESOLVED crystal target for this turn - set in StartMonitor. Intentionally
        // NOT a [SerializeField]: end-game counts are authored only via the tool, never
        // per-scene. Do not re-add [SerializeField] here (see /EndGameConditions skill).
        protected int CrystalCollisions;

        [Header("Optional Configuration")]
        [SerializeField] SpawnableWaypointTrack optionalEnvironment;
        [SerializeField] int optionalLaps = 4;

        public override void StartMonitor()
        {
            CrystalCollisions = GetCrystalCollisionCount();

            // Every player's crystal event drives the DOMAIN-sum display (not just the
            // local player's own count). B15 bookkeeping is the base's.
            SubscribeRoster();
            RaiseRemainingUI();

            base.StartMonitor();

            SyncTarget(CrystalCollisions);
            if (IsServer)
                CSDebug.Log($"[NetworkCrystalMonitor] Server set crystal target: {CrystalCollisions} " +
                            $"(intensity={gameData.SelectedIntensity.Value})");
        }

        protected override void PublishTarget(int value) => gameData.CrystalTargetCount = value;

        protected override void AttachStatsHandlers(IRoundStats stats) =>
            stats.OnCrystalsCollectedChanged += OnAnyCrystalChanged;

        protected override void DetachStatsHandlers(IRoundStats stats) =>
            stats.OnCrystalsCollectedChanged -= OnAnyCrystalChanged;

        void OnAnyCrystalChanged(IRoundStats _) => RaiseRemainingUI();

        /// <summary>
        /// The LOCAL PLAYER'S individual remaining count (target - own crystals) - the
        /// HexRaceScoreTracker's end-of-race win check reads this (individual semantics,
        /// distinct from the domain-deficit display).
        /// </summary>
        public string GetRemainingCrystalsCountToCollect()
        {
            if (!gameData.TryGetLocalPlayerStats(out IPlayer _, out IRoundStats ownStats) || ownStats == null)
                return CrystalCollisions.ToString();
            return Mathf.Max(0, CrystalCollisions - ownStats.CrystalsCollected).ToString();
        }

        /// <summary>
        /// Target resolution: the End Game Conditions tool is the authority
        /// (Resources/EndConditionOverrides; keyed by GameMode so HexRace and Crystal
        /// Capture stay independent). 0 there means auto: waypoints x laps, else 39.
        /// </summary>
        protected int GetCrystalCollisionCount()
        {
            int autoCalc = ComputeAutoCalcCount();
            var overrides = EndConditionOverridesSO.Instance;
            return overrides != null ? overrides.GetCrystalCount(gameData.GameMode, autoCalc) : autoCalc;
        }

        int ComputeAutoCalcCount()
        {
            if (optionalEnvironment)
                return optionalEnvironment.waypoints[optionalEnvironment.intensityLevel - 1].positions.Count * optionalLaps;

            CSDebug.LogWarning($"[NetworkCrystalMonitor] No crystal count configured for {gameObject.name} and no waypoints. Defaulting to 39.");
            return 39;
        }
    }
}
