// NetworkCrystalCollisionTurnMonitor.cs
using System.Linq;
using CosmicShore.Data;
using Unity.Netcode;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class NetworkCrystalCollisionTurnMonitor : CrystalCollisionTurnMonitor
    {
        [Header("Test Override")]
        [Tooltip("Enable to override the calculated crystal target for quick testing.")]
        [SerializeField] bool useTestCrystalOverride;
        [Tooltip("Crystal count when useTestCrystalOverride is true (e.g. 1-3 for quick testing).")]
        [SerializeField] int crystalsToFinishOverride = 3;

        private readonly NetworkVariable<int> _netCrystalCollisions = new NetworkVariable<int>(0);

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
            base.StartMonitor();

            if (!IsServer) return;

            int overrideTarget = useTestCrystalOverride ? Mathf.Max(1, crystalsToFinishOverride) : -1;
            int target = overrideTarget > 0 ? overrideTarget : GetCrystalCollisionCount();

            _netCrystalCollisions.Value = target;
            gameData.CrystalTargetCount = target;

            CSDebug.Log($"[NetworkCrystalMonitor] Server set crystal target: {target} " +
                      $"(override={overrideTarget > 0}, intensity={gameData.SelectedIntensity.Value})");
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            int target = _netCrystalCollisions.Value > 0
                ? _netCrystalCollisions.Value
                : CrystalCollisions;

            return gameData.RoundStatsList.Any(s => s.CrystalsCollected >= target);
        }

        protected override void UpdateCrystalsRemainingUI()
        {
            int target = _netCrystalCollisions.Value > 0
                ? _netCrystalCollisions.Value
                : CrystalCollisions;

            int current = ownStats?.CrystalsCollected ?? 0;
            int remaining = Mathf.Max(0, target - current);

            if (onUpdateTurnMonitorDisplay)
                onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }
    }
}
