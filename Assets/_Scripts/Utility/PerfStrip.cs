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
        public static bool TrailsDisabled =>
            Enabled && !CappedTrailActive && !WanderwayTetherActive && !FreestyleTrailActive;

        /// <summary>
        /// The pilot is flying freestyle in Menu_Main, so vessels lay their trail - UNCAPPED,
        /// because nothing in freestyle is allowed to age trail mass out (CLAUDE.md: the menu
        /// trail cap is a rejected cheat, "no cosmetic exemptions"). What bounds it on a phone is
        /// the pair the law sanctions: the FOOD WEB (Garland's fauna graze trail mass - its life
        /// runs on the strip, see <see cref="CellLifeRuns"/>) and a SPAWNER THAT WAITS - while the
        /// cell holds more than <see cref="FreestyleCellPrismBudget"/> live prisms the trail's
        /// pen is up, and it comes back down once grazing has brought the cell under the budget.
        /// Nothing is removed to make room; creation just waits. Set by MenuCrystalClickHandler on
        /// freestyle enter/exit. The menu's autopilot lava lamp lays no trail.
        /// </summary>
        public static bool FreestyleTrailActive;


        /// <summary>
        /// Live prisms (all sources: boughs, garden, creatures, trail) above which the freestyle
        /// trail waits. Garland mature is ~8,100, so this leaves ~1,900 prisms of trail - about two
        /// minutes of Squirrel flight - before the pen lifts. The one dial for how much freestyle
        /// mass a phone carries.
        /// </summary>
        public const int FreestyleCellPrismBudget = 10000;

        /// <summary>The pen comes back down this far under the budget (hysteresis).</summary>
        public const int FreestyleCellPrismResume = 9700;

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
        /// owner for this branch (2026-07-07). Two writers, one per shipped mode:
        ///   • SkimRaceController - the skimmable race trail (the track is ~4000u per circuit and the
        ///     Squirrel lays a prism every 5–7u ⇒ ~600–800 prisms/lap, so 2000 guarantees AT LEAST
        ///     two laps to skim after lap one; shared across vessels by
        ///     <see cref="SkimRaceTrailPrismsPerVessel"/>);
        ///   • JoustController - the ribbon a pilot skims for speed in an empty cell
        ///     (<see cref="JoustTrailPrismsPerVessel"/>).
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
        public static int SkimRaceTrailPrismsPerVessel(int vessels) =>
            CappedTrailPrismsPerVessel(vessels, SkimRaceTrailBudget, SkimRaceTrailFloorPrisms, SkimRaceTrailPrisms);

        /// <summary>
        /// Joust's per-vessel trail ceiling. Joust has no laps - its trail is what a pilot skims to
        /// out-run the rival it is about to overtake, in an EMPTY cell (Barren: no environment, no
        /// flora or fauna), so without a trail there is nothing to skim and no speed to joust with.
        /// A Squirrel lays a prism every 5-7u, so 1,200 is roughly the last minute of flight.
        /// </summary>
        public const int JoustTrailPrisms = 1200;

        /// <summary>Joust race-wide live-trail budget, shared across every seat.</summary>
        public const int JoustTrailBudget = 4000;

        /// <summary>The least a Joust vessel keeps - enough ribbon behind it to be worth skimming.</summary>
        public const int JoustTrailFloorPrisms = 400;

        /// <summary>
        /// Joust per-vessel cap: 2-3 vessels → 1,200, 4 → 1,000, 8 → 500, 10+ → 400. Worst case
        /// (12 seats) is 4,800 live prisms.
        /// </summary>
        public static int JoustTrailPrismsPerVessel(int vessels) =>
            CappedTrailPrismsPerVessel(vessels, JoustTrailBudget, JoustTrailFloorPrisms, JoustTrailPrisms);

        /// <summary>
        /// A per-vessel capped-trail length as a share of one match-wide <paramref name="budget"/>,
        /// clamped to [<paramref name="floor"/>, <paramref name="ceiling"/>]. The cap is enforced per
        /// <c>VesselPrismController</c>, so without the share it multiplies with the seat count.
        /// </summary>
        public static int CappedTrailPrismsPerVessel(int vessels, int budget, int floor, int ceiling)
        {
            int share = budget / System.Math.Max(1, vessels);
            return System.Math.Clamp(share, floor, ceiling);
        }

        /// <summary>
        /// On touch, a single thumb raises Left/RightStickAction only when it is alone because the
        /// other thumb was LIFTED - never because it was the first one down. Those events are the
        /// Butterfly's Fold and mode switch; off the strip, touching the screen right-thumb-first
        /// toggled its mode and left-thumb-first started a Fold. See TouchInputStrategy.
        /// </summary>
        public static bool TouchStickEventsOnLiftOnly => Enabled;

        /// <summary>
        /// Ceiling on a Butterfly fold-gate window's render resolution, as a fraction of the
        /// gameplay camera's. The window renders only its own on-screen FOOTPRINT
        /// (FoldGatePortalView), so a distant gate costs a few thousand pixels; this cap is for the
        /// moments it fills the screen - the approach and the carry through - where an uncapped
        /// 0.75 would be a second near-full-resolution frame on a phone. The window is composited
        /// into the world and post-processed with it, so half resolution reads as a soft view
        /// through glass rather than as a low-resolution picture.
        /// </summary>
        public static float FoldGateWindowMaxRenderScale => Enabled ? 0.5f : 1f;

        /// <summary>
        /// Hide arcade/arena cards whose scene is not in this build. The strip ships a handful of
        /// scenes (see EditorBuildSettings); every other card is a launch that cannot load. Read at
        /// runtime from the build itself (<c>Application.CanStreamedLevelBeLoaded</c>), so enabling a
        /// mode's scene is the whole of bringing its card back - no second list to keep in step.
        /// </summary>
        public static bool HideUnbuiltModes => Enabled;

        /// <summary>
        /// Whether a cell's LIFE runs - its flora and fauna spawners, and flora growth. The strip
        /// pauses life everywhere (creation-side only: "not creating mass is allowed"; nothing that
        /// exists is culled) EXCEPT in the one world authored to be cheap to live in: the config
        /// that declares itself the home-screen world (<c>CellConfigDataSO.BootDefault</c> - Garland,
        /// the only asset that sets it). Garland's roster is four phyllotactic flora and three fauna
        /// on HARD caps (37 always-on heart colliders, mature at ~8,100 prisms including its
        /// 4,259-prism boughs), and its fauna are also the food web that grazes the freestyle
        /// trail. The races keep theirs paused: Skim Race and Waystation share a cell whose two
        /// fauna species would graze the race trail for nothing the race needs.
        /// </summary>
        public static bool CellLifeRuns(bool isHomeWorld) => !Enabled || isHomeWorld;

        /// <summary>
        /// Ship only the LIGHT toys in the freestyle toybox: the conveyor (the reason this build
        /// exists), the domain changer (two switch rings - repaints your trail and HUD), the
        /// element charger (one station opening into four accent-material crystals - lets a pilot
        /// feel the Squirrel's element upgrades) and the vessel changer (its roster narrowed to
        /// <see cref="ShipsVessel"/>, so it opens onto one hull). Each is a few meshes and no
        /// prisms until it is opened. Skipped: the toys whose whole content is mass or ecology
        /// this build cannot afford - painting (trail strokes), cell selector (34-69k-prism
        /// worlds), spawn matrix (releases flora/fauna/AI), Arkway (three satellite cells).
        /// </summary>
        public static bool LightToysOnly => Enabled;

        /// <summary>
        /// The hulls this build is tuned for: the Squirrel (two thumbs; the boost ring on a left-
        /// thumb lift, the drift on a right-thumb lift) and the Butterfly (two BINARY abilities, both mapped to a thumb
        /// lift - which is what makes it the second hull to bring to glass). The vessel changer
        /// offers exactly these (ToyVesselRoster); every other hull is its own touch design pass.
        /// </summary>
        public static bool ShipsVessel(CosmicShore.Data.VesselClassType vessel) =>
            !Enabled
            || vessel == CosmicShore.Data.VesselClassType.Squirrel
            || vessel == CosmicShore.Data.VesselClassType.Butterfly;

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
