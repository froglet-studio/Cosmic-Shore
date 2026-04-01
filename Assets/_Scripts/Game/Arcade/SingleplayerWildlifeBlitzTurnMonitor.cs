using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class SingleplayerWildlifeBlitzTurnMonitor : TurnMonitor
    {
        // Internal tracking variable, not serialized
        private int currentTargetScore;
        
        [Header("References")]
        [SerializeField] CellRuntimeDataSO cellData;

        [Header("Events")]
        [SerializeField] ScriptableEventInt onSetScoreTargetEvent;
        [SerializeField] ScriptableEventString onLifeFormCounterUpdatedEvent;
        
        bool _didPlayerWin;
        public bool DidPlayerWin => _didPlayerWin;

        public override void StartMonitor()
        {
            _didPlayerWin = false;
            base.StartMonitor();
  
            // [Visual Note] Fetch Dynamic Score from the active Cell Type
            SetTargetScoreFromCellType();

            if (onSetScoreTargetEvent)
                onSetScoreTargetEvent.Raise(currentTargetScore);
        }

        void SetTargetScoreFromCellType()
        {
            if (!cellData || !cellData.Config) return;
            currentTargetScore = cellData.Config.CellEndGameScore;
        }

        public override bool CheckForEndOfTurn()
        {
            if (!gameData || gameData.LocalRoundStats == null) return false;
            if (!(gameData.LocalRoundStats.Score >= currentTargetScore)) return false;
            
            _didPlayerWin = true;
            return true;
        }

        protected override void RestrictedUpdate()
        {
            UpdateUI();
        }
        
        void UpdateUI()
        {
            if (!onLifeFormCounterUpdatedEvent) return;
            string message = GetLifeFormsSafe().ToString();
            onLifeFormCounterUpdatedEvent.Raise(message);
        }
        
        int CurrentCellIdOrMinusOne()
        {
            if (!cellData || !cellData.Cell) return -1;
            return cellData.Cell.ID;
        }

        int GetLifeFormsSafe()
        {
            int id = CurrentCellIdOrMinusOne();
            return id < 0 ? 0 : cellData.GetLifeFormsInCellSafe(id);
        }
        
        protected override void ResetState()
        {
            _didPlayerWin = false;
        }
    }
}