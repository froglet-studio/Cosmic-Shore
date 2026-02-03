using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class SingleplayerWildlifeBlitzTurnMonitor : TurnMonitor
    {
        [Header("Win Conditions")]
        [SerializeField] int targetScoreToWin = 500;
        
        [Header("References")]
        [SerializeField] CellDataSO cellData;

        [Header("Events")]
        [SerializeField] ScriptableEventInt onSetScoreTargetEvent;
        [SerializeField] ScriptableEventString onLifeFormCounterUpdatedEvent;
        
        bool _didPlayerWin;
        public bool DidPlayerWin => _didPlayerWin;

        public override void StartMonitor()
        {
            _didPlayerWin = false;
            base.StartMonitor();
  
            if (onSetScoreTargetEvent)
                onSetScoreTargetEvent.Raise(targetScoreToWin);
        }

        public override bool CheckForEndOfTurn()
        {
            if (!gameData || gameData.LocalRoundStats == null) return false;

            if (!(gameData.LocalRoundStats.Score >= targetScoreToWin)) return false;
            _didPlayerWin = true;
            return true;
        }

        protected override void RestrictedUpdate()
        {
            UpdateUI();
        }
        
        void UpdateUI()
        {
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