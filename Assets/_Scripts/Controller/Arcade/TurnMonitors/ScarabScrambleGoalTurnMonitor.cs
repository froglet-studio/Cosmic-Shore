using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Scarab Scramble. The GOAL TARGET — how many hoop goals a domain must
    /// bank to win (default 10) — is resolved at <see cref="StartMonitor"/> from
    /// <see cref="EndConditionOverridesSO"/> (FrogletTools ▸ Game Modes ▸ End Game Conditions;
    /// never a per-scene field, per the /EndGameConditions skill), synced to every client via
    /// NetworkVariable, and published to <see cref="GameDataSO.GoalTargetCount"/>.
    ///
    /// The turn ends (server-side) when the mode's scoring rule reports an active domain's
    /// goal sum has reached the target. The display channel shows the LOCAL player's DOMAIN
    /// deficit — a team race, so a teammate's goal really does count down your match.
    /// (The DogFightPointTurnMonitor shape, on the Goals metric.)
    /// </summary>
    public class ScarabScrambleGoalTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netGoalTarget = new(0);

        void OnEnable()
        {
            _netGoalTarget.OnValueChanged += OnGoalTargetSynced;
        }

        void OnDisable()
        {
            _netGoalTarget.OnValueChanged -= OnGoalTargetSynced;
        }

        void OnGoalTargetSynced(int previousValue, int newValue)
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
                    ? overrides.GetScarabScrambleGoalTarget()
                    : EndConditionOverridesSO.DefaultScarabScrambleGoalTarget;

                _netGoalTarget.Value = target;
                gameData.GoalTargetCount = target;

                CSDebug.Log($"[ScarabScrambleGoalMonitor] Server set goal target: {target}");
            }
            else if (_netGoalTarget.Value > 0)
            {
                // Late start on a client that already replicated the value.
                gameData.GoalTargetCount = _netGoalTarget.Value;
            }

            UpdateRemainingUI();
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // End condition delegated to the mode's ScoringRule: the first active domain
            // whose GoalsScored sum reaches the target wins.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate()
        {
            // Poll on the 1s display tick rather than subscribing per-stat events — nothing
            // to leak into the next scene (Docs/ScoringSystem/BUGS.md B15 class).
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
