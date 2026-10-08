namespace CosmicShore.Data
{
    /// <summary>
    /// The hardware class a device runs as, decided once per session by
    /// <c>PlatformProfile</c> (<c>_Scripts/System/Platform/</c>). It picks the device's
    /// <c>PlatformProfileSO</c>, the single place every per-platform choice reads from.
    ///
    /// A tier is a CAPABILITY, not an operating system: a flagship Android phone and an iPhone
    /// both land on <see cref="MobileHigh"/>, a 4 GB budget Android phone (or a 2 GB iPhone) on
    /// <see cref="MobileLow"/>. Full rationale: <c>Docs/PLATFORM_UNIFICATION.md</c> §3.
    /// </summary>
    public enum DeviceTier
    {
        /// <summary>Windows / macOS / Linux, and the Editor unless a tier is simulated.</summary>
        Desktop = 0,
        /// <summary>A phone or tablet fast enough for the full game (every current iPhone).</summary>
        MobileHigh = 1,
        /// <summary>A phone or tablet that needs the reduced-cost profile.</summary>
        MobileLow = 2,
    }
}
