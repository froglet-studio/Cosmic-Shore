using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turn monitor for Wildlife Liberation. The KILL TARGET - how many creatures a DOMAIN
    /// must kill between them to win (default 30) - is resolved at <see cref="StartMonitor"/> from
    /// <see cref="EndConditionOverridesSO"/> (FrogletTools ▸ Game Modes ▸ End Game Conditions;
    /// never a per-scene field, per the /EndGameConditions skill), synced to every client via
    /// NetworkVariable, and published to <see cref="GameDataSO.LifeformTargetCount"/>.
    ///
    /// The turn ends (server-side) when the mode's
    /// <see cref="ScoringRuleSO.IsObjectiveReached"/> reports that an active DOMAIN's summed
    /// kills have reached it - the same shape every other race in this family uses.
    ///
    /// The display channel shows the LOCAL player's DOMAIN deficit ("creatures remaining"), so
    /// a teammate's kills shorten your readout too.
    /// </summary>
    public class WildlifeKillTurnMonitor : TurnMonitor
    {
        readonly NetworkVariable<int> _netKillTarget = new(0);

        void OnEnable()
        {
            _netKillTarget.OnValueChanged += OnKillTargetSynced;
        }

        void OnDisable()
        {
            _netKillTarget.OnValueChanged -= OnKillTargetSynced;
        }

        void OnKillTargetSynced(int previousValue, int newValue)
        {
            if (newValue > 0)
                gameData.LifeformTargetCount = newValue;
        }

        public override void StartMonitor()
        {
            base.StartMonitor();

            if (IsServer)
            {
                var overrides = EndConditionOverridesSO.Instance;
                int target = overrides != null
                    ? overrides.GetWildlifeKillTarget()
                    : EndConditionOverridesSO.DefaultWildlifeKillTarget;

                _netKillTarget.Value = target;
                gameData.LifeformTargetCount = target;

                CSDebug.Log($"[WildlifeKillMonitor] Server set kill target: {target}");
            }
            else if (_netKillTarget.Value > 0)
            {
                // Late start on a client that already replicated the value.
                gameData.LifeformTargetCount = _netKillTarget.Value;
            }

            UpdateRemainingUI();
        }

        public override bool CheckForEndOfTurn()
        {
            if (!IsServer) return false;

            // Delegated to the mode's ScoringRule: the first DOMAIN whose summed kill count
            // reaches the target ends the hunt.
            return gameData.ScoringRule.IsObjectiveReached(gameData, out _);
        }

        protected override void RestrictedUpdate()
        {
            // Kills are bursty (a missile through a tadpole swarm lands several in one frame) but
            // the 1s display tick is plenty - and polling here means no per-stat event
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
