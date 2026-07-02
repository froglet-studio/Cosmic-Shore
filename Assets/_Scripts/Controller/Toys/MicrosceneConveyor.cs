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
        public int PoolSize = 6;
        public int PrismBudget = 42;
        public float SceneRadius = 55f;
        public float SceneSpacing = 220f;
        public float FirstSceneDistance = 170f;
        public float LookaheadDistance = 470f;
        public float RecycleBehindDistance = 320f;
        public float TransitionSeconds = 1.2f;
        public float CourseFollow = 0.6f;
        public int MaxCrystalsPerScene = 3;
        public bool LifeformScenes = true;
        public int Seed;
    }

    /// <summary>
    /// The freestyle conveyor-belt runner: keeps a chain of <see cref="Microscene"/>s blooming in
    /// ahead of the local player's flight path — open-world exploring crossed with an infinite
    /// runner. Scenes arrive one after another until the pool is populated; from then on the belt
    /// is a CLOSED SYSTEM — the scene farthest behind is suctioned up, re-arranged from the
    /// shuffled recipe pool with fresh domain/rotation variation, and bloomed back in ahead, so
    /// the ride never repeats obviously and never creates or removes mass (fauna grazing on belt
    /// prisms is the only sink, and it's the food web's own active force).
    ///
    /// Toy-faithful: no score, no end condition, no timers — every belt advance is driven by the
    /// player's own motion. Exiting freestyle just makes the belt dormant; its scenes remain part
    /// of the world (conserved mass, living citizens) until the toybox tears down with the scene.
    /// </summary>
    public class MicrosceneConveyor : MonoBehaviour
    {
        const float TickSeconds = 0.25f;

        // Two arrivals may transition at once: with one, belt throughput tops out around
        // full-throttle speed and a boost burst permanently outruns the belt; with two it
        // comfortably covers throttle + elemental Time buffs while bursts just trigger a
        // re-anchor catch-up.
        const int MaxConcurrentArrivals = 2;

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
        Vector3 _boundsCenter;
        float _boundsRadius;
        float _anchorRadius; // bounds minus scene extent, so edge-scene CONTENT stays inside too
        float _nextTickAt;
        bool _running;

        public bool IsRunning => _running;

        public void Begin(ConveyorConfig cfg, IVesselStatus vessel, Func<bool> isFreestyleActive, GameDataSO gameData)
        {
            _cfg = cfg;
            _vessel = vessel;
            _isFreestyleActive = isFreestyleActive;
            _gameData = gameData;
            _rng = cfg.Seed != 0 ? new System.Random(cfg.Seed) : new System.Random(Environment.TickCount);

            ResolveBounds(vessel.Transform.position);
            ReanchorAhead();
            _running = true;
        }

        /// <summary>Re-entry pass through the toy (or after a vessel swap): keep the belt, follow the new vessel.</summary>
        public void Resume(IVesselStatus vessel)
        {
            _vessel = vessel;
            if (!_running) _running = true;

            // If the player wandered far from the whole belt, restart the chain ahead of them —
            // the abandoned scenes recycle naturally as the farthest-behind candidates.
            if (NearestSceneDistance(vessel.Transform.position) > _cfg.LookaheadDistance * 1.5f)
                ReanchorAhead();
        }

        void Update()
        {
            if (!_running || _cfg == null) return;
            if (Time.unscaledTime < _nextTickAt) return;
            _nextTickAt = Time.unscaledTime + TickSeconds;

            // Dormant while the player is back in the menu / lava lamp — the belt's scenes stay
            // in the world (conserved mass, released citizens), it just stops advancing.
            if (_isFreestyleActive != null && !_isFreestyleActive()) return;
            if (!TryGetVessel(out Vector3 playerPos, out Vector3 course)) return;
            if (BusySceneCount() >= MaxConcurrentArrivals) return;

            float frontier = FrontierProgress(playerPos, course);
            if (frontier >= _cfg.LookaheadDistance) return; // enough world ahead already

            // If the recorded head fell behind the player (sharp turn, long wander, re-entry),
            // restart the chain from the player's own path instead of extending a stale line.
            if (Vector3.Dot(_headAnchor - playerPos, course) < _cfg.FirstSceneDistance * 0.4f)
                ReanchorAhead();

            bool placed = _scenes.Count < _cfg.PoolSize
                ? PlaceNewScene()
                : RecycleFarthestScene(playerPos, course);

            if (placed)
                AdvanceHead(playerPos, course); // step the head to where the NEXT scene will arrive
        }

        // ── Belt geometry ────────────────────────────────────────────────────

        void ReanchorAhead()
        {
            if (!TryGetVessel(out Vector3 pos, out Vector3 course)) return;
            _headDir = course;
            _headAnchor = ClampToBounds(pos + course * _cfg.FirstSceneDistance);
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

        void AdvanceHead(Vector3 playerPos, Vector3 course)
        {
            // Follow the player's course with a little organic wander…
            Vector3 dir = Vector3.Slerp(_headDir, course, _cfg.CourseFollow);
            dir += new Vector3(Jitter(0.18f), Jitter(0.22f), Jitter(0.18f));
            dir = dir.sqrMagnitude > 0.001f ? dir.normalized : course;

            Vector3 candidate = _headAnchor + dir * _cfg.SceneSpacing;

            // …but bend back inside the play bounds so belt mass stays inside the cell's sense
            // radius (mass outside is invisible to the ecosystem) and the run arcs through the
            // world instead of leaving it.
            Vector3 fromCenter = candidate - _boundsCenter;
            if (fromCenter.magnitude > _anchorRadius * 0.85f)
            {
                Vector3 inward = (_boundsCenter - _headAnchor).normalized;
                dir = Vector3.Slerp(dir, inward, Mathf.InverseLerp(_anchorRadius * 0.85f, _anchorRadius * 1.15f, fromCenter.magnitude));
                dir = dir.normalized;
                candidate = _headAnchor + dir * _cfg.SceneSpacing;
            }

            _headAnchor = ClampToBounds(candidate);
            _headDir = dir;
        }

        Vector3 ClampToBounds(Vector3 position)
        {
            Vector3 offset = position - _boundsCenter;
            return offset.magnitude <= _anchorRadius ? position : _boundsCenter + offset.normalized * _anchorRadius;
        }

        void ResolveBounds(Vector3 near)
        {
            var cell = Cell.FindCellContaining(near);
            if (!cell) cell = Cell.FindNearestActiveCell(near); // Unity-null-safe fallback, no ??
            if (cell && cell.SenseRadius > 10f)
            {
                _boundsCenter = cell.transform.position;
                _boundsRadius = cell.SenseRadius * 0.85f;
            }
            else
            {
                _boundsCenter = Vector3.zero;
                _boundsRadius = 420f; // matches the toybox's no-membrane fallback play space
            }

            // Anchors are clamped tighter than the raw bounds by the scene's own reach (content
            // extends ~1.1 × radius along the flight axis, crystals a bit past that), so even an
            // edge scene's farthest prism still registers inside the cell's sense radius.
            _anchorRadius = Mathf.Max(60f, _boundsRadius - (_cfg.SceneRadius * 1.1f + 30f));
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

        bool RecycleFarthestScene(Vector3 playerPos, Vector3 course)
        {
            // Only scenes genuinely out of the ride are reclaimable: BEHIND the player's course
            // and beyond the recycle distance, or so far away they're an abandoned chain. A pure
            // farthest-by-distance pick would suction fully visible destinations right on the
            // player's flight line after a course reversal.
            float abandonedDistance = Mathf.Max(_cfg.LookaheadDistance * 2f, _cfg.RecycleBehindDistance * 2f);

            Microscene candidate = null;
            float farthest = 0f;
            foreach (var scene in _scenes)
            {
                if (!scene || scene.Busy) continue;
                float dist = Vector3.Distance(scene.Anchor, playerPos);
                bool behind = Vector3.Dot(scene.Anchor - playerPos, course) < 0f;
                bool eligible = (behind && dist > _cfg.RecycleBehindDistance) || dist > abandonedDistance;
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
                int recipes = _cfg.LifeformScenes ? MicroscenePatterns.RecipeCount : MicroscenePatterns.RecipeCount - 2;
                for (int i = 0; i < recipes; i++) _recipeBag.Add(i);
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

        bool TryGetVessel(out Vector3 position, out Vector3 course)
        {
            position = default;
            course = default;

            // A vessel swap (Vessel Changer toy / selection panel) destroys the pinned vessel —
            // re-acquire the live local one so the belt follows the new ship instead of
            // freezing until the player re-flies the Wanderway trigger.
            if (_vessel == null || (_vessel is UnityEngine.Object uo && !uo) || _vessel.Vessel == null)
                _vessel = _gameData?.LocalPlayer?.Vessel?.VesselStatus;
            if (_vessel == null || (_vessel is UnityEngine.Object o && !o) || _vessel.Vessel == null) return false;

            position = _vessel.Transform.position;
            course = _vessel.Course.sqrMagnitude > 0.01f ? _vessel.Course.normalized : _vessel.Transform.forward;
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
