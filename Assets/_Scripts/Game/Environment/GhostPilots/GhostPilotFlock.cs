using CosmicShore.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Game
{
    /// <summary>
    /// "Ghost pilots": a render-only, footage-tuned ambient wander flock for backgrounds such as the menu cell
    /// (Docs/GENOME_GHOST_PILOTS.md). Pilots wander under a turn cap inside a soft sphere and drop faceted trail
    /// prisms; the motion is the genome lab's, tuned until rendered clips matched real gameplay footage.
    ///
    /// <para><b>Not fauna.</b> No colliders, no NetworkObject, no heart, no mass, no PrismSpatialIndex entries; nothing
    /// eats it and it eats nothing (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md). It owns no GameObjects: bodies and
    /// trail prisms are drawn with <c>Graphics.RenderMeshInstanced</c>.</para>
    ///
    /// <para><b>Continuity.</b> It blooms in on enable (scale from zero, staggered per pilot) and <see cref="FadeOut"/>
    /// shrinks everything to nothing before switching itself off; trail prisms grow in and taper to nothing at the end
    /// of their life. Disabling the component directly skips the fade, so callers use <see cref="FadeOut"/>.</para>
    ///
    /// <para><b>Cost.</b> One Burst <see cref="IJobParallelFor"/> per matrix tick steps every pilot (neighbour-free,
    /// O(n)) at the lab's fixed 60 Hz and writes the body and trail matrices; the main thread only submits the cached
    /// draws (one per team per part, chunked at 1023). On WebGL (no threads, no Burst) the same job runs inline with
    /// <c>Run</c>.</para>
    ///
    /// <para><b>Space.</b> Pilots fly in this transform's local space (the bound is centred on it); trail prisms are
    /// fixed in world space where they were laid, so moving the flock leaves its trails behind, as the game's own
    /// trails do. A tick only rescales a laid prism's stored frame (no TRS, no matrix multiply per prism).</para>
    ///
    /// <para><b>Lighting.</b> Lit and faceted: the meshes have per-face normals and the default material is
    /// <c>Resources/OrcaCrewLit.mat</c> (URP/Lit, instancing on), cloned with emission on. Colours go in through one
    /// property block per team, never into a shared material.</para>
    /// </summary>
    public class GhostPilotFlock : MonoBehaviour
    {
        const string DefaultLitResource = "OrcaCrewLit";
        const int MaxInstancesPerDraw = 1023;

        [Header("Profile")]
        [Tooltip("Motion and look. Presets: GhostPilots_ChaseNoHud (tight, busy, 146 u bound) and " +
                 "GhostPilots_OrbitOverview (wide, slow, 566 u bound), both decoded from the lab's tuned genomes.")]
        [SerializeField] GhostPilotFlockProfileSO profile;

        [Header("Rendering")]
        [Tooltip("Lit, instanced material for the pilot bodies. Empty = a clone of Resources/OrcaCrewLit.mat (URP/Lit).")]
        [SerializeField] Material bodyMaterial;

        [Tooltip("Lit, instanced material for the trail prisms. Empty = a clone of Resources/OrcaCrewLit.mat (URP/Lit).")]
        [SerializeField] Material trailMaterial;

        [Tooltip("Body mesh. Empty = the lab's faceted dart, built in code.")]
        [SerializeField] Mesh bodyMesh;

        [Tooltip("Trail prism mesh. Empty = the lab's faceted pyramid, built in code.")]
        [SerializeField] Mesh trailMesh;

        [Tooltip("Multiplies body and trail sizes (never the motion). The lab's sizes read at chase-camera distance; " +
                 "seen from the menu camera's ~686 u orbit, try 4-8 so pilots are more than a pixel.")]
        [Min(0.01f)]
        [SerializeField] float visualScale = 1f;

        [Tooltip("Multiplies the profile's emission. 0 = lit only, no glow.")]
        [Min(0f)]
        [SerializeField] float emissionScale = 0.5f;

        [Tooltip("Rendering layer mask for the instanced draws.")]
        [SerializeField] uint renderingLayerMask = 1;

        [Header("Continuity")]
        [Tooltip("Bloom in automatically when enabled.")]
        [SerializeField] bool bloomOnEnable = true;

        [Tooltip("Seconds to bloom in from nothing.")]
        [Min(0.01f)]
        [SerializeField] float bloomSeconds = 2.5f;

        [Tooltip("Seconds FadeOut takes to shrink everything to nothing.")]
        [Min(0.01f)]
        [SerializeField] float fadeSeconds = 2f;

        [Header("Cost")]
        [Tooltip("Matrix and simulation tick rate in Hz. Pilots are stepped at the lab's fixed 60 Hz inside each tick, " +
                 "so this changes only how often the draw catches up, not the motion. 0 = every frame. The cached " +
                 "matrices are submitted every frame either way (same pattern as CapsuleMembrane).")]
        [Range(0, 60)]
        [SerializeField] int matrixUpdateHz = 30;

        [Tooltip("Most fixed 60 Hz steps one tick may run; time beyond it is dropped (no catch-up spiral after a hitch).")]
        [Range(1, 32)]
        [SerializeField] int maxStepsPerTick = 8;

        [Tooltip("Run the step job on worker threads. Ignored on WebGL, which has none (the job runs inline).")]
        [SerializeField] bool simulateOffMainThread = true;

        [Tooltip("Keep moving while time is paused (every Menu_Main screen except HOME pauses time).")]
        [SerializeField] bool useUnscaledTime = true;

        static readonly ProfilerMarker s_step = new("GhostPilotFlock.Step");
        static readonly ProfilerMarker s_submit = new("GhostPilotFlock.RenderMeshInstanced");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // ---- native state (allocated in OnEnable, disposed in OnDisable) ----
        NativeArray<float3> _pos, _vel, _phaseA, _phaseB;
        NativeArray<float> _trailPhase, _trailBirth;
        NativeArray<int> _trailHead;
        NativeArray<float3> _trailPos;
        NativeArray<float3x3> _trailBasis;
        NativeArray<Matrix4x4> _bodyMatrices, _trailMatrices;
        JobHandle _handle;
        bool _pending, _allocated;

        int _count, _trailCap, _teams;
        int[] _teamStart;
        GhostPilotMotion _motion;
        double _simTime;
        float _accum, _nextTickAt, _envelope, _envelopeTarget;
        bool _disableWhenFaded, _inline;

        Mesh _bodyMesh, _trailMesh, _ownedBodyMesh, _ownedTrailMesh;
        Material _bodyMat, _trailMat;
        MaterialPropertyBlock[] _bodyBlocks, _trailBlocks;
        bool _warnedNoMaterial;

        /// <summary>The profile driving this flock.</summary>
        public GhostPilotFlockProfileSO Profile => profile;

        /// <summary>0 = nothing drawn, 1 = fully bloomed.</summary>
        public float Envelope => _envelope;

        /// <summary>Bloom in from wherever the envelope is now.</summary>
        public void Bloom()
        {
            _envelopeTarget = 1f;
            _disableWhenFaded = false;
        }

        /// <summary>Shrink everything to nothing, then (by default) switch this component off.</summary>
        public void FadeOut(bool disableWhenDone = true)
        {
            _envelopeTarget = 0f;
            _disableWhenFaded = disableWhenDone;
        }

        void OnEnable()
        {
            if (profile == null)
            {
                CSDebug.LogWarning($"{nameof(GhostPilotFlock)} '{name}': no profile assigned - nothing to fly.", this);
                enabled = false;
                return;
            }
            Allocate();
            EnsureRenderResources();
            _envelope = 0f;
            _envelopeTarget = bloomOnEnable ? 1f : 0f;
            _disableWhenFaded = false;
            _accum = 0f;
            _nextTickAt = 0f;
            _inline = !simulateOffMainThread || Application.platform == RuntimePlatform.WebGLPlayer;

            // Settle the flock into its bound before anything is drawn: motion only, no trails, envelope 0.
            int warm = Mathf.Min(Mathf.RoundToInt(profile.WarmupSeconds / GhostPilotMotion.StepSeconds), 60 * 60);
            if (warm > 0)
            {
                using (s_step.Auto())
                    BuildJob(warm, dropTrails: false).Run(_count);
                _simTime += warm * GhostPilotMotion.StepSeconds;
            }
        }

        void OnDisable()
        {
            _handle.Complete();
            _pending = false;
            Dispose();
        }

        void OnDestroy()
        {
            DestroyOwned(ref _ownedBodyMesh);
            DestroyOwned(ref _ownedTrailMesh);
            DestroyOwned(ref _bodyMat);
            DestroyOwned(ref _trailMat);
        }

        void Update()
        {
            if (!_allocated) return;
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float now = useUnscaledTime ? Time.unscaledTime : Time.time;

            float rate = _envelopeTarget > _envelope ? 1f / bloomSeconds : 1f / fadeSeconds;
            _envelope = Mathf.MoveTowards(_envelope, _envelopeTarget, rate * dt);
            if (_envelope <= 0f && _envelopeTarget <= 0f && _disableWhenFaded)
            {
                enabled = false;
                return;
            }

            _accum += dt;
            if (matrixUpdateHz > 0 && now < _nextTickAt) return;
            _nextTickAt = matrixUpdateHz > 0 ? now + 1f / matrixUpdateHz : 0f;

            int steps = (int)(_accum / GhostPilotMotion.StepSeconds);
            if (steps > maxStepsPerTick)
            {
                steps = maxStepsPerTick;
                _accum = 0f;
            }
            else _accum -= steps * GhostPilotMotion.StepSeconds;

            using (s_step.Auto())
            {
                var job = BuildJob(steps, dropTrails: true);
                if (_inline) job.Run(_count);
                else
                {
                    _handle = job.Schedule(_count, 4);
                    _pending = true;
                    JobHandle.ScheduleBatchedJobs();
                }
            }
            _simTime += steps * GhostPilotMotion.StepSeconds;
        }

        void LateUpdate()
        {
            if (!_allocated) return;
            if (_pending)
            {
                _handle.Complete();
                _pending = false;
            }
            if (_envelope <= 0f) return;
            if (_bodyMat == null || _trailMat == null || _bodyMesh == null || _trailMesh == null)
            {
                if (!_warnedNoMaterial)
                {
                    _warnedNoMaterial = true;
                    CSDebug.LogWarning($"{nameof(GhostPilotFlock)} '{name}': no lit material or mesh - not drawing. " +
                                       "Assign one, or check Resources/OrcaCrewLit.mat ships.", this);
                }
                return;
            }

            float extent = (profile.BoundRadius + profile.SpawnRadius) * 2f * Mathf.Max(1f, transform.lossyScale.x);
            var bounds = new Bounds(transform.position, Vector3.one * extent);
            using (s_submit.Auto())
            {
                for (int k = 0; k < _teams; k++)
                {
                    int a = _teamStart[k], b = _teamStart[k + 1];
                    if (b <= a) continue;
                    Draw(_bodyMat, _bodyMesh, _bodyBlocks[k], bounds, _bodyMatrices, a, b - a);
                    Draw(_trailMat, _trailMesh, _trailBlocks[k], bounds, _trailMatrices, a * _trailCap, (b - a) * _trailCap);
                }
            }
        }

        void Draw(Material mat, Mesh mesh, MaterialPropertyBlock block, Bounds bounds, NativeArray<Matrix4x4> matrices,
                  int start, int count)
        {
            var rp = new RenderParams(mat)
            {
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                renderingLayerMask = renderingLayerMask,
                layer = gameObject.layer,
                matProps = block,
            };
            for (int s = 0; s < count; s += MaxInstancesPerDraw)
                Graphics.RenderMeshInstanced(rp, mesh, 0, matrices, Mathf.Min(MaxInstancesPerDraw, count - s), start + s);
        }

        GhostPilotStepJob BuildJob(int steps, bool dropTrails) => new()
        {
            Motion = _motion,
            Pos = _pos,
            Vel = _vel,
            PhaseA = _phaseA,
            PhaseB = _phaseB,
            TrailPhase = _trailPhase,
            TrailHead = _trailHead,
            TrailPos = _trailPos,
            TrailBasis = _trailBasis,
            TrailBirth = _trailBirth,
            BodyMatrices = _bodyMatrices,
            TrailMatrices = _trailMatrices,
            T0 = (float)_simTime,
            Dt = GhostPilotMotion.StepSeconds,
            Steps = steps,
            DropTrails = dropTrails,
            TrailCap = _trailCap,
            TrailInterval = profile.TrailInterval,
            TrailLife = profile.TrailLifetime,
            TrailTaper = profile.TrailTaperPower,
            TrailSize = (float3)profile.TrailSize * visualScale,
            BodySize = (float3)profile.BodySize * visualScale,
            Envelope = _envelope,
            LocalToWorld = transform.localToWorldMatrix,
        };

        void Allocate()
        {
            Dispose();
            _count = profile.Count;
            _trailCap = profile.TrailCapacity;
            _teams = Mathf.Clamp(profile.Teams, 1, 4);
            _motion = GhostPilotMotion.From(profile);
            _simTime = 0;

            _pos = new NativeArray<float3>(_count, Allocator.Persistent);
            _vel = new NativeArray<float3>(_count, Allocator.Persistent);
            _phaseA = new NativeArray<float3>(_count, Allocator.Persistent);
            _phaseB = new NativeArray<float3>(_count, Allocator.Persistent);
            _trailPhase = new NativeArray<float>(_count, Allocator.Persistent);
            _trailHead = new NativeArray<int>(_count, Allocator.Persistent);
            int slots = _count * _trailCap;
            _trailPos = new NativeArray<float3>(slots, Allocator.Persistent);
            _trailBasis = new NativeArray<float3x3>(slots, Allocator.Persistent);
            _trailBirth = new NativeArray<float>(slots, Allocator.Persistent);
            _bodyMatrices = new NativeArray<Matrix4x4>(_count, Allocator.Persistent);
            _trailMatrices = new NativeArray<Matrix4x4>(slots, Allocator.Persistent);
            _allocated = true;

            GhostPilotMotion.Seed((uint)profile.Seed, profile.SpawnShape, profile.SpawnRadius, profile.CruiseSpeed,
                                  _pos, _vel, _phaseA, _phaseB);
            var rng = Unity.Mathematics.Random.CreateFromIndex((uint)profile.Seed ^ 0x9E3779B9u);
            for (int i = 0; i < _count; i++) _trailPhase[i] = rng.NextFloat();   // the lab's per-pilot drop phase
            for (int j = 0; j < slots; j++) _trailBirth[j] = float.NegativeInfinity;

            // Teams are contiguous blocks so each team is one instanced range per part.
            _teamStart = new int[_teams + 1];
            for (int k = 0; k <= _teams; k++) _teamStart[k] = k * _count / _teams;
        }

        void Dispose()
        {
            if (!_allocated) return;
            _allocated = false;
            _pos.Dispose(); _vel.Dispose(); _phaseA.Dispose(); _phaseB.Dispose();
            _trailPhase.Dispose(); _trailHead.Dispose();
            _trailPos.Dispose(); _trailBasis.Dispose(); _trailBirth.Dispose();
            _bodyMatrices.Dispose(); _trailMatrices.Dispose();
        }

        void EnsureRenderResources()
        {
            if (bodyMesh) _bodyMesh = bodyMesh;
            else _bodyMesh = _ownedBodyMesh ? _ownedBodyMesh : _ownedBodyMesh = GhostPilotMeshes.BuildDart();
            if (trailMesh) _trailMesh = trailMesh;
            else _trailMesh = _ownedTrailMesh ? _ownedTrailMesh : _ownedTrailMesh = GhostPilotMeshes.BuildPyramid();

            if (!_bodyMat) _bodyMat = CloneLit(bodyMaterial, "GhostPilotBody");
            if (!_trailMat) _trailMat = CloneLit(trailMaterial, "GhostPilotTrail");

            _bodyBlocks = new MaterialPropertyBlock[_teams];
            _trailBlocks = new MaterialPropertyBlock[_teams];
            for (int k = 0; k < _teams; k++)
            {
                _bodyBlocks[k] = ColourBlock(profile.BodyColor(k), profile.BodyEmission);
                _trailBlocks[k] = ColourBlock(profile.TrailColor(k), profile.TrailEmission);
            }
        }

        /// <summary>A lit albedo (the HDR colour normalised so the hue survives) and an emission (the colour x emit).</summary>
        MaterialPropertyBlock ColourBlock(Color hdr, float emit)
        {
            float peak = Mathf.Max(1f, Mathf.Max(hdr.r, Mathf.Max(hdr.g, hdr.b)));
            var albedo = new Color(hdr.r / peak, hdr.g / peak, hdr.b / peak, 1f);
            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, albedo);
            block.SetColor(ColorId, albedo);
            block.SetColor(EmissionColorId, hdr * (emit * emissionScale));
            return block;
        }

        /// <summary>A private copy (so the emission keyword never touches a shared asset) with instancing on.</summary>
        static Material CloneLit(Material assigned, string label)
        {
            Material template = assigned ? assigned : Resources.Load<Material>(DefaultLitResource);
            if (!template || !template.shader || !template.shader.isSupported) return null;
            var m = new Material(template) { name = label, enableInstancing = true };
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        static void DestroyOwned<T>(ref T obj) where T : Object
        {
            if (obj) Destroy(obj);
            obj = null;
        }

        void OnDrawGizmosSelected()
        {
            if (!profile) return;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.6f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(Vector3.zero, profile.BoundRadius);
        }
    }

    /// <summary>
    /// Steps every ghost pilot <see cref="Steps"/> fixed steps (each pilot independently: no neighbours), drops its
    /// trail prisms into its own ring buffer, and writes its body matrix and its trail prisms' matrices.
    /// Each index writes only its own pilot and its own ring range.
    /// </summary>
    [BurstCompile]
    public struct GhostPilotStepJob : IJobParallelFor
    {
        public GhostPilotMotion Motion;

        public NativeArray<float3> Pos;
        public NativeArray<float3> Vel;
        [ReadOnly] public NativeArray<float3> PhaseA;
        [ReadOnly] public NativeArray<float3> PhaseB;
        public NativeArray<float> TrailPhase;
        public NativeArray<int> TrailHead;

        /// <summary>World-space position of each laid prism (trails stay where they were laid, like the game's).</summary>
        [NativeDisableParallelForRestriction] public NativeArray<float3> TrailPos;
        /// <summary>World-space frame of each laid prism at unit scale, fixed when laid: a tick only rescales it.</summary>
        [NativeDisableParallelForRestriction] public NativeArray<float3x3> TrailBasis;
        [NativeDisableParallelForRestriction] public NativeArray<float> TrailBirth;
        [WriteOnly] public NativeArray<Matrix4x4> BodyMatrices;
        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<Matrix4x4> TrailMatrices;

        public float T0, Dt;
        public int Steps;
        public bool DropTrails;
        public int TrailCap;
        public float TrailInterval, TrailLife, TrailTaper;
        public float3 TrailSize, BodySize;
        public float Envelope;
        public float4x4 LocalToWorld;

        public void Execute(int i)
        {
            float3 pos = Pos[i], vel = Vel[i];
            float3 pa = PhaseA[i], pb = PhaseB[i];
            float phase = TrailPhase[i];
            int head = TrailHead[i];
            int ring = i * TrailCap;
            float t = T0;

            for (int s = 0; s < Steps; s++)
            {
                t = T0 + s * Dt;
                GhostPilotMotion.Step(ref pos, ref vel, pa, pb, t, Dt, Motion);
                if (!DropTrails) continue;
                // The lab's drop: one prism per step when the per-pilot phase wraps, set back along the heading.
                phase += Dt / math.max(TrailInterval, 1e-3f);
                if (phase < 1f) continue;
                phase -= math.floor(phase);
                float3 fwd = math.normalizesafe(vel, new float3(0f, 0f, 1f));
                int slot = ring + head;
                float3 local = pos - fwd * (TrailSize.z * 0.6f);
                TrailPos[slot] = math.transform(LocalToWorld, local);
                TrailBasis[slot] = math.mul(new float3x3(LocalToWorld), new float3x3(GhostPilotMotion.Frame(vel)));
                TrailBirth[slot] = t;
                head = (head + 1) % TrailCap;
            }

            Pos[i] = pos;
            Vel[i] = vel;
            TrailPhase[i] = phase;
            TrailHead[i] = head;

            float now = T0 + Steps * Dt;
            float env = PilotEnvelope(Envelope, pa.x);
            BodyMatrices[i] = math.mul(LocalToWorld, float4x4.TRS(pos, GhostPilotMotion.Frame(vel), BodySize * env));

            float invLife = 1f / math.max(TrailLife, 1e-3f);
            for (int j = 0; j < TrailCap; j++)
            {
                int slot = ring + j;
                float age = now - TrailBirth[slot];
                if (!(age >= 0f && age < TrailLife))
                {
                    TrailMatrices[slot] = default;   // an empty slot draws nothing (zero scale)
                    continue;
                }
                float grow = math.saturate(age * 6f);   // the lab's grow-in
                float cross = grow * math.pow(math.saturate(1f - age * invLife), TrailTaper) * env;
                float3x3 b = TrailBasis[slot];
                TrailMatrices[slot] = new float4x4(
                    new float4(b.c0 * (TrailSize.x * cross), 0f),
                    new float4(b.c1 * (TrailSize.y * cross), 0f),
                    new float4(b.c2 * (TrailSize.z * env), 0f),
                    new float4(TrailPos[slot], 1f));
            }
        }

        /// <summary>The flock envelope staggered per pilot (by its first wander phase), smoothed.</summary>
        public static float PilotEnvelope(float envelope, float phase)
        {
            float h = phase / (2f * math.PI);
            return math.smoothstep(0f, 1f, math.saturate(envelope * (1f + BloomStagger) - BloomStagger * h));
        }

        /// <summary>How much of the bloom is spread across pilots (0 = all at once).</summary>
        const float BloomStagger = 0.35f;
    }
}
