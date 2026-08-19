using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
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
    /// the resulting velocity estimate for strikes - this works identically for the host's
    /// own vessel, replicated client vessels, and AI (VesselStatus.Speed/Course is only
    /// trustworthy on the owning peer, see ResolveStrikerVelocity).
    ///
    /// Impact juice (emission flash, burst particles, distance-scaled camera shake, haptics)
    /// plays on every peer via ClientRpc. Hitstop is solo-session-only - local timescale
    /// changes desync connected peers.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class AstroLeagueBall : NetworkBehaviour
    {
        [Header("Config")]
        [SerializeField] AstroLeagueSettingsSO settings;
        [SerializeField] GameDataSO gameData;

        // Strike audio - the mode had NO sound on a vessel hit at all, which is most of why
        // connecting with the ball read as nothing happening. Injected like the controller's;
        // null-guarded because this is a service reference, not a SOAP event channel.
        [Inject] AudioSystem audioSystem;

        [Header("Shield interaction")]
        [Tooltip("Bounce off an OPPOSING shielded prism (and still pop its shield) instead of " +
                 "passing through it. OFF for the Astro League match ball, which keeps the " +
                 "shipped pop-and-continue behaviour; ON for the Scarab's forged balls.")]
        [SerializeField] bool bounceOffShieldedPrisms;

        [Tooltip("Die on contact with a SUPER-shielded prism, popping its super-shield on the way " +
                 "out. MUST stay OFF for the Astro League match ball: the arena's 480-prism edge " +
                 "lining is super-shielded and is inset INSIDE the analytic boundary, so the ball " +
                 "reaches it before it reaches the wall — a lethal super-shield would kill the " +
                 "match ball on its first approach to any edge.")]
        [SerializeField] bool destroyedBySuperShielded;

        /// <summary>Every live ball, for systems that must find them without a per-frame
        /// FindObjectsByType (the Scarab's switch rings). Registered on enable, removed on
        /// disable, so it is exact and costs nothing to maintain.</summary>
        public static readonly List<AstroLeagueBall> Live = new();

        bool _dieAfterScan;

        // Server-side: while true, nothing re-colours the ball — not a strike, not a blast claim.
        // Both claim sites are server writes to n_LastHitDomain, so a server-local flag is the
        // whole mechanism; clients keep reading colour off the replicated variable as always.
        // ONE exception, and it is the exception on purpose: a strike delivered mid-JUKE (the
        // Scarab's committed dash) STEALS the ball — the deliberate skill move converts, the
        // casual bump never does, and the robbed owner can always dash it straight back.
        //
        // Set by ScarabBallForge at the MINT POINT, so it is a property of the Scarab's forge
        // rather than of any mode: every forged ball, everywhere, is permanently its maker's and
        // stealable only by a dash. No mode installs it and none can forget to.
        bool _ownershipLocked;

        // Server-side embed anchor: where the ball is pinned and which way is "out of the nucleus".
        Vector3 _embedOutward = Vector3.up;
        Vector3 _embedAnchor;

        // Server-side touch ledger (Scarab Scramble's arming gate + bank-shot juice; harmless
        // bookkeeping in Astro League, which never reads it). Domains.Blue = untouched since
        // launch/reset — a fresh forge still carries its maker's launch.
        /// <summary>Server: domain of the last vessel/blast to touch the ball (Blue = none since launch).</summary>
        public Domains LastTouchDomainServer { get; private set; } = Domains.Blue;
        /// <summary>Server: player name of the last vessel to touch the ball (empty = none / a blast).</summary>
        public string LastToucherNameServer { get; private set; } = string.Empty;
        /// <summary>Server: wall caroms since the last vessel/blast touch — the bank-shot count.</summary>
        public int WallBouncesSinceTouchServer { get; private set; }

        void RecordTouchServer(Domains domain, string toucherName)
        {
            LastTouchDomainServer = domain;
            LastToucherNameServer = toucherName ?? string.Empty;
            WallBouncesSinceTouchServer = 0;
        }

        void ResetTouchLedgerServer()
        {
            LastTouchDomainServer = Domains.Blue;
            LastToucherNameServer = string.Empty;
            WallBouncesSinceTouchServer = 0;
        }

        [Header("Visuals")]
        [Tooltip("The prism fresnel material (PrismMaterial.mat) - cloned at runtime so the ball " +
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
        // EMBEDDED IN THE NUCLEUS (the Scarab's seeding ability, SCARAB.md §4.6): the ball is stuck
        // in the nucleus surface waiting to be struck loose. Deliberately NOT expressed as n_Frozen:
        // every vessel-contact gate bails on frozen (a kickoff ball must ignore the ships stacked on
        // it), and an embedded ball's whole purpose is to BE struck. So it is its own state — physics
        // integration is skipped like frozen, contact is live like a normal ball.
        readonly NetworkVariable<bool> n_Embedded =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        // Size factor over the authored base scale (SetSizeScale). Replicated because a forged
        // ball is sized after its spawn payload is built — see SetSizeScale's doc note.
        readonly NetworkVariable<float> n_SizeScale =
            new(1f, readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        float _lastSnapshotTime;

        // Velocity the ball carries INTO each physics step (captured pre-simulation in
        // ServerFixedUpdate). OnCollisionEnter runs post-solver, so this is the pre-bounce velocity -
        // used to size the wall-bounce juice by true impact speed (not the just-reflected value).
        Vector3 _velocityBeforePhysics;

        // Server time of the last wall-bounce juice (camera shake / haptic / burst) - rate-limits it so
        // a frictionless ball bouncing or skimming the wall can't spam the camera shake (see HandleWallBounce).
        float _lastWallJuiceTime;

        // Court play boundary (the cell nucleus) - set by AstroLeagueArena.Build via SetBoundary. The
        // ball is contained by a server-side reflect off the boundary's walls (no collider) - flat
        // polytope faces BANK the ball (billiards/air-hockey), a sphere focuses it toward center. The
        // shape is chosen per intensity. null means "not set yet" (frozen showpiece pre-kickoff).
        AstroLeagueBoundary _boundary;

        // Server-side velocity estimates for transform-driven vessels (root → last pos + velocity)
        readonly Dictionary<Transform, Vector3> _vesselLastPos = new();
        readonly Dictionary<Transform, Vector3> _vesselVelocity = new();
        readonly List<Transform> _deadSampleKeys = new();

        // Per-vessel-root time of last strike - dedups the hull+trigger double-fire and paces dribble
        // taps (see VesselContact). Gated by settings.vesselStrikeCooldown.
        readonly Dictionary<Transform, float> _lastStrikeTime = new();

        // Strike POP (every peer): a fast scale pulse driven in Update. It rides a VISUAL CHILD
        // (see SetupVisuals) and never the root, because the root's lossyScale is the ball's
        // physical size - the SphereCollider, the goal-line threshold, the prism scan radius and
        // the depenetration clearance all read it, and a deforming hitbox would be a physics bug
        // wearing a juice costume. This is the impact read that survives the far end of a huge
        // court, where a particle burst is a couple of pixels.
        Transform _visual;
        float _popTimer;
        float _bloomTimer; // birth bloom countdown, armed once in Awake (continuity of existence)

        // Per-tick prism scan state (ProcessPrismInteractions, every peer): reusable query buffer, the
        // set of prisms seen this tick (for pruning), the opposing prisms whose shield we popped this
        // visit (protected from being eaten until they leave range), and last scan position (sweep).
        readonly List<Prism> _prismQueryBuffer = new(64);
        readonly HashSet<Prism> _scanInRange = new();
        readonly List<Prism> _shieldPoppedThisVisit = new(16);
        Vector3 _lastPrismScanPos;
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

        /// <summary>True while this ball is stuck in the nucleus surface awaiting a strike.</summary>
        public bool IsEmbeddedOnNucleus => n_Embedded.Value;

        /// <summary>Server: the outward (away-from-cell-centre) normal at this ball's embed point.</summary>
        public Vector3 EmbedOutwardServer => _embedOutward;

        /// <summary>
        /// Server: raised the instant an embedded ball is struck loose, carrying whether it went
        /// INWARD (into the nucleus — the court, where balls are of consequence) or OUTWARD (into the
        /// cytoplasm — where it lives on, bouncing off the nucleus from the outside). The ball resolves
        /// the direction because only it knows its own embed normal; <c>ScarabNucleusField</c> owns
        /// what the two directions MEAN, so policy never leaks into the payload.
        /// </summary>
        public static event System.Action<AstroLeagueBall, bool> OnNucleusReleasedServer;
        /// <summary>Domain whose color the ball currently carries (Blue = neutral). Set by the last striker.</summary>
        public Domains LastHitDomain => n_LastHitDomain.Value;

        void OnEnable()
        {
            if (!Live.Contains(this)) Live.Add(this);
        }

        void OnDisable()
        {
            Live.Remove(this);
        }

        void Awake()
        {
            destroyToken = this.GetCancellationTokenOnDestroy();

            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.mass = settings != null ? settings.ballMass : 3f;
            rb.linearDamping = 0f; // ZERO passive drag - the ball coasts at constant speed (see ServerFixedUpdate)
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

            // The ball NEVER physically collides with prisms - it passes through ALL of them and
            // resolves them by domain via a per-tick spatial scan (ProcessPrismInteractions), which is
            // reliable along the WHOLE path (prism colliders are LOD-culled away from vessels, and a
            // fast ball tunnels past tiny colliders). Excluding the TrailBlocks layer on the ball's own
            // collider stops the solver from ever bouncing/snagging it on a prism. It still bounces off
            // walls and vessels (other layers), so the elastic game stays intact.
            int trailBlocksLayer = LayerMask.NameToLayer("TrailBlocks");
            if (trailBlocksLayer >= 0)
                sphereCol.excludeLayers = 1 << trailBlocksLayer;

            spawnPosition = transform.position;
            _baseScale = transform.localScale;
            SetupVisuals();

            // Arm the birth bloom: a ball coming into existence grows in (continuity law).
            // Every peer Awakes its own copy — server, client replica, and no-network local
            // mints alike — so no RPC is needed; the scene showpiece blooms once at load,
            // behind the connecting veil, which is harmless and equally lawful.
            _bloomTimer = settings != null ? settings.spawnBloomSeconds : 0.55f;
        }

        /// <summary>
        /// Scale the ball (visual + collider) by the intensity factor on top of its authored
        /// base size. Runs on every peer (server physics + client rendering both need it).
        /// BallWorldRadius reads lossyScale, so the strike/eject maths track the new size.
        ///
        /// On the server the factor ALSO replicates through <see cref="n_SizeScale"/>: a
        /// runtime-forged ball (the Scarab's SPACE-scaled forge) is stamped AFTER
        /// NetworkObject.Spawn, so the spawn payload carries the prefab scale and, without
        /// the variable, every remote peer would render — and prism-scan with — the wrong
        /// radius forever. Astro League's per-peer intensity calls are unaffected: each peer
        /// applies the same value locally and the replicated echo is idempotent.
        /// </summary>
        public void SetSizeScale(float factor)
        {
            factor = Mathf.Max(0.01f, factor);
            transform.localScale = _baseScale * factor;
            if (IsSpawned && IsServer)
                n_SizeScale.Value = factor;
        }

        /// <summary>
        /// Dress a visual-only ghost of this ball for the goal replay: same icosphere mesh, same
        /// shared material with the CURRENT property-block tint (frozen at the goal moment - the
        /// scorer's domain color), same world scale, and a matching motion trail. No collider, no
        /// networking - <see cref="AstroLeagueGoalReplay"/> animates the ghost's transform locally.
        /// </summary>
        public void DressReplayGhost(MeshFilter ghostMesh, MeshRenderer ghostRenderer, TrailRenderer ghostTrail)
        {
            if (ghostMesh != null && meshFilter != null)
            {
                ghostMesh.sharedMesh = meshFilter.sharedMesh;
                ghostMesh.transform.localScale = transform.localScale;
            }

            if (ghostRenderer != null && ballRenderer != null)
            {
                ghostRenderer.sharedMaterial = ballRenderer.sharedMaterial;
                if (mpb != null) ghostRenderer.SetPropertyBlock(mpb);
                ghostRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ghostRenderer.receiveShadows = false;
            }

            if (ghostTrail != null && trail != null)
            {
                ghostTrail.time = 0.35f;
                ghostTrail.startWidth = trail.startWidth;
                ghostTrail.endWidth = trail.endWidth;
                ghostTrail.numCapVertices = trail.numCapVertices;
                ghostTrail.sharedMaterial = trail.sharedMaterial;
                ghostTrail.startColor = trail.startColor;
                ghostTrail.endColor = trail.endColor;
                ghostTrail.minVertexDistance = trail.minVertexDistance;
                ghostTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                ghostTrail.receiveShadows = false;
                ghostTrail.generateLightingData = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                n_Position.Value = transform.position;
                ApplyFrozenPhysics(n_Frozen.Value);
                if (n_Embedded.Value) ApplyEmbeddedPhysics(true);
            }
            else
            {
                // Non-server peers never simulate the ball - kinematic, replication-driven.
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = true;
                transform.position = n_Position.Value;
                n_Position.OnValueChanged += (_, _) => _lastSnapshotTime = Time.time;
                _lastSnapshotTime = Time.time;

                // Catch a size stamped before this peer spawned the replica (a forged ball is
                // sized right after Spawn; a late joiner sees only the variable).
                n_SizeScale.OnValueChanged += (_, factor) =>
                    transform.localScale = _baseScale * Mathf.Max(0.01f, factor);
                if (!Mathf.Approximately(n_SizeScale.Value, 1f))
                    transform.localScale = _baseScale * Mathf.Max(0.01f, n_SizeScale.Value);
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
            // rotation is legible as it travels - each flat facet catches the fresnel rim
            // differently, making the spin readable instead of a uniform glowing ring. Mesh radius
            // matches the SphereCollider, so the visual hull tracks the physics hull at every
            // intensity scale (BallWorldRadius reads lossyScale).
            // The mesh lives on a VISUAL CHILD so the impact pop has something to deform that is
            // not the physics body. The authored root renderer (if any) is stood down rather than
            // removed, so a scene that still carries one can't double-draw the ball.
            var rootRenderer = GetComponent<MeshRenderer>();
            if (rootRenderer != null) rootRenderer.enabled = false;

            var visualGo = new GameObject("BallVisual");
            _visual = visualGo.transform;
            _visual.SetParent(transform, false);
            meshFilter = visualGo.AddComponent<MeshFilter>();
            var visualRenderer = visualGo.AddComponent<MeshRenderer>();
            visualRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            visualRenderer.receiveShadows = false;

            {
                int subdiv = settings != null ? settings.ballMeshSubdivisions : IcosphereMeshGenerator.DefaultSubdivisions;
                float meshRadius = sphereCol != null ? sphereCol.radius : 0.5f;
                _ballMesh = IcosphereMeshGenerator.Generate(subdiv, meshRadius, flatShaded: true);
                meshFilter.sharedMesh = _ballMesh;
            }

            ballRenderer = visualRenderer;
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

            if (n_Embedded.Value)
            {
                // Pinned: hold the anchor exactly. No integration, no containment, no drag — an
                // embedded ball is scenery with a hitbox until somebody knocks it loose.
                transform.position = _embedAnchor;
            }
            else if (!n_Frozen.Value)
            {
                // Cap the top speed so strikes can't make the ball run away.
                if (rb.linearVelocity.sqrMagnitude > settings.maxSpeed * settings.maxSpeed)
                    rb.linearVelocity = rb.linearVelocity.normalized * settings.maxSpeed;

                // The ball SETTLES. It used to be perfectly frictionless with perfectly elastic
                // walls, so an untouched ball ricocheted around the court forever - the game read as
                // pong. Two dials do it, both authored on the settings asset: wallRestitution takes
                // energy out of every carom (below), and this exponential drag bleeds the coast so a
                // ball nobody strikes comes to rest and becomes a thing players contest rather than
                // dodge. Frame-rate independent, and it composes with the prism-mass drag rather than
                // replacing it. Below restSpeed the remainder is snapped out, or the asymptote leaves
                // the ball creeping forever at an invisible speed.
                if (settings.ballDrag > 0f)
                    rb.linearVelocity *= Mathf.Exp(-EffectiveDrag() * Time.fixedDeltaTime);
                if (rb.linearVelocity.sqrMagnitude < settings.ballRestSpeed * settings.ballRestSpeed)
                    rb.linearVelocity = Vector3.zero;

                // First-class per-tick prism resolution - shield own / eat+slow opposing / unshield
                // shielded. The server applies the eaten-mass speed drag here, before replication.
                ProcessPrismInteractions();

                // Snapshot the pre-simulation velocity; the wall-bounce juice (OnCollisionEnter,
                // post-solver) reads it for the true impact speed.
                _velocityBeforePhysics = rb.linearVelocity;

                // Spherical boundary: bounce the ball off the inner surface of the nucleus sphere.
                ContainWithinBoundary();
            }

            if (IsSpawned)
            {
                n_Position.Value = transform.position;
                bool still = n_Frozen.Value || n_Embedded.Value;
                n_Velocity.Value = still ? Vector3.zero : rb.linearVelocity;
                n_AngularVelocity.Value = still ? Vector3.zero : rb.angularVelocity;
            }
        }

        /// <summary>
        /// The coast drag for where the ball currently IS: authored `ballDrag` inside the cell's
        /// nucleus, ramping linearly up to `ballDrag × outsideNucleusDragMultiplier` once it is a
        /// full `outsideNucleusDragFalloff` beyond the nucleus surface.
        ///
        /// This is a SOFT boundary and is deliberately the only kind the payload gets: a ball that
        /// leaves the pitch is never teleported back, culled, or bounced off an invisible wall —
        /// the hypersea simply gets thicker, and the ball settles instead of sailing away forever.
        /// A mode whose court IS the nucleus (Astro League) reflects the ball at that same radius,
        /// so the ramp never engages there and the mode's feel is untouched; it exists for a ball
        /// that got out, and for the Scarab's forged balls in a cell that has no court at all.
        ///
        /// NUCLEUS SIZE COMES FROM `NucleusVisualWorldRadius`, NOT `NucleusWorldRadius`. Astro
        /// League sets `Cell.NucleusIsControlZone = false` because it borrowed the nucleus as play
        /// geometry, and that collapses the CONTROL radius to zero — so the obvious read reports 0
        /// for the very mode this is about and the whole world would count as "outside".
        /// </summary>
        float EffectiveDrag()
        {
            float baseDrag = settings.ballDrag;
            if (settings.outsideNucleusDragMultiplier <= 1f) return baseDrag;

            var cell = ResolveCell();
            if (cell == null) return baseDrag;

            float nucleus = cell.NucleusVisualWorldRadius;
            if (nucleus <= 0f) return baseDrag;      // no nucleus: nothing to be outside of

            float distance = Vector3.Distance(transform.position, cell.transform.position);
            float outside01 = Mathf.Clamp01(
                (distance - nucleus) / Mathf.Max(1f, settings.outsideNucleusDragFalloff));

            return baseDrag * Mathf.Lerp(1f, settings.outsideNucleusDragMultiplier, outside01);
        }

        // Cached because Cell's finders are O(cells-in-scene) and documented as lifecycle-time
        // calls, not per-tick ones. Re-resolved only when the reference goes null - which covers
        // both "the ball spawned before the cell finished initializing" and a Cell Selector swap
        // destroying the old one.
        Cell _cell;

        Cell ResolveCell()
        {
            if (_cell != null) return _cell;
            _cell = Cell.FindCellContaining(transform.position)
                    ?? Cell.FindNearestActiveCell(transform.position);
            return _cell;
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

            // Clients resolve prisms locally too (per-peer trail copies) so each peer sees the ball
            // shield/clear the trail it sees; the server's eaten-mass drag arrives via replicated velocity.
            ProcessPrismInteractions();
        }

        /// <summary>
        /// Server-side velocity estimation for transform-driven vessels. One pass over the
        /// roster per physics tick (≤ a handful of vessels) - no per-collision allocation.
        /// </summary>
        void SampleVesselVelocities()
        {
            if (gameData == null) return;

            float dt = Time.fixedDeltaTime;
            for (int i = 0, n = gameData.Vessels.Count; i < n; i++)
            {
                var root = LiveTransform(gameData.Vessels[i]);
                if (root == null) continue;

                if (_vesselLastPos.TryGetValue(root, out var lastPos))
                    _vesselVelocity[root] = (root.position - lastPos) / dt;
                _vesselLastPos[root] = root.position;
            }

            PruneDeadVesselSamples();
        }

        /// <summary>The roster is `List&lt;IVessel&gt;`, and `==` on an INTERFACE reference never
        /// reaches UnityEngine.Object's overload — so a destroyed VesselController sails through a
        /// plain null check and throws on the first member access. <see cref="VesselLiveness"/> is
        /// the shared guard; a stale entry is rare now (VesselController leaves the roster in
        /// OnDestroy) but a vessel can be destroyed at any point in a frame, so the roster is never
        /// something to walk unguarded.</summary>
        static Transform LiveTransform(IVessel vessel) => vessel.LiveTransform();

        /// <summary>Drop sample entries whose transform has been destroyed. Keyed by Transform, so
        /// a swap-heavy session (the freestyle vessel changer) would otherwise grow these
        /// dictionaries for the ball's whole life. Only runs when they have outgrown the roster,
        /// so the steady state costs one integer compare.</summary>
        void PruneDeadVesselSamples()
        {
            if (_vesselLastPos.Count <= gameData.Vessels.Count) return;

            _deadSampleKeys.Clear();
            foreach (var key in _vesselLastPos.Keys)
                if (!key) _deadSampleKeys.Add(key);

            for (int i = 0; i < _deadSampleKeys.Count; i++)
            {
                _vesselLastPos.Remove(_deadSampleKeys[i]);
                _vesselVelocity.Remove(_deadSampleKeys[i]);
            }
            _deadSampleKeys.Clear();
        }

        void OnCollisionEnter(Collision collision)
        {
            if (settings == null || n_Frozen.Value || n_Hidden.Value) return;
            if (IsSpawned && !IsServer) return; // Server-authoritative contact resolution
            if (collision.contactCount == 0) return;
            if (collision.collider == null) return;

            Vector3 contactPoint = collision.contacts[0].point;
            Vector3 contactNormal = collision.contacts[0].normal;

            // Vessel: momentum-conserving elastic bounce off the moving hull + anti-clip depenetration.
            var vessel = collision.collider.GetComponentInParent<IVessel>();
            if (vessel != null)
            {
                VesselContact(vessel, contactPoint, collision.collider);
                return;
            }

            // Anything else is an arena wall - the ball can't collide with prisms at all (excludeLayers
            // in Awake), and resolves them by a per-tick spatial scan instead. Perfectly elastic carom:
            // the solver already reflected at bounciness 1, no decay.
            HandleWallBounce(contactPoint, contactNormal);
        }

        /// <summary>
        /// Server: a vessel is STILL overlapping the ball (physics hull). Re-runs VesselContact every
        /// physics tick so the ball is depenetrated out of the hull continuously - the ball can never
        /// clip even if the pilot keeps driving into it - and a re-hit lands as soon as the per-vessel
        /// strike cooldown allows (dribbling). Prisms/walls do nothing on Stay (the ball passes through
        /// prisms and bounces cleanly off walls on Enter).
        /// </summary>
        void OnCollisionStay(Collision collision)
        {
            if (settings == null || n_Frozen.Value || n_Hidden.Value) return;
            if (IsSpawned && !IsServer) return;
            if (collision.collider == null || collision.contactCount == 0) return;

            var vessel = collision.collider.GetComponentInParent<IVessel>();
            if (vessel != null)
                VesselContact(vessel, collision.contacts[0].point, collision.collider);
        }

        /// <summary>
        /// FIRST-CLASS per-tick prism resolution - the ball is treated like a player. Every physics
        /// tick (on EVERY peer) it sweeps `PrismSpatialIndex.QuerySphere` over the segment it just
        /// travelled and resolves the prisms it overlaps by domain, INDEPENDENT of physics colliders
        /// (the ball excludes the TrailBlocks layer entirely, and prism colliders are LOD-culled away
        /// from vessels - neither is reliable for a fast ball). This is what makes the ball
        /// clear/shield trail consistently along its WHOLE path, not just near vessels:
        ///   • SAME color (own trail)              → SHIELD it (if not already). No speed change.
        ///   • OPPOSING + UNSHIELDED (or a NEUTRAL → EAT it: destroy + slow the ball by the prism's
        ///     ball, which has no color yet)           mass (speed ×= M/(M + k·volume), direction kept).
        ///                                             The ONLY thing that slows the ball.
        ///   • OPPOSING + SHIELDED                 → UNSHIELD it and LEAVE it standing this visit (the
        ///                                             shield absorbs the pass); a later visit eats it.
        ///   • SUPER-SHIELDED (any domain)         → UNTOUCHED. Invulnerable structure (the arena's
        ///                                             edge lining) - never popped, never eaten, no cost.
        /// Prisms are per-peer GameObjects, so each peer resolves its OWN local copies (position-
        /// deterministic). Only the SERVER applies the speed drag; clients get the slowed velocity via
        /// replication and just mirror the shield/destroy on their copies.
        /// </summary>
        void ProcessPrismInteractions()
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null || n_Frozen.Value || n_Hidden.Value)
            {
                _shieldPoppedThisVisit.Clear();
                _lastPrismScanPos = transform.position;
                return;
            }

            bool isServer = !IsSpawned || IsServer;
            Domains ballDomain = n_LastHitDomain.Value;
            bool ballNeutral = ballDomain == Domains.Blue;
            Vector3 ballVel = isServer ? rb.linearVelocity : n_Velocity.Value;

            float radius = BallWorldRadius() * Mathf.Max(1f, settings.prismScanRadiusFactor);
            Vector3 to = transform.position;
            Vector3 from = _lastPrismScanPos;
            _lastPrismScanPos = to;

            // Sweep the segment travelled this tick so a fast ball doesn't skip prisms between samples.
            float moved = Vector3.Distance(from, to);
            int samples = Mathf.Clamp(Mathf.CeilToInt(moved / Mathf.Max(0.5f, radius)), 1, 8);

            _scanInRange.Clear();
            float eatenMass = 0f;

            for (int s = 0; s < samples; s++)
            {
                Vector3 c = samples == 1 ? to : Vector3.Lerp(from, to, (s + 1) / (float)samples);
                index.QuerySphere(c, radius, _prismQueryBuffer);
                for (int i = 0, n = _prismQueryBuffer.Count; i < n; i++)
                {
                    var prism = _prismQueryBuffer[i];
                    if (prism == null || prism.destroyed) continue;

                    // SUPER-shielded prisms are fully invulnerable structure (Prism.Damage/Consume
                    // no-op on them - e.g. the arena's edge lining): the ball passes through
                    // untouched regardless of domain - never unshielded, never eaten, no speed cost.
                    // UNLESS this ball is authored to die on them (the Scarab's forged balls): then
                    // the super-shield is popped and the ball is spent. Both happen - the structure
                    // is downgraded, and the ball paid for it.
                    if (prism.prismProperties.IsSuperShielded)
                    {
                        if (!destroyedBySuperShielded) continue;
                        prism.DeactivateShields();
                        _dieAfterScan = true;   // never despawn mid-scan; drained below
                        continue;
                    }

                    _scanInRange.Add(prism);

                    bool same = !ballNeutral && prism.Domain == ballDomain;
                    bool shielded = prism.prismProperties.IsShielded;

                    if (same)
                    {
                        if (!shielded) prism.ActivateShield();
                    }
                    else if (shielded)
                    {
                        // Opposing + shielded: pop the shield and LEAVE the prism this visit.
                        prism.DeactivateShields();
                        if (!_shieldPoppedThisVisit.Contains(prism)) _shieldPoppedThisVisit.Add(prism);

                        // ...and, for balls authored to do so, CAROM off it. The shield is spent
                        // either way; the difference is whether the armour also turned the shot.
                        // Server-only: the reflection is a velocity write, and clients mirror it
                        // through n_Velocity like every other ball impulse.
                        if (bounceOffShieldedPrisms && isServer)
                            ReflectOffPrism(prism);
                    }
                    else
                    {
                        // Opposing + unshielded: eat it - unless we just popped its shield THIS visit.
                        //
                        // A DANGER prism arrives here too, and that is deliberate: danger is
                        // mutually exclusive with BOTH shield tiers (PrismStateManager.MakeDangerous
                        // clears them), so it can only ever be "unshielded", and nothing below
                        // reads the tier. A danger prism and a plain prism of the same volume
                        // therefore cost the ball exactly the same speed. Danger is a punishment
                        // for a PILOT (the slow, the debuff, the boost reset), not for the
                        // payload - a ball that braked harder on danger mass would hand the
                        // defending side a wall it could build out of a hazard. Do not add a
                        // per-tier multiplier to the drag.
                        if (_shieldPoppedThisVisit.Contains(prism)) continue;
                        eatenMass += Mathf.Max(0f, prism.CurrentVolume);
                        prism.Damage(ballVel, ballDomain, BallAttackerName);
                    }
                }
            }

            // Drop "just-popped" protection for prisms that left range, so a later visit can eat them.
            for (int i = _shieldPoppedThisVisit.Count - 1; i >= 0; i--)
            {
                var p = _shieldPoppedThisVisit[i];
                if (p == null || p.destroyed || !_scanInRange.Contains(p))
                {
                    int lastIdx = _shieldPoppedThisVisit.Count - 1;
                    _shieldPoppedThisVisit[i] = _shieldPoppedThisVisit[lastIdx];
                    _shieldPoppedThisVisit.RemoveAt(lastIdx);
                }
            }

            // A ball spent on super-shielded structure. Drained AFTER the scan so nothing is
            // despawned while the query buffer is still being walked.
            if (_dieAfterScan)
            {
                _dieAfterScan = false;
                if (isServer) ExpireServer();
                return;
            }

            if (eatenMass <= 0f) return;

            // Server slows the ball by the eaten mass (direction preserved); clients mirror via velocity.
            if (isServer)
            {
                float prismMass = settings.prismDragMassScale * eatenMass;
                rb.linearVelocity *= rb.mass / Mathf.Max(0.0001f, rb.mass + prismMass);
            }
            TriggerFlash(0.6f); // local "chomp" feedback on every peer that ate mass
        }

        /// <summary>
        /// Server: carom off a prism. The ball has no physics contact with prisms at all (their
        /// layer is excluded from its collider — see Awake), so the normal is derived
        /// geometrically: prism centre → ball centre, which for a sphere-vs-box at contact range
        /// is the face normal to within the box's corner rounding. Reflects only the INTO-prism
        /// component, so a glancing pass is barely turned and a head-on one comes straight back.
        ///
        /// This is a pure REDIRECT: at `prismCaromRestitution` 1 the reflection is an exact
        /// mirror, so |v| is unchanged and only the heading turns. That is what armour is
        /// supposed to buy — a shielded prism costs the SHIELD and the ball's line, never its
        /// momentum, and the prism itself is left standing (the caller pops the shield and adds
        /// the prism to `_shieldPoppedThisVisit`, so it is not eaten on the way past either). It
        /// used to reuse `wallRestitution` (0.72), which quietly bled 28% of the approach speed
        /// out of every deflection and made a shielded wall a brake as well as a bumper.
        /// </summary>
        void ReflectOffPrism(Prism prism)
        {
            Vector3 n = transform.position - prism.transform.position;
            if (n.sqrMagnitude < 1e-6f) return;
            n.Normalize();

            Vector3 v = rb.linearVelocity;
            float into = Vector3.Dot(v, n);
            if (into >= 0f) return;   // already leaving — never "bounce" a ball outward twice

            float e = settings != null ? settings.prismCaromRestitution : 1f;
            rb.linearVelocity = v - (1f + e) * into * n;

            // Nudge clear so the next tick's scan doesn't re-reflect on the same prism.
            transform.position += n * (BallWorldRadius() * 0.5f);
            HandleWallBounce(prism.transform.position, n);
        }

        /// <summary>Server: this ball is spent — detonate for the visual and remove it. Used by
        /// balls that die on super-shielded structure; a scene-placed match ball is only ever
        /// hidden, never despawned, so the NetworkObject despawn is guarded on being spawned.</summary>
        void ExpireServer()
        {
            if (!IsServer) return;
            DetonateServer();                       // burst + hide (continuity: it does not pop out)
            DespawnIfForged();
        }

        /// <summary>Server: retire a FORGED ball. A scene-placed match ball is only ever hidden, so it
        /// can be reset to centre for the next kickoff.</summary>
        void DespawnIfForged()
        {
            if (IsSpawned && NetworkObject != null && !NetworkObject.IsSceneObject.GetValueOrDefault(false))
                NetworkObject.Despawn(true);
        }

        /// <summary>
        /// Server: a wall bounce. Perfectly elastic (the reflection stands, no decay); just bounce juice.
        /// Intensity is the PERPENDICULAR (into-wall) speed, NOT the full speed: a frictionless ball
        /// skimming tangentially along a curved wall pokes through a sliver every tick, so full-speed
        /// intensity fired the camera shake + haptic CONTINUOUSLY as the ball orbited the wall - a
        /// persistent ~25 Hz jitter. A glancing skim now reads as ~0; only a real perpendicular slam
        /// shakes, and a cooldown stops even repeated hard bounces from spamming.
        /// </summary>
        void HandleWallBounce(Vector3 contactPoint, Vector3 contactNormal)
        {
            float perpSpeed = Mathf.Abs(Vector3.Dot(_velocityBeforePhysics, contactNormal));
            float intensity = Mathf.Clamp01(perpSpeed / settings.maxSpeed);

            if (intensity < settings.wallJuiceMinIntensity) return; // glancing skim - no juice
            if (Time.time - _lastWallJuiceTime < settings.wallJuiceCooldown) return;
            _lastWallJuiceTime = Time.time;

            WallBounce_ClientRpc(contactPoint, contactNormal, intensity);
        }

        /// <summary>
        /// Set the court play boundary (the cell nucleus). Called by <c>AstroLeagueArena.Build</c> once
        /// the intensity scale + shape are known. The ball bounces off its walls (see
        /// <see cref="ContainWithinBoundary"/>); a box/prism BANKS the ball off flat faces, a sphere
        /// focuses it. This replaced the six BoxCollider arena walls (and now the single sphere too).
        /// </summary>
        public void SetBoundary(AstroLeagueBoundary boundary)
        {
            _boundary = boundary;
        }

        /// <summary>
        /// Server: keep the ball inside the court by reflecting its velocity off the boundary walls and
        /// clamping its position (no collider, no decay) - flat polytope faces preserve the wall-parallel
        /// component (the bank), curved shapes reflect radially. Runs once per server tick after the
        /// speed cap. Fires the shared wall juice at the contact point on a real bounce.
        /// </summary>
        void ContainWithinBoundary()
        {
            if (_boundary == null) return; // not configured yet (frozen showpiece pre-kickoff)

            Vector3 pos = rb.position;
            Vector3 vel = rb.linearVelocity;
            // wallRestitution, not ballBounciness: a carom LOSES energy (that is what stops the ball
            // pinballing forever), while a vessel strike stays fully elastic so the sword still fires it.
            if (!_boundary.Contain(ref pos, ref vel, BallWorldRadius(), settings.wallRestitution,
                    out Vector3 contactPoint, out Vector3 contactNormal))
                return;

            rb.position = pos;
            rb.linearVelocity = vel;
            WallBouncesSinceTouchServer++; // every real carom counts toward a bank shot
            HandleWallBounce(contactPoint, contactNormal);
        }

        /// <summary>
        /// Trigger-collider vessel path (Enter). Serpent and Sparrow have NO non-trigger hull collider,
        /// so the ball never gets an OnCollisionEnter against them - without this they'd pass straight
        /// through. Every vessel has at least a trigger collider. The per-vessel strike cooldown dedups
        /// the double-fire on ships that have both a hull and a trigger.
        /// </summary>
        void OnTriggerEnter(Collider other) => HandleVesselTrigger(other);

        /// <summary>
        /// Trigger-collider vessel path (Stay): keep depenetrating + allow re-hits every frame the
        /// vessel overlaps, so trigger-only ships (no physics depenetration) can never clip the ball.
        /// </summary>
        void OnTriggerStay(Collider other) => HandleVesselTrigger(other);

        /// <summary>
        /// True while the striking vessel is inside a committed juke dash — the Scarab's
        /// side-shove skill move. Resolved off the vessel root's own controller; a vessel with
        /// no juke (every other hull) simply never steals. Strikes are cooldown-paced, so the
        /// component walk costs nothing measurable.
        /// </summary>
        static bool IsJukeStrike(IVessel vessel)
        {
            var t = vessel?.Transform;
            return t != null
                   && t.TryGetComponent(out ScarabJukeController juke)
                   && juke.IsJukeStrikeWindowOpen;
        }

        void HandleVesselTrigger(Collider other)
        {
            if (settings == null || n_Frozen.Value || n_Hidden.Value) return;
            if (IsSpawned && !IsServer) return;
            if (other == null) return;

            var vessel = other.GetComponentInParent<IVessel>();
            if (vessel == null || vessel.Transform == null) return;

            // Approximate the contact as the point on the ball surface facing the vessel. A blade
            // contact overrides this inside VesselContact (the sword's trigger is a capsule tens of
            // units long, so "the direction of the vessel root" is nowhere near where it touched).
            Vector3 ballCenter = transform.position;
            Vector3 toVessel = vessel.Transform.position - ballCenter;
            Vector3 contactPoint = toVessel.sqrMagnitude > 0.0001f
                ? ballCenter + toVessel.normalized * BallWorldRadius()
                : ballCenter;

            VesselContact(vessel, contactPoint, other);
        }

        /// <summary>
        /// The swing model for a SKIMMER THAT MOVES relative to its vessel, if this contact came
        /// through one - the Rhino's sword. Returns null for a fixed skimmer, a hull collider, or a
        /// blade whose model has not sampled a frame yet, in which case the caller keeps the
        /// vessel-root behaviour unchanged.
        /// </summary>
        SkimmerSwingKinematics ResolveBlade(Collider hitCollider)
        {
            if (hitCollider == null || settings == null || !settings.bladeAwareStrikes) return null;
            var skimmer = hitCollider.GetComponentInParent<Skimmer>();
            var swing = skimmer != null ? skimmer.SwingKinematics : null;
            return swing != null && swing.IsReady ? swing : null;
        }

        /// <summary>
        /// Server: unified vessel↔ball contact (from both collider paths, Enter AND Stay), layered so
        /// the ball can NEVER clip a vessel and ALWAYS bounces off one:
        ///   1. Anti-clip - ALWAYS depenetrate the ball out of the hull (EjectBallFromVessel only acts
        ///      while overlapping), every contact frame. The hull can't pass through the ball even if
        ///      the pilot keeps driving in, and even for trigger-only ships with no physics depenetration.
        ///   2. Elastic bounce - on every frame the ball is moving INTO the vessel (approach &lt; 0), it
        ///      bounces off (momentum-conserving moving-paddle reflection) + re-colors + spins. This is
        ///      self-limiting (once it bounces away it stops approaching) and self-deduping (a second
        ///      collider path the same frame sees the ball already separating), so a stationary or
        ///      trigger-only ship still cleanly reflects the ball instead of letting it stick.
        ///   3. Deliberate-strike extras - the arcade pop, vessel recoil (it bounces off too), and
        ///      hitstop are rate-limited per vessel by vesselStrikeCooldown (and gated on minimumHitSpeed)
        ///      so a fast committed hit pops + recoils while continuous dribble contact doesn't spam RPCs.
        /// </summary>
        void VesselContact(IVessel vessel, Vector3 contactPoint, Collider hitCollider = null)
        {
            var root = vessel.Transform;
            if (root == null) return;

            // ── Blade contact (the Rhino's sword) ───────────────────────────────────────────
            // A swinging skimmer is a rigid SEGMENT, so neither "the vessel's position" nor "the
            // vessel's speed" describes the hit: the tip can be 60 units from the hull and moving
            // many times faster. Resolve the contact ON the blade and take that point's true
            // velocity from the same model the prism impact path uses (SkimmerSwingKinematics /
            // PrismEffectHelper.ContactVelocity). Everything downstream - the bounce normal, the
            // arcade pop's aim, the recoil, the feedback intensity - then describes the swing.
            var blade = ResolveBlade(hitCollider);
            float bladeT = 0f;
            Vector3 strikerVelocity;

            // ── A plain SKIM FIELD never strikes the ball ──────────────────────────────────
            // The blade branch below already refuses the Rhino's skim SPHERE for this exact
            // reason ("worse than the vessel-root behaviour it replaced"), but the refusal only
            // covered swords: every other vessel's skimmer is also parented under the vessel, so
            // GetComponentInParent<IVessel> found it and the ball was being batted from tens of
            // units away by an invisible aura.
            //
            // It matters most on the Scarab, whose skimmer CREATES the ball out of a crystal
            // (SCARAB.md §4.1). Without this, the very sphere that just converted the crystal
            // would strike the new ball on the same frame and throw it clear before the hull
            // arrived — which is precisely the "ball leaves before the ship gets there" feel the
            // skimmer forge exists to remove.
            //
            // Tested on the presence of SwingKinematics rather than on `blade`, so a sword is
            // still routed through the blade branch even when `bladeAwareStrikes` is off; that
            // flag keeps its own meaning instead of being backdoored into "swords cannot hit".
            if (blade == null && hitCollider != null)
            {
                var skimField = hitCollider.GetComponentInParent<Skimmer>();
                if (skimField != null && skimField.SwingKinematics == null) return;
            }

            if (blade != null)
            {
                Vector3 ballCenter = transform.position;
                contactPoint = blade.ClosestBladePoint(ballCenter);

                // A swinging skimmer carries TWO volumes: the sword's own thin CapsuleCollider and
                // the much larger SPHERE trigger that is its skim field (radius = half the blade's
                // length, so 15-60 units). Both reach this method. Only the blade may strike the
                // ball - otherwise the Rhino bats the payload from meters away with an invisible
                // aura, which is worse than the vessel-root behaviour it replaced. Reach is measured
                // off the blade's CENTRELINE, so it is the same test at the hilt and at the tip.
                float reach = BallWorldRadius() + settings.bladeClearRadius;
                if ((ballCenter - contactPoint).sqrMagnitude > reach * reach) return;

                bladeT = blade.NormalizedAlongBlade(ballCenter);
                strikerVelocity = Vector3.ClampMagnitude(blade.VelocityAt(contactPoint), settings.maxSpeed);
                EjectBallFromPoint(contactPoint, settings.bladeClearRadius);
            }
            else
            {
                EjectBallFromVessel(root); // anti-clip every frame - independent of the bounce/strike gating
                strikerVelocity = ResolveStrikerVelocity(vessel);
            }

            // Only respond when the ball is actually moving INTO the vessel - avoids re-launching a ball
            // that has already bounced away (self-limiting) and double-bouncing on the second collider path.
            Vector3 n = (transform.position - contactPoint).normalized;
            if (Vector3.Dot(rb.linearVelocity - strikerVelocity, n) >= 0f) return;

            float now = Time.time;
            float strikerSpeed = strikerVelocity.magnitude; // computed once, reused inside VesselStrike
            bool deliberate = strikerSpeed >= settings.minimumHitSpeed
                && (!_lastStrikeTime.TryGetValue(root, out var last) || now - last >= settings.vesselStrikeCooldown);
            if (deliberate) _lastStrikeTime[root] = now;

            VesselStrike(vessel, contactPoint, strikerVelocity, strikerSpeed, n, deliberate,
                blade != null, bladeT);
        }

        /// <summary>The ball's world-space radius (collider radius × max lossy scale) - tracks intensity scaling.</summary>
        public float BallWorldRadius()
        {
            var s = transform.lossyScale;
            return sphereCol.radius * Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        }

        /// <summary>Configured top speed (used by AstroLeagueGoal's teleport guard).</summary>
        public float MaxSpeed => settings != null ? settings.maxSpeed : 220f;

        /// <summary>
        /// Guarantees the ball never overlaps the striking vessel's hull: if the ball center is
        /// closer than (ball radius + vesselClearRadius) to the vessel root, push it straight out
        /// to that distance. With the ≥1x launch speed this keeps the ball ahead of the vessel,
        /// so the vessel mesh can't clip through it - including the trigger-only ships that have
        /// no physical depenetration barrier. Server position is republished immediately so peers
        /// see the ejected position without waiting for the next tick.
        /// </summary>
        void EjectBallFromVessel(Transform vesselRoot) =>
            EjectBallFromPoint(vesselRoot.position, settings.vesselClearRadius);

        /// <summary>
        /// The depenetration above, generalized to any contact origin. A BLADE hit passes the point
        /// on the sword's centreline nearest the ball with the blade's own (much smaller) clearance:
        /// pushing off the vessel ROOT cannot protect a 30-120 unit sword, because at a tip strike
        /// the ball is already far outside the hull's clear radius and the check would no-op while
        /// the blade sweeps straight through it.
        /// </summary>
        void EjectBallFromPoint(Vector3 origin, float clearRadius)
        {
            float minClear = BallWorldRadius() + clearRadius;
            Vector3 away = transform.position - origin;
            float dist = away.magnitude;
            if (dist <= 0.001f || dist >= minClear) return;

            Vector3 cleared = origin + away * (minClear / dist);
            rb.position = cleared;          // physics-authoritative (server ball is non-kinematic)
            transform.position = cleared;   // immediate visual + the n_Position read below
            if (IsSpawned) n_Position.Value = cleared;
        }

        /// <summary>
        /// Server: momentum-conserving ELASTIC bounce of the ball off the moving vessel hull. Vessels
        /// are transform-driven, so the hull is treated as an infinite-mass moving paddle: in the
        /// vessel's frame the approaching component of the ball's velocity reflects about the contact
        /// normal (restitution = ballBounciness); transforming back adds the vessel velocity, so a fast
        /// vessel imparts up to ~2× its speed (the kick) and the ball cleanly bounces off - a stationary
        /// vessel still reflects the ball's own velocity. The off-center contact injects torque → spin,
        /// and the ball re-colors to the striker's domain. Always runs on an approaching contact (the
        /// caller guarantees that). When <paramref name="deliberate"/> (fast hit, off cooldown) it also
        /// adds the arcade pop (hitBoostMultiplier, aim-biased), recoils the vessel, and may hitstop.
        /// </summary>
        void VesselStrike(IVessel vessel, Vector3 contactPoint, Vector3 strikerVelocity, float strikerSpeed,
            Vector3 n, bool deliberate, bool bladeHit = false, float bladeT = 0f)
        {
            // Re-color the ball to the striker's domain - every bounce counts as the last hit. The
            // per-tick prism scan picks up the new same/opposing relationship automatically next tick.
            // Unless ownership is LOCKED (SCARAB.md §4.2 — every Scarab-forged ball belongs to its
            // maker forever): then a strike moves the ball but never claims it — EXCEPT a strike
            // delivered mid-juke, which is the sanctioned STEAL (see _ownershipLocked). The steal
            // is symmetric: whoever holds it, any Scarab's dash takes it, including back again.
            Domains strikerDomain = vessel.VesselStatus != null ? vessel.VesselStatus.Domain : Domains.Blue;
            bool jukeSteal = _ownershipLocked
                             && strikerDomain != Domains.Blue
                             && strikerDomain != n_LastHitDomain.Value
                             && IsJukeStrike(vessel);
            if ((!_ownershipLocked || jukeSteal) && n_LastHitDomain.Value != strikerDomain)
                n_LastHitDomain.Value = strikerDomain;

            RecordTouchServer(strikerDomain,
                vessel.VesselStatus != null ? vessel.VesselStatus.PlayerName : string.Empty);

            // EMBEDDED: this strike knocks the ball out of the nucleus surface rather than batting a
            // ball already in flight, so it resolves here and returns — the impulse maths below assume
            // a free body. The push is the striker's own velocity, so how hard you hit it is how fast
            // it leaves, and WHICH SIDE of the embed normal you hit it from decides where it goes:
            // outward into the cytoplasm, inward into the nucleus. The steal above already ran, so a
            // dash can take an enemy's seeded ball and knock it loose in the same contact.
            if (n_Embedded.Value)
            {
                float releaseSpeed = Mathf.Clamp(strikerSpeed, settings.ballRestSpeed, settings.maxSpeed);
                Vector3 push = strikerVelocity.sqrMagnitude > 1e-4f
                    ? strikerVelocity.normalized
                    : -_embedOutward;                       // a dead-stop contact nudges it inward
                ReleaseFromNucleusServer(push * releaseSpeed);
                return;
            }

            Vector3 ballVel = rb.linearVelocity;

            // Elastic collision off the moving paddle (momentum-conserving against an infinite-mass
            // hull): reflect the APPROACHING component of the relative velocity, then add V back.
            Vector3 rel = ballVel - strikerVelocity;
            float approach = Vector3.Dot(rel, n);                       // < 0: ball moving into the hull
            float e = Mathf.Clamp01(settings.ballBounciness);
            Vector3 reflectedRel = rel - (1f + e) * Mathf.Min(0f, approach) * n;
            Vector3 desiredVelocity = reflectedRel + strikerVelocity;

            if (deliberate)
            {
                // Arcade pop: extra launch biased from the contact normal toward the pilot's heading
                // (aim), scaled by strike strength. directionalBias 0 = pure normal, 1 = pure heading.
                Vector3 aimDir = strikerSpeed > 0.0001f
                    ? Vector3.Slerp(n, strikerVelocity / strikerSpeed, settings.directionalBias).normalized
                    : n;
                // Sweet spot: a TIP strike pops harder than a hilt one. The swing model already
                // makes the tip physically faster; this is the arcade reward on top for timing it.
                float pop = Mathf.Max(0f, settings.hitBoostMultiplier - 1f);
                if (bladeHit)
                    pop *= Mathf.Lerp(1f, Mathf.Max(1f, settings.bladeTipStrikeBonus), Mathf.Clamp01(bladeT));
                desiredVelocity += aimDir * (strikerSpeed * pop);
            }

            if (desiredVelocity.sqrMagnitude > settings.maxSpeed * settings.maxSpeed)
                desiredVelocity = desiredVelocity.normalized * settings.maxSpeed;

            // Apply as set-linear + matching off-center torque (the split form of
            // AddForceAtPosition(impulse, contactPoint)): spin emerges from real angular dynamics while
            // the linear result is immediate (AddForce* would defer a tick).
            Vector3 impulse = rb.mass * (desiredVelocity - ballVel);
            rb.linearVelocity = desiredVelocity;
            Vector3 lever = contactPoint - rb.worldCenterOfMass;
            rb.AddTorque(Vector3.Cross(lever, impulse), ForceMode.Impulse); // clamped by rb.maxAngularVelocity

            float finalSpeed = desiredVelocity.magnitude;
            float intensity = Mathf.Clamp01(finalSpeed / settings.maxSpeed);
            // Prisms near the strike are handled by the per-tick ProcessPrismInteractions scan, which
            // already sees the freshly-set domain - no separate strike-time prism pass needed.

            if (!deliberate) return;

            if (finalSpeed > settings.hitstopSpeedThreshold && IsSoloSession())
                RunHitstopAsync().Forget();

            // THE feedback beat. Before this existed a vessel connecting with the ball produced
            // nothing at all - no flash, no burst, no shake, no sound - which is the single largest
            // reason the mode read as unresponsive: the only evidence you had hit the payload was
            // that it changed direction. Broadcast so every peer sees the hit, and carry the
            // striking vessel so the pilot who actually connected gets the emphasised shake.
            if (settings.strikeFeedbackEnabled)
            {
                var strikerNo = vessel.Transform != null
                    ? vessel.Transform.GetComponentInParent<NetworkObject>()
                    : null;
                ulong strikerNetId = strikerNo != null ? strikerNo.NetworkObjectId : 0UL;
                Strike_ClientRpc(contactPoint, n, intensity, strikerNetId, bladeHit && bladeT > 0.66f);
            }

            OnStruckServer?.Invoke(vessel, intensity); // controller recoils the vessel (it bounces off too)
        }

        /// <summary>
        /// SERVER: an AOE blast reached the ball. The payload is shoved the way prism mass is
        /// shoved — with the blast's OWN impact vector (<see cref="AOEExplosion.CalculateImpactVector"/>,
        /// i.e. <c>ExplosionImpulse.Along(radialDirection)</c>), so a weapon tuned to throw mass
        /// hard throws the ball hard and no second tuning surface exists to drift from the first.
        /// There is no distance falloff because prisms do not get one either: the blast either
        /// reached you or it did not.
        ///
        /// Before this, an explosion passed straight through the ball. Nothing in the mode could
        /// move the payload except a hull or a blade, which quietly made every AOE weapon useless
        /// on the one object the match is about.
        ///
        /// A blast also CLAIMS the ball (<see cref="AstroLeagueSettingsSO.explosionClaimsBall"/>),
        /// on the same rule as a strike: whoever touched it last owns it. And unlike a prism, the
        /// ball has no friendly-fire exemption — blowing your OWN ball toward the goal is a play,
        /// not an accident, so the blast's domain is never a reason to skip.
        /// </summary>
        /// <param name="blastOrigin">Centre the impact vector radiates from.</param>
        /// <param name="impactVector">The blast's impact vector at the ball's position — already
        /// direction × magnitude, exactly as a prism receives it.</param>
        /// <param name="blastDomain">Domain that fired the blast; claims the ball.</param>
        /// <summary>
        /// Route a blast to the ball from whichever peer owns the explosion. Blasts are LOCAL —
        /// a projectile or a juke-fired cavitation punch exists only on the machine that fired
        /// it — while the ball is server-simulated, so a client's blast has to ask. Without this
        /// hop, "explosions move the ball" would quietly mean "the host's explosions move the
        /// ball", which is the kind of asymmetry that reads as netcode jitter rather than as a
        /// missing feature.
        /// </summary>
        public static void RequestBlast(AstroLeagueBall ball, IVessel source, Vector3 blastOrigin,
                                        Vector3 impactVector, Domains blastDomain)
        {
            if (ball == null) return;

            if (!ball.IsSpawned || ball.IsServer)
            {
                ball.ApplyBlastServer(blastOrigin, impactVector, blastDomain);
                return;
            }

            // Client: ask through our own Player, so ownership is the identity check and the
            // server decides the domain from its own copy of that player's vessel.
            var player = source?.VesselStatus?.Player as Player;
            if (player == null || !player.IsSpawned || !player.IsOwner) return;
            player.RequestBlastBall_ServerRpc(ball.NetworkObjectId, blastOrigin, impactVector);
        }

        public void ApplyBlastServer(Vector3 blastOrigin, Vector3 impactVector, Domains blastDomain)
        {
            if (settings == null || !settings.explosionsAffectBall) return;
            if (IsSpawned && !IsServer) return;
            if (n_Frozen.Value || n_Hidden.Value) return;

            Vector3 kick = impactVector * settings.explosionKickMultiplier;
            if (kick.sqrMagnitude < 1e-6f) return;

            Vector3 desired = rb.linearVelocity + kick;
            if (desired.sqrMagnitude > settings.maxSpeed * settings.maxSpeed)
                desired = desired.normalized * settings.maxSpeed;

            Vector3 before = rb.linearVelocity;
            rb.linearVelocity = desired;

            // Spin comes from applying the impulse off-centre, at the point on the ball's surface
            // facing the blast — the split form of AddForceAtPosition, same as VesselStrike uses,
            // so a blast tumbles the ball instead of sliding it flat.
            if (settings.explosionSpinFraction > 0f)
            {
                Vector3 toBall = transform.position - blastOrigin;
                Vector3 facing = toBall.sqrMagnitude > 1e-6f ? -toBall.normalized : Vector3.up;
                Vector3 contact = transform.position + facing * BallWorldRadius();
                Vector3 impulse = rb.mass * (desired - before) * settings.explosionSpinFraction;
                rb.AddTorque(Vector3.Cross(contact - rb.worldCenterOfMass, impulse), ForceMode.Impulse);
            }

            if (settings.explosionClaimsBall && !_ownershipLocked && blastDomain != Domains.Blue
                && n_LastHitDomain.Value != blastDomain)
                n_LastHitDomain.Value = blastDomain;

            // A blast is a touch for the arming/bank ledger (no name — nobody escorted it),
            // but never a steal: only the juke's committed dash converts a locked ball.
            // A DOMAIN-LESS blast (Blue) records nothing: Blue is the ledger's "untouched"
            // sentinel, so stamping it would silently RE-ARM a ball an enemy touch had
            // disarmed — a neutral explosion must not launder the arming state.
            if (blastDomain != Domains.Blue)
                RecordTouchServer(blastDomain, string.Empty);

            if (IsSpawned)
            {
                n_Velocity.Value = rb.linearVelocity;
                n_AngularVelocity.Value = rb.angularVelocity;
            }

            if (settings.strikeFeedbackEnabled && IsSpawned)
            {
                Vector3 toBall = transform.position - blastOrigin;
                Vector3 normal = toBall.sqrMagnitude > 1e-6f ? toBall.normalized : Vector3.up;
                float intensity = Mathf.Clamp01(rb.linearVelocity.magnitude / settings.maxSpeed);
                // No striker vessel: a blast is credited to the blast, so the emphasised
                // striker shake has nobody to land on and every peer gets the shared one.
                Strike_ClientRpc(transform.position - normal * BallWorldRadius(), normal,
                                 intensity, 0UL, tipHit: false);
            }
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
            // (the server rigidbody owns the real rotation, interpolated natively). Purely cosmetic -
            // no gameplay reads client rotation, so any dead-reckoned drift is invisible.
            if (IsSpawned && !IsServer && !n_Frozen.Value && !n_Hidden.Value)
            {
                Vector3 w = n_AngularVelocity.Value;
                float wMag = w.magnitude;
                if (wMag > 1e-4f)
                    transform.rotation = Quaternion.AngleAxis(wMag * Mathf.Rad2Deg * Time.deltaTime, w / wMag) * transform.rotation;
            }

            // Impact pop: a fast swell that eases back over strikePopSeconds. Visual child only -
            // the root's scale is the ball's physical size and must not move (see _visual).
            if (_visual != null)
            {
                float pop = 1f;
                if (_popTimer > 0f)
                {
                    _popTimer -= Time.deltaTime;
                    float t = Mathf.Clamp01(_popTimer / Mathf.Max(0.0001f, settings.strikePopSeconds));
                    pop = 1f + settings.strikePopAmount * t * t;
                }

                // Birth bloom (continuity of existence): every peer's fresh instance GROWS in
                // over spawnBloomSeconds instead of popping into the court at full size. Runs
                // once per instance from Awake; composes with the pop as a multiplier.
                float bloom = 1f;
                if (_bloomTimer > 0f)
                {
                    _bloomTimer -= Time.deltaTime;
                    float u = 1f - Mathf.Clamp01(_bloomTimer / Mathf.Max(0.0001f, settings.spawnBloomSeconds));
                    bloom = u * u * (3f - 2f * u); // smoothstep 0→1, settles without a snap
                }

                Vector3 targetScale = Vector3.one * (pop * bloom);
                if (_visual.localScale != targetScale)
                    _visual.localScale = targetScale;
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

        /// <summary>Base hue for a claimed (non-neutral) ball - matches the arena's per-domain palette
        /// (all three domains config-driven from <see cref="AstroLeagueSettingsSO"/>).</summary>
        Color DomainTint(Domains d)
        {
            switch (d)
            {
                case Domains.Jade: return settings.jadeGoalColor;
                case Domains.Ruby: return settings.rubyGoalColor;
                case Domains.Gold: return settings.goldGoalColor;
                default: return primaryColor;
            }
        }

        #endregion

        #region Juice (replicated to every peer)

        [ClientRpc]
        void Detonate_ClientRpc(Vector3 position, float radiusScale)
        {
            float s = Mathf.Max(0.1f, radiusScale);
            EmitBurst(position, Vector3.up, Mathf.RoundToInt(settings.goalParticleBurst * s));
            ShakeCamera(settings.goalShakeIntensity * s, settings.goalShakeDuration, position);
            HapticController.PlayHaptic(HapticType.MineCollision);
        }

        /// <summary>
        /// Every peer: the VESSEL STRIKE beat - the mode's primary act, and until now the only one
        /// with no feedback at all. Four layers, each covering a different distance:
        ///   • ball emission FLASH + a scale POP on the ball's visual child (never the root, whose
        ///     lossyScale is the ball's physical size) - readable from across the arena, where a
        ///     particle burst is a few pixels;
        ///   • particle BURST off the contact point - the close-up read;
        ///   • camera SHAKE, distance-scaled as usual, and multiplied for the pilot who actually
        ///     connected so striking feels different from watching somebody strike;
        ///   • an audio CUE, heavier above bigHitSpeedFraction (and heavier again on a sword TIP),
        ///     so power is audible even when the ball leaves frame instantly.
        /// Haptics are deliberately absent: Docs/HAPTICS.md ships two feels (skim reward / prism
        /// punish) plus one rare alert, and a ball strike is none of them.
        /// </summary>
        [ClientRpc]
        void Strike_ClientRpc(Vector3 position, Vector3 normal, float intensity, ulong strikerVesselNetId, bool tipHit)
        {
            if (settings == null) return;

            bool bigHit = intensity >= settings.bigHitSpeedFraction;
            float weight = Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(intensity));

            TriggerFlash(bigHit ? 1f : weight);
            EmitBurst(position, normal, Mathf.RoundToInt(settings.impactParticleBurst * weight));

            if (settings.strikePopSeconds > 0f)
                _popTimer = settings.strikePopSeconds;

            // The striking pilot gets the emphasised shake. Resolved by NetworkObjectId against the
            // LOCAL player's vessel rather than by ownership, because AI vessels are server-owned -
            // an ownership test would hand the host every AI's emphasis.
            float emphasis = 1f;
            if (strikerVesselNetId != 0 && gameData != null && gameData.LocalPlayer?.Vessel != null
                && gameData.TryGetVesselByNetworkObjectId(strikerVesselNetId, out var struck)
                && ReferenceEquals(struck, gameData.LocalPlayer.Vessel))
                emphasis = Mathf.Max(1f, settings.strikerShakeEmphasis);

            ShakeCamera(settings.strikeShakeIntensity * weight * emphasis, settings.strikeShakeDuration, position);

            audioSystem?.PlayGameplaySFX(bigHit || tipHit
                ? GameplaySFXCategory.Explosion
                : GameplaySFXCategory.VesselImpact);
        }

        /// <summary>
        /// Every peer: lightweight wall-bounce feedback (perfectly elastic - no prism interaction,
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
                // Restore to known constants, not captured values - a concurrent
                // celebration slow-mo must not be clobbered by a stale capture.
                Time.timeScale = 1f;
                Time.fixedDeltaTime = baseFixedDelta;
                hitstopActive = false;
            }
        }

        #endregion

        #region Match control API (server-only, driven by AstroLeagueController)

        /// <summary>Server: detonate at the goal mouth - burst + shake on every peer, then hide until kickoff.</summary>
        public void DetonateServer()
        {
            if (!IsServer) return;
            Detonate_ClientRpc(transform.position, 1f);
            SetHiddenServer(true);
        }

        /// <summary>
        /// Server: EMBED this ball in the nucleus surface at <paramref name="surfacePoint"/>, with
        /// <paramref name="outward"/> pointing away from the cell centre. The ball stops simulating and
        /// pins to the anchor, but stays contactable — being struck loose is the entire point of it.
        ///
        /// It keeps its domain and its ownership lock, so a seeded ball is already its Scarab's, and an
        /// enemy who wants it must dash-steal it exactly like any other ball.
        /// </summary>
        public void EmbedOnNucleusServer(Vector3 surfacePoint, Vector3 outward)
        {
            if (!IsServer) return;

            _embedAnchor = surfacePoint;
            _embedOutward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.up;

            SetHiddenServer(false);
            if (n_Frozen.Value) SetFrozenServer(false); // embedded is its own state, never frozen
            n_Embedded.Value = true;
            ApplyEmbeddedPhysics(true);

            SetSpawnPosition(surfacePoint);
            transform.position = surfacePoint;
            if (IsSpawned)
            {
                n_Position.Value = surfacePoint;
                n_Velocity.Value = Vector3.zero;
                n_AngularVelocity.Value = Vector3.zero;
            }
            if (trail != null) trail.Clear();
        }

        /// <summary>
        /// Server: knock an embedded ball loose along <paramref name="velocity"/>. Returns true if it
        /// went INWARD (toward the cell centre — into the nucleus/court), false if it went OUTWARD into
        /// the cytoplasm. The caller supplies the push; the ball reports which side of its own embed
        /// normal that push was on, and raises <see cref="OnNucleusReleasedServer"/> so the field can
        /// count nucleus entries without duplicating the geometry test.
        /// </summary>
        public bool ReleaseFromNucleusServer(Vector3 velocity)
        {
            if (!IsServer || !n_Embedded.Value) return false;

            bool inward = Vector3.Dot(velocity, _embedOutward) < 0f;

            n_Embedded.Value = false;
            ApplyEmbeddedPhysics(false);

            rb.linearVelocity = velocity;
            rb.angularVelocity = Vector3.zero;
            if (IsSpawned)
            {
                n_Velocity.Value = velocity;
                n_AngularVelocity.Value = Vector3.zero;
            }
            _lastPrismScanPos = transform.position;

            OnNucleusReleasedServer?.Invoke(this, inward);
            return inward;
        }

        /// <summary>
        /// The embedded twin of <see cref="ApplyFrozenPhysics"/>: kinematic + pinned while embedded,
        /// dynamic again on release. Deliberately does NOT snap to spawnPosition on the way out — the
        /// release writes the ball's launch velocity immediately after, and a snap would fight it.
        /// </summary>
        void ApplyEmbeddedPhysics(bool embedded)
        {
            if (embedded)
            {
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.isKinematic = true;
                transform.position = _embedAnchor;
            }
            else if (!n_Frozen.Value)
            {
                rb.isKinematic = false;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }
        }

        /// <summary>
        /// Server: blow the ball up where it stands with an explosion <paramref name="radiusScale"/>×
        /// its own radius, then spend it. This is the nucleus overload detonation (SCARAB.md §4.6) —
        /// the burst and shake scale with the blast so a 2× explosion reads as one, and the ball leaves
        /// through the same detonate-then-despawn beat as a scored ball (continuity of existence: it
        /// bursts and fades, it never blinks out).
        /// </summary>
        public void DetonateWithRadiusServer(float radiusScale)
        {
            if (!IsServer) return;
            Detonate_ClientRpc(transform.position, Mathf.Max(0.1f, radiusScale));
            SetHiddenServer(true);
            DespawnIfForged();   // NOT ExpireServer: that would fire a second, unscaled burst
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
                // Start the kickoff coast from rest - no stale linear/angular velocity carried
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

        /// <summary>
        /// Override where the ball resets to on kickoff (default = the authored arena center). The
        /// central shared-goal layout spawns it off-center, in the goal's plane, so it doesn't start
        /// sitting in the central goal. Set by the controller; only the server uses spawnPosition.
        /// </summary>
        public void SetSpawnPosition(Vector3 worldPosition)
        {
            spawnPosition = worldPosition;
        }

        /// <summary>Server: respawn at the spawn position - visible, frozen, zero velocity, NEUTRAL color again.</summary>
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
            // Fresh ball at kickoff: unclaimed until the first strike.
            n_LastHitDomain.Value = Domains.Blue;
            ResetTouchLedgerServer();
            _shieldPoppedThisVisit.Clear();
            _lastPrismScanPos = spawnPosition;
            if (trail != null) trail.Clear();
        }

        /// <summary>
        /// Server: permanently pin the ball's domain to whoever it belongs to NOW (SCARAB.md §4.2).
        /// While locked, a strike or a blast moves the ball but never re-colours it, so "your ball
        /// always eats the enemy's trail and always shields yours, from birth to death" — the
        /// legibility rule that keeps a multi-ball arena readable. The one act that converts a
        /// locked ball is a Scarab's juke-dash STEAL, and it converts in either direction, so a
        /// robbed owner always has the same move available to take it back.
        ///
        /// Called by <see cref="ScarabBallForge"/> on every ball it mints — permanent ownership is
        /// a property of the Scarab's forge, not of a mode. Astro League's scene-placed match ball
        /// never routes through the forge, so it keeps last-touch colouring.
        /// </summary>
        public void SetOwnershipLockedServer(bool locked)
        {
            if (!IsServer) return;
            _ownershipLocked = locked;
        }

        /// <summary>
        /// Server: spend the ball — the goal-mouth burst on every peer, then despawn (a scene-placed
        /// match ball is only ever hidden). This is the public face of the private expiry the
        /// super-shield contact path already uses; Scarab Scramble retires a scored ball through it,
        /// so a spent payload always leaves through the same detonation beat (continuity of
        /// existence — it bursts and fades, it never blinks out).
        /// </summary>
        public void SpendServer()
        {
            if (!IsServer) return;
            ExpireServer();
        }

        /// <summary>
        /// Server: bring a freshly-spawned ball into play at <paramref name="position"/> carrying
        /// <paramref name="velocity"/>, owned by <paramref name="ownerDomain"/>. This is the entry
        /// point for a ball that is CREATED mid-match rather than reset to the arena centre — the
        /// Scarab's crystal forge (R_VesselActions/SCARAB.md §4.1) is the first caller; the Astro
        /// League controller never uses it, so the mode's single-ball flow is unchanged.
        ///
        /// Order is load-bearing: a fresh ball's n_Frozen defaults to TRUE (kinematic, snapped to
        /// spawnPosition), and un-freezing deliberately ZEROES the rigidbody's velocity, so the
        /// launch velocity has to be written after the unfreeze or it is silently discarded.
        ///
        /// The domain written here is the ball's OWNER. It replicates through the same variable a
        /// strike would rewrite, so until per-ball permanent ownership lands (SCARAB.md §4.2) an
        /// opposing strike still re-colours it — the forge sets the STARTING allegiance, which is
        /// what makes it eat the enemy's trail and shield yours from birth.
        /// </summary>
        public void LaunchServer(Vector3 position, Vector3 velocity, Domains ownerDomain)
        {
            if (!IsServer) return;

            SetSpawnPosition(position);
            transform.position = position;
            transform.rotation = Quaternion.identity;

            SetHiddenServer(false);
            SetFrozenServer(false);   // zeroes rb velocity — everything below must follow it

            rb.linearVelocity = velocity;
            rb.angularVelocity = Vector3.zero;

            n_Position.Value = position;
            n_Velocity.Value = velocity;
            n_AngularVelocity.Value = Vector3.zero;
            n_LastHitDomain.Value = ownerDomain;

            ResetTouchLedgerServer(); // fresh forge: untouched, so it still carries its maker's launch
            _shieldPoppedThisVisit.Clear();
            _lastPrismScanPos = position;   // or the first scan sweeps from the origin
            if (trail != null) trail.Clear();
        }

        #endregion

        public override void OnDestroy()
        {
            base.OnDestroy();
            if (_ballMesh != null) Destroy(_ballMesh);
        }
    }
}
