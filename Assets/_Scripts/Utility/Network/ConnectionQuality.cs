namespace CosmicShore.Utility
{
    public enum ConnectionQualityLevel { Unknown, Good, Fair, Poor }

    /// <summary>
    /// Pure RTT thresholds shared by the host-side RTT sampler (<c>Player.NetRttMs</c>), the
    /// multiplayer debug overlay and the bad-connection badge. See
    /// Docs/Multiplayer/MULTIPLAYER_ARCHITECTURE.md §2.5.
    /// </summary>
    public static class ConnectionQuality
    {
        /// <summary>At or below this the connection is unremarkable.</summary>
        public const int GoodMaxMs = 100;
        /// <summary>Above this the player is warned ("bad connection").</summary>
        public const int PoorAboveMs = 200;
        /// <summary>How often the host re-samples each client's RTT.</summary>
        public const float SampleIntervalSeconds = 1f;

        public static ConnectionQualityLevel Classify(int rttMs)
        {
            if (rttMs <= 0) return ConnectionQualityLevel.Unknown;
            if (rttMs <= GoodMaxMs) return ConnectionQualityLevel.Good;
            if (rttMs <= PoorAboveMs) return ConnectionQualityLevel.Fair;
            return ConnectionQualityLevel.Poor;
        }

        public static bool IsBad(int rttMs) => Classify(rttMs) == ConnectionQualityLevel.Poor;

        /// <summary>Clamp a transport RTT (ulong ms) into the ushort the network variable carries.</summary>
        public static ushort ToWire(ulong rttMs) => rttMs > ushort.MaxValue ? ushort.MaxValue : (ushort)rttMs;

        public static string Label(int rttMs) =>
            rttMs <= 0 ? "-- ms" : rttMs + " ms (" + Classify(rttMs) + ")";
    }
}
