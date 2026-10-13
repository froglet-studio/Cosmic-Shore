using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Serpent Charge. The TARGET - the hostile prisms a domain must destroy with
    /// the sniper rifle to win, sniped rival pilots folded in (default 800) - is resolved at
    /// <see cref="StartMonitor"/> from <see cref="EndConditionOverridesSO"/> (FrogletTools > Game
    /// Modes > End Game Conditions; never a per-scene field), synced to every client via
    /// NetworkVariable, and published to <see cref="GameDataSO.PrismTargetCount"/>. The turn ends
    /// (server-side) when the mode's <see cref="ScoringRuleSO.IsObjectiveReached"/> reports an
    /// active domain's FOLDED score (<see cref="SerpentChargeScoringRuleSO.DomainValue"/>) has
    /// reached the target. Structurally RampagePrismTurnMonitor with its own getter.
    /// </summary>
    public class SerpentChargePrismTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netPrismTarget = new(0);

        void OnEnable()  => _netPrismTarget.OnValueChanged += OnPrismTargetSynced;
        void OnDisable() => _netPrismTarget.OnValueChanged -= OnPrismTargetSynced;

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
                    ? overrides.GetSerpentChargePrismTarget()
                    : EndConditionOverridesSO.DefaultSerpentChargePrismTarget;

                _netPrismTarget.Value = target;
                gameData.PrismTargetCount = target;

                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[SerpentChargePrismMonitor] Server set prism target: {target}");
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
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate()
        {
            // Destruction is bursty (one round takes a whole stand in a frame), but the 1s display
            // tick is plenty - no per-stat event subscriptions to leak (BUGS.md B15 class).
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
