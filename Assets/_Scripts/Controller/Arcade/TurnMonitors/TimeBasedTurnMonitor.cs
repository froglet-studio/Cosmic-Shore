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
        /// whose match length is authored centrally rather than on the scene component: a
        /// subclass reads its duration from <c>EndConditionOverridesSO</c>, the one place every
        /// other mode's end-game count lives, and a per-scene <c>duration</c> would be a second
        /// authority for the same number. A negative value is ignored so a missing override
        /// cannot zero the clock and end the turn on its first tick.
        ///
        /// <para>No subclass uses it today - the only one that did was Drumfire's monitor,
        /// removed with that mode in 2026-09. Kept because the SHAPE is the platform's answer
        /// for a clock-ended mode, and <see cref="PublishesSecondsRemaining"/> below (which the
        /// goal-stack HUD reads) exists for the same reason.</para>
        /// </summary>
        protected void SetDuration(float seconds)
        {
            if (seconds <= 0f) return;
            duration = seconds;
            elapsedTime = 0f;
        }

        // TurnMonitor.RunLoopAsync ticks ONCE before its first wait. Counted, that tick put a
        // whole interval on the clock at t=0: a 120 s round showed "119" from its first frame
        // and ended at 119 s. StartMonitor already published the full time, so the t=0 tick is
        // skipped.
        bool _primed;

        public override void StartMonitor()
        {
            elapsedTime = 0;
            _primed = false;
            UpdateTimerUI();
            base.StartMonitor();
        }
        
        protected override void RestrictedUpdate()
        {
            if (!_primed)
            {
                _primed = true;
                return;
            }

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