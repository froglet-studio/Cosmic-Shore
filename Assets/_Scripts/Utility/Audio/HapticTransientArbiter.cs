using System;
using System.Collections.Generic;

namespace CosmicShore.Utility
{
    /// <summary>
    /// Admission control for the single haptic channel.
    ///
    /// The Lofelt runtime can hold exactly ONE loaded clip — every
    /// <c>Load()</c> evicts whatever is playing. Without arbitration, a burst of
    /// prism-break haptics would eat a mine explosion, and UI ticks would chop
    /// gameplay rumble mid-swell. The arbiter decides, purely from
    /// (priority, strength, time), whether a new transient may take the channel.
    ///
    /// Pure C# — no Unity dependency — so the policy is edit-mode testable.
    /// </summary>
    public sealed class HapticTransientArbiter
    {
        /// <summary>A request to play one transient haptic on the shared channel.</summary>
        public struct Request
        {
            /// <summary>Identity used for per-category cooldown tracking.</summary>
            public int CategoryKey;

            /// <summary>Higher wins the channel. Ties resolve by strength.</summary>
            public int Priority;

            /// <summary>Effective 0..1 playback strength (event gain × config gain × distance attenuation).</summary>
            public float Strength;

            /// <summary>Clip duration in seconds — how long the channel stays claimed.</summary>
            public float Duration;

            /// <summary>Minimum seconds between two admissions of the same CategoryKey.</summary>
            public float CooldownSeconds;
        }

        public struct Settings
        {
            /// <summary>Requests weaker than this are inaudible on an actuator — rejected outright.</summary>
            public float MinStrength;

            /// <summary>Global floor between any two admissions; keeps rapid-fire events from thrashing Load().</summary>
            public float GlobalMinIntervalSeconds;

            /// <summary>An equal-priority request must be at least this fraction of the playing strength to steal the channel.</summary>
            public float EqualPriorityStealFactor;

            /// <summary>When the playing transient has less than this left, anything admissible may take over (its tail is spent).</summary>
            public float TailSeconds;

            public static Settings Default => new Settings
            {
                MinStrength = 0.02f,
                GlobalMinIntervalSeconds = 0.03f,
                EqualPriorityStealFactor = 0.8f,
                TailSeconds = 0.06f,
            };
        }

        readonly Dictionary<int, float> _lastAdmittedByCategory = new();
        float _lastAdmitTime = float.NegativeInfinity;

        int _playingPriority;
        float _playingStrength;
        float _playingEndTime = float.NegativeInfinity;

        /// <summary>Whether a previously admitted transient still claims the channel at <paramref name="now"/>.</summary>
        public bool IsPlaying(float now) => now < _playingEndTime;

        /// <summary>Seconds until the channel frees up (0 when idle).</summary>
        public float RemainingSeconds(float now) => Math.Max(0f, _playingEndTime - now);

        /// <summary>
        /// Decides whether <paramref name="request"/> may take the channel at
        /// time <paramref name="now"/>, and records it as playing when admitted.
        /// </summary>
        public bool TryAdmit(in Request request, float now, in Settings settings)
        {
            if (request.Strength < settings.MinStrength)
                return false;

            if (_lastAdmittedByCategory.TryGetValue(request.CategoryKey, out float lastForCategory)
                && now - lastForCategory < request.CooldownSeconds)
                return false;

            bool channelBusy = IsPlaying(now);

            if (channelBusy && RemainingSeconds(now) > settings.TailSeconds)
            {
                if (request.Priority < _playingPriority)
                    return false;
                if (request.Priority == _playingPriority
                    && request.Strength < _playingStrength * settings.EqualPriorityStealFactor)
                    return false;
            }

            // The global interval guards against Load() thrash from same-frame
            // bursts; a strictly higher priority event may always punch through.
            if (now - _lastAdmitTime < settings.GlobalMinIntervalSeconds
                && !(channelBusy && request.Priority > _playingPriority)
                && _lastAdmitTime <= now)
                return false;

            _lastAdmitTime = now;
            _lastAdmittedByCategory[request.CategoryKey] = now;
            _playingPriority = request.Priority;
            _playingStrength = request.Strength;
            _playingEndTime = now + Math.Max(0f, request.Duration);
            return true;
        }

        /// <summary>Frees the channel early (external stop, settings toggle, focus loss).</summary>
        public void NotifyStopped()
        {
            _playingEndTime = float.NegativeInfinity;
        }

        /// <summary>Full reset — also clears per-category cooldowns.</summary>
        public void Reset()
        {
            _lastAdmittedByCategory.Clear();
            _lastAdmitTime = float.NegativeInfinity;
            _playingEndTime = float.NegativeInfinity;
            _playingPriority = 0;
            _playingStrength = 0f;
        }
    }
}
