using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class AllLifeFormsDestroyedTurnMonitor : TurnMonitor
    {
        [SerializeField] CellDataSO cellData;
        [SerializeField] ScriptableEventString onLifeFormCounterUpdatedEvent;

        int CurrentCellIdOrMinusOne()
        {
            if (!cellData || !cellData.Cell) return -1;
            return cellData.Cell.ID;
        }

        int GetLifeFormsSafe()
        {
            int id = CurrentCellIdOrMinusOne();
            if (id < 0) return 0;
            return cellData.GetLifeFormsInCellSafe(id);
        }

        public override bool CheckForEndOfTurn()
        {
            return GetLifeFormsSafe() <= 0;
        }

        public override void StartMonitor()
        {
            base.StartMonitor();
            UpdateUI();
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
    }
}