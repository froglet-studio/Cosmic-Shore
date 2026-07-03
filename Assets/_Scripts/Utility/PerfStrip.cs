namespace CosmicShore.Utility
{
    /// <summary>
    /// Central kill-switch for the Android <b>stripped-performance</b> branch
    /// (<c>claude/android-performance-stripped-dap5z2</c>).
    ///
    /// This branch has ONE objective: hold 60 fps on a mid-tier, years-old Android device while
    /// running the Squirrel + the Wanderway conveyor toy in freestyle. Everything that is not
    /// load-bearing for that experience is stripped. Rather than delete code (fragile, breaks
    /// scene/prefab references, un-reviewable without Unity), each heavy system is gated behind a
    /// flag here and early-returns when the strip is on. One file to read to know what was cut;
    /// one file to flip to restore the full experience.
    ///
    /// All flags derive from <see cref="Enabled"/>. Flip <see cref="Enabled"/> to <c>false</c> to
    /// turn the whole strip off and get vanilla behaviour back — the guards become no-ops.
    ///
    /// NOTE on the fundamentals (see CLAUDE.md "Mass is conserved"): the trail kill disables prism
    /// *creation* at the source — it never ages out or culls existing mass. Not creating trail mass
    /// is explicitly allowed; aging it out is the cheat. The conveyor's own conserved-mass stock is
    /// untouched.
    /// </summary>
    public static class PerfStrip
    {
        /// <summary>
        /// Master switch for the entire strip. <c>true</c> on this branch by design. Set to
        /// <c>false</c> (or wrap in <c>#if</c>) to restore the full, un-stripped experience.
        /// A field (not a const) so nothing trips "unreachable code" analysis and so tests /
        /// bootstrap could flip it if ever needed.
        /// </summary>
        public static bool Enabled = true;

        /// <summary>Stop vessels laying their continuous prism trail (the biggest per-frame win).</summary>
        public static bool TrailsDisabled => Enabled;

        /// <summary>Show only the conveyor toy in the freestyle toybox; skip the other three.</summary>
        public static bool ConveyorOnlyToybox => Enabled;

        /// <summary>
        /// Skip the offline-useless social/networking overhead: the presence-lobby refresh loop
        /// (a UGS read + main-thread marshal every 1.5s → periodic GC/hitch) and the UGS Friends
        /// init/presence writes. The Relay-backed NetworkManager host that the vessel-spawn pipeline
        /// depends on (created by HostConnectionService.EnsurePartySessionAsync) is NOT touched —
        /// only the periodic refresh + friends are gated, so the Squirrel still spawns.
        /// </summary>
        public static bool DisableSocialNetworking => Enabled;

        /// <summary>
        /// Full <b>offline boot</b>: never touch Unity Gaming Services. This build has no UGS
        /// project configured, so <c>UnityServices.InitializeAsync()</c> / anonymous sign-in /
        /// the Relay host bring-up all throw and crash the app on launch. In offline mode we skip
        /// UGS entirely — sign in locally, skip presence lobby / Relay / CloudSave / Analytics —
        /// and start a plain local <c>NetworkManager.StartHost()</c> (no Relay transport) so the
        /// existing menu vessel-spawn pipeline and the conveyor toy run with no backend.
        /// </summary>
        public static bool OfflineMode => Enabled;

        /// <summary>
        /// Show the on-screen <see cref="BootTrace"/> overlay. For the untethered mobile workflow
        /// (no adb/logcat): checkpoints + errors are persisted to a file and the PREVIOUS run is
        /// rendered on screen so a crash-looping app shows where it died last launch — screenshot
        /// it to report. Works in Release builds too (not gated on DEVELOPMENT_BUILD).
        /// </summary>
        public static bool ShowBootTrace => Enabled;
    }
}
