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

        /// <summary>
        /// Re-authors the countdown and restarts it from zero. The extension point for a mode
        /// whose match length is authored centrally rather than on the scene component -
        /// <see cref="DrumfireTimeTurnMonitor"/> reads it from
        /// <c>EndConditionOverridesSO</c>, the one place every other mode's end-game count
        /// lives, and a per-scene <c>duration</c> would be a second authority for the same
        /// number. A negative value is ignored so a missing override cannot zero the clock and
        /// end the turn on its first tick.
        /// </summary>
        protected void SetDuration(float seconds)
        {
            if (seconds <= 0f) return;
            duration = seconds;
            elapsedTime = 0f;
        }

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

        /// <inheritdoc/>
        public override bool PublishesSecondsRemaining => true;
    }
}