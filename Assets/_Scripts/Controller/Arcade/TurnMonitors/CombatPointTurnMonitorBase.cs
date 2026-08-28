using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shared turn monitor for every mode that races on <see cref="IRoundStats.CombatPoints"/> -
    /// the vessel-vs-vessel score. Two modes ride it today and they differ in exactly one thing:
    /// where the TARGET comes from.
    ///
    ///   • <see cref="DogFightPointTurnMonitor"/> - gunnery (bullets 1 / rockets 50).
    ///   • <see cref="BendsPointTurnMonitor"/> - bends (an opposing pilot caught in your
    ///     Dolphin crystal blast, 10 each).
    ///
    /// Everything else is identical and deliberately lives here once: the target is resolved
    /// server-side from <see cref="ScriptableObjects.EndConditionOverridesSO"/> (FrogletTools ▸
    /// Game Modes ▸ End Game Conditions; never a per-scene field, per the /EndGameConditions
    /// skill), synced to every client through one NetworkVariable, published to
    /// <see cref="GameDataSO.CombatPointTargetCount"/>, and the turn ends when the MODE'S OWN
    /// <c>ScoringRuleSO.IsObjectiveReached</c> says a domain has arrived - so the weighting stays
    /// the rule's business and the monitor never learns what a hit is worth.
    ///
    /// The display channel shows the LOCAL player's DOMAIN deficit: both modes are team races,
    /// so a wingman's hit really does count down your match.
    /// </summary>
    public abstract class CombatPointTurnMonitorBase : TurnMonitor
    {
        readonly NetworkVariable<int> _netPointTarget = new(0);

        /// <summary>The mode's point target, read on the SERVER from the end-conditions asset.</summary>
        protected abstract int ResolvePointTarget();

        /// <summary>Log prefix, so a mis-resolved target names the mode that resolved it.</summary>
        protected abstract string LogTag { get; }

        void OnEnable()  => _netPointTarget.OnValueChanged += OnPointTargetSynced;
        void OnDisable() => _netPointTarget.OnValueChanged -= OnPointTargetSynced;

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
                int target = ResolvePointTarget();
                _netPointTarget.Value = target;
                gameData.CombatPointTargetCount = target;

                CSDebug.Log($"[{LogTag}] Server set point target: {target}");
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
            // Points are bursty - one blast into a pair of opponents banks two hits in a frame -
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
