using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Tunables for the microscene conveyor, authored on the toy definition.</summary>
    public sealed class ConveyorConfig
    {
        /// <summary>The toy's player-facing name — shown on the build veil ("GROWING WANDERWAY…").</summary>
        public string DisplayName = "WANDERWAY";

        public Prism PrismPrefab;
        public Crystal OmniCrystalPrefab;
        public SkimmerCrystalEffectSO[] CrystalEffects;
        public MicroscenePalette Palette = new();

        // Defaults are the GRAND scale: PoolSize × PrismBudget is the belt's whole conserved
        // stock, built once behind the load veil and transported forever after (20 × 1500 = 30,000
        // prisms — the same order as an authored cell environment, which is the proven envelope
        // for the instanced render path + collider LOD).
        public int PoolSize = 20;
        public int PrismBudget = 1500;
        public float SceneRadius = 180f;
        public float SceneSpacing = 520f;
        public float FirstSceneDistance = 460f;
        public int AheadTargetScenes = 5;
        public float MinSceneIntervalSeconds = 2f;
        public float RecycleBehindDistance = 520f;
        public float TransitionSeconds = 1.2f;
        public float TurnBreakDegrees = 55f;
        public int MaxCrystalsPerScene = 6;
        public bool LifeformScenes = true;
        public int Seed;

        // Visibility guards (see MicrosceneConveyor): the player must never watch a scene bloom in
        // on top of them, nor watch one suction away.
        public float MinPlacementDistance = 380f;
        public float OffscreenMargin = 80f;

        // ── The run (see WanderwayRun): Wanderway as its own mode ────────────
        public bool RevertCellOnStart = true;
        public int TetherPrisms = 120;
        public float ReturnStationRadius = 22f;
        public Color ReturnStationColor = new(1f, 0.78f, 0.25f, 1f);
    }

    /// <summary>
    /// The freestyle conveyor-belt runner: keeps a field of <see cref="Microscene"/>s blooming in
    /// ahead of the local player's flight path - open-world exploring crossed with an infinite
    /// runner. The belt follows the player ANYWHERE (fast, far, odd deviations included): spacing
    /// and lookahead scale with current speed so there is always a field of scenes ahead, and the
    /// scene farthest behind clears (collapses + is carried away) as new ones arrive - spawn
    /// frequency IS the
    /// clear frequency, because the pool is finite and closed: the same prisms are endlessly
    /// re-arranged, never created or destroyed (fauna grazing on belt prisms is the only sink,
    /// and it's the food web's own active force). Living recipes (meadow/menagerie) release their
    /// flora/fauna only while the scene sits inside a live <see cref="Cell"/>; out in open space
    /// the belt is pure prisms + crystals.
    ///
    /// Placement is a CONNECTED ribbon that can break and re-lay. Every scene sits on the flight
    /// line - never scattered laterally (no orthogonal "sphere in front of you"). The belt reads a
    /// forward cone (half-angle <see cref="ConveyorConfig.TurnBreakDegrees"/>) around the live
    /// course each tick and does one of two things:
    ///   • NEAR-FILL - if nothing lies just ahead on the current heading (start-up, or a turn just
    ///     ejected the near scenes out of the cone), it drops a scene directly ahead at
    ///     firstSceneDistance, so a structure appears in front of the player right after any turn.
    ///   • EXTEND - otherwise it chains the next scene off the ACTUAL frontmost scene along the
    ///     current course (tip + course × spacing). Chaining off real mass (not a free-floating
    ///     "head") keeps the ribbon connected and lets it BEND with gentle/moderate turns without
    ///     ever drifting into a parallel path far to the side.
    /// A sharp turn drops the whole old ribbon out of the cone: its reach collapses, near-fill
    /// re-lays straight down the NEW heading, and the now-lateral leftovers become the
    /// farthest-first recycle candidates that rebuild ahead. A scene mid-recycle claims its
    /// destination slot immediately (<see cref="Microscene.PendingAnchor"/>) so the rebuild never
    /// piles several scenes onto the same point while the blooms are in flight.
    ///
    /// Two visibility guards keep the transport itself invisible - the belt should read as a world
    /// that is simply THERE, never as props being spawned and despawned in view:
    ///   • PLACEMENT never blooms a scene closer than <see cref="ConveyorConfig.MinPlacementDistance"/>
    ///     to the player - structures arrive at a respectful distance ahead, never in your face.
    ///   • REMOVAL only reclaims a scene the player CANNOT currently see. The recycle's first half
    ///     collapses the scene's prisms in place at its OLD anchor; that anchor must lie fully
    ///     outside the camera frustum (by <see cref="ConveyorConfig.OffscreenMargin"/>) before the
    ///     belt will touch it, so a scene is never watched vanishing - which is also what licenses
    ///     the transport itself to simply hide the stock and move it (see <see cref="Microscene"/>).
    ///     As the player flies on, passed scenes fall out of view and become reclaimable - the belt
    ///     self-heals without ever popping in view (and simply idles, placing nothing, if every
    ///     pooled scene is on screen).
    ///
    /// SCALE: the belt's whole conserved stock (PoolSize × PrismBudget - 30,000 prisms at the
    /// authored defaults) is built ONCE, up front, behind an <see cref="EnvironmentLoadVeil"/> - the
    /// same hold the Cell Selector raises for a world swap. After that the belt never instantiates
    /// again; every arrival is transport of mass that already exists.
    ///
    /// Toy-faithful: no score, no end condition, no timers - every belt advance is driven by the
    /// player's own motion. The Wanderway toy toggles the belt on/off; exiting freestyle just
    /// makes it dormant. Either way its scenes remain part of the world until the toybox tears
    /// down with the scene.
    /// </summary>
    public class MicrosceneConveyor : MonoBehaviour
    {
        const float TickSeconds = 0.25f;

        // Three arrivals may transition at once: the belt must outpace full throttle + elemental
        // Time buffs (~150 u/s), and at high speed the speed-scaled spacing stretches so each
        // arrival buys more distance. Boost bursts beyond that just fill in over the next few ticks.
        const int MaxConcurrentArrivals = 3;

        // A near hole opens (and the belt drops a fresh scene straight ahead) once the nearest scene
        // on the flight line sits farther than firstDistance + this × spacing - big enough that a
        // just-placed near scene doesn't immediately re-trigger, small enough that a real gap fills.
        const float NearHoleSpacingFactor = 0.5f;

        /// <summary>Shuffle-bag entries per grand assembly once the budget affords them (the
        /// classic recipes get one each) — see NextPlan.</summary>
        const int GrandRecipeWeight = 3;

        ConveyorConfig _cfg;
        IVesselStatus _vessel;
        Func<bool> _isFreestyleActive;
        GameDataSO _gameData;

        readonly List<Microscene> _scenes = new();
        readonly List<int> _recipeBag = new();

        System.Random _rng;
        float _nextTickAt;
        bool _running;
        bool _priming;

        /// <summary>True while the belt is flowing (the Wanderway toy toggles this) — including
        /// the initial build, so a second pass through the toy mid-build reads as "already on".</summary>
        public bool IsRunning => _running || _priming;

        public void Begin(ConveyorConfig cfg, IVesselStatus vessel, Func<bool> isFreestyleActive, GameDataSO gameData)
        {
            _cfg = cfg;
            _vessel = vessel;
            _isFreestyleActive = isFreestyleActive;
            _gameData = gameData;
            _rng = cfg.Seed != 0 ? new System.Random(cfg.Seed) : new System.Random(Environment.TickCount);

            PrimeAsync().Forget();
        }

        /// <summary>
        /// Build the belt's ENTIRE conserved stock up front, behind the same veil the cell
        /// selector raises for a world swap — the freestyle counterpart of a game scene's
        /// connecting screen (<see cref="EnvironmentLoadVeil"/> + the arena-ready gate).
        ///
        /// Why up front rather than one scene per belt tick (the old behaviour): at grand-assembly
        /// scale the stock is tens of thousands of prisms, and instantiating a slice of that under
        /// live gameplay is the exact failure the cell environments already learned — it starves
        /// audio, wedges clone batches, and drips structures into view for the first minute of the
        /// ride. Behind the veil the gate raises the lay slice ~10×, holds until every prism is
        /// laid, created AND grown, and the ride opens on a world that is simply THERE. From that
        /// moment the belt never instantiates again: it only transports (collapse out → bloom in).
        ///
        /// The arena-build bracket is closed in a finally so a mid-build teardown (scene change,
        /// toybox destroyed) can never leave the global gate held open for the next scene.
        /// </summary>
        async UniTaskVoid PrimeAsync()
        {
            _priming = true;
            if (!TryGetVessel(out Vector3 playerPos, out Vector3 course, out _))
            {
                // No live vessel yet (rare) — fall back to the lazy behaviour: the first Update
                // tick near-fills ahead of whatever vessel exists then, one scene per tick.
                _priming = false;
                _running = !_stopRequestedDuringPrime;
                return;
            }

            // Announce the build BEFORE raising the veil so the ready-poll can never see a
            // pre-lay all-clear and open the screen onto an empty world.
            PrismTrailBuilder.BeginArenaBuild();
            EnvironmentLoadVeil.Hold(_cfg.DisplayName);
            try
            {
                // Lay the whole pool as one straight ribbon down the current heading. The belt's
                // normal near-fill/extend logic re-lays and bends it from the first tick after
                // release, so this only has to be a sane starting field.
                var pending = new List<UniTask>(_cfg.PoolSize);
                for (int i = 0; i < _cfg.PoolSize; i++)
                {
                    Vector3 target = playerPos + course * (_cfg.FirstSceneDistance + _cfg.SceneSpacing * i);
                    pending.Add(PlaceNewScene(new Pose(target, Quaternion.LookRotation(course, UpFor(course)))));
                }

                // All PoolSize lays draw from PrismTrailBuilder's ONE shared per-frame budget, so
                // running them concurrently costs max(budget) per frame, not pool × budget.
                await UniTask.WhenAll(pending);
            }
            finally
            {
                PrismTrailBuilder.EndArenaBuild();
                _priming = false;
                _running = !_stopRequestedDuringPrime;
            }
        }

        /// <summary>Re-entry pass through the toy (or after a vessel swap): keep the field, resume the flow.</summary>
        public void Resume(IVesselStatus vessel)
        {
            // Re-acquire the (possibly swapped) vessel; the next Update near-fills the ribbon directly
            // ahead of wherever the player now is, and any scenes left behind recycle naturally as
            // off-cone candidates. Nothing else needs restarting.
            _vessel = vessel;
            _stopRequestedDuringPrime = false;
            _running = true;
        }

        /// <summary>
        /// Stop the flow (fly through the toy again to restart). Existing scenes stay in the
        /// world - they are conserved mass and released citizens, not toy props to vanish.
        /// </summary>
        public void StopBelt()
        {
            _running = false;
            // A stop requested WHILE the stock is still building must survive the prime's
            // completion, or the belt would switch itself back on when the veil drops.
            if (_priming) _stopRequestedDuringPrime = true;
        }

        bool _stopRequestedDuringPrime;

        void Update()
        {
            if (_priming || !_running || _cfg == null) return;
            if (Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + TickSeconds;

            // Dormant while the player is back in the menu / lava lamp - the belt's scenes stay
            // in the world (conserved mass, released citizens), it just stops advancing.
            if (_isFreestyleActive != null && !_isFreestyleActive()) return;
            if (!TryGetVessel(out Vector3 playerPos, out Vector3 course, out float speed)) return;
            if (BusySceneCount() >= MaxConcurrentArrivals) return;

            // Speed-scaled belt geometry: at cruise the base spacing rules; the faster you fly,
            // the wider the spacing and the deeper the lookahead, so the field ahead holds
            // AheadTargetScenes structures and arrivals stay comfortably ahead of you.
            float effSpacing = Mathf.Max(_cfg.SceneSpacing, speed * _cfg.MinSceneIntervalSeconds);
            float lookahead = _cfg.AheadTargetScenes * effSpacing;
            float recycleBehind = Mathf.Max(_cfg.RecycleBehindDistance, effSpacing * 0.9f);
            float firstDist = FirstDistance(speed);
            float leadCos = LeadCos;

            // One scan of the field along the LIVE flight cone: the nearest scene ahead (near-field
            // health) and the frontmost connected scene (the ribbon's tip + how deep it reaches).
            // Only scenes inside the cone count - a turn ejects the old ribbon out of it, so both
            // collapse and the belt re-lays straight down the new heading. Effective anchors count a
            // recycling scene's CLAIMED slot so a rebuild never piles arrivals onto one point.
            Microscene tip = null;
            float nearestAhead = float.MaxValue;
            float frontier = 0f;
            foreach (var scene in _scenes)
            {
                if (!scene) continue;
                Vector3 rel = EffectiveAnchor(scene) - playerPos;
                float along = Vector3.Dot(rel, course);
                if (along <= 0f) continue;                              // behind me
                if (Vector3.Dot(rel.normalized, course) < leadCos) continue; // off the flight cone
                if (along < nearestAhead) nearestAhead = along;
                if (along > frontier) { frontier = along; tip = scene; }
            }

            bool nearFill = tip == null || nearestAhead > firstDist + effSpacing * NearHoleSpacingFactor;
            Vector3 target;
            if (nearFill)
            {
                // NEAR-FILL: nothing on the flight line just ahead (start-up, or a turn just ejected
                // the near scenes) - drop a scene directly ahead on the live course so a structure
                // appears in front of the player. No lateral scatter: it lands on the line.
                target = playerPos + course * firstDist;
            }
            else if (frontier < lookahead)
            {
                // EXTEND: chain the next scene off the ACTUAL frontmost scene along the current
                // course. Chaining off real mass (not a free-floating head) keeps the ribbon
                // connected and lets it bend with the turn instead of drifting to a parallel path.
                target = EffectiveAnchor(tip) + course * effSpacing;
            }
            else
            {
                return; // a full ribbon already reaches ahead on this heading
            }

            // PLACEMENT GATE: never bloom a scene closer than MinPlacementDistance to the player.
            // Near-fill (>= firstDist) and extend (off the frontier tip) already target far ahead;
            // this is the hard floor that also covers any degenerate geometry, so a structure never
            // materialises in the player's face. Squared compare - no sqrt.
            float minPlaceSqr = _cfg.MinPlacementDistance * _cfg.MinPlacementDistance;
            if ((target - playerPos).sqrMagnitude < minPlaceSqr) return;

            Pose pose = new(target, Quaternion.LookRotation(course, UpFor(course)));

            if (_scenes.Count < _cfg.PoolSize)
                PlaceNewScene(pose).Forget(); // only reachable if the prime pass was skipped (no vessel)
            else
                RecycleFarthestScene(playerPos, course, recycleBehind, lookahead, leadCos, pose, nearFill);
        }

        // ── Belt geometry ────────────────────────────────────────────────────

        float FirstDistance(float speed) => Mathf.Max(_cfg.FirstSceneDistance, speed * 1.2f);

        /// <summary>Cosine of the forward-cone half-angle: scenes farther off the live course than
        /// this don't count as "ahead," so a turn past it re-lays the ribbon straight down the new
        /// heading (config-driven so designers tune how sharp a turn breaks the ribbon).</summary>
        float LeadCos => Mathf.Cos(Mathf.Clamp(_cfg.TurnBreakDegrees, 5f, 89f) * Mathf.Deg2Rad);

        /// <summary>A scene's slot for placement decisions: its in-flight recycle destination if it
        /// has claimed one, else its settled anchor (see <see cref="Microscene.PendingAnchor"/>).</summary>
        static Vector3 EffectiveAnchor(Microscene scene) => scene.PendingAnchor ?? scene.Anchor;

        /// <summary>A stable up-vector for orienting a scene along the course, avoiding the
        /// LookRotation degeneracy when the player is flying near-vertically.</summary>
        static Vector3 UpFor(Vector3 course) =>
            Mathf.Abs(Vector3.Dot(course, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;

        // ── Scene arrivals ───────────────────────────────────────────────────

        UniTask PlaceNewScene(Pose pose)
        {
            var scene = Microscene.Create(transform, _scenes.Count.ToString());
            scene.Configure(_cfg.PrismPrefab, _cfg.OmniCrystalPrefab, _cfg.CrystalEffects);
            scene.transform.SetPositionAndRotation(pose.position, pose.rotation);
            _scenes.Add(scene);

            var plan = NextPlan();
            // Each arrival gets its own derived rng: async populate/recycle draws would otherwise
            // interleave on the shared stream and break per-seed reproducibility.
            var sceneRng = new System.Random(_rng.Next());
            return scene.PopulateAsync(plan, sceneRng, this.GetCancellationTokenOnDestroy());
        }

        bool RecycleFarthestScene(Vector3 playerPos, Vector3 course, float recycleBehind, float lookahead,
            float leadCos, Pose pose, bool nearFill)
        {
            // Pass 1: reclaim only mass genuinely out of the ride - protect the ribbon ahead (in the
            // flight cone, what the player is flying toward) and just-passed scenes. This is the
            // steady-state path for every normal flight; a turn's now-lateral leftovers and
            // dropped-behind scenes rebuild ahead, farthest first.
            if (TryRecycleFarthest(playerPos, course, recycleBehind, lookahead, leadCos, pose,
                    protectRibbonAhead: true))
                return true;

            // Pass 2 (near-fill only): the near field is empty AND nothing off-cone/behind is
            // reclaimable (e.g. the whole belt bunched far ahead while the player is near-stationary).
            // Steal the farthest in-cone scene to fill the hole - a surplus far structure is worth
            // less than one directly in front. EXTEND never steals the ribbon it is flying toward.
            return nearFill && TryRecycleFarthest(playerPos, course, recycleBehind, lookahead, leadCos,
                pose, protectRibbonAhead: false);
        }

        bool TryRecycleFarthest(Vector3 playerPos, Vector3 course, float recycleBehind, float lookahead,
            float leadCos, Pose pose, bool protectRibbonAhead)
        {
            Microscene candidate = null;
            float farthest = 0f;
            foreach (var scene in _scenes)
            {
                if (!scene || scene.Busy) continue;
                Vector3 rel = scene.Anchor - playerPos;
                float along = Vector3.Dot(rel, course);
                float dist = rel.magnitude;

                bool aheadInCone = protectRibbonAhead && along > 0f
                                   && Vector3.Dot(rel.normalized, course) >= leadCos && along <= lookahead * 1.5f;
                bool justPassed = along <= 0f && along > -recycleBehind;
                if (aheadInCone || justPassed) continue; // still part of the ride - leave it be

                // REMOVAL GATE: only reclaim a scene the player CANNOT currently see. The recycle's
                // first half suctions the container to a point AT THIS ANCHOR - if that were on
                // screen the player would watch a scene vanish (forbidden). Requiring the anchor to
                // sit fully outside the view frustum guarantees the suction is invisible; the bloom
                // then happens far ahead at the new pose. A fully on-screen field simply yields no
                // candidate and the belt idles until motion pushes something out of view.
                if (!IsSceneOffScreen(scene.Anchor, playerPos, course)) continue;

                if (dist <= farthest) continue;

                farthest = dist;
                candidate = scene;
            }

            if (!candidate) return false;

            var plan = NextPlan();
            var sceneRng = new System.Random(_rng.Next()); // see PlaceNewScene - per-arrival stream
            candidate.RecycleAsync(plan, pose, sceneRng, _cfg.TransitionSeconds,
                this.GetCancellationTokenOnDestroy()).Forget();
            return true;
        }

        // ── Variation (shuffle bag + domain cycle) ───────────────────────────

        MicroscenePlan NextPlan()
        {
            if (_recipeBag.Count == 0)
            {
                // The monument-scale family only reads as intended once a scene can afford the
                // architecture; below the threshold the belt stays on the classic recipes. Above
                // it they enter WEIGHTED, so a grand ride lands a landmark roughly every third
                // scene while the classic forty still carry the variety between them.
                bool grand = _cfg.PrismBudget >= MicroscenePatterns.GrandBudgetThreshold;
                for (int i = 0; i < MicroscenePatterns.RecipeCount; i++)
                {
                    if (!_cfg.LifeformScenes && MicroscenePatterns.IsLifeformRecipe(i)) continue;
                    if (MicroscenePatterns.IsGrandRecipe(i))
                    {
                        if (!grand) continue;
                        for (int w = 0; w < GrandRecipeWeight; w++) _recipeBag.Add(i);
                        continue;
                    }
                    _recipeBag.Add(i);
                }
                for (int i = _recipeBag.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (_recipeBag[i], _recipeBag[j]) = (_recipeBag[j], _recipeBag[i]);
                }
            }

            int recipe = _recipeBag[^1];
            _recipeBag.RemoveAt(_recipeBag.Count - 1);

            // The belt paints from the FULL playable triad, always. Belt prisms are environment
            // mass, not player property - limiting the palette to the domains present in the
            // session (the old behaviour) collapsed every solo-freestyle scene to the player's one
            // colour and starved every multi-domain scheme. The painter's per-scene schemes decide
            // how the triad lands (alternating gates, gradients, pinwheels, mono…).
            _cfg.Palette.PlayableDomains = AllPlayableDomains;
            return MicroscenePatterns.Plan(recipe, _rng, _cfg.PrismBudget, _cfg.SceneRadius, _cfg.MaxCrystalsPerScene, _cfg.Palette);
        }

        static readonly Domains[] AllPlayableDomains = { Domains.Jade, Domains.Ruby, Domains.Gold };

        // ── Plumbing ─────────────────────────────────────────────────────────

        bool TryGetVessel(out Vector3 position, out Vector3 course, out float speed)
        {
            position = default;
            course = default;
            speed = 0f;

            // A vessel swap (Vessel Changer toy / selection panel) destroys the pinned vessel -
            // re-acquire the live local one so the belt follows the new ship instead of
            // freezing until the player re-flies the Wanderway trigger.
            if (_vessel == null || (_vessel is UnityEngine.Object uo && !uo) || _vessel.Vessel == null)
                _vessel = _gameData?.LocalPlayer?.Vessel?.VesselStatus;
            if (_vessel == null || (_vessel is UnityEngine.Object o && !o) || _vessel.Vessel == null) return false;

            position = _vessel.Transform.position;
            course = _vessel.Course.sqrMagnitude > 0.01f ? _vessel.Course.normalized : _vessel.Transform.forward;
            speed = Mathf.Max(0f, _vessel.Speed);
            return true;
        }

        int BusySceneCount()
        {
            int busy = 0;
            foreach (var scene in _scenes)
                if (scene && scene.Busy) busy++;
            return busy;
        }

        // ── Off-screen test (a removal must never be visible) ────────────────

        Camera _cam;
        readonly Plane[] _frustumPlanes = new Plane[6];

        /// <summary>
        /// True when the whole scene - a <see cref="ConveyorConfig.SceneRadius"/> + margin sphere at
        /// <paramref name="anchor"/> - lies completely outside the player camera's view frustum, the
        /// only state in which its suction-to-a-point recycle is safe to run unseen. Uses the
        /// non-allocating frustum-planes overload; the <see cref="ConveyorConfig.OffscreenMargin"/>
        /// buffers against the player turning slightly while the container shrinks. Camera-less
        /// fallback (rare): a scene clearly BEHIND the flight course is never on screen, since the
        /// follow camera trails the vessel along that course.
        /// </summary>
        bool IsSceneOffScreen(Vector3 anchor, Vector3 playerPos, Vector3 course)
        {
            float radius = _cfg.SceneRadius + Mathf.Max(0f, _cfg.OffscreenMargin);

            var cam = ResolveCamera();
            if (cam)
            {
                GeometryUtility.CalculateFrustumPlanes(cam, _frustumPlanes);
                // Unity's frustum-plane normals point INWARD. If the bounding sphere sits fully on
                // the OUTSIDE half-space of ANY plane (near / far / left / right / top / bottom), the
                // scene is wholly outside the frustum -> not visible. Conservative in the safe
                // direction: a sphere straddling a plane counts as visible, so we refuse to recycle.
                for (int i = 0; i < _frustumPlanes.Length; i++)
                    if (_frustumPlanes[i].GetDistanceToPoint(anchor) < -radius)
                        return true;
                return false;
            }

            return Vector3.Dot(anchor - playerPos, course) < -radius;
        }

        /// <summary>
        /// The player camera, cached and re-resolved only when the cached one dies (scene loads,
        /// vessel swaps). <see cref="Camera.main"/> tag-searches, so it must not run every tick - the
        /// same pattern as the toy labels' <c>BillboardLabel</c>.
        /// </summary>
        Camera ResolveCamera()
        {
            if (!_cam) _cam = Camera.main;
            return _cam;
        }
    }
}
