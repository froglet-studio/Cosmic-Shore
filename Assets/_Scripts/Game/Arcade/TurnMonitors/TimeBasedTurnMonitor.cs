using UnityEngine;

namespace CosmicShore.Game.Arcade
{
    public class TimeBasedTurnMonitor : TurnMonitor
    {
        [SerializeField] float duration;
        float elapsedTime;

        public override bool CheckForEndOfTurn() => elapsedTime >= duration;

        public override void StartMonitor()
        {
            elapsedTime = 0;
            UpdateTimerUI();
            base.StartMonitor();
        }
        
        protected override void RestrictedUpdate()
        {
            base.RestrictedUpdate();
            elapsedTime += _updateInterval;
            UpdateTimerUI();
        }

        protected virtual void UpdateTimerUI() =>
            InvokeUpdateTurnMonitorDisplay(GetTimeToDisplay());

        protected void InvokeUpdateTurnMonitorDisplay(string message) =>
            onUpdateTurnMonitorDisplay?.Raise(message);
        
        protected string GetTimeToDisplay() => 
            ((int)duration - (int)elapsedTime).ToString();
    }
}