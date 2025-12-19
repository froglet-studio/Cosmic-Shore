using CosmicShore.Core;
using CosmicShore.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class AllLifeFormsDestroyedTurnMonitor : TurnMonitor
    {
        [SerializeField] 
        CellDataSO cellData;
        
        public override bool CheckForEndOfTurn()
        {
            // Check if any life forms exist in the current node
            return cellData.CellStatsList[cellData.Cell.ID].LifeFormsInCell <= 0;
        }

        public override void StartMonitor()
        {
            UpdateUI();
        }

        protected override void RestrictedUpdate()
        {
            UpdateUI();
        }

        void UpdateUI()
        {
            string message = (cellData.CellStatsList[cellData.Cell.ID].LifeFormsInCell).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}
