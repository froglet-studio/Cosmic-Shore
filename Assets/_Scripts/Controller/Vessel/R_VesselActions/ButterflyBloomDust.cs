using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The omni-crystal bloom's DUST: what the bloom does to every prism it reaches, and how it
    /// looks, so the bloom and the capsule read as one verb. Lives on
    /// <c>AOEButterflyBloom.prefab</c> beside the <see cref="AOEExplosion"/>.
    /// Design record: <c>R_VesselActions/BUTTERFLY.md</c> §3.3a.
    ///
    /// <para><b>The outcome is the capsule's own.</b> As an <see cref="IExplosionPrismPayload"/>
    /// it is handed each prism the blast's sweep reaches (once per blast, 48 a frame) and calls
    /// <see cref="SkimmerScaleDustPrismEffectSO.Apply"/> on the dust's OWN asset — the same weights,
    /// the same per-prism hash, the same Diamond Dust gate — with the blast's impact vector as the
    /// destroy velocity. Nothing is restated. (This replaced a separate
    /// <c>ExplosionScaleDustPrismEffectSO</c> container asset that loaded as null in the editor and
    /// left the bloom dusting nothing; see BUTTERFLY.md §3.3a.)</para>
    ///
    /// <para><b>Two layers of motes, both the capsule's motes.</b> Same material, same colour rule
    /// (<see cref="ButterflyDustField.ResolveMoteColour"/> — the shielded rim of the pilot's
    /// domain), same in-hold-out alpha and the same slow downward drift:</para>
    /// <list type="bullet">
    /// <item><b>The bloom</b> — as the wavefront passes each radius it leaves dust behind, at a
    /// constant density through the whole sphere (the emitted count follows the swept VOLUME,
    /// r³, not the radius), so the sphere fills with a falling haze rather than a shell that
    /// races off-screen.</item>
    /// <item><b>A puff on every prism the dust changed</b>, so the eye is led to exactly the mass
    /// that grew, shielded, went dangerous, shrank, was stolen or died.</item>
    /// </list>
    ///
    /// <para>The particle object is DETACHED from the blast: the blast's transform scales to the
    /// bloom's full diameter and is destroyed the frame its sweep ends, and the motes must outlive
    /// it and fade (continuity of existence). It retires itself one mote lifetime later.</para>
    ///
    /// <para>The motes are cosmetic and per machine; their scatter uses a private hash rather than
    /// <c>UnityEngine.Random</c> (vessel contract rule 18).</para>
    ///
    /// <para><b>Telemetry.</b> On retirement it reports, on <see cref="CSLogChannel.ButterflyBloom"/>,
    /// how many prisms the sweep reached and what the dust did to them, by outcome.</para>
    /// </summary>
    [RequireComponent(typeof(AOEExplosion))]
    public sealed class ButterflyBloomDust : MonoBehaviour, IExplosionPrismPayload
    {
        [Header("Dust")]
        [Tooltip("The Dust-mode capsule's own effect asset (ButterflyScaleDustPrismEffect). Its " +
                 "weights, grow/shrink fractions, debris tuning and Diamond Dust gate ARE the " +
                 "bloom's — nothing is restated here. Empty = the bloom changes no prism (reported " +
                 "once).")]
        [SerializeField] SkimmerScaleDustPrismEffectSO dust;

        [Header("Look")]
        [Tooltip("Particle material for the motes. Author the SAME material as the dust capsule " +
                 "(ButterflyDustField) so the two read as one dust. A missing one falls back to " +
                 "URP Particles/Unlit and is reported once.")]
        [SerializeField] Material particleMaterial;

        [Tooltip("FALLBACK mote colour (no playable domain or no palette). In play the motes wear " +
                 "the shielded rim of the pilot's domain, exactly as the capsule's do. The alpha " +
                 "is the peak of each mote's in-hold-out fade either way.")]
        [SerializeField] Color dustColor = new(1f, 0.86f, 0.52f, 0.9f);

        [Tooltip("Motes the bloom leaves through its whole sphere, spread at constant density as " +
                 "the wavefront passes. At a 450-unit radius, 2400 motes are ~57 units apart.")]
        [SerializeField, Min(0)] int bloomMotes = 2400;

        [Tooltip("Mote size range in WORLD units. Larger than the capsule's 2.5-6 because the " +
                 "bloom's motes are spread through a 900-unit sphere rather than packed into a " +
                 "24-unit column.")]
        [SerializeField] Vector2 moteSize = new(6f, 14f);

        [SerializeField, Min(0.05f)] float moteLifetime = 2.2f;

        [Tooltip("Motes drift slowly downward, as the capsule's dust does.")]
        [SerializeField] float fallSpeed = 6f;

        [Header("Puffs (one per prism the dust changed)")]
        [Tooltip("Motes released at a prism the bloom dusted.")]
        [SerializeField, Min(0)] int motesPerDustedPrism = 5;

        [Tooltip("How fast a puff's motes spread from the prism, world units per second.")]
        [SerializeField, Min(0f)] float puffSpeed = 10f;

        [Tooltip("Puff motes are this much larger than the bloom's, so the changed mass is the " +
                 "brightest dust on screen.")]
        [SerializeField, Min(0.1f)] float puffSizeScale = 1.4f;

        [Tooltip("Ceiling on motes alive at once, bloom and puffs together. A bloom over a dense " +
                 "arena dusts up to 48 prisms a frame; past this, further motes are simply not " +
                 "drawn (the outcomes still land — this is presentation only).")]
        [SerializeField, Min(1)] int maxMotes = 12000;

        [Inject] GameDataSO _gameData;

        AOEExplosion _explosion;
        ExplosionImpactor _impactor;
        float _colliderRadius = 0.5f;
        float _fullRadius;
        Domains _domain;
        ParticleSystem _particles;
        Color _colour;
        int _bloomEmitted;
        float _lastRadius;
        uint _rng;
        readonly int[] _tally = new int[10];
        static bool s_warnedNoMaterial;
        static bool s_warnedNoDust;

        void Awake()
        {
            _explosion = GetComponent<AOEExplosion>();
            TryGetComponent(out _impactor);
            if (TryGetComponent(out SphereCollider sphere)) _colliderRadius = sphere.radius;
        }

        // Start, not Awake: the blast is Instantiated, THEN injected and Initialized, and only
        // after that does it know its domain, size and position (ExplosionHelper.SpawnAllAndDetonate).
        void Start()
        {
            _rng = (uint)GetInstanceID() * 2654435761u ^ (uint)Mathf.RoundToInt(transform.position.x * 7f);
            if (_rng == 0) _rng = 0x9E3779B9u;
            _domain = _explosion.Domain;
            _fullRadius = _colliderRadius * _explosion.MaxScale;
            _colour = ButterflyDustField.ResolveMoteColour(_gameData, _domain, dustColor);
            Build();
        }

        void LateUpdate()
        {
            if (!_particles || _fullRadius <= 0f) return;

            // The wavefront's radius this frame, read off the blast itself (AOEExplosion scales
            // its transform from 0 to MaxScale), so the dust can never run ahead of or behind it.
            float full = _fullRadius;
            float radius = Mathf.Min(_colliderRadius * transform.localScale.x, full);
            if (radius <= _lastRadius) return;

            // Constant density through the sphere: the cumulative count follows the swept volume.
            float swept = radius / full;
            int target = Mathf.Min(bloomMotes, Mathf.CeilToInt(bloomMotes * swept * swept * swept));
            var centre = transform.position;
            var emit = new ParticleSystem.EmitParams();
            for (; _bloomEmitted < target; _bloomEmitted++)
            {
                float r = Mathf.Lerp(_lastRadius, radius, Next01());
                emit.position = centre + RandomDirection() * r;
                emit.velocity = new Vector3(0f, -fallSpeed, 0f);
                emit.startSize = Mathf.Lerp(moteSize.x, moteSize.y, Next01());
                emit.startColor = _colour;
                _particles.Emit(emit, 1);
            }
            _lastRadius = radius;
        }

        /// <summary>
        /// The blast reached <paramref name="prismImpactee"/>: roll the capsule's dust on it, then
        /// puff where it stood.
        /// </summary>
        public void OnPrismReached(ExplosionImpactor blast, PrismImpactor prismImpactee)
        {
            if (!dust)
            {
                if (!s_warnedNoDust)
                {
                    s_warnedNoDust = true;
                    CSDebug.LogError("[ButterflyBloomDust] no Scale Dust asset wired — the bloom " +
                                     "changes no prism. Wire ButterflyScaleDustPrismEffect on " +
                                     "AOEButterflyBloom.prefab.", this);
                }
                return;
            }

            var status = blast ? blast.SourceVessel?.VesselStatus : null;
            var prism = prismImpactee ? prismImpactee.Prism : null;
            if (status == null || !prism) { Count(ScaleDustOutcome.None); return; }

            // Captured BEFORE the outcome: a destroy or steal can retire/reparent the prism.
            Vector3 at = prism.transform.position;
            OnDusted(at, dust.Apply(prismImpactee, status, blast.BlastImpactVector(at)));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetWarnings() => s_warnedNoMaterial = s_warnedNoDust = false;

        void Count(ScaleDustOutcome outcome)
        {
            int slot = (int)outcome;
            if (slot >= 0 && slot < _tally.Length) _tally[slot]++;
        }

        /// <summary>
        /// The dust changed <paramref name="outcome"/> on the prism that stood at
        /// <paramref name="position"/>: release a puff there and count it.
        /// </summary>
        void OnDusted(Vector3 position, ScaleDustOutcome outcome)
        {
            Count(outcome);
            if (outcome == ScaleDustOutcome.None || !_particles || motesPerDustedPrism <= 0) return;

            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < motesPerDustedPrism; i++)
            {
                emit.position = position;
                emit.velocity = RandomDirection() * puffSpeed + new Vector3(0f, -fallSpeed, 0f);
                emit.startSize = Mathf.Lerp(moteSize.x, moteSize.y, Next01()) * puffSizeScale;
                emit.startColor = _colour;
                _particles.Emit(emit, 1);
            }
        }

        void OnDestroy()
        {
            if (_particles)
            {
                // The motes in the air finish their lives; the emitter object goes with the last.
                _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                Destroy(_particles.gameObject, moteLifetime + 0.25f);
            }

            if (!CSDebug.IsVerbose(CSLogChannel.ButterflyBloom)) return;
            // ReferenceEquals, not Unity's null: the sibling impactor is being destroyed in this
            // same teardown, which Unity's == would report as null, but its fields are still ours
            // to read.
            bool hasImpactor = !ReferenceEquals(_impactor, null);
            int reached = hasImpactor ? _impactor.PrismEffectsReached : -1;
            int dispatched = hasImpactor ? _impactor.PrismEffectsDispatched : -1;
            CSDebug.LogVerbose(CSLogChannel.ButterflyBloom,
                $"[ButterflyBloom] r={_fullRadius:F0} domain={_domain} reached={reached} " +
                $"dispatched={dispatched} | own: " +
                $"grow {T(ScaleDustOutcome.Grown)}, danger {T(ScaleDustOutcome.Dangerous)}, " +
                $"shield {T(ScaleDustOutcome.Shielded)}, super {T(ScaleDustOutcome.SuperShielded)}, " +
                $"untouched {T(ScaleDustOutcome.Untouched)} | opposing: " +
                $"destroy {T(ScaleDustOutcome.Destroyed)}, shrink {T(ScaleDustOutcome.Shrunk)}, " +
                $"steal {T(ScaleDustOutcome.Stolen)}, deflect {T(ScaleDustOutcome.Deflected)} | " +
                $"skipped {T(ScaleDustOutcome.None)}");
        }

        int T(ScaleDustOutcome outcome) => _tally[(int)outcome];

        void Build()
        {
            var go = new GameObject("BloomDustMotes");
            go.layer = gameObject.layer;
            go.transform.position = transform.position;

            _particles = go.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = _particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = moteLifetime;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(moteSize.x, moteSize.y);
            main.startColor = _colour;
            main.gravityModifier = 0f;
            main.maxParticles = Mathf.Max(maxMotes, bloomMotes);

            // Every mote is placed by hand (Emit), so the module that would place them is off.
            var emission = _particles.emission;
            emission.enabled = false;
            var shape = _particles.shape;
            shape.enabled = false;

            // In, hold, out — the capsule's own fade.
            var colour = _particles.colorOverLifetime;
            colour.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f),
                    new GradientAlphaKey(1f, 0.65f), new GradientAlphaKey(0f, 1f),
                });
            colour.color = new ParticleSystem.MinMaxGradient(g);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = ResolveMaterial();

            _particles.Play(true);
        }

        Material ResolveMaterial()
        {
            if (particleMaterial) return particleMaterial;
            if (!s_warnedNoMaterial)
            {
                s_warnedNoMaterial = true;
                CSDebug.LogWarning("[ButterflyBloomDust] no particle material wired — falling back " +
                                   "to URP Particles/Unlit. Wire the dust capsule's material on " +
                                   "AOEButterflyBloom.prefab.", this);
            }
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            return shader ? new Material(shader) : null;
        }

        // ── scatter: a private xorshift, never the global RNG ──────────────────────

        float Next01()
        {
            unchecked
            {
                _rng ^= _rng << 13;
                _rng ^= _rng >> 17;
                _rng ^= _rng << 5;
            }
            return (_rng & 0xFFFFFF) / 16777216f;
        }

        Vector3 RandomDirection()
        {
            float z = Next01() * 2f - 1f;
            float phi = Next01() * Mathf.PI * 2f;
            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            return new Vector3(s * Mathf.Cos(phi), s * Mathf.Sin(phi), z);
        }
    }
}
