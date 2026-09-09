using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Skein. The GATE COUNT - how many switches the course has, and
    /// therefore how many a pilot must thread to finish it - is resolved at
    /// <see cref="StartMonitor"/> from <see cref="EndConditionOverridesSO"/> (FrogletTools &gt;
    /// Game Modes &gt; End Game Conditions; never a per-scene field), synced to every client via
    /// NetworkVariable, and published to <see cref="GameDataSO.SwitchTargetCount"/>. Structural
    /// clone of <see cref="SalvoPrismTurnMonitor"/> reading its own overrides key.
    ///
    /// <para><b>The COURSE is the authority, not the override.</b> The controller asks the same
    /// overrides key for how many rings to lay, but a shell too tight for that many makes it back
    /// off (<c>SkeinController.GenerateAndBroadcastCourse</c>) - and a target naming a ring
    /// that does not exist is unreachable, which is a match that cannot end. So this reads the
    /// controller's <c>AuthoritativeGateCount</c> and falls back to the override only before the
    /// course exists. The two can then never disagree by construction rather than by agreement.</para>
    ///
    /// <para>The display channel publishes the LOCAL player's OWN remaining rings
    /// (<c>ScoringRuleSO.RemainingForPlayer</c>). This mode folds a domain by its BEST pilot, so
    /// a domain reading here would show a trailing teammate the ace's progress while their own
    /// objective arrow pointed several rings back.</para>
    /// </summary>
    public class SkeinRingTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netRingTarget = new(0);

        void OnEnable()
        {
            _netRingTarget.OnValueChanged += OnRingTargetSynced;
        }

        void OnDisable()
        {
            _netRingTarget.OnValueChanged -= OnRingTargetSynced;
        }

        void OnRingTargetSynced(int previousValue, int newValue)
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
                    ? overrides.GetSkeinRingTarget()
                    : EndConditionOverridesSO.DefaultSkeinRingTarget;

                // The course has been generated since OnNetworkSpawn, so its length is known and
                // is the honest target - see the class summary. One scene lookup at turn start,
                // never a hot path.
                var controller = FindFirstObjectByType<SkeinController>(FindObjectsInactive.Include);
                int laid = controller != null ? controller.AuthoritativeGateCount : 0;
                if (laid > 0 && laid != target)
                {
                    CSDebug.LogWarning($"[SkeinRingMonitor] Authored target {target} but the " +
                                       $"course laid {laid} rings - racing to {laid}, the number " +
                                       "of rings that actually exist.");
                    target = laid;
                }
                else if (laid > 0)
                {
                    target = laid;
                }

                _netRingTarget.Value = target;
                gameData.SwitchTargetCount = target;

                CSDebug.Log($"[SkeinRingMonitor] Server set ring target: {target}");
            }
            else if (_netRingTarget.Value > 0)
            {
                // Late start on a client that already replicated the value.
                gameData.SwitchTargetCount = _netRingTarget.Value;
            }

            UpdateRemainingUI();
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // Deleringd to the mode's ScoringRule: the first active domain whose LEAD RUNNER has
            // threaded every ring wins.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate()
        {
            // Polled at _updateInterval rather than subscribed to each RoundStats' change event:
            // those live on the persistent Player object and a turn-end-ringd unsubscribe leaks
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
