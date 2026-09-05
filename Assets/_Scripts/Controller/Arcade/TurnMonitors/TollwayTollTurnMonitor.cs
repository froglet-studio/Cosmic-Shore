using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Tollway. The TOLL TARGET — how many tolls a domain must collect to win
    /// (default 12) — is resolved at <see cref="StartMonitor"/> from
    /// <see cref="EndConditionOverridesSO"/> (FrogletTools ▸ Game Modes ▸ End Game Conditions;
    /// never a per-scene field), synced to every client via NetworkVariable, and published to
    /// <see cref="GameDataSO.GoalTargetCount"/> — the same counter Astro League and Scramble
    /// publish, because Tollway reuses <see cref="ScoringMetric.Goals"/>.
    ///
    /// The turn ends (server-side) when the mode's <see cref="ScoringRuleSO.IsObjectiveReached"/>
    /// reports an active domain's toll sum has reached the target. The display channel shows the
    /// LOCAL player's domain deficit. Structural clone of <c>ScarabScrambleGoalTurnMonitor</c>
    /// reading its own overrides key.
    ///
    /// <see cref="TurnMonitor.PublishesSecondsRemaining"/> is left at its false default: this
    /// publishes a COUNT, and the top bar's goal-stack row keys off that flag rather than
    /// parsing the string.
    /// </summary>
    public class TollwayTollTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netTollTarget = new(0);

        void OnEnable()
        {
            _netTollTarget.OnValueChanged += OnTollTargetSynced;
        }

        void OnDisable()
        {
            _netTollTarget.OnValueChanged -= OnTollTargetSynced;
        }

        void OnTollTargetSynced(int previousValue, int newValue)
        {
            if (newValue > 0)
                gameData.GoalTargetCount = newValue;
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (IsServer)
            {
                var overrides = EndConditionOverridesSO.Instance;
                int target = overrides != null
                    ? overrides.GetTollwayTollTarget()
                    : EndConditionOverridesSO.DefaultTollwayTollTarget;

                _netTollTarget.Value = target;
                gameData.GoalTargetCount = target;
            }
            else if (_netTollTarget.Value > 0)
            {
                // Late start on a client that already replicated the value.
                gameData.GoalTargetCount = _netTollTarget.Value;
            }

            UpdateRemainingUI();
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // End condition delegated to the mode's ScoringRule: the first active domain whose
            // toll sum reaches the target wins.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate()
        {
            // Poll on the 1s display tick rather than subscribing per-stat events — nothing to
            // leak into the next scene (Docs/ScoringSystem/BUGS.md B15 class).
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
