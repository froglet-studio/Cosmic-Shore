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

        /// <summary>
        /// Stop vessels laying their continuous prism trail (the biggest per-frame win) — EXCEPT
        /// while a capped trail is active (see <see cref="CappedTrailActive"/>): the conveyor's
        /// breadcrumb (300) or Skim Race's skimmable trail (2000, ≥ two laps).
        /// </summary>
        public static bool TrailsDisabled => Enabled && !CappedTrailActive;

        /// <summary>
        /// Capped-trail mode: vessels lay their trail, FIFO-capped at <see cref="CappedTrailLimit"/>
        /// prisms — past the cap the OLDEST prism is consumed via the sanctioned Prism.Consume
        /// (implode-toward-target) path, never a silent despawn, so the continuity law's
        /// visible-transition requirement is met. The cap was explicitly authorized by the design
        /// owner for this branch (2026-07-07). Two writers:
        ///   • ConveyorToy — breadcrumb home (limit 300, prisms implode into the tail-riding switch);
        ///   • HexRaceController ("Skim Race") — the skimmable race trail (limit 2000: the track is
        ///     ~4000u per circuit and the Squirrel lays a prism every 5–7u ⇒ ~600–800 prisms/lap,
        ///     so 2000 guarantees AT LEAST two laps of trail to skim after lap one).
        /// </summary>
        public static bool CappedTrailActive;

        /// <summary>Capped-trail length, in prisms. Set alongside <see cref="CappedTrailActive"/>.</summary>
        public static int CappedTrailLimit = 300;

        /// <summary>Skim Race trail cap — sized to always cover ≥ 2 laps (see CappedTrailActive doc).</summary>
        public const int SkimRaceTrailPrisms = 2000;

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
        /// Strip the menu UI to the minimum the conveyor flow needs: only the HOME screen ships
        /// (the other screen roots are deactivated at Awake — their hidden per-frame tickers never
        /// start), and while flying freestyle the remaining menu UI (HOME + NavBar) is fully
        /// deactivated, not just alpha-faded — CanvasGroup alpha=0 does not stop Update ticks,
        /// TMP rebuilds, or canvas re-batching. See ScreenSwitcher.
        /// </summary>
        public static bool MenuUIStripped => Enabled;
    }
}
