using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.ScriptableObjects;
using System.Linq;
namespace CosmicShore.Gameplay
{
    public class TimeBasedTurnMonitor : TurnMonitor
    {
        [SerializeField] float duration;
        float elapsedTime;

        public float ElapsedTime => elapsedTime;
        public float Duration => duration;
        public float TimeRemaining => Mathf.Max(0, duration - elapsedTime);

        public override bool CheckForEndOfTurn() => elapsedTime >= duration;

        public override void StartMonitor()
        {
            elapsedTime = 0;
            UpdateTimerUI();
            base.StartMonitor();
        }
        
        protected override void RestrictedUpdate()
        {
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