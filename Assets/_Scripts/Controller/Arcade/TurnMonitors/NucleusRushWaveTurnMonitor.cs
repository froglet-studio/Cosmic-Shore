using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Nucleus Rush ("Brood Rush"). The wave TARGET - how many
    /// fauna waves a domain must claim to win (default 3) - is resolved at
    /// <see cref="StartMonitor"/> from <see cref="EndConditionOverridesSO"/>
    /// (Tools &gt; Cosmic Shore &gt; End Game Conditions; never a per-scene field),
    /// synced to every client via NetworkVariable, and published to
    /// <see cref="GameDataSO.GoalTargetCount"/>. The turn ends (server-side) when
    /// the mode's <see cref="ScoringRuleSO.IsObjectiveReached"/> reports an active
    /// domain's brood sum has reached the target. The display channel shows the
    /// LOCAL player's domain deficit ("broods remaining").
    /// </summary>
    public class NucleusRushWaveTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netWaveTarget = new(0);

        void OnEnable()
        {
            _netWaveTarget.OnValueChanged += OnWaveTargetSynced;
        }

        void OnDisable()
        {
            _netWaveTarget.OnValueChanged -= OnWaveTargetSynced;
        }

        void OnWaveTargetSynced(int previousValue, int newValue)
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
                    ? overrides.GetNucleusRushWaveTarget()
                    : EndConditionOverridesSO.DefaultNucleusRushWaveTarget;

                _netWaveTarget.Value = target;
                gameData.GoalTargetCount = target;

                CSDebug.Log($"[NucleusRushWaveMonitor] Server set wave target: {target}");
            }
            else if (_netWaveTarget.Value > 0)
            {
                // Late start on a client that already replicated the value.
                gameData.GoalTargetCount = _netWaveTarget.Value;
            }

            UpdateRemainingUI();
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // End condition delegated to the mode's ScoringRule: first active domain
            // whose brood (GoalsScored) sum reaches the target ends the race.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate()
        {
            // Brood points land from the wave event (30s cadence), so a 1s display
            // tick is plenty - no per-stat event subscriptions to leak (B15 class).
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
