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
        public static bool TrailsDisabled => Enabled && !CappedTrailActive && !WanderwayTetherActive;

        /// <summary>
        /// A Wanderway run is live: the vessel lays its trail so <c>WanderwayRun</c>'s rolling
        /// tether exists. The tether IS the run's way home — the return station rides its tail, and
        /// with no trail there is no tail, so the station never plants. It is already bounded
        /// (<c>tetherPrisms</c>, recycled into its own pool — fixed memory), so it does NOT use the
        /// capped-trail FIFO below; the run owns the cap. Set by <c>WanderwayRun.Begin</c>, cleared
        /// by <c>WanderwayRun.End</c>.
        /// </summary>
        public static bool WanderwayTetherActive;

        /// <summary>
        /// Capped-trail mode: vessels lay their trail, FIFO-capped at <see cref="CappedTrailLimit"/>
        /// prisms — past the cap the OLDEST prism is consumed via the sanctioned Prism.Consume
        /// (implode-toward-target) path, never a silent despawn, so the continuity law's
        /// visible-transition requirement is met. The cap was explicitly authorized by the design
        /// owner for this branch (2026-07-07). One writer today: SkimRaceController - the
        /// skimmable race trail (the track is ~4000u per circuit and the Squirrel lays a prism
        /// every 5–7u ⇒ ~600–800 prisms/lap, so 2000 guarantees AT LEAST two laps of trail to skim
        /// after lap one; shared across vessels by <see cref="SkimRaceTrailPrismsPerVessel"/>).
        /// The conveyor's old breadcrumb writer is superseded by <see cref="WanderwayTetherActive"/>.
        /// </summary>
        public static bool CappedTrailActive;

        /// <summary>Capped-trail length, in prisms. Set alongside <see cref="CappedTrailActive"/>.</summary>
        public static int CappedTrailLimit = 300;

        /// <summary>Skim Race trail cap — sized to always cover ≥ 2 laps (see CappedTrailActive doc).</summary>
        public const int SkimRaceTrailPrisms = 2000;

        /// <summary>
        /// Race-wide live-trail budget, shared by every vessel in a Skim Race. Up to three vessels
        /// each keep the full two-lap <see cref="SkimRaceTrailPrisms"/>; past that the share shrinks
        /// toward <see cref="SkimRaceTrailFloorPrisms"/> (one lap) instead of multiplying.
        /// </summary>
        public const int SkimRaceTrailBudget = 6000;

        /// <summary>One lap of trail (~600–800 prisms/lap) — the least a race vessel keeps.</summary>
        public const int SkimRaceTrailFloorPrisms = 800;

        /// <summary>
        /// Per-vessel Skim Race cap for a race of <paramref name="vessels"/>: 1–3 → 2000,
        /// 4 → 1500, 6 → 1000, 8+ → 800. Worst case (12 seats) is 9,600 live prisms where a flat
        /// per-vessel cap allowed 24,000.
        /// </summary>
        public static int SkimRaceTrailPrismsPerVessel(int vessels)
        {
            int share = SkimRaceTrailBudget / System.Math.Max(1, vessels);
            return System.Math.Clamp(share, SkimRaceTrailFloorPrisms, SkimRaceTrailPrisms);
        }

        /// <summary>
        /// Boot Menu_Main into the cell's bare canvas (Barren) instead of its authored boot
        /// default (Garland, 4,259 laid prisms). Upstream moved the home screen onto Garland so it
        /// is furnished in the first frame; on this build that is 3.5x the conveyor's whole prism
        /// budget spent on a menu backdrop, rebuilt behind a load veil on every menu entry. Flip to
        /// false to get the furnished home screen back and pay for it.
        /// </summary>
        public static bool BootBareMenuCell => Enabled;

        /// <summary>
        /// Ship only the LIGHT toys in the freestyle toybox: the conveyor (the reason this build
        /// exists), the domain changer (two switch rings - repaints your trail and HUD) and the
        /// element charger (one station opening into four accent-material crystals - lets a pilot
        /// feel the Squirrel's element upgrades). Each is a few meshes and no prisms. Skipped: the
        /// vessel changer (other hulls, not tuned here), and the toys whose whole content is mass
        /// or ecology this build turns off - painting (trail strokes), cell selector (34-69k-prism
        /// worlds), spawn matrix (flora/fauna are paused), Arkway (three satellite cells).
        /// </summary>
        public static bool LightToysOnly => Enabled;

        /// <summary>
        /// Let a scene keep the post-processing it AUTHORED (see
        /// <c>PerfStripRuntime.SceneHasActivePostOverride</c>). On for gameplay because the
        /// gameplay profile's two active overrides are both feel-critical: Bloom is the vaporwave
        /// neon read, and PaniniProjection is half the speed tunnel (Docs/SPEED_TUNNEL.md) — with
        /// post off, a race keeps the FOV narrowing and loses the bend that sells the speed.
        /// A scene with nothing active (Menu_Main) still pays nothing either way.
        ///
        /// Flip to false to reclaim the UberPost blit + LUT if the frame budget ever demands it —
        /// this is the one switch that trades the race's look for frame time.
        /// </summary>
        public static bool AllowAuthoredPostProcessing = true;

        /// <summary>
        /// FXAA on the presenting camera in every scene (see
        /// <c>PerfStripRuntime.ApplyAntiAliasing</c>). Independent of
        /// <see cref="AllowAuthoredPostProcessing"/> — FXAA is a per-camera final-blit setting, not
        /// a Volume override, so it costs one full-screen pass whether or not gameplay's Bloom /
        /// Panini are on. Chosen over MSAA (per-sample multisample bandwidth on tile-based mobile
        /// GPUs) and TAA (motion vectors + history buffer, plus ghosting on thin fast-moving prism
        /// trails) as the cheapest real edge smoothing this build can afford.
        ///
        /// Flip to false to reclaim that one pass if the frame budget ever demands it.
        /// </summary>
        public static bool AllowAntiAliasing = true;

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
