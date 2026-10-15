namespace CosmicShore.Core
{
    /// <summary>
    /// The browser Toybox build: freestyle in Menu_Main with the toys, single player, played in a
    /// web browser from www.froglet.games/play (Docs/WEBGL_TOYBOX.md).
    ///
    /// <para>
    /// The build defines <c>FROGLET_WEB_TOYBOX</c> (set by <c>WebToyboxBuild</c>, never in
    /// ProjectSettings, so no other build can carry it). Code reads it through
    /// <see cref="IsBuild"/> rather than through its own <c>#if</c>, so every branch compiles in
    /// every configuration and a broken toybox path cannot hide until the WebGL build runs
    /// (Docs/CONDITIONAL_COMPILATION.md). Only code that names a platform-only API (a jslib
    /// import) still needs a real <c>#if UNITY_WEBGL</c>.
    /// </para>
    ///
    /// <para>
    /// What the define changes, and nothing else:
    /// <list type="bullet">
    /// <item>No Unity Gaming Services: no sign-in, Cloud Save, presence lobby, Friends, party,
    /// Relay or analytics. The session is the offline local host, on <c>SinglePlayerTransport</c>.</item>
    /// <item>Boot goes straight from Bootstrap to Menu_Main and into freestyle; no consent screen,
    /// no username step.</item>
    /// <item>Menu_Main shows the toybox only: the Arcade, Arena and Mission entries are hidden and
    /// refuse to open.</item>
    /// </list>
    /// Platform differences that are about WebGL rather than about the toybox (no compute shaders,
    /// no threads, no NativeShare) key off the platform or <c>SystemInfo</c>, not this flag, so
    /// they also hold for any other WebGL or low-end build.
    /// </para>
    /// </summary>
    public static class WebToybox
    {
        /// <summary>True only in the browser Toybox build (<c>FROGLET_WEB_TOYBOX</c>).</summary>
        public static readonly bool IsBuild =
#if FROGLET_WEB_TOYBOX
            true;
#else
            false;
#endif

        /// <summary>The scripting define the build sets. One spelling, used by the build script and tests.</summary>
        public const string Define = "FROGLET_WEB_TOYBOX";
    }
}
