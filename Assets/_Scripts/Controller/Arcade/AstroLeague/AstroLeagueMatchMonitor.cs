using System;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Match clock for Astro League. Counts down regulation time on the shared
    /// turn-monitor display channel ("M:SS"), pauses during kickoffs/celebrations
    /// (via TurnMonitor.Pause/Resume), and reports expiry to the match controller —
    /// which decides between full time and golden-goal overtime. The turn only ends
    /// when the controller calls ForceEnd().
    /// </summary>
    public class AstroLeagueMatchMonitor : TurnMonitor
    {
        float remainingSeconds;
        bool clockExpired;
        bool forceEnd;
        bool inOvertime;
        bool clockPaused;

        /// <summary>Raised once when the regulation clock hits zero.</summary>
        public event Action OnClockExpired;

        public float RemainingSeconds => remainingSeconds;

        /// <summary>Set the regulation length before StartMonitor (from AstroLeagueSettingsSO).</summary>
        public void ConfigureDuration(float seconds) => remainingSeconds = Mathf.Max(1f, seconds);

        /// <summary>
        /// Halts/resumes the countdown without touching the TurnMonitor loop —
        /// base Pause() kills the async loop permanently (Resume only flips a flag),
        /// so kickoff/celebration freezes must go through this instead.
        /// </summary>
        public void SetClockPaused(bool paused) => clockPaused = paused;

        /// <summary>Switches the display to sudden-death overtime mode.</summary>
        public void EnterOvertime()
        {
            inOvertime = true;
            UpdateDisplay();
        }

        /// <summary>Ends the turn on the next TurnMonitorController poll.</summary>
        public void ForceEnd() => forceEnd = true;

        public override bool CheckForEndOfTurn() => forceEnd;

        public override void StartMonitor()
        {
            clockExpired = false;
            forceEnd = false;
            inOvertime = false;
            clockPaused = false;
            UpdateDisplay();
            base.StartMonitor();
        }

        protected override void RestrictedUpdate()
        {
            if (clockPaused || inOvertime || clockExpired) return;

            remainingSeconds -= _updateInterval;
            UpdateDisplay();

            if (remainingSeconds > 0f) return;

            clockExpired = true;
            OnClockExpired?.Invoke();
        }

        protected override void ResetState()
        {
            clockExpired = false;
            forceEnd = false;
            inOvertime = false;
            clockPaused = false;
        }

        void UpdateDisplay()
        {
            string display = inOvertime
                ? "OT"
                : $"{Mathf.Max(0, (int)remainingSeconds) / 60}:{Mathf.Max(0, (int)remainingSeconds) % 60:00}";
            onUpdateTurnMonitorDisplay?.Raise(display);
        }
    }
}
