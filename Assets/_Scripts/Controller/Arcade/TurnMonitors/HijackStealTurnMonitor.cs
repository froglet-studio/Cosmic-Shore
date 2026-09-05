using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Hijack. The steal TARGET - how many prisms a domain must take between
    /// them to win (default 1500) - is resolved at <see cref="StartMonitor"/> from
    /// <see cref="EndConditionOverridesSO"/> (FrogletTools &gt; Game Modes &gt; End Game
    /// Conditions; never a per-scene field), synced to every client via NetworkVariable, and
    /// published to <see cref="GameDataSO.PrismTargetCount"/>. The turn ends (server-side) when
    /// the mode's <see cref="ScoringRuleSO.IsObjectiveReached"/> reports an active domain's steal
    /// sum has reached it. The display channel carries the LOCAL player's domain deficit, which
    /// the goal row renders as "STEAL PRISMS n/1500". Structural clone of
    /// <see cref="SalvoPrismTurnMonitor"/> reading its own overrides key.
    /// </summary>
    public class HijackStealTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netStealTarget = new(0);

        void OnEnable() => _netStealTarget.OnValueChanged += OnStealTargetSynced;

        void OnDisable() => _netStealTarget.OnValueChanged -= OnStealTargetSynced;

        void OnStealTargetSynced(int previousValue, int newValue)
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
                    ? overrides.GetHijackStealTarget()
                    : EndConditionOverridesSO.DefaultHijackStealTarget;

                _netStealTarget.Value = target;
                gameData.PrismTargetCount = target;

                CSDebug.Log($"[HijackStealMonitor] Server set steal target: {target}");
            }
            else if (_netStealTarget.Value > 0)
            {
                // Late start on a client that already replicated the value.
                gameData.PrismTargetCount = _netStealTarget.Value;
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
            // Stealing is CONTINUOUS while a pilot grinds hostile mass (one prism per hop), so
            // unlike a bursty destruction count this ticks steadily - a 1s display tick is still
            // plenty, and it keeps the monitor free of per-stat event subscriptions
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
