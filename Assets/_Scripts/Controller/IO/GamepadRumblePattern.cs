namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A gamepad rumble envelope: a run of segments, each holding both motors at a fixed speed
    /// for a fixed number of milliseconds.
    ///
    /// <para>First-party replacement for the vendor <c>GamepadRumble</c> struct, kept to the same
    /// shape on purpose — the four envelopes in <see cref="HapticController"/> were authored
    /// against these three parallel arrays and are carried across unchanged. The struct is a
    /// plain data carrier: everything about how it is PLAYED lives in
    /// <see cref="GamepadRumblePlayer"/>.</para>
    /// </summary>
    public readonly struct GamepadRumblePattern
    {
        /// <summary>How long, in milliseconds, segment <c>i</c> holds its two motor speeds.</summary>
        public readonly int[] DurationsMs;

        /// <summary>Segment <c>i</c>'s speed for the low-frequency (heavy) motor, 0..1.</summary>
        public readonly float[] LowFrequencyMotorSpeeds;

        /// <summary>Segment <c>i</c>'s speed for the high-frequency (bright) motor, 0..1.</summary>
        public readonly float[] HighFrequencyMotorSpeeds;

        /// <summary>
        /// The whole envelope's length in milliseconds — the sum of <see cref="DurationsMs"/>,
        /// derived here rather than authored beside it so the two can never disagree.
        /// </summary>
        public readonly int TotalDurationMs;

        public GamepadRumblePattern(int[] durationsMs, float[] low, float[] high)
        {
            DurationsMs = durationsMs;
            LowFrequencyMotorSpeeds = low;
            HighFrequencyMotorSpeeds = high;

            TotalDurationMs = 0;
            if (durationsMs != null)
                for (int i = 0; i < durationsMs.Length; i++)
                    TotalDurationMs += durationsMs[i];
        }

        /// <summary>
        /// True when the three arrays are present, the same length, and non-empty — the player
        /// indexes all three with one index, so a ragged pattern is not playable.
        /// </summary>
        public bool IsValid =>
            DurationsMs != null &&
            LowFrequencyMotorSpeeds != null &&
            HighFrequencyMotorSpeeds != null &&
            DurationsMs.Length == LowFrequencyMotorSpeeds.Length &&
            DurationsMs.Length == HighFrequencyMotorSpeeds.Length &&
            DurationsMs.Length > 0;
    }
}
