using CosmicShore.Data;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's held-drift BALL GRAPPLE (design: R_VesselActions/SCARAB.md §4.7). With the
    /// drift trigger fully held, a hull that touches a ball STICKS to it and swings around it —
    /// the orbit's plane and speed come from the hull's velocity and where it struck, so a
    /// glancing contact whips round fast and a dead-centre one just holds. Letting go of the
    /// drift FLINGS the ball the way the hull was swinging at that instant. A moving ball is
    /// carried, not stopped: the hull rides the orbit around wherever the ball is, the ball keeps
    /// its own linear velocity (only its spin follows the hull), and it is redirected only on
    /// release. Where you release is the whole skill, and it works equally to bat an enemy ball
    /// off your hoop and to sling your own through one.
    ///
    /// AUTHORITY IS SPLIT THE ONLY WAY IT CAN BE. The ball is server-simulated and the vessel's
    /// pose is owner-authoritative, so neither end can own the pair: the SERVER decides that a
    /// grapple began (it sees the physics contact, in <c>AstroLeagueBall.VesselContact</c>) and
    /// flings the ball; the OWNER writes the vessel's pose. What lets them agree without a per-tick
    /// exchange is that the orbit is PARAMETRIC (<see cref="ScarabGrappleOrbit"/>): six numbers and
    /// the shared clock reproduce the hull's position and tangent on any peer, so the server's
    /// fling and the owner's exit velocity are the same vector to within one half-RTT of phase.
    /// The owner's outbound signal is "my drift is fully held" (<see cref="n_Armed"/>) plus a
    /// monotonic RELEASE COUNTER (<see cref="n_ReleaseSeq"/>), both owner-write. The server ARMS on
    /// the bool and ends on either — the level, which is also what releases an owner who
    /// disconnects mid-hold, or the counter, which is what carries a hold that dropped and returned
    /// inside a single tick and would otherwise be coalesced away before the server ever sampled
    /// it. Nothing is requested; the variables are the message. Both halves of that, and the
    /// owner's own attach/detach level, are <see cref="ScarabGrappleLatch"/>.
    ///
    /// What it deliberately is NOT: not a kinematic pin (the ball stays a live body — a rival's
    /// hull, blast or juke-steal reaches a held ball exactly as it reaches a free one, and the
    /// grappler simply follows), not a timer (the drift release is the only exit; an opponent's
    /// steal is the counter-play), and not an ownership conversion (the juke-dash stays the one
    /// sanctioned steal; a grab and a fling are TOUCHES for the arming ledger, so flinging an
    /// enemy ball disarms it and slinging your own re-arms it). Human pilots only for now: an AI
    /// drift is binary and would grab every ball it brushed and never let go (SCARAB.md §15).
    /// </summary>
    [RequireComponent(typeof(ScarabJukeController))]
    public class ScarabBallGrapple : NetworkBehaviour
    {
        [Header("Hold")]
        [Tooltip("Gap kept between the hull collider and the ball surface while held (world " +
                 "units). The orbit radius is ball radius + hull radius + this.")]
        [SerializeField, Min(0f)] float holdClearance = 1.5f;
        [Tooltip("Hull radius used only if the hull collider cannot be measured (the shipped " +
                 "Scarab hull is a single 4.5-unit sphere).")]
        [SerializeField, Min(0.1f)] float fallbackHullRadius = 4.5f;
        [Tooltip("Spin the held ball at this fraction of the orbit's angular velocity, so it rolls " +
                 "with the hull circling it. Cosmetic: the ball's angular velocity carries no " +
                 "gameplay. 0 = the ball does not turn while held.")]
        [SerializeField, Range(0f, 2f)] float ballSpinFraction = 1f;
        [Tooltip("Pause the trail spawner while holding. The orbit would otherwise lay a ring of " +
                 "prisms through the ball, which the ball then eats or shields every tick. Not " +
                 "creating mass is allowed; nothing is removed.")]
        [SerializeField] bool pauseTrailWhileHolding = true;

        [Header("Release")]
        [Tooltip("The ball leaves at the hull's orbital speed × this. Above 1 so the ball outruns " +
                 "the hull that threw it and the two separate cleanly. Clamped to the ball's own " +
                 "maxSpeed on the ball side.")]
        [SerializeField, Min(0f)] float flingMultiplier = 1.6f;
        [Tooltip("Seconds after a release before this hull can grab a ball again — so a fling " +
                 "cannot re-stick to the ball it just threw if the drift is re-buried at once.")]
        [SerializeField, Min(0f)] float regrappleCooldownSeconds = 0.6f;

        [Header("Camera")]
        [Tooltip("Hold the camera on the BALL while grappling, so the hull spins in front of a " +
                 "still frame instead of dragging the view around its orbit. Off = the camera " +
                 "follows the spinning vessel (fast, and reliably nauseating).")]
        [SerializeField] bool holdCameraOnBall = true;
        [Tooltip("Seconds to ease the camera onto the ball when a grapple begins.")]
        [SerializeField, Min(0.01f)] float cameraHoldBlendSeconds = 0.3f;
        [Tooltip("Seconds to ease the camera back behind the vessel on release. Slightly longer " +
                 "than the entry: the exit is a throw the player wants to watch land.")]
        [SerializeField, Min(0.01f)] float cameraReleaseBlendSeconds = 0.5f;
        [Tooltip("Extra distance added to the camera's hold distance, so the whole orbit fits in " +
                 "frame. 0 keeps exactly the distance the pilot flew in at.")]
        [SerializeField, Min(0f)] float cameraHoldExtraDistance = 0f;
        [Tooltip("Seconds to ease the camera into the plane the hull is swinging in, so its own x " +
                 "axis lies along the orbit axis and the swing reads as one clean arc.")]
        [SerializeField, Min(0.01f)] float cameraAlignBlendSeconds = 0.35f;

        [Header("Aim (left stick, while held)")]
        [Tooltip("Degrees per second the camera swings around the ball, about the orbit axis, at " +
                 "full stick. PITCH — the same stick direction that pitches the ship in flight, " +
                 "and purely a vantage: it changes nothing about the throw.")]
        [SerializeField, Min(0f)] float cameraOrbitDegreesPerSecond = 120f;
        [Tooltip("Degrees per second the ORBIT AXIS rolls about the camera's view direction at " +
                 "full stick. YAW — this is GAMEPLAY: it re-aims the plane the hull swings in, so " +
                 "it re-aims the throw. The camera's roll follows for free, since its x axis is " +
                 "pinned to that same axis.")]
        [SerializeField, Min(0f)] float aimRollDegreesPerSecond = 90f;
        [Tooltip("Stick magnitude below which the aim does not move. Guards a worn stick from " +
                 "slowly rolling a throw the pilot is trying to hold steady.")]
        [SerializeField, Range(0f, 0.5f)] float aimDeadzone = 0.15f;

        /// <summary>
        /// Everything a peer needs to ride the orbit. <c>BallId 0</c> means no grapple. Sent as
        /// ONE variable so a peer never sees a ball id without its orbit or vice versa.
        /// </summary>
        public struct GrappleState : INetworkSerializable
        {
            public ulong BallId;
            public ScarabGrappleOrbitState Orbit;

            public bool IsActive => BallId != 0UL && Orbit.IsValid;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref BallId);
                serializer.SerializeValue(ref Orbit.Axis);
                serializer.SerializeValue(ref Orbit.Radial0);
                serializer.SerializeValue(ref Orbit.Radius);
                serializer.SerializeValue(ref Orbit.AngularSpeed);
                serializer.SerializeValue(ref Orbit.StartTime);
            }
        }

        // SERVER-write: the live grapple, or none. OWNER-write: "my drift is fully held".
        readonly NetworkVariable<GrappleState> n_State =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        readonly NetworkVariable<bool> n_Armed =
            new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // OWNER-write, and the reason a bool alone is not enough: a NetworkVariable carries the
        // value it holds when the tick serialises, so a hold that drops and returns between two
        // ticks never existed as far as the server is concerned. This only ever counts UP, on the
        // falling edge the owner's own frame can see.
        readonly NetworkVariable<uint> n_ReleaseSeq =
            new(0u, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>
        /// The pilot's re-aim of the orbit, STAMPED with the grapple it belongs to. The stamp is
        /// the point: the aim is owner-written and the state is server-written, so without it a
        /// tilt from the previous grapple can still be in flight when the server flings the next
        /// one — and the throw would leave along an axis the pilot aimed at a different ball.
        /// Comparing <see cref="ForStartTime"/> for exact equality is safe because it is the
        /// server's own number, replicated verbatim rather than recomputed.
        /// </summary>
        public struct OrbitAim : INetworkSerializable
        {
            public double ForStartTime;
            public Quaternion Tilt;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref ForStartTime);
                serializer.SerializeValue(ref Tilt);
            }
        }

        readonly NetworkVariable<OrbitAim> n_Aim =
            new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Local mirrors for the non-networked spawn path (IsSpawned false), where a
        // NetworkVariable cannot be written — the ball's own n_SizeScale idiom.
        GrappleState _localState;
        bool _localArmed;
        uint _localReleaseSeq;
        OrbitAim _localAim;

        IVesselStatus _status;
        ScarabJukeController _juke;
        VesselImpactor _impactor;
        AstroLeagueBall _ball;            // resolved from the state's BallId on every peer that needs it
        bool _following;                  // OWNER: the transformer's pose is ours right now
        bool _trailPaused;
        bool _armedLastFrame;             // OWNER: for the falling edge that bumps the counter
        bool _releasedThisGrapple;        // OWNER: sticky "I let this one go", cleared on a state change
        uint _releaseSeqAtGrab;           // SERVER: the owner's counter when this grapple began
        double _regrappleReadyAt = double.NegativeInfinity;

        /// <summary>The live grapple as every peer sees it.</summary>
        public GrappleState State => IsSpawned ? n_State.Value : _localState;

        /// <summary>True while this hull holds a ball (any peer).</summary>
        public bool IsGrappling => State.IsActive;

        /// <summary>The pilot's drift is fully held (owner-written, server-read): the grapple
        /// ARMS on it and RELEASES when it drops.</summary>
        public bool IsArmed => IsSpawned ? n_Armed.Value : _localArmed;

        /// <summary>How many times this pilot has let a held drift go (owner-written,
        /// server-read). Monotonic, so the server can tell "still held" from "let go and grabbed
        /// again" without having sampled the frames in between.</summary>
        public uint ReleaseSeq => IsSpawned ? n_ReleaseSeq.Value : _localReleaseSeq;

        /// <summary>The pilot's stamped re-aim of the current orbit.</summary>
        public OrbitAim Aim => IsSpawned ? n_Aim.Value : _localAim;

        /// <summary>
        /// The orbit as everything downstream must read it: the server's orbit with the pilot's
        /// aim applied, and ONLY when that aim was stamped for this grapple. Every consumer —
        /// the owner's pose, the ball's held spin, the release fling — goes through here, so the
        /// hull the pilot is watching and the throw the server produces cannot disagree.
        /// </summary>
        public ScarabGrappleOrbitState AimedOrbit
        {
            get
            {
                var orbit = State.Orbit;
                var aim = Aim;
                return aim.ForStartTime == orbit.StartTime
                    ? ScarabGrappleOrbit.Aimed(orbit, aim.Tilt)
                    : orbit;
            }
        }

        /// <summary>The ball this hull is holding, or null. Server: authoritative; owner: resolved
        /// from the replicated id.</summary>
        public AstroLeagueBall HeldBall => IsGrappling ? _ball : null;

        /// <summary>Raised on every peer when a grapple begins (true) or ends (false) — the
        /// animation/HUD hook. Carries no authority.</summary>
        public event System.Action<bool> OnGrappleChanged;

        bool ActsAsServer => !IsSpawned || IsServer;

        double Now => IsSpawned && NetworkManager != null
            ? NetworkManager.ServerTime.Time
            : Time.timeAsDouble;

        void Awake()
        {
            _status = GetComponent<VesselStatus>();
            _juke = GetComponent<ScarabJukeController>();
            _impactor = GetComponent<VesselImpactor>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            n_State.OnValueChanged += HandleStateChanged;
            // A late-spawned replica (or a host whose own write already landed) must not miss a
            // live grapple that began before this callback was wired.
            if (n_State.Value.IsActive) HandleStateChanged(default, n_State.Value);
        }

        public override void OnNetworkDespawn()
        {
            n_State.OnValueChanged -= HandleStateChanged;
            base.OnNetworkDespawn();
        }

        void Update()
        {
            if (_status == null) return;

            if (_status.Player is { IsLocalPilot: true })
                OwnerUpdate();

            if (ActsAsServer)
                ServerUpdate();
        }

        // ------------------------------------------------------------------ OWNER

        void OwnerUpdate()
        {
            // The signals the server needs from the pilot. Never armed under autopilot: an
            // AI drift is binary (the non-gamepad trigger sum) and would read as fully held for
            // the whole ability, so the AI would grab every ball it touched and never let go.
            bool armed = !_status.AutoPilotEnabled && _juke != null && _juke.IsDriftFullyHeld;
            uint seq = ScarabGrappleLatch.AdvanceReleaseSeq(ReleaseSeq, _armedLastFrame, armed);
            if (seq != ReleaseSeq) _releasedThisGrapple = true;
            WriteReleaseSeqSafe(seq);
            _armedLastFrame = armed;
            WriteArmedSafe(armed);

            // ATTACHMENT IS A LEVEL, ASKED EVERY FRAME — not an edge taken off the state
            // replication callback. An edge-driven attach against a level-driven detach means any
            // frame that trips a detach without n_State also changing strands the hull off the
            // orbit for the rest of the grapple; asked this way, a momentarily missing ball or
            // transformer costs one frame and heals itself.
            var state = State;
            if (state.IsActive && _ball == null) _ball = ResolveBall(state.BallId);
            bool ballUsable = _ball != null && !_ball.IsHidden && !_ball.IsFrozen;
            var transformer = _status.VesselTransformer;

            if (!ScarabGrappleLatch.ShouldFollow(
                    armed, state.IsActive, ballUsable, transformer, _releasedThisGrapple))
            {
                // Release is LOCAL first — the hull answers the trigger on the frame it lifts, with
                // no round-trip — and the server reaches the same conclusion through n_Armed and
                // n_ReleaseSeq, and flings.
                EndFollow();
                return;
            }

            BeginFollow();
            if (!_following) return;

            var orbit = UpdateAim(state.Orbit);

            double now = Now;
            Vector3 ballPos = _ball.transform.position;
            Vector3 radial = ScarabGrappleOrbit.RadialAt(orbit, now);
            Vector3 relative = ScarabGrappleOrbit.RelativeVelocityAt(orbit, now);
            Vector3 position = ballPos + radial * orbit.Radius;
            Vector3 velocity = _ball.Velocity + relative;
            Quaternion rotation = ScarabGrappleOrbit.PoseRotation(radial, relative, transform.forward);

            transformer.SetExternalMotion(position, rotation, velocity);
        }

        /// <summary>
        /// The pilot's two controls while held, read off the SAME stick that flies the ship — the
        /// hull is externally driven, so the left stick is doing nothing else and needs no mode.
        /// Both use the flight expression verbatim (pitch <c>-y</c> about the camera's x, roll
        /// <c>-x</c> about the camera's z), so the muscle memory carries over rather than being
        /// re-learned.
        ///
        /// PITCH swings the camera around the ball, about the orbit axis. That is the one rotation
        /// the alignment leaves free (rotating about the axis maps the plane to itself), and it is
        /// purely a vantage — the throw is untouched.
        ///
        /// YAW rolls the ORBIT AXIS about the camera's own view direction, which is GAMEPLAY: the
        /// plane the hull swings in IS the plane it will be thrown in, so this is how a pilot aims
        /// a held ball. The camera's roll comes along for free, because its x axis is pinned to
        /// that axis — one rotation, stated once. Note the fling stays the ORBIT TANGENT and does
        /// not add the plane's own rate of turn: "aim the plane, throw along the swing" is the rule
        /// a player can hold, and a precession term would make a throw depend on how fast they
        /// happened to be rolling at the instant they let go.
        /// </summary>
        ScarabGrappleOrbitState UpdateAim(in ScarabGrappleOrbitState orbit)
        {
            // A new grapple starts un-aimed. Self-healing off the stamp rather than hooked to the
            // state callback, so a missed edge cannot leave the last ball's aim on this one.
            var aim = Aim;
            if (aim.ForStartTime != orbit.StartTime)
            {
                aim = new OrbitAim { ForStartTime = orbit.StartTime, Tilt = Quaternion.identity };
                WriteAimSafe(aim);
            }

            Vector2 stick = _status.InputStatus?.EasedLeftJoystickPosition ?? Vector2.zero;
            if (stick.magnitude < aimDeadzone) stick = Vector2.zero;

            var camera = CameraManager.Instance;
            Vector3 view = camera ? camera.PlayerAnchorViewDirection : Vector3.zero;

            // YAW → roll the axis about the view direction. Skipped outright when there is no
            // camera to ask: rolling about a fallback axis would aim the throw somewhere the
            // pilot cannot see.
            float roll = -stick.x * aimRollDegreesPerSecond * Time.deltaTime;
            if (Mathf.Abs(roll) > 1e-5f && view.sqrMagnitude > 1e-6f)
            {
                Quaternion tilt = Quaternion.AngleAxis(roll, view.normalized) * SafeTilt(aim.Tilt);
                aim = new OrbitAim { ForStartTime = orbit.StartTime, Tilt = Quaternion.Normalize(tilt) };
                WriteAimSafe(aim);
            }

            var aimed = ScarabGrappleOrbit.Aimed(orbit, SafeTilt(aim.Tilt));

            if (camera)
            {
                // Re-stated every frame because the axis MOVES: this is what rolls the camera.
                camera.SetPlayerAnchorAlignmentAxis(aimed.Axis, cameraAlignBlendSeconds);
                camera.OrbitPlayerAnchorHold(-stick.y * cameraOrbitDegreesPerSecond * Time.deltaTime);
            }
            return aimed;
        }

        static Quaternion SafeTilt(Quaternion tilt)
            => ScarabGrappleOrbit.IsRotation(tilt) ? tilt : Quaternion.identity;

        void WriteArmed(bool armed)
        {
            if (IsSpawned)
            {
                if (n_Armed.Value != armed) n_Armed.Value = armed;
            }
            else _localArmed = armed;
        }

        void WriteAimSafe(OrbitAim aim)
        {
            if (IsSpawned && !IsOwner) return;
            if (IsSpawned) n_Aim.Value = aim;
            else _localAim = aim;
        }

        void WriteReleaseSeqSafe(uint seq)
        {
            if (IsSpawned && !IsOwner) return;
            if (IsSpawned)
            {
                if (n_ReleaseSeq.Value != seq) n_ReleaseSeq.Value = seq;
            }
            else _localReleaseSeq = seq;
        }

        void BeginFollow()
        {
            if (_following) return;
            var transformer = _status?.VesselTransformer;
            if (!transformer) return;

            _following = true;
            transformer.BeginExternalMotion();

            // The camera stops following the hull's rotation and holds on the BALL — the hull then
            // visibly orbits in front of a still frame, which is both what makes the release
            // timeable and what stops the spin reading as motion sickness. Local pilot only, which
            // BeginFollow already is.
            if (holdCameraOnBall && _ball && CameraManager.Instance)
                CameraManager.Instance.BeginPlayerAnchorHold(
                    _ball.transform, cameraHoldBlendSeconds, cameraHoldExtraDistance);

            if (pauseTrailWhileHolding && _status.VesselPrismController)
            {
                _status.VesselPrismController.SetSpawnerPaused(true);
                _trailPaused = true;
            }
        }

        void EndFollow()
        {
            if (!_following) return;
            _following = false;

            // Carry the orbital velocity out: the hull keeps swinging the way it was, which is
            // what makes the release read as a throw rather than a stop. The transformer replays
            // the velocity WE last wrote — reconstructing it here from Course × Speed would use
            // the PUBLISHED speed, which has already been through throttleMultiplier, so a live
            // impact debuff would be baked into the vessel's base speed and then applied again.
            var transformer = _status?.VesselTransformer;
            if (transformer) transformer.EndExternalMotion();

            if (holdCameraOnBall && CameraManager.Instance)
                CameraManager.Instance.EndPlayerAnchorHold(cameraReleaseBlendSeconds);

            if (_trailPaused && _status?.VesselPrismController)
                _status.VesselPrismController.SetSpawnerPaused(false);
            _trailPaused = false;
        }

        // ------------------------------------------------------------------ SERVER

        /// <summary>
        /// SERVER, from <c>AstroLeagueBall.VesselContact</c>: this hull just touched
        /// <paramref name="ball"/> with the drift fully held. Build the orbit from the contact
        /// and take the ball. Returns false when nothing was grabbed (not armed, still cooling
        /// from the last release, already holding, or the ball is taken/frozen/hidden).
        /// </summary>
        public bool TryBeginServer(AstroLeagueBall ball, Vector3 hullVelocity)
        {
            if (!ActsAsServer || ball == null || _status == null) return false;
            if (!IsArmed || IsGrappling) return false;
            if (Now < _regrappleReadyAt) return false;
            if (ball.IsHidden || ball.IsFrozen) return false;

            float hullRadius = ScarabCavitationBlast.MeasureHullRadius(_impactor);
            if (hullRadius <= 0f) hullRadius = fallbackHullRadius;
            float radius = ball.BallWorldRadius() + hullRadius + holdClearance;

            Domains domain = _status.Domain;
            string name = _status.PlayerName;
            if (!ball.TryBeginGrappleServer(this, domain, name)) return false;

            var orbit = ScarabGrappleOrbit.FromContact(
                ball.transform.position, ball.Velocity,
                transform.position, hullVelocity,
                radius, Now, transform.up);

            _ball = ball;
            // Anything the owner counts from here on is a release of THIS grapple.
            _releaseSeqAtGrab = ReleaseSeq;
            WriteState(new GrappleState
            {
                BallId = ball.IsSpawned ? ball.NetworkObjectId : ulong.MaxValue,
                Orbit = orbit,
            });

            if (CSDebug.IsVerbose(CSLogChannel.ScarabGrapple))
                CSDebug.LogVerbose(CSLogChannel.ScarabGrapple,
                    $"[ScarabGrapple] {name} grabbed ball {ball.name}: radius {radius:F1}, " +
                    $"orbital speed {ScarabGrappleOrbit.OrbitalSpeed(orbit):F1} u/s, " +
                    $"ball speed {ball.Velocity.magnitude:F1} u/s.");
            return true;
        }

        void ServerUpdate()
        {
            var state = State;
            if (!state.IsActive) return;

            if (_ball == null || _ball.IsHidden || _ball.IsFrozen || !_ball.IsGrappledBy(this))
            {
                // The ball died, was reset, or somebody else's authority took it (a hoop spent
                // it, an overload detonated it). Nothing to fling; just let go.
                EndServer(fling: false);
                return;
            }

            // The ball keeps its own linear velocity — only its spin follows the hull, about the
            // axis the pilot has aimed.
            _ball.HoldSpinServer(ScarabGrappleOrbit.BallSpin(AimedOrbit, ballSpinFraction));

            // The RELEASE: the owner's drift hold dropped — either it is down as we sample it (which
            // is also how an owner that disconnects mid-hold releases), or their release counter has
            // moved, which is the only way a hold that flickered off and back inside one tick
            // reaches us at all. Observed here rather than requested.
            if (ScarabGrappleLatch.ServerShouldRelease(IsArmed, _releaseSeqAtGrab, ReleaseSeq))
                EndServer(fling: true);
        }

        void EndServer(bool fling)
        {
            var state = State;
            if (!state.IsActive) return;

            if (fling && _ball != null && _ball.IsGrappledBy(this))
            {
                double now = Now;
                // The AIMED orbit: the pilot's yaw re-aims the plane, and the throw is along the
                // swing in that plane. Up to one half-RTT of the pilot's very last aim may not
                // have arrived — the same phase caveat the parametric orbit already carries.
                var aimed = AimedOrbit;
                Vector3 delta = ScarabGrappleOrbit.FlingVelocity(aimed, now, flingMultiplier);
                Vector3 spin = ScarabGrappleOrbit.BallSpin(aimed, ballSpinFraction);
                _ball.FlingServer(_status?.Vessel, delta, spin, _status?.Domain ?? Domains.Blue,
                                  _status?.PlayerName ?? string.Empty);

                if (CSDebug.IsVerbose(CSLogChannel.ScarabGrapple))
                    CSDebug.LogVerbose(CSLogChannel.ScarabGrapple,
                        $"[ScarabGrapple] released: fling {delta.magnitude:F1} u/s along {delta.normalized}.");
            }

            if (_ball != null) _ball.EndGrappleServer(this);
            _regrappleReadyAt = Now + regrappleCooldownSeconds;
            WriteState(default);
        }

        void WriteState(GrappleState state)
        {
            if (IsSpawned) n_State.Value = state;
            else
            {
                var previous = _localState;
                _localState = state;
                HandleStateChanged(previous, state);
            }
        }

        // ------------------------------------------------------------------ EVERY PEER

        void HandleStateChanged(GrappleState previous, GrappleState current)
        {
            // Deliberately does NOT touch _following: OwnerUpdate is the single owner of that, as
            // a per-frame level. This callback only resolves the ball and announces the change —
            // and clears the sticky release, because a new state is a new grapple.
            _releasedThisGrapple = false;
            if (current.IsActive)
            {
                // The server set _ball when it began the grapple; every other peer resolves it
                // from the replicated id.
                if (!ActsAsServer || _ball == null)
                    _ball = ResolveBall(current.BallId) ?? _ball;
                if (!previous.IsActive) OnGrappleChanged?.Invoke(true);
            }
            else
            {
                if (_status?.Player is { IsLocalPilot: true }) EndFollow();
                if (!ActsAsServer) _ball = null;
                if (previous.IsActive) OnGrappleChanged?.Invoke(false);
            }
        }

        AstroLeagueBall ResolveBall(ulong ballId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.SpawnManager == null) return null;
            if (!nm.SpawnManager.SpawnedObjects.TryGetValue(ballId, out var netObj) || netObj == null) return null;
            return netObj.TryGetComponent(out AstroLeagueBall ball) ? ball : null;
        }

        void OnDisable()
        {
            // Never leave a vessel stranded on an orbit, or a ball stranded as "held" by a hull
            // that is gone (pooling / vessel swap safety). No fling: an interrupted hold is not a
            // throw.
            EndFollow();
            if (ActsAsServer) EndServer(fling: false);
            _ball = null;
            // Not a release the pilot made: bump nothing, just forget the edge so re-enabling
            // cannot manufacture one.
            _armedLastFrame = false;
            _releasedThisGrapple = false;
            WriteArmedSafe(false);
        }

        void WriteArmedSafe(bool armed)
        {
            if (IsSpawned && !IsOwner) return;
            WriteArmed(armed);
        }
    }
}
