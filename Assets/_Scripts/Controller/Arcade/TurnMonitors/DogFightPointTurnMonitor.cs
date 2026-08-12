using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Dog Fight. The POINT TARGET - how many gunnery points a domain must
    /// bank to win (default 500, where a bullet hit is 1 and a missile hit is 50) - is resolved
    /// at <see cref="StartMonitor"/> from <see cref="EndConditionOverridesSO"/>
    /// (FrogletTools ▸ Game Modes ▸ End Game Conditions; never a per-scene field, per the
    /// /EndGameConditions skill), synced to every client via NetworkVariable, and published to
    /// <see cref="GameDataSO.CombatPointTargetCount"/>.
    ///
    /// The turn ends (server-side) when the mode's
    /// <see cref="DogFightScoringRuleSO.IsObjectiveReached"/> reports an active domain's point
    /// sum has reached the target. The display channel shows the LOCAL player's DOMAIN deficit -
    /// unlike the Wildlife Liberation hunt this is a team race, so a wingman's rocket really
    /// does count down your match.
    /// </summary>
    public class DogFightPointTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netPointTarget = new(0);

        void OnEnable()
        {
            _netPointTarget.OnValueChanged += OnPointTargetSynced;
        }

        void OnDisable()
        {
            _netPointTarget.OnValueChanged -= OnPointTargetSynced;
        }

        void OnPointTargetSynced(int previousValue, int newValue)
        {
            if (newValue > 0)
                gameData.CombatPointTargetCount = newValue;
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (IsServer)
            {
                var overrides = EndConditionOverridesSO.Instance;
                int target = overrides != null
                    ? overrides.GetDogFightPointTarget()
                    : EndConditionOverridesSO.DefaultDogFightPointTarget;

                _netPointTarget.Value = target;
                gameData.CombatPointTargetCount = target;

                CSDebug.Log($"[DogFightPointMonitor] Server set point target: {target}");
            }
            else if (_netPointTarget.Value > 0)
            {
                // Late start on a client that already replicated the value.
                gameData.CombatPointTargetCount = _netPointTarget.Value;
            }

            UpdateRemainingUI();
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // End condition delegated to the mode's ScoringRule: the first active domain whose
            // CombatPoints sum reaches the target wins.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate()
        {
            // Points are bursty - a rocket into a pair of opponents banks 100 in one frame -
            // but the 1s display tick is plenty, and polling here means no per-stat event
            // subscriptions to leak into the next scene (Docs/ScoringSystem/BUGS.md B15 class).
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
