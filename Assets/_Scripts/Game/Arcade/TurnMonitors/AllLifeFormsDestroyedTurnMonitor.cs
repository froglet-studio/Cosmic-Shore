using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class AllLifeFormsDestroyedTurnMonitor : TurnMonitor
    {
        string nodeID;

        private void Awake()
        {
            eliminatesPlayer = true; // This monitor eliminates players when they destroy all life forms
        }

        private void Start()
        {
            if (CellControlManager.Instance != null) nodeID = CellControlManager.Instance.GetNearestCell(Vector3.zero).ID;
        }

        public override bool CheckForEndOfTurn()
        {
            if (paused) return false;

            // Check if any life forms exist in the current node
            if (StatsManager.Instance.CellStats[nodeID].LifeFormsInNode > 0)
            {
                return false;
            }

            // If we get here, all life forms have been destroyed
            return true;
        }

        public override void NewTurn(string playerName)
        {
            StatsManager.Instance.ResetStats();
        }

        void Update()
        {
            if (paused) return;

            if (Display != null)
            Display.text = (StatsManager.Instance.CellStats[nodeID].LifeFormsInNode).ToString();
        }
    }
}
