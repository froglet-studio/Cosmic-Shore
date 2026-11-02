namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Host-side sanity limits on client prism reports (<c>Player.ReportEnvironmentPrismDestroyed_ServerRpc</c>,
    /// <c>Player.ReportPrismStolen_ServerRpc</c>). Pure and time-injected so it is EditMode testable.
    ///
    /// Two checks, both deliberately GENEROUS so honest play is never touched - they exist to stop
    /// a modified client from posting absurd numbers, not to referee close calls:
    ///   1. <see cref="ClampVolume"/>: a single prism's volume is capped at <see cref="MaxReportedVolume"/>
    ///      (10x the volume of a prism at the default PrismScaleAnimator max scale of 10x10x10).
    ///   2. <see cref="TryConsume"/>: a token bucket per player - bursts of up to <see cref="BurstCapacity"/>
    ///      reports, refilled at <see cref="RefillPerSecond"/>. A wall shredded in one frame fits in the burst.
    /// See Docs/Multiplayer/MULTIPLAYER_ARCHITECTURE.md §2.13.
    /// </summary>
    public sealed class HitReportGuard
    {
        public const float MaxReportedVolume = 10000f;
        public const float BurstCapacity = 600f;
        public const float RefillPerSecond = 300f;

        float _tokens = BurstCapacity;
        double _lastTime = double.NaN;

        /// <summary>Reports refused so far (for logging / the overlay).</summary>
        public int Throttled { get; private set; }

        public static float ClampVolume(float volume) =>
            volume > MaxReportedVolume ? MaxReportedVolume : volume;

        /// <summary>True if one more report may be credited at <paramref name="now"/> (seconds, monotonic).</summary>
        public bool TryConsume(double now)
        {
            if (!double.IsNaN(_lastTime) && now > _lastTime)
            {
                _tokens += (float)((now - _lastTime) * RefillPerSecond);
                if (_tokens > BurstCapacity) _tokens = BurstCapacity;
            }
            _lastTime = now;

            if (_tokens >= 1f) { _tokens -= 1f; return true; }
            Throttled++;
            return false;
        }
    }
}
