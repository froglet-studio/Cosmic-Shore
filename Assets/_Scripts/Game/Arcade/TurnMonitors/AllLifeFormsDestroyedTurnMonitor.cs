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
            return gameData.CellStatsList[cellID].LifeFormsInCell <= 0;
            // If we get here, all life forms have been destroyed
        }

        public override void StartMonitor()
        {
            // StatsManager.Instance.ResetStats();
            UpdateUI();
        }

        protected override void RestrictedUpdate()
        {
            UpdateUI();
        }

        void UpdateUI()
        {
            string message = (gameData.CellStatsList[cellID].LifeFormsInCell).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}
