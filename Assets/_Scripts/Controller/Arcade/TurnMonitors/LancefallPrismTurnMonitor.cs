using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Lancefall. The prism TARGET - how many hostile prisms a domain must break to win - is resolved at
    /// <see cref="StartMonitor"/> from <see cref="EndConditionOverridesSO.GetLancefallPrismTarget"/> (FrogletTools > Game
    /// Modes > End Game Conditions; never a per-scene field), on the server, synced to every client via NetworkVariable
    /// (a client receives the number rather than deriving it, Cleave's rule), and published to
    /// <see cref="GameDataSO.PrismTargetCount"/>. The turn ends (server-side) when the mode's
    /// <see cref="ScoringRuleSO.IsObjectiveReached"/> reports an active domain's sum has reached it.
    /// Structurally <see cref="ShatterplanePrismTurnMonitor"/> with a scalar getter.
    /// </summary>
    public class LancefallPrismTurnMonitor : TurnMonitor
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
                    ? overrides.GetLancefallPrismTarget()
                    : EndConditionOverridesSO.DefaultLancefallPrismTarget;

                _netPrismTarget.Value = target;
                gameData.PrismTargetCount = target;

                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[LancefallPrismMonitor] Server set prism target: {target}");
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
            // An edge-on lance takes a strip at once, but the 1 s display tick is plenty - no per-stat event
            // subscriptions to leak (BUGS.md B15 class).
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
