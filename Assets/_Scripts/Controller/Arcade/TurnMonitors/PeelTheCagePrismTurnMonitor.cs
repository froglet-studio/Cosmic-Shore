using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for PeelTheCage. The cage TARGET - how many prisms a domain must destroy to
    /// break out and win (default 600) - is resolved at <see cref="StartMonitor"/> from
    /// <see cref="EndConditionOverridesSO"/> (FrogletTools ▸ Game Modes ▸ End Game Conditions;
    /// never a per-scene field), synced to every client via NetworkVariable, and published to
    /// <see cref="GameDataSO.PrismTargetCount"/>. The turn ends (server-side) when the mode's
    /// <see cref="ScoringRuleSO.IsObjectiveReached"/> reports an active domain has reached it.
    ///
    /// Publishing the target to gameData is what makes the fauna ladder work as well as the end
    /// condition: <c>PeelTheCageController</c> reads the SAME number to place the 25% / 50% release
    /// thresholds, so moving the target in the editor moves the whole escalation with it and
    /// the two can never drift.
    ///
    /// The display channel shows the LOCAL player's domain deficit ("bars remaining").
    /// </summary>
    public class PeelTheCagePrismTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netPrismTarget = new(0);

        void OnEnable()
        {
            _netPrismTarget.OnValueChanged += OnPrismTargetSynced;
        }

        void OnDisable()
        {
            _netPrismTarget.OnValueChanged -= OnPrismTargetSynced;
        }

        void OnPrismTargetSynced(int previousValue, int newValue)
        {
            if (newValue > 0)
                gameData.PrismTargetCount = newValue;
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (IsServer)
            {
                var overrides = EndConditionOverridesSO.Instance;
                int target = overrides != null
                    ? overrides.GetPeelTheCagePrismTarget()
                    : EndConditionOverridesSO.DefaultPeelTheCagePrismTarget;

                _netPrismTarget.Value = target;
                gameData.PrismTargetCount = target;

                CSDebug.Log($"[PeelTheCagePrismMonitor] Server set cage target: {target}");
            }
            else if (_netPrismTarget.Value > 0)
            {
                // Late start on a client that already replicated the value.
                gameData.PrismTargetCount = _netPrismTarget.Value;
            }

            UpdateRemainingUI();
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // End condition delegated to the mode's ScoringRule: first active domain whose
            // destruction (HostilePrismsDestroyed) sum reaches the target breaks out and wins.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate()
        {
            // Destruction is bursty (a ram down a rib lands several bars in one frame), but a
            // 1s display tick is plenty - no per-stat event subscriptions to leak
            // (Docs/ScoringSystem/BUGS.md B15 class).
            UpdateRemainingUI();
        }

        void UpdateRemainingUI()
        {
            if (!onUpdateTurnMonitorDisplay || gameData.ScoringRule == null) return;

            int remaining = gameData.ScoringRule.Remaining(
                gameData, gameData.LocalPlayer?.Domain ?? Domains.Blue);
            onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }
    }
}
