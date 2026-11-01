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
    /// The Brittle Star — a one-touch flyer built round a cockpit and a five-pointed support ring (design:
    /// <c>R_VesselActions/BRITTLE_STAR.md</c>; tuning: <see cref="BrittleStarConfigSO"/>). Owns every piece of the
    /// vessel's state; the five <see cref="BrittleStarActionSO"/> controls only call in.
    ///
    /// <list type="bullet">
    /// <item><b>Change Modes</b> (A / B / X / RB): Speed, Creation, Space (Destruction) or Charge; pressing the selected
    /// mode again returns to Cruise. The modes need the mounted ring.</item>
    /// <item><b>Speed Mode</b> (Time): the hull's cruise × <c>speedModeMultiplier</c> (×6), every thruster lit, the base
    /// trail's spacing stretched so production per second does not rise, and its prisms stretched with it so the trail
    /// joins up.</item>
    /// <item><b>Creation Mode</b>: a trail behind EACH of the ring's five corners instead of the cockpit's one, the
    /// base trail's own small block, turning with the ring's firing rotation.</item>
    /// <item><b>Mass</b>: the x and y of every block the hull lays (<c>prismWidthScale</c>), never its length.</item>
    /// <item><b>Space Mode</b> (<see cref="Mode.Destruction"/>): FIVE GUNS, the RT pull setting their rate (analog); the
    /// right stick moves them in (meeting ahead, a crosshair) and out (splayed).</item>
    /// <item><b>Charge Mode</b>: FIVE CORDS (<see cref="BrittleStarCord"/>) — whips at 150% length with RT released,
    /// shortening and stiffening as it is pulled into five sabers that keep some give (<c>cordMaxRigidity</c>). They
    /// slice every prism they touch and cool the guns.</item>
    /// <item><b>Every other mode</b>: RT fires the one turret the ring has turned to the aim — the same gun, no spread.</item>
    /// <item><b>Heat</b>: every round heats the guns (<see cref="BrittleStarRules.HeatState"/>), which hold more heat
    /// with Space (<c>heatCapacity</c>); overheated they cannot fire until they cool, and in Space Mode the trail lays
    /// DANGER prisms meanwhile.</item>
    /// <item><b>Ring Detonation</b> (LT): throw the ring forward — only with a SPARE ring in stock; the mounted ring then
    /// regrows on its own. It blows on contact or on its fuse in a nucleus-wide disk.</item>
    /// <item><b>Crystals</b> (<see cref="BrittleStarRingChargeByCrystalEffectSO"/>): one spare ring each, up to two.</item>
    /// </list>
    ///
    /// <para><b>Simulated on EVERY peer.</b> Every input it reacts to is replicated — the presses round-trip through the
    /// server, crystal pickups run through <c>NetworkVesselImpactor</c>'s ClientRpc, and the right stick that aims is a
    /// replicated input NetworkVariable — so every machine's copy holds the same mode, the same ring and the same charge,
    /// and lays the same trail (vessel rule 48: nothing simulated keys off a local-only state). Prism destruction is per
    /// peer by design; each peer's copy of the ring, the cords and the rounds breaks its own copy of the mass. Scored hits
    /// are decided once, on the shooter's owner (<see cref="ElementalTransfer.IsDecidedHere"/>).</para>
    ///
    /// <para><b>PvP is petals only (LOCKED).</b> Nothing here moves, slows or disables another pilot's vessel; a hit on
    /// an enemy hull is a scored combat hit that moves petals (see the config's type doc).</para>
    /// </summary>
    public sealed class BrittleStarExecutor : ShipActionExecutorBase
    {
        public enum Mode { Cruise = 0, Speed = 1, Creation = 2, Destruction = 3, Charge = 4 }

        [Header("Wiring (serialized on the prefab so a missing wire shows in the inspector)")]
        [Tooltip("Every number this executor reads. Wired directly (vessel rule 20).")]
        [SerializeField] BrittleStarConfigSO config;
        [Tooltip("The hull (BrittleStarHullModel, which finds its parts in BrittleStar.fbx by name; or the procedural " +
                 "BrittleStarHullBuilder it replaced): the ring, the six muzzles, the copy of the ring a detonation throws.")]
        [SerializeField] BrittleStarHullSource hull;
        [Tooltip("Fires every turret's rounds (its ProjectileFactory pools the round prefab).")]
        [SerializeField] Gun gun;
        [Tooltip("The fleet's shared turn-end channel: the trigger lets go and every thrown ring is cleared.")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;
        [Tooltip("Destruction Mode's five cords are drawn with this: ThresherChainMaterial (ThresherChainFresnelShader - a " +
                 "fresnel TUBE across a view-aligned ribbon, the Thresher's chain). Empty falls back to a vertex-colour line.")]
        [SerializeField] Material cordMaterial;

        [Inject] GameDataSO _gameData;

        IVesselStatus _status;
        VesselImpactor _impactor;
        readonly BrittleStarRules.RingState _ring = new();
        readonly BrittleStarRules.HeatState _heat = new();
        BrittleStarRules.Mode _selected = BrittleStarRules.Mode.Cruise;

        float _speed01, _creation01, _destruction01, _charge01;
        float _ringPhase;            // radians: the support ring's firing rotation about the nose axis
        int _aimedPoint;             // the turret the ring has turned to the aim (it holds when the stick centres)
        bool _aiming;                // the stick is outside the deadzone this frame
        bool _firing;                // RT held (the replicated press)
        bool _shotOwedNow;           // the press owes one volley at once
        float _gunsOwed;
        float _cordsOut01;           // eased 0..1: how far the five cords are grown out of their muzzles
        float _cordPull01;           // eased 0..1: RT's pull as the cords feel it (length; x cordMaxRigidity = rigidity)
        bool _dangerHeld;            // the base spawner is laying danger prisms (written on EDGES only)
        bool _penHeld;
        float _creationOwed;
        Vector3 _lastHullPosition;
        bool _hasLastHullPosition;
        // Creation Mode: one trail per ring corner (a Trail each, so every corner's line is its own ordered trail).
        readonly Trail[] _cornerTrails =
            { new Trail(), new Trail(), new Trail(), new Trail(), new Trail() };

        // Thrown rings and the ring explosions (expanding disks) they leave.
        struct Flight
        {
            public GameObject Body;
            public Vector3 Velocity;
            public float Age;
            public Vector3 Previous;
        }

        struct Wave
        {
            public Vector3 Centre;
            public Vector3 Normal;
            public float StartsAt;
            public float InnerRadius;
            public float OuterRadius;
            public float LastRadius;
            public float HalfThickness;
            public GameObject Band;      // the expanding ring drawn at the disk's edge
            public Mesh BandMesh;
        }

        readonly List<Flight> _flights = new();
        readonly List<Wave> _waves = new();
        readonly List<GameObject> _idleBodies = new();
        readonly List<Prism> _candidates = new();
        readonly HashSet<Prism> _cutThisFrame = new();
        BrittleStarCord[] _cords;
        readonly List<ShipActionSO> _bindScratch = new();
        readonly Dictionary<BrittleStarControl, InputEvents> _boundInputs = new();

        // Autopilot.
        float _aiNextScan, _aiNextMode, _aiTriggerSince, _aiNextDetonate;
        int _aiTargetsAhead, _aiTargetsNear, _aiTargetsReach;
        bool _aiHoldingFire;
        bool? _aiSpeedWish;          // the AI boost policy's call (BrittleStarSpeedModeAIPolicySO); null = no policy

        const float VesselHitRadius = 6f;   // how far from a hull's centre the ring and the wave count as touching it
        const float MinSpeedToLay = 3f;      // the fleet spawner's own threshold

        // ------------------------------------------------------------------ read by the HUD and the animation

        /// <summary>Eased 0..1: how far each mode's effect is on.</summary>
        public float Speed01 => _speed01;
        public float Creation01 => _creation01;
        public float Destruction01 => _destruction01;
        public float Charge01 => _charge01;
        /// <summary>The mode the pilot has selected (on only while the ring is mounted).</summary>
        public Mode Selected => (Mode)(int)_selected;
        public bool IsOn(Mode mode) => BrittleStarRules.IsOn((BrittleStarRules.Mode)(int)mode, _selected, _ring);
        /// <summary>0 just thrown, rising as the next one regrows, 1 while mounted.</summary>
        public float RingPresence01 => _ring.Presence01(config ? config.RebuildSeconds : 0.5f);
        public bool RingMounted => _ring.Mounted;
        /// <summary>Spare rings in stock (each one throw), up to <see cref="MaxSpareRings"/>.</summary>
        public int SpareRings => _ring.Spares;
        public int MaxSpareRings => config ? config.MaxSpareRings : 2;
        public bool CanThrowRing => _ring.CanLaunch;
        /// <summary>The guns' heat, 0..1.</summary>
        public float Heat01 => _heat.Heat01;
        /// <summary>Overheated: the guns cannot fire until they cool (and in Space Mode the trail lays danger prisms).</summary>
        public bool Overheated => _heat.Overheated;
        /// <summary>Eased 0..1: Charge Mode's cords from whip (0) toward rigid (1), following the RT pull. A full pull
        /// reaches <c>cordMaxRigidity</c>, never 1.</summary>
        public float CordRigidity01 => config ? _cordPull01 * config.CordMaxRigidity : _cordPull01;
        /// <summary>Eased 0..1: the RT pull itself (0 = whips, 1 = a full pull), before the rigidity cap. The Charge card
        /// crossfades its whips into sabers on it.</summary>
        public float CordPull01 => _cordPull01;
        /// <summary>The ring's firing rotation about the nose axis, radians. The animation draws it.</summary>
        public float RingPhase => _ringPhase;
        public bool IsFiring => _firing;

        /// <summary>The AI boost policy's wish for Speed Mode (true / false), or null to let the autopilot's own
        /// heuristic decide. Only the policy writes it; the autopilot acts on a change at once.</summary>
        public void SetAutopilotSpeedWish(bool? wish)
        {
            if (_aiSpeedWish == wish) return;
            _aiSpeedWish = wish;
            _aiNextMode = 0f;
        }
        /// <summary>The turret the ring has turned to the aim (RT's gun outside Space and Charge Mode). It holds when the
        /// stick centres, as the ring does.</summary>
        public int AimedPoint => _aimedPoint;
        /// <summary>Is the right stick outside its deadzone (turning the ring) this frame?</summary>
        public bool Aiming => _aiming;
        /// <summary>Eased 0..1: how far Charge Mode's five cords are grown out of their muzzles.</summary>
        public float CordsOut01 => _cordsOut01;
        public BrittleStarConfigSO Config => config;

        // ------------------------------------------------------------------ lifecycle

        public override void Initialize(IVesselStatus shipStatus)
        {
            ClearFlights();
            ReleasePen();
            _status = shipStatus;
            _impactor = _status != null && _status.Transform ? _status.Transform.GetComponent<VesselImpactor>() : null;
            if (!hull && _status != null && _status.Transform)
                hull = _status.Transform.GetComponentInChildren<BrittleStarHullSource>(true);
            if (gun) gun.Initialize(shipStatus);

            _ring.Reset();
            _selected = BrittleStarRules.Mode.Cruise;
            _speed01 = _creation01 = _destruction01 = _charge01 = 0f;
            _ringPhase = 0f;
            _aimedPoint = 0;
            _aiming = false;
            _firing = false;
            _shotOwedNow = false;
            _gunsOwed = 0f;
            _cordPull01 = 0f;
            _heat.Reset();
            ReleaseDanger();
            ClearCords();
            _creationOwed = 0f;
            _hasLastHullPosition = false;
            for (int i = 0; i < _cornerTrails.Length; i++) _cornerTrails[i].Clear();
            _boundInputs.Clear();
            _aiHoldingFire = false;
            _aiSpeedWish = null;
            _aiNextScan = _aiNextMode = _aiNextDetonate = 0f;

            if (!config)
                CSDebug.LogError($"[BrittleStar] {name}: BrittleStarExecutor has no BrittleStarConfig — the vessel has no " +
                                 "modes, no guns and no ring. Wire Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarConfig.asset.");
            if (!hull)
                CSDebug.LogError($"[BrittleStar] {name}: no BrittleStarHullSource under the vessel — there is no ring to " +
                                 "aim, lay from or throw.");
            if (!gun)
                CSDebug.LogError($"[BrittleStar] {name}: BrittleStarExecutor has no Gun — the turrets cannot fire.");
        }

        void OnEnable()
        {
            if (OnMiniGameTurnEnd != null) OnMiniGameTurnEnd.OnRaised += OnTurnEnd;
        }

        // Unconditional and idempotent: a despawned or pooled vessel must not keep a held pen, a speed bonus or a
        // ring in flight.
        void OnDisable()
        {
            if (OnMiniGameTurnEnd != null) OnMiniGameTurnEnd.OnRaised -= OnTurnEnd;
            _firing = false;
            ClearFlights();
            ClearCords();
            ReleasePen();
            ReleaseDanger();
            if (_status?.VesselTransformer is BrittleStarVesselTransformer transformer) transformer.SpeedModeMultiplier = 1f;
            if (_status?.VesselPrismController is BrittleStarPrismController prisms)
            {
                prisms.SpacingMultiplier = 1f;
                prisms.LengthMultiplier = 1f;
            }
            if (_status?.VesselPrismController is { } spawner)
            {
                spawner.WidthMultiplier = 1f;
                spawner.ThicknessMultiplier = 1f;
            }
        }

        void OnDestroy()
        {
            if (_cordRoot) Destroy(_cordRoot);
            _cordRoot = null;
            for (int i = 0; i < _idleBodies.Count; i++)
                if (_idleBodies[i]) Destroy(_idleBodies[i]);
            _idleBodies.Clear();
            for (int i = 0; i < _waves.Count; i++) ReturnBand(_waves[i].Band);   // a blast still expanding
            _waves.Clear();
            for (int i = 0; i < _idleBands.Count; i++)
            {
                if (!_idleBands[i]) continue;
                if (_idleBands[i].TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh) Destroy(mf.sharedMesh);
                Destroy(_idleBands[i]);
            }
            _idleBands.Clear();
        }

        void OnTurnEnd()
        {
            _firing = false;
            ClearFlights();
            ClearCords();
            ReleasePen();
            ReleaseDanger();
        }

        // ------------------------------------------------------------------ controls (every peer: presses replicate)

        /// <summary>A mode control: select it, or back to Cruise if it was selected.</summary>
        public void SelectMode(Mode mode)
        {
            var before = _selected;
            _selected = BrittleStarRules.Press(_selected, (BrittleStarRules.Mode)(int)mode);
            if (!config || before == _selected) return;
            PlayAt(_selected switch
            {
                BrittleStarRules.Mode.Speed => config.SpeedModeEvent,
                BrittleStarRules.Mode.Creation => config.CreationModeEvent,
                BrittleStarRules.Mode.Destruction => config.DestructionModeEvent,
                BrittleStarRules.Mode.Charge => config.ChargeModeEvent,
                _ => config.ModeOffEvent,
            }, HullPosition);
        }

        public void BeginFire()
        {
            if (_firing) return;
            _firing = true;
            _shotOwedNow = true;   // one volley the instant the trigger goes down
        }

        public void EndFire() => _firing = false;

        /// <summary>Ring Detonation: throw the ring — only with a spare in stock; the mounted ring then regrows on its own.</summary>
        public void DetonateRing()
        {
            if (!config || !hull || !hull.Ring || _status == null) return;
            if (!_ring.TryLaunch(config.RebuildSeconds)) return;
            Launch();
            PlayAt(config.RingRebuildEvent, HullPosition);
        }

        /// <summary>A crystal (any kind): one more spare ring, up to the stock's size (<see cref="BrittleStarRules.RingState"/>).</summary>
        public void CollectCrystal()
        {
            if (!config) return;
            if (_ring.CollectCrystal(config.MaxSpareRings)) PlayAt(config.ChargeStoredEvent, HullPosition);
        }

        // ------------------------------------------------------------------ the frame

        void Update()
        {
            if (!config || _status == null || !_status.Transform) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _ring.Tick(dt);
            EaseModes(dt);
            ApplySpeedMode();
            ApplyBlockGirth();
            AimRing(dt);
            UpdateCreation();
            float depth = TriggerDepth();
            UpdateCords(depth, dt);
            UpdateGuns(depth, dt);
            UpdateHeat(dt);
            UpdateFlights(dt);
            UpdateWaves();

            if (_status.AutoPilotEnabled && IsSimAuthority()) Autopilot();
        }

        void EaseModes(float dt)
        {
            float step = dt / config.ModeBlendSeconds;
            _speed01 = Mathf.MoveTowards(_speed01, IsOn(Mode.Speed) ? 1f : 0f, step);
            _creation01 = Mathf.MoveTowards(_creation01, IsOn(Mode.Creation) ? 1f : 0f, step);
            _destruction01 = Mathf.MoveTowards(_destruction01, IsOn(Mode.Destruction) ? 1f : 0f, step);
            _charge01 = Mathf.MoveTowards(_charge01, IsOn(Mode.Charge) ? 1f : 0f, step);
        }

        /// <summary>
        /// The transformer's speed multiplier — Speed Mode's ×6 and Creation Mode's slow-down (the slowest mode), from
        /// their eased blends — and the base trail's spacing and prism length.
        ///
        /// <para>The spacing follows the hull's ACTUAL speed over its plain cruise, not the mode's target: the hull eases
        /// into and out of a speed change, and a spacing keyed to the target lays several times too many prisms while the
        /// hull is still coasting down from ×6 (the momentum hole the Brittle Star design studio's scorecard found). It
        /// never shrinks below the authored wavelength, so the slower Creation Mode simply lays fewer per second.</para>
        ///
        /// <para>Each prism is stretched along the path by the same factor (× <c>speedModeStretch</c>, Garrett 2026-10-16:
        /// "stretch its prisms so its trail connects better"), so the fast trail's blocks fill as much of their wider
        /// spacing as the cruise trail's do.</para>
        /// </summary>
        void ApplySpeedMode()
        {
            float speedMode = Mathf.Max(1f, config.SpeedModeMultiplier.EvaluateReplicated(_status));
            float now = BrittleStarRules.ModeSpeed(_speed01, _creation01, speedMode, config.CreationSpeedMultiplier);
            if (_status.VesselTransformer is not BrittleStarVesselTransformer transformer) return;
            transformer.SpeedModeMultiplier = now;
            if (_status.VesselPrismController is BrittleStarPrismController prisms)
            {
                float cruise = transformer.CurrentThrottleTarget / Mathf.Max(0.01f, now);
                float ratio = cruise > 1f ? Mathf.Max(1f, _status.Speed / cruise) : 1f;
                prisms.SpacingMultiplier = Mathf.Lerp(1f, ratio, config.SpeedModeSpacingFollow);
                prisms.LengthMultiplier = Mathf.Lerp(1f, prisms.SpacingMultiplier, config.SpeedModeStretch);
            }
        }

        /// <summary>
        /// MASS sizes the x and y of every block the hull lays, never its length (Garrett, 2026-10-16: "stretch the prisms
        /// in z and decrease them in x and y at base. mass can control the x and y"): the base trail through the fleet
        /// spawner's own width and thickness dials (this executor is their single writer on this hull), Creation Mode's
        /// corner trails at lay time (<see cref="LayCornerPrism"/>). Read on the replicated level, so every peer lays the
        /// same block.
        /// </summary>
        void ApplyBlockGirth()
        {
            if (_status.VesselPrismController is not { } prisms) return;
            float girth = BlockGirth();
            prisms.WidthMultiplier = girth;
            prisms.ThicknessMultiplier = girth;
        }

        float BlockGirth() => Mathf.Max(0.1f, config.PrismWidthScale.EvaluateReplicated(_status));

        // ------------------------------------------------------------------ aim: the ring's firing rotation

        Vector2 AimStick()
        {
            if (_status.AutoPilotEnabled) return Vector2.zero;   // the AI aims with its nose
            var input = _status.Player != null ? _status.InputStatus : null;
            return input != null ? Vector2.ClampMagnitude(input.RightNormalizedJoystickPosition, 1f) : Vector2.zero;
        }

        void AimRing(float dt)
        {
            Vector2 stick = AimStick();
            // The stick's DIRECTION turns the ring in every mode (in Space and Charge Mode it turns the five guns or cords
            // with it); its DEFLECTION moves those in and out (ArmDirection). The turret it brings round is RT's one gun
            // in every other mode, and it stays the gun when the stick centres - the ring holds its rotation.
            float? aim = BrittleStarRules.AimAngle(stick, config.AimDeadzone);
            _aiming = aim != null;
            if (aim == null) return;   // the ring holds its last rotation, and its aimed turret
            _aimedPoint = BrittleStarRules.NearestPoint(aim.Value, _ringPhase);
            float target = BrittleStarRules.PhaseToAim(aim.Value, _aimedPoint, _ringPhase);
            float maxStep = config.RingTurnRate * Mathf.Deg2Rad * dt;
            _ringPhase = Mathf.MoveTowards(_ringPhase, target, maxStep);
            // Keep the float small; the ring has five-fold symmetry so a full turn changes nothing.
            const float turn = Mathf.PI * 2f;
            if (_ringPhase > turn * 8f) _ringPhase -= turn * 8f;
            else if (_ringPhase < -turn * 8f) _ringPhase += turn * 8f;
        }

        /// <summary>World position of point <paramref name="index"/> on the ring at the current firing rotation, pushed
        /// out by <paramref name="radiusScale"/>.</summary>
        Vector3 RingPoint(int index, float radiusScale)
        {
            // The VESSEL's transform, not the drawn hull's: the animation leans the drawn hull into turns, and what is
            // laid must not depend on a presentation pose (the hull sits at the vessel's origin, unscaled).
            var hullT = _status.Transform;
            float a = BrittleStarHullForm.PointAngle(index) + _ringPhase;
            float r = hull.RingRadius * radiusScale;
            Vector3 local = new(Mathf.Cos(a) * r, Mathf.Sin(a) * r, hull.RingPlaneZ);
            return hullT.TransformPoint(local);
        }

        // ------------------------------------------------------------------ Creation Mode: a trail behind each corner

        void UpdateCreation()
        {
            var prisms = _status.VesselPrismController;
            bool creating = IsOn(Mode.Creation);
            // The shared pen is written on EDGES only (it has other writers: vessel rule 48).
            if (creating && !_penHeld && prisms)
            {
                prisms.SetSpawnerPaused(true);
                _penHeld = true;
            }
            else if (!creating) ReleasePen();

            Vector3 here = HullPosition;
            if (!_hasLastHullPosition)
            {
                _lastHullPosition = here;
                _hasLastHullPosition = true;
                return;
            }
            float travelled = Vector3.Distance(here, _lastHullPosition);
            _lastHullPosition = here;
            if (!creating || !hull || !prisms || !prisms.SpawnerEnabled) { _creationOwed = 0f; return; }
            if (_status.IsAttached || _status.Speed <= MinSpeedToLay) return;

            // The base trail's stretch applies here too, so a hand-over from Speed Mode does not burst blocks while it coasts.
            float spacing = config.CreationSpacing * (_status.VesselPrismController is BrittleStarPrismController bp
                ? Mathf.Max(1f, bp.SpacingMultiplier) : 1f);
            _creationOwed += travelled;
            int lays = Mathf.Min(Mathf.FloorToInt(_creationOwed / spacing), config.CreationMaxPerFrame);
            if (lays <= 0) return;
            _creationOwed = Mathf.Min(_creationOwed - lays * spacing, spacing);   // excess DROPPED (rule 17)
            var channel = prisms.PrismSpawnChannel;
            if (!channel) return;
            Vector3 course = _status.Course.sqrMagnitude > 1e-6f ? _status.Course.normalized : _status.Transform.forward;
            for (int k = 0; k < lays; k++)
            {
                // A frame that owes several blocks spreads them back along the path it flew, newest at the ring.
                float back = config.CreationOffset + (lays - 1 - k) * spacing;
                for (int i = 0; i < BrittleStarHullForm.Points; i++)
                    LayCornerPrism(channel, prisms.SpawnPrismType, i, course, back);
            }
        }

        /// <summary>One block of corner <paramref name="index"/>'s trail, <paramref name="back"/> behind the corner along
        /// the course, its length along the path and its broad side tangent to the ring. MASS sizes its x and y, read at lay
        /// time.</summary>
        void LayCornerPrism(PrismEventChannelWithReturnSO channel, PrismType type, int index, Vector3 course, float back)
        {
            Vector3 corner = RingPoint(index, config.CreationRadiusScale);
            Vector3 pos = corner - course * back;
            Vector3 radial = Vector3.ProjectOnPlane(corner - HullPosition, course);
            if (radial.sqrMagnitude < 1e-6f) radial = _status.Transform.up;
            if (!SafeLookRotation.TryGet(course, radial.normalized, out var rot, this, logError: false)) return;

            // The base trail's own small block (Garrett, 2026-10-16: "use the same smaller prisms for all 5 prism
            // trails"), its x and y × MASS (ApplyBlockGirth).
            Vector3 scale = _status.VesselPrismController.TrailBaseScale;
            float girth = BlockGirth();
            scale.x *= girth;
            scale.y *= girth;
            var ret = channel.RaiseEvent(new PrismEventData
            {
                ownDomain = _status.Domain,
                Rotation = rot,
                SpawnPosition = pos,
                Scale = scale,
                PrismType = type,
            });
            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) return;

            var trail = _cornerTrails[index];
            prism.TargetScale = scale;
            prism.ownerID = _status.PlayerName;
            prism.ChangeTeam(_status.Domain);
            prism.waitTime = config.CreationWaitTime;
            trail.Add(prism);
            prism.prismProperties.Index = (ushort)Mathf.Min(trail.TrailList.Count - 1, ushort.MaxValue);
            prism.Initialize(_status.PlayerName);
            // A Mass-scaled prism is a stated size: admit it past the pool's scale window AFTER Initialize (whose
            // reset re-clamps it), or a big one is trimmed with no error.
            prism.AdmitTargetScale(scale);
            prism.TargetScale = scale;
            prism.AssignTrail(trail);   // AFTER Initialize: pool reuse clears trail membership
        }

        void ReleasePen()
        {
            if (!_penHeld) return;
            _penHeld = false;
            var prisms = _status?.VesselPrismController;
            if (prisms) prisms.SetSpawnerPaused(false);
        }

        // ------------------------------------------------------------------ Destruction Mode: the five cords

        /// <summary>
        /// CHARGE MODE's five cords (Garrett, 2026-10-16: whips that "extend to 150% of [their] current length";
        /// "pulling the trigger should shrink it to the current 100% and rigidify it so they are more like 5 straight
        /// sabers… analog"). One hangs from each turret's muzzle along the arm's line — the right stick's direction turns
        /// them with the ring, its deflection moves them in (meeting ahead) and out (splayed) — and each is a wave-carrying
        /// rope (<see cref="BrittleStarCord"/>): released, a turn, a ring flick or a stick sweep sends a slow wave down it;
        /// pulled, it shortens and stiffens into a saber. They grow out of the muzzles while the mode comes on, draw back
        /// in as it goes off, run only while the trail does (the turn's clock) and with the ring, since they hang from it.
        /// </summary>
        void UpdateCords(float depth, float dt)
        {
            var prisms = _status.VesselPrismController;
            bool want = IsOn(Mode.Charge) && hull && prisms && prisms.SpawnerEnabled;
            _cordsOut01 = Mathf.MoveTowards(_cordsOut01, want ? 1f : 0f, dt / config.ModeBlendSeconds);
            _cordPull01 += (depth - _cordPull01) * (1f - Mathf.Exp(-config.CordRigidityRate * dt));
            float length = config.CordLength * Mathf.Lerp(config.CordLooseLengthScale, 1f, _cordPull01)
                         * _cordsOut01 * RingPresence01;
            if (length < 0.5f || !hull)
            {
                ClearCords();
                return;
            }

            int links = Mathf.Max(2, config.CordLinks);
            if (_cords == null || _cords[0].Links != links)
            {
                _cords = new BrittleStarCord[BrittleStarHullForm.Points];
                for (int i = 0; i < _cords.Length; i++) _cords[i] = new BrittleStarCord(links);
            }

            var settings = new BrittleStarCord.Settings
            {
                LooseWave = config.CordLooseWave,
                RigidWave = config.CordRigidWave,
                LooseDamping = config.CordLooseDamping,
                RigidDamping = config.CordRigidDamping,
                Pose = config.CordPose,
            };
            // A full pull shortens the cords all the way but stiffens them only to cordMaxRigidity: the saber keeps 60% of
            // the whip's give (Garrett: "not completely rigid at full press", 0.4 since 2026-10-17).
            float rigidity = CordRigidity01;
            float splay01 = BrittleStarRules.Spread01(AimStick(), config.AimDeadzone);
            for (int i = 0; i < _cords.Length; i++)
            {
                var muzzle = hull.Muzzle(i);
                if (!muzzle) continue;
                _cords[i].Step(muzzle.position, ArmLine(muzzle, splay01), length, rigidity, settings, dt);
            }

            float cut = config.CordCutRadius * Mathf.Max(0.1f, config.ProjectileScaleMultiplier.EvaluateReplicated(_status));
            CutWithCords(cut);
            StrikeHullsWithCords(cut);
            DrawCords(cut);
        }

        /// <summary>The line a turret's gun (Space Mode) or cord (Charge Mode) runs along: in toward the nose axis with
        /// the stick centred, splayed out with it pushed (<see cref="BrittleStarRules.ArmDirection"/>).</summary>
        Vector3 ArmLine(Transform muzzle, float splay01)
        {
            Vector3 forward = _status.Transform.forward;
            Vector3 radial = Vector3.ProjectOnPlane(muzzle.position - HullPosition, forward);
            float distance = radial.magnitude;
            return distance > 1e-4f
                ? BrittleStarRules.ArmDirection(forward, radial / distance, distance, splay01,
                                                config.ArmFocusDistance, config.ArmSplayDegrees)
                : forward;
        }

        /// <summary>
        /// Every prism a cord's link touches is <see cref="Prism.Slice"/>d along the plane that link is sweeping, debris
        /// at the link's own velocity — the Thresher's chain cut. Friendly fire, as the rounds had it ("players need to be
        /// careful not to destroy allied blocks"): any domain, but never this pilot's own FRESH trail (the fleet's grace
        /// window), and super-shielded mass refuses. A shielded prism loses its shield to the first cut and dies to the next.
        /// Each prism is cut once a frame however many links reach it.
        /// </summary>
        void CutWithCords(float cut)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;
            const float candidateExtent = 8f;
            _cutThisFrame.Clear();
            for (int c = 0; c < _cords.Length; c++)
            {
                var cord = _cords[c];
                if (!cord.Seeded) continue;
                var pts = cord.Points;
                for (int s = 0; s < pts.Length - 1; s++)
                {
                    Vector3 a = pts[s], b = pts[s + 1];
                    index.QuerySegment(a, b, cut + candidateExtent, _candidates);
                    if (_candidates.Count == 0) continue;
                    Vector3 ab = b - a;
                    float abLenSq = ab.sqrMagnitude;
                    for (int i = 0; i < _candidates.Count; i++)
                    {
                        var prism = _candidates[i];
                        if (!prism || prism.destroyed || _cutThisFrame.Contains(prism)) continue;
                        if (SelfTrailContactConfigSO.SuppressesSkimContact(prism, _status)) continue;

                        Vector3 centre = prism.transform.position;
                        float t = abLenSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(centre - a, ab) / abLenSq) : 0f;
                        float contact = cut + 0.5f * prism.transform.lossyScale.magnitude;
                        Vector3 onCord = a + ab * t;
                        if ((centre - onCord).sqrMagnitude > contact * contact) continue;

                        _cutThisFrame.Add(prism);
                        Vector3 v = Vector3.Lerp(cord.NodeVelocity(s), cord.NodeVelocity(s + 1), t);
                        // Super-shielded mass refuses the cut, visibly: the deflection jiggle, never
                        // a cord passing through armour in silence (Docs/LIT.md).
                        if (prism.AbsorbSuperShieldHit(v.magnitude)) continue;
                        Vector3 cutNormal = Vector3.Cross(ab, v);
                        if (cutNormal.sqrMagnitude < 1e-8f) cutNormal = Vector3.Cross(ab, _status.Transform.up);
                        if (cutNormal.sqrMagnitude < 1e-8f) cutNormal = Vector3.right;
                        prism.Slice(v * config.CordDebrisRestitution, _status.Domain, _status.PlayerName,
                                    onCord, cutNormal.normalized, debrisSpeedLimit: config.CordDebrisSpeedLimit);
                        PlayAt(config.CordSliceEvent, onCord);
                    }
                }
            }
            _candidates.Clear();
        }

        /// <summary>A cord drawn across an enemy hull: a scored STRIKE (the contact verb: a blade, nothing fired) at the
        /// fleet price, admitted through <see cref="CombatHitDrain.TryAdmit"/> (<c>cordHitCooldown</c> per victim; a warded
        /// pilot is neither scored on nor robbed). Petals only — it never moves the victim. Decided once, on the shooter's
        /// owner.</summary>
        void StrikeHullsWithCords(float cut)
        {
            if (_gameData == null || _gameData.Players == null || !ElementalTransfer.IsDecidedHere(_status)) return;
            float reach = cut + VesselHitRadius;
            var players = _gameData.Players;
            for (int p = 0; p < players.Count; p++)
            {
                var victim = EnemyStatus(players[p]);
                if (victim == null) continue;
                Vector3 at = victim.Transform.position;
                for (int c = 0; c < _cords.Length; c++)
                {
                    var cord = _cords[c];
                    if (!cord.Seeded || !TouchesCord(cord, at, reach, out Vector3 velocity)) continue;
                    ScoreCordHit(victim, velocity);
                    break;
                }
            }
        }

        static bool TouchesCord(BrittleStarCord cord, Vector3 at, float reach, out Vector3 velocity)
        {
            var pts = cord.Points;
            for (int s = 0; s < pts.Length - 1; s++)
            {
                Vector3 a = pts[s], ab = pts[s + 1] - a;
                float abLenSq = ab.sqrMagnitude;
                float t = abLenSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(at - a, ab) / abLenSq) : 0f;
                if ((at - (a + ab * t)).sqrMagnitude > reach * reach) continue;
                velocity = Vector3.Lerp(cord.NodeVelocity(s), cord.NodeVelocity(s + 1), t);
                return true;
            }
            velocity = Vector3.zero;
            return false;
        }

        void ScoreCordHit(IVesselStatus victim, Vector3 velocity)
        {
            const CombatHitClass hitClass = CombatHitClass.Strike;
            if (!CombatHitDrain.TryAdmit(victim, _status, hitClass, config.CordHitCooldown,
                                         ElementalDebuffSources.VesselContact, out int superseded))
                return;
            // Strike's take is normally authored per weapon; the cords author none, so it is taken at the fleet price.
            CombatHitDrain.ApplyPriced(victim, _status, hitClass, superseded, velocity, ElementalDebuffSources.VesselContact);
            if (config.OnCombatHitLanded)
                config.OnCombatHitLanded.Raise(new CombatHitStats
                {
                    ShooterName = _status.PlayerName,
                    VictimName = victim.PlayerName,
                    HitClass = hitClass,
                    SupersededRank = superseded,
                });
        }

        void ClearCords()
        {
            if (_cords != null)
                for (int i = 0; i < _cords.Length; i++) _cords[i].Clear();
            if (_cordRoot && _cordRoot.activeSelf) _cordRoot.SetActive(false);
        }

        // ---- the cords' look: the Thresher's chain, five times, one LineRenderer each (cosmetic; every peer draws its own)

        GameObject _cordRoot;
        LineRenderer[] _cordLines;
        Vector3[] _cordDrawn;
        MaterialPropertyBlock _cordMpb;
        static Material s_cordFallbackMaterial;
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");

        void DrawCords(float cut)
        {
            if (!EnsureCordVisuals()) return;
            if (!_cordRoot.activeSelf) _cordRoot.SetActive(true);

            Color body = config.CordColor;
            body.a = 1f;
            Color rim = WithMaxChannel(CordRimHue(), config.CordRimBrightness);
            _cordMpb.Clear();
            _cordMpb.SetColor(DarkColorId, body);
            _cordMpb.SetColor(BrightColorId, rim);

            int drawn = ThresherChainLinks.SmoothedCount(_cords[0].Count, config.CordRenderSubdivisions);
            if (_cordDrawn == null || _cordDrawn.Length != drawn) _cordDrawn = new Vector3[drawn];
            for (int i = 0; i < _cordLines.Length; i++)
            {
                var line = _cordLines[i];
                var cord = _cords[i];
                if (!cord.Seeded)
                {
                    line.positionCount = 0;
                    continue;
                }
                // Drawn at the width that CUTS, as a smooth curve through the gameplay links (it passes through each).
                line.startWidth = line.endWidth = 2f * cut;
                line.SetPropertyBlock(_cordMpb);
                line.startColor = line.endColor = rim;   // the vertex-colour fallback material
                int written = ThresherChainLinks.Smooth(cord.Points, config.CordRenderSubdivisions, _cordDrawn);
                if (line.positionCount != written) line.positionCount = written;
                line.SetPositions(_cordDrawn);
            }
        }

        bool EnsureCordVisuals()
        {
            if (_cordRoot) return true;
            var material = cordMaterial ? cordMaterial : CordFallbackMaterial();
            if (!material) return false;   // headless: no graphics, no visuals

            _cordRoot = new GameObject($"BrittleStarCords::{(_status != null ? _status.PlayerName : name)}");
            _cordMpb = new MaterialPropertyBlock();
            _cordLines = new LineRenderer[BrittleStarHullForm.Points];
            for (int i = 0; i < _cordLines.Length; i++)
            {
                var go = new GameObject($"Cord{i}");
                go.transform.SetParent(_cordRoot.transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = true;
                line.alignment = LineAlignment.View;   // the fresnel tube is computed across a VIEW-aligned ribbon
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.positionCount = 0;
                _cordLines[i] = line;
            }
            return true;
        }

        static Material CordFallbackMaterial()
        {
            if (s_cordFallbackMaterial) return s_cordFallbackMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return null;
            s_cordFallbackMaterial = new Material(shader) { name = "BrittleStarCord (runtime)", renderQueue = 3000 };
            return s_cordFallbackMaterial;
        }

        /// <summary>The rim's hue: the pilot's domain signal colour from the palette (the Thresher chain's rule), else the
        /// configured fallback.</summary>
        Color CordRimHue()
        {
            var palette = _gameData && _gameData.ThemeManagerData ? _gameData.ThemeManagerData.ColorSet : null;
            if (_status == null || _status.Domain == Domains.Blue || !palette) return config.CordFallbackRim;
            return palette.GetDomainSignalColor(_status.Domain);
        }

        static Color WithMaxChannel(Color c, float max)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return m > 1e-4f
                ? new Color(c.r / m * max, c.g / m * max, c.b / m * max, 1f)
                : new Color(max, max, max, 1f);
        }

        // ------------------------------------------------------------------ the guns and their heat

        /// <summary>
        /// RT's depth, 0..1 (Garrett, 2026-10-16: analog). The owner's own trigger where the device has an analog channel;
        /// a press read as a FULL pull where it does not — the keyboard and mouse, the autopilot, and every remote replica,
        /// because only the press and release replicate (the Thresher's rule, THRESHER.md §7).
        /// </summary>
        float TriggerDepth()
        {
            if (!_firing) return 0f;
            var input = _status.Player != null && !_status.AutoPilotEnabled ? _status.InputStatus : null;
            float rt = input != null ? input.RightTriggerAnalog : 0f;
            return rt > 1e-3f ? Mathf.Clamp01(rt) : 1f;
        }

        /// <summary>
        /// RT fires every gun the mode has (<see cref="BrittleStarRules.GunsFor"/>): SPACE Mode's five, along the arms'
        /// lines; CHARGE Mode none (the trigger works the cords); every other mode the ONE turret the ring has turned to the
        /// aim, straight ahead — the same gun, no spread (Garrett, 2026-10-16). The pull sets the rate, up to
        /// <c>fireRate</c> per gun; every round heats the guns, and overheated they fire nothing. Fire is owed in seconds
        /// and paid in whole volleys, capped at four a frame (rule 17).
        /// </summary>
        void UpdateGuns(float depth, float dt)
        {
            bool shotNow = _shotOwedNow;
            _shotOwedNow = false;
            var on = IsOn(Mode.Destruction) ? BrittleStarRules.Mode.Destruction
                   : IsOn(Mode.Charge) ? BrittleStarRules.Mode.Charge
                   : BrittleStarRules.Mode.Cruise;
            int guns = BrittleStarRules.GunsFor(on);
            if (!gun || !hull || guns == 0 || depth <= 0f || _heat.Overheated)
            {
                _gunsOwed = 0f;
                return;
            }

            float interval = 1f / Mathf.Max(0.01f, depth * config.FireRate);
            _gunsOwed = Mathf.Min(_gunsOwed + dt, interval * 4f);
            int volleys = Mathf.FloorToInt(_gunsOwed / interval);
            _gunsOwed -= volleys * interval;
            if (shotNow && volleys == 0) volleys = 1;
            if (volleys <= 0) return;

            float time = config.ProjectileTime * Mathf.Max(0.1f, config.RangeMultiplier.EvaluateReplicated(_status));
            float scale = config.ProjectileScale * Mathf.Max(0.1f, config.ProjectileScaleMultiplier.EvaluateReplicated(_status));
            Vector3 inherit = _status.Course * _status.Speed;
            float splay01 = BrittleStarRules.Spread01(AimStick(), config.AimDeadzone);
            for (int v = 0; v < volleys && !_heat.Overheated; v++)
            {
                if (guns > 1)
                    for (int i = 0; i < guns; i++)
                    {
                        var muzzle = hull.Muzzle(i);
                        if (muzzle) FireAlong(muzzle, ArmLine(muzzle, splay01), scale, time, inherit);
                    }
                else
                {
                    // The ring's aimed turret; the cockpit's own gun only while the ring regrows.
                    var muzzle = hull.Muzzle(_ring.Mounted ? _aimedPoint : -1);
                    if (muzzle) FireAlong(muzzle, _status.Transform.forward, scale, time, inherit);
                }
                if (_heat.AddRounds(guns, config.HeatPerRound, HeatCapacity())) PlayAt(config.OverheatEvent, HullPosition);
            }
            PlayAt(config.FireEvent, HullPosition);
        }

        void FireAlong(Transform muzzle, Vector3 direction, float scale, float time, Vector3 inherit)
        {
            // The Sparrow's round, fired the Sparrow's way: its prefab (look and impact effects), detached, stopped by the
            // first prism, swelling in flight (FullAutoActionExecutor's flightGrowthFactor).
            gun.FireGun(muzzle, config.ProjectileSpeed, inherit, scale, true, time, 0f, FiringPatterns.Default, 0,
                        detachAfterSpawn: true, stopOnFirstPrismImpact: true, aimDirection: direction,
                        flightGrowthFactor: config.RoundGrowthFactor);
        }

        /// <summary>
        /// The guns cool every frame — faster while Charge Mode's cords are out ("using charge lasers cools the guns") —
        /// and recover once cool enough. While they are overheated in SPACE Mode the base trail lays DANGER prisms
        /// (the fleet spawner's danger mode, written on EDGES only, rule 48).
        /// </summary>
        void UpdateHeat(float dt)
        {
            _heat.Cool(config.CoolRate + config.ChargeCoolRate * _cordsOut01, config.OverheatRecoverBelow, dt, HeatCapacity());
            bool danger = _heat.Overheated && IsOn(Mode.Destruction);
            if (danger && !_dangerHeld && _status.VesselPrismController)
            {
                _status.VesselPrismController.EnableDangerMode(null, Vector3.one);
                _dangerHeld = true;
            }
            else if (!danger) ReleaseDanger();
        }

        /// <summary>SPACE: the heat the guns hold, in Space 0 capacities (×1 → ×3.33 at Space 10: a full five-gun
        /// burst runs ~20 s instead of ~6 s). Read on the replicated level, so every peer's copy overheats on the same round.</summary>
        float HeatCapacity() => Mathf.Max(0.1f, config.HeatCapacity.EvaluateReplicated(_status));

        void ReleaseDanger()
        {
            if (!_dangerHeld) return;
            _dangerHeld = false;
            var prisms = _status?.VesselPrismController;
            if (prisms) prisms.DisableDangerMode();
        }

        // ------------------------------------------------------------------ Ring Detonation

        void Launch()
        {
            var body = TakeBody();
            if (!body) return;
            // A still copy of the mounted ring, posed where it is (the hull draws it: a baked copy of the model's ring).
            hull.DressThrownRing(body);
            body.SetActive(true);
            Vector3 from = body.transform.position;
            Vector3 velocity = _status.Transform.forward * config.RingLaunchSpeed + _status.Course * _status.Speed;
            _flights.Add(new Flight { Body = body, Velocity = velocity, Age = 0f, Previous = from });
            PlayAt(config.RingLaunchEvent, from);
        }

        GameObject TakeBody()
        {
            GameObject body = null;
            while (_idleBodies.Count > 0 && !body)
            {
                body = _idleBodies[_idleBodies.Count - 1];
                _idleBodies.RemoveAt(_idleBodies.Count - 1);
            }
            if (!body)
            {
                body = new GameObject($"BrittleStarRing::{_status.PlayerName}");
                body.AddComponent<MeshFilter>();
                body.AddComponent<MeshRenderer>();
            }
            return body;
        }

        void ReturnBody(GameObject body)
        {
            if (!body) return;
            body.SetActive(false);
            _idleBodies.Add(body);
        }

        void UpdateFlights(float dt)
        {
            for (int f = _flights.Count - 1; f >= 0; f--)
            {
                var flight = _flights[f];
                if (!flight.Body) { _flights.RemoveAt(f); continue; }
                var t = flight.Body.transform;
                flight.Previous = t.position;
                Vector3 next = flight.Previous + flight.Velocity * dt;
                t.position = next;
                t.Rotate(0f, 0f, config.RingFlightSpin * dt, Space.Self);
                flight.Age += dt;
                _flights[f] = flight;

                if (flight.Age >= config.RingFuseSeconds || Touches(flight.Previous, next, flight.Age))
                {
                    Detonate(next, t.rotation);
                    ReturnBody(flight.Body);
                    _flights.RemoveAt(f);
                }
            }
        }

        /// <summary>Does the ring's swept path this frame touch mass (any domain but the pilot's own fresh trail) or an
        /// enemy hull?</summary>
        bool Touches(Vector3 a, Vector3 b, float age)
        {
            if (age < 0.06f) return false;   // clear of the hull it left
            float reach = hull.RingRadius * config.RingContactRadiusScale;
            var index = PrismSpatialIndex.Instance;
            if (index && index.IsAvailable)
            {
                index.QuerySegment(a, b, reach, _candidates);
                for (int i = 0; i < _candidates.Count; i++)
                {
                    var prism = _candidates[i];
                    if (!prism || prism.destroyed) continue;
                    if (SelfTrailContactConfigSO.SuppressesSkimContact(prism, _status)) continue;
                    _candidates.Clear();
                    return true;
                }
                _candidates.Clear();
            }

            if (_gameData == null || _gameData.Players == null) return false;
            Vector3 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            float touch = reach + VesselHitRadius;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var victim = EnemyStatus(players[i]);
                if (victim == null) continue;
                Vector3 p = victim.Transform.position;
                float s = abLenSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLenSq) : 0f;
                if ((p - (a + ab * s)).sqrMagnitude <= touch * touch) return true;
            }
            return false;
        }

        /// <summary>
        /// The RING EXPLOSION (Garrett, 2026-10-14): a flat disk lying in the ring's plane, centred where it blew,
        /// <c>ringBlastThicknessScale</c> (×4, 2026-10-18) times as thick as the ring (× Charge) and as wide as the cell's
        /// NUCLEUS. It is swept outward from the ring's own radius to the nucleus's over <c>ringBlastSeconds</c>, drawn in
        /// the thrown ring's own frame (<paramref name="ringRotation"/>), so its five-fold look starts from the ring's points.
        /// </summary>
        void Detonate(Vector3 at, Quaternion ringRotation)
        {
            PlayAt(config.RingBlastEvent, at);
            float half = (hull ? hull.RingHalfThickness : 0.2f) * Mathf.Max(0.1f, config.RingBlastThicknessScale)
                       * Mathf.Max(0.1f, config.BlastScale.EvaluateReplicated(_status));
            Vector3 normal = ringRotation * Vector3.forward;
            float inner = hull ? hull.RingRadius : 1f;
            var wave = new Wave
            {
                Centre = at,
                Normal = normal,
                StartsAt = Time.time,
                InnerRadius = inner,
                OuterRadius = Mathf.Max(inner, NucleusRadiusAt(at)),
                LastRadius = 0f,
                HalfThickness = half,
            };
            wave.Band = TakeBand(out wave.BandMesh);
            if (wave.Band) wave.Band.transform.SetPositionAndRotation(at, ringRotation);
            _waves.Add(wave);
        }

        /// <summary>The nucleus's geometric radius of the cell the ring blew in (or the nearest active one), else the
        /// configured fallback. The marker's GEOMETRY, not its control radius: a mode that borrows the nucleus as play
        /// geometry collapses the control radius to 0 while the nucleus is still there (Cell.NucleusVisualWorldRadius).</summary>
        float NucleusRadiusAt(Vector3 at)
        {
            var cell = Cell.FindCellContaining(at) ?? Cell.FindNearestActiveCell(at);
            float r = cell ? cell.NucleusVisualWorldRadius : 0f;
            if (r <= 0f && cell) r = cell.ExpectedNucleusWorldRadius;
            return r > 0f ? r : config.RingBlastFallbackRadius;
        }

        /// <summary>
        /// Sweep the disk outward in steps (a handful of queries per explosion, not one per frame), breaking every prism it
        /// passes — any domain, friendly fire; a shield pops first, super-shielded mass refuses (<see cref="Prism.Damage"/>)
        /// — and scoring every rival hull it passes. The band grows with it and goes when it reaches the nucleus's edge.
        /// </summary>
        void UpdateWaves()
        {
            const int Steps = 10;
            for (int w = _waves.Count - 1; w >= 0; w--)
            {
                var wave = _waves[w];
                float t = Mathf.Clamp01((Time.time - wave.StartsAt) / config.RingBlastSeconds);
                float radius = Mathf.Lerp(wave.InnerRadius, wave.OuterRadius, Mathf.Sin(t * Mathf.PI * 0.5f));
                float stride = (wave.OuterRadius - wave.InnerRadius) / Steps;
                if (radius - wave.LastRadius >= stride * 0.999f || t >= 1f)
                {
                    SweepWave(wave, wave.LastRadius, radius);
                    wave.LastRadius = radius;
                    _waves[w] = wave;
                }
                if (wave.BandMesh) BuildBand(wave.BandMesh, wave, radius, t);
                if (t >= 1f)
                {
                    ReturnBand(wave.Band);
                    _waves.RemoveAt(w);
                }
            }
        }

        void SweepWave(in Wave wave, float fromRadius, float toRadius)
        {
            var index = PrismSpatialIndex.Instance;
            if (index && index.IsAvailable)
            {
                index.QuerySphere(wave.Centre, toRadius, _candidates);
                for (int i = 0; i < _candidates.Count; i++)
                {
                    var prism = _candidates[i];
                    if (!prism || prism.destroyed) continue;
                    var pt = prism.transform;
                    Vector3 d = pt.position - wave.Centre;
                    float along = Vector3.Dot(d, wave.Normal);
                    // The disk touches the prism if their slabs overlap: the prism's own half-extent along the normal
                    // (a box's support distance) plus the disk's half-thickness.
                    Vector3 ls = pt.lossyScale;
                    float extent = 0.5f * (Mathf.Abs(Vector3.Dot(pt.right, wave.Normal)) * Mathf.Abs(ls.x)
                                         + Mathf.Abs(Vector3.Dot(pt.up, wave.Normal)) * Mathf.Abs(ls.y)
                                         + Mathf.Abs(Vector3.Dot(pt.forward, wave.Normal)) * Mathf.Abs(ls.z));
                    if (!BrittleStarRules.DiskReaches(d, wave.Normal, wave.HalfThickness, toRadius, extent, 0f)) continue;
                    Vector3 radial = d - wave.Normal * along;
                    if (radial.sqrMagnitude < fromRadius * fromRadius) continue;   // already swept
                    if (SelfTrailContactConfigSO.SuppressesSkimContact(prism, _status)) continue;
                    Vector3 push = radial.sqrMagnitude > 1e-6f ? radial.normalized * 60f : wave.Normal * 60f;
                    prism.Damage(push, _status.Domain, _status.PlayerName);
                }
                _candidates.Clear();
            }

            if (_gameData == null || _gameData.Players == null || !ElementalTransfer.IsDecidedHere(_status)) return;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var victim = EnemyStatus(players[i]);
                if (victim == null) continue;
                Vector3 d = victim.Transform.position - wave.Centre;
                if (!BrittleStarRules.DiskReaches(d, wave.Normal, wave.HalfThickness, toRadius, VesselHitRadius, VesselHitRadius))
                    continue;
                Vector3 radial = d - wave.Normal * Vector3.Dot(d, wave.Normal);
                // Each hull is scored once per explosion: by the step whose sweep its edge falls in.
                if (fromRadius > 0f && radial.magnitude <= fromRadius + VesselHitRadius) continue;
                ScoreWaveHit(victim, radial);
            }
        }

        // ---- the explosion's band: one dynamic annulus mesh per live explosion, pooled

        readonly List<GameObject> _idleBands = new();
        readonly List<Vector3> _bandVerts = new();
        readonly List<Vector3> _bandNormals = new();
        readonly List<int> _bandTris = new();

        GameObject TakeBand(out Mesh mesh)
        {
            mesh = null;
            GameObject band = null;
            while (_idleBands.Count > 0 && !band)
            {
                band = _idleBands[_idleBands.Count - 1];
                _idleBands.RemoveAt(_idleBands.Count - 1);
            }
            if (!band)
            {
                band = new GameObject($"BrittleStarRingExplosion::{(_status != null ? _status.PlayerName : name)}");
                band.AddComponent<MeshFilter>().sharedMesh = new Mesh { name = "BrittleStarRingExplosion" };
                band.AddComponent<MeshRenderer>();
            }
            mesh = band.GetComponent<MeshFilter>().sharedMesh;
            // The domain colour: the material the hull's domain slot is painted with (ShipHelper.ApplyShipMaterial).
            var domain = hull ? hull.DomainMaterial : null;
            if (domain) band.GetComponent<MeshRenderer>().sharedMaterial = domain;
            band.SetActive(true);
            return band;
        }

        void ReturnBand(GameObject band)
        {
            if (!band) return;
            band.SetActive(false);
            _idleBands.Add(band);
        }

        /// <summary>The explosion's look at this moment (<see cref="BrittleStarBlastForm"/>): faceted volumes - the front at
        /// the disk's true edge with five crests, the thrown ring's pentagon grown, five coiling arms led by crystals - all
        /// turning on with the ring's spin. The lists are reused, so a frame allocates nothing but the mesh upload.</summary>
        void BuildBand(Mesh mesh, in Wave wave, float radius, float t01)
        {
            float spinSign = config.RingFlightSpin < 0f ? -1f : 1f;
            var form = new BrittleStarBlastForm.Settings
            {
                RingRadius = wave.InnerRadius,
                Front = radius,
                HalfThickness = wave.HalfThickness,
                FrontWidth = config.RingBlastFrontWidth,
                FrontHeight = config.RingBlastFrontHeight,
                Phase = BrittleStarBlastForm.PhaseAt(0f, spinSign * config.RingBlastSpinDegrees * Mathf.Deg2Rad, t01),
                T01 = t01,
                CrestDepth = config.RingBlastCrestDepth,
                PentagonLag = config.RingBlastPentagonLag,
                ArmWidth = config.RingBlastArmWidth,
                ArmWiggle = config.RingBlastArmWiggle,
                ArmCurl = spinSign * config.RingBlastArmCurlDegrees * Mathf.Deg2Rad,
                Relief = config.RingBlastRelief,
            };
            BrittleStarBlastForm.Build(form, _bandVerts, _bandNormals, _bandTris);
            mesh.Clear();
            mesh.SetVertices(_bandVerts);
            mesh.SetNormals(_bandNormals);
            mesh.SetTriangles(_bandTris, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>The ring explosion's hit on an enemy hull, under the LOCKED PvP rule: admitted through
        /// <see cref="CombatHitDrain.TryAdmit"/> (a warded victim is neither scored on nor robbed), its petals ejected
        /// at the class's fleet price, the score raised on the stats channel. It never moves the victim.</summary>
        void ScoreWaveHit(IVesselStatus victim, Vector3 outward)
        {
            var hitClass = config.RingBlastHitClass;
            if (!CombatHitDrain.TryAdmit(victim, _status, hitClass, config.RingBlastHitCooldown,
                                         ElementalDebuffSources.Explosion, out int superseded))
                return;
            CombatHitDrain.Apply(victim, _status, hitClass, superseded, outward.normalized * 40f,
                                 ElementalDebuffSources.Explosion);
            if (config.OnCombatHitLanded)
                config.OnCombatHitLanded.Raise(new CombatHitStats
                {
                    ShooterName = _status.PlayerName,
                    VictimName = victim.PlayerName,
                    HitClass = hitClass,
                    SupersededRank = superseded,
                });
        }

        void ClearFlights()
        {
            for (int i = 0; i < _flights.Count; i++) ReturnBody(_flights[i].Body);
            _flights.Clear();
            for (int i = 0; i < _waves.Count; i++) ReturnBand(_waves[i].Band);
            _waves.Clear();
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
        /// The AI flies the Brittle Star with the SAME replicated controls a human presses (vessel-ai's input-only rule).
        /// It holds the trigger while rival mass or a rival hull lies on the line ahead, and picks its mode every few
        /// seconds: a dense wall inside cord reach — or overheated guns, which the cords cool — takes CHARGE Mode (the
        /// held trigger makes the cords sabers); a dense wall further out takes SPACE Mode's five guns; otherwise SPEED
        /// when its target is far round the nose, else CREATION. It throws its ring into a big cluster, or near any
        /// cluster when its spare-ring stock is full (a crystal would be wasted).
        /// </summary>
        void Autopilot()
        {
            var handler = _status.ActionHandler;
            var ai = _status.AIPilot;
            if (!handler || !ai) return;
            float now = Time.time;
            Vector3 pos = HullPosition;
            Vector3 fwd = _status.Transform.forward;

            if (now >= _aiNextScan)
            {
                _aiNextScan = now + 0.25f;
                float range = BrittleStarRules.RangeFor(config.ProjectileSpeed,
                    config.ProjectileTime * Mathf.Max(0.1f, config.RangeMultiplier.EvaluateReplicated(_status)));
                float reach = config.CordLength * config.CordLooseLengthScale;
                _aiTargetsAhead = CountRivalPrisms(pos, pos + fwd * range, 8f, segment: true);
                _aiTargetsReach = CountRivalPrisms(pos, pos + fwd * reach, 12f, segment: true);
                _aiTargetsNear = CountRivalPrisms(pos + fwd * (range * 0.6f), default, range * 0.4f, segment: false);
                if (EnemyAhead(pos, fwd, range)) _aiTargetsAhead += config.AiFireMinTargets;
                if (EnemyAhead(pos, fwd, reach)) _aiTargetsReach += config.AiFireMinTargets;
            }

            // The trigger, with a minimum hold / release so it does not chatter.
            bool wantFire = _aiTargetsAhead >= config.AiFireMinTargets;
            if (wantFire != _aiHoldingFire && now - _aiTriggerSince >= config.AiTriggerMinSeconds
                && TryResolve(handler, BrittleStarControl.Fire, out var fireInput))
            {
                _aiHoldingFire = wantFire;
                _aiTriggerSince = now;
                if (wantFire) handler.PerformShipControllerActionsReplicated(fireInput);
                else handler.StopShipControllerActionsReplicated(fireInput);
            }

            if (now >= _aiNextMode && _ring.ModesAvailable)
            {
                _aiNextMode = now + config.AiModeIntervalSeconds;
                // Speed is the AI boost policy's call when one is wired (the fleet's straight-and-runway law);
                // otherwise a target far round the nose. A dense wall ahead always wins: clear it first.
                bool wantSpeed = _aiSpeedWish ?? Vector3.Angle(fwd, ai.TargetPosition - pos) >= config.AiSpeedModeDegrees;
                var want = _aiTargetsReach >= config.AiDestructionMinTargets
                           || _heat.Overheated && _aiTargetsReach >= config.AiFireMinTargets ? BrittleStarControl.ChargeMode
                         : _aiTargetsAhead >= config.AiDestructionMinTargets && !_heat.Overheated ? BrittleStarControl.DestructionMode
                         : wantSpeed ? BrittleStarControl.SpeedMode
                         : BrittleStarControl.CreationMode;
                var wantMode = want switch
                {
                    BrittleStarControl.SpeedMode => BrittleStarRules.Mode.Speed,
                    BrittleStarControl.DestructionMode => BrittleStarRules.Mode.Destruction,
                    BrittleStarControl.ChargeMode => BrittleStarRules.Mode.Charge,
                    _ => BrittleStarRules.Mode.Creation,
                };
                if (wantMode != _selected && TryResolve(handler, want, out var modeInput))
                {
                    handler.PerformShipControllerActionsReplicated(modeInput);
                    handler.StopShipControllerActionsReplicated(modeInput);
                }
            }

            if (now >= _aiNextDetonate && _ring.CanLaunch
                && (_ring.Spares >= config.MaxSpareRings && _aiTargetsNear >= config.AiFireMinTargets
                    || _aiTargetsNear >= config.AiDetonateMinTargets)
                && TryResolve(handler, BrittleStarControl.RingDetonation, out var ringInput))
            {
                _aiNextDetonate = now + 2f;
                handler.PerformShipControllerActionsReplicated(ringInput);
                handler.StopShipControllerActionsReplicated(ringInput);
            }
        }

        int CountRivalPrisms(Vector3 a, Vector3 b, float radius, bool segment)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return 0;
            if (segment) index.QuerySegment(a, b, radius, _candidates);
            else index.QuerySphere(a, radius, _candidates);
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

        bool EnemyAhead(Vector3 pos, Vector3 fwd, float range)
        {
            if (_gameData == null || _gameData.Players == null) return false;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var victim = EnemyStatus(players[i]);
                if (victim == null) continue;
                Vector3 d = victim.Transform.position - pos;
                if (d.sqrMagnitude > range * range) continue;
                if (Vector3.Angle(fwd, d) <= 8f) return true;
            }
            return false;
        }

        /// <summary>Which input carries <paramref name="control"/> on THIS vessel — retried until the binding maps have
        /// filled (vessel rule 6), never latched on a miss.</summary>
        bool TryResolve(R_VesselActionHandler handler, BrittleStarControl control, out InputEvents input)
        {
            if (_boundInputs.TryGetValue(control, out input)) return true;
            foreach (InputEvents candidate in System.Enum.GetValues(typeof(InputEvents)))
            {
                _bindScratch.Clear();
                handler.CollectBoundActions(candidate, _bindScratch);
                for (int i = 0; i < _bindScratch.Count; i++)
                    if (_bindScratch[i] is BrittleStarActionSO action && action.Kind == control)
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

        /// <summary>Is THIS machine the one that simulates this hull's autopilot? The owner on the networked paths (the
        /// host for an AI); unconditionally yes with no network session running (the single-player spawn path never
        /// network-spawns its Player). The fleet's rule (<c>MantaStingActionExecutor.IsSimAuthority</c>), kept local so
        /// this executor does not reach into another vessel's.</summary>
        bool IsSimAuthority()
        {
            var player = _status?.Player;
            if (player == null) return false;
            if (player.IsNetworkOwner) return true;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm == null || !nm.IsListening;
        }

        // ------------------------------------------------------------------ feedback

        Vector3 HullPosition => _status != null && _status.Transform ? _status.Transform.position : transform.position;

        static void PlayAt(EventReference reference, Vector3 at)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, at);
        }
    }
}
