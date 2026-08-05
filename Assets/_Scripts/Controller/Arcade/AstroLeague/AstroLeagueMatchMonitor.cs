using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-authoritative match clock for Astro League. Counts down regulation time and
    /// pushes the "M:SS" (or "OT") readout to every peer through the shared turn-monitor
    /// display channel, pauses during kickoffs/celebrations via <see cref="SetClockPaused"/>,
    /// and reports expiry to the match controller - which decides between full time and
    /// golden-goal overtime. The turn only ends when the controller calls
    /// <see cref="ForceEnd"/>; <see cref="CheckForEndOfTurn"/> is server-gated like the other
    /// network turn monitors.
    /// </summary>
    public class AstroLeagueMatchMonitor : TurnMonitor
    {
        float remainingSeconds;
        bool clockExpired;
        bool forceEnd;
        bool inOvertime;
        bool clockPaused;

        /// <summary>Server-only: raised once when the regulation clock hits zero.</summary>
        public event Action OnClockExpired;

        public float RemainingSeconds => remainingSeconds;

        /// <summary>Server: set the regulation length before StartMonitor (from AstroLeagueSettingsSO).</summary>
        public void ConfigureDuration(float seconds) => remainingSeconds = Mathf.Max(1f, seconds);

        /// <summary>
        /// Server: halts/resumes the countdown without touching the TurnMonitor loop -
        /// base Pause() stalls the async loop until Resume, so kickoff/celebration freezes
        /// go through this flag instead and the display keeps refreshing.
        /// </summary>
        public void SetClockPaused(bool paused) => clockPaused = paused;

        /// <summary>Server: switches the display to sudden-death overtime mode.</summary>
        public void EnterOvertime()
        {
            inOvertime = true;
            UpdateDisplay();
        }

        /// <summary>Server: ends the turn on the next TurnMonitorController poll.</summary>
        public void ForceEnd() => forceEnd = true;

        public override bool CheckForEndOfTurn() => IsServer && forceEnd;

        public override void StartMonitor()
        {
            clockExpired = false;
            forceEnd = false;
            inOvertime = false;
            clockPaused = false;
            if (IsServer)
                UpdateDisplay();
            base.StartMonitor();
        }

        protected override void RestrictedUpdate()
        {
            if (!IsServer) return;
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
            UpdateDisplay_ClientRpc(display);
        }

        [ClientRpc]
        void UpdateDisplay_ClientRpc(FixedString32Bytes message) =>
            onUpdateTurnMonitorDisplay?.Raise(message.ToString());
    }
}
