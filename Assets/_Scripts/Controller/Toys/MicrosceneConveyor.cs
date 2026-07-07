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
        public float PathSpread = 0.6f;
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
    /// Placement is NOT a connected ribbon. Each scene lands directly on the player's LIVE flight
    /// line (position + course), scattered a little laterally so the field has width — there is no
    /// running "head" the scenes chain off. The field's reach is measured along the current course
    /// inside a flight corridor, so the instant the player changes direction the old field falls
    /// off-corridor, its reach collapses, and fresh scenes drop straight into the NEW path; the
    /// now-lateral leftovers become the recycle candidates that rebuild ahead. Structures appear in
    /// front of the player shortly after any turn, regardless of where the belt was pointing.
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

        ConveyorConfig _cfg;
        IVesselStatus _vessel;
        Func<bool> _isFreestyleActive;
        GameDataSO _gameData;

        readonly List<Microscene> _scenes = new();
        readonly List<int> _recipeBag = new();
        readonly List<Domains> _domainCycle = new();
        int _domainIndex;

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

            // No head to seed — the first Update tick places directly ahead of the live vessel.
            _running = true;
        }

        /// <summary>Re-entry pass through the toy (or after a vessel swap): keep the field, resume the flow.</summary>
        public void Resume(IVesselStatus vessel)
        {
            // Re-acquire the (possibly swapped) vessel; Update rebuilds the field directly ahead of
            // wherever the player now is, so nothing else needs restarting — abandoned scenes
            // recycle naturally as off-corridor candidates.
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

            // Half-width of the flight corridor: the band around the player's course that counts as
            // "the path directly ahead." Wider than the placement scatter (so fresh scenes register
            // as in-corridor), far narrower than the spacing (so a turn clearly ejects the old
            // field out of the corridor). Everything outside it is a leftover, not part of the ride.
            float scatter = _cfg.SceneRadius * _cfg.PathSpread;
            float corridor = _cfg.SceneRadius * 2f + scatter;

            // How far the field already reaches straight ahead of the player, measured ONLY over
            // scenes inside the corridor. After a direction change the old scenes are off-corridor,
            // so this collapses and fresh scenes fill the new path from FirstDistance outward.
            float frontier = FrontierProgress(playerPos, course, corridor);
            if (frontier >= lookahead) return; // enough world ahead already

            float nextDist = frontier <= 0.01f
                ? FirstDistance(speed)
                : Mathf.Max(FirstDistance(speed), frontier + effSpacing);
            Pose pose = PoseAhead(playerPos, course, nextDist, scatter);

            if (_scenes.Count < _cfg.PoolSize)
                PlaceNewScene(pose);
            else
                RecycleFarthestScene(playerPos, course, recycleBehind, corridor, pose);
        }

        // ── Belt geometry ────────────────────────────────────────────────────

        float FirstDistance(float speed) => Mathf.Max(_cfg.FirstSceneDistance, speed * 1.2f);

        /// <summary>
        /// How far ahead the field reaches along the current course, counting only scenes inside
        /// the flight corridor. Scenes behind, or laterally off the flight line (a turn's
        /// leftovers), don't extend the path — so 0 means "nothing in front of me on this heading."
        /// </summary>
        float FrontierProgress(Vector3 playerPos, Vector3 course, float corridor)
        {
            float best = 0f;
            foreach (var scene in _scenes)
            {
                if (!scene) continue;
                Vector3 rel = scene.Anchor - playerPos;
                float along = Vector3.Dot(rel, course);
                if (along <= 0f) continue;                          // behind — not ahead of me
                if (Vector3.Distance(rel, course * along) > corridor) continue; // off to the side
                if (along > best) best = along;
            }
            return best;
        }

        /// <summary>
        /// A pose <paramref name="distanceAhead"/> down the player's live flight line, scattered up
        /// to <paramref name="scatter"/> laterally so the field reads as a field (not a single-file
        /// line), and oriented so the scene's +z runs along the course — you fly straight into it.
        /// </summary>
        Pose PoseAhead(Vector3 playerPos, Vector3 course, float distanceAhead, float scatter)
        {
            Vector3 up = Mathf.Abs(Vector3.Dot(course, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
            Vector3 right = Vector3.Cross(up, course).normalized;
            up = Vector3.Cross(course, right).normalized;

            Vector3 lateral = (right * Jitter(1f) + up * Jitter(1f)) * scatter;
            Vector3 pos = playerPos + course * distanceAhead + lateral;
            return new Pose(pos, Quaternion.LookRotation(course, up));
        }

        // ── Scene arrivals ───────────────────────────────────────────────────

        void PlaceNewScene(Pose pose)
        {
            var scene = Microscene.Create(transform, _scenes.Count.ToString());
            scene.Configure(_cfg.PrismPrefab, _cfg.CrystalEffects);
            scene.transform.SetPositionAndRotation(pose.position, pose.rotation);
            _scenes.Add(scene);

            var plan = NextPlan();
            // Each arrival gets its own derived rng: async populate/recycle draws would otherwise
            // interleave on the shared stream and break per-seed reproducibility.
            var sceneRng = new System.Random(_rng.Next());
            scene.PopulateAsync(plan, NextDomain(), sceneRng, this.GetCancellationTokenOnDestroy()).Forget();
        }

        bool RecycleFarthestScene(Vector3 playerPos, Vector3 course, float recycleBehind, float corridor, Pose pose)
        {
            // Reclaimable = NOT part of the ride the player is flying into. A scene stays protected
            // while it is near the flight line (inside the corridor) and not yet far behind — i.e.
            // something the player is heading toward or just passed. Everything else — scenes left
            // off to the side by a course change, or dropped far behind — is fair game, farthest
            // first, so the most-abandoned mass rebuilds the new path ahead.
            Microscene candidate = null;
            float farthest = 0f;
            foreach (var scene in _scenes)
            {
                if (!scene || scene.Busy) continue;
                Vector3 rel = scene.Anchor - playerPos;
                float along = Vector3.Dot(rel, course);
                float perp = Vector3.Distance(rel, course * along);
                float dist = rel.magnitude;

                bool onCorridorAhead = perp <= corridor && along > -recycleBehind;
                if (onCorridorAhead) continue; // still part of the ride — leave it be
                if (dist <= farthest) continue;

                farthest = dist;
                candidate = scene;
            }

            if (!candidate) return false;

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

        float Jitter(float magnitude) => (float)(_rng.NextDouble() * 2 - 1) * magnitude;
    }
}
