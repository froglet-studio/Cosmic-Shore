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

        [Header("Cell overload")]
        [Tooltip("How many balls may be LOOSE IN ONE CELL at once. The ball whose arrival crosses " +
                 "this count detonates every loose ball in that cell, ITSELF INCLUDED, regardless " +
                 "of domain. 0 disables the rule. This is a property of BALLS, not of a mode: it " +
                 "travels with the prefab into freestyle, the menu and any future mode, so no " +
                 "controller can forget to install it.")]
        [SerializeField, Min(0)] int cellBallLimit = 4;

        [Tooltip("Explosion radius of a cell overload, as a multiple of each ball's own radius. " +
                 "Kept EQUAL to ScarabNucleusFieldConfig.detonationRadiusScale (both 2) so the two " +
                 "'too many balls' events read as the same event - they are deliberately separate " +
                 "fields because one belongs to the ball and the other to a vessel ability, and a " +
                 "platform ball must not depend on a Scarab config to know how loudly to die.")]
        [SerializeField, Min(0.1f)] float cellOverloadRadiusScale = 2f;

        [Tooltip("Seconds between cell-membership checks. Cell.FindCellContaining is O(active " +
                 "cells) and its own docs say to call it at lifecycle points rather than per " +
                 "frame, so entry is detected on a poll rather than every physics step.")]
        [SerializeField, Min(0.02f)] float cellPollSeconds = 0.2f;

        /// <summary>
        /// Server-side: the cell this ball is currently LOOSE in, or null. Deliberately SEPARATE
        /// from <c>_cell</c> / <c>ResolveCell</c>, which is a sticky cache for the drag lookup and
        /// re-resolves only when its reference dies. This one must go null the moment the ball is
        /// hidden or embedded, because it exists to detect the ENTRY EDGE and a sticky cache has no
        /// edges. It is only ever the edge detector: every COUNT re-tests position, so a stale
        /// value here can delay an overload but can never miscount one.
        /// </summary>
        Cell _looseInCell;
        float _nextCellPoll;

        /// <summary>
        /// Raised on EVERY peer when a cell overloads, carrying where it happened and how many
        /// balls went up. Presentation only - the detonation itself is already server-authoritative
        /// and replicated per ball. Static because balls are spawned and destroyed per forge, so
        /// there is nothing durable for a HUD or a controller to subscribe to.
        /// </summary>
        public static event System.Action<Vector3, int> OnCellOverload;

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

        // How far a studding ball must be shoved off its seed point, as a fraction of its own
        // radius, before it counts as having LEFT the nucleus surface even though it is not
        // moving (a vessel depenetration does exactly this). Half a radius: far enough that
        // physics noise cannot trip it, near enough that the ball is visibly out of the shell.
        const float NucleusDepartureRadiusFraction = 0.5f;

        // Server-side, ONE WAY: set the first time this ball leaves the nucleus surface and never
        // cleared. A ball that has been dislodged is a ball, permanently — it can be struck,
        // blasted, banked and detonated like any other, and it can never be re-seeded into the
        // shell, which would be the one state a player cannot reach on purpose.
        bool _releasedFromNucleus;

        // While Time.time is below this, ContainWithinBoundary is skipped — see
        // AstroLeagueSettingsSO.nucleusReleaseGraceSeconds. Server-side; containment is server-only.
        float _containmentGraceUntil;

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

        // ── Held-drift grapple (SCARAB.md §4.7) ─────────────────────────────────────────────
        // Server-side: the Scarab hull currently HOLDING this ball, or null. The ball stays an
        // ordinary live body throughout — every other hull, blade and blast reaches it exactly as
        // before and the holder simply follows — so the only thing this changes on the ball is
        // that the holder's own contact neither strikes nor depenetrates it (VesselContact), its
        // spin follows the hull (HoldSpinServer), and the release adds a velocity (FlingServer).
        ScarabBallGrapple _grappledBy;

        /// <summary>Server: is <paramref name="grapple"/> the hull holding this ball right now?</summary>
        public bool IsGrappledBy(ScarabBallGrapple grapple) => grapple != null && _grappledBy == grapple;

        /// <summary>Server: a hull with the drift fully held touched this ball — take it, if it
        /// is free. A grab is a TOUCH for the arming ledger (the escort who held it is who pushed
        /// it home), never an ownership conversion.</summary>
        public bool TryBeginGrappleServer(ScarabBallGrapple grapple, Domains domain, string toucherName)
        {
            if (!(IsSpawned ? IsServer : true) || grapple == null) return false;
            if (n_Frozen.Value || n_Hidden.Value || _grappledBy != null) return false;
            _grappledBy = grapple;
            if (domain != Domains.Blue) RecordTouchServer(domain, toucherName);
            return true;
        }

        /// <summary>Server: <paramref name="grapple"/> let go (or died). No velocity change here —
        /// a throw goes through <see cref="FlingServer"/> first.</summary>
        public void EndGrappleServer(ScarabBallGrapple grapple)
        {
            if (_grappledBy != grapple) return;
            _grappledBy = null;
        }

        /// <summary>Server: drop whoever is holding this ball because the BALL is leaving play
        /// (spent, detonated, reset, hidden, frozen). The holder's own tick notices it is no
        /// longer the grappler and stands down without a fling.</summary>
        void ReleaseGrapplerServer() => _grappledBy = null;

        /// <summary>Server: the held ball's spin follows the hull circling it. Linear velocity is
        /// deliberately untouched — a carried ball keeps going where it was going until release.</summary>
        public void HoldSpinServer(Vector3 angularVelocity)
        {
            if (_grappledBy == null || n_Frozen.Value || n_Hidden.Value) return;
            float maxSpin = settings != null ? settings.maxAngularSpeed : 40f;
            rb.angularVelocity = Vector3.ClampMagnitude(angularVelocity, maxSpin);
        }

        /// <summary>
        /// Server: the grapple's RELEASE. <paramref name="deltaVelocity"/> is added to whatever the
        /// ball already carries (a carried ball keeps its momentum and gains the throw), clamped to
        /// the ball's universal ceiling, with the orbit's spin stamped on. Records a touch for the
        /// arming ledger, paces the thrower's next strike so the hull cannot re-hit the ball it
        /// just threw, and plays the strike beat on every peer — the throw is the mode's primary
        /// act arriving by a second route, so it gets the same feedback the hull strike gets.
        /// </summary>
        public void FlingServer(IVessel thrower, Vector3 deltaVelocity, Vector3 spin, Domains domain, string toucherName)
        {
            if (IsSpawned && !IsServer) return;
            if (settings == null || n_Frozen.Value || n_Hidden.Value) return;

            Vector3 before = rb.linearVelocity;
            Vector3 desired = before + deltaVelocity;
            if (desired.sqrMagnitude > settings.maxSpeed * settings.maxSpeed)
                desired = desired.normalized * settings.maxSpeed;
            rb.linearVelocity = desired;
            rb.angularVelocity = Vector3.ClampMagnitude(spin, settings.maxAngularSpeed);

            if (domain != Domains.Blue) RecordTouchServer(domain, toucherName);

            var root = thrower?.Transform;
            if (root != null) _lastStrikeTime[root] = Time.time;

            if (IsSpawned)
            {
                n_Velocity.Value = rb.linearVelocity;
                n_AngularVelocity.Value = rb.angularVelocity;
            }

            float intensity = Mathf.Clamp01(desired.magnitude / settings.maxSpeed);
            if (thrower != null) OnStruckServer?.Invoke(thrower, intensity);

            if (settings.strikeFeedbackEnabled && IsSpawned)
            {
                Vector3 normal = deltaVelocity.sqrMagnitude > 1e-6f ? deltaVelocity.normalized : Vector3.up;
                var strikerNo = root != null ? root.GetComponentInParent<NetworkObject>() : null;
                ulong strikerNetId = strikerNo != null ? strikerNo.NetworkObjectId : 0UL;
                Strike_ClientRpc(transform.position - normal * BallWorldRadius(), normal,
                                 intensity, strikerNetId, tipHit: false);
            }
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
        // STUDDING THE NUCLEUS (the Scarab's seeding ability, SCARAB.md §4.6): the ball was seeded
        // part-sunk in the nucleus surface and nothing has dislodged it yet.
        //
        // IT IS BOOKKEEPING, NOT A PHYSICS MODE. The ball is an ordinary live body the whole time —
        // dynamic, contactable, blastable, depenetrated like any other. This flag only says three
        // things: its containment is suspended (it sits on the wrong side of both volumes), it is
        // not counted among the cell's LOOSE balls, and the seeding field still has it on its
        // books. It was a pinned kinematic state for two passes and both of that state's
        // properties were defects: KINEMATIC meant no blast could move it (the Scarab's own dash
        // included), and PINNED meant the anchor fought the vessel depenetration every contact
        // frame, which is what made a seeded ball jitter in and out of the shell.
        //
        // It is also ONE WAY (see _releasedFromNucleus): once dislodged, a ball is just a ball.
        readonly NetworkVariable<bool> n_Embedded =
            new(readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);
        // Size factor over the authored base scale (SetSizeScale). Replicated because a forged
        // ball is sized after its spawn payload is built — see SetSizeScale's doc note.
        readonly NetworkVariable<float> n_SizeScale =
            new(1f, readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        // WHICH crystal this ball was forged out of, and the pose that crystal was standing in
        // when it was spent. Replicated for the same reason n_SizeScale is — the stamp happens
        // AFTER NetworkObject.Spawn, so the spawn payload cannot carry it — and because the
        // retirement animation must play on EVERY peer, not just the server that minted the ball.
        //
        // The POSE has to travel rather than being read back off the crystal, and across the wire
        // that is not a subtlety: collection and respawn are independent RPC chains, so by the time
        // a remote peer instantiates the ball its copy of the crystal has usually already moved to
        // its next home wearing the respawn's identity rotation. See Crystal.CollectPose.
        readonly NetworkVariable<CrystalForgeOrigin> n_ForgedFrom =
            new(default, readPerm: NetworkVariableReadPermission.Everyone, writePerm: NetworkVariableWritePermission.Server);

        float _lastSnapshotTime;

        // Velocity the ball carries INTO each physics step (captured pre-simulation in
        // ServerFixedUpdate). OnCollisionEnter runs post-solver, so this is the pre-bounce velocity -
        // used to size the wall-bounce juice by true impact speed (not the just-reflected value).
        Vector3 _velocityBeforePhysics;

        // Server time of the last wall-bounce juice (camera shake / haptic / burst) - rate-limits it so
        // a frictionless ball bouncing or skimming the wall can't spam the camera shake (see HandleWallBounce).
        float _lastWallJuiceTime;

        // MODE OVERRIDE court boundary - installed by AstroLeagueArena.Build via SetBoundary, and
        // ONLY for a court whose shape the nucleus sphere cannot express (Astro League's polytopes).
        // The ball is contained by a server-side reflect off the boundary's walls (no collider) -
        // flat polytope faces BANK the ball (billiards/air-hockey), a sphere focuses it toward
        // center. null (the normal case, including every mode-less context) means the ball falls
        // back to its OWN nucleus containment below.
        AstroLeagueBoundary _courtBoundary;

        // The ball's own containment: the nucleus of whatever cell it is currently in, ridden from
        // whichever side it is on (see ResolveNucleusBoundary). Cached because building one walks
        // the boundary's plane/extent setup; the cache key is every input it was built from, so it
        // rebuilds by itself when the ball crosses the surface, drifts into another cell, or a Cell
        // Selector swap resizes the world underneath it.
        //
        // The one input NOT in the key is the cell's CENTRE, which the boundary bakes at
        // construction — verified safe because nothing in the project writes a Cell's transform
        // (cells are scene-placed and a Cell Selector swap replaces the world INSIDE one, not the
        // object). Make a cell movable and this key needs the centre too.
        AstroLeagueBoundary _nucleusBoundary;
        Cell _nucleusBoundaryCell;
        float _nucleusBoundaryNucleusRadius = -1f;
        float _nucleusBoundaryOuterRadius = -1f;
        bool _nucleusBoundaryOutside;      // which side the CACHED boundary was built for

        // Which side of the nucleus this ball plays on. STICKY, with a dead band either side of
        // the surface (see ResolveNucleusBoundary) — cleared on a teleport so a relaunched ball
        // re-reads it rather than inheriting the side it had somewhere else.
        bool _outsideNucleus;
        bool _nucleusSideResolved;

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

        /// <summary>
        /// True while this ball is still STUDDING the nucleus surface — seeded there and not yet
        /// dislodged. It is an ordinary live body throughout; this only reports that it has not
        /// moved off its seed point yet. Once false it never becomes true again for this ball.
        /// </summary>
        public bool IsEmbeddedOnNucleus => n_Embedded.Value;

        /// <summary>
        /// Server: raised the instant a studding ball is dislodged BY ANYTHING — a hull, a blade, a
        /// blast, a shove — carrying whether it went INWARD (into the nucleus, the court, where balls
        /// are of consequence) or OUTWARD (into the cytoplasm, where it lives on, bouncing off the
        /// nucleus from the outside).
        ///
        /// It is raised from <see cref="TickNucleusDepartureServer"/>, which OBSERVES the ball rather
        /// than being called by whatever moved it — so no force has to know this ability exists, and
        /// one added tomorrow is covered for free. The ball resolves the direction because only it
        /// knows its own embed normal; <c>ScarabNucleusField</c> owns what the two directions MEAN,
        /// so policy never leaks into the payload.
        ///
        /// A SUBSCRIBER MAY DETONATE THE BALL (banking one too many overloads the nucleus, and the
        /// shipped default takes every live ball with it), which is why it is raised on the closing
        /// line of the server tick.
        /// </summary>
        public static event System.Action<AstroLeagueBall, bool> OnNucleusReleasedServer;

        // Paired with ScarabNucleusField.ResetStatics: its s_hooked latch and this event's
        // subscriber list must clear TOGETHER or the field either double-subscribes or never
        // re-subscribes across domain-reload-free play sessions.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticEvents()
        {
            OnCellOverload = null;
            OnNucleusReleasedServer = null;
        }
        /// <summary>Domain whose color the ball currently carries (Blue = neutral). Set by the last striker.</summary>
        public Domains LastHitDomain => n_LastHitDomain.Value;

        void OnEnable()
        {
            if (!Live.Contains(this)) Live.Add(this);
        }

        void OnDisable()
        {
            Live.Remove(this);
            ReleaseGrapplerServer();
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
            // NEVER SLEEP. The ball is DESIGNED to come to rest — `ballDrag` exists so an untouched
            // ball settles and becomes a thing players contest — and a resting rigidbody sleeps,
            // which drops it out of the simulation's active set. An AOE blast finds the ball
            // through a trigger on a collider that has no rigidbody of its own and merely GROWS
            // (AOECylindricalExplosion reshapes its box each frame); a pair with no awake actor in
            // it is not something a physics engine owes you an event for. So a settled ball — and
            // above all a ball studding the nucleus, which never moves at all — is exactly the ball
            // a blast could silently fail to reach. One always-simulated sphere per live ball is a
            // cheap price for "every force reaches every ball".
            rb.sleepThreshold = 0f;

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
            //
            // A ball forged out of a CRYSTAL cancels it (ScarabCrystalMorph.Begin): there the
            // crystal's own body closes onto this ball's hull and the ball takes over at full
            // size, so the bloom would be a SECOND birth animation playing underneath the first —
            // the ball growing out of nothing while the crystal is already landing on where it
            // will end up. Continuity of existence is satisfied either way; what it forbids is
            // popping into existence, not blooming twice.
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

            // The retirement animation runs on EVERY peer, and the value it needs can arrive
            // either before this replica spawned (a late joiner) or a frame after it (the server
            // stamps just past Spawn). Both are covered by reading it now AND subscribing — the
            // same shape n_SizeScale uses two blocks up, for the same reason.
            n_ForgedFrom.OnValueChanged += (_, origin) => TryBeginCrystalMorph(origin);
            TryBeginCrystalMorph(n_ForgedFrom.Value);
        }

        /// <summary>
        /// Stamps this ball as forged out of <paramref name="crystal"/>, so every peer plays the
        /// crystal→ball morph instead of the shared husk spray. Server-side and idempotent; a
        /// no-network local mint (the freestyle toys) applies it directly, since there the ball is
        /// never spawned and the NetworkVariable would never deliver.
        ///
        /// Call it AFTER the spawn, beside <see cref="SetSizeScale"/> — the two share the
        /// after-the-payload problem and the same solution.
        /// </summary>
        public void MarkForgedFromCrystal(Crystal crystal)
        {
            if (crystal == null) return;

            var pose = crystal.CollectPose;
            var origin = new CrystalForgeOrigin
            {
                CrystalId = crystal.Id,
                Position = pose.position,
                Rotation = pose.rotation,
                Scale = crystal.CollectScale,
                Valid = true,
            };

            if (IsSpawned && IsServer) n_ForgedFrom.Value = origin;
            TryBeginCrystalMorph(origin);
        }

        void TryBeginCrystalMorph(in CrystalForgeOrigin origin)
        {
            if (!origin.Valid || _crystalMorph != null) return;
            _crystalMorph = ScarabCrystalMorph.Begin(this, origin);
        }

        ScarabCrystalMorph _crystalMorph;

        /// <summary>
        /// Holds this ball's PHOTONS while the crystal's body is drawing it, and nothing else: the
        /// collider stays live, the rigidbody keeps simulating, the strike path is unchanged. The
        /// ball is fully live and strikeable from the frame it is forged — a pilot arriving one
        /// frame later hits a finished ball — and only its rendering waits.
        ///
        /// It is deliberately NOT <see cref="SetHidden"/>, which is replicated GAMEPLAY state (a
        /// ball parked out of play) and also freezes the body. Here nothing about the ball's
        /// situation has changed; a different object is drawing it for a third of a second.
        ///
        /// ALWAYS paired: the stand-in must clear the hold when it finishes or dies, or the ball is
        /// invisible for the rest of its life. <see cref="ScarabCrystalMorph"/> clears it from
        /// OnDestroy as well as on the hand-off, so an interrupted morph cannot strand it.
        /// </summary>
        public void SetMorphStandIn(bool active)
        {
            if (_morphStandIn == active) return;
            _morphStandIn = active;
            if (active) _bloomTimer = 0f;   // the morph IS this ball's birth animation
            ApplyHiddenVisuals(n_Hidden.Value);
        }

        bool _morphStandIn;

        /// <summary>The ball's own faceted hull mesh, and the radius it was generated at — the two
        /// halves a morph needs to land exactly on this ball's surface rather than on an
        /// approximation of it. Null before <see cref="SetupVisuals"/> has run.</summary>
        public Mesh HullMesh => _ballMesh;

        /// <summary>Radius the <see cref="HullMesh"/> was generated at, in the ball's own local
        /// units (the SphereCollider's authored radius). Scale is the transform's business.</summary>
        public float HullMeshRadius => sphereCol != null ? sphereCol.radius : 0.5f;

        /// <summary>The transform the hull mesh is drawn by — a child of the ball, so a morph
        /// parented here inherits the ball's motion and spin for free.</summary>
        public Transform VisualRoot => _visual != null ? _visual : transform;

        /// <summary>
        /// The prism-fresnel pair this ball is currently drawing with — its base face and its
        /// fresnel rim (Docs/PALETTE.md). Read off the live MaterialPropertyBlock rather than a
        /// theme lookup, because the ball animates this pair every frame through its own domain
        /// phase; a morph that converged on a re-derived colour would land next to the ball's
        /// colour rather than on it.
        /// </summary>
        public bool TryGetShellColours(out Color dark, out Color bright)
        {
            dark = default;
            bright = default;
            if (mpb == null || ballRenderer == null || !_usesFresnel) return false;
            ballRenderer.GetPropertyBlock(mpb);
            dark = mpb.GetColor(DarkColorId);
            bright = mpb.GetColor(BrightColorId);
            return true;
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
            // May DESTROY this ball (an overload detonates every loose ball in the cell, this one
            // included), so it must be able to stop the tick — everything below writes
            // NetworkVariables and the rigidbody, which a despawned ball no longer owns.
            if (TickCellMembershipServer()) return;

            SampleVesselVelocities();

            // A BALL STUDDING THE NUCLEUS RUNS THIS BRANCH LIKE ANY OTHER BALL. There is no
            // pinned state any more: `n_Embedded` is BOOKKEEPING (it suspends containment, keeps
            // the ball out of the cell's loose-ball count, and tells the seeding field it is still
            // studding), never a physics mode. See EmbedOnNucleusServer.
            if (!n_Frozen.Value)
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

                // Bounce the ball off the nucleus - its own boundary in any cell, a mode's court
                // override where one is installed.
                //
                // CONTAINMENT IS THE ONE THING A STUDDING BALL IS EXEMPT FROM, and it is the only
                // exemption the state has: the ball sits part-sunk in the shell, which is on the
                // wrong side of BOTH the court and the cytoplasm volumes, so containing it there
                // would shove it radially out of the surface the moment it was seeded. It resumes
                // the tick after the ball leaves, and `nucleusReleaseGraceSeconds` then carries it
                // clear of the shell rather than letting the walls correct it mid-flight — so
                // leaving reads as a hit in either direction (SCARAB.md §4.6).
                if (!n_Embedded.Value && Time.time >= _containmentGraceUntil)
                    ContainWithinBoundary();
            }

            if (IsSpawned)
            {
                n_Position.Value = transform.position;
                n_Velocity.Value = n_Frozen.Value ? Vector3.zero : rb.linearVelocity;
                n_AngularVelocity.Value = n_Frozen.Value ? Vector3.zero : rb.angularVelocity;
            }

            // LAST. A departure announcement can DETONATE this ball (banking one too many
            // overloads the nucleus, and the shipped default takes every live ball with it), so
            // nothing may touch this instance afterwards.
            if (n_Embedded.Value) TickNucleusDepartureServer();
        }

        /// <summary>
        /// Server: has this studding ball actually LEFT the nucleus surface? The whole release
        /// mechanism, and it is a passive OBSERVATION rather than a call any force has to make.
        ///
        /// That is the point. A release used to be something each force announced for itself, so a
        /// force nobody had wired announced nothing — and the ball being KINEMATIC meant such a
        /// force could not move it either, which is how every AOE blast in the game came to pass
        /// straight through a seeded ball. It is the same lesson this file already records for the
        /// forge-time ball cap: A RULE ENFORCED AT ONE PRODUCER CAN ONLY EVER SEE THAT PRODUCER.
        /// Watching the ball itself sees every force there is, including the ones added later.
        ///
        /// "Left" is either real motion or real displacement: a nudge the ball absorbs (below
        /// `ballRestSpeed`, which the tick above snaps to zero) leaves it studding, exactly as the
        /// same nudge would leave a ball stopped anywhere else stopped.
        /// </summary>
        void TickNucleusDepartureServer()
        {
            bool moving = rb.linearVelocity.sqrMagnitude > 0f;
            float leaveDistance = BallWorldRadius() * NucleusDepartureRadiusFraction;
            bool displaced = (transform.position - _embedAnchor).sqrMagnitude
                             > leaveDistance * leaveDistance;
            if (!moving && !displaced) return;

            n_Embedded.Value = false;
            _releasedFromNucleus = true;   // one way: a ball never studs the nucleus twice

            // Let whatever freed it carry the ball out of the shell before the walls start
            // correcting it, so leaving reads as a hit in either direction.
            _containmentGraceUntil = Time.time
                + (settings != null ? Mathf.Max(0f, settings.nucleusReleaseGraceSeconds) : 1f);
            _lastPrismScanPos = transform.position;

            // Which side of its own embed normal it left on — read off the velocity it actually
            // carries, or off where it ended up when it was shoved rather than struck.
            Vector3 heading = moving ? rb.linearVelocity : transform.position - _embedAnchor;
            OnNucleusReleasedServer?.Invoke(this, Vector3.Dot(heading, _embedOutward) < 0f);
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
            // EMBEDDED joins frozen and hidden here, and it is a FIX rather than a new rule: the
            // server already skipped the scan for a pinned ball (ServerFixedUpdate resolves prisms
            // only on the free branch — "scenery with a hitbox until somebody knocks it loose"),
            // while ClientFixedUpdate ran it for every non-frozen, non-hidden ball. So every peer
            // but the host was popping shields and destroying the prisms an embedded ball happened
            // to be sitting in, and — for a ball authored `destroyedBySuperShielded` — stripping
            // super-shielded structure the host never touched. One gate, one answer, on every peer.
            if (index == null || n_Frozen.Value || n_Hidden.Value || n_Embedded.Value)
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
        /// Install a MODE'S court boundary, overriding the ball's own nucleus containment
        /// (<see cref="ResolveNucleusBoundary"/>). Called by <c>AstroLeagueArena.Build</c> once the
        /// intensity scale + shape are known; a box/prism BANKS the ball off flat faces, a sphere
        /// focuses it. This replaced the six BoxCollider arena walls.
        ///
        /// A MODE ONLY NEEDS THIS FOR A COURT THE NUCLEUS SPHERE CANNOT EXPRESS - which today means
        /// Astro League's polytopes, whose walls are flat and whose nucleus is mesh-morphed to match
        /// (<c>Cell.SetNucleusMesh</c>). A mode whose court simply IS the nucleus sphere (Scarab
        /// Scramble, which resizes it with <c>Cell.SetNucleusWorldRadius</c>) must install NOTHING:
        /// the ball already bounces off its cell's nucleus everywhere, so a mode that installs the
        /// same sphere is re-declaring a platform behaviour it would then own the bugs in.
        /// Pass null to hand containment back to the ball.
        /// </summary>
        public void SetBoundary(AstroLeagueBoundary boundary)
        {
            _courtBoundary = boundary;
        }

        /// <summary>
        /// THE BALL BOUNCES OFF THE NUCLEUS, IN EVERY CELL IT CAN REACH - and that is a property of
        /// the ball rather than of any mode (SCARAB.md §4.6). It is the same reasoning that puts the
        /// ownership lock at the forge (§4.2) and the ball limit on the cell (§4.6): a rule a mode
        /// installs is a rule every other context silently lacks, and a Scarab forges balls in
        /// freestyle, in the menu, and in any future mode.
        ///
        /// ONE SURFACE SERVES BOTH SIDES, and which side is read from where the ball IS:
        ///   • inside  → a Sphere at the nucleus radius: the court, ridden from within.
        ///   • outside → the cytoplasm: outer sphere = the membrane (scaled in a little so a ball
        ///     never rides the literal skin), core obstacle = that same nucleus, ridden from without.
        ///
        /// Position, not the strike direction the seeding field knows, because each regime pushes
        /// AWAY from the surface (<c>ContainSphere</c> clamps distance to a maximum,
        /// <c>ContainCore</c> to a minimum) - so a ball settles into whichever side it is on and
        /// cannot oscillate, and a ball that gets across by any route is contained correctly with
        /// nobody having to tell it. <c>nucleusReleaseGraceSeconds</c> is what lets a struck embed
        /// carry across the shell before this engages.
        ///
        /// Returns null - no containment at all, exactly as before - for a cell with NO nucleus
        /// (Dog Fight's Boneyard), or with no cell in reach. The <c>outsideNucleusDrag</c> ramp is
        /// still the soft boundary out there; nothing is teleported or culled either way.
        ///
        /// NUCLEUS SIZE COMES FROM <c>NucleusVisualWorldRadius</c>, NEVER <c>NucleusWorldRadius</c>:
        /// the latter reports 0 whenever a mode has declared the nucleus play geometry rather than a
        /// territorial claim (<c>NucleusIsControlZone = false</c>, which both Scramble and Astro
        /// League set), and this needs the shape, not the claim. Docs/ECOSYSTEM.md §25.1.
        /// </summary>
        AstroLeagueBoundary ResolveNucleusBoundary()
        {
            var cell = ResolveCell();
            if (cell == null) return null;

            float nucleus = cell.NucleusVisualWorldRadius;
            if (nucleus <= 1e-3f) return null;          // no nucleus here - nothing to bounce off

            Vector3 centre = cell.transform.position;
            bool outside = ResolveNucleusSide(cell, centre, nucleus);

            // Outside, the membrane is the far wall. Floored just clear of the nucleus so a cell
            // whose membrane read is missing or tiny still leaves the ball somewhere to be.
            float outer = outside
                ? Mathf.Max(nucleus * 1.2f, cell.MembraneRadius * CytoplasmOuterFraction())
                : nucleus;

            bool cached = _nucleusBoundary != null
                          && _nucleusBoundaryCell == cell
                          && _nucleusBoundaryOutside == outside
                          && Mathf.Approximately(_nucleusBoundaryNucleusRadius, nucleus)
                          && Mathf.Approximately(_nucleusBoundaryOuterRadius, outer);
            if (cached) return _nucleusBoundary;

            _nucleusBoundary = new AstroLeagueBoundary(
                AstroLeagueBoundaryShape.Sphere, centre,
                new Vector3(outer, outer, outer), outer,
                coreObstacleRadius: outside ? nucleus : 0f);
            _nucleusBoundaryCell = cell;
            _nucleusBoundaryOutside = outside;
            _nucleusBoundaryNucleusRadius = nucleus;
            _nucleusBoundaryOuterRadius = outer;
            return _nucleusBoundary;
        }

        /// <summary>
        /// Which side of the nucleus the ball is playing on — read from position ONCE, then STICKY
        /// behind a dead band, because a bare per-tick position test would let the containment
        /// defeat itself. Containment runs BEFORE the physics step, so a ball can legitimately end
        /// a tick slightly past the wall it was just reflected off; re-classifying on that would
        /// flip a court ball to cytoplasm mode, which EJECTS it instead of pulling it back, and the
        /// court would leak balls at exactly the moment it was working.
        ///
        /// The band is the largest a ball can be past the surface WITHOUT having genuinely left:
        /// its own radius (containment parks its centre one radius short of the wall) plus one
        /// tick of travel at top speed. So the only thing that ever flips the side is a real
        /// crossing — which in practice means a nucleus release, whose containment grace is
        /// precisely the window that lets the strike carry the ball across.
        ///
        /// Note the two regimes are self-reinforcing once set: inside, ContainSphere holds the ball
        /// at most `nucleus - r` and it can never reach `+band`; outside, ContainCore holds it at
        /// least `nucleus + r` and it can never reach `-band`.
        /// </summary>
        bool ResolveNucleusSide(Cell cell, Vector3 centre, float nucleus)
        {
            float distance = Vector3.Distance(rb.position, centre);

            // A different cell is a different nucleus — re-read rather than carrying the old side.
            if (!_nucleusSideResolved || _nucleusBoundaryCell != cell)
            {
                _outsideNucleus = distance > nucleus;
                _nucleusSideResolved = true;
                return _outsideNucleus;
            }

            float band = BallWorldRadius() + settings.maxSpeed * Time.fixedDeltaTime;
            if (_outsideNucleus && distance < nucleus - band) _outsideNucleus = false;
            else if (!_outsideNucleus && distance > nucleus + band) _outsideNucleus = true;
            return _outsideNucleus;
        }

        float CytoplasmOuterFraction() =>
            settings != null ? Mathf.Clamp(settings.cytoplasmOuterFraction, 0.1f, 1f) : 0.95f;

        /// <summary>
        /// Server: keep the ball inside its containment by reflecting its velocity off the walls and
        /// clamping its position (no collider, no decay) - flat polytope faces preserve the wall-parallel
        /// component (the bank), curved shapes reflect radially. Runs once per server tick after the
        /// speed cap. Fires the shared wall juice at the contact point on a real bounce.
        ///
        /// A mode's installed court wins outright; otherwise the ball rides its own cell's nucleus.
        /// </summary>
        void ContainWithinBoundary()
        {
            var boundary = _courtBoundary ?? ResolveNucleusBoundary();
            if (boundary == null) return; // no court installed and no nucleus in reach

            Vector3 pos = rb.position;
            Vector3 vel = rb.linearVelocity;
            // wallRestitution, not ballBounciness: a carom LOSES energy (that is what stops the ball
            // pinballing forever), while a vessel strike stays fully elastic so the sword still fires it.
            if (!boundary.Contain(ref pos, ref vel, BallWorldRadius(), settings.wallRestitution,
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
        ///   1. Anti-clip - depenetrate the ball out of the hull (EjectBallFromPoint only acts while
        ///      overlapping), every contact frame. The hull can't pass through the ball even if the
        ///      pilot keeps driving in, and even for trigger-only ships with no physics depenetration.
        ///      The ONE exception is an EMBEDDED ball, which is pinned: it is depenetrated after the
        ///      strike that frees it, never against the pin (see the note on the eject itself).
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

            // Where the anti-clip depenetration pushes off, and with what clearance. Captured so
            // the hull and blade branches can share one call site; it is applied unconditionally,
            // to a studding ball exactly as to any other, because a studding ball is an ordinary
            // body and nothing writes its position back (see EmbedOnNucleusServer).
            Vector3 ejectOrigin;
            float ejectClear;

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
                ejectOrigin = contactPoint;
                ejectClear = settings.bladeClearRadius;
            }
            else
            {
                ejectOrigin = root.position;
                ejectClear = settings.vesselClearRadius;
                strikerVelocity = ResolveStrikerVelocity(vessel);
            }

            // ── The held-drift grapple (SCARAB.md §4.7) ────────────────────────────────────
            // The HOLDER's own hull neither strikes nor depenetrates the ball it is holding: its
            // pose is on a parametric orbit around the ball, so an eject here would shove the
            // ball out from under an orbit that immediately re-centres on it. And a HULL contact
            // (never a blade, never the skim field, which already returned above) from an armed
            // Scarab that is not yet holding anything is the grab itself — the entry velocity
            // and contact point become the orbit, and the strike that would otherwise launch the
            // ball never happens. Both tests are one component lookup on a cooldown-paced path.
            if (blade == null && root.TryGetComponent(out ScarabBallGrapple grapple))
            {
                if (IsGrappledBy(grapple)) return;
                if (grapple.TryBeginServer(this, strikerVelocity)) return;
            }

            EjectBallFromPoint(ejectOrigin, ejectClear); // anti-clip every frame - independent of the bounce/strike gating

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
        /// Guarantees the ball never overlaps what struck it: if the ball centre is closer than
        /// (ball radius + <paramref name="clearRadius"/>) to <paramref name="origin"/>, push it
        /// straight out to that distance. With the ≥1x launch speed this keeps the ball ahead of
        /// the vessel, so the vessel mesh can't clip through it - including the trigger-only ships
        /// that have no physical depenetration barrier. Server position is republished immediately
        /// so peers see the ejected position without waiting for the next tick.
        ///
        /// A HULL hit passes the vessel root with <c>vesselClearRadius</c>; a BLADE hit passes the
        /// point on the sword's centreline nearest the ball with the blade's own (much smaller)
        /// clearance, because pushing off the vessel ROOT cannot protect a 30-120 unit sword: at a
        /// tip strike the ball is already far outside the hull's clear radius and the check would
        /// no-op while the blade sweeps straight through it.
        ///
        /// NEVER call it on a ball that is still EMBEDDED. The server re-asserts a pinned ball's
        /// anchor every physics step, so the push is undone on the next tick and the ball reads as
        /// jumping out of the nucleus surface and snapping back for as long as a hull overlaps it.
        /// <see cref="VesselContact"/> defers it until the strike has un-pinned the ball.
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

            // NOTHING HERE KNOWS ABOUT THE NUCLEUS, and that is the design. A ball studding the
            // nucleus surface is an ordinary body at rest, so it takes the elastic moving-paddle
            // bounce, the arcade pop, the off-centre torque and the feedback beat below exactly as
            // any resting ball does; ServerFixedUpdate then NOTICES that it moved and reports the
            // release. This replaced a short-circuit that carried a SECOND impulse model for the
            // embedded case (the striker's speed along the striker's heading, floored at
            // ballRestSpeed, no pop, no spin, no strike RPC) — two models for one contact is one
            // model too many. The steal above already ran, so a dash can take an enemy's seeded
            // ball and knock it loose in the same contact.
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

            if (deliberate)
            {
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

            // NOTHING HERE KNOWS ABOUT THE NUCLEUS EITHER. A studding ball is a dynamic body, so
            // the velocity write below actually moves it and ServerFixedUpdate reports the release
            // — which is what makes the SCARAB'S DASH work on a seeded ball. Its reach onto a ball
            // it does not physically touch is the cavitation blast (ScarabJukeController.OnJukeFired
            // → ScarabCavitationBlast → ExplosionImpactor → here); while the ball was pinned and
            // KINEMATIC, every line below wrote into a body that does not integrate, so the punch
            // — and every other blast in the game — passed straight through it.
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

        /// <summary>
        /// A ball is LOOSE IN A CELL when it is neither hidden nor still embedded in the nucleus.
        /// The embedded exclusion is the same ruling the nucleus overload makes: a ball studding
        /// the core is the Scarab's seeding ABILITY, not yet in play, and it has its own cap
        /// (<c>ScarabNucleusFieldConfigSO.nucleusEntryLimit</c>). Knocking one loose is precisely
        /// what makes it ENTER the cell, which is the event this rule is about.
        /// </summary>
        public bool IsLooseIn(Cell cell) =>
            cell != null && !n_Hidden.Value && !n_Embedded.Value
            && cell.ContainsPosition(transform.position);

        /// <summary>How many balls are loose in <paramref name="cell"/> right now, ANY domain.</summary>
        public static int CountLooseInCell(Cell cell)
        {
            if (cell == null) return 0;
            int n = 0;
            for (int i = 0; i < Live.Count; i++)
            {
                var ball = Live[i];
                if (ball != null && ball.IsLooseIn(cell)) n++;
            }
            return n;
        }

        /// <summary>
        /// Server: detonate every ball loose in <paramref name="cell"/> - the ball that triggered
        /// the overload included, because by the time this runs it is loose like any other and
        /// needs no special case. Returns how many went up.
        /// </summary>
        public static int DetonateAllLooseInCellServer(Cell cell, float radiusScale)
        {
            if (cell == null) return 0;
            var doomed = new List<AstroLeagueBall>();
            for (int i = 0; i < Live.Count; i++)
            {
                var ball = Live[i];
                if (ball != null && ball.IsLooseIn(cell)) doomed.Add(ball);
            }
            for (int i = 0; i < doomed.Count; i++)
                doomed[i].DetonateWithRadiusServer(radiusScale);   // no-ops off the server on its own
            return doomed.Count;
        }

        /// <summary>
        /// Server: detect the moment this ball ENTERS a cell, and overload that cell if its arrival
        /// is the one that crosses <see cref="cellBallLimit"/>.
        ///
        /// Entry is a TRANSITION, not a position test, which is why the cell is cached: a ball
        /// already sitting in a crowded cell must not re-trigger every poll. All four ways in are
        /// covered by the same edge - forged there by a skimmer or a blast, knocked loose from the
        /// nucleus, un-hidden after a goal, or simply drifting across the membrane - because each
        /// of them ends with a ball that was not loose in this cell last poll and is now.
        /// </summary>
        /// <returns>true when this ball's arrival overloaded the cell — it has detonated and is
        /// very likely DESPAWNED, so the caller must stop touching it this tick.</returns>
        bool TickCellMembershipServer()
        {
            if (cellBallLimit <= 0) return false;
            // Only a peer that can actually DETONATE may count. DetonateWithRadiusServer requires
            // IsServer, and CellOverload_ClientRpc requires a SPAWNED object, so in a no-network
            // local session (the freestyle toys mint balls with no NetworkManager) this rule would
            // otherwise announce an overload it could not carry out. Nothing overloads there, the
            // same way the nucleus overload does nothing there.
            if (!IsSpawned || !IsServer) return false;
            if (Time.time < _nextCellPoll) return false;
            _nextCellPoll = Time.time + Mathf.Max(0.02f, cellPollSeconds);

            Cell now = (n_Hidden.Value || n_Embedded.Value)
                ? null
                : Cell.FindCellContaining(transform.position);

            if (now == _looseInCell) return false;
            _looseInCell = now;
            if (now == null) return false;

            int loose = CountLooseInCell(now);
            if (loose < cellBallLimit) return false;

            // ANNOUNCE FIRST, THEN DETONATE — the order is load-bearing, not style. A forged ball
            // is DESPAWNED by its own detonation (DetonateWithRadiusServer → DespawnIfForged), and
            // this ball is one of the doomed, so sending the RPC afterwards would be sending it
            // from a NetworkObject that no longer exists. `loose` is the same predicate over the
            // same list in the same frame as the detonation loop, so it is the honest count.
            //
            // The DETONATIONS are already replicated one ball at a time (each runs its own
            // Detonate_ClientRpc + domain blast). This announces the OVERLOAD itself, so every peer
            // can react to it as one event rather than inferring it from a burst of unrelated
            // bursts — which is what lets the mode post a single court-wide notice instead of N.
            CellOverload_ClientRpc(now.transform.position, loose);
            CSDebug.LogVerbose(CSLogChannel.ScarabNucleus,
                $"[AstroLeagueBall] Cell overload — {loose} loose ball(s) detonating in {now.name} " +
                $"(limit {cellBallLimit}, any domain).");

            DetonateAllLooseInCellServer(now, cellOverloadRadiusScale);
            return true;
        }

        [ClientRpc]
        void CellOverload_ClientRpc(Vector3 at, int count) => OnCellOverload?.Invoke(at, count);

        /// <summary>Server: detonate at the goal mouth - burst + shake on every peer, then hide until kickoff.</summary>
        public void DetonateServer()
        {
            if (!IsServer) return;
            Detonate_ClientRpc(transform.position, 1f);
            SetHiddenServer(true);
        }

        /// <summary>
        /// Server: SEED this ball into the nucleus surface at <paramref name="surfacePoint"/>, with
        /// <paramref name="outward"/> pointing away from the cell centre.
        ///
        /// IT IS PLACED, NOT PINNED. The ball is left a completely ordinary live body — dynamic,
        /// contactable, blastable, depenetrable — that simply happens to be at rest inside a shell
        /// with no collider in it. The only thing suspended is its containment, because the seed
        /// point is on the wrong side of both the court and the cytoplasm volumes. Everything else
        /// about it is a ball, so every force in the game reaches it with no force having to know
        /// this ability exists.
        ///
        /// ONE WAY. A ball that has ever been dislodged refuses to be seeded again
        /// (<c>_releasedFromNucleus</c>): studding the shell is a state the world puts a ball INTO,
        /// never one it can fall back into, so a loose ball can never quietly stop behaving like a
        /// ball because it drifted through the wrong place.
        ///
        /// It keeps its domain and its ownership lock, so a seeded ball is already its Scarab's, and an
        /// enemy who wants it must dash-steal it exactly like any other ball.
        /// </summary>
        public void EmbedOnNucleusServer(Vector3 surfacePoint, Vector3 outward)
        {
            if (!IsServer || _releasedFromNucleus) return;

            _embedAnchor = surfacePoint;
            _embedOutward = outward.sqrMagnitude > 1e-6f ? outward.normalized : Vector3.up;

            SetHiddenServer(false);
            SetFrozenServer(false);   // a studding ball is a LIVE body, never frozen and never pinned
            n_Embedded.Value = true;

            SetSpawnPosition(surfacePoint);
            transform.position = surfacePoint;
            rb.position = surfacePoint;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            if (IsSpawned)
            {
                n_Position.Value = surfacePoint;
                n_Velocity.Value = Vector3.zero;
                n_AngularVelocity.Value = Vector3.zero;
            }
            _lastPrismScanPos = surfacePoint;
            if (trail != null) trail.Clear();
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
            float scale = Mathf.Max(0.1f, radiusScale);
            Detonate_ClientRpc(transform.position, scale);
            SpawnDomainBlast(scale);
            SetHiddenServer(true);
            DespawnIfForged();   // NOT ExpireServer: that would fire a second, unscaled burst
        }

        /// <summary>
        /// A DOMAIN explosion where the ball died: coloured by the ball's own domain, and carrying
        /// that domain into the standard blast rules — so own-domain prisms take a temporary shield
        /// (the no-perceived-clipping rule) while other domains are destroyed. None of that is new
        /// behaviour; it is what <c>ExplosionImpactor</c> already does with
        /// <c>affectSelf = false, destructive = true</c>, which every shipped blast prefab authors.
        ///
        /// The blast radius is <paramref name="radiusScale"/>× the ball's own radius. MaxScale is a
        /// DIAMETER (the explosion mesh is a unit sphere grown by localScale), hence the ×2.
        /// </summary>
        void SpawnDomainBlast(float radiusScale)
        {
            if (settings == null) return;
            var prefabs = settings.detonationExplosionPrefabs;
            if (prefabs == null || prefabs.Length == 0) return;   // unwired = burst only, by design

            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain = n_LastHitDomain.Value,
                Vessel = null,
                // No vessel made this blast, so nothing may be credited to a pilot for it.
                AnnonymousExplosion = true,
                MaxScale = 2f * radiusScale * BallWorldRadius(),
                OverrideMaterial = ResolveDomainBlastMaterial(n_LastHitDomain.Value),
                SpawnPosition = transform.position,
                SpawnRotation = Quaternion.identity,
            };

            // No DI container: the ball is spawned through NetworkObject.Spawn, not through an
            // injecting call site, so it has none to hand on. Safe — every use of the explosion's
            // injected gameData is null-guarded — at the cost of the blast not auto-cancelling on
            // a turn end or replay reset that lands inside its ~3s life.
            ExplosionHelper.CreateExplosion(prefabs, init, null);
        }

        /// <summary>
        /// The AOE material for a domain, off the live theme data — the same per-domain set every
        /// vessel's own blast material comes from, so a ball's explosion cannot drift from the
        /// palette. Null (the prefab's authored material) when the theme has no set for the domain,
        /// which is the correct fallback for the neutral Blue sentinel.
        /// </summary>
        Material ResolveDomainBlastMaterial(Domains domain)
        {
            var theme = gameData != null ? gameData.ThemeManagerData : null;
            if (theme?.TeamMaterialSets == null) return null;
            return theme.TeamMaterialSets.TryGetValue(domain, out var set) && set != null
                ? set.AOEExplosionMaterial
                : null;
        }

        /// <summary>
        /// Server: detonate EVERY live ball at <paramref name="radiusScale"/>× its own radius. The
        /// whole-world variant of <see cref="DetonateAllLooseInCellServer"/>, kept for the nucleus
        /// overload's <c>detonateAllLiveBalls</c> option so the two "too many balls" events can
        /// never drift into different-looking detonations.
        /// Snapshots first, because detonating despawns and that mutates <see cref="Live"/>.
        /// </summary>
        public static int DetonateAllLiveServer(float radiusScale)
        {
            var doomed = new List<AstroLeagueBall>(Live);
            int n = 0;
            for (int i = 0; i < doomed.Count; i++)
            {
                var ball = doomed[i];
                if (ball == null) continue;
                ball.DetonateWithRadiusServer(radiusScale);   // no-ops off the server on its own
                n++;
            }
            return n;
        }

        /// <summary>Server: freeze the ball in place at center (kickoff count-in). Velocity is cleared.</summary>
        public void SetFrozenServer(bool frozen)
        {
            if (!IsServer) return;
            if (frozen) ReleaseGrapplerServer();
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
            if (hidden) ReleaseGrapplerServer();
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
            // A morph stand-in is the OTHER reason this ball may not be drawing itself, and the two
            // compose: hidden gameplay state OR a crystal currently drawing the body. Folding it in
            // here rather than at the call sites is what stops a replicated n_Hidden echo — which
            // arrives on its own schedule — from switching the renderer back on underneath a live
            // morph.
            bool draw = !hidden && !_morphStandIn;
            if (ballRenderer != null) ballRenderer.enabled = draw;
            if (ballLight != null) ballLight.enabled = draw;
            if (trail != null)
            {
                trail.emitting = draw;
                if (!draw) trail.Clear();
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
            _nucleusSideResolved = false;   // teleported: re-read which side of the nucleus it is on
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
            _nucleusSideResolved = false;   // teleported: re-read which side of the nucleus it is on
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
