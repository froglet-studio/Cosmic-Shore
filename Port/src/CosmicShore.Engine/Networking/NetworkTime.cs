namespace CosmicShore.Engine.Networking
{
    /// <summary>
    /// Minimal stand-in for Netcode's <c>NetworkTime</c> (engine addition for SA1:
    /// <c>TimePlayedScoring</c> reads <c>NetworkManager.Singleton.ServerTime.Time</c>).
    /// Until the session/transport phase provides a real synchronized clock, the value
    /// is whatever the harness assigns to <see cref="NetworkManager.ServerTime"/>;
    /// the default (0) models a server clock that hasn't started ticking.
    /// </summary>
    public readonly struct NetworkTime
    {
        public double Time { get; }

        /// <summary>Netcode's single-precision copy of <see cref="Time"/>.</summary>
        public float TimeAsFloat => (float)Time;

        public NetworkTime(double time) => Time = time;
    }
}
