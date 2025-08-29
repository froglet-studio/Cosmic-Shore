using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class AllLifeFormsDestroyedTurnMonitor : TurnMonitor
    {
        int cellID;

        private void Awake()
        {
            // eliminatesPlayer = true; // This monitor eliminates players when they destroy all life forms
        }

        private void Start()
        {
            if (CellControlManager.Instance != null) cellID = CellControlManager.Instance.GetNearestCell(Vector3.zero).ID;
        }

        public override bool CheckForEndOfTurn()
        {
            // Check if any life forms exist in the current node
            return miniGameData.CellStatsList[cellID].LifeFormsInCell <= 0;
            // If we get here, all life forms have been destroyed
        }

        protected override void StartTurn()
        {
            // StatsManager.Instance.ResetStats();
        }

        protected override void RestrictedUpdate()
        {
            string message = (miniGameData.CellStatsList[cellID].LifeFormsInCell).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}
