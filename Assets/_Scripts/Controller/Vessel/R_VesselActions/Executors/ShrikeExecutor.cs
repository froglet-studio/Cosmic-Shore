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
    /// The Shrike — a slow sniper that docks to the mass it touches, builds nests, slings itself across the cell and
    /// calls missiles down from its nests' turrets (design: <c>R_VesselActions/SHRIKE.md</c>; tuning:
    /// <see cref="ShrikeConfigSO"/>; source: the Shrike Studio, <c>Docs/Studios/VesselStudio/shrike.html</c>, whose
    /// <c>stepPilot</c> this class ports). Owns every piece of the vessel's state; the four
    /// <see cref="ShrikeActionSO"/> controls only call in.
    ///
    /// <list type="bullet">
    /// <item><b>Docking</b>: a touch on any block docks the hull to it (translation pinned, the nose still aims, faster:
    /// the fleet's restricted-stance turn). Docked, every cooldown runs ×1.6, ×2.5 in a nest or on super-shielded mass.</item>
    /// <item><b>Nest</b> (A): docked, undock (a push off the surface); flying, build a cup of super-shielded blocks round
    /// the hull, its turret flora on top, and dock in it (Mass: more blocks). At most three; a fourth retires the oldest.</item>
    /// <item><b>Void Lance</b> (RT, held): a hitscan beam that breaks every block it passes, super-shielded included,
    /// except the nest you are docked in (Charge: a wider beam). A rival hull on the line is a scored petal hit.</item>
    /// <item><b>Void Sling</b> (B): a tether shot anchors on the first block it meets; the hull flies straight there in
    /// exactly <c>slingSeconds</c>, laying a thick trail, and docks where it lands (Time: a shorter cooldown). A crystal
    /// resets it.</item>
    /// <item><b>Targeting Array</b> (LT, held): paint a rival hull inside the lock cone, else the centre of the mass
    /// nearest the aim; each of your nests launches one homing missile per paint while it has a health block to spare
    /// (Space: a wider cone).</item>
    /// </list>
    ///
    /// <para><b>Simulated on EVERY peer</b> (the Brittle Star's rule): the presses replicate, so every machine holds the
    /// same docks, nests, tethers and missiles and lays the same trail; prism destruction is per peer by design. Only
    /// the hull's own MOTION (the sling's flight, the undock push, the snap onto a surface) is written by the machine
    /// that owns it (<see cref="IsAuthority"/>); scored hits are decided once, on the shooter's owner
    /// (<see cref="ElementalTransfer.IsDecidedHere"/>).</para>
    ///
    /// <para><b>PvP is petals only (LOCKED).</b> Nothing here moves, slows or disables another pilot's vessel.</para>
    /// </summary>
    public sealed class ShrikeExecutor : ShipActionExecutorBase
    {
        [Header("Wiring (serialized on the prefab so a missing wire shows in the inspector)")]
        [Tooltip("Every number this executor reads. Wired directly (vessel rule 20).")]
        [SerializeField] ShrikeConfigSO config;
        [Tooltip("The procedural hull: its renderer's materials dress the turrets and missiles.")]
        [SerializeField] ShrikeHullBuilder hull;
        [Tooltip("The fleet's shared turn-end channel: triggers let go, the tether and missiles are cleared.")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

        [Inject] GameDataSO _gameData;

        enum DockKind { None = 0, Block = 1, Nest = 2 }
        enum LockKind { None = 0, Vessel = 1, Mass = 2 }

        sealed class Nest
        {
            public readonly List<Prism> Blocks = new();
            public readonly List<float> Born = new();   // each block's TimeCreated at lay: a pooled reuse is not ours
            public Vector3 Centre;
            public Quaternion Rotation;
            public int Health;
            public float Regen;
            public float FireCooldown;
            public int FiredForLock = -1;
            public bool TurretAlive = true;
            public GameObject Turret;
            public Vector3 TurretPosition;
            public Vector3 Up;
            public bool Dead;
        }

        sealed class Missile
        {
            public GameObject Body;
            public Vector3 Position;
            public Vector3 Direction;
            public Nest From;
            public float Age;
        }

        IVesselStatus _status;
        bool _pinned;

        // Docking.
        DockKind _dock;
        Prism _dockPrism;
        float _dockPrismBorn;
        Nest _dockNest;
        float _dockGrace;
        float _dockedFor;
        Vector3 _push;

        // Cooldowns (seconds left; they tick at the dock bonus's rate).
        float _lanceCd, _slingCd, _nestCd;
        bool _lanceHeld;

        // Sling: the tether in flight, then the boost.
        bool _tethering;
        Vector3 _tetherPos, _tetherDir;
        float _tetherWent;
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
        bool _painting;
        int _lockId;
        LockKind _lock;
        IVesselStatus _lockVessel;
        Vector3 _lockPoint;
        float _lockScanAt;

        readonly List<Nest> _nests = new();
        readonly List<Missile> _missiles = new();
        readonly List<GameObject> _idleMissiles = new();
        readonly List<Prism> _candidates = new();
        readonly List<ShipActionSO> _bindScratch = new();
        readonly Dictionary<ShrikeControl, InputEvents> _boundInputs = new();
        static Mesh _budMesh;

        // Autopilot.
        float _aiNextScan, _aiNextNest;
        int _aiTargetsAhead;
        bool _aiEnemyAhead, _aiHoldingLance, _aiHoldingPaint;

        // ------------------------------------------------------------------ read by the HUD and the animation

        public ShrikeConfigSO Config => config;
        public bool Docked => _dock != DockKind.None;
        public bool DockedInNest => _dock == DockKind.Nest;
        public bool Slinging => _slinging;
        public bool Tethering => _tethering;
        public bool Painting => _painting;
        public bool Locked => _lock != LockKind.None;
        public int NestCount { get { int n = 0; foreach (var nest in _nests) if (!nest.Dead) n++; return n; } }
        /// <summary>0 just fired … 1 ready.</summary>
        public float Lance01 => config ? 1f - Mathf.Clamp01(_lanceCd / config.LanceCooldown) : 0f;
        public float Nest01 => config && config.NestCooldown > 0f ? 1f - Mathf.Clamp01(_nestCd / config.NestCooldown) : 1f;
        public float Sling01 => config ? 1f - Mathf.Clamp01(_slingCd / Mathf.Max(0.01f, config.SlingCooldown.EvaluateReplicated(_status))) : 0f;
        /// <summary>The sum of every live nest's turret health — the missiles the array can still call.</summary>
        public int ArrayHealth { get { int h = 0; foreach (var nest in _nests) if (!nest.Dead && nest.TurretAlive) h += Mathf.Max(0, nest.Health - 1); return h; } }

        // ------------------------------------------------------------------ lifecycle

        public override void Initialize(IVesselStatus shipStatus)
        {
            ClearAll();
            _status = shipStatus;
            if (!hull && _status != null && _status.Transform)
                hull = _status.Transform.GetComponentInChildren<ShrikeHullBuilder>(true);
            _lanceCd = _slingCd = _nestCd = 0f;
            _lockId = 0;
            _boundInputs.Clear();
            _aiNextScan = _aiNextNest = 0f;

            if (!config)
                CSDebug.LogError($"[Shrike] {name}: ShrikeExecutor has no ShrikeConfig — the vessel has no lance, sling, " +
                                 "array or nest. Wire Assets/_SO_Assets/VesselActions/Shrike/ShrikeConfig.asset.");
            if (!hull)
                CSDebug.LogError($"[Shrike] {name}: no ShrikeHullBuilder under the vessel — turrets and missiles have no material.");
        }

        void OnEnable()
        {
            if (OnMiniGameTurnEnd != null) OnMiniGameTurnEnd.OnRaised += OnTurnEnd;
        }

        // Unconditional and idempotent: a despawned or pooled vessel must not keep a pin, a held pen, a tether or a
        // missile in flight. Nests are MASS in the world and stay, like any trail.
        void OnDisable()
        {
            if (OnMiniGameTurnEnd != null) OnMiniGameTurnEnd.OnRaised -= OnTurnEnd;
            ClearAll();
        }

        void OnDestroy()
        {
            for (int i = 0; i < _idleMissiles.Count; i++)
                if (_idleMissiles[i]) Destroy(_idleMissiles[i]);
            _idleMissiles.Clear();
            for (int i = 0; i < _nests.Count; i++)
                if (_nests[i].Turret) Destroy(_nests[i].Turret);
            _nests.Clear();
        }

        void OnTurnEnd() => ClearAll();

        void ClearAll()
        {
            _lanceHeld = false;
            _painting = false;
            _lock = LockKind.None;
            _lockVessel = null;
            _tethering = false;
            _slinging = false;
            _slingAnchor = _slingFrom = null;
            _slingTrail.Clear();
            _dock = DockKind.None;
            _dockPrism = null;
            _dockNest = null;
            _push = Vector3.zero;
            for (int i = 0; i < _missiles.Count; i++) ReturnMissile(_missiles[i].Body);
            _missiles.Clear();
            Pin(false);
            ReleasePen();
            if (_status?.VesselPrismController is ShrikePrismController prisms) prisms.SpacingMultiplier = 1f;
        }

        // ------------------------------------------------------------------ controls (every peer: presses replicate)

        public void BeginLance() => _lanceHeld = true;
        public void EndLance() => _lanceHeld = false;

        /// <summary>Paint on: a fresh paint is a fresh salvo, even on the same mass (page <c>stepPilot</c>).</summary>
        public void BeginPaint()
        {
            if (!_painting) _lockId++;
            _painting = true;
        }

        public void EndPaint()
        {
            _painting = false;
            _lock = LockKind.None;
            _lockVessel = null;
        }

        /// <summary>The Void Sling: shoot the tether, if its cooldown is up and nothing is already flying.</summary>
        public void PressSling()
        {
            if (!config || _status == null || !_status.Transform) return;
            if (_slingCd > 0f || _tethering || _slinging) return;
            Vector3 fwd = _status.Transform.forward;
            _tethering = true;
            _tetherPos = HullPosition + fwd * 4f;
            _tetherDir = fwd;
            _tetherWent = 0f;
            _slingCd = Mathf.Max(0.05f, config.SlingCooldown.EvaluateReplicated(_status));
            PlayAt(config.TetherEvent, HullPosition);
        }

        /// <summary>A: docked, undock with a push off the surface; flying, build a nest round the hull and dock in it.</summary>
        public void PressDock()
        {
            if (!config || _status == null || !_status.Transform) return;
            if (_slinging || _tethering) return;
            if (Docked)
            {
                Undock(push: true);
                PlayAt(config.UndockEvent, HullPosition);
                return;
            }
            if (_nestCd > 0f) return;
            var nest = BuildNest();
            if (nest == null) return;
            _nestCd = config.NestCooldown;
            DockInNest(nest);
            PlayAt(config.NestEvent, HullPosition);
        }

        /// <summary>A crystal (any kind): the sling's cooldown resets and every nest gains health
        /// (page <c>pickUp</c>: "resetting your void sling"; "each nest 3 health blocks").</summary>
        public void CollectCrystal()
        {
            if (!config) return;
            _slingCd = 0f;
            for (int i = 0; i < _nests.Count; i++)
                if (!_nests[i].Dead) _nests[i].Health = Mathf.Min(config.NestHealthMax, _nests[i].Health + config.CrystalHealth);
            PlayAt(config.CrystalEvent, HullPosition);
        }

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
            if (_lanceHeld && _lanceCd <= 0f && !_slinging) FireLance();
            StepTether(dt);
            UpdateLock();
            LaunchMissiles(dt);

            if (_slinging) StepSling(dt);
            else if (Docked) HoldDock();
            else Fly(dt);

            StepMissiles(dt);
            StepNests(dt);
            UpdatePen();

            if (_status.AutoPilotEnabled && IsAuthority()) Autopilot();
        }

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
            return true;
        }

        bool IsOwnFreshTrail(Prism prism)
            => prism.ownerID == _status.PlayerName && !IsNestBlock(prism, out _)
               && Time.time - prism.prismProperties.TimeCreated < config.OwnTrailGraceSeconds;

        void DockOn(Prism prism)
        {
            if (IsNestBlock(prism, out var nest) && _dockNest == null)
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

        // ------------------------------------------------------------------ nests

        Nest BuildNest()
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

            var nest = new Nest
            {
                Centre = HullPosition,
                Rotation = _status.Transform.rotation,
                Health = config.NestHealth,
            };
            int count = ShrikeRules.NestBlockCount(config.NestBlocks.EvaluateReplicated(_status));
            Vector3 size = Vector3.one * ShrikeRules.NestBlockSize(false);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = nest.Centre + nest.Rotation * ShrikeRules.NestLocal(i, count, false);
                var prism = BoostRingBuilder.LayOne(channel, pos, nest.Rotation, size, PrismKind.SuperShielded,
                    _status.Domain, _status.PlayerName, _status.PlayerName);
                if (!prism) continue;
                nest.Blocks.Add(prism);
                nest.Born.Add(prism.prismProperties.TimeCreated);
            }
            nest.Up = nest.Rotation * Vector3.up;
            nest.TurretPosition = nest.Centre + nest.Rotation * ShrikeRules.TurretLocal(false);
            nest.Turret = MakeBud("ShrikeTurret", nest.TurretPosition, nest.Rotation, 1.8f);
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
            KillNest(nest);
        }

        void KillNest(Nest nest)
        {
            nest.Dead = true;
            nest.TurretAlive = false;
            if (nest.Turret) Destroy(nest.Turret);
            nest.Turret = null;
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

        /// <summary>The turret flora regrows health blocks, faster while docked in it; a nest whose every block is gone
        /// is gone (page <c>stepNests</c>).</summary>
        void StepNests(float dt)
        {
            for (int n = 0; n < _nests.Count; n++)
            {
                var nest = _nests[n];
                if (nest.Dead) continue;
                if (nest.TurretAlive)
                    ShrikeRules.RegenTick(ref nest.Health, ref nest.Regen, dt, _dockNest == nest, config.TurretRegen, config.NestHealthMax);
                bool any = false;
                for (int i = 0; i < nest.Blocks.Count && !any; i++) any = PrismAlive(nest.Blocks[i], nest.Born[i]);
                if (!any) KillNest(nest);
            }
        }

        // ------------------------------------------------------------------ the Void Lance

        /// <summary>Page <c>fireLance</c>: every block the beam passes breaks — super-shielded included, "even if super
        /// shielded" — except the nest you are docked in and the block you sit on; a rival hull on the line is a petal hit.</summary>
        void FireLance()
        {
            _lanceCd = config.LanceCooldown;
            Vector3 f = _status.Transform.forward;
            Vector3 o = HullPosition + f * 4f;
            float r = Mathf.Max(0.1f, config.LanceRadius.EvaluateReplicated(_status));
            float len = config.LanceRange;
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

            SniperBeam.Fire(o, o + f * len, r * 2f, r * 2f, DomainColour(), config.LanceBeamSeconds, r * 3f, 0.2f, hit);
            PlayAt(config.LanceEvent, o);
        }

        /// <summary>The sanctioned teardown (SniperShotActionExecutor.DestroyPrism): a super-shield comes off first,
        /// then <c>devastate</c> stops the prism restoring its armour instead of dying.</summary>
        void BreakPrism(Prism prism, Vector3 impact, bool devastate)
        {
            if (prism.prismProperties is { IsSuperShielded: true })
            {
                prism.DeactivateShields(impact);
                if (!devastate) return;   // a missile blast strips the super-shield and stops (one tier per hit)
            }
            prism.Damage(impact, _status.Domain, _status.PlayerName, devastate: devastate);
        }

        // ------------------------------------------------------------------ the Void Sling

        /// <summary>Page <c>stepTether</c>: the tether flies until it meets a block (never a crystal or a vessel), then
        /// the sling starts toward that point — speed = distance / slingSeconds.</summary>
        void StepTether(float dt)
        {
            if (!_tethering) return;
            float step = config.TetherSpeed * dt;
            var index = PrismSpatialIndex.Instance;
            Prism best = null;
            float bestT = float.MaxValue;
            if (index && index.IsAvailable)
            {
                index.QuerySegment(_tetherPos, _tetherPos + _tetherDir * step, config.DockQueryReach, _candidates);
                for (int i = 0; i < _candidates.Count; i++)
                {
                    var prism = _candidates[i];
                    if (!prism || prism.destroyed || prism.prismProperties == null) continue;
                    if (IsOwnFreshTrail(prism)) continue;
                    if (_dock == DockKind.Block && prism == _dockPrism) continue;
                    if (_dock == DockKind.Nest && IsNestBlock(prism, out var nest) && nest == _dockNest) continue;
                    if (RayHits(prism, _tetherPos, _tetherDir, 0f, step, out float t) && t < bestT)
                    {
                        bestT = t;
                        best = prism;
                    }
                }
                _candidates.Clear();
            }

            if (best)
            {
                _tethering = false;
                Vector3 x = _tetherPos + _tetherDir * bestT;
                float dist = ShrikeRules.SlingDistance(HullPosition, x, config.HullRadius);
                if (dist < 1f) return;
                Prism from = _dock == DockKind.Block ? _dockPrism : null;
                if (Docked) Undock(push: false);
                _slinging = true;
                _slingAnchor = best;
                _slingAnchorBorn = best.prismProperties.TimeCreated;
                _slingFrom = from;
                _slingDir = (x - HullPosition).normalized;
                _slingDist = dist;
                _slingWent = 0f;
                _slingSpeed = ShrikeRules.SlingSpeed(dist, config.SlingSeconds);
                _slingTrailOwed = 0f;
                _slingTrail.Clear();
                Pin(true);
                PlayAt(config.SlingEvent, HullPosition);
                return;
            }
            _tetherPos += _tetherDir * step;
            _tetherWent += step;
            if (_tetherWent > config.SlingRange) _tethering = false;
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

            // Swept contact: the earliest block along this frame's path within the hull's radius.
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

        // ------------------------------------------------------------------ the Targeting Array

        /// <summary>Page <c>updateLock</c>: a rival hull inside the cone first (kept to 1.6× the cone); otherwise the
        /// centre of the mass round the block nearest the aim, re-scanned every 0.25 s.</summary>
        void UpdateLock()
        {
            if (!_painting) { _lock = LockKind.None; _lockVessel = null; return; }
            Vector3 pos = HullPosition, f = _status.Transform.forward;
            float cone = Mathf.Max(0.5f, config.LockCone.EvaluateReplicated(_status)) * Mathf.Deg2Rad;

            if (_lock == LockKind.Vessel && _lockVessel != null && _lockVessel.Transform
                && Angle(pos, f, _lockVessel.Transform.position) < cone * config.LockKeepScale)
                return;   // keep an enemy lock through clutter

            IVesselStatus best = null;
            float bestAngle = cone;
            var players = _gameData != null ? _gameData.Players : null;
            if (players != null)
                for (int i = 0; i < players.Count; i++)
                {
                    var victim = EnemyStatus(players[i]);
                    if (victim == null) continue;
                    float a = Angle(pos, f, victim.Transform.position);
                    if (a < bestAngle) { bestAngle = a; best = victim; }
                }
            if (best != null)
            {
                if (_lock != LockKind.Vessel || !ReferenceEquals(_lockVessel, best))
                {
                    _lockId++;
                    PlayAt(config.LockEvent, pos);
                }
                _lock = LockKind.Vessel;
                _lockVessel = best;
                return;
            }
            if (_lock == LockKind.Mass && Time.time < _lockScanAt) return;
            _lockScanAt = Time.time + 0.25f;

            // "Query the map for volume density and path find into the centre of mass": the block nearest the paint
            // ray, then the centre of the mass round it.
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) { _lock = LockKind.None; return; }
            index.QuerySegment(pos, pos + f * config.LockRange, Mathf.Tan(cone) * 300f, _candidates);
            Prism seed = null;
            float seedAngle = cone;
            for (int i = 0; i < _candidates.Count; i++)
            {
                var prism = _candidates[i];
                if (!prism || prism.destroyed || IsNestBlock(prism, out _) || prism.ownerID == _status.PlayerName) continue;
                float a = Angle(pos, f, prism.transform.position);
                if (a < seedAngle) { seedAngle = a; seed = prism; }
            }
            _candidates.Clear();
            if (!seed) { _lock = LockKind.None; return; }

            Vector3 seedPos = seed.transform.position, sum = Vector3.zero;
            int count = 0;
            index.QuerySphere(seedPos, config.MassLockRadius, _candidates);
            for (int i = 0; i < _candidates.Count; i++)
            {
                var prism = _candidates[i];
                if (!prism || prism.destroyed || IsNestBlock(prism, out _) || prism.ownerID == _status.PlayerName) continue;
                sum += prism.transform.position;
                count++;
            }
            _candidates.Clear();
            Vector3 point = count > 0 ? sum / count : seedPos;
            if (_lock != LockKind.Mass || Vector3.Distance(_lockPoint, point) > 15f)
            {
                _lockId++;   // the same mass keeps its lock as it is eaten away
                PlayAt(config.LockEvent, pos);
            }
            _lock = LockKind.Mass;
            _lockVessel = null;
            _lockPoint = point;
        }

        float Angle(Vector3 pos, Vector3 fwd, Vector3 at)
        {
            Vector3 d = at - pos;
            float l = d.magnitude;
            if (l > config.LockRange || l < 1f) return 9f;
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(d, fwd) / l, -1f, 1f));
        }

        bool LockAlive => _lock == LockKind.Mass || _lock == LockKind.Vessel && _lockVessel != null && _lockVessel.Transform;
        Vector3 LockPosition => _lock == LockKind.Vessel ? _lockVessel.Transform.position : _lockPoint;

        /// <summary>Page <c>launchMissiles</c>: each nest fires ONE missile per painted target, never its last health
        /// block.</summary>
        void LaunchMissiles(float dt)
        {
            for (int n = 0; n < _nests.Count; n++)
            {
                var nest = _nests[n];
                if (nest.Dead) continue;
                nest.FireCooldown -= dt;
                if (!_painting || !LockAlive) continue;
                if (!ShrikeRules.NestMayFire(nest.Health, nest.TurretAlive, nest.FireCooldown, nest.FiredForLock, _lockId)) continue;
                nest.FireCooldown = config.MissileEvery;
                nest.FiredForLock = _lockId;
                nest.Health--;
                Vector3 dir = ShrikeRules.LaunchDirection(nest.Up, LockPosition - nest.TurretPosition);
                var missile = new Missile
                {
                    Position = nest.TurretPosition + nest.Up * 3f,
                    Direction = dir,
                    From = nest,
                    Body = TakeMissile(),
                };
                _missiles.Add(missile);
                PlayAt(config.MissileLaunchEvent, missile.Position);
            }
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
                if (_painting && LockAlive)
                {
                    Vector3 target = LockPosition;
                    Vector3 want = (target - missile.Position).normalized;
                    if (indexed)
                    {
                        // A look-ahead swerve round the first block 45 u ahead that is not part of the target's mass.
                        index.QuerySegment(missile.Position, missile.Position + dir * 45f, 2.5f + config.DockQueryReach, _candidates);
                        Prism obstacle = null;
                        float obstacleT = float.MaxValue;
                        for (int i = 0; i < _candidates.Count; i++)
                        {
                            var prism = _candidates[i];
                            if (!prism || prism.destroyed) continue;
                            if (IsNestBlock(prism, out var own) && own == missile.From) continue;
                            if (_lock == LockKind.Mass && Vector3.Distance(prism.transform.position, target) < 40f) continue;
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
                {
                    missile.Body.transform.SetPositionAndRotation(missile.Position, Quaternion.LookRotation(dir, UpOf(dir)));
                }
                if (!boom) continue;
                Detonate(missile);
                ReturnMissile(missile.Body);
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

        GameObject TakeMissile()
        {
            for (int i = _idleMissiles.Count - 1; i >= 0; i--)
            {
                var body = _idleMissiles[i];
                _idleMissiles.RemoveAt(i);
                if (!body) continue;
                body.SetActive(true);
                return body;
            }
            var made = MakeBud("ShrikeMissile", HullPosition, Quaternion.identity, 0.7f);
            if (made) made.transform.localScale = new Vector3(0.5f, 0.5f, 1.4f);
            return made;
        }

        void ReturnMissile(GameObject body)
        {
            if (!body) return;
            body.SetActive(false);
            _idleMissiles.Add(body);
        }

        // ------------------------------------------------------------------ scoring

        /// <summary>A hit on a rival hull under the LOCKED PvP rule: admitted through <see cref="CombatHitDrain.TryAdmit"/>
        /// (a warded victim is neither scored on nor robbed), its petals ejected, the score raised. It never moves the
        /// victim. Callers have already asked <see cref="ElementalTransfer.IsDecidedHere"/>.</summary>
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
        /// The AI flies the Shrike with the SAME replicated controls a human presses (vessel-ai's input-only rule): it
        /// holds the lance while rival mass or a rival hull lies on the line, paints a rival hull its nests can reach,
        /// undocks after a few seconds, builds a nest when it has none, and slings toward a far target ahead.
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
                _aiEnemyAhead = EnemyInCone(pos, fwd, config.LanceRange, 4f);
            }

            bool wantLance = _aiTargetsAhead >= config.AiLanceMinTargets || _aiEnemyAhead;
            if (wantLance != _aiHoldingLance && TryResolve(handler, ShrikeControl.Lance, out var lanceInput))
            {
                _aiHoldingLance = wantLance;
                if (wantLance) handler.PerformShipControllerActionsReplicated(lanceInput);
                else handler.StopShipControllerActionsReplicated(lanceInput);
            }

            bool wantPaint = ArrayHealth > 0 && EnemyInCone(pos, fwd, config.LockRange,
                config.LockCone.EvaluateReplicated(_status) * config.LockKeepScale);
            if (wantPaint != _aiHoldingPaint && TryResolve(handler, ShrikeControl.Paint, out var paintInput))
            {
                _aiHoldingPaint = wantPaint;
                if (wantPaint) handler.PerformShipControllerActionsReplicated(paintInput);
                else handler.StopShipControllerActionsReplicated(paintInput);
            }

            bool pressDock = Docked ? _dockedFor >= config.AiDockSeconds
                                    : !_slinging && _nestCd <= 0f && NestCount == 0 && now >= _aiNextNest;
            if (pressDock && TryResolve(handler, ShrikeControl.Dock, out var dockInput))
            {
                if (!Docked) _aiNextNest = now + config.AiNestEvery;
                handler.PerformShipControllerActionsReplicated(dockInput);
                handler.StopShipControllerActionsReplicated(dockInput);
            }

            Vector3 toTarget = ai.TargetPosition - pos;
            if (!_slinging && !_tethering && _slingCd <= 0f && toTarget.magnitude >= config.AiSlingMinDistance
                && Vector3.Angle(fwd, toTarget) <= config.AiSlingDegrees
                && TryResolve(handler, ShrikeControl.Sling, out var slingInput))
            {
                handler.PerformShipControllerActionsReplicated(slingInput);
                handler.StopShipControllerActionsReplicated(slingInput);
            }
        }

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
            return colorSet != null ? colorSet.GetDomainSignalColor(_status.Domain) : Color.white;
        }

        /// <summary>A turret bud or a missile: an octahedron dressed in the hull's own materials (the domain colour is
        /// its slot 1), no collider — nothing touches it but this executor's own queries.</summary>
        GameObject MakeBud(string label, Vector3 at, Quaternion rotation, float radius)
        {
            var go = new GameObject(label);
            go.transform.SetPositionAndRotation(at, rotation);
            go.transform.localScale = Vector3.one * radius;
            go.AddComponent<MeshFilter>().sharedMesh = BudMesh();
            var renderer = go.AddComponent<MeshRenderer>();
            if (hull && hull.TryGetComponent(out MeshRenderer source))
            {
                var mats = source.sharedMaterials;
                if (mats.Length > 0) renderer.sharedMaterial = mats[mats.Length > 1 ? 1 : 0];
            }
            return go;
        }

        static Mesh BudMesh()
        {
            if (_budMesh) return _budMesh;
            Vector3[] v = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
            int[] faces = { 0, 4, 3, 0, 3, 5, 0, 5, 2, 0, 2, 4, 1, 3, 4, 1, 5, 3, 1, 2, 5, 1, 4, 2 };
            var verts = new Vector3[faces.Length];
            var tris = new int[faces.Length];
            for (int i = 0; i < faces.Length; i++) { verts[i] = v[faces[i]]; tris[i] = i; }
            _budMesh = new Mesh { name = "ShrikeBud", vertices = verts, triangles = tris };
            _budMesh.RecalculateNormals();
            _budMesh.RecalculateBounds();
            return _budMesh;
        }

        static void PlayAt(EventReference reference, Vector3 at)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, at);
        }
    }
}
