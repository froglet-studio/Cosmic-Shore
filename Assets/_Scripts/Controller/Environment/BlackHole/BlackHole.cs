using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One black hole in the HyperSea (Docs/BLACK_HOLE.md). A spawned object with ONE authored
    /// number — its <see cref="Strength"/> — from which the config derives everything the physics
    /// needs: the gravitational parameter, the event horizon, the influence radius. It moves
    /// itself along <see cref="Velocity"/> (a hole can be driven through a prism field), spins
    /// about <see cref="SpinAxis"/> (which only the frame dragging and the accretion disc read),
    /// and eases its warp weight in on spawn and out on despawn so the GPU bend never pops.
    ///
    /// It does nothing to the world by itself. <see cref="BlackHoleRegistry"/> owns the list of
    /// live holes and runs the three things a hole does to its surroundings each frame — pull
    /// prism bodies (<see cref="BlackHoleGravityField"/>), pull vessels
    /// (<see cref="BlackHoleVesselPull"/>), bend what is drawn (<see cref="BlackHoleWarp"/>) —
    /// so a hole is a record the systems read, never a system of its own. Spawn through the
    /// registry; a hand-placed component registers itself at <c>OnEnable</c> with its serialized
    /// strength, so a scene can author one too.
    ///
    /// What the player SEES is the <see cref="BlackHoleLens"/> (Docs/BLACK_HOLE.md §5.1): a
    /// per-pixel ray trace of the scene behind the hole bent through Schwarzschild spacetime —
    /// the background distorted into arcs and an Einstein ring, the shadow (~2.6× the horizon),
    /// and the accretion disc lensed over the top and bottom of the shadow. The disc is FED:
    /// every prism the hole consumes adds to <see cref="DiskFeed"/>, which decays, so a hole that
    /// is eating forms its disc in real time and a starving one fades. If the lens shader cannot
    /// load, the hole falls back to a plain black sphere — never an invisible hole.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackHole : MonoBehaviour
    {
        [Header("Black hole")]
        [Tooltip("The one authored number. GM, the horizon and the influence radius all scale from it " +
                 "(BlackHoleConfigSO). 10 is a modest hole; 50 swallows a cell's worth of mass.")]
        [Min(0f)]
        [SerializeField] float strength = 10f;

        [Tooltip("World-space velocity the hole travels at, u/s. Zero is a hole parked where it was " +
                 "spawned; a moving hole drags the mass it passes along with it.")]
        [SerializeField] Vector3 velocity = Vector3.zero;

        [Tooltip("Axis the hole's spacetime rotates about (frame dragging) and the normal of its " +
                 "accretion disc. Mass is swept into orbits in the plane perpendicular to it.")]
        [SerializeField] Vector3 spinAxis = Vector3.forward;

        const string HorizonName = "Horizon";

        static Material s_horizonMaterial;

        Transform _horizon;     // fallback only: the lens draws the shadow itself
        BlackHoleLens _lens;
        bool _visualBuilt;
        float _weight;
        float _diskFeed;
        bool _despawning;
        bool _registered;

        /// <summary>Registry id (1-based, never reused within a session). 0 until registered.</summary>
        public int Id { get; internal set; }

        public float Strength => strength;
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

        /// <summary>
        /// Accretion-disc density this hole has been FED (on top of the config's base density):
        /// rises with every prism it consumes, halves every <c>diskFeedHalfLife</c> seconds.
        /// </summary>
        public float DiskFeed => _diskFeed;

        /// <summary>A prism crossed this hole's horizon: its mass joins the disc.</summary>
        public void NotifyCapture()
        {
            var config = BlackHoleRegistry.Config;
            _diskFeed = Mathf.Min(config.DiskFeedMax, _diskFeed + config.DiskFeedPerCapture);
        }

        public float GM => BlackHoleRegistry.Config.GM(strength);
        public float HorizonRadius => BlackHoleRegistry.Config.HorizonRadius(strength);
        public float InfluenceRadius => BlackHoleRegistry.Config.InfluenceRadius(strength);
        public float WarpReach => BlackHoleRegistry.Config.WarpReach(strength);

        /// <summary>The job-side snapshot of this hole for this frame.</summary>
        public BlackHolePhysics.Well ToWell(BlackHoleConfigSO config)
        {
            var p = transform.position;
            var v = velocity;
            var axis = SpinAxis;
            return new BlackHolePhysics.Well
            {
                Position = new Unity.Mathematics.float3(p.x, p.y, p.z),
                Velocity = new Unity.Mathematics.float3(v.x, v.y, v.z),
                GM = config.GM(strength),
                Horizon = BlackHolePhysics.Horizon.Of(config.HorizonRadius(strength)),
                InfluenceRadius = config.InfluenceRadius(strength),
                SpinAxis = new Unity.Mathematics.float3(axis.x, axis.y, axis.z),
                FrameDragging = config.FrameDragging,
            };
        }

        /// <summary>Spawn-time configuration (the registry's path). Idempotent.</summary>
        internal void Configure(float newStrength, Vector3 newVelocity, Vector3 newSpinAxis)
        {
            strength = Mathf.Max(0f, newStrength);
            velocity = newVelocity;
            spinAxis = newSpinAxis;
            EnsureVisual();
            ApplyScale();
        }

        /// <summary>Retune a live hole. Its horizon, influence and disc follow immediately.</summary>
        public void SetStrength(float newStrength)
        {
            strength = Mathf.Max(0f, newStrength);
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

            if (_diskFeed > 0f)
                _diskFeed *= Mathf.Exp(-0.6931472f * dt / config.DiskFeedHalfLife);

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
                _horizon = existing != null ? existing : BuildSphere(HorizonName, HorizonMaterial());
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
}
