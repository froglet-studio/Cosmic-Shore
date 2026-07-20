using Obvious.Soap;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wildlife Blitz objective monitor. The clear TARGET is the cell's authored
    /// <c>CellEndGameScore</c> (an ecology config, not an End Game Conditions count),
    /// resolved at <see cref="StartMonitor"/> and synced/published into
    /// <see cref="GameDataSO.GoalTargetCount"/> via the base target leg. The turn ends
    /// (server-side, sealed in <see cref="ObjectiveTurnMonitor"/>) when the rule reports
    /// the TEAM's summed blitz composite reached the target; the loss clock is the
    /// scene's NetworkTimeBasedTurnMonitor. Also feeds the blitz HUD: the score-target
    /// event at turn start and the lifeforms-in-cell counter on the 1s display tick
    /// (cell data is peer-local - no per-stat subscriptions to leak).
    /// </summary>
    public class WildlifeBlitzObjectiveTurnMonitor : ObjectiveTurnMonitor
    {
        [Header("References")]
        [SerializeField] CellRuntimeDataSO cellData;

        [Header("Events")]
        [SerializeField] ScriptableEventInt onSetScoreTargetEvent;
        [SerializeField] ScriptableEventString onLifeFormCounterUpdatedEvent;

        public override void StartMonitor()
        {
            base.StartMonitor();

            int target = cellData && cellData.Config ? cellData.Config.CellEndGameScore : 0;
            SyncTarget(target);

            if (onSetScoreTargetEvent)
                onSetScoreTargetEvent.Raise(gameData.GoalTargetCount);

            RaiseRemainingUI();
        }

        protected override void PublishTarget(int value) => gameData.GoalTargetCount = value;

        protected override void RestrictedUpdate()
        {
            RaiseRemainingUI();
            UpdateLifeFormCounter();
        }

        void UpdateLifeFormCounter()
        {
            if (!onLifeFormCounterUpdatedEvent) return;
            onLifeFormCounterUpdatedEvent.Raise(GetLifeFormsSafe().ToString());
        }

        int GetLifeFormsSafe()
        {
            if (!cellData || !cellData.Cell) return 0;
            int id = cellData.Cell.ID;
            return id < 0 ? 0 : cellData.GetLifeFormsInCellSafe(id);
        }
    }
}
