using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class AllLifeFormsDestroyedTurnMonitor : TurnMonitor
    {
        string cellID;

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
            if (StatsManager.Instance.CellStats[cellID].LifeFormsInNode > 0)
            {
                return false;
            }

            // If we get here, all life forms have been destroyed
            return true;
        }

        protected override void StartTurn()
        {
            // StatsManager.Instance.ResetStats();
        }

        protected override void RestrictedUpdate()
        {
            string message = (StatsManager.Instance.CellStats[cellID].LifeFormsInNode).ToString();
            onUpdateTurnMonitorDisplay.Raise(message);
        }
    }
}
