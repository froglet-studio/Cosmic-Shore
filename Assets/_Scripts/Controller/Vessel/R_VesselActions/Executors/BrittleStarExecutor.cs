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
    /// <item><b>Change Modes</b> (A / B / X): Speed, Creation or Destruction; pressing the selected mode again returns to
    /// Cruise. The modes need the support ring.</item>
    /// <item><b>Speed Mode</b> (Time): the hull's cruise × <c>speedModeMultiplier</c>, every thruster lit, and the base
    /// trail's spacing stretched so production per second does not rise.</item>
    /// <item><b>Creation Mode</b> (Mass): the trail becomes a PENTAGON of blocks laid from the ring's five points, turning
    /// with the ring's firing rotation.</item>
    /// <item><b>Destruction Mode</b> (Space) and <b>Fire</b> (RT, held): one stream from the turret the right stick
    /// points at (the cockpit gun with the stick centred), or all five turrets in Destruction Mode with larger rounds;
    /// the stick's deflection opens the spread. Friendly fire is on (the round's prefab).</item>
    /// <item><b>Ring Detonation</b> (Charge, LT): throw the ring forward; it blows on contact or on its fuse — an
    /// all-domain blast, then a wave out in the ring's plane. A stored crystal charge rebuilds a new ring at once; with
    /// none the hull flies ringless, modes and all, until it collects a crystal.</item>
    /// <item><b>Crystals</b> (<see cref="BrittleStarRingChargeByCrystalEffectSO"/>): rebuild a missing ring, else store
    /// the one charge, else OVERDRIVE — all three modes for a few seconds.</item>
    /// </list>
    ///
    /// <para><b>Simulated on EVERY peer.</b> Every input it reacts to is replicated — the presses round-trip through the
    /// server, crystal pickups run through <c>NetworkVesselImpactor</c>'s ClientRpc, and the right stick that aims is a
    /// replicated input NetworkVariable — so every machine's copy holds the same mode, the same ring and the same charge,
    /// and lays the same trail (vessel rule 48: nothing simulated keys off a local-only state). Prism destruction is per
    /// peer by design; each peer's copy of the ring and the rounds breaks its own copy of the mass. Scored hits are decided
    /// once, on the shooter's owner (<see cref="ElementalTransfer.IsDecidedHere"/>).</para>
    ///
    /// <para><b>PvP is petals only (LOCKED).</b> Nothing here moves, slows or disables another pilot's vessel; a hit on
    /// an enemy hull is a scored combat hit that moves petals (see the config's type doc).</para>
    /// </summary>
    public sealed class BrittleStarExecutor : ShipActionExecutorBase
    {
        public enum Mode { Cruise = 0, Speed = 1, Creation = 2, Destruction = 3 }

        [Header("Wiring (serialized on the prefab so a missing wire shows in the inspector)")]
        [Tooltip("Every number this executor reads. Wired directly (vessel rule 20).")]
        [SerializeField] BrittleStarConfigSO config;
        [Tooltip("The procedural hull: the ring, the six muzzles, the ring mesh a detonation throws.")]
        [SerializeField] BrittleStarHullBuilder hull;
        [Tooltip("Fires every turret's rounds (its ProjectileFactory pools the round prefab).")]
        [SerializeField] Gun gun;
        [Tooltip("The fleet's shared turn-end channel: the trigger lets go and every thrown ring is cleared.")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

        [Inject] GameDataSO _gameData;

        IVesselStatus _status;
        VesselImpactor _impactor;
        readonly BrittleStarRules.RingState _ring = new();
        BrittleStarRules.Mode _selected = BrittleStarRules.Mode.Cruise;

        float _speed01, _creation01, _destruction01;
        float _ringPhase;            // radians: the support ring's firing rotation about the nose axis
        int _aimedPoint = -1;        // the turret the stick points at, or -1 (the cockpit gun)
        bool _firing;
        float _fireOwed;
        uint _shotSerial;
        bool _penHeld;
        float _creationOwed;
        Vector3 _lastHullPosition;
        bool _hasLastHullPosition;
        readonly Trail _creationTrail = new();

        // Thrown rings and the plane waves they leave.
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
        }

        readonly List<Flight> _flights = new();
        readonly List<Wave> _waves = new();
        readonly List<GameObject> _idleBodies = new();
        readonly List<Prism> _candidates = new();
        readonly List<ShipActionSO> _bindScratch = new();
        readonly Dictionary<BrittleStarControl, InputEvents> _boundInputs = new();

        // Autopilot.
        float _aiNextScan, _aiNextMode, _aiTriggerSince, _aiNextDetonate;
        int _aiTargetsAhead, _aiTargetsNear;
        bool _aiHoldingFire;
        bool? _aiSpeedWish;          // the AI boost policy's call (BrittleStarSpeedModeAIPolicySO); null = no policy

        const float VesselHitRadius = 6f;   // how far from a hull's centre the ring and the wave count as touching it
        const float MinSpeedToLay = 3f;      // the fleet spawner's own threshold

        // ------------------------------------------------------------------ read by the HUD and the animation

        /// <summary>Eased 0..1: how far each mode's effect is on (Overdrive raises all three).</summary>
        public float Speed01 => _speed01;
        public float Creation01 => _creation01;
        public float Destruction01 => _destruction01;
        /// <summary>The mode the pilot has selected (on only while the ring is mounted, or in Overdrive).</summary>
        public Mode Selected => (Mode)(int)_selected;
        public bool IsOn(Mode mode) => BrittleStarRules.IsOn((BrittleStarRules.Mode)(int)mode, _selected, _ring);
        /// <summary>0 with no ring, rising as one is built, 1 while mounted.</summary>
        public float RingPresence01 => _ring.Presence01(config ? config.RebuildSeconds : 0.5f);
        public bool RingMounted => _ring.Mounted;
        public int Charges => _ring.Charges;
        public bool Overdrive => _ring.Overdrive;
        /// <summary>Overdrive's remaining share of its duration, 0..1.</summary>
        public float Overdrive01 => config && config.OverdriveSeconds > 0f
            ? Mathf.Clamp01(_ring.OverdriveRemaining / config.OverdriveSeconds) : 0f;
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
        /// <summary>The turret the right stick points at, or -1 (the cockpit gun).</summary>
        public int AimedPoint => _aimedPoint;
        public BrittleStarConfigSO Config => config;

        // ------------------------------------------------------------------ lifecycle

        public override void Initialize(IVesselStatus shipStatus)
        {
            ClearFlights();
            ReleasePen();
            _status = shipStatus;
            _impactor = _status != null && _status.Transform ? _status.Transform.GetComponent<VesselImpactor>() : null;
            if (!hull && _status != null && _status.Transform)
                hull = _status.Transform.GetComponentInChildren<BrittleStarHullBuilder>(true);
            if (gun) gun.Initialize(shipStatus);

            _ring.Reset();
            _selected = BrittleStarRules.Mode.Cruise;
            _speed01 = _creation01 = _destruction01 = 0f;
            _ringPhase = 0f;
            _aimedPoint = -1;
            _firing = false;
            _fireOwed = 0f;
            _shotSerial = 0;
            _creationOwed = 0f;
            _hasLastHullPosition = false;
            _creationTrail.Clear();
            _boundInputs.Clear();
            _aiHoldingFire = false;
            _aiSpeedWish = null;
            _aiNextScan = _aiNextMode = _aiNextDetonate = 0f;

            if (!config)
                CSDebug.LogError($"[BrittleStar] {name}: BrittleStarExecutor has no BrittleStarConfig — the vessel has no " +
                                 "modes, no guns and no ring. Wire Assets/_SO_Assets/VesselActions/BrittleStar/BrittleStarConfig.asset.");
            if (!hull)
                CSDebug.LogError($"[BrittleStar] {name}: no BrittleStarHullBuilder under the vessel — there is no ring to " +
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
            ReleasePen();
            if (_status?.VesselTransformer is BrittleStarVesselTransformer transformer) transformer.SpeedModeMultiplier = 1f;
            if (_status?.VesselPrismController is BrittleStarPrismController prisms) prisms.SpacingMultiplier = 1f;
        }

        void OnDestroy()
        {
            for (int i = 0; i < _idleBodies.Count; i++)
                if (_idleBodies[i]) Destroy(_idleBodies[i]);
            _idleBodies.Clear();
        }

        void OnTurnEnd()
        {
            _firing = false;
            ClearFlights();
            ReleasePen();
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
                _ => config.ModeOffEvent,
            }, HullPosition);
        }

        public void BeginFire()
        {
            if (_firing) return;
            _firing = true;
            // One volley is owed the instant the trigger goes down.
            _fireOwed = config ? 1f / Mathf.Max(0.01f, config.FireRate) : 0f;
        }

        public void EndFire() => _firing = false;

        /// <summary>Ring Detonation: throw the ring if there is one. No cooldown — only a ring.</summary>
        public void DetonateRing()
        {
            if (!config || !hull || !hull.Ring || _status == null) return;
            if (!_ring.TryLaunch(config.RebuildSeconds)) return;
            Launch();
            if (_ring.Rebuilding) PlayAt(config.RingRebuildEvent, HullPosition);
        }

        /// <summary>A crystal (any kind): rebuild, store, or Overdrive (<see cref="BrittleStarRules.RingState"/>).</summary>
        public void CollectCrystal()
        {
            if (!config) return;
            var outcome = _ring.CollectCrystal(config.RebuildSeconds, config.OverdriveSeconds);
            PlayAt(outcome switch
            {
                BrittleStarRules.RingState.CrystalOutcome.Rebuilt => config.RingRebuildEvent,
                BrittleStarRules.RingState.CrystalOutcome.Stored => config.ChargeStoredEvent,
                _ => config.OverdriveEvent,
            }, HullPosition);
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
            AimRing(dt);
            UpdateCreation();
            UpdateFire(dt);
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
        }

        /// <summary>The transformer's speed multiplier and the base trail's spacing, from one eased value.</summary>
        void ApplySpeedMode()
        {
            float multiplier = Mathf.Max(1f, config.SpeedModeMultiplier.EvaluateReplicated(_status));
            float now = Mathf.Lerp(1f, multiplier, _speed01);
            if (_status.VesselTransformer is BrittleStarVesselTransformer transformer) transformer.SpeedModeMultiplier = now;
            if (_status.VesselPrismController is BrittleStarPrismController prisms)
                prisms.SpacingMultiplier = Mathf.Lerp(1f, now, config.SpeedModeSpacingFollow);
        }

        // ------------------------------------------------------------------ aim: the ring's firing rotation

        Vector2 AimStick()
        {
            if (_status.AutoPilotEnabled) return Vector2.zero;   // the AI aims with its nose and the cockpit gun
            var input = _status.Player != null ? _status.InputStatus : null;
            return input != null ? Vector2.ClampMagnitude(input.RightNormalizedJoystickPosition, 1f) : Vector2.zero;
        }

        void AimRing(float dt)
        {
            Vector2 stick = AimStick();
            float? aim = BrittleStarRules.AimAngle(stick, config.AimDeadzone);
            if (aim == null)
            {
                _aimedPoint = -1;
                return;   // the ring holds its last rotation
            }
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
            Vector3 local = new(Mathf.Cos(a) * r, Mathf.Sin(a) * r, hull.Ring ? hull.Ring.localPosition.z : 0f);
            return hullT.TransformPoint(local);
        }

        // ------------------------------------------------------------------ Creation Mode: the pentagon trail

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

            // Speed Mode stretches the pentagon's wavelength too, so Overdrive does not double production.
            float spacing = config.CreationSpacing * (_status.VesselPrismController is BrittleStarPrismController bp
                ? Mathf.Max(1f, bp.SpacingMultiplier) : 1f);
            _creationOwed += travelled;
            int lays = Mathf.Min(Mathf.FloorToInt(_creationOwed / spacing), config.CreationMaxPerFrame);
            if (lays <= 0) return;
            _creationOwed = Mathf.Min(_creationOwed - lays * spacing, spacing);   // excess DROPPED (rule 17)
            var channel = prisms.PrismSpawnChannel;
            if (!channel) return;
            for (int k = 0; k < lays; k++)
                for (int i = 0; i < BrittleStarHullForm.Points; i++)
                    LayCreationPrism(channel, prisms.SpawnPrismType, i);
        }

        /// <summary>One pentagon block at ring point <paramref name="index"/>: faces along the course with its broad
        /// side tangent to the ring, so the five read as a pentagon. MASS sizes it, read at lay time.</summary>
        void LayCreationPrism(PrismEventChannelWithReturnSO channel, PrismType type, int index)
        {
            Vector3 pos = RingPoint(index, config.CreationRadiusScale);
            Vector3 course = _status.Course.sqrMagnitude > 1e-6f ? _status.Course : _status.Transform.forward;
            Vector3 radial = Vector3.ProjectOnPlane(pos - HullPosition, course);
            if (radial.sqrMagnitude < 1e-6f) radial = _status.Transform.up;
            if (!SafeLookRotation.TryGet(course, radial.normalized, out var rot, this, logError: false)) return;

            Vector3 scale = config.CreationBlockSize * Mathf.Max(0.1f, config.CreationBlockScale.EvaluateReplicated(_status));
            var ret = channel.RaiseEvent(new PrismEventData
            {
                ownDomain = _status.Domain,
                Rotation = rot,
                SpawnPosition = pos,
                Scale = scale,
                PrismType = type,
            });
            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) return;

            prism.TargetScale = scale;
            prism.ownerID = _status.PlayerName;
            prism.ChangeTeam(_status.Domain);
            prism.waitTime = config.CreationWaitTime;
            _creationTrail.Add(prism);
            prism.prismProperties.Index = (ushort)Mathf.Min(_creationTrail.TrailList.Count - 1, ushort.MaxValue);
            prism.Initialize(_status.PlayerName);
            // A Mass-scaled prism is a stated size: admit it past the pool's scale window AFTER Initialize (whose
            // reset re-clamps it), or a big one is trimmed with no error.
            prism.AdmitTargetScale(scale);
            prism.TargetScale = scale;
            prism.AssignTrail(_creationTrail);   // AFTER Initialize: pool reuse clears trail membership
        }

        void ReleasePen()
        {
            if (!_penHeld) return;
            _penHeld = false;
            var prisms = _status?.VesselPrismController;
            if (prisms) prisms.SetSpawnerPaused(false);
        }

        // ------------------------------------------------------------------ shooting

        void UpdateFire(float dt)
        {
            if (!_firing || !gun || !hull) return;
            float interval = 1f / Mathf.Max(0.01f, config.FireRate);
            _fireOwed = Mathf.Min(_fireOwed + dt, interval * 3f);   // a hitch never discharges as a burst (rule 17)
            int volleys = Mathf.FloorToInt(_fireOwed / interval);
            if (volleys <= 0) return;
            _fireOwed -= volleys * interval;

            bool destruction = IsOn(Mode.Destruction);
            float time = config.ProjectileTime * Mathf.Max(0.1f, config.RangeMultiplier.EvaluateReplicated(_status));
            float scale = config.ProjectileScale * Mathf.Max(0.1f, config.ProjectileScaleMultiplier.EvaluateReplicated(_status))
                          * (destruction ? config.DestructionProjectileScale : 1f);
            float spread = BrittleStarRules.Spread01(AimStick(), config.AimDeadzone) * config.MaxSpreadDegrees;
            Vector3 inherit = _status.Course * _status.Speed;
            Vector3 up = _status.Transform.up;

            for (int v = 0; v < volleys; v++)
            {
                if (destruction)
                    for (int i = 0; i < BrittleStarHullForm.Points; i++) FireFrom(hull.Muzzle(i), scale, time, spread, inherit, up);
                else
                    // Ringless, only the cockpit's own gun is left.
                    FireFrom(hull.Muzzle(_ring.Mounted ? _aimedPoint : -1), scale, time, spread, inherit, up);
            }
            PlayAt(config.FireEvent, HullPosition);
        }

        void FireFrom(Transform muzzle, float scale, float time, float spread, Vector3 inherit, Vector3 up)
        {
            if (!muzzle) return;
            Vector3 aim = BrittleStarRules.Deflect(_status.Transform.forward, up, spread, _shotSerial++);
            gun.FireGun(muzzle, config.ProjectileSpeed, inherit, scale, true, time, 0f, FiringPatterns.Default, 0,
                        detachAfterSpawn: true, stopOnFirstPrismImpact: true, aimDirection: aim);
        }

        // ------------------------------------------------------------------ Ring Detonation

        void Launch()
        {
            var body = TakeBody();
            if (!body) return;
            var ringT = hull.Ring;
            body.transform.SetPositionAndRotation(ringT.position, ringT.rotation);
            body.transform.localScale = ringT.lossyScale;
            body.SetActive(true);
            Vector3 velocity = _status.Transform.forward * config.RingLaunchSpeed + _status.Course * _status.Speed;
            _flights.Add(new Flight { Body = body, Velocity = velocity, Age = 0f, Previous = ringT.position });
            PlayAt(config.RingLaunchEvent, ringT.position);
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
            body.GetComponent<MeshFilter>().sharedMesh = hull.RingMesh;
            if (hull.TryGetComponent<MeshRenderer>(out var source))
                body.GetComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
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
                    Detonate(next, t.forward);
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

        /// <summary>The all-domain blast, then the wave out in the ring's plane.</summary>
        void Detonate(Vector3 at, Vector3 ringNormal)
        {
            float scale = Mathf.Max(0.1f, config.BlastScale.EvaluateReplicated(_status));
            float diameter = config.BlastDiameter * scale;
            if (config.BlastExplosions is { Length: > 0 })
            {
                var init = new AOEExplosion.InitializeStruct
                {
                    OwnDomain = _status.Domain,
                    AnnonymousExplosion = false,
                    Vessel = _status.Vessel,
                    OverrideMaterial = _status.AOEExplosionMaterial,
                    MaxScale = diameter,
                    SpawnPosition = at,
                    SpawnRotation = ringNormal.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(ringNormal) : Quaternion.identity,
                    // Friendly fire: "destroying both enemy and allied mass".
                    AffectSelfOverride = true,
                };
                ExplosionHelper.CreateExplosion(config.BlastExplosions, init, _impactor ? _impactor.DIContainer : null);
            }
            PlayAt(config.RingBlastEvent, at);

            float inner = diameter * 0.5f;
            _waves.Add(new Wave
            {
                Centre = at,
                Normal = ringNormal.sqrMagnitude > 1e-6f ? ringNormal.normalized : Vector3.forward,
                StartsAt = Time.time + config.PlaneWaveDelay,
                InnerRadius = inner,
                OuterRadius = inner * config.PlaneWaveReach,
                LastRadius = inner,
            });
        }

        /// <summary>
        /// The plane wave: a disc of <c>planeWaveHalfThickness</c> expanding from the blast's radius to
        /// <c>planeWaveReach</c> times it over <c>planeWaveSeconds</c>, breaking every prism it passes (any domain; a
        /// shield pops first, super-shielded mass refuses — <see cref="Prism.Damage"/>) and scoring a hit on every enemy
        /// hull it passes. Swept in a few rings rather than every frame, so a wide wave costs a handful of queries.
        /// </summary>
        void UpdateWaves()
        {
            const int Steps = 6;
            for (int w = _waves.Count - 1; w >= 0; w--)
            {
                var wave = _waves[w];
                float t = (Time.time - wave.StartsAt) / config.PlaneWaveSeconds;
                if (t < 0f) continue;
                float eased = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * 0.5f);
                float radius = Mathf.Lerp(wave.InnerRadius, wave.OuterRadius, eased);
                float stride = (wave.OuterRadius - wave.InnerRadius) / Steps;
                if (radius - wave.LastRadius >= stride * 0.999f || t >= 1f)
                {
                    SweepWave(wave, wave.LastRadius, radius);
                    wave.LastRadius = radius;
                    _waves[w] = wave;
                }
                if (t >= 1f) _waves.RemoveAt(w);
            }
        }

        void SweepWave(in Wave wave, float fromRadius, float toRadius)
        {
            float half = config.PlaneWaveHalfThickness;
            var index = PrismSpatialIndex.Instance;
            if (index && index.IsAvailable)
            {
                index.QuerySphere(wave.Centre, toRadius, _candidates);
                for (int i = 0; i < _candidates.Count; i++)
                {
                    var prism = _candidates[i];
                    if (!prism || prism.destroyed) continue;
                    Vector3 d = prism.transform.position - wave.Centre;
                    float along = Vector3.Dot(d, wave.Normal);
                    if (Mathf.Abs(along) > half) continue;
                    Vector3 radial = d - wave.Normal * along;
                    if (radial.sqrMagnitude < fromRadius * fromRadius * 0.81f) continue;   // already swept
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
                float along = Vector3.Dot(d, wave.Normal);
                if (Mathf.Abs(along) > half + VesselHitRadius) continue;
                Vector3 radial = d - wave.Normal * along;
                if (radial.magnitude > toRadius + VesselHitRadius) continue;
                ScoreWaveHit(victim, radial);
            }
        }

        /// <summary>The wave's hit on an enemy hull, under the LOCKED PvP rule: admitted through
        /// <see cref="CombatHitDrain.TryAdmit"/> (a warded victim is neither scored on nor robbed), its petals ejected
        /// at the class's fleet price, the score raised on the stats channel. It never moves the victim.</summary>
        void ScoreWaveHit(IVesselStatus victim, Vector3 outward)
        {
            var hitClass = config.PlaneWaveHitClass;
            if (!CombatHitDrain.TryAdmit(victim, _status, hitClass, config.PlaneWaveHitCooldown,
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
        /// The AI flies the Brittle Star with the SAME replicated controls a human presses (vessel-ai's input-only rule):
        /// it holds the trigger while rival mass or a rival hull lies on the line ahead, switches to Destruction Mode in
        /// front of a dense wall, to Speed Mode when its target is far round, and to Creation Mode otherwise, and throws
        /// its ring into a big cluster — or whenever a stored charge means it costs nothing.
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
                _aiTargetsAhead = CountRivalPrisms(pos, pos + fwd * range, 8f, segment: true);
                _aiTargetsNear = CountRivalPrisms(pos + fwd * (range * 0.6f), default, range * 0.4f, segment: false);
                if (EnemyAhead(pos, fwd, range)) _aiTargetsAhead += config.AiFireMinTargets;
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
                var want = _aiTargetsAhead >= config.AiDestructionMinTargets ? BrittleStarControl.DestructionMode
                         : wantSpeed ? BrittleStarControl.SpeedMode
                         : BrittleStarControl.CreationMode;
                var wantMode = want switch
                {
                    BrittleStarControl.SpeedMode => BrittleStarRules.Mode.Speed,
                    BrittleStarControl.DestructionMode => BrittleStarRules.Mode.Destruction,
                    _ => BrittleStarRules.Mode.Creation,
                };
                if (wantMode != _selected && TryResolve(handler, want, out var modeInput))
                {
                    handler.PerformShipControllerActionsReplicated(modeInput);
                    handler.StopShipControllerActionsReplicated(modeInput);
                }
            }

            if (now >= _aiNextDetonate && _ring.Mounted
                && (_ring.Charges > 0 && _aiTargetsNear >= config.AiFireMinTargets || _aiTargetsNear >= config.AiDetonateMinTargets)
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
