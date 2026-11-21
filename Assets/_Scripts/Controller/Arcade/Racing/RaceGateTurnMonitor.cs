using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for any GATE RACE (Switchback's open chain, Headlong's lapped circuit). The
    /// RACE LENGTH - how many gate threadings finish it - is resolved at
    /// <see cref="StartMonitor"/>, synced to every client via NetworkVariable, and published to
    /// <see cref="GameDataSO.SwitchTargetCount"/>.
    ///
    /// <para><b>The COURSE is the authority, not the override, and the CONTROLLER is asked for
    /// both.</b> Each mode's controller reads its own end-condition key to size its course, and a
    /// course can come out shorter than asked (Switchback backs off when a shell is too tight) or
    /// longer (Headlong rounds its ring count up so a lap is whole). A target naming a gate that
    /// does not exist is unreachable, which is a match that cannot end - so this reads
    /// <c>GateRaceController.AuthoritativeGateCount</c> and falls back to that controller's
    /// authored target only before the course exists. One authority, asked twice; the monitor
    /// deliberately does NOT know which overrides key its mode uses.</para>
    ///
    /// <para>The display channel publishes the LOCAL player's OWN remaining gates
    /// (<c>ScoringRuleSO.RemainingForPlayer</c>). This mode folds a domain by its BEST pilot, so
    /// a domain reading here would show a trailing teammate the ace's progress while their own
    /// objective arrow pointed several gates back.</para>
    /// </summary>
    public class RaceGateTurnMonitor : TurnMonitor
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
                // One scene lookup at turn start, never a hot path.
                var controller = FindFirstObjectByType<GateRaceController>(FindObjectsInactive.Include);

                int target = controller != null
                    ? controller.AuthoredGateTarget()
                    : EndConditionOverridesSO.DefaultSwitchbackGateTarget;

                // The course has been generated since OnNetworkSpawn, so its length is known and
                // is the honest target - see the class summary.
                int laid = controller != null ? controller.AuthoritativeGateCount : 0;
                if (laid > 0 && laid != target)
                {
                    CSDebug.LogWarning($"[RaceGateMonitor] Authored target {target} but the " +
                                       $"course yields {laid} - racing to {laid}, the number " +
                                       "of gate threadings that actually exist.");
                    target = laid;
                }
                else if (laid > 0)
                {
                    target = laid;
                }

                _netGateTarget.Value = target;
                gameData.SwitchTargetCount = target;

                CSDebug.LogVerbose(CSLogChannel.ArcadeMatch, $"[RaceGateMonitor] Server set gate target: {target}");
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
