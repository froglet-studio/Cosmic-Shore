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
        public SkimmerCrystalEffectSO[] CrystalEffects;
        public int PoolSize = 10;
        public int PrismBudget = 42;
        public float SceneRadius = 55f;
        public float SceneSpacing = 220f;
        public float FirstSceneDistance = 170f;
        public int AheadTargetScenes = 7;
        public float MinSceneIntervalSeconds = 2f;
        public float RecycleBehindDistance = 250f;
        public float TransitionSeconds = 1.2f;
        public float CourseFollow = 0.6f;
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
    /// Toy-faithful: no score, no end condition, no timers — every belt advance is driven by the
    /// player's own motion. The Wanderway toy toggles the belt on/off; exiting freestyle just
    /// makes it dormant. Either way its scenes remain part of the world until the toybox tears
    /// down with the scene.
    /// </summary>
    public class MicrosceneConveyor : MonoBehaviour
    {
        const float TickSeconds = 0.25f;

        // Three arrivals may transition at once: the belt must outpace full throttle + elemental
        // Time buffs (~150 u/s), and at high speed the speed-scaled spacing stretches so each
        // arrival buys more distance. Boost bursts beyond that just trigger a re-anchor catch-up.
        const int MaxConcurrentArrivals = 3;

        ConveyorConfig _cfg;
        IVesselStatus _vessel;
        Func<bool> _isFreestyleActive;
        GameDataSO _gameData;

        readonly List<Microscene> _scenes = new();
        readonly List<int> _recipeBag = new();
        readonly List<Domains> _domainCycle = new();
        int _domainIndex;

        System.Random _rng;
        Vector3 _headAnchor;
        Vector3 _headDir = Vector3.forward;
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

            ReanchorAhead();
            _running = true;
        }

        /// <summary>Re-entry pass through the toy (or after a vessel swap): keep the field, resume the flow.</summary>
        public void Resume(IVesselStatus vessel)
        {
            _vessel = vessel;
            _running = true;

            // If the player wandered far from the whole field, restart the chain ahead of them —
            // the abandoned scenes recycle naturally as the farthest-behind candidates.
            if (NearestSceneDistance(vessel.Transform.position) > _cfg.SceneSpacing * _cfg.AheadTargetScenes)
                ReanchorAhead();
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

            float frontier = FrontierProgress(playerPos, course);
            if (frontier >= lookahead) return; // enough world ahead already

            // If the recorded head fell behind the player (sharp turn, long wander, re-entry),
            // restart the chain from the player's own path instead of extending a stale line.
            if (Vector3.Dot(_headAnchor - playerPos, course) < FirstDistance(speed) * 0.4f)
                ReanchorAhead();

            bool placed = _scenes.Count < _cfg.PoolSize
                ? PlaceNewScene()
                : RecycleFarthestScene(playerPos, course, recycleBehind, lookahead);

            if (placed)
                AdvanceHead(course, effSpacing); // step the head to where the NEXT scene will arrive
        }

        // ── Belt geometry ────────────────────────────────────────────────────

        float FirstDistance(float speed) => Mathf.Max(_cfg.FirstSceneDistance, speed * 1.2f);

        void ReanchorAhead()
        {
            if (!TryGetVessel(out Vector3 pos, out Vector3 course, out float speed)) return;
            _headDir = course;
            _headAnchor = pos + course * FirstDistance(speed);
        }

        /// <summary>How far ahead (along the course) the front-most scene sits. Negative = all behind.</summary>
        float FrontierProgress(Vector3 playerPos, Vector3 course)
        {
            float best = float.MinValue;
            foreach (var scene in _scenes)
            {
                if (!scene) continue;
                best = Mathf.Max(best, Vector3.Dot(scene.Anchor - playerPos, course));
            }
            return best == float.MinValue ? 0f : best;
        }

        void AdvanceHead(Vector3 course, float effSpacing)
        {
            // Follow the player's course with a little organic wander — the belt goes wherever
            // they point it, including far outside the cell (open-space scenes are just prisms +
            // crystals; the living recipes re-engage whenever the path re-enters a cell).
            Vector3 dir = Vector3.Slerp(_headDir, course, _cfg.CourseFollow);
            dir += new Vector3(Jitter(0.18f), Jitter(0.22f), Jitter(0.18f));
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : course;

            _headAnchor += dir * effSpacing;
            _headDir = dir;
        }

        // ── Scene arrivals ───────────────────────────────────────────────────

        bool PlaceNewScene()
        {
            var scene = Microscene.Create(transform, _scenes.Count.ToString());
            scene.Configure(_cfg.PrismPrefab, _cfg.CrystalEffects);
            scene.transform.SetPositionAndRotation(_headAnchor, Quaternion.LookRotation(_headDir, Vector3.up));
            _scenes.Add(scene);

            var plan = NextPlan();
            // Each arrival gets its own derived rng: async populate/recycle draws would otherwise
            // interleave on the shared stream and break per-seed reproducibility.
            var sceneRng = new System.Random(_rng.Next());
            scene.PopulateAsync(plan, NextDomain(), sceneRng, this.GetCancellationTokenOnDestroy()).Forget();
            return true;
        }

        bool RecycleFarthestScene(Vector3 playerPos, Vector3 course, float recycleBehind, float lookahead)
        {
            // Only scenes genuinely out of the ride are reclaimable: BEHIND the player's course
            // and beyond the recycle distance, or so far away they're an abandoned chain. A pure
            // farthest-by-distance pick would suction fully visible destinations right on the
            // player's flight line after a course reversal.
            float abandonedDistance = Mathf.Max(lookahead * 2f, recycleBehind * 2f);

            Microscene candidate = null;
            float farthest = 0f;
            foreach (var scene in _scenes)
            {
                if (!scene || scene.Busy) continue;
                float dist = Vector3.Distance(scene.Anchor, playerPos);
                bool behind = Vector3.Dot(scene.Anchor - playerPos, course) < 0f;
                bool eligible = (behind && dist > recycleBehind) || dist > abandonedDistance;
                if (eligible && dist > farthest)
                {
                    farthest = dist;
                    candidate = scene;
                }
            }

            if (!candidate) return false;

            var pose = new Pose(_headAnchor, Quaternion.LookRotation(_headDir, Vector3.up));
            var plan = NextPlan();
            var sceneRng = new System.Random(_rng.Next()); // see PlaceNewScene — per-arrival stream
            candidate.RecycleAsync(plan, pose, NextDomain(), sceneRng, _cfg.TransitionSeconds,
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
            return MicroscenePatterns.Plan(recipe, _rng, _cfg.PrismBudget, _cfg.SceneRadius, _cfg.MaxCrystalsPerScene);
        }

        /// <summary>
        /// Read the live player domains on EVERY draw — never snapshot domain at creation time
        /// (CLAUDE.md ▸ Team Domains): the Domain Changer toy can re-pick mid-freestyle and the
        /// belt should start colouring scenes from the new set immediately.
        /// </summary>
        Domains NextDomain()
        {
            _domainCycle.Clear();
            if (_gameData?.Players != null)
                foreach (var player in _gameData.Players)
                    if (player != null && player.Domain != Domains.Blue && !_domainCycle.Contains(player.Domain))
                        _domainCycle.Add(player.Domain);

            if (_domainCycle.Count == 0)
            {
                _domainCycle.Add(Domains.Jade);
                _domainCycle.Add(Domains.Ruby);
                _domainCycle.Add(Domains.Gold);
            }

            return _domainCycle[_domainIndex++ % _domainCycle.Count];
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

        float NearestSceneDistance(Vector3 from)
        {
            float best = float.MaxValue;
            foreach (var scene in _scenes)
                if (scene) best = Mathf.Min(best, Vector3.Distance(scene.Anchor, from));
            return best;
        }

        float Jitter(float magnitude) => (float)(_rng.NextDouble() * 2 - 1) * magnitude;
    }
}
