using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ECS;
using CosmicShore.ScriptableObjects;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CARRIER of the prism's fine DETAIL LEVELS (Docs/PRISM_ANIMATION.md §4.12): the pure render
    /// entities that draw a destruction or a steal on a shared high-resolution prism mesh — the unwelded
    /// prism cut union-jack (<see cref="HighPolyPrismMesh.GetShed"/>) at the sheds' resolutions, or the destruction's
    /// cut (<see cref="HighPolyPrismMesh.GetSilverGnomon"/>, <see cref="HighPolyPrismMesh.GetSierpinski"/>) at one
    /// recursion depth — each run entirely
    /// on the GPU off one stamp (<c>PrismCollisionBurst.hlsl</c>; the class keeps the name of its first use, prism
    /// movement's collision). <see cref="PrismInteractionLod"/> decides WHICH level and mesh; this class only draws.
    ///
    /// <list type="bullet">
    /// <item><b>Destruction</b> (<see cref="TryRequestDestruction"/>): the explosion's wedges cut at a depth into a
    /// range of sizes, every fragment a <see cref="Fragment"/> (round 9, "now I just want it to look awesome"): it
    /// bursts out on its own direction — its face's radial, lifted off the face, leaning along the hit — eased in and
    /// drifting on, the smaller a little farther; tumbles about its direction and cross(velocity, N) at the
    /// explosion's own speed-capped rate (the explosion material's <c>_ExplosiveRotation</c>, copied at runtime), the
    /// smaller a little faster; and shrinks away to its centre at its own end, the smallest first — on the
    /// explosion's own flight (<see cref="PrismDebris.ResolveExplosionFlight"/>) for the explosion's own life, in the
    /// explosion's palette. A continuous gradient noise keeps neighbours alike and never in step.</item>
    /// <item><b>Shed</b> (<see cref="TryRequestShed"/>): a skin in the prism's OLD colours on both sides, a hair outside
    /// it, coming off as a ROLL (the game's one shed since round 7; <see cref="Bloom"/>, <see cref="Unravel"/> and
    /// <see cref="Flake"/> stay in the shader for the lab) while the prism underneath already wears the new colour:
    /// each face's skin rolls up AWAY from where the camera was at the steal, over the grade's length, then over the
    /// edge and round until its own prism hides it — the mass crystal's trick: nothing changes where it is seen.</item>
    /// </list>
    ///
    /// <para><b>It rides the slice's carrier unchanged</b> — <see cref="PrismRenderService.SpawnSliceDebrisBatch"/>
    /// and the slice's stamp channels, with its own mesh and material — so it added no ECS component, no
    /// prototype and no render-service code. Same shape as <see cref="PrismSlice"/>: requests queue during the
    /// frame, ONE batch spawns them, ONE batched destroy retires each expired batch, nothing writes to a live
    /// entity.</para>
    ///
    /// <para><b>It never costs an interaction its visual.</b> Every refusal — the config off or missing, the
    /// budget full, the render service down — returns false, and the caller draws the ordinary effect.
    /// Budget: <see cref="PrismInteractionLodConfigSO.MaxLiveBursts"/> entities and
    /// <see cref="PrismInteractionLodConfigSO.MaxLiveTriangles"/> triangles, destruction and sheds together, each
    /// drawing the mesh its detail level picked (one spawn batch per mesh). ZERO colliders.</para>
    /// </summary>
    public static class PrismCollisionBurst
    {
        /// <summary>The kinds, in <c>PrismCollisionBurst.hlsl</c>'s order.</summary>
        public const int Shatter = 0, Bloom = 1, Unravel = 2, Glitter = 3, Flake = 4, Fragment = 5, Roll = 6, KindCount = 7;

        /// <summary>
        /// A ROLL's life past the grade it stands for, as a share of the grade's length — <c>PRISM_BURST_ROLL_TAIL</c>
        /// in the HLSL (a test holds the two equal): the face rolls up over the grade's length, the tail carries the
        /// roll over the edge and out of sight behind its own prism.
        /// </summary>
        public const float RollTail = 0.6f;

        /// <summary>RotateFacesAlongAxis / TangentSlider's constants — <c>PRISM_BURST_PIVOT_BIAS</c> and
        /// <c>PRISM_BURST_SLIDER_GAIN</c> in the HLSL (a test holds them equal; the lab's build reads them from the graph).</summary>
        public const float PivotBias = -0.5f, SliderGain = 0.5f;

        /// <summary>The explosion material's dials, as shipped (ExplodingBlockMaterial.mat) — what the burst shader
        /// carries as defaults until the explosion is configured.</summary>
        public static readonly Vector3 ShippedExplosionSpread = new(0.18f, 0.1f, 0.1f);
        public const float ShippedExplosiveRotation = 0.0169f, ShippedExplosiveSpread = 0.05f;

        /// <summary>
        /// How far past 1 the dissolve runs at retirement — <c>PRISM_BURST_DISSOLVE_OVERSHOOT</c> in the HLSL
        /// (a test holds the two equal). A shed's life is the grade's length × (1 + this), so its fade ends
        /// exactly when the grade's would.
        /// </summary>
        public const float DissolveOvershoot = 0.06f;

        /// <summary>
        /// How far from the travelling centre any piece of any kind can be, in prism radii — the SAME
        /// formula as <c>PrismBurstEnvelope</c> in the HLSL, whose harness proves every pose stays inside
        /// it. The culling bounds are sized by it.
        /// </summary>
        public static float EnvelopeRadii(float spread) => 3.5f + 1.5f * Mathf.Max(0f, spread);

        /// <summary>How a <see cref="Fragment"/> flies — <c>PrismBurstFragmentShape</c> in the HLSL, from the config.</summary>
        public struct FragmentShape
        {
            public float Burst, Drift, SizeSpeed, SpeedGain, SpeedRef, Variety;

            public static FragmentShape Of(PrismInteractionLodConfigSO cfg) => new()
            {
                Burst = cfg.FragmentBurst, Drift = cfg.FragmentDrift, SizeSpeed = cfg.FragmentSizeSpeed,
                SpeedGain = cfg.FragmentSpeedGain, SpeedRef = cfg.FragmentSpeedRef, Variety = cfg.FragmentVariety,
            };
        }

        /// <summary>
        /// How much harder than the gentlest a hit of flight speed <paramref name="flightSpeed"/> is (≥ 1) —
        /// <c>PrismBurstFragmentBoost</c> in the HLSL: the fragments' distance and lean along the hit × this.
        /// </summary>
        public static float FragmentBoost(float flightSpeed, FragmentShape shape) =>
            1f + shape.SpeedGain * Mathf.Max(flightSpeed / Mathf.Max(shape.SpeedRef, 1e-3f) - 1f, 0f);

        /// <summary>
        /// How far, in WORLD units, a <see cref="Fragment"/> can be from the travelling centre over a life of
        /// <paramref name="life"/> seconds — the SAME sum as <c>PrismBurstFragmentReach</c> in the HLSL, whose harness
        /// proves every pose of every depth stays inside it: 3R (a centroid within R of the centre, every vertex within
        /// 2R of its centroid), plus the travel R × (burst + drift × life) × (1 + 6 sizeSpeed) × (1 + |travel noise|) ×
        /// the boost at the flight's speed (<see cref="FragmentBoost"/>) × the most a death's character stretches a
        /// distance (<see cref="FragmentVarietyReach"/>).
        /// </summary>
        public static float FragmentReach(float radius, float life, FragmentShape shape, float travelNoise, float flightSpeed) =>
            radius * (3f + (shape.Burst + shape.Drift * life) * (1f + 6f * shape.SizeSpeed) * (1f + Mathf.Abs(travelNoise))
                         * FragmentBoost(flightSpeed, shape) * FragmentVarietyReach(shape.Variety));

        /// <summary>
        /// <c>PrismBurstFragmentVarietyReach</c> in the HLSL: the most one death's character draws a distance out,
        /// (1 + v/2) for its spread × (1 + 0.6 v) for its ellipsoid.
        /// </summary>
        public static float FragmentVarietyReach(float variety)
        {
            float v = Mathf.Clamp01(variety);
            return (1f + 0.5f * v) * (1f + 0.6f * v);
        }

        /// <summary>
        /// The explosion material's own dials — its <c>_Spread</c>, <c>_ExplosiveRotation</c> and <c>_ExplosiveSpead</c>
        /// (sic, the graph's ExplosiveSpread) — so every depth of the cut moves by the explosion's tuning, single-sourced.
        /// False (and the shipped values) until the explosion pool is configured or when its material lacks them.
        /// </summary>
        public static bool TryGetExplosionTuning(out Vector3 spreadValue, out float rotation, out float spreadPerAmount)
        {
            spreadValue = ShippedExplosionSpread;
            rotation = ShippedExplosiveRotation;
            spreadPerAmount = ShippedExplosiveSpread;
            if (!PrismDebris.TryGetExplosionConfig(out var material, out _, out _) || material == null) return false;
            if (!material.HasProperty(SourceSpreadId) || !material.HasProperty(SourceRotationId) ||
                !material.HasProperty(SourceSlideId)) return false;
            spreadValue = material.GetVector(SourceSpreadId);
            rotation = material.GetFloat(SourceRotationId);
            spreadPerAmount = material.GetFloat(SourceSlideId);
            return true;
        }

        /// <summary>
        /// A steal's seed: the pose's (<see cref="SeedOf"/>) moved on by the clock, so the same prism stolen twice
        /// peels two different ways. Cosmetic only — two peers may peel one steal differently, never to a different colour.
        /// </summary>
        public static float StealSeed(Vector3 position, float now) =>
            Mathf.Repeat(SeedOf(position) + now * 0.618034f, 1f);

        /// <summary>A shed's life for a grade of <paramref name="gradeSeconds"/>: its fade ends with the grade.</summary>
        public static float ShedLife(float gradeSeconds) => Mathf.Max(0.05f, gradeSeconds) * (1f + DissolveOvershoot);

        /// <summary>The life of a shed of <paramref name="kind"/>: a roll's runs on into its tail, every other kind's
        /// ends with the grade (<see cref="ShedLife(float)"/>).</summary>
        public static float ShedLife(float gradeSeconds, int kind) => kind == Roll
            ? Mathf.Max(0.05f, gradeSeconds) * (1f + RollTail)
            : ShedLife(gradeSeconds);

        static readonly int ShapeId = Shader.PropertyToID("_BurstShape");
        static readonly int CurlId = Shader.PropertyToID("_BurstCurl");
        static readonly int PeelId = Shader.PropertyToID("_BurstPeel");
        static readonly int RollId = Shader.PropertyToID("_BurstRoll");
        static readonly int FragmentMotionId = Shader.PropertyToID("_FragmentMotion");
        static readonly int FragmentSizeId = Shader.PropertyToID("_FragmentSize");
        static readonly int FragmentFadeId = Shader.PropertyToID("_FragmentFade");
        static readonly int FragmentNoiseId = Shader.PropertyToID("_FragmentNoise");
        static readonly int FragmentVarietyId = Shader.PropertyToID("_FragmentVariety");
        static readonly int ExplosionDialsId = Shader.PropertyToID("_ExplosionDials");
        static readonly int SourceSpreadId = Shader.PropertyToID("_Spread");
        static readonly int SourceRotationId = Shader.PropertyToID("_ExplosiveRotation");
        static readonly int SourceSlideId = Shader.PropertyToID("_ExplosiveSpead");

        /// <summary>
        /// The shader programs a burst can draw with (round 11, the performance pass): the game's two KIND-ONLY ones —
        /// the destruction's (<c>_BURST_FRAGMENT</c>) and the steal's (<c>_BURST_ROLL</c>), each carrying only its own
        /// kind's code and registers — and the every-kind one the lab-only sheds (bloom, unravel, flake, shatter,
        /// glitter) still need. <see cref="VariantOf"/> picks one from a stamp's kind.
        /// </summary>
        public const int VariantAll = 0, VariantFragment = 1, VariantRoll = 2, VariantCount = 3;

        /// <summary>The <c>multi_compile_local</c> keyword of each variant (none for <see cref="VariantAll"/>).</summary>
        public static readonly string[] VariantKeywords = { null, "_BURST_FRAGMENT", "_BURST_ROLL" };

        /// <summary>The program a burst of <paramref name="kind"/> draws with.</summary>
        public static int VariantOf(int kind) => kind == Fragment ? VariantFragment : kind == Roll ? VariantRoll : VariantAll;

        static readonly Material[] s_runtimeMaterials = new Material[VariantCount];
        static PrismInteractionLodConfigSO s_runtimeMaterialFor;
        static bool s_runtimeMaterialTuned;

        struct Record
        {
            public Entity Entity;
            public float EndTime;
            public int Triangles;
        }

        /// <summary>
        /// Live entities in expiry order. Every destruction lives the explosion's one life and every shed the
        /// grade's, so each queue is in expiry order by construction — and they are TWO queues so a 0.85 s shed
        /// never waits behind a 7.5 s destruction to retire.
        /// </summary>
        sealed class LiveQueue
        {
            readonly List<Record> _records = new(128);
            int _head;

            public int Count => _records.Count - _head;
            public int Triangles { get; private set; }

            public void Add(Entity entity, float endTime, int triangles)
            {
                _records.Add(new Record { Entity = entity, EndTime = endTime, Triangles = triangles });
                Triangles += triangles;
            }

            public void Clear()
            {
                _records.Clear();
                _head = 0;
                Triangles = 0;
            }

            /// <summary>Appends every expired entity to <paramref name="into"/> and drops it from the queue.</summary>
            public void TakeExpired(float now, List<Entity> into)
            {
                int end = _head;
                while (end < _records.Count && _records[end].EndTime <= now) end++;
                for (int i = _head; i < end; i++)
                {
                    into.Add(_records[i].Entity);
                    Triangles -= _records[i].Triangles;
                }
                _head = end;
                if (_head == _records.Count) Clear();
                else if (_head >= 256 && _head * 2 >= _records.Count)
                {
                    _records.RemoveRange(0, _head);
                    _head = 0;
                }
            }
        }

        // Which live queue a request retires from: a close-up destruction, a shed, or a level-0 explosion (round 11,
        // kept out of the detail budget).
        const byte QueueDestruction = 0, QueueShed = 1, QueueExplosion = 2;

        /// <summary>The requests of one frame that draw one mesh with one program: one spawn batch each.</summary>
        sealed class Bucket
        {
            public Mesh Mesh;
            public int Variant;
            public readonly List<PrismRenderService.SliceDebrisSpawn> Spawns = new(32);
            public readonly List<byte> Queues = new(32);
        }

        // At most one bucket per (detail mesh, program): the explosion's 24 wedges, four cut depths, three roll resolutions.
        static readonly List<Bucket> s_buckets = new(8);
        static int s_pendingCount;
        static int s_pendingTriangles;
        static int s_pendingExplosions;
        static readonly List<Entity> s_scratch = new(64);
        static readonly LiveQueue s_liveDestruction = new();
        static readonly LiveQueue s_liveShed = new();
        static readonly LiveQueue s_liveExplosion = new();
        static int s_liveEpoch = -1;

        static TickHost s_host;
        static bool s_quitting;
        static float s_suspendedUntil;
        const float SuspendSeconds = 5f;

        /// <summary>Detail-level entities alive now — diagnostics and the budget.</summary>
        public static int LiveCount => s_liveDestruction.Count + s_liveShed.Count;

        /// <summary>Detail-level triangles alive now — diagnostics and the budget.</summary>
        public static int LiveTriangles => s_liveDestruction.Triangles + s_liveShed.Triangles;

        /// <summary>Level-0 explosions alive now (outside the detail budget; capped by
        /// <see cref="PrismInteractionLodConfigSO.MaxLiveExplosions"/>).</summary>
        public static int LiveExplosions => s_liveExplosion.Count;

        /// <summary>
        /// How full the budget is, counting this frame's queued requests: the fuller of entities and triangles, 0–1+.
        /// The AMOUNT half of <see cref="PrismInteractionLod"/>'s choice.
        /// </summary>
        public static float Fill(PrismInteractionLodConfigSO cfg)
        {
            if (cfg == null) return 1f;
            float entities = (LiveCount + s_pendingCount) / (float)Mathf.Max(1, cfg.MaxLiveBursts);
            float triangles = (LiveTriangles + s_pendingTriangles) / (float)Mathf.Max(1, cfg.MaxLiveTriangles);
            return Mathf.Max(entities, triangles);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_buckets.Clear();
            s_pendingCount = 0;
            s_pendingTriangles = 0;
            s_pendingExplosions = 0;
            s_scratch.Clear();
            s_liveDestruction.Clear();
            s_liveShed.Clear();
            s_liveExplosion.Clear();
            s_liveEpoch = -1;
            s_host = null;
            s_quitting = false;
            s_suspendedUntil = 0f;
            System.Array.Clear(s_runtimeMaterials, 0, VariantCount);
            s_runtimeMaterialFor = null;
            s_runtimeMaterialTuned = false;
            Application.quitting -= HandleQuitting;
            Application.quitting += HandleQuitting;
        }

        static void HandleQuitting() => s_quitting = true;

        /// <summary>
        /// Whether one more detail-level entity could be accepted right now (config sane, an entity free, service
        /// up). <see cref="PrismInteractionLod"/> asks before it spends anything on a choice.
        /// </summary>
        public static bool CanAccept(PrismInteractionLodConfigSO cfg) =>
            !s_quitting && PrismRenderService.Enabled && Time.unscaledTime >= s_suspendedUntil &&
            cfg != null && cfg.IsSane && cfg.BurstMaterial != null &&
            LiveCount + s_pendingCount < cfg.MaxLiveBursts;

        /// <summary>
        /// Whether one more LEVEL-0 explosion could be accepted right now: the service up, the config sane, and the
        /// explosions' own cap (<see cref="PrismInteractionLodConfigSO.MaxLiveExplosions"/>) not reached — the detail
        /// budget is not consulted, so a field-wide wipe never crowds the close-up levels nor is refused by them.
        /// </summary>
        public static bool CanAcceptExplosion(PrismInteractionLodConfigSO cfg) =>
            !s_quitting && PrismRenderService.Enabled && Time.unscaledTime >= s_suspendedUntil &&
            cfg != null && cfg.IsSane && cfg.BurstMaterial != null &&
            s_liveExplosion.Count + s_pendingExplosions < cfg.MaxLiveExplosions;

        /// <summary>Whether <paramref name="triangles"/> more fit the triangle budget right now.</summary>
        public static bool FitsTriangles(PrismInteractionLodConfigSO cfg, int triangles) =>
            cfg != null && LiveTriangles + s_pendingTriangles + triangles <= cfg.MaxLiveTriangles;

        /// <summary>Triangles in <paramref name="mesh"/> (the budget's unit).</summary>
        public static int TrianglesOf(Mesh mesh) => mesh != null ? (int)(mesh.GetIndexCount(0) / 3) : 0;

        /// <summary>
        /// Queues a DESTRUCTION of <paramref name="kind"/> on <paramref name="mesh"/> (the game draws its cut with
        /// <see cref="Fragment"/>) for the
        /// prism that was at (<paramref name="position"/>, <paramref name="rotation"/>, <paramref name="scale"/>).
        /// <paramref name="impactVelocity"/>, <paramref name="speedLimitOverride"/> and <paramref name="tier"/>
        /// are exactly what the explosion would have been handed, so the cloud leaves exactly like it.
        /// <paramref name="level0"/>: this is the EXPLOSION itself (the cut's depth 0, round 11), accepted by
        /// <see cref="CanAcceptExplosion"/> and retired from its own queue, outside the detail budget. Returns
        /// false — the caller explodes the prism with the explosion's graph instead — on any refusal.
        /// </summary>
        public static bool TryRequestDestruction(Vector3 position, Quaternion rotation, Vector3 scale,
            Color bright, Color dark, Vector3 impactVelocity, float speedLimitOverride, PrismKind tier, int kind, Mesh mesh,
            bool level0 = false)
        {
            // A rotation that drifted off unit length (1.00001 after many prism-to-prism multiplications) makes
            // Matrix4x4.TRS log "Quaternion To Matrix conversion failed" (Bug Ledger E-16d65ffecb and three more).
            // Normalize once here; the bounds below read the same rotation.
            rotation = Quaternion.Normalize(rotation);

            var cfg = PrismInteractionLodConfigSO.Instance;
            bool accepted = level0 ? CanAcceptExplosion(cfg) : CanAccept(cfg) && FitsTriangles(cfg, TrianglesOf(mesh));
            if (!accepted || mesh == null || !PrismDebris.TryGetExplosionLayer(out _)) return false;

            Vector3 flight = PrismDebris.ResolveExplosionFlight(impactVelocity, speedLimitOverride, tier, out float speed);
            float life = PrismExplosion.DefaultDuration;
            float3 centre, extents;
            if (kind == Fragment)
            {
                float reach = FragmentReach(0.5f * Abs(scale).magnitude, life, FragmentShape.Of(cfg), cfg.FragmentTravelNoise,
                    flight.magnitude);
                BuildBoundsOfReach(rotation, scale, flight * life, reach, out centre, out extents);
            }
            else
            {
                BuildBounds(rotation, scale, flight * life, cfg.Spread, out centre, out extents);
            }

            Queue(new PrismRenderService.SliceDebrisSpawn
            {
                LocalToWorld = Matrix4x4.TRS(position, rotation, scale),
                BrightColor = PrismRenderService.ToFloat4(bright),
                DarkColor = PrismRenderService.ToFloat4(dark),
                Timing = new float4(0f, life, SeedOf(position), kind),
                Plane = float4.zero,                    // fade curve 0: linear, the explosion's (a fragment shrinks instead)
                Centre = new float4(0f, 0f, 0f, 1f),
                Pivot = float4.zero,                    // inside: the domain's own bright
                Axis = float4.zero,
                Drift = new float4(flight.x, flight.y, flight.z, speed),
                Bounds = new AABB { Center = centre, Extents = extents },
            }, level0 ? QueueExplosion : QueueDestruction, mesh, VariantOf(kind));
            return true;
        }

        /// <summary>
        /// Queues a SHED of <paramref name="kind"/> (<see cref="Roll"/>, <see cref="Bloom"/>, <see cref="Unravel"/> or
        /// <see cref="Flake"/>): a skin in the prism's OLD colours (<paramref name="oldBright"/>,
        /// <paramref name="oldDark"/>) on BOTH sides, a hair outside the prism at
        /// (<paramref name="position"/>, <paramref name="rotation"/>, <paramref name="scale"/>), over a grade of
        /// <paramref name="gradeSeconds"/> (<see cref="ShedLife(float, int)"/>). A roll peels away from
        /// <paramref name="cameraPosition"/> on a mesh of <paramref name="subdivision"/> cells a face edge. The caller
        /// then gives the prism its new colour AT ONCE. Returns false — the caller grades as usual — on any refusal.
        /// </summary>
        public static bool TryRequestShed(Vector3 position, Quaternion rotation, Vector3 scale,
            Color oldBright, Color oldDark, float gradeSeconds, int kind, Mesh mesh,
            Vector3 cameraPosition, int subdivision)
        {
            rotation = Quaternion.Normalize(rotation);   // Bug Ledger E-16d65ffecb: see TryRequestDestruction

            var cfg = PrismInteractionLodConfigSO.Instance;
            if (!CanAccept(cfg) || !FitsTriangles(cfg, TrianglesOf(mesh)) || !PrismDebris.TryGetExplosionLayer(out _)) return false;

            Vector3 skin = SkinScale(scale, cfg.ShedInflate);
            BuildBounds(rotation, skin, Vector3.zero, cfg.Spread, out var centre, out var extents);

            Queue(new PrismRenderService.SliceDebrisSpawn
            {
                LocalToWorld = Matrix4x4.TRS(position, rotation, skin),
                BrightColor = PrismRenderService.ToFloat4(oldBright),
                DarkColor = PrismRenderService.ToFloat4(oldDark),
                Timing = new float4(0f, ShedLife(gradeSeconds, kind), StealSeed(position, PrismClock.Now), kind),
                Plane = new float4(1f, 0f, 0f, 0f),     // fade curve 1: the grade's smoothstep
                Centre = new float4(0f, 0f, 0f, 1f),
                // The skin is the OLD domain through and through (Garrett, 2026-10-23: "curl the original domain and
                // reveal the stolen domain underneath"): its underside wears the old bright (w 0), so a roll - which
                // winds top side in and shows that underside - reads as the old colour curling away, and the only
                // new colour on screen is the prism it uncovers.
                Pivot = float4.zero,
                // A roll peels AWAY from where the camera was at the steal, on cells this fine.
                Axis = new float4(cameraPosition.x, cameraPosition.y, cameraPosition.z, subdivision),
                Drift = float4.zero,                    // the prism stays where it is
                Bounds = new AABB { Center = centre, Extents = extents },
            }, QueueShed, mesh, VariantOf(kind));
            return true;
        }

        /// <summary>
        /// The skin's scale: the prism's, grown by <paramref name="inflate"/> prism radii on every side so the
        /// skin and the prism under it never share a depth.
        /// </summary>
        public static Vector3 SkinScale(Vector3 scale, float inflate)
        {
            float grow = 2f * Mathf.Max(0f, inflate) * 0.5f * scale.magnitude;
            return new Vector3(scale.x + Mathf.Sign(scale.x) * grow, scale.y + Mathf.Sign(scale.y) * grow,
                scale.z + Mathf.Sign(scale.z) * grow);
        }

        /// <summary>One random per prism, hashed from the pose: deterministic on every peer, and never two alike.</summary>
        public static float SeedOf(Vector3 position) =>
            math.frac(math.dot(new float3(position.x, position.y, position.z),
                new float3(0.1031f, 0.11369f, 0.13787f)) * 43.7585f);

        static void Queue(in PrismRenderService.SliceDebrisSpawn spawn, byte queue, Mesh mesh, int variant)
        {
            Bucket bucket = null;
            for (int i = 0; i < s_buckets.Count; i++)
                if (ReferenceEquals(s_buckets[i].Mesh, mesh) && s_buckets[i].Variant == variant) { bucket = s_buckets[i]; break; }
            if (bucket == null)
            {
                bucket = new Bucket { Mesh = mesh, Variant = variant };
                s_buckets.Add(bucket);
            }
            bucket.Spawns.Add(spawn);
            bucket.Queues.Add(queue);
            if (queue == QueueExplosion)
            {
                s_pendingExplosions++;
            }
            else
            {
                s_pendingCount++;
                s_pendingTriangles += TrianglesOf(mesh);
            }
            EnsureHost();
        }

        /// <summary>
        /// A weighted pick of a shed kind from <paramref name="roll01"/> in [0, 1): bloom, unravel, flake, roll in that
        /// order. Zero-weight kinds are never picked; all weights zero picks the bloom.
        /// </summary>
        public static int PickShed(float roll01, float bloom, float unravel, float flake, float roll = 0f)
        {
            bloom = Mathf.Max(0f, bloom);
            unravel = Mathf.Max(0f, unravel);
            flake = Mathf.Max(0f, flake);
            roll = Mathf.Max(0f, roll);
            float total = bloom + unravel + flake + roll;
            if (total <= 0f) return Bloom;
            float r = Mathf.Clamp01(roll01) * total;
            if (bloom > 0f && (r -= bloom) < 0f) return Bloom;
            if (unravel > 0f && (r -= unravel) < 0f) return Unravel;
            if (flake > 0f && (r -= flake) < 0f) return Flake;
            if (roll > 0f) return Roll;
            // roll01 == 1 lands past the end: the last kind with weight.
            return flake > 0f ? Flake : unravel > 0f ? Unravel : Bloom;
        }

        /// <summary>
        /// The OBJECT-space culling box (the entity matrix never moves): every pose stays within
        /// <see cref="EnvelopeRadii"/> prism radii of the centre in WORLD space — an ellipsoid of semi-axes
        /// R / |scale_i| in object space — swept along the whole flight.
        /// </summary>
        public static void BuildBounds(Quaternion rotation, Vector3 scale, Vector3 worldFlight, float spread,
            out float3 centre, out float3 extents) =>
            BuildBoundsOfReach(rotation, scale, worldFlight, EnvelopeRadii(spread) * 0.5f * Abs(scale).magnitude,
                out centre, out extents);

        /// <summary>
        /// <see cref="BuildBounds"/> for a WORLD reach (<see cref="FragmentReach"/>): every pose within
        /// <paramref name="reach"/> of the centre, swept along the whole flight.
        /// </summary>
        public static void BuildBoundsOfReach(Quaternion rotation, Vector3 scale, Vector3 worldFlight, float reach,
            out float3 centre, out float3 extents)
        {
            var s = Abs(scale);
            Vector3 local = Quaternion.Inverse(rotation) * worldFlight;
            var flightOS = new float3(local.x / s.x, local.y / s.y, local.z / s.z);
            centre = flightOS * 0.5f;
            extents = new float3(reach / s.x, reach / s.y, reach / s.z) + math.abs(flightOS) * 0.5f;
        }

        static Vector3 Abs(Vector3 scale) =>
            new(Mathf.Max(1e-4f, Mathf.Abs(scale.x)), Mathf.Max(1e-4f, Mathf.Abs(scale.y)),
                Mathf.Max(1e-4f, Mathf.Abs(scale.z)));

        static Material ResolveRuntimeMaterial(PrismInteractionLodConfigSO cfg, int variant)
        {
            if (s_runtimeMaterials[VariantAll] != null && s_runtimeMaterialFor == cfg)
            {
                // The explosion pool can configure after the first burst: copy its dials the first drain they exist.
                if (!s_runtimeMaterialTuned)
                {
                    bool real = true;
                    for (int v = 0; v < VariantCount; v++) real &= ApplyExplosionTuning(s_runtimeMaterials[v]);
                    s_runtimeMaterialTuned = real;
                }
                return s_runtimeMaterials[variant];
            }
            DropRuntimeMaterial();
            if (cfg == null || cfg.BurstMaterial == null) return null;

            bool tuned = true;
            for (int v = 0; v < VariantCount; v++)
            {
                var m = new Material(cfg.BurstMaterial)
                {
                    name = cfg.BurstMaterial.name + (VariantKeywords[v] != null ? " " + VariantKeywords[v] : "") + " (runtime)",
                    hideFlags = HideFlags.DontSave,
                };
                if (VariantKeywords[v] != null) m.EnableKeyword(VariantKeywords[v]);
                m.SetVector(ShapeId,
                    new Vector4(cfg.Spread, cfg.OpenSeconds, cfg.SpinPerSpeed, cfg.DissolveGrain));
                m.SetVector(CurlId,
                    new Vector4(cfg.BloomTurns, cfg.UnravelTurns, cfg.RibbonsPerFace, cfg.ShardsPerFaceEdge));
                m.SetVector(PeelId, new Vector4(cfg.BloomRings, cfg.BloomSegments, cfg.PeelNoise, 0f));
                m.SetVector(RollId, new Vector4(cfg.RollRadius, cfg.RollGap, 0f, 0f));
                m.SetVector(FragmentMotionId,
                    new Vector4(cfg.FragmentBurst, cfg.FragmentDrift, cfg.FragmentLift, cfg.FragmentAim));
                m.SetVector(FragmentSizeId,
                    new Vector4(cfg.FragmentSizeSpeed, cfg.FragmentSizeSpin, cfg.FragmentSpinCap, cfg.FragmentLevelRef));
                m.SetVector(FragmentFadeId,
                    new Vector4(cfg.FragmentEndSmall, cfg.FragmentEndBig, cfg.FragmentShrink, cfg.FragmentSpeedGain));
                m.SetVector(FragmentNoiseId,
                    new Vector4(cfg.FragmentSpinNoise, cfg.FragmentNoiseFrequency, cfg.FragmentTravelNoise, cfg.FragmentSpeedRef));
                m.SetFloat(FragmentVarietyId, cfg.FragmentVariety);
                tuned &= ApplyExplosionTuning(m);
                s_runtimeMaterials[v] = m;
            }
            s_runtimeMaterialTuned = tuned;
            s_runtimeMaterialFor = cfg;
            return s_runtimeMaterials[variant];
        }

        /// <summary>Copies the explosion material's dials onto the burst material (<see cref="TryGetExplosionTuning"/>);
        /// true once they came from the real explosion, false while the shipped defaults stand in.</summary>
        static bool ApplyExplosionTuning(Material material)
        {
            bool real = TryGetExplosionTuning(out _, out float rotation, out float spreadPerAmount);
            material.SetVector(ExplosionDialsId, new Vector4(rotation, spreadPerAmount, 0f, 0f));
            return real;
        }

        /// <summary>Editor tooling: forget the runtime clone so the next burst re-reads the config.</summary>
        public static void InvalidateConfig()
        {
            PrismInteractionLodConfigSO.InvalidateInstance();
            DropRuntimeMaterial();
        }

        static void DropRuntimeMaterial()
        {
            for (int v = 0; v < VariantCount; v++)
            {
                var m = s_runtimeMaterials[v];
                if (m != null)
                {
                    if (Application.isPlaying) Object.Destroy(m);
                    else Object.DestroyImmediate(m);
                }
                s_runtimeMaterials[v] = null;
            }
            s_runtimeMaterialFor = null;
            s_runtimeMaterialTuned = false;
        }

        static void EnsureHost()
        {
            if (s_host != null) return;
            if (s_quitting || !Application.isPlaying) return;
            var go = new GameObject("[PrismCollisionBurst]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            s_host = go.AddComponent<TickHost>();
        }

        // Beside the slice (29001), before the render service's visibility flush (30000): a prism hidden
        // by SetupDestruction this frame has its destruction drawing the SAME frame, and a stolen prism's
        // skin covers it the same frame it takes its new colour.
        [DefaultExecutionOrder(29002)]
        sealed class TickHost : MonoBehaviour
        {
            void LateUpdate()
            {
                Drain();
                Sweep();
            }
        }

        static readonly Unity.Profiling.ProfilerMarker s_drainMarker = new("PrismCollisionBurst.Drain");
        static readonly Unity.Profiling.ProfilerMarker s_sweepMarker = new("PrismCollisionBurst.Sweep");

        static void Drain()
        {
            if (s_pendingCount == 0 && s_pendingExplosions == 0) return;

            using (s_drainMarker.Auto())
            {
                var cfg = PrismInteractionLodConfigSO.Instance;
                bool layerOk = PrismDebris.TryGetExplosionLayer(out int layer);
                int failed = 0;

                for (int b = 0; b < s_buckets.Count; b++)
                {
                    var bucket = s_buckets[b];
                    if (bucket.Spawns.Count == 0) continue;

                    s_scratch.Clear();
                    var material = ResolveRuntimeMaterial(cfg, bucket.Variant);
                    bool spawned = material != null && layerOk && bucket.Mesh != null
                        && PrismRenderService.SpawnSliceDebrisBatch(bucket.Mesh, material, layer,
                            bucket.Spawns, PrismClock.Now, s_scratch);

                    if (spawned)
                    {
                        int epoch = PrismRenderService.CurrentEpoch;
                        if (s_liveEpoch != epoch)
                        {
                            s_liveDestruction.Clear();
                            s_liveShed.Clear();
                            s_liveExplosion.Clear();
                            s_liveEpoch = epoch;
                        }

                        float now = PrismClock.Now;
                        int triangles = TrianglesOf(bucket.Mesh);
                        for (int i = 0; i < s_scratch.Count; i++)
                        {
                            byte q = bucket.Queues[i];
                            var live = q == QueueShed ? s_liveShed : q == QueueExplosion ? s_liveExplosion : s_liveDestruction;
                            live.Add(s_scratch[i], now + bucket.Spawns[i].Timing.y, triangles);
                        }
                    }
                    else
                    {
                        failed += bucket.Spawns.Count;
                    }

                    bucket.Spawns.Clear();
                    bucket.Queues.Clear();
                }

                if (failed > 0)
                {
                    // Accepted while the service looked usable, lost before the drain. Hold new requests so
                    // interactions go back to their ordinary effects, time-based so a rebuilt world re-enables it.
                    s_suspendedUntil = Time.unscaledTime + SuspendSeconds;
                    CSDebug.LogWarning($"[PrismCollisionBurst] Batch spawn failed for {failed} queued detail-level " +
                                     $"entities (render service: {PrismRenderService.StatusLine()}). These destructions and " +
                                     $"steals have no visual; interactions fall back to their ordinary effects for {SuspendSeconds:F0}s.");
                }

                s_pendingCount = 0;
                s_pendingTriangles = 0;
                s_pendingExplosions = 0;
                s_scratch.Clear();
            }
        }

        static void Sweep()
        {
            if (LiveCount == 0 && s_liveExplosion.Count == 0) return;

            using (s_sweepMarker.Auto())
            {
                if (s_liveEpoch != PrismRenderService.CurrentEpoch)
                {
                    s_liveDestruction.Clear();
                    s_liveShed.Clear();
                    s_liveExplosion.Clear();
                    return;
                }

                float now = PrismClock.Now;
                s_scratch.Clear();
                s_liveShed.TakeExpired(now, s_scratch);
                s_liveDestruction.TakeExpired(now, s_scratch);
                s_liveExplosion.TakeExpired(now, s_scratch);
                if (s_scratch.Count > 0)
                    PrismRenderService.DestroyDebrisBatch(s_scratch, s_liveEpoch);
                s_scratch.Clear();
            }
        }
    }
}
