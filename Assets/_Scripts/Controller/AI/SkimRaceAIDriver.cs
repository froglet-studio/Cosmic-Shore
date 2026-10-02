using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Skim Race's AI pilot: flies a hull along the ribbon with <see cref="SkimRacerBrain"/> — skims
    /// it close enough to build the boost, swings out to its own crystal, and threads the rails the
    /// other racers leave in the air. Installed on each AI's <see cref="AIPilot"/> by
    /// <c>SkimRaceController</c>; Docs/AISystem/SQUIRREL_SKIM.md is the design.
    ///
    /// <para><b>This class is only the senses and the hands.</b> Everything the AI decides lives in
    /// the pure brain (Assets/_Scripts/Controller/AI/SkimRacing/), which
    /// Tools/Build/squirrel_ai_harness compiles and races against a transcription of the flight
    /// model. Here the brain is handed what a human pilot can know — its own hull, its own boost
    /// gauge, the crystal it can collect, and the mass it can see ahead — read live off the vessel
    /// every frame, and its sticks and throttle are written to <c>InputStatus</c> exactly where a
    /// human's would be. Nothing reaches past the flight model (ARCHITECTURE.md R1/R2).</para>
    ///
    /// <para><b>Measured, not assumed.</b> The hull box comes from the vessel's own hull colliders
    /// (<see cref="VesselImpactor.HullColliders"/>), the skimmer's reach from its sphere at its live
    /// scale, the crystal's capture radius from its trigger, and the turn rates and rotation lag
    /// from the transformer — so a retune of the Squirrel retunes the pilot instead of leaving a
    /// copy of a number to drift. The harness flies a deliberately conservative box instead, because
    /// a YAML reader cannot pose the hull's bones.</para>
    /// </summary>
    public sealed class SkimRaceAIDriver : IAIPilotDriver
    {
        /// <summary>Seconds between sweeps of the spatial index for mass along the route ahead. The
        /// brain re-checks its line against them every 0.08 s and re-plans every 0.25 s; rails do
        /// not move, and a new one appears behind the racer that laid it.</summary>
        const float ObstacleScanInterval = 0.1f;
        /// <summary>How far ahead (route arc) mass is looked for: the length of line the brain plans.</summary>
        const float ObstacleHorizon = 980f;
        /// <summary>Route arc per spatial query.</summary>
        const float ObstacleStep = 70f;
        /// <summary>Radius around the ribbon's centre line mass is looked for in: a crystal is never
        /// more than ~70 u off the ribbon, and the line never strays much past it.</summary>
        const float ObstacleQueryRadius = 120f;
        /// <summary>Seconds between re-reads of which crystal is this racer's next.</summary>
        const float CrystalScanInterval = 0.2f;

        /// <summary>Used only if a hull carries no measurable collider: the harness's box, which
        /// bounds the whole Squirrel mesh with a margin.</summary>
        static readonly Vector3 FallbackHullHalfExtents = new(2.3f, 0.9f, 3.3f);
        /// <summary>Used only if the skimmer carries no sphere: the Squirrel's at rest.</summary>
        const float FallbackSkimRadius = 7.5f;
        /// <summary>Used only if a crystal carries no sphere trigger: Skim Race's crystal.</summary>
        const float FallbackCrystalRadius = 24f;

        readonly SkimRaceAICourse _course;
        readonly SkimRacerProfile _profile;
        readonly int _seed;

        SkimRacerBrain _brain;
        IVessel _hull;
        VesselTransformer _transformer;
        SphereCollider _skimSphere;
        Vector3 _hullHalfExtents;
        int _teleports;
        float _nextObstacleScan;
        float _nextCrystalScan;

        readonly List<SkimObstacle> _obstacles = new(512);
        readonly List<Prism> _scratch = new(256);
        readonly HashSet<Prism> _seen = new();

        Crystal _crystal;
        readonly Dictionary<Crystal, Vector4> _crystalArcs = new();   // xyz = where it was measured, w = route arc

        public SkimRaceAIDriver(SkimRaceAICourse course, SkimRacerProfile profile, int seed)
        {
            _course = course;
            _profile = profile ?? SkimRacerProfile.Expert();
            _seed = seed;
        }

        public bool Drive(AIPilot pilot, IVessel vessel, float deltaTime)
        {
            var status = vessel?.VesselStatus;
            // The brain speaks the dual-stick flight model; a one-thumb hull reads none of its axes.
            if (status == null || status.IsSingleStickControls) return false;
            var input = status.InputStatus;
            if (input == null) return false;
            if (!_course.TryGetRoute(out var route)) return false;
            if (!ReferenceEquals(vessel, _hull) && !Bind(vessel)) return false;

            if (_brain == null)
            {
                _brain = new SkimRacerBrain(route, _profile, _seed);
                if (_course.RacingLine != null) _brain.UseRacingLine(_course.RacingLine);
            }

            // A teleport is not travel: the line the brain was flying is somewhere else now.
            if (_transformer.TeleportCount != _teleports)
            {
                _teleports = _transformer.TeleportCount;
                _brain.Reset();
            }

            float now = Time.time;
            var tf = vessel.Transform;
            var sensors = ReadSensors(tf, status, deltaTime, now);
            ResolveCrystal(route, status, tf.position, now, ref sensors);

            if (now >= _nextObstacleScan)
            {
                _nextObstacleScan = now + ObstacleScanInterval;
                // Before the brain's first tick it has no arc of its own yet.
                float from = _brain.KeyCount > 0 ? _brain.RouteS : route.ProjectGlobal(tf.position);
                GatherObstacles(route, status, from);
            }

            var cmd = _brain.Tick(sensors, _obstacles);
            _course.OfferRacingLine(_brain.RacingLine);

            input.XSum = cmd.XSum;
            input.YSum = cmd.YSum;
            input.YDiff = cmd.YDiff;
            input.XDiff = cmd.XDiff;
            return true;
        }

        public void Release()
        {
            _brain?.Reset();
            _hull = null;
            _transformer = null;
            _skimSphere = null;
            _crystal = null;
            _nextObstacleScan = 0f;
            _nextCrystalScan = 0f;
            _obstacles.Clear();
        }

        // ============================================================================ senses

        bool Bind(IVessel vessel)
        {
            Release();
            var status = vessel.VesselStatus;
            _transformer = status.VesselTransformer;
            if (_transformer == null) return false;
            _hull = vessel;
            _teleports = _transformer.TeleportCount;
            _hullHalfExtents = MeasureHull(vessel.Transform);
            _skimSphere = ResolveSkimSphere(status);
            if (CSDebug.IsVerbose(CSLogChannel.SkimRacerAI))
                CSDebug.LogVerbose(CSLogChannel.SkimRacerAI,
                    $"[SkimRacerAI] {status.PlayerName}: hull half-extents {_hullHalfExtents}, " +
                    $"skim radius {SkimRadius():F1}, follow rate {_transformer.RotationFollowRate:F2}/s");
            return true;
        }

        SkimRacerSensors ReadSensors(Transform tf, IVesselStatus status, float deltaTime, float now)
        {
            Vector3 course = status.Course;
            return new SkimRacerSensors
            {
                Time = now,
                DeltaTime = deltaTime,
                Position = tf.position,
                Rotation = tf.rotation,
                CommandedRotation = _transformer.CommandedRotation,
                Course = course.sqrMagnitude > 1e-6f ? course : tf.forward,
                Speed = status.Speed,
                // What the throttle target multiplies by: the gauge only counts while boosting.
                BoostMultiplier = status.IsBoosting ? status.BoostMultiplier : 1f,
                MaxBoost = _transformer.MaxBoost,
                ThrottleScaler = _transformer.EffectiveThrottleScaler,
                PitchRateDegrees = _transformer.PitchRateDegreesPerSecond,
                YawRateDegrees = _transformer.YawRateDegreesPerSecond,
                RollRateDegrees = _transformer.RollRateDegreesPerSecond,
                FollowRate = _transformer.RotationFollowRate,
                SkimRadius = SkimRadius(),
                HullHalfExtents = _hullHalfExtents,
                IsDrifting = status.IsDrifting,
            };
        }

        /// <summary>
        /// The crystal this racer is after: of the crystals it can collect, the nearest one AHEAD
        /// along the route. In the usual race each racer's domain has exactly one, and this is
        /// simply it; with teammates on one domain there is one per teammate, and the next one on
        /// the ribbon is the one to fly through.
        /// </summary>
        void ResolveCrystal(SkimRoute route, IVesselStatus status, Vector3 position, float now, ref SkimRacerSensors sensors)
        {
            var domain = status.Domain;
            bool stale = _crystal == null || !_crystal.isActiveAndEnabled || !_crystal.CanBeCollected(domain);
            if (stale || now >= _nextCrystalScan)
            {
                _nextCrystalScan = now + CrystalScanInterval;
                _crystal = PickCrystal(route, domain, position);
            }
            if (_crystal == null) return;

            sensors.HasCrystal = true;
            sensors.CrystalPosition = _crystal.transform.position;
            sensors.CrystalRadius = CrystalRadius(_crystal);
        }

        Crystal PickCrystal(SkimRoute route, Domains domain, Vector3 position)
        {
            float here = _brain.KeyCount > 0 ? _brain.RouteS : route.ProjectGlobal(position);
            here = route.Wrap(here);
            Crystal best = null;
            float bestAhead = float.MaxValue;
            var crystals = Crystal.Active;
            for (int i = 0, n = crystals.Count; i < n; i++)
            {
                var crystal = crystals[i];
                if (crystal == null || crystal.CrystalManager == null) continue;
                if (!crystal.CanBeCollected(domain)) continue;

                float ahead = route.Wrap(ArcOf(route, crystal) - here);
                if (ahead < bestAhead)
                {
                    bestAhead = ahead;
                    best = crystal;
                }
            }
            return best;
        }

        /// <summary>A crystal's route arc, measured once per place it spawns (the projection walks
        /// the whole route).</summary>
        float ArcOf(SkimRoute route, Crystal crystal)
        {
            Vector3 p = crystal.transform.position;
            if (_crystalArcs.TryGetValue(crystal, out var cached) &&
                ((Vector3)cached - p).sqrMagnitude < 1f)
                return cached.w;
            float arc = route.ProjectGlobal(p);
            _crystalArcs[crystal] = new Vector4(p.x, p.y, p.z, arc);
            return arc;
        }

        static float CrystalRadius(Crystal crystal)
        {
            if (!crystal.TryGetComponent(out SphereCollider sphere)) return FallbackCrystalRadius;
            return sphere.radius * MaxAxis(sphere.transform.lossyScale);
        }

        /// <summary>
        /// Every prism along the route ahead that is not the ribbon: other racers' rails, a ring's
        /// danger prisms, this racer's own older wake. Its own wake inside the self-trail grace is
        /// left out — the hull does not touch it either (<see cref="SelfTrailContactConfigSO"/>).
        /// A shielded prism is handed over as the box that bounds its shell, which is what a hull
        /// collides with.
        /// </summary>
        void GatherObstacles(SkimRoute route, IVesselStatus status, float from)
        {
            _obstacles.Clear();
            _seen.Clear();
            var index = PrismSpatialIndex.Instance;
            if (index == null || !index.IsAvailable) return;

            for (float ds = 0f; ds < ObstacleHorizon; ds += ObstacleStep)
            {
                Vector3 a = route.Position(from + ds);
                Vector3 b = route.Position(from + ds + ObstacleStep);
                index.QuerySegment(a, b, ObstacleQueryRadius, _scratch);
                for (int i = 0; i < _scratch.Count; i++)
                {
                    var prism = _scratch[i];
                    if (prism == null || !_seen.Add(prism)) continue;
                    if (_course.IsTrackPrism(prism)) continue;
                    if (SelfTrailContactConfigSO.SuppressesHullContact(prism, status)) continue;

                    Vector3 half = SkimRaceAICourse.HalfExtents(prism);
                    if (SkimRaceAICourse.IsShelled(prism)) half *= OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE;
                    var tf = prism.transform;
                    _obstacles.Add(new SkimObstacle(tf.position, tf.rotation, half));
                }
            }
        }

        float SkimRadius()
        {
            if (_skimSphere == null) return FallbackSkimRadius;
            return _skimSphere.radius * MaxAxis(_skimSphere.transform.lossyScale);
        }

        static SphereCollider ResolveSkimSphere(IVesselStatus status)
        {
            var skimmer = status.NearFieldSkimmer;
            return skimmer != null ? skimmer.GetComponentInChildren<SphereCollider>(true) : null;
        }

        /// <summary>
        /// Half-extents, along the vessel's own axes and in world units, of the box that holds every
        /// hull collider — the set the shell tier probes with, the skimmer excluded.
        /// </summary>
        static Vector3 MeasureHull(Transform root)
        {
            var impactor = root.GetComponentInChildren<VesselImpactor>(true);
            var colliders = impactor != null ? impactor.HullColliders : null;
            Vector3 ext = Vector3.zero;
            if (colliders != null)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    var col = colliders[i];
                    if (col == null || !col.enabled) continue;
                    if (col is BoxCollider box)
                    {
                        var t = box.transform;
                        Vector3 h = box.size * 0.5f;
                        for (int c = 0; c < 8; c++)
                        {
                            var corner = new Vector3((c & 1) == 0 ? -h.x : h.x, (c & 2) == 0 ? -h.y : h.y, (c & 4) == 0 ? -h.z : h.z);
                            Grow(root, t.TransformPoint(box.center + corner), ref ext);
                        }
                    }
                    else
                    {
                        var b = col.bounds;
                        for (int c = 0; c < 8; c++)
                            Grow(root, b.center + Vector3.Scale(b.extents,
                                new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1)), ref ext);
                    }
                }
            }
            return ext.x > 0.01f && ext.y > 0.01f && ext.z > 0.01f ? ext : FallbackHullHalfExtents;
        }

        static void Grow(Transform root, Vector3 world, ref Vector3 ext)
        {
            Vector3 d = world - root.position;
            ext.x = Mathf.Max(ext.x, Mathf.Abs(Vector3.Dot(d, root.right)));
            ext.y = Mathf.Max(ext.y, Mathf.Abs(Vector3.Dot(d, root.up)));
            ext.z = Mathf.Max(ext.z, Mathf.Abs(Vector3.Dot(d, root.forward)));
        }

        static float MaxAxis(Vector3 v) => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
    }
}
