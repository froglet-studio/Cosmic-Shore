using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Switchback. The GATE COUNT - how many switches the course has, and
    /// therefore how many a pilot must thread to finish it - is resolved at
    /// <see cref="StartMonitor"/> from <see cref="EndConditionOverridesSO"/> (FrogletTools &gt;
    /// Game Modes &gt; End Game Conditions; never a per-scene field), synced to every client via
    /// NetworkVariable, and published to <see cref="GameDataSO.SwitchTargetCount"/>. Structural
    /// clone of <see cref="SalvoPrismTurnMonitor"/> reading its own overrides key.
    ///
    /// <para><b>The same number builds the course.</b> <c>SwitchbackController</c> reads this
    /// target to decide how many gates to lay, so "the course" and "the target" cannot disagree
    /// - there is no second place a gate count can be authored, and a pilot's goal row counts up
    /// to exactly the last ring in the world.</para>
    ///
    /// <para>The display channel publishes the LOCAL player's REMAINING gates, which for this
    /// mode is their DOMAIN's deficit measured on its lead runner (the rule's fold) - so a
    /// trailing teammate sees the team's real distance from the finish rather than their own.</para>
    /// </summary>
    public class SwitchbackGateTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netGateTarget = new(0);

        void OnEnable()
        {
            _netGateTarget.OnValueChanged += OnGateTargetSynced;
        }

        void OnDisable()
        {
            _netGateTarget.OnValueChanged -= OnGateTargetSynced;
        }

        void OnGateTargetSynced(int previousValue, int newValue)
        {
            if (newValue > 0)
                gameData.SwitchTargetCount = newValue;
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (IsServer)
            {
                var overrides = EndConditionOverridesSO.Instance;
                int target = overrides != null
                    ? overrides.GetSwitchbackGateTarget()
                    : EndConditionOverridesSO.DefaultSwitchbackGateTarget;

                _netGateTarget.Value = target;
                gameData.SwitchTargetCount = target;

                CSDebug.Log($"[SwitchbackGateMonitor] Server set gate target: {target}");
            }
            else if (_netGateTarget.Value > 0)
            {
                // Late start on a client that already replicated the value.
                gameData.SwitchTargetCount = _netGateTarget.Value;
            }

            UpdateRemainingUI();
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // Delegated to the mode's ScoringRule: the first active domain whose LEAD RUNNER has
            // threaded every gate wins.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate()
        {
            // Polled at _updateInterval rather than subscribed to each RoundStats' change event:
            // those live on the persistent Player object and a turn-end-gated unsubscribe leaks
            // into the next match (Docs/ScoringSystem/BUGS.md B15).
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
