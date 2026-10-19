using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Shrike — a slow sniper that docks to the mass it touches, leaves nests behind it, slings itself across the cell
    /// and calls large missiles down from the flora its crystals become (design: <c>R_VesselActions/SHRIKE.md</c>; tuning:
    /// <see cref="ShrikeConfigSO"/>; first source: the Shrike Studio's <c>stepPilot</c>; the control scheme and the flora
    /// are Garrett's 2026-10-15 redesign). Owns every piece of the vessel's state; the <see cref="ShrikeActionSO"/>
    /// controls only call in.
    ///
    /// <list type="bullet">
    /// <item><b>Modes</b> (X Space · RB Charge · B Time): the mode decides what the RIGHT TRIGGER does —
    /// Space fires the <b>Void Lance</b> (held), Charge <b>paints</b> a target for the flora's missiles (held), Time
    /// shoots the <b>Void Sling</b> (press).</item>
    /// <item><b>Zoom</b> (LT, always): the flight view narrows and a scope in the mode's own shape comes up.</item>
    /// <item><b>Docking</b>: a touch on any block docks the hull (translation pinned, the nose aims faster, the camera
    /// eases back). Docked, every cooldown runs ×1.6, ×2.5 in a nest or on super-shielded mass.</item>
    /// <item><b>Mass</b> (A): docked, DETACH — leaving behind a nest of super-shielded blocks with a flora carrying ONE
    /// missile.</item>
    /// <item><b>Crystals</b> (<see cref="ShrikeCrystalEffectSO"/>): the crystal becomes a nest flora that visibly grows
    /// large missiles; the sling resets.</item>
    /// <item><b>Void Lance</b> (Space): breaks every block it passes; SUPER-SHIELDED mass only with the Space upgrade.</item>
    /// <item><b>Targeting Array</b> (Charge): locks vessels, then fauna, then flora, then mass; while painted, every flora
    /// with a ready missile launches it. The Charge card counts the missiles left across all flora.</item>
    /// </list>
    ///
    /// <para><b>Simulated on EVERY peer</b> (the Brittle Star's rule): the presses and crystal pickups replicate, so every
    /// machine holds the same mode, docks, nests, flora and missiles. Only the hull's MOTION (the sling, the undock push,
    /// the snap onto a surface) is written by its owner (<see cref="IsAuthority"/>); the camera and the overlay are the
    /// local pilot's; scored hits are decided on the shooter's owner (<see cref="ElementalTransfer.IsDecidedHere"/>).</para>
    ///
    /// <para><b>PvP is petals only (LOCKED).</b> Nothing here moves, slows or disables another pilot's vessel.</para>
    /// </summary>
    public sealed class ShrikeExecutor : ShipActionExecutorBase
    {
        /// <summary>What the right trigger does (values serialized nowhere; the order is the HUD's).</summary>
        public enum Mode { Space = 0, Charge = 1, Time = 2 }

        [Header("Wiring (serialized on the prefab so a missing wire shows in the inspector)")]
        [Tooltip("Every number this executor reads. Wired directly (vessel rule 20).")]
        [SerializeField] ShrikeConfigSO config;
        [Tooltip("The Hull: the transform that holds the model (Shrike.fbx, SHRIKE.md §2). Its renderer's Domain slot " +
                 "dresses the flora and the missiles, and the model's `gun.mouth` bone is where the lance's beam leaves " +
                 "the barrel. Left empty, the vessel's child named 'ShrikeHull' is used.")]
        [SerializeField] Transform hull;
        [Tooltip("The fleet's shared turn-end channel: triggers let go, the tether and missiles are cleared.")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

        [Inject] GameDataSO _gameData;

        // The hull's parts, found by NAME (ResolveHullParts). Names, not tuning: the prefab's Hull object and the bones
        // Tools/Build/vessel_model/specs/shrike.py writes into Shrike.fbx.
        const string HullName = "ShrikeHull";
        const string ModelMuzzleName = "gun.mouth";
        const string PlaceholderMuzzleName = "Muzzle";
        Renderer _hullRenderer;
        int _domainSlot;
        Transform _muzzle;

        enum DockKind { None = 0, Block = 1, Nest = 2 }

        sealed class Nest
        {
            public readonly List<Prism> Blocks = new();
            public readonly List<float> Born = new();   // each block's TimeCreated at lay: a pooled reuse is not ours
            public Vector3 Centre;
            public bool Dead;
        }

        sealed class Flora
        {
            public ShrikeNestFlora View;
            public Nest Nest;            // the nest it crowns, or null (a crystal's)
            public int Ready;
            public float Growth;
            public float LaunchCooldown;
            public bool Dead;
        }

        sealed class Missile
        {
            public GameObject Body;
            public TrailRenderer Trail;
            public Vector3 Position;
            public Vector3 Direction;
            public Nest From;
            public float Age;
        }

        IVesselStatus _status;
        bool _pinned;
        Mode _mode = Mode.Space;
        bool _triggerHeld;
        bool _zoomHeld;

        // Docking.
        DockKind _dock;
        Prism _dockPrism;
        float _dockPrismBorn;
        Nest _dockNest;
        float _dockGrace;
        float _dockedFor;
        Vector3 _push;
        Nest _leftNest;                 // the nest a Mass detach just left behind: it does not catch the hull leaving it
        float _leftNestUntil;

        // Cooldowns (seconds left; they tick at the dock bonus's rate).
        float _lanceCd, _slingCd, _nestCd;

        // Sling: the tether in flight, then the boost.
        bool _tethering;
        Prism _tetherAnchor;            // resolved at the press: a sling with nothing to anchor to never fires
        float _tetherAnchorBorn;
        Vector3 _tetherPoint;
        float _tetherDist, _tetherWent;
        bool _slinging;
        Prism _slingAnchor;
        float _slingAnchorBorn;
        Prism _slingFrom;
        Vector3 _slingDir;
        float _slingDist, _slingWent, _slingSpeed;
        float _slingTrailOwed;
        readonly Trail _slingTrail = new();
        bool _penHeld;

        // Array.
        int _lockId;
        ShrikeRules.LockClass _lock = ShrikeRules.LockClass.None;
        Transform _lockTarget;          // a vessel's, a creature's or a plant heart's transform
        Vector3 _lockPoint;
        float _lockScanAt;

        readonly List<Nest> _nests = new();
        readonly List<Flora> _flora = new();
        readonly List<Missile> _missiles = new();
        readonly List<Missile> _idleMissiles = new();
        readonly List<Prism> _candidates = new();
        readonly List<ShipActionSO> _bindScratch = new();
        readonly Dictionary<ShrikeControl, InputEvents> _boundInputs = new();
        static Material _trailMaterial;

        // The local pilot's camera, zoom and overlay.
        CustomCameraController _cam;
        float _camWritten;
        bool _camEngaged;
        float _zoom01;
        ShrikeScopeOverlay _overlay;
        bool _litPublished;
        float _litScanAt;
        bool _litAnchorFound;
        Vector3 _litAnchorPoint;
        float _litAnchorSize;

        // Autopilot.
        float _aiNextScan, _aiNextMode;
        int _aiTargetsAhead;
        bool _aiEnemyAhead, _aiHoldingTrigger;

        // ------------------------------------------------------------------ read by the HUD and the animation

        public ShrikeConfigSO Config => config;
        public Mode CurrentMode => _mode;
        public bool Docked => _dock != DockKind.None;
        public bool DockedInNest => _dock == DockKind.Nest;
        public bool Slinging => _slinging;
        public bool Tethering => _tethering;
        public bool Painting => _mode == Mode.Charge && _triggerHeld;
        public bool Locked => _lock != ShrikeRules.LockClass.None;
        public bool Zooming => _zoomHeld;
        /// <summary>0 just fired … 1 ready.</summary>
        public float Lance01 => config ? 1f - Mathf.Clamp01(_lanceCd / config.LanceCooldown) : 0f;
        public float Nest01 => config && config.NestCooldown > 0f ? 1f - Mathf.Clamp01(_nestCd / config.NestCooldown) : 1f;
        public float Sling01 => config ? 1f - Mathf.Clamp01(_slingCd / Mathf.Max(0.01f, config.SlingCooldown.EvaluateReplicated(_status))) : 0f;
        /// <summary>The ready missiles across every nest flora — what the Charge card counts.</summary>
        public int MissilesReady { get { int n = 0; foreach (var f in _flora) if (!f.Dead) n += f.Ready; return n; } }
        public int FloraCount { get { int n = 0; foreach (var f in _flora) if (!f.Dead) n++; return n; } }

        // ------------------------------------------------------------------ lifecycle

        public override void Initialize(IVesselStatus shipStatus)
        {
            ClearAll();
            _status = shipStatus;
            if (!hull && _status != null && _status.Transform)
                hull = _status.Transform.Find(HullName);
            ResolveHullParts();
            _mode = Mode.Space;
            _lanceCd = _slingCd = _nestCd = 0f;
            _lockId = 0;
            _boundInputs.Clear();
            _aiNextScan = _aiNextMode = 0f;

            if (!config)
                CSDebug.LogError($"[Shrike] {name}: ShrikeExecutor has no ShrikeConfig — the vessel has no lance, sling, " +
                                 "array or nest. Wire Assets/_SO_Assets/VesselActions/Shrike/ShrikeConfig.asset.");
            if (!_hullRenderer)
                CSDebug.LogError($"[Shrike] {name}: no hull renderer under '{HullName}' (the Shrike.fbx model) — flora and " +
                                 "missiles have no material.");
        }

        /// <summary>
        /// The hull's renderer, its domain slot and the lance's muzzle, found BY NAME under <see cref="hull"/>: the model's
        /// sub-objects have fileIDs only after Unity imports it, so they cannot be wired (asset-surgery 4.8c). The model
        /// (Shrike.fbx, FLEET_CONVENTION §3) wears the domain on slot 0 and names its muzzle bone <c>gun.mouth</c>; the
        /// retired placeholder (<see cref="ShrikeHullBuilder"/>, kept until the model is accepted) wore it on slot 1 of
        /// its own MeshRenderer and named its muzzle <c>Muzzle</c>, so either hull resolves.
        /// </summary>
        void ResolveHullParts()
        {
            _hullRenderer = null;
            _muzzle = null;
            _domainSlot = 0;
            if (!hull) return;
            var model = hull.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (model)
                _hullRenderer = model;
            else if (hull.TryGetComponent(out MeshRenderer placeholder))
            {
                _hullRenderer = placeholder;
                _domainSlot = 1;
            }
            _muzzle = FindDescendant(hull, ModelMuzzleName);
            if (!_muzzle) _muzzle = FindDescendant(hull, PlaceholderMuzzleName);
        }

        static Transform FindDescendant(Transform root, string childName)
        {
            if (root.name == childName) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDescendant(root.GetChild(i), childName);
                if (found) return found;
            }
            return null;
        }

        void OnEnable()
        {
            if (OnMiniGameTurnEnd != null) OnMiniGameTurnEnd.OnRaised += OnTurnEnd;
        }

        // Unconditional and idempotent: a despawned or pooled vessel must not keep a pin, a held pen, a zoom, a pulled-back
        // camera, a tether or a missile in flight. Nests and flora are things in the world and stay.
        void OnDisable()
        {
            if (OnMiniGameTurnEnd != null) OnMiniGameTurnEnd.OnRaised -= OnTurnEnd;
            ClearAll();
        }

        void OnDestroy()
        {
            for (int i = 0; i < _idleMissiles.Count; i++)
                if (_idleMissiles[i].Body) Destroy(_idleMissiles[i].Body);
            _idleMissiles.Clear();
            for (int i = 0; i < _flora.Count; i++)
                if (_flora[i].View) Destroy(_flora[i].View.gameObject);
            _flora.Clear();
            _nests.Clear();
            if (_overlay) _overlay.Dispose();
            _overlay = null;
        }

        void OnTurnEnd() => ClearAll();

        void ClearAll()
        {
            _triggerHeld = false;
            _zoomHeld = false;
            _lock = ShrikeRules.LockClass.None;
            _lockTarget = null;
            _tethering = false;
            _tetherAnchor = null;
            _slinging = false;
            _slingAnchor = _slingFrom = null;
            _slingTrail.Clear();
            _dock = DockKind.None;
            _dockPrism = null;
            _dockNest = null;
            _push = Vector3.zero;
            for (int i = 0; i < _missiles.Count; i++) ReturnMissile(_missiles[i]);
            _missiles.Clear();
            Pin(false);
            ReleasePen();
            ReleaseCamera();
            if (_overlay) _overlay.Hide();
            if (_status?.VesselPrismController is ShrikePrismController prisms) prisms.SpacingMultiplier = 1f;
        }

        // ------------------------------------------------------------------ controls (every peer: presses replicate)

        /// <summary>X / RB / B: pick the mode — what the right trigger does. A held trigger lets go of the old mode's
        /// action first (a held lance does not become a held paint).</summary>
        public void SelectMode(Mode mode)
        {
            if (mode == _mode) return;
            bool held = _triggerHeld;
            if (held) EndTrigger();
            _mode = mode;
            if (config) PlayAt(config.ModeEvent, HullPosition);
        }

        /// <summary>RT down: Space fires the lance while held, Charge paints while held, Time shoots the sling.</summary>
        public void BeginTrigger()
        {
            if (_triggerHeld) return;
            _triggerHeld = true;
            switch (_mode)
            {
                case Mode.Charge: _lockId++; break;   // a fresh paint is a fresh salvo
                case Mode.Time: PressSling(); break;
            }
        }

        public void EndTrigger()
        {
            _triggerHeld = false;
            _lock = ShrikeRules.LockClass.None;
            _lockTarget = null;
        }

        /// <summary>LT: zoom, whatever the mode.</summary>
        public void BeginZoom() => _zoomHeld = true;
        public void EndZoom() => _zoomHeld = false;

        /// <summary>A, the Mass ability: docked, detach and leave a nest behind with a flora carrying one missile (on
        /// the nest's cooldown; inside it, just detach). Flying, nothing — a nest is what a stay leaves.</summary>
        public void PressMass()
        {
            if (!config || _status == null || !_status.Transform || !Docked || _slinging) return;
            Vector3 at = HullPosition;
            Quaternion rot = _status.Transform.rotation;
            bool leaveNest = _nestCd <= 0f && _dock != DockKind.Nest;
            Undock(push: true);
            PlayAt(config.UndockEvent, at);
            if (!leaveNest) return;
            var nest = BuildNest(at, rot);
            if (nest == null) return;
            // Out through the nest's open mouth (the nose's way out), and the nest does not catch the hull leaving it.
            _push = _status.Transform.forward * config.UndockPush;
            _leftNest = nest;
            _leftNestUntil = Time.time + config.LeaveNestSeconds;
            _nestCd = config.NestCooldown;
            float radius = ShrikeRules.NestRadiusFor(nest.Blocks.Count, false, config.NestBlockSize, config.NestClearance);
            AddFlora(at + rot * ShrikeRules.TurretLocal(radius), rot * Vector3.up, config.DetachNestMissiles, nest);
            PlayAt(config.NestEvent, at);
        }

        /// <summary>A crystal (any kind) becomes a nest flora where it was, and the sling resets (page <c>pickUp</c>:
        /// "resetting your void sling").</summary>
        public void CollectCrystal(Vector3 at)
        {
            if (!config) return;
            _slingCd = 0f;
            AddFlora(at, Vector3.up, 0, null);
            PlayAt(config.CrystalEvent, at);
        }

        /// <summary>The Void Sling: only with something to anchor to (Garrett, 2026-10-16: "when there is no target for
        /// void sling it should not use the ability and it should not go on cooldown"). The anchor is found at the press
        /// (<see cref="FindSlingAnchor"/>: the first block on the hull's axis within the sling's range); the tether then
        /// flies out to it at <c>tetherSpeed</c>, and the cooldown starts only now that there is one.</summary>
        void PressSling()
        {
            if (!config || _status == null || !_status.Transform) return;
            if (_slingCd > 0f || _tethering || _slinging) return;
            if (!FindSlingAnchor(out var anchor, out var point)) return;
            _tethering = true;
            _tetherAnchor = anchor;
            _tetherAnchorBorn = anchor.prismProperties.TimeCreated;
            _tetherPoint = point;
            _tetherDist = Vector3.Distance(AimOrigin, point);
            _tetherWent = 0f;
            _slingCd = Mathf.Max(0.05f, config.SlingCooldown.EvaluateReplicated(_status));
            PlayAt(config.TetherEvent, HullPosition);
        }

        /// <summary>The first block on the weapons' axis (<see cref="AimOrigin"/> along the hull's forward) within the
        /// sling's range that the tether may anchor to — never your own fresh trail, the block you sit on or the nest you
        /// sit in.</summary>
        bool FindSlingAnchor(out Prism anchor, out Vector3 point)
        {
            anchor = null;
            point = default;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return false;
            Vector3 o = AimOrigin, f = _status.Transform.forward;
            float best = float.MaxValue;
            index.QuerySegment(o, o + f * config.SlingRange, config.DockQueryReach, _candidates);
            for (int i = 0; i < _candidates.Count; i++)
            {
                var prism = _candidates[i];
                if (!prism || prism.destroyed || prism.prismProperties == null) continue;
                if (IsOwnFreshTrail(prism)) continue;
                if (_dock == DockKind.Block && prism == _dockPrism) continue;
                if (_dock == DockKind.Nest && IsNestBlock(prism, out var nest) && nest == _dockNest) continue;
                if (RayHits(prism, o, f, 0f, config.SlingRange, out float t) && t < best)
                {
                    best = t;
                    anchor = prism;
                }
            }
            _candidates.Clear();
            if (!anchor) return false;
            point = o + f * best;
            return ShrikeRules.SlingDistance(HullPosition, point, config.HullRadius) >= 1f;
        }

        /// <summary>Where every weapon's line starts: the hull's centre, on its forward axis — the axis the on-axis chase
        /// camera and the eyepiece look down, so the lance, the tether, the lock cone and the reticles agree.</summary>
        Vector3 AimOrigin => HullPosition;

        // ------------------------------------------------------------------ the frame (page stepPilot, in its order)

        void Update()
        {
            if (!config || _status == null || !_status.Transform) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float rate = ShrikeRules.CooldownRate(Docked, DockedShielded(), config.DockBonus, config.DockBonusShielded);
            _lanceCd = Mathf.Max(0f, _lanceCd - dt * rate);
            _slingCd = Mathf.Max(0f, _slingCd - dt * rate);
            _nestCd = Mathf.Max(0f, _nestCd - dt * rate);
            _dockGrace = Mathf.Max(0f, _dockGrace - dt);
            _dockedFor = Docked ? _dockedFor + dt : 0f;

            UpdateTrailSpacing();
            if (_mode == Mode.Space && _triggerHeld && _lanceCd <= 0f && !_slinging) FireLance();
            StepTether(dt);
            UpdateLock();
            StepFlora(dt);

            if (_slinging) StepSling(dt);
            else if (Docked) HoldDock();
            else Fly(dt);

            StepMissiles(dt);
            StepNests();
            UpdatePen();

            if (_status.AutoPilotEnabled && IsAuthority()) Autopilot();
        }

        void LateUpdate()
        {
            if (!config || _status == null || !_status.Transform) return;
            if (!IsLocalPilot())
            {
                ReleaseCamera();
                if (_overlay) _overlay.Hide();
                return;
            }
            float dt = Time.deltaTime;
            UpdateCamera(dt);
            UpdateZoom(dt);
            UpdateLit();
            if (!_overlay) _overlay = ShrikeScopeOverlay.Create();
            float cone = _mode == Mode.Charge ? Mathf.Max(0.5f, config.LockCone.EvaluateReplicated(_status)) : 0f;
            _overlay.Drive(_status.Transform, _zoom01 > 0.01f, ScopeFieldOfView, ModeElement(_mode), DomainColour(),
                cone, ModeRange(_mode), Painting && Locked, LockPosition, CameraManager.ViewCamera);
        }

        /// <summary>The element whose petal the eyepiece wears in each mode.</summary>
        public static Element ModeElement(Mode mode) => ShrikeRules.ModeElement((int)mode);

        float ModeRange(Mode mode) => mode switch
        {
            Mode.Charge => config.LockRange,
            Mode.Time => config.SlingRange,
            _ => config.LanceRange,
        };

        bool DockedShielded()
        {
            if (_dock == DockKind.Nest) return true;
            return _dock == DockKind.Block && _dockPrism && _dockPrism.prismProperties is { IsSuperShielded: true };
        }

        void UpdateTrailSpacing()
        {
            if (_status.VesselPrismController is not ShrikePrismController prisms) return;
            float authored = Mathf.Max(0.01f, config.TrailWavelength.EvaluateAtNormalizedLevel(0f));
            prisms.SpacingMultiplier = config.TrailWavelength.EvaluateReplicated(_status) / authored;
        }

        // ------------------------------------------------------------------ the local pilot's camera and zoom

        bool IsLocalPilot() => _status.Player != null && _status.Player.IsLocalPilot && !_status.AutoPilotEnabled;

        /// <summary>"After colliding with a prism the camera should move further away": docked, the chase eases back to
        /// <c>dockedCameraPullBack</c> × its authored distance; undocked, it eases home. Written only while this hull is
        /// the camera's target, and handed back only if nobody else wrote it since (ThresherExecutor's rule).</summary>
        void UpdateCamera(float dt)
        {
            var cam = CameraManager.Instance ? CameraManager.Instance.GetActiveController() as CustomCameraController : null;
            var ship = _status.Transform;
            if (!cam || !cam.FollowTarget || !(cam.FollowTarget == ship || cam.FollowTarget.IsChildOf(ship)))
            {
                ReleaseCamera();
                return;
            }
            _cam = cam;
            float neutral = cam.NeutralOffsetZ;
            if (neutral >= 0f) return;   // not a chase camera
            if (!Docked && !_camEngaged) return;
            if (_camEngaged && !Mathf.Approximately(cam.GetCameraDistance(), _camWritten))
            {
                _camEngaged = false;     // somebody else moved it: theirs now
                return;
            }
            float target = Docked ? neutral * config.DockedCameraPullBack : neutral;
            float now = _camEngaged ? _camWritten : cam.GetCameraDistance();
            float next = Mathf.Lerp(now, target, 1f - Mathf.Exp(-config.CameraEaseRate * dt));
            if (!Docked && Mathf.Abs(next - neutral) < 0.05f) next = neutral;
            cam.SetCameraDistance(next);
            _camWritten = next;
            _camEngaged = Docked || !Mathf.Approximately(next, neutral);
        }

        void ReleaseCamera()
        {
            if (_camEngaged && _cam && Mathf.Approximately(_cam.GetCameraDistance(), _camWritten))
                _cam.SetCameraDistance(_cam.NeutralOffsetZ);
            _camEngaged = false;
            _cam = null;
            _zoom01 = 0f;
            ClearLit();
        }

        /// <summary>
        /// LT, the Serpent's zoom (Garrett, 2026-10-16: "the zoom should work like the serpent's zoom"): the flight view
        /// is never magnified; the eyepiece's field of view follows the trigger's DEPTH (past a deadzone) through a
        /// frame-rate independent chase, so a pad's analog trigger is the zoom dial and a mouse button's 0/1 is a ramp
        /// (SniperScopeActionExecutor.ResolveZoomTarget, one mechanism for every device).
        /// </summary>
        void UpdateZoom(float dt)
        {
            float target = 0f;
            if (_zoomHeld)
            {
                var input = _status.InputStatus;
                float depth = input != null ? Mathf.Clamp01(input.LeftTriggerAnalog) : 1f;
                target = depth <= config.ZoomDeadzone ? 0f : Mathf.InverseLerp(config.ZoomDeadzone, 1f, depth);
                // A held control with no analog depth to read (a bound button) still raises the scope fully.
                if (input == null || depth <= 0f) target = 1f;
            }
            _zoom01 = Mathf.MoveTowards(_zoom01, target, config.ZoomResponse * dt);
        }

        float ScopeFieldOfView => Mathf.Lerp(config.UnscopedFieldOfView, config.ZoomFieldOfView, _zoom01);

        /// <summary>
        /// "Whatever the player zooms in on should be lit based on the mode's effect" (Garrett, 2026-10-16): while the
        /// eyepiece is up, the mass the selected mode would act on is LIT through the platform's <see cref="PrismLit"/>
        /// (a GPU volume test on every prism, no per-prism CPU work), in the pilot's domain colour:
        /// <list type="bullet">
        /// <item><b>Space</b> — the Void Lance's own swept cylinder, along the axis, at its range and radius.</item>
        /// <item><b>Charge</b> — the lock cone, and a sphere on the painted target (the missiles' blast for mass).</item>
        /// <item><b>Time</b> — the sling's path to the anchor the tether would take, and the anchor itself; nothing when
        /// there is nothing to anchor to (the sling would not fire).</item>
        /// </list>
        /// Local pilot only: it is this pilot's aiming instrument, not a sentence for rivals.
        /// </summary>
        void UpdateLit()
        {
            float strength = _zoom01;
            if (strength <= 0.001f) { ClearLit(); return; }
            Vector3 o = AimOrigin, f = _status.Transform.forward;
            var domain = _status.Domain;
            // Two lights: the volume on GetInstanceID() and the target on its complement. A scene component's id is
            // negative and ~id is not, so the second key cannot be another scene producer's (id + 1 could be).
            int id = GetInstanceID();
            switch (_mode)
            {
                case Mode.Space:
                {
                    float r = Mathf.Max(0.1f, config.LanceRadius.EvaluateReplicated(_status));
                    PrismLit.PublishLight(id, LitVolume.Cylinder(o, f, config.LanceRange, r, false), strength, domain);
                    PrismLit.ClearLight(~id);
                    break;
                }
                case Mode.Charge:
                {
                    float cone = Mathf.Max(0.5f, config.LockCone.EvaluateReplicated(_status)) * Mathf.Deg2Rad;
                    PrismLit.PublishLight(id, LitVolume.Cone(o, f, _status.Transform.right, config.LockRange, Mathf.Tan(cone), 0f),
                        strength * 0.6f, domain);
                    if (Painting && Locked)
                        PrismLit.PublishLight(~id, LitVolume.Sphere(LockPosition,
                            _lock == ShrikeRules.LockClass.Mass ? config.MissileBlast : 10f), strength, domain);
                    else PrismLit.ClearLight(~id);
                    break;
                }
                default:
                {
                    if (Time.time >= _litScanAt)
                    {
                        _litScanAt = Time.time + 0.1f;
                        _litAnchorFound = FindSlingAnchor(out var anchor, out _litAnchorPoint);
                        _litAnchorSize = _litAnchorFound ? Half(anchor).magnitude : 0f;
                    }
                    if (!_litAnchorFound)
                    {
                        PrismLit.ClearLight(id);
                        PrismLit.ClearLight(~id);
                        break;
                    }
                    Vector3 to = _litAnchorPoint - o;
                    PrismLit.PublishLight(id, LitVolume.Cylinder(o, to.normalized, to.magnitude, config.HullRadius, false),
                        strength * 0.6f, domain);
                    PrismLit.PublishLight(~id, LitVolume.Sphere(_litAnchorPoint, _litAnchorSize + 2f), strength, domain);
                    break;
                }
            }
            _litPublished = true;
        }

        void ClearLit()
        {
            if (!_litPublished) return;
            _litPublished = false;
            int id = GetInstanceID();
            PrismLit.ClearLight(id);
            PrismLit.ClearLight(~id);
        }

        // ------------------------------------------------------------------ docking

        /// <summary>Flying: the undock push decays, and the first block the hull touches docks it (page <c>move</c> /
        /// <c>contact</c>).</summary>
        void Fly(float dt)
        {
            if (_push.sqrMagnitude > 1e-6f)
            {
                if (IsAuthority()) _status.VesselTransformer.TranslateShip(_push * dt);
                _push *= Mathf.Exp(-2f * dt);
            }
            if (_dockGrace > 0f || _status.IsAttached) return;
            if (TryContact(HullPosition, out var prism)) DockOn(prism);
        }

        /// <summary>The first block within the hull's radius, not your own fresh trail and not the block a sling just
        /// left (page <c>contact</c>).</summary>
        bool TryContact(Vector3 at, out Prism hit)
        {
            hit = null;
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return false;
            index.QuerySphere(at, config.HullRadius + config.DockQueryReach, _candidates);
            float best = float.MaxValue;
            for (int i = 0; i < _candidates.Count; i++)
            {
                var prism = _candidates[i];
                if (!Dockable(prism)) continue;
                float d = BoxDistance(prism, at);
                if (d < config.HullRadius && d < best)
                {
                    best = d;
                    hit = prism;
                }
            }
            _candidates.Clear();
            return hit;
        }

        bool Dockable(Prism prism)
        {
            if (!prism || prism.destroyed || prism.prismProperties == null) return false;
            if (IsOwnFreshTrail(prism)) return false;
            if (_slinging && prism == _slingFrom && _slingWent < 8f) return false;
            if (_leftNest != null && Time.time < _leftNestUntil && IsNestBlock(prism, out var nest) && nest == _leftNest) return false;
            return true;
        }

        bool IsOwnFreshTrail(Prism prism)
            => prism.ownerID == _status.PlayerName && !IsNestBlock(prism, out _)
               && Time.time - prism.prismProperties.TimeCreated < config.OwnTrailGraceSeconds;

        void DockOn(Prism prism)
        {
            if (IsNestBlock(prism, out var nest))
            {
                DockInNest(nest);
                return;
            }
            EndSling();
            _push = Vector3.zero;
            _dock = DockKind.Block;
            _dockPrism = prism;
            _dockPrismBorn = prism.prismProperties.TimeCreated;
            _dockNest = null;
            Pin(true);
            // Seat the hull on the surface, its radius off the closest point (page dock()).
            if (IsAuthority())
            {
                Vector3 pos = HullPosition;
                Vector3 closest = BoxClosest(prism, pos);
                Vector3 normal = pos - closest;
                if (normal.sqrMagnitude < 1e-6f) normal = -_status.Transform.forward;
                _status.VesselTransformer.TranslateShip(closest + normal.normalized * config.HullRadius - pos);
            }
            PlayAt(config.DockEvent, HullPosition);
        }

        /// <summary>"Docking anywhere on the nest snaps the Shrike to the correct location in the center."</summary>
        void DockInNest(Nest nest)
        {
            EndSling();
            _push = Vector3.zero;
            _dock = DockKind.Nest;
            _dockNest = nest;
            _dockPrism = null;
            Pin(true);
            if (IsAuthority()) _status.VesselTransformer.TranslateShip(nest.Centre - HullPosition);
            PlayAt(config.DockEvent, HullPosition);
        }

        void Undock(bool push)
        {
            if (!Docked) return;
            Vector3 normal = _dock == DockKind.Block && _dockPrism ? HullPosition - BoxClosest(_dockPrism, HullPosition) : Vector3.zero;
            _dock = DockKind.None;
            _dockPrism = null;
            _dockNest = null;
            _dockGrace = config.DockGraceSeconds;
            Pin(false);
            if (push)
                _push = (normal.sqrMagnitude > 1e-6f ? normal.normalized : _status.Transform.forward) * config.UndockPush;
        }

        /// <summary>Docked: the dock breaks with its block or its nest (page <c>stepPilot</c>).</summary>
        void HoldDock()
        {
            if (_dock == DockKind.Nest)
            {
                if (_dockNest == null || _dockNest.Dead) Undock(push: false);
                return;
            }
            if (!PrismAlive(_dockPrism, _dockPrismBorn)) Undock(push: false);
        }

        void Pin(bool on)
        {
            if (_pinned == on || _status == null) return;
            _pinned = on;
            if (_status.Vessel is VesselController controller) controller.SetTranslationRestricted(on);
            else _status.IsTranslationRestricted = on;
        }

        // ------------------------------------------------------------------ nests and flora

        /// <summary>A cup of super-shielded blocks round <paramref name="centre"/>, the nose's way out open, its radius
        /// grown so that NO two blocks clip (<see cref="ShrikeRules.NestRadiusFor"/>), each block facing out.</summary>
        Nest BuildNest(Vector3 centre, Quaternion rot)
        {
            var prisms = _status.VesselPrismController;
            var channel = prisms ? prisms.PrismSpawnChannel : null;
            if (!channel) return null;

            // "The oldest makes way."
            int live = 0;
            Nest oldest = null;
            for (int i = 0; i < _nests.Count; i++)
                if (!_nests[i].Dead) { live++; oldest ??= _nests[i]; }
            if (live >= config.MaxNests && oldest != null) RetireNest(oldest);
            _nests.RemoveAll(n => n.Dead);

            var nest = new Nest { Centre = centre };
            int count = ShrikeRules.NestBlockCount(config.NestBlocks.EvaluateReplicated(_status));
            float size = config.NestBlockSize;
            float radius = ShrikeRules.NestRadiusFor(count, false, size, config.NestClearance);
            Vector3 scale = Vector3.one * size;
            Vector3 up = rot * Vector3.up;
            for (int i = 0; i < count; i++)
            {
                Vector3 local = ShrikeRules.NestLocal(i, count, false, radius);
                Vector3 outward = rot * local;
                Quaternion face = SafeLookRotation.TryGet(outward, up, out var r, this, logError: false) ? r : rot;
                var prism = BoostRingBuilder.LayOne(channel, centre + outward, face, scale, PrismKind.SuperShielded,
                    _status.Domain, _status.PlayerName, _status.PlayerName);
                if (!prism) continue;
                nest.Blocks.Add(prism);
                nest.Born.Add(prism.prismProperties.TimeCreated);
            }
            _nests.Add(nest);
            return nest;
        }

        void RetireNest(Nest nest)
        {
            for (int i = 0; i < nest.Blocks.Count; i++)
            {
                var prism = nest.Blocks[i];
                if (!PrismAlive(prism, nest.Born[i])) continue;
                PrismKinds.Clear(prism);
                prism.ReturnToPool();
            }
            nest.Dead = true;
            if (_dockNest == nest) Undock(push: false);
        }

        bool IsNestBlock(Prism prism, out Nest owner)
        {
            for (int n = 0; n < _nests.Count; n++)
            {
                var nest = _nests[n];
                if (nest.Dead) continue;
                for (int i = 0; i < nest.Blocks.Count; i++)
                    if (nest.Blocks[i] == prism && Mathf.Approximately(prism.prismProperties.TimeCreated, nest.Born[i]))
                    {
                        owner = nest;
                        return true;
                    }
            }
            owner = null;
            return false;
        }

        /// <summary>A nest whose every block is gone is gone (page <c>stepNests</c>); its flora stays standing.</summary>
        void StepNests()
        {
            for (int n = 0; n < _nests.Count; n++)
            {
                var nest = _nests[n];
                if (nest.Dead) continue;
                bool any = false;
                for (int i = 0; i < nest.Blocks.Count && !any; i++) any = PrismAlive(nest.Blocks[i], nest.Born[i]);
                if (any) continue;
                nest.Dead = true;
                if (_dockNest == nest) Undock(push: false);
            }
        }

        void AddFlora(Vector3 at, Vector3 up, int ready, Nest nest)
        {
            // The oldest withers when there are too many.
            int live = 0;
            Flora oldest = null;
            for (int i = 0; i < _flora.Count; i++)
                if (!_flora[i].Dead) { live++; oldest ??= _flora[i]; }
            if (live >= config.MaxFlora && oldest != null) KillFlora(oldest);
            _flora.RemoveAll(f => f.Dead);

            var view = ShrikeNestFlora.Create(at, up, config.FloraMissileMax, config.MissileLength, DomainMaterial());
            _flora.Add(new Flora { View = view, Nest = nest, Ready = Mathf.Min(ready, config.FloraMissileMax) });
        }

        void KillFlora(Flora flora)
        {
            flora.Dead = true;
            if (flora.View) Destroy(flora.View.gameObject);
            flora.View = null;
        }

        /// <summary>Each flora grows its missiles (twice as fast while you sit in its nest) and, while you paint a lock in
        /// Charge mode, launches a ready one every <c>missileEvery</c>.</summary>
        void StepFlora(float dt)
        {
            bool fire = Painting && LockAlive;
            for (int i = 0; i < _flora.Count; i++)
            {
                var f = _flora[i];
                if (f.Dead) continue;
                if (!f.View) { f.Dead = true; continue; }
                ShrikeRules.GrowTick(ref f.Ready, ref f.Growth, dt, f.Nest != null && _dockNest == f.Nest,
                    config.TurretRegen, config.FloraMissileMax);
                f.LaunchCooldown -= dt;
                if (fire && ShrikeRules.FloraMayLaunch(f.Ready, f.LaunchCooldown)) Launch(f);
                f.View.Show(f.Ready, f.Growth / Mathf.Max(0.01f, config.TurretRegen));
            }
        }

        // ------------------------------------------------------------------ the Void Lance (Space)

        bool IsPierceUnlocked
        {
            get
            {
                var elemental = _status?.ElementalAbilityHandler;
                return elemental != null && elemental.IsUpgradeActive(Element.Space);
            }
        }

        /// <summary>Page <c>fireLance</c>: every block the beam passes breaks, super-shielded mass only with the Space
        /// upgrade (the beam passes it below), except the nest you are docked in and the block you sit on; a rival
        /// hull on the line is a petal hit.</summary>
        void FireLance()
        {
            _lanceCd = config.LanceCooldown;
            Vector3 f = _status.Transform.forward;
            Vector3 o = AimOrigin;
            float r = Mathf.Max(0.1f, config.LanceRadius.EvaluateReplicated(_status));
            float len = config.LanceRange;
            bool pierce = IsPierceUnlocked;
            bool hit = false;

            var index = PrismSpatialIndex.Instance;
            if (index && index.IsAvailable)
            {
                index.QuerySegment(o, o + f * len, r + config.DockQueryReach, _candidates);
                for (int i = 0; i < _candidates.Count; i++)
                {
                    var prism = _candidates[i];
                    if (!prism || prism.destroyed) continue;
                    if (_dock == DockKind.Block && prism == _dockPrism) continue;
                    if (_dock == DockKind.Nest && IsNestBlock(prism, out var nest) && nest == _dockNest) continue;
                    if (!ShrikeRules.LanceBreaks(prism.prismProperties is { IsSuperShielded: true }, pierce)) continue;
                    if (!RayHits(prism, o, f, r, len, out _)) continue;
                    BreakPrism(prism, f * config.LanceDebrisSpeed, devastate: true);
                    hit = true;
                }
                _candidates.Clear();
            }

            bool decided = ElementalTransfer.IsDecidedHere(_status);
            var players = _gameData != null ? _gameData.Players : null;
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                {
                    var victim = EnemyStatus(players[i]);
                    if (victim == null || !ShrikeRules.RaySphere(o, f, victim.Transform.position, config.HullRadius + r, len)) continue;
                    hit = true;
                    if (decided) ScoreHit(victim, config.LanceHitClass, ElementalDebuffSources.Other, f * 40f);
                }

            // Drawn from the barrel's muzzle to the point the axis reaches, so the beam leaves the gun and lands where the
            // hit test ran (they meet at range; 0.75 u apart at the nose).
            Vector3 from = _muzzle ? _muzzle.position : o;
            SniperBeam.Fire(from, o + f * len, r * 2f, r * 2f, DomainColour(), config.LanceBeamSeconds, r * 3f, 0.2f, hit);
            PlayAt(config.LanceEvent, o);
        }

        /// <summary>The sanctioned teardown (SniperShotActionExecutor.DestroyPrism): a super-shield comes off first, then
        /// <c>devastate</c> stops the prism restoring its armour instead of dying.</summary>
        void BreakPrism(Prism prism, Vector3 impact, bool devastate)
        {
            if (prism.prismProperties is { IsSuperShielded: true })
            {
                prism.DeactivateShields(impact);
                if (!devastate) return;   // a missile blast strips the super-shield and stops (one tier per hit)
            }
            prism.Damage(impact, _status.Domain, _status.PlayerName, devastate: devastate);
        }

        // ------------------------------------------------------------------ the Void Sling (Time)

        /// <summary>The tether flies out to the anchor found at the press; on reaching it the sling starts toward that
        /// point — speed = distance / slingSeconds (page <c>stepTether</c>). An anchor broken while the tether is out
        /// gives the cooldown back: the sling never happened.</summary>
        void StepTether(float dt)
        {
            if (!_tethering) return;
            _tetherWent += config.TetherSpeed * dt;
            if (_tetherWent < _tetherDist) return;
            _tethering = false;
            var anchor = _tetherAnchor;
            _tetherAnchor = null;
            float dist = ShrikeRules.SlingDistance(HullPosition, _tetherPoint, config.HullRadius);
            if (!PrismAlive(anchor, _tetherAnchorBorn) || dist < 1f)
            {
                _slingCd = 0f;
                return;
            }
            Prism from = _dock == DockKind.Block ? _dockPrism : null;
            if (Docked) Undock(push: false);
            _slinging = true;
            _slingAnchor = anchor;
            _slingAnchorBorn = _tetherAnchorBorn;
            _slingFrom = from;
            _slingDir = (_tetherPoint - HullPosition).normalized;
            _slingDist = dist;
            _slingWent = 0f;
            _slingSpeed = ShrikeRules.SlingSpeed(dist, config.SlingSeconds);
            _slingTrailOwed = 0f;
            _slingTrail.Clear();
            Pin(true);
            PlayAt(config.SlingEvent, HullPosition);
        }

        /// <summary>Page <c>stepSling</c> / <c>move</c>: straight at the anchor, swept, laying the thick trail; it docks
        /// on the first block it touches, else on the anchor when it arrives.</summary>
        void StepSling(float dt)
        {
            if (!PrismAlive(_slingAnchor, _slingAnchorBorn))
            {
                EndSling();
                return;
            }
            float move = Mathf.Min(_slingSpeed * dt, _slingDist - _slingWent);
            Vector3 from = HullPosition;
            Vector3 to = from + _slingDir * move;

            Prism hit = null;
            float hitAlong = float.MaxValue;
            var index = PrismSpatialIndex.Instance;
            if (index && index.IsAvailable && _dockGrace <= 0f && move > 0f)
            {
                index.QuerySegment(from, to, config.HullRadius + config.DockQueryReach, _candidates);
                for (int i = 0; i < _candidates.Count; i++)
                {
                    var prism = _candidates[i];
                    if (!Dockable(prism)) continue;
                    if (!RayHits(prism, from, _slingDir, config.HullRadius, move, out float t) || t >= hitAlong) continue;
                    hitAlong = t;
                    hit = prism;
                }
                _candidates.Clear();
            }
            if (hit) move = hitAlong;

            if (IsAuthority())
            {
                _status.VesselTransformer.TranslateShip(_slingDir * move);
                // The nose turns onto the line (page: cmd = basis(dir), slerped at 6/s).
                var hullT = _status.Transform;
                Quaternion.FromToRotation(hullT.forward, _slingDir).ToAngleAxis(out float degrees, out var axis);
                if (degrees > 1e-3f && axis.sqrMagnitude > 1e-8f)
                    _status.VesselTransformer.ApplyRotation(degrees * Mathf.Min(1f, config.SlingTurnRate * dt), axis);
            }
            _slingWent += move;
            LaySlingTrail(move);

            if (hit) { DockOn(hit); return; }
            if (_slingWent >= _slingDist - 0.01f) DockOn(_slingAnchor);
        }

        void EndSling()
        {
            if (!_slinging) return;
            _slinging = false;
            _slingAnchor = _slingFrom = null;
            Pin(false);
        }

        /// <summary>"Thin long blocks that get thicker while boosting with the void sling": one thick block per
        /// wavelength flown, laid behind the hull along the line (page <c>layTrail</c>).</summary>
        void LaySlingTrail(float travelled)
        {
            var prisms = _status.VesselPrismController;
            var channel = prisms ? prisms.PrismSpawnChannel : null;
            if (!channel) return;
            float wave = Mathf.Max(0.5f, config.TrailWavelength.EvaluateReplicated(_status));
            _slingTrailOwed += travelled;
            int lays = Mathf.Min(Mathf.FloorToInt(_slingTrailOwed / wave), config.SlingMaxBlocksPerFrame);
            if (lays <= 0) return;
            _slingTrailOwed = Mathf.Min(_slingTrailOwed - lays * wave, wave);   // excess DROPPED (rule 17)
            if (!SafeLookRotation.TryGet(_slingDir, _status.Transform.up, out var rot, this, logError: false)) return;
            Vector3 scale = new(config.TrailThick, config.TrailThick, config.TrailLength);
            Vector3 behind = HullPosition - _slingDir * (config.HullRadius + config.TrailLength * 0.5f + 1f);
            for (int k = 0; k < lays; k++)
            {
                Vector3 pos = behind - _slingDir * ((lays - 1 - k) * wave);
                var ret = channel.RaiseEvent(new PrismEventData
                {
                    ownDomain = _status.Domain,
                    Rotation = rot,
                    SpawnPosition = pos,
                    Scale = scale,
                    PrismType = prisms.SpawnPrismType,
                });
                if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) continue;
                prism.TargetScale = scale;
                prism.ownerID = _status.PlayerName;
                prism.ChangeTeam(_status.Domain);
                _slingTrail.Add(prism);
                prism.prismProperties.Index = (ushort)Mathf.Min(_slingTrail.TrailList.Count - 1, ushort.MaxValue);
                prism.Initialize(_status.PlayerName);
                prism.AdmitTargetScale(scale);
                prism.TargetScale = scale;
                prism.AssignTrail(_slingTrail);   // AFTER Initialize: pool reuse clears trail membership
            }
        }

        /// <summary>The fleet spawner lays the cruising trail; docked or slinging the hull's pen is up (the sling's thick
        /// trail is ours). Written on EDGES only (vessel rule 48: the pen has other writers).</summary>
        void UpdatePen()
        {
            var prisms = _status.VesselPrismController;
            bool up = Docked || _slinging;
            if (up && !_penHeld && prisms)
            {
                prisms.SetSpawnerPaused(true);
                _penHeld = true;
            }
            else if (!up) ReleasePen();
        }

        void ReleasePen()
        {
            if (!_penHeld) return;
            _penHeld = false;
            var prisms = _status?.VesselPrismController;
            if (prisms) prisms.SetSpawnerPaused(false);
        }

        // ------------------------------------------------------------------ the Targeting Array (Charge)

        /// <summary>
        /// While painting: the lock goes to the best target in the cone by CLASS first — vessels, then fauna, then flora,
        /// then mass (Garrett, 2026-10-15) — and within a class to the one nearest the aim. A held lock on a live target
        /// is kept to 1.6× the cone until something of a higher class enters the cone. Mass is the centre of the blocks
        /// round the one nearest the aim (page <c>updateLock</c>), re-scanned every 0.25 s.
        /// </summary>
        void UpdateLock()
        {
            if (!Painting) { _lock = ShrikeRules.LockClass.None; _lockTarget = null; return; }
            Vector3 pos = HullPosition, f = _status.Transform.forward;
            float cone = Mathf.Max(0.5f, config.LockCone.EvaluateReplicated(_status)) * Mathf.Deg2Rad;

            var bestKind = ShrikeRules.LockClass.None;
            float bestAngle = cone;
            Transform bestTarget = null;

            void Consider(ShrikeRules.LockClass kind, Transform target, Vector3 at)
            {
                float a = Angle(pos, f, at);
                if (a >= cone) return;
                if (!ShrikeRules.BetterLock(kind, a, bestKind, bestAngle)) return;
                bestKind = kind;
                bestAngle = a;
                bestTarget = target;
            }

            var players = _gameData != null ? _gameData.Players : null;
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                {
                    var victim = EnemyStatus(players[i]);
                    if (victim != null) Consider(ShrikeRules.LockClass.Vessel, victim.Transform, victim.Transform.position);
                }
            if (bestKind > ShrikeRules.LockClass.Fauna)
            {
                var cells = Cell.ActiveCellsSnapshot;
                for (int c = 0; c < cells.Count; c++)
                {
                    var cell = cells[c];
                    if (!cell) continue;
                    var fauna = cell.LiveFauna;
                    for (int i = fauna.Count - 1; i >= 0; i--)
                    {
                        var creature = fauna[i];
                        if (!creature || creature.IsDying || !creature.isActiveAndEnabled) continue;
                        Consider(ShrikeRules.LockClass.Fauna, creature.transform, creature.transform.position);
                    }
                }
            }
            if (bestKind > ShrikeRules.LockClass.Flora)
            {
                var plants = FloraHeartRegistry.Live;
                for (int i = 0; i < plants.Count; i++)
                {
                    var plant = plants[i];
                    if (!plant || plant.IsDying || !plant.HeartTransform) continue;
                    Consider(ShrikeRules.LockClass.Flora, plant.HeartTransform, plant.HeartTransform.position);
                }
            }

            // Keep a live lock of the same or a higher class while it stays within 1.6x the cone.
            if (_lock != ShrikeRules.LockClass.None && _lock != ShrikeRules.LockClass.Mass && LockAlive
                && _lock <= bestKind && Angle(pos, f, LockPosition) < cone * config.LockKeepScale)
                return;

            if (bestKind != ShrikeRules.LockClass.None)
            {
                if (_lock != bestKind || _lockTarget != bestTarget)
                {
                    _lockId++;
                    PlayAt(config.LockEvent, pos);
                }
                _lock = bestKind;
                _lockTarget = bestTarget;
                return;
            }

            if (_lock == ShrikeRules.LockClass.Mass && Time.time < _lockScanAt) return;
            _lockScanAt = Time.time + 0.25f;
            LockMass(pos, f, cone);
        }

        /// <summary>"Query the map for volume density and path find into the centre of mass": the block nearest the paint
        /// ray, then the centre of the mass round it. Your own blocks (nests, trail) are never the target.</summary>
        void LockMass(Vector3 pos, Vector3 f, float cone)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) { ClearLock(); return; }
            index.QuerySegment(pos, pos + f * config.LockRange, Mathf.Tan(cone) * 300f, _candidates);
            Prism seed = null;
            float seedAngle = cone;
            for (int i = 0; i < _candidates.Count; i++)
            {
                var prism = _candidates[i];
                if (!prism || prism.destroyed || prism.ownerID == _status.PlayerName) continue;
                float a = Angle(pos, f, prism.transform.position);
                if (a < seedAngle) { seedAngle = a; seed = prism; }
            }
            _candidates.Clear();
            if (!seed) { ClearLock(); return; }

            Vector3 seedPos = seed.transform.position, sum = Vector3.zero;
            int count = 0;
            index.QuerySphere(seedPos, config.MassLockRadius, _candidates);
            for (int i = 0; i < _candidates.Count; i++)
            {
                var prism = _candidates[i];
                if (!prism || prism.destroyed || prism.ownerID == _status.PlayerName) continue;
                sum += prism.transform.position;
                count++;
            }
            _candidates.Clear();
            Vector3 point = count > 0 ? sum / count : seedPos;
            if (_lock != ShrikeRules.LockClass.Mass || Vector3.Distance(_lockPoint, point) > 15f)
            {
                _lockId++;   // the same mass keeps its lock as it is eaten away
                PlayAt(config.LockEvent, pos);
            }
            _lock = ShrikeRules.LockClass.Mass;
            _lockTarget = null;
            _lockPoint = point;
        }

        void ClearLock()
        {
            _lock = ShrikeRules.LockClass.None;
            _lockTarget = null;
        }

        float Angle(Vector3 pos, Vector3 fwd, Vector3 at)
        {
            Vector3 d = at - pos;
            float l = d.magnitude;
            if (l > config.LockRange || l < 1f) return 9f;
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, fwd) / l, -1f, 1f));
        }

        bool LockAlive => _lock == ShrikeRules.LockClass.Mass
                          || _lock != ShrikeRules.LockClass.None && _lockTarget && _lockTarget.gameObject.activeInHierarchy;
        Vector3 LockPosition => _lock == ShrikeRules.LockClass.Mass ? _lockPoint : _lockTarget ? _lockTarget.position : _lockPoint;

        // ------------------------------------------------------------------ missiles

        void Launch(Flora flora)
        {
            flora.Ready--;
            flora.LaunchCooldown = config.MissileEvery;
            Vector3 from = flora.View.LaunchPoint;
            Vector3 up = flora.View.transform.up;
            var missile = TakeMissile();
            missile.Position = from;
            missile.Direction = ShrikeRules.LaunchDirection(up, LockPosition - from);
            missile.From = flora.Nest;
            missile.Age = 0f;
            if (missile.Body) missile.Body.transform.SetPositionAndRotation(from, Quaternion.LookRotation(missile.Direction, UpOf(missile.Direction)));
            if (missile.Trail) missile.Trail.Clear();
            _missiles.Add(missile);
            PlayAt(config.MissileLaunchEvent, from);
        }

        /// <summary>Page <c>stepMissiles</c>: "as long as the Shrike continues to paint the target, missiles actively home
        /// in", swerving round blocks that are not part of the target's mass; they blow on contact or at the end of life.</summary>
        void StepMissiles(float dt)
        {
            if (_missiles.Count == 0) return;
            var index = PrismSpatialIndex.Instance;
            bool indexed = index && index.IsAvailable;
            float speed = config.MissileSpeed;
            for (int m = _missiles.Count - 1; m >= 0; m--)
            {
                var missile = _missiles[m];
                missile.Age += dt;
                Vector3 dir = missile.Direction;
                if (Painting && LockAlive)
                {
                    Vector3 target = LockPosition;
                    Vector3 want = (target - missile.Position).normalized;
                    if (indexed)
                    {
                        index.QuerySegment(missile.Position, missile.Position + dir * 45f, 2.5f + config.DockQueryReach, _candidates);
                        Prism obstacle = null;
                        float obstacleT = float.MaxValue;
                        for (int i = 0; i < _candidates.Count; i++)
                        {
                            var prism = _candidates[i];
                            if (!prism || prism.destroyed) continue;
                            if (IsNestBlock(prism, out var own) && own == missile.From) continue;
                            if (_lock != ShrikeRules.LockClass.Vessel && Vector3.Distance(prism.transform.position, target) < 40f) continue;
                            if (RayHits(prism, missile.Position, dir, 2.5f, 45f, out float t) && t < obstacleT)
                            {
                                obstacleT = t;
                                obstacle = prism;
                            }
                        }
                        _candidates.Clear();
                        if (obstacle)
                        {
                            Vector3 away = missile.Position + dir * obstacleT - obstacle.transform.position;
                            away -= dir * Vector3.Dot(away, dir);
                            if (away.sqrMagnitude < 1e-4f) away = UpOf(dir);
                            want = (want * 0.3f + away.normalized).normalized;
                        }
                    }
                    dir = ShrikeRules.TurnToward(dir, want, config.MissileTurn * Mathf.Deg2Rad * dt, UpOf(dir));
                }
                missile.Direction = dir;

                float len = speed * dt;
                int steps = Mathf.Max(1, Mathf.CeilToInt(len / 2f));
                bool boom = false;
                for (int s = 0; s < steps && !boom; s++)
                {
                    missile.Position += dir * (len / steps);
                    boom = MissileTouches(missile, index, indexed);
                }
                if (!boom && missile.Age > config.MissileLife) boom = true;
                if (missile.Body)
                    missile.Body.transform.SetPositionAndRotation(missile.Position, Quaternion.LookRotation(dir, UpOf(dir)));
                if (!boom) continue;
                Detonate(missile);
                ReturnMissile(missile);
                _missiles.RemoveAt(m);
            }
        }

        bool MissileTouches(Missile missile, PrismSpatialIndex index, bool indexed)
        {
            if (indexed)
            {
                index.QuerySphere(missile.Position, 2f + config.DockQueryReach, _candidates);
                bool touched = false;
                for (int i = 0; i < _candidates.Count && !touched; i++)
                {
                    var prism = _candidates[i];
                    if (!prism || prism.destroyed) continue;
                    if (missile.Age < 0.4f && IsNestBlock(prism, out var own) && own == missile.From) continue;
                    touched = BoxDistance(prism, missile.Position) < 1.5f;
                }
                _candidates.Clear();
                if (touched) return true;
            }
            // The painted creature or plant (its body may be off the prism index).
            if (_lock is ShrikeRules.LockClass.Fauna or ShrikeRules.LockClass.Flora && _lockTarget
                && (_lockTarget.position - missile.Position).sqrMagnitude < 16f)
                return true;
            var players = _gameData != null ? _gameData.Players : null;
            if (players == null) return false;
            for (int i = 0; i < players.Count; i++)
            {
                var victim = EnemyStatus(players[i]);
                if (victim != null && Vector3.Distance(victim.Transform.position, missile.Position) < config.HullRadius + 1.5f)
                    return true;
            }
            return false;
        }

        /// <summary>Page <c>detonate</c>: "missiles detonate upon impact, destroying blocks in a medium radius" — a shield
        /// takes the hit first; every rival hull inside the blast is a petal hit.</summary>
        void Detonate(Missile missile)
        {
            Vector3 at = missile.Position;
            float blast = config.MissileBlast;
            PlayAt(config.MissileBlastEvent, at);
            var index = PrismSpatialIndex.Instance;
            if (index && index.IsAvailable)
            {
                index.QuerySphere(at, blast, _candidates);
                for (int i = 0; i < _candidates.Count; i++)
                {
                    var prism = _candidates[i];
                    if (!prism || prism.destroyed) continue;
                    Vector3 impact = (prism.transform.position - at).normalized * 30f;
                    BreakPrism(prism, impact, devastate: false);
                }
                _candidates.Clear();
            }
            SniperBeam.Fire(at, at + Vector3.up * 0.01f, 0.01f, 0.01f, DomainColour(), 0.01f, blast, 0.35f, hit: true);
            if (!ElementalTransfer.IsDecidedHere(_status)) return;
            var players = _gameData != null ? _gameData.Players : null;
            if (players == null) return;
            for (int i = 0; i < players.Count; i++)
            {
                var victim = EnemyStatus(players[i]);
                if (victim == null) continue;
                Vector3 d = victim.Transform.position - at;
                if (d.magnitude < blast + config.HullRadius)
                    ScoreHit(victim, config.MissileHitClass, ElementalDebuffSources.Explosion, d.normalized * 40f);
            }
        }

        /// <summary>A missile body: the flora's large missile mesh in the domain's material, with a bright domain-coloured
        /// trail so it reads across the cell ("the missiles should be much more visible").</summary>
        Missile TakeMissile()
        {
            for (int i = _idleMissiles.Count - 1; i >= 0; i--)
            {
                var idle = _idleMissiles[i];
                _idleMissiles.RemoveAt(i);
                if (!idle.Body) continue;
                idle.Body.SetActive(true);
                return idle;
            }
            var missile = new Missile();
            var go = new GameObject("ShrikeMissile");
            go.AddComponent<MeshFilter>().sharedMesh = ShrikeMeshes.Missile();
            var renderer = go.AddComponent<MeshRenderer>();
            var mat = DomainMaterial();
            if (mat) renderer.sharedMaterial = mat;
            float l = config.MissileLength;
            go.transform.localScale = new Vector3(0.45f * l, 0.45f * l, l);
            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = TrailMaterial();
            trail.time = config.MissileTrailSeconds;
            trail.minVertexDistance = 0.5f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            trail.widthMultiplier = config.MissileTrailWidth;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            Color c = DomainColour();
            trail.startColor = new Color(Mathf.Lerp(c.r, 1f, 0.5f), Mathf.Lerp(c.g, 1f, 0.5f), Mathf.Lerp(c.b, 1f, 0.5f), 1f);
            trail.endColor = new Color(c.r, c.g, c.b, 0f);
            missile.Body = go;
            missile.Trail = trail;
            return missile;
        }

        void ReturnMissile(Missile missile)
        {
            if (missile.Body) missile.Body.SetActive(false);
            if (missile.Trail) missile.Trail.Clear();
            _idleMissiles.Add(missile);
        }

        static Material TrailMaterial()
        {
            if (_trailMaterial) return _trailMaterial;
            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return null;
            _trailMaterial = new Material(shader) { name = "ShrikeMissileTrail", renderQueue = 3000 };
            return _trailMaterial;
        }

        // ------------------------------------------------------------------ scoring

        /// <summary>A hit on a rival hull under the LOCKED PvP rule: admitted through <see cref="CombatHitDrain.TryAdmit"/>,
        /// its petals ejected, the score raised. It never moves the victim. Callers have already asked
        /// <see cref="ElementalTransfer.IsDecidedHere"/>.</summary>
        void ScoreHit(IVesselStatus victim, CombatHitClass hitClass, ElementalDebuffSources source, Vector3 velocity)
        {
            if (!CombatHitDrain.TryAdmit(victim, _status, hitClass, config.HitCooldown, source, out int superseded)) return;
            CombatHitDrain.Apply(victim, _status, hitClass, superseded, velocity, source);
            if (config.OnCombatHitLanded)
                config.OnCombatHitLanded.Raise(new CombatHitStats
                {
                    ShooterName = _status.PlayerName,
                    VictimName = victim.PlayerName,
                    HitClass = hitClass,
                    SupersededRank = superseded,
                });
        }

        /// <summary>The player's vessel status if it is a live ENEMY of this pilot (another pilot, another domain).</summary>
        IVesselStatus EnemyStatus(IPlayer player)
        {
            var victim = player?.Vessel?.VesselStatus;
            if (victim == null || ReferenceEquals(victim, _status)) return null;
            if (victim.Domain == _status.Domain || !victim.Transform) return null;
            return victim;
        }

        // ------------------------------------------------------------------ autopilot (input-only)

        /// <summary>
        /// The AI flies the Shrike with the SAME replicated controls a human presses (vessel-ai's input-only rule): Charge
        /// with the trigger held while a rival hull is in the cone and it has missiles; Space with the trigger held while
        /// rival mass lies on the line; otherwise Time, pressing the trigger to sling at a far target ahead. Docked a few
        /// seconds, it presses Mass to detach and leave a nest.
        /// </summary>
        void Autopilot()
        {
            var handler = _status.ActionHandler;
            var ai = _status.AIPilot;
            if (!handler || !ai) return;
            float now = Time.time;
            Vector3 pos = HullPosition, fwd = _status.Transform.forward;

            if (now >= _aiNextScan)
            {
                _aiNextScan = now + 0.25f;
                _aiTargetsAhead = CountRivalPrisms(pos, pos + fwd * config.LanceRange,
                    config.LanceRadius.EvaluateReplicated(_status) + 2f);
                _aiEnemyAhead = EnemyInCone(pos, fwd, config.LockRange,
                    config.LockCone.EvaluateReplicated(_status) * config.LockKeepScale);
            }

            Vector3 toTarget = ai.TargetPosition - pos;
            Mode want = _aiEnemyAhead && MissilesReady > 0 ? Mode.Charge
                      : _aiTargetsAhead >= config.AiLanceMinTargets ? Mode.Space
                      : Mode.Time;
            if (want != _mode && now >= _aiNextMode && TryResolve(handler, ModeControl(want), out var modeInput))
            {
                _aiNextMode = now + 0.5f;
                if (_aiHoldingTrigger && TryResolve(handler, ShrikeControl.Trigger, out var release))
                {
                    handler.StopShipControllerActionsReplicated(release);
                    _aiHoldingTrigger = false;
                }
                handler.PerformShipControllerActionsReplicated(modeInput);
                handler.StopShipControllerActionsReplicated(modeInput);
            }

            if (TryResolve(handler, ShrikeControl.Trigger, out var trigger))
            {
                bool hold = _mode switch
                {
                    Mode.Charge => _aiEnemyAhead && MissilesReady > 0,
                    Mode.Space => _aiTargetsAhead >= config.AiLanceMinTargets || _aiEnemyAhead,
                    _ => false,
                };
                if (hold != _aiHoldingTrigger)
                {
                    _aiHoldingTrigger = hold;
                    if (hold) handler.PerformShipControllerActionsReplicated(trigger);
                    else handler.StopShipControllerActionsReplicated(trigger);
                }
                if (_mode == Mode.Time && !_slinging && !_tethering && _slingCd <= 0f
                    && toTarget.magnitude >= config.AiSlingMinDistance && Vector3.Angle(fwd, toTarget) <= config.AiSlingDegrees
                    && FindSlingAnchor(out _, out _))   // the press does nothing without one: never spam it
                {
                    handler.PerformShipControllerActionsReplicated(trigger);
                    handler.StopShipControllerActionsReplicated(trigger);
                }
            }

            if (Docked && _dockedFor >= config.AiDockSeconds && TryResolve(handler, ShrikeControl.Mass, out var massInput))
            {
                handler.PerformShipControllerActionsReplicated(massInput);
                handler.StopShipControllerActionsReplicated(massInput);
            }
        }

        static ShrikeControl ModeControl(Mode mode) => mode switch
        {
            Mode.Charge => ShrikeControl.ChargeMode,
            Mode.Time => ShrikeControl.TimeMode,
            _ => ShrikeControl.SpaceMode,
        };

        int CountRivalPrisms(Vector3 a, Vector3 b, float radius)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return 0;
            index.QuerySegment(a, b, radius, _candidates);
            int count = 0;
            var own = _status.Domain;
            for (int i = 0; i < _candidates.Count; i++)
            {
                var prism = _candidates[i];
                if (prism && !prism.destroyed && prism.Domain != own) count++;
            }
            _candidates.Clear();
            return count;
        }

        bool EnemyInCone(Vector3 pos, Vector3 fwd, float range, float degrees)
        {
            var players = _gameData != null ? _gameData.Players : null;
            if (players == null) return false;
            for (int i = 0; i < players.Count; i++)
            {
                var victim = EnemyStatus(players[i]);
                if (victim == null) continue;
                Vector3 d = victim.Transform.position - pos;
                if (d.sqrMagnitude <= range * range && Vector3.Angle(fwd, d) <= degrees) return true;
            }
            return false;
        }

        /// <summary>Which input carries <paramref name="control"/> on THIS vessel — retried until the binding maps have
        /// filled (vessel rule 6), never latched on a miss.</summary>
        bool TryResolve(R_VesselActionHandler handler, ShrikeControl control, out InputEvents input)
        {
            if (_boundInputs.TryGetValue(control, out input)) return true;
            foreach (InputEvents candidate in System.Enum.GetValues(typeof(InputEvents)))
            {
                _bindScratch.Clear();
                handler.CollectBoundActions(candidate, _bindScratch);
                for (int i = 0; i < _bindScratch.Count; i++)
                    if (_bindScratch[i] is ShrikeActionSO action && action.Kind == control)
                    {
                        _bindScratch.Clear();
                        _boundInputs[control] = candidate;
                        input = candidate;
                        return true;
                    }
            }
            _bindScratch.Clear();
            input = default;
            return false;
        }

        /// <summary>Is THIS machine the one that moves this hull (and runs its autopilot)? The owner on the networked
        /// paths (the host for an AI); unconditionally yes with no network session running. The fleet's rule
        /// (<c>MantaStingActionExecutor.IsSimAuthority</c>), kept local.</summary>
        bool IsAuthority()
        {
            var player = _status?.Player;
            if (player != null && player.IsNetworkOwner) return true;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm == null || !nm.IsListening;
        }

        // ------------------------------------------------------------------ geometry (ShrikeRules on the prism's box)

        static bool PrismAlive(Prism prism, float born)
            => prism && !prism.destroyed && prism.gameObject.activeInHierarchy && prism.prismProperties != null
               && Mathf.Approximately(prism.prismProperties.TimeCreated, born);

        static Vector3 Half(Prism prism) => prism.transform.lossyScale * 0.5f;

        static float BoxDistance(Prism prism, Vector3 world)
        {
            var t = prism.transform;
            return ShrikeRules.BoxDistance(Quaternion.Inverse(t.rotation) * (world - t.position), Half(prism));
        }

        static Vector3 BoxClosest(Prism prism, Vector3 world)
        {
            var t = prism.transform;
            Vector3 local = ShrikeRules.BoxClosestLocal(Quaternion.Inverse(t.rotation) * (world - t.position), Half(prism));
            return t.position + t.rotation * local;
        }

        static bool RayHits(Prism prism, Vector3 origin, Vector3 dir, float pad, float maxT, out float entry)
        {
            var t = prism.transform;
            var inv = Quaternion.Inverse(t.rotation);
            return ShrikeRules.TryRayBox(inv * (origin - t.position), inv * dir, Half(prism), pad, maxT, out entry);
        }

        static Vector3 UpOf(Vector3 dir)
        {
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, dir);
            return up.sqrMagnitude > 1e-4f ? up.normalized : Vector3.ProjectOnPlane(Vector3.right, dir).normalized;
        }

        // ------------------------------------------------------------------ feedback

        Vector3 HullPosition => _status != null && _status.Transform ? _status.Transform.position : transform.position;

        Color DomainColour()
        {
            var colorSet = _gameData?.ThemeManagerData?.ColorSet;
            return colorSet != null && _status != null ? colorSet.GetDomainSignalColor(_status.Domain) : Color.white;
        }

        /// <summary>The hull's domain material: the renderer's domain slot (<see cref="ResolveHullParts"/>), where
        /// VesselCustomization paints the domain.</summary>
        Material DomainMaterial()
        {
            if (!_hullRenderer) return null;
            var mats = _hullRenderer.sharedMaterials;
            if (mats.Length == 0) return null;
            return mats[_domainSlot < mats.Length ? _domainSlot : 0];
        }

        static void PlayAt(EventReference reference, Vector3 at)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, at);
        }
    }
}
