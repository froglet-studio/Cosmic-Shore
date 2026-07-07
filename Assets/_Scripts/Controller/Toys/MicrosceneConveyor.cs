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
        public Prism PrismPrefab;
        public Crystal OmniCrystalPrefab;
        public SkimmerCrystalEffectSO[] CrystalEffects;
        public MicroscenePalette Palette = new();
        public int PoolSize = 10;
        public int PrismBudget = 42;
        public float SceneRadius = 55f;
        public float SceneSpacing = 220f;
        public float FirstSceneDistance = 170f;
        public int AheadTargetScenes = 7;
        public float MinSceneIntervalSeconds = 2f;
        public float RecycleBehindDistance = 250f;
        public float TransitionSeconds = 1.2f;
        public float TurnBreakDegrees = 55f;
        public int MaxCrystalsPerScene = 3;
        public bool LifeformScenes = true;
        public int Seed;
    }

    /// <summary>
    /// The freestyle conveyor-belt runner: keeps a field of <see cref="Microscene"/>s blooming in
    /// ahead of the local player's flight path — open-world exploring crossed with an infinite
    /// runner. The belt follows the player ANYWHERE (fast, far, odd deviations included): spacing
    /// and lookahead scale with current speed so there is always a field of scenes ahead, and the
    /// scene farthest behind clears (suctions up) as new ones arrive — spawn frequency IS the
    /// clear frequency, because the pool is finite and closed: the same prisms are endlessly
    /// re-arranged, never created or destroyed (fauna grazing on belt prisms is the only sink,
    /// and it's the food web's own active force). Living recipes (meadow/menagerie) release their
    /// flora/fauna only while the scene sits inside a live <see cref="Cell"/>; out in open space
    /// the belt is pure prisms + crystals.
    ///
    /// Placement is a CONNECTED ribbon that can break and re-lay. Every scene sits on the flight
    /// line — never scattered laterally (no orthogonal "sphere in front of you"). The belt reads a
    /// forward cone (half-angle <see cref="ConveyorConfig.TurnBreakDegrees"/>) around the live
    /// course each tick and does one of two things:
    ///   • NEAR-FILL — if nothing lies just ahead on the current heading (start-up, or a turn just
    ///     ejected the near scenes out of the cone), it drops a scene directly ahead at
    ///     firstSceneDistance, so a structure appears in front of the player right after any turn.
    ///   • EXTEND — otherwise it chains the next scene off the ACTUAL frontmost scene along the
    ///     current course (tip + course × spacing). Chaining off real mass (not a free-floating
    ///     "head") keeps the ribbon connected and lets it BEND with gentle/moderate turns without
    ///     ever drifting into a parallel path far to the side.
    /// A sharp turn drops the whole old ribbon out of the cone: its reach collapses, near-fill
    /// re-lays straight down the NEW heading, and the now-lateral leftovers become the
    /// farthest-first recycle candidates that rebuild ahead. A scene mid-recycle claims its
    /// destination slot immediately (<see cref="Microscene.PendingAnchor"/>) so the rebuild never
    /// piles several scenes onto the same point while the blooms are in flight.
    ///
    /// Toy-faithful: no score, no end condition, no timers — every belt advance is driven by the
    /// player's own motion. The Wanderway toy toggles the belt on/off; exiting freestyle just
    /// makes it dormant. Either way its scenes remain part of the world until the toybox tears
    /// down with the scene.
    /// </summary>
    public class MicrosceneConveyor : MonoBehaviour
    {
        const float TickSeconds = 0.25f;

        // Two arrivals may transition at once (was 3 — halved worst-case per-frame transition cost
        // for the mobile strip): the belt must outpace full throttle + elemental Time buffs
        // (~150 u/s), and at high speed the speed-scaled spacing stretches so each arrival buys
        // more distance. Boost bursts beyond that just fill in over the next few ticks.
        const int MaxConcurrentArrivals = 2;

        // A near hole opens (and the belt drops a fresh scene straight ahead) once the nearest scene
        // on the flight line sits farther than firstDistance + this × spacing — big enough that a
        // just-placed near scene doesn't immediately re-trigger, small enough that a real gap fills.
        const float NearHoleSpacingFactor = 0.5f;

        ConveyorConfig _cfg;
        IVesselStatus _vessel;
        Func<bool> _isFreestyleActive;
        GameDataSO _gameData;

        readonly List<Microscene> _scenes = new();
        readonly List<int> _recipeBag = new();
        readonly List<Domains> _domainList = new();

        System.Random _rng;
        float _nextTickAt;
        bool _running;

        /// <summary>True while the belt is flowing (the Wanderway toy toggles this).</summary>
        public bool IsRunning => _running;

        public void Begin(ConveyorConfig cfg, IVesselStatus vessel, Func<bool> isFreestyleActive, GameDataSO gameData)
        {
            _cfg = cfg;
            _vessel = vessel;
            _isFreestyleActive = isFreestyleActive;
            _gameData = gameData;
            _rng = cfg.Seed != 0 ? new System.Random(cfg.Seed) : new System.Random(Environment.TickCount);

            // Nothing to seed — the first Update tick sees an empty cone and near-fills a scene
            // directly ahead of the live vessel.
            _running = true;
        }

        /// <summary>Re-entry pass through the toy (or after a vessel swap): keep the field, resume the flow.</summary>
        public void Resume(IVesselStatus vessel)
        {
            // Re-acquire the (possibly swapped) vessel; the next Update near-fills the ribbon directly
            // ahead of wherever the player now is, and any scenes left behind recycle naturally as
            // off-cone candidates. Nothing else needs restarting.
            _vessel = vessel;
            _running = true;
        }

        /// <summary>
        /// Stop the flow (fly through the toy again to restart). Existing scenes stay in the
        /// world — they are conserved mass and released citizens, not toy props to vanish.
        /// </summary>
        public void StopBelt() => _running = false;

        void Update()
        {
            if (!_running || _cfg == null) return;
            if (Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + TickSeconds;

            // Dormant while the player is back in the menu / lava lamp — the belt's scenes stay
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
            // Only scenes inside the cone count — a turn ejects the old ribbon out of it, so both
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
                // the near scenes) — drop a scene directly ahead on the live course so a structure
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

            Pose pose = new(target, Quaternion.LookRotation(course, UpFor(course)));

            if (_scenes.Count < _cfg.PoolSize)
                PlaceNewScene(pose);
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

        void PlaceNewScene(Pose pose)
        {
            var scene = Microscene.Create(transform, _scenes.Count.ToString());
            scene.Configure(_cfg.PrismPrefab, _cfg.OmniCrystalPrefab, _cfg.CrystalEffects);
            scene.transform.SetPositionAndRotation(pose.position, pose.rotation);
            _scenes.Add(scene);

            var plan = NextPlan();
            // Each arrival gets its own derived rng: async populate/recycle draws would otherwise
            // interleave on the shared stream and break per-seed reproducibility.
            var sceneRng = new System.Random(_rng.Next());
            scene.PopulateAsync(plan, sceneRng, this.GetCancellationTokenOnDestroy()).Forget();
        }

        bool RecycleFarthestScene(Vector3 playerPos, Vector3 course, float recycleBehind, float lookahead,
            float leadCos, Pose pose, bool nearFill)
        {
            // Pass 1: reclaim only mass genuinely out of the ride — protect the ribbon ahead (in the
            // flight cone, what the player is flying toward) and just-passed scenes. This is the
            // steady-state path for every normal flight; a turn's now-lateral leftovers and
            // dropped-behind scenes rebuild ahead, farthest first.
            if (TryRecycleFarthest(playerPos, course, recycleBehind, lookahead, leadCos, pose,
                    protectRibbonAhead: true))
                return true;

            // Pass 2 (near-fill only): the near field is empty AND nothing off-cone/behind is
            // reclaimable (e.g. the whole belt bunched far ahead while the player is near-stationary).
            // Steal the farthest in-cone scene to fill the hole — a surplus far structure is worth
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
                if (aheadInCone || justPassed) continue; // still part of the ride — leave it be
                if (dist <= farthest) continue;

                farthest = dist;
                candidate = scene;
            }

            if (!candidate) return false;

            var plan = NextPlan();
            var sceneRng = new System.Random(_rng.Next()); // see PlaceNewScene — per-arrival stream
            candidate.RecycleAsync(plan, pose, sceneRng, _cfg.TransitionSeconds,
                this.GetCancellationTokenOnDestroy()).Forget();
            return true;
        }

        // ── Variation (shuffle bag + domain cycle) ───────────────────────────

        MicroscenePlan NextPlan()
        {
            if (_recipeBag.Count == 0)
            {
                for (int i = 0; i < MicroscenePatterns.RecipeCount; i++)
                    if (_cfg.LifeformScenes || !MicroscenePatterns.IsLifeformRecipe(i))
                        _recipeBag.Add(i);
                for (int i = _recipeBag.Count - 1; i > 0; i--)
                {
                    int j = _rng.Next(i + 1);
                    (_recipeBag[i], _recipeBag[j]) = (_recipeBag[j], _recipeBag[i]);
                }
            }

            int recipe = _recipeBag[^1];
            _recipeBag.RemoveAt(_recipeBag.Count - 1);

            // Read the live player domains on EVERY draw — never snapshot domain at creation time
            // (CLAUDE.md ▸ Team Domains): the Domain Changer toy can re-pick mid-freestyle and the
            // belt should start colouring scenes from the new set immediately. The palette then
            // distributes them per-prism under a coherent per-scene scheme.
            _cfg.Palette.PlayableDomains = LivePlayableDomains();
            return MicroscenePatterns.Plan(recipe, _rng, _cfg.PrismBudget, _cfg.SceneRadius, _cfg.MaxCrystalsPerScene, _cfg.Palette);
        }

        /// <summary>The live, distinct playable domains in the session (falls back to all three).</summary>
        Domains[] LivePlayableDomains()
        {
            _domainList.Clear();
            if (_gameData?.Players != null)
                foreach (var player in _gameData.Players)
                    if (player != null && player.Domain != Domains.Blue && !_domainList.Contains(player.Domain))
                        _domainList.Add(player.Domain);

            if (_domainList.Count == 0)
            {
                _domainList.Add(Domains.Jade);
                _domainList.Add(Domains.Ruby);
                _domainList.Add(Domains.Gold);
            }

            return _domainList.ToArray();
        }

        // ── Plumbing ─────────────────────────────────────────────────────────

        bool TryGetVessel(out Vector3 position, out Vector3 course, out float speed)
        {
            position = default;
            course = default;
            speed = 0f;

            // A vessel swap (Vessel Changer toy / selection panel) destroys the pinned vessel —
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
    }
}
