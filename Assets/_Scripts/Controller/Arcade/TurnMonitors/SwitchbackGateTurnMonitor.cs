using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

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
    /// <para><b>The COURSE is the authority, not the override.</b> The controller asks the same
    /// overrides key for how many gates to lay, but a shell too tight for that many makes it back
    /// off (<c>SwitchbackController.GenerateAndBroadcastCourse</c>) - and a target naming a gate
    /// that does not exist is unreachable, which is a match that cannot end. So this reads the
    /// controller's <c>AuthoritativeGateCount</c> and falls back to the override only before the
    /// course exists. The two can then never disagree by construction rather than by agreement.</para>
    ///
    /// <para>The display channel publishes the LOCAL player's OWN remaining gates
    /// (<c>ScoringRuleSO.RemainingForPlayer</c>). This mode folds a domain by its BEST pilot, so
    /// a domain reading here would show a trailing teammate the ace's progress while their own
    /// objective arrow pointed several gates back.</para>
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

                // The course has been generated since OnNetworkSpawn, so its length is known and
                // is the honest target - see the class summary. One scene lookup at turn start,
                // never a hot path.
                var controller = FindFirstObjectByType<SwitchbackController>(FindObjectsInactive.Include);
                int laid = controller != null ? controller.AuthoritativeGateCount : 0;
                if (laid > 0 && laid != target)
                {
                    CSDebug.LogWarning($"[SwitchbackGateMonitor] Authored target {target} but the " +
                                       $"course laid {laid} gates - racing to {laid}, the number " +
                                       "of rings that actually exist.");
                    target = laid;
                }
                else if (laid > 0)
                {
                    target = laid;
                }

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

            int remaining = gameData.ScoringRule.RemainingForPlayer(
                gameData, gameData.LocalPlayer?.RoundStats);
            onUpdateTurnMonitorDisplay.Raise(remaining.ToString());
        }
    }
}
