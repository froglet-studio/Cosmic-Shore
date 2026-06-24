using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Server-authoritative billiard-physics match ball for Astro League.
    ///
    /// The server owns the rigidbody simulation, resolves vessel strikes, and replicates
    /// position + velocity through NetworkVariables; non-server peers run a kinematic body
    /// and dead-reckon toward the replicated state (netPos + netVel * age), which stays
    /// smooth at billiard speeds where plain interpolation lags.
    ///
    /// Vessels move via transform (not rigidbody), so momentum transfer cannot read physics
    /// velocity. The server samples every vessel root's position each FixedUpdate and uses
    /// the resulting velocity estimate for strikes — this works identically for the host's
    /// own vessel, replicated client vessels, and AI (VesselStatus.Speed/Course is only
    /// trustworthy on the owning peer, see ResolveStrikerVelocity).
    ///
    /// Impact juice (emission flash, burst particles, distance-scaled camera shake, haptics)
    /// plays on every peer via ClientRpc. Hitstop is solo-session-only — local timescale
    /// changes desync connected peers.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class AstroLeagueBall : NetworkBehaviour
    {
        [Header("Config")]
        [SerializeField] AstroLeagueSettingsSO settings;
        [SerializeField] GameDataSO gameData;

        [Header("Visuals")]
        [Tooltip("The prism fresnel material (PrismMaterial.mat) — cloned at runtime so the ball " +
                 "renders with the same 3D fresnel-rim look as trail prisms. Falls back to the " +
                 "Shader Graphs/BlockGraph shader, then URP/Lit, if unassigned.")]
        [SerializeField] Material prismMaterial;

        [Header("Payload Colors")]
        [SerializeField] Color primaryColor = new(1f, 0.6f, 0.1f, 1f);
        [SerializeField] Color secondaryColor = new(0.2f, 0.5f, 1f, 1f);
        [SerializeField] Color tertiaryColor = new(1f, 0.15f, 0.6f, 1f);
        [SerializeField] float colorCycleSpeed = 1.2f;
        [SerializeField] float baseLightIntensity = 3f;

        // Prism fresnel-shader (BlockGraph) properties: bright rim + dark base.
        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        bool _usesFresnel; // true when the ball material is the prism BlockGraph shader

        Rigidbody rb;
        SphereCollider sphereCol;
        Vector3 spawnPosition;
        Vector3 _baseScale = Vector3.one;
        bool hitstopActive;
        CancellationToken destroyToken;

        // ── Replicated state (server write) ─────────────────────────────────
        readonly NetworkVariable<Vector3> n_Position =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<Vector3> n_Velocity =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> n_Frozen =
            new(true, readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> n_Hidden =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        // Real rigidbody angular velocity (rad/s), replicated so non-server peers can free-spin
        // the kinematic replica and the icosphere's tumble is visible everywhere, not just on the host.
        readonly NetworkVariable<Vector3> n_AngularVelocity =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        // Domain (team color) of the vessel that struck the ball LAST. Drives the ball tint, the
        // selective prism interaction (own color → pass through + shield, opposing → destroy + decay),
        // and the attacker domain for Prism.Damage. Blue = neutral (no strike yet) → smashes any team's mass.
        readonly NetworkVariable<Domains> n_LastHitDomain =
            new(Domains.Blue, readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        float _lastSnapshotTime;

        // Velocity the ball carries INTO each physics step (captured pre-simulation in
        // ServerFixedUpdate). OnCollisionEnter runs post-solver, so this is the pre-bounce velocity —
        // used to (a) restore motion on a same-color pass-through and (b) size the explosion by true
        // impact speed rather than the just-decayed/just-reflected value.
        Vector3 _velocityBeforePhysics;

        // Prism colliders the ball is currently ignoring (same-color pass-throughs). Cleared on every
        // domain flip / kickoff / hide so a prism that changes color — or a pooled collider reused by a
        // new prism — stops being ignored. Server-only (non-server peers never run ball collisions).
        readonly HashSet<Collider> _ignoredColliders = new();

        // Server-side velocity estimates for transform-driven vessels (root → last pos + velocity)
        readonly Dictionary<Transform, Vector3> _vesselLastPos = new();
        readonly Dictionary<Transform, Vector3> _vesselVelocity = new();

        // Per-vessel strike cooldown so one fly-through doesn't register as several hits
        readonly Dictionary<Transform, float> _lastStrikeTime = new();
        const float StrikeCooldownSeconds = 0.2f;

        // Prism-interaction broadcast flood guard + reusable query buffer (every peer)
        float _lastPrismExplodeTime = -999f;
        readonly List<Prism> _prismQueryBuffer = new(32);
        const string BallAttackerName = "Astro League";

        // Visuals
        Light ballLight;
        TrailRenderer trail;
        Renderer ballRenderer;
        MeshFilter meshFilter;
        Mesh _ballMesh; // generated icosphere, owned (destroyed in OnDestroy)
        MaterialPropertyBlock mpb;
        ParticleSystem auraParticles;
        ParticleSystem impactParticles;
        float flashTimer;
        float currentEmissionBoost = 1f;

        CustomCameraController cameraController;

        /// <summary>Server-only: raised when a vessel strikes the ball. Payload: striking vessel, hit intensity 0..1.</summary>
        public event Action<IVessel, float> OnStruckServer;

        public Vector3 Velocity => IsServer ? rb.linearVelocity : n_Velocity.Value;
        public bool IsFrozen => n_Frozen.Value;
        public bool IsHidden => n_Hidden.Value;
        /// <summary>Domain whose color the ball currently carries (Blue = neutral). Set by the last striker.</summary>
        public Domains LastHitDomain => n_LastHitDomain.Value;

        void Awake()
        {
            destroyToken = this.GetCancellationTokenOnDestroy();

            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = settings != null ? settings.ballMass : 3f;
            rb.linearDamping = 0f; // ZERO passive drag — the ball coasts at constant speed (see ServerFixedUpdate)
            // Keep angular damping low so spin imparted by off-center strikes persists (momentum
            // conserved), and lift the default 7 rad/s angular-velocity clamp so a hard off-center
            // smack reads as a real tumble on the faceted icosphere.
            rb.angularDamping = settings != null ? settings.ballAngularDamping : 0.05f;
            rb.maxAngularVelocity = settings != null ? settings.maxAngularSpeed : 40f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            sphereCol = GetComponent<SphereCollider>();
            sphereCol.material = new PhysicsMaterial("AstroLeagueBall")
            {
                bounciness = settings != null ? settings.ballBounciness : 0.98f,
                bounceCombine = PhysicsMaterialCombine.Maximum,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                dynamicFriction = 0f,
                staticFriction = 0f
            };

            spawnPosition = transform.position;
            _baseScale = transform.localScale;
            SetupVisuals();
        }

        /// <summary>
        /// Scale the ball (visual + collider) by the intensity factor on top of its authored
        /// base size. Runs on every peer (server physics + client rendering both need it).
        /// BallWorldRadius reads lossyScale, so the strike/eject maths track the new size.
        /// </summary>
        public void SetSizeScale(float factor)
        {
            transform.localScale = _baseScale * Mathf.Max(0.01f, factor);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                n_Position.Value = transform.position;
                ApplyFrozenPhysics(n_Frozen.Value);
            }
            else
            {
                // Non-server peers never simulate the ball — kinematic, replication-driven.
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = true;
                transform.position = n_Position.Value;
                n_Position.OnValueChanged += (_, _) => _lastSnapshotTime = Time.time;
                _lastSnapshotTime = Time.time;
            }

            n_Hidden.OnValueChanged += (_, hidden) => ApplyHiddenVisuals(hidden);
            ApplyHiddenVisuals(n_Hidden.Value);
        }

        void Start()
        {
            if (CameraManager.Instance != null)
                cameraController = CameraManager.Instance.GetActiveController() as CustomCameraController;
        }

        #region Visual construction

        void SetupVisuals()
        {
            mpb = new MaterialPropertyBlock();

            // Swap the authored Sphere mesh for a medium-poly faceted icosphere so the ball's
            // rotation is legible as it travels — each flat facet catches the fresnel rim
            // differently, making the spin readable instead of a uniform glowing ring. Mesh radius
            // matches the SphereCollider, so the visual hull tracks the physics hull at every
            // intensity scale (BallWorldRadius reads lossyScale).
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null)
            {
                int subdiv = settings != null ? settings.ballMeshSubdivisions : IcosphereMeshGenerator.DefaultSubdivisions;
                float meshRadius = sphereCol != null ? sphereCol.radius : 0.5f;
                _ballMesh = IcosphereMeshGenerator.Generate(subdiv, meshRadius, flatShaded: true);
                meshFilter.sharedMesh = _ballMesh;
            }

            ballRenderer = GetComponent<Renderer>();
            if (ballRenderer != null)
            {
                // Clone the prism fresnel material so the ball reads as 3D with a bright
                // view-dependent rim, exactly like trail prisms. One instance at startup;
                // per-frame color animation goes through the MaterialPropertyBlock, never .material.
                Material mat = null;
                if (prismMaterial != null)
                {
                    mat = new Material(prismMaterial);
                    _usesFresnel = true;
                }
                else
                {
                    var fresnelShader = Shader.Find("Shader Graphs/BlockGraph");
                    if (fresnelShader != null)
                    {
                        mat = new Material(fresnelShader);
                        mat.SetVector("_Spread", new Vector4(0.1f, 0.1f, 0.1f, 0f));
                        _usesFresnel = true;
                    }
                    else
                    {
                        // Last-resort fallback: lit sphere with emission.
                        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        mat.SetFloat("_Metallic", 0.9f);
                        mat.SetFloat("_Smoothness", 0.95f);
                        mat.SetColor(BaseColorId, primaryColor);
                        mat.EnableKeyword("_EMISSION");
                        _usesFresnel = false;
                    }
                }
                ballRenderer.sharedMaterial = mat;
            }

            ballLight = gameObject.AddComponent<Light>();
            ballLight.type = LightType.Point;
            ballLight.color = primaryColor;
            ballLight.range = settings != null ? settings.minLightRange : 25f;
            ballLight.intensity = baseLightIntensity;
            ballLight.shadows = LightShadows.None;

            trail = gameObject.AddComponent<TrailRenderer>();
            trail.time = 0.3f;
            trail.startWidth = settings != null ? settings.minTrailWidth : 0.6f;
            trail.endWidth = 0.1f;
            trail.numCapVertices = 4;
            var trailMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            trailMat.color = primaryColor;
            MakeTransparent(trailMat);
            trail.sharedMaterial = trailMat;
            trail.startColor = primaryColor;
            trail.endColor = new Color(secondaryColor.r, secondaryColor.g, secondaryColor.b, 0f);
            trail.minVertexDistance = 0.5f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.generateLightingData = false;

            auraParticles = CreateParticles("PayloadAura", burstOnly: false);
            impactParticles = CreateParticles("ImpactBurst", burstOnly: true);
        }

        static void MakeTransparent(Material mat)
        {
            mat.SetFloat("_Surface", 1);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.renderQueue = 3000;
        }

        ParticleSystem CreateParticles(string childName, bool burstOnly)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.playOnAwake = !burstOnly;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(primaryColor, secondaryColor);

            var emission = ps.emission;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;

            if (burstOnly)
            {
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(15f, 45f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
                main.maxParticles = 200;
                main.startColor = new ParticleSystem.MinMaxGradient(Color.white, primaryColor);
                emission.rateOverTime = 0f;
                shape.radius = 0.5f;
            }
            else
            {
                main.startLifetime = 0.8f;
                main.startSpeed = 2f;
                main.startSize = 0.6f;
                main.maxParticles = 60;
                emission.rateOverTime = 20f;
                shape.radius = 1.5f;

                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.orbitalX = 3f;
                vel.orbitalY = 2f;
                vel.orbitalZ = 1.5f;
                vel.radial = -1f;
            }

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(secondaryColor, 1f) },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            var psRenderer = go.GetComponent<ParticleSystemRenderer>();
            var psMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            psMat.SetFloat("_Surface", 1);
            psMat.SetInt("_Blend", 1); // Additive
            psMat.SetColor(BaseColorId, Color.white);
            psMat.renderQueue = 3100;
            psRenderer.sharedMaterial = psMat;
            psRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return ps;
        }

        #endregion

        #region Physics (server) + replication (clients)

        void FixedUpdate()
        {
            if (settings == null) return;

            if (!IsSpawned || IsServer)
                ServerFixedUpdate();
            else
                ClientFixedUpdate();
        }

        void ServerFixedUpdate()
        {
            SampleVesselVelocities();

            if (!n_Frozen.Value)
            {
                // ZERO friction: the ball coasts at constant speed between collisions. There is no
                // passive drag — the ONLY speed decay is the per-collision loss when the ball bounces
                // off an opposing-color prism (HandlePrismContact). Walls and same-color prisms are
                // lossless. Just cap the top speed so vessel strikes can't make it run away.
                if (rb.linearVelocity.sqrMagnitude > settings.maxSpeed * settings.maxSpeed)
                    rb.linearVelocity = rb.linearVelocity.normalized * settings.maxSpeed;

                // Snapshot the pre-simulation velocity; OnCollisionEnter (post-solver) reads it to
                // restore motion through same-color prisms and to size explosions by true impact speed.
                _velocityBeforePhysics = rb.linearVelocity;
            }

            if (IsSpawned)
            {
                n_Position.Value = transform.position;
                n_Velocity.Value = n_Frozen.Value ? Vector3.zero : rb.linearVelocity;
                n_AngularVelocity.Value = n_Frozen.Value ? Vector3.zero : rb.angularVelocity;
            }
        }

        void ClientFixedUpdate()
        {
            // Dead reckoning: project the last snapshot forward by its age, then blend.
            float age = Mathf.Clamp(Time.time - _lastSnapshotTime, 0f, 0.25f);
            Vector3 predicted = n_Position.Value + n_Velocity.Value * age;

            if ((predicted - transform.position).sqrMagnitude > settings.clientSnapDistance * settings.clientSnapDistance)
            {
                transform.position = predicted;
                if (trail != null) trail.Clear();
            }
            else
            {
                float t = 1f - Mathf.Exp(-settings.clientSmoothingRate * Time.fixedDeltaTime);
                transform.position = Vector3.Lerp(transform.position, predicted, t);
            }
        }

        /// <summary>
        /// Server-side velocity estimation for transform-driven vessels. One pass over the
        /// roster per physics tick (≤ a handful of vessels) — no per-collision allocation.
        /// </summary>
        void SampleVesselVelocities()
        {
            if (gameData == null) return;

            float dt = Time.fixedDeltaTime;
            for (int i = 0, n = gameData.Vessels.Count; i < n; i++)
            {
                var vessel = gameData.Vessels[i];
                if (vessel == null) continue;
                var root = vessel.Transform;
                if (root == null) continue;

                if (_vesselLastPos.TryGetValue(root, out var lastPos))
                    _vesselVelocity[root] = (root.position - lastPos) / dt;
                _vesselLastPos[root] = root.position;
            }
        }

        void OnCollisionEnter(Collision collision)
        {
            if (settings == null || n_Frozen.Value || n_Hidden.Value) return;
            if (IsSpawned && !IsServer) return; // Server-authoritative contact resolution
            if (collision.contactCount == 0) return;
            if (collision.collider == null) return;

            Vector3 contactPoint = collision.contacts[0].point;
            Vector3 contactNormal = collision.contacts[0].normal;

            // Vessel strikes are the energy INPUT (launch): they re-color the ball and re-energize it.
            var vessel = collision.collider.GetComponentInParent<IVessel>();
            if (vessel != null)
            {
                VesselStrike(vessel, contactPoint);
                return;
            }

            // Trail prism: the DOMAIN relationship decides bounce-and-destroy vs pass-and-shield.
            var prism = collision.collider.GetComponentInParent<Prism>();
            if (prism != null)
            {
                HandlePrismContact(collision.collider, prism, contactPoint, contactNormal);
                return;
            }

            // Arena wall (or other non-prism geometry): perfectly elastic — the solver already
            // reflected at bounciness 1, and we add NO decay and destroy nothing (requirement 4).
            HandleWallBounce(contactPoint, contactNormal);
        }

        /// <summary>
        /// Server: the ball contacted a trail prism. The ball's domain (the last striker's team)
        /// decides everything:
        ///   • SAME color as the ball (own trail) → PASS THROUGH: restore the pre-contact velocity so
        ///     no bounce/decay happens, ignore this collider for subsequent frames, and shield the
        ///     prisms via the broadcast. The single micro-bounce before the restore is negligible.
        ///   • OPPOSING color (or a NEUTRAL ball that has not been struck yet) → BOUNCE (the solver
        ///     already reflected at bounciness 1) + DESTROY the prisms via the broadcast + apply the
        ///     per-collision speed decay. Bouncing off opposing mass is the ONLY thing that slows the
        ///     ball (requirement 4) — the energy lost is, conceptually, what powers the explosion.
        /// </summary>
        void HandlePrismContact(Collider prismCollider, Prism prism, Vector3 contactPoint, Vector3 contactNormal)
        {
            Domains ballDomain = n_LastHitDomain.Value;
            bool same = ballDomain != Domains.Blue && prism.Domain == ballDomain;
            float impactSpeed = _velocityBeforePhysics.magnitude;

            if (same)
            {
                // Undo the solver's bounce — coast straight through own-color mass, losslessly.
                rb.linearVelocity = _velocityBeforePhysics;
                IgnorePrismCollider(prismCollider);
            }

            // Domain-aware radius interaction (shields same-color, destroys opposing) on every peer.
            EmitPrismInteraction(contactPoint, contactNormal, impactSpeed, ballDomain);

            // Speed decay fires ONLY on an opposing-color bounce (requirement 4).
            if (!same)
                rb.linearVelocity *= settings.collisionSpeedRetention;
        }

        /// <summary>Server: ignore future contacts with a passed-through prism collider (tracked so it can be cleared).</summary>
        void IgnorePrismCollider(Collider prismCollider)
        {
            if (prismCollider == null || sphereCol == null) return;
            if (_ignoredColliders.Add(prismCollider))
                Physics.IgnoreCollision(sphereCol, prismCollider, true);
        }

        /// <summary>
        /// Server: stop ignoring every prism collider we passed through. Called whenever the
        /// same/opposing relationship can change (domain flip on a strike, kickoff reset, hide), so a
        /// recolored prism — or a pooled collider reused by a new prism — collides normally again.
        /// (Same-color prisms are shielded on pass-through and rarely die-then-reuse mid-segment, and
        /// domain flips between Jade/Ruby strikes are frequent, so the stale window is tiny.)
        /// </summary>
        void ClearIgnoredColliders()
        {
            if (_ignoredColliders.Count == 0) return;
            foreach (var col in _ignoredColliders)
                if (col != null && sphereCol != null)
                    Physics.IgnoreCollision(sphereCol, col, false);
            _ignoredColliders.Clear();
        }

        /// <summary>
        /// Server: a wall (or other non-prism geometry) bounce. Perfectly elastic — the solver's
        /// reflection at bounciness 1 stands, NO decay, no prism interaction; just bounce juice.
        /// </summary>
        void HandleWallBounce(Vector3 contactPoint, Vector3 contactNormal)
        {
            float intensity = Mathf.Clamp01(_velocityBeforePhysics.magnitude / settings.maxSpeed);
            WallBounce_ClientRpc(contactPoint, contactNormal, intensity);
        }

        /// <summary>
        /// Server: broadcast a domain-aware, speed-scaled prism interaction to every peer so each one
        /// resolves it against its OWN local trail copies (prisms are per-peer GameObjects laid by
        /// VesselPrismController on every peer — not shared NetworkObjects, so a server-only resolution
        /// would desync). Each peer runs PrismSpatialIndex.QuerySphere and, per prism: shields it if it
        /// matches the ball's domain (own mass), else destroys it via the canonical animated
        /// Prism.Damage path (spatial-index release + VFX, mass conserved — the ball is the active
        /// force). Blast radius scales from prismDestroyRadius up to prismDestroyRadiusAtMaxSpeed.
        /// </summary>
        void EmitPrismInteraction(Vector3 contactPoint, Vector3 contactNormal, float speed, Domains ballDomain)
        {
            if (speed < settings.prismDestroyMinSpeed) return;

            float now = Time.time;
            if (now - _lastPrismExplodeTime < settings.prismDestroyCooldown) return;
            _lastPrismExplodeTime = now;

            float intensity = Mathf.Clamp01(speed / settings.maxSpeed);
            float radius = Mathf.Lerp(settings.prismDestroyRadius, settings.prismDestroyRadiusAtMaxSpeed, intensity);
            Vector3 impactVector = -contactNormal * speed; // scatter fragments in the ball's travel direction
            PrismInteraction_ClientRpc(contactPoint, contactNormal, impactVector, intensity, radius, (int)ballDomain);
        }

        /// <summary>
        /// Trigger-collider strike path. Serpent and Sparrow have NO non-trigger hull collider,
        /// so the ball never gets an OnCollisionEnter against them — without this they'd pass
        /// straight through the ball. Every vessel has at least a trigger collider, so detect
        /// strikes here too (the per-vessel cooldown dedups the double-fire on ships that have
        /// both). The strike model sets the ball velocity and ejects the ball clear of the hull,
        /// so the ball never clips even without a physical barrier.
        /// </summary>
        void OnTriggerEnter(Collider other)
        {
            if (settings == null || n_Frozen.Value || n_Hidden.Value) return;
            if (IsSpawned && !IsServer) return;
            if (other == null) return;

            var vessel = other.GetComponentInParent<IVessel>();
            if (vessel == null || vessel.Transform == null) return;

            // Approximate the contact as the point on the ball surface facing the vessel.
            Vector3 ballCenter = transform.position;
            Vector3 toVessel = vessel.Transform.position - ballCenter;
            Vector3 contactPoint = toVessel.sqrMagnitude > 0.0001f
                ? ballCenter + toVessel.normalized * BallWorldRadius()
                : ballCenter;

            VesselStrike(vessel, contactPoint);
        }

        float BallWorldRadius()
        {
            var s = transform.lossyScale;
            return sphereCol.radius * Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        }

        /// <summary>
        /// Guarantees the ball never overlaps the striking vessel's hull: if the ball center is
        /// closer than (ball radius + vesselClearRadius) to the vessel root, push it straight out
        /// to that distance. With the ≥1x launch speed this keeps the ball ahead of the vessel,
        /// so the vessel mesh can't clip through it — including the trigger-only ships that have
        /// no physical depenetration barrier. Server position is republished immediately so peers
        /// see the ejected position without waiting for the next tick.
        /// </summary>
        void EjectBallFromVessel(Transform vesselRoot)
        {
            float minClear = BallWorldRadius() + settings.vesselClearRadius;
            Vector3 away = transform.position - vesselRoot.position;
            float dist = away.magnitude;
            if (dist <= 0.001f || dist >= minClear) return;

            Vector3 cleared = vesselRoot.position + away * (minClear / dist);
            rb.position = cleared;          // physics-authoritative (server ball is non-kinematic)
            transform.position = cleared;   // immediate visual + the n_Position read below
            if (IsSpawned) n_Position.Value = cleared;
        }

        void VesselStrike(IVessel vessel, Vector3 contactPoint)
        {
            var root = vessel.Transform;
            float now = Time.time;
            if (_lastStrikeTime.TryGetValue(root, out var last) && now - last < StrikeCooldownSeconds)
                return;

            Vector3 strikerVelocity = ResolveStrikerVelocity(vessel);
            float strikerSpeed = strikerVelocity.magnitude;
            if (strikerSpeed < settings.minimumHitSpeed) return;

            _lastStrikeTime[root] = now;

            // Re-color the ball to the striker's domain (requirement 1). The same/opposing prism
            // relationship flips with it, so drop every same-color pass-through we were ignoring.
            Domains strikerDomain = vessel.VesselStatus != null ? vessel.VesselStatus.Domain : Domains.Blue;
            if (n_LastHitDomain.Value != strikerDomain)
            {
                n_LastHitDomain.Value = strikerDomain;
                ClearIgnoredColliders();
            }

            // Billiard deflection blended toward the striker's heading.
            Vector3 deflectionDir = (transform.position - contactPoint).normalized;
            Vector3 pushDir = strikerVelocity.normalized;
            Vector3 resultDir = Vector3.Slerp(deflectionDir, pushDir, settings.directionalBias).normalized;

            // Desired post-strike COM velocity (same tuned model as before): keep a little of the
            // inbound velocity, add the striker's speed × boost along the result direction, cap to max.
            Vector3 desiredVelocity = rb.linearVelocity * settings.velocityRetention
                                    + resultDir * (strikerSpeed * settings.hitBoostMultiplier);
            if (desiredVelocity.sqrMagnitude > settings.maxSpeed * settings.maxSpeed)
                desiredVelocity = desiredVelocity.normalized * settings.maxSpeed;

            // Impulse-based contact (requirement 6): instead of overwriting linearVelocity, set the
            // COM velocity exactly (preserving the tuned feel) AND inject the matching torque from the
            // OFF-CENTER contact, so the ball spins from real angular dynamics. This is the split form
            // of AddForceAtPosition(impulse, contactPoint) — direct linear keeps the result immediate
            // (AddForce* defers a tick), AddTorque ramps the spin in over the next tick (cosmetic).
            Vector3 oldVelocity = rb.linearVelocity;
            rb.linearVelocity = desiredVelocity;
            Vector3 impulse = rb.mass * (desiredVelocity - oldVelocity);
            Vector3 lever = contactPoint - rb.worldCenterOfMass;
            rb.AddTorque(Vector3.Cross(lever, impulse), ForceMode.Impulse); // clamped by rb.maxAngularVelocity

            float finalSpeed = desiredVelocity.magnitude;
            float intensity = Mathf.Clamp01(finalSpeed / settings.maxSpeed);
            // A strike is also a prism interaction at the contact, scaled by launch speed (no decay —
            // the strike IS the energy input). With the freshly-set domain it shields own-color mass
            // and clears opposing mass around the smack, on top of the burst + shake juice.
            EmitPrismInteraction(contactPoint, deflectionDir, finalSpeed, strikerDomain);

            if (finalSpeed > settings.hitstopSpeedThreshold && IsSoloSession())
                RunHitstopAsync().Forget();

            // Pop the ball clear of the vessel hull so the vessel never clips through it.
            EjectBallFromVessel(root);

            OnStruckServer?.Invoke(vessel, intensity);
        }

        /// <summary>
        /// Vessels move via transform.position (VesselTransformer), so rigidbody velocity is ~0
        /// and VesselStatus.Speed/Course only updates on the owning peer. The server's sampled
        /// transform delta is the one source that is correct for every vessel; Speed/Course is
        /// the fallback for the first tick before a sample exists (host's own vessel and AI).
        /// </summary>
        Vector3 ResolveStrikerVelocity(IVessel vessel)
        {
            if (vessel.Transform != null
                && _vesselVelocity.TryGetValue(vessel.Transform, out var sampled)
                && sampled.sqrMagnitude > 1f)
                return sampled;

            var vesselStatus = vessel.VesselStatus;
            if (vesselStatus != null && vesselStatus.Speed > 0.1f)
                return vesselStatus.Course * vesselStatus.Speed;

            return Vector3.zero;
        }

        bool IsSoloSession()
        {
            var nm = NetworkManager.Singleton;
            return nm == null || !nm.IsListening || nm.ConnectedClientsIds.Count <= 1;
        }

        #endregion

        #region Per-frame visuals (every peer)

        void Update()
        {
            if (settings == null || ballRenderer == null) return;

            // Non-server peers free-spin the kinematic replica from the replicated angular velocity
            // (the server rigidbody owns the real rotation, interpolated natively). Purely cosmetic —
            // no gameplay reads client rotation, so any dead-reckoned drift is invisible.
            if (IsSpawned && !IsServer && !n_Frozen.Value && !n_Hidden.Value)
            {
                Vector3 w = n_AngularVelocity.Value;
                float wMag = w.magnitude;
                if (wMag > 1e-4f)
                    transform.rotation = Quaternion.AngleAxis(wMag * Mathf.Rad2Deg * Time.deltaTime, w / wMag) * transform.rotation;
            }

            float speedRatio = Mathf.Clamp01(Velocity.magnitude / settings.speedForMaxVisuals);

            // Impact flash decay
            if (flashTimer > 0f)
            {
                flashTimer -= Time.deltaTime;
                float flashRatio = Mathf.Clamp01(flashTimer / settings.impactFlashDuration);
                currentEmissionBoost = Mathf.Lerp(1f, settings.impactFlashIntensity, flashRatio);
            }
            else
            {
                currentEmissionBoost = 1f;
            }

            // Color keys to the LAST-HIT domain (requirement 1): a Jade striker tints the ball Jade,
            // a Ruby striker Ruby. Before any strike the payload is neutral (Blue) and runs the
            // original three-way rainbow cycle so it reads as "unclaimed".
            Color emissionColor;
            Domains dom = n_LastHitDomain.Value;
            if (dom == Domains.Blue)
            {
                float phase = (Time.time * colorCycleSpeed) % 3f;
                emissionColor = phase < 1f
                    ? Color.Lerp(primaryColor, secondaryColor, phase)
                    : phase < 2f
                        ? Color.Lerp(secondaryColor, tertiaryColor, phase - 1f)
                        : Color.Lerp(tertiaryColor, primaryColor, phase - 2f);
            }
            else
            {
                emissionColor = DomainTint(dom);
            }

            float breath = 0.8f + 0.2f * Mathf.Sin(Time.time * 4f);
            float emissionIntensity = Mathf.Lerp(settings.minEmissionIntensity, settings.maxEmissionIntensity, speedRatio);
            float glow = emissionIntensity * breath * currentEmissionBoost;

            if (_usesFresnel)
            {
                // Drive the prism fresnel rim: bright HDR rim cycles + reacts to speed/flash,
                // dark base is a dim version of the same hue so the sphere reads as 3D. Cap the
                // flash boost so a thin rim doesn't blow out to pure white (full-surface emission
                // tolerates much higher HDR than a grazing-angle rim).
                float rim = emissionIntensity * breath * Mathf.Min(currentEmissionBoost, 4f);
                mpb.SetColor(BrightColorId, emissionColor * rim);
                mpb.SetColor(DarkColorId, emissionColor * 0.06f);
            }
            else
            {
                mpb.SetColor(EmissionColorId, emissionColor * glow);
            }
            ballRenderer.SetPropertyBlock(mpb);

            if (ballLight != null)
            {
                ballLight.color = emissionColor;
                float boost = currentEmissionBoost > 1f ? Mathf.Sqrt(currentEmissionBoost) : 1f;
                ballLight.intensity = baseLightIntensity * (1f + speedRatio * 2f) * breath * boost;
                ballLight.range = Mathf.Lerp(settings.minLightRange, settings.maxLightRange, speedRatio);
            }

            if (trail != null)
            {
                trail.startWidth = Mathf.Lerp(settings.minTrailWidth, settings.maxTrailWidth, speedRatio);
                trail.time = Mathf.Lerp(0.15f, 0.8f, speedRatio);
            }

            if (auraParticles != null)
            {
                var emission = auraParticles.emission;
                emission.rateOverTime = n_Hidden.Value ? 0f : Mathf.Lerp(8f, 45f, speedRatio);
            }
        }

        /// <summary>Base hue for a claimed (non-neutral) ball — matches the arena's per-domain palette.</summary>
        Color DomainTint(Domains d)
        {
            switch (d)
            {
                case Domains.Jade: return settings.jadeGoalColor;
                case Domains.Ruby: return settings.rubyGoalColor;
                case Domains.Gold: return new Color(1f, 0.82f, 0.2f, 1f);
                default: return primaryColor;
            }
        }

        #endregion

        #region Juice (replicated to every peer)

        [ClientRpc]
        void Detonate_ClientRpc(Vector3 position)
        {
            EmitBurst(position, Vector3.up, settings.goalParticleBurst);
            ShakeCamera(settings.goalShakeIntensity, settings.goalShakeDuration, position);
            HapticController.PlayHaptic(HapticType.MineCollision);
        }

        /// <summary>
        /// Every peer (host included): resolve a domain-aware prism interaction within the blast
        /// radius of the contact, on this peer's OWN local trail copies. Per prism: SHIELD it if it
        /// matches the ball's domain (own-color mass, requirement 2), else DESTROY it via the
        /// canonical animated Prism.Damage path (opposing-color, requirement 3 — spatial-index
        /// release + explosion VFX, mass conserved). A NEUTRAL ball (Blue, not yet struck) smashes
        /// every team's mass, as before. Position-deterministic (not instance-based) so it lands
        /// consistently on the host's, each client's, and the AI's independently-laid trail copies.
        /// </summary>
        [ClientRpc]
        void PrismInteraction_ClientRpc(Vector3 position, Vector3 normal, Vector3 impactVector, float intensity, float radius, int ballDomainInt)
        {
            var ballDomain = (Domains)ballDomainInt;
            bool ballClaimed = ballDomain != Domains.Blue;

            var index = PrismSpatialIndex.Instance;
            if (index != null)
            {
                index.QuerySphere(position, radius, _prismQueryBuffer);
                for (int i = 0, n = _prismQueryBuffer.Count; i < n; i++)
                {
                    var prism = _prismQueryBuffer[i];
                    if (prism == null || prism.destroyed) continue;

                    if (ballClaimed && prism.Domain == ballDomain)
                    {
                        // own-color: shield it (skip if already shielded so repeated broadcasts
                        // while passing through don't re-fire the shield audio/material).
                        if (!prism.prismProperties.IsShielded && !prism.prismProperties.IsSuperShielded)
                            prism.ActivateShield();
                    }
                    else
                    {
                        prism.Damage(impactVector, ballDomain, BallAttackerName); // opposing (or neutral ball): destroy
                    }
                }
            }

            // Bounce feedback (same channel as wall/vessel impacts), plus a haptic thunk.
            // Burst + shake scale with both the impact intensity and the blast radius.
            float radiusScale = Mathf.Clamp01(radius / Mathf.Max(1f, settings.prismDestroyRadiusAtMaxSpeed));
            TriggerFlash(intensity);
            EmitBurst(position, normal, (int)(settings.impactParticleBurst * Mathf.Max(0.4f, intensity) * (0.6f + radiusScale)));
            ShakeCamera(settings.strikeShakeIntensity * intensity * 0.6f, settings.strikeShakeDuration, position);
            HapticController.PlayHaptic(HapticType.ShipCollision);
        }

        /// <summary>
        /// Every peer: lightweight wall-bounce feedback (perfectly elastic — no prism interaction,
        /// no decay). Half the prism-impact burst/shake so a wall ricochet reads as a clean carom.
        /// </summary>
        [ClientRpc]
        void WallBounce_ClientRpc(Vector3 position, Vector3 normal, float intensity)
        {
            TriggerFlash(intensity * 0.6f);
            EmitBurst(position, normal, (int)(settings.impactParticleBurst * 0.5f * Mathf.Max(0.4f, intensity)));
            ShakeCamera(settings.strikeShakeIntensity * intensity * 0.35f, settings.strikeShakeDuration, position);
            HapticController.PlayHaptic(HapticType.ShipCollision);
        }

        void TriggerFlash(float intensity) =>
            flashTimer = settings.impactFlashDuration * Mathf.Max(0.3f, intensity);

        void EmitBurst(Vector3 position, Vector3 normal, int count)
        {
            if (impactParticles == null || count <= 0) return;
            impactParticles.transform.position = position;
            impactParticles.transform.forward = normal;
            impactParticles.Emit(count);
        }

        /// <summary>Shake the local camera, scaled down with distance from the impact.</summary>
        void ShakeCamera(float intensity, float duration, Vector3 impactPosition)
        {
            if (cameraController == null && CameraManager.Instance != null)
                cameraController = CameraManager.Instance.GetActiveController() as CustomCameraController;
            if (cameraController == null) return;

            float falloff = 1f;
            if (settings.shakeFalloffRadius > 0f)
            {
                float distance = Vector3.Distance(cameraController.transform.position, impactPosition);
                falloff = Mathf.Clamp01(1f - distance / settings.shakeFalloffRadius);
            }
            if (falloff <= 0f) return;

            cameraController.Shake(intensity * falloff, duration);
        }

        async UniTaskVoid RunHitstopAsync()
        {
            if (hitstopActive) return;
            hitstopActive = true;

            float baseFixedDelta = Time.fixedDeltaTime / Mathf.Max(Time.timeScale, 0.0001f);
            Time.timeScale = settings.hitstopTimeScale;
            Time.fixedDeltaTime = baseFixedDelta * settings.hitstopTimeScale;

            try
            {
                await UniTask.Delay(
                    TimeSpan.FromSeconds(settings.hitstopDuration),
                    ignoreTimeScale: true,
                    cancellationToken: destroyToken);
            }
            finally
            {
                // Restore to known constants, not captured values — a concurrent
                // celebration slow-mo must not be clobbered by a stale capture.
                Time.timeScale = 1f;
                Time.fixedDeltaTime = baseFixedDelta;
                hitstopActive = false;
            }
        }

        #endregion

        #region Match control API (server-only, driven by AstroLeagueController)

        /// <summary>Server: detonate at the goal mouth — burst + shake on every peer, then hide until kickoff.</summary>
        public void DetonateServer()
        {
            if (!IsServer) return;
            Detonate_ClientRpc(transform.position);
            SetHiddenServer(true);
        }

        /// <summary>Server: freeze the ball in place at center (kickoff count-in). Velocity is cleared.</summary>
        public void SetFrozenServer(bool frozen)
        {
            if (!IsServer) return;
            n_Frozen.Value = frozen;
            ApplyFrozenPhysics(frozen);
        }

        void ApplyFrozenPhysics(bool frozen)
        {
            if (frozen)
            {
                // Kinematic bodies only support speculative continuous detection.
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = true;
                transform.position = spawnPosition;
            }
            else
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                // Start the kickoff coast from rest — no stale linear/angular velocity carried
                // across the freeze (a kinematic body preserves its velocity fields).
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>Server: hide/show the ball (between goal detonation and kickoff respawn).</summary>
        public void SetHiddenServer(bool hidden)
        {
            if (!IsServer) return;
            n_Hidden.Value = hidden;
            if (hidden)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                ClearIgnoredColliders(); // ball is leaving play — drop any same-color pass-throughs
            }
            ApplyHiddenVisuals(hidden); // NetworkVariable callback covers remote peers; host applies inline
        }

        void ApplyHiddenVisuals(bool hidden)
        {
            if (ballRenderer != null) ballRenderer.enabled = !hidden;
            if (ballLight != null) ballLight.enabled = !hidden;
            if (trail != null)
            {
                trail.emitting = !hidden;
                if (hidden) trail.Clear();
            }
        }

        /// <summary>Server: respawn at center — visible, frozen, zero velocity, NEUTRAL color again.</summary>
        public void ResetToCenterServer()
        {
            if (!IsServer) return;
            SetFrozenServer(true);
            SetHiddenServer(false);
            transform.position = spawnPosition;
            transform.rotation = Quaternion.identity;
            n_Position.Value = spawnPosition;
            n_Velocity.Value = Vector3.zero;
            n_AngularVelocity.Value = Vector3.zero;
            // Fresh ball at kickoff: unclaimed until the first strike, and no stale pass-throughs.
            n_LastHitDomain.Value = Domains.Blue;
            ClearIgnoredColliders();
            if (trail != null) trail.Clear();
        }

        #endregion

        public override void OnDestroy()
        {
            base.OnDestroy();
            ClearIgnoredColliders();
            if (_ballMesh != null) Destroy(_ballMesh);
        }
    }
}
