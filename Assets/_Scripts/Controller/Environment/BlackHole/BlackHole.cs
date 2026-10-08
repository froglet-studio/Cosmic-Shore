using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One black hole in the HyperSea (Docs/BLACK_HOLE.md) — or, with <see cref="Polarity"/> Source,
    /// one WHITE hole: the same object with its radial law reversed (§11), the repulsor of a wormhole pair. A spawned object with two authored
    /// numbers — its <see cref="Strength"/> (the pull: the gravitational parameter and, with it, the
    /// influence radius) and its <see cref="Size"/> (the event-horizon radius; 0 derives it from
    /// the strength, which is what the console's one-number spawn does). It moves
    /// itself along <see cref="Velocity"/> (a hole can be driven through a prism field), spins
    /// about <see cref="SpinAxis"/> (which only the frame dragging reads),
    /// and eases its warp weight in on spawn and out on despawn so the drawn tidal stretch never pops.
    ///
    /// It does nothing to the world by itself. <see cref="BlackHoleRegistry"/> owns the list of
    /// live holes and runs the three things a hole does to its surroundings each frame — pull
    /// prism bodies (<see cref="BlackHoleGravityField"/>), pull vessels
    /// (<see cref="BlackHoleVesselPull"/>), stretch what is drawn by its tides (<see cref="BlackHoleWarp"/>) —
    /// so a hole is a record the systems read, never a system of its own. Spawn through the
    /// registry; a hand-placed component registers itself at <c>OnEnable</c> with its serialized
    /// strength, so a scene can author one too.
    ///
    /// What the player SEES is the <see cref="BlackHoleLens"/> (Docs/BLACK_HOLE.md §5.1): a
    /// per-pixel ray trace of the scene behind the hole bent through Schwarzschild spacetime —
    /// the background distorted into arcs and an Einstein ring, and the shadow (~2.6× the
    /// horizon). There is no painted accretion disc: what orbits and spirals in is the real mass
    /// the gravity field moves. If the lens shader cannot load, the hole falls back to a plain
    /// black sphere — never an invisible hole.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackHole : MonoBehaviour
    {
        [Header("Black hole")]

        [Tooltip("The hole's PULL. GM and the influence radius scale from it (BlackHoleConfigSO); with " +
                 "Horizon Radius at 0 so does the size. 10 is a modest hole; 50 swallows a cell's worth " +
                 "of mass.")]
        [Min(0f)]
        [SerializeField] float strength = 10f;

        [Tooltip("The hole's SIZE: its event-horizon radius r_s, world units — what it swallows and what " +
                 "the lens draws (the black shadow is ~2.6 r_s in radius). 0 = derived from strength " +
                 "(BlackHoleConfigSO.horizonPerStrength).")]
        [Min(0f)]
        [SerializeField] float horizonRadius = 0f;

        [Tooltip("World-space velocity the hole travels at, u/s. Zero is a hole parked where it was " +
                 "spawned. A moving hole does not drag mass along — it pulls: mass it passes is deflected, " +
                 "slingshot, set orbiting or swallowed by the potential, the way a real one does.")]
        [SerializeField] Vector3 velocity = Vector3.zero;

        [Tooltip("Axis the hole spins about. Its spacetime is dragged around this axis (Lense-Thirring, " +
                 "strength set by BlackHoleConfig.spin), so infalling mass winds up about it near the " +
                 "horizon.")]
        [SerializeField] Vector3 spinAxis = Vector3.forward;

        [Tooltip("SINK: a black hole — pulls, swallows at the horizon, shadows its centre. SOURCE: its " +
                 "antisymmetric twin, a white hole — the same strength REPELS prisms and vessels, nothing " +
                 "ever crosses its horizon inward, its spacetime is dragged the other way, its tides " +
                 "flatten instead of stretching, and its lens diverges light instead of focusing it " +
                 "(Docs/BLACK_HOLE.md §12).")]
        [SerializeField] HolePolarity polarity = HolePolarity.Sink;

        [Header("Felt pull on vessels (0 = the physical pull)")]
        [Tooltip("How hard VESSELS feel this hole, in the vessel's own frame (Docs/BLACK_HOLE.md §12): " +
                 "felt acceleration = Sign · k · cruise² · R_throat · s(r) / r², where cruise is the hull's own " +
                 "unboosted speed and s the warp field's local scale. Inverse-square when nothing is warped; " +
                 "under a radial warp it falls as 1/r, so it is felt across the whole shrinking approach rather " +
                 "than in its last few units. 0 = vessels feel the hole's physical Paczynski-Wiita pull instead.")]
        [Min(0f)]
        [SerializeField] float vesselFeltStrength = 0f;

        [Tooltip("The felt pull's ceiling, in multiples of the hull's own cruise speed. Above 1 a source " +
                 "holds off a hull at cruise — it has to boost through — and a sink carries one into it at " +
                 "(1 + this) × cruise.")]
        [Min(0f)]
        [SerializeField] float vesselFeltCap = 1.3f;

        [Tooltip("How far the felt pull reaches, in throat radii (the seated mouth's; the shadow's without one).")]
        [Min(1f)]
        [SerializeField] float vesselFeltReach = 12f;

        [Header("Smooth well (0 = a black hole)")]
        [Tooltip("Core radius of a SMOOTH well, world units (Docs/CRYSTAL_WORMHOLE.md). Above 0 the hole is " +
                 "not a black hole at all: its gravity is Plummer-softened (−GM·r / (r² + ε²)^1.5, finite and " +
                 "smooth everywhere, zero at the centre), it drags no frame, its tides are softened by the same " +
                 "core, and its lens is a smooth graded bulge (see Lens Strength) with no horizon, shadow or ring. " +
                 "0 = the black hole: Paczynski-Wiita, Lense-Thirring, the Schwarzschild ray trace.")]
        [Min(0f)]
        [SerializeField] float softening = 0f;

        [Tooltip("A smooth well's lens: the peak deflection, as a fraction of the angle its core subtends. " +
                 "Kept under 1 so the warp never folds the image (no caustic, no ring): an attractor magnifies " +
                 "what is behind it by up to 1/(1−A), a repulsor shrinks it by 1/(1+A).")]
        [Range(0f, 0.9f)]
        [SerializeField] float lensStrength = 0.6f;

        const string HorizonName = "Horizon";

        static Material s_horizonMaterial;
        static Material s_sourceHorizonMaterial;

        Transform _horizon;     // fallback only: the lens draws the shadow itself
        BlackHoleLens _lens;
        bool _visualBuilt;
        float _weight;
        float _amplitude = 1f;
        bool _despawning;
        bool _registered;

        /// <summary>Registry id (1-based, never reused within a session). 0 until registered.</summary>
        public int Id { get; internal set; }

        /// <summary>
        /// The vessel that slung this hole (the Stoat's wormhole pair), or null for an environmental
        /// hole (the tool, the console, a cell). An owned hole pulls ONLY its owner: a vessel may not
        /// move an opposing vessel (Docs/ELEMENTAL_ECONOMY.md §9, LOCKED), and a pair a pilot laid is
        /// that pilot's act. Prisms feel every hole either way.
        /// </summary>
        public Transform OwnerVessel { get; internal set; }

        public float Strength => strength;

        /// <summary>The authored size (event-horizon radius, world units); 0 = derived from strength.</summary>
        public float Size => horizonRadius;
        public Vector3 Velocity { get => velocity; set => velocity = value; }

        /// <summary>Unit spin axis; a zero vector authored by hand reads as +Z.</summary>
        public Vector3 SpinAxis
        {
            get => spinAxis.sqrMagnitude > 1e-6f ? spinAxis.normalized : Vector3.forward;
            set => spinAxis = value;
        }

        /// <summary>The warp's eased weight, 0..1. Rises over the config ease on spawn, falls on despawn.</summary>
        public float WarpWeight => _weight;

        /// <summary>True from <see cref="BeginDespawn"/> until the object is destroyed.</summary>
        public bool IsDespawning => _despawning;

        public HolePolarity Polarity => polarity;
        public bool IsSource => polarity == HolePolarity.Source;

        /// <summary>+1 for a sink, −1 for a source: the sign every signed quantity of the hole carries.</summary>
        public float Sign => IsSource ? -1f : 1f;

        /// <summary>
        /// The other pole of a DIPOLE (Docs/BLACK_HOLE.md §12), or null for a lone hole. A sink with a
        /// throat does not destroy what it captures: a prism crossing its horizon is carried through
        /// to the same point inside the throat's horizon and goes on from there.
        /// </summary>
        public BlackHole Throat { get; internal set; }

        /// <summary>
        /// Radius of the wormhole mouth seated at this hole's centre (world units; 0 = none). The
        /// lens treats it as solid: a bent ray that lands on it falls back to the sky, so the mouth
        /// is seen only where it is — in place of the shadow — and never smeared into the rings.
        /// </summary>
        public float ThroatRadius { get; internal set; }

        /// <summary>Plummer core radius of a smooth well, world units; 0 = a black hole.</summary>
        public float Softening => softening;
        public bool IsSmooth => softening > 0f;
        /// <summary>A smooth well's lens strength A (peak deflection / core angle), before <see cref="Amplitude"/>.</summary>
        public float LensStrength => lensStrength;

        /// <summary>
        /// A live multiplier on everything the hole does to its surroundings — its gravity (and so the
        /// felt pull), its tides and its lens (Docs/CRYSTAL_WORMHOLE.md §4). 1 = as authored, 0 = space
        /// is flat around it. Driven by a crystal wormhole's formation and annihilation, which is how a
        /// pair grows out of nothing and interferes back into it.
        /// </summary>
        public float Amplitude
        {
            get => _amplitude;
            set => _amplitude = Mathf.Max(0f, value);
        }

        /// <summary>Make this a smooth well (see <see cref="Softening"/>). Removes a black hole's fallback sphere.</summary>
        internal void ConfigureSmoothWell(float coreRadius, float lens)
        {
            softening = Mathf.Max(0f, coreRadius);
            lensStrength = Mathf.Clamp(lens, 0f, 0.9f);
            if (IsSmooth && _horizon != null)
            {
                Destroy(_horizon.gameObject);
                _horizon = null;
            }
        }

        /// <summary>The felt-pull law's strength k (0 = vessels feel the physical pull). See the field's tooltip.</summary>
        public float VesselFeltStrength => vesselFeltStrength;
        /// <summary>The felt pull's ceiling, × the hull's cruise speed.</summary>
        public float VesselFeltCap => vesselFeltCap;
        /// <summary>The felt pull's reach, world units: <c>vesselFeltReach</c> throat radii.</summary>
        public float VesselFeltReach => vesselFeltReach * FeltThroatRadius;
        /// <summary>The radius the felt law is measured from: the seated mouth's, else the shadow's.</summary>
        public float FeltThroatRadius => ThroatRadius > 0f ? ThroatRadius : HorizonRadius * 2.598076f;

        /// <summary>Switch vessels to the felt law (the dipole's holes). k = 0 restores the physical pull.</summary>
        internal void ConfigureFeltVesselLaw(float strengthK, float capCruises, float reachThroats)
        {
            vesselFeltStrength = Mathf.Max(0f, strengthK);
            vesselFeltCap = Mathf.Max(0f, capCruises);
            vesselFeltReach = Mathf.Max(1f, reachThroats);
        }

        /// <summary>SIGNED gravitational parameter: positive pulls (sink), negative pushes (source).</summary>
        public float GM => Sign * BlackHoleRegistry.Config.GM(strength) * _amplitude;
        /// <summary>The event-horizon radius in effect: <see cref="Size"/>, or derived from strength.</summary>
        public float HorizonRadius => BlackHoleRegistry.Config.HorizonRadius(strength, horizonRadius);
        public float InfluenceRadius => BlackHoleRegistry.Config.InfluenceRadius(strength, HorizonRadius);
        public float WarpReach => BlackHoleRegistry.Config.WarpReachForHorizon(HorizonRadius);

        /// <summary>The job-side snapshot of this hole for this frame.</summary>
        public BlackHolePhysics.Well ToWell(BlackHoleConfigSO config)
        {
            var p = transform.position;
            var axis = SpinAxis;
            float rs = config.HorizonRadius(strength, horizonRadius);
            float gm = config.GM(strength);   // magnitude; the sign is the polarity's
            return new BlackHolePhysics.Well
            {
                Position = new Unity.Mathematics.float3(p.x, p.y, p.z),
                GM = Sign * gm * _amplitude,
                Horizon = BlackHolePhysics.Horizon.Of(rs),
                InfluenceRadius = config.InfluenceRadius(strength, rs),
                SpinAxis = new Unity.Mathematics.float3(axis.x, axis.y, axis.z),
                // A source's frame turns the other way: the dipole is antisymmetric in its spin too. A
                // smooth well drags nothing — frame dragging is a horizon's, and it has none.
                FrameDrag = IsSmooth ? 0f : Sign * _amplitude * BlackHolePhysics.FrameDragCoefficient(gm, rs, config.Spin),
                Softening = softening,
            };
        }

        /// <summary>Spawn-time configuration (the registry's path). Idempotent.</summary>
        internal void Configure(float newStrength, Vector3 newVelocity, Vector3 newSpinAxis, float newHorizonRadius = 0f,
            HolePolarity newPolarity = HolePolarity.Sink)
        {
            polarity = newPolarity;
            // OnEnable built the visual before this ran; a fallback sphere follows the polarity.
            if (_horizon != null && _horizon.TryGetComponent<MeshRenderer>(out var horizonRenderer))
                horizonRenderer.sharedMaterial = IsSource ? SourceHorizonMaterial() : HorizonMaterial();
            strength = Mathf.Max(0f, newStrength);
            horizonRadius = Mathf.Max(0f, newHorizonRadius);
            velocity = newVelocity;
            spinAxis = newSpinAxis;
            polarity = newPolarity;
            EnsureVisual();
            ApplyScale();
        }

        /// <summary>Retune a live hole. Its horizon, influence and lens follow immediately.</summary>
        public void SetStrength(float newStrength)
        {
            strength = Mathf.Max(0f, newStrength);
            ApplyScale();
        }

        /// <summary>Resize a live hole (event-horizon radius, world units; 0 = derive from strength).</summary>
        public void SetSize(float newHorizonRadius)
        {
            horizonRadius = Mathf.Max(0f, newHorizonRadius);
            ApplyScale();
        }

        /// <summary>
        /// Ease the warp out over the config's ease, then destroy. Bodies the hole was pulling are
        /// released by the field on the next tick (a hole that is gone stops pulling at once —
        /// only the PHOTONS ease, the same split every §4.7 global keeps).
        /// </summary>
        public void BeginDespawn()
        {
            if (_despawning) return;
            _despawning = true;
            BlackHoleRegistry.Unregister(this);
            _registered = false;
        }

        void OnEnable()
        {
            EnsureVisual();
            ApplyScale();
            if (!_registered && !_despawning)
            {
                BlackHoleRegistry.Register(this);
                _registered = true;
            }
        }

        void OnDisable()
        {
            if (_registered)
            {
                BlackHoleRegistry.Unregister(this);
                _registered = false;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (velocity.sqrMagnitude > 0f)
                transform.position += velocity * dt;

            var config = BlackHoleRegistry.Config;
            float ease = config.WarpEaseSeconds;
            float rate = ease > 0f ? dt / ease : 1f;
            if (_despawning)
            {
                _weight = Mathf.Max(0f, _weight - rate);
                if (_weight <= 0f) Destroy(gameObject);
            }
            else
            {
                _weight = Mathf.Min(1f, _weight + rate);
            }

            if (_horizon != null) _horizon.localScale = Vector3.one * (2f * HorizonRadius * _weight);
        }

        void ApplyScale()
        {
            if (_horizon != null) _horizon.localScale = Vector3.one * (2f * HorizonRadius * _weight);
        }

        /// <summary>
        /// The lens when the config wants it and its shader loads; otherwise the plain black
        /// sphere. Built once — a config change mid-hole takes effect on the next spawn.
        /// </summary>
        void EnsureVisual()
        {
            if (_visualBuilt) return;
            _visualBuilt = true;
            if (BlackHoleRegistry.Config.LensEnabled)
                _lens = BlackHoleLens.Create(this);
            if (_lens == null)
            {
                var existing = transform.Find(HorizonName);
                _horizon = existing != null ? existing : BuildSphere(HorizonName, IsSource ? SourceHorizonMaterial() : HorizonMaterial());
            }
        }

        Transform BuildSphere(string childName, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = childName;
            // A hole's photons are not a collider: nothing touches the sphere, the FIELD does
            // the touching (Prism bodies are consumed at the horizon by the gravity job).
            if (go.TryGetComponent<Collider>(out var collider)) Destroy(collider);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            if (material != null && go.TryGetComponent<MeshRenderer>(out var renderer))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return go.transform;
        }

        /// <summary>The fallback for a SOURCE: white where a sink's is black — antisymmetric even without the lens.</summary>
        static Material SourceHorizonMaterial()
        {
            if (s_sourceHorizonMaterial != null) return s_sourceHorizonMaterial;
            s_sourceHorizonMaterial = UnlitMaterial(Color.white, "WhiteHoleHorizon");
            return s_sourceHorizonMaterial;
        }

        static Material HorizonMaterial()
        {
            if (s_horizonMaterial != null) return s_horizonMaterial;
            s_horizonMaterial = UnlitMaterial(Color.black, "BlackHoleHorizon");
            return s_horizonMaterial;
        }


        /// <summary>
        /// Same runtime material path as <c>ToyFactory.AccentMaterial</c>: URP's Unlit, with the
        /// sprite shader as the fallback that always exists. Cached per role, never mutated.
        /// </summary>
        static Material UnlitMaterial(Color color, string materialName)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader == null)
            {
                CSDebug.LogWarning("[BlackHole] Neither URP Unlit nor Sprites/Default could be found; the hole will render with no material.");
                return null;
            }
            return new Material(shader) { color = color, name = materialName };
        }
    }

    /// <summary>Which way a hole's gravity points (Docs/BLACK_HOLE.md §12). Static values: serialized.</summary>
    public enum HolePolarity
    {
        /// <summary>A black hole: pulls, captures at the horizon.</summary>
        Sink = 0,
        /// <summary>A white hole: the same strength, repelling; nothing enters its horizon.</summary>
        Source = 1,
    }
}
