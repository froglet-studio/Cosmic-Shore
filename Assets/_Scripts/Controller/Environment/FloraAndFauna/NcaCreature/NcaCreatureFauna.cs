using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ECS;
using CosmicShore.Utility;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// An NCA CREATURE (Docs/NCA_CREATURES.md): a fauna whose body IS a trained 3D neural cellular automaton - the
    /// lab's lizard today, the whale and the jellyfish when their trainings finish. The network (NcaVoxelCore, bit-exact
    /// with the lab runtime) grows the animal from one seed cell, swims it (the stroke lives in the network, there is no
    /// animation), and regrows any hole cut in it. This class is the glue that makes that a game creature:
    ///
    ///   BODY    - every visible surface voxel is drawn as a prism entity (PrismRenderService) in the creature's domain
    ///             BLOCK material, oriented to the skin's normal like the lab's scales. No mesh, no rig, no colliders.
    ///   HITS    - the body is split into <see cref="NcaCreatureConfigSO.Segments"/> slabs, each one VIRTUAL spatial-
    ///             index entry, so every AOE, hitscan round and projectile finds it. A weapon that must apply gameplay
    ///             materialises ONE transient, owner-hidden HealthPrism at that segment (the collider budget: one per
    ///             creature at a time); when it explodes the creature takes a bite there. A vessel of another domain
    ///             flying through the body bites it directly.
    ///   WOUNDS  - a bite cuts a sphere out of the network's state and the creature loses that much body volume. The
    ///             wound is held open (re-cut after every step) until the creature has EATEN its volume: regrowth is
    ///             paid for, never free.
    ///   FOOD    - a herbivore: it steers to the nearest edible flora heart and grazes prisms at its mouth. Each meal
    ///             pays toward open wounds first (<see cref="NcaCreatureConfigSO.HealShareOfMeal"/>), the rest feeds the
    ///             stomach, and every feed drives the platform's reproduction (Fauna.NotifyFed) - so it can complete its
    ///             lifecycle (Garrett's rule: feed and reproduce before dying).
    ///   DEATH   - the sealed Fauna.Die: starvation, its heart jousted, devoured, the network going extinct, or its body
    ///             shot below <see cref="NcaCreatureConfigSO.DeathFraction"/> of grown. A withering body comes apart
    ///             outside-in around its heart and the heart is released last (Docs/ECOSYSTEM.md §26).
    ///   HEART   - one elemental crystal, seated behind the head's front, sized by the config (§40).
    ///
    /// Not networked (no FaunaNetworkSync) and not under ecology LOD - see the doc's limitations.
    /// </summary>
    public class NcaCreatureFauna : Fauna, IVirtualPrismOwner, IVirtualPrismBudget
    {
        const string AttributionName = "nca creature";

        [Header("NCA creature")]
        [Tooltip("Every number this creature runs on, including the trained network (Docs/NCA_CREATURES.md).")]
        [SerializeField] NcaCreatureConfigSO config;

        public NcaCreatureConfigSO Config => config;

        static readonly ProfilerMarker s_mUpdate = new("NcaCreatureFauna.Update");
        static readonly ProfilerMarker s_mRender = new("NcaCreatureFauna.Render");
        static readonly Dictionary<NcaVoxelWeights, float[]> s_bodyPositions = new();

        NcaVoxelWeights _weights;
        NcaVoxelTicker _ticker;
        float[] _bodyPos;           // per grid cell: its body-space position in voxels (head at +z)
        bool _booted;
        bool _threaded;
        float _nextStepAt;
        float _collectedAt;
        float _stepInterval = 0.05f;
        float _stroke;
        bool _mature;
        bool _reportedError;
        string _lastKiller = string.Empty;

        // drawing
        Mesh _mesh;
        Material _material;
        Material _fallbackMaterial;
        bool _entities;
        NativeArray<PrismRenderHandle> _handles;
        NativeArray<float4x4> _matrices;
        Matrix4x4[] _fallbackMatrices;
        int _shown;
        int _poolSize;

        // the spatial index: one virtual entry per body segment
        PrismSpatialIndex _index;
        int[] _entryIds;
        bool[] _entryEmpty;         // suspended by this creature because the segment holds no body
        HealthPrism _hitPrism;
        int _hitSlot = -1;
        float _hitAt;

        // wounds and meals are applied to the network only between steps
        struct PendingBite { public float Z, Y, X, R; public string Killer; }
        readonly List<PendingBite> _bites = new();
        float _pendingMeal;

        // behaviour
        Transform _mouth;
        float _speed;
        float _boltUntil = -1f;
        Vector3 _boltAway;
        float _nextSenseAt;
        float _nextFeedAt;
        readonly Dictionary<int, float> _vesselBiteAt = new();
        Predicate<Flora> _rejectFlora;
        Vector3 _heartSeat;         // body space, voxels
        bool _heartSeated;

        // death
        bool _dying;
        float _deathAt;
        float _deathReach;
        LifeformDeathStyle _deathStyleAtDeath;
        Vector3 _devourFrom;

        float VoxelVolume => _weights != null ? config.BodyVolume / Mathf.Max(1, _weights.GrownVoxels) : 0f;

        /// <summary>The body volume the creature still has - its whole body less every wound it has not paid for.</summary>
        public float LiveBodyVolume => _ticker != null ? Mathf.Max(0f, config.BodyVolume - _ticker.Debt) : 0f;

        public override float CurrentSpeed => _speed;

        protected override bool DefersHeartRelease => true;

        protected override float BodyMealVolume => LiveBodyVolume;

        // ───────────────────────────────────────────────────────────────
        //  Lifecycle
        // ───────────────────────────────────────────────────────────────

        public override void Initialize(Cell cell)
        {
            base.Initialize(cell);
            Boot();
        }

        protected override void Start()
        {
            base.Start();   // the stomach and the goal coroutine
            Boot();         // a scene-placed creature is never Initialize()d
        }

        void Boot()
        {
            if (_booted) return;
            _booted = true;
            if (!config || !config.Weights)
            {
                CSDebug.LogError($"[NcaCreature] {name}: no NcaCreatureConfigSO / weights - the creature cannot grow " +
                                 "(run Tools/Build/author_nca_creatures.py).");
                enabled = false;
                return;
            }

            _weights = NcaVoxelWeights.Parse(config.Weights.text);
            var core = new NcaVoxelCore(_weights, (uint)Random.Range(1, int.MaxValue))
            {
                Respawn = false,   // a creature whose body dies is dead (the lab respawns its showcase animal)
                AlphaThreshold = config.AlphaThreshold,
            };
            _ticker = new NcaVoxelTicker(core, config.Segments);
            _bodyPos = BodyPositions(_weights);
            _threaded = Application.platform != RuntimePlatform.WebGLPlayer;
            _collectedAt = Time.time;
            _nextStepAt = Time.time;
            _rejectFlora = f => !f || f.IsDying || !IsPreyForMe(f.HeartTransform.position, f.Domain);

            var mouth = new GameObject("Mouth");
            _mouth = mouth.transform;
            _mouth.SetParent(transform, false);

            if (!crystal) ProvisionHeart(Element.None);
            ApplyHeartSize(config.HeartWorldScale);
            BindBody();
            BindEntries();
        }

        protected override void ProvisionHeart(Element element)
        {
            base.ProvisionHeart(element);
            if (crystal) crystal.transform.localPosition = HeartSeatLocal();
        }

        protected override void OnDestroy()
        {
            RetireHitPrism();
            ReleaseEntries();
            ReleaseBody();
            if (IsDying) ReleaseHeart();   // an interrupted wither still drops its crystal (the base backstop agrees)
            base.OnDestroy();
        }

        // ───────────────────────────────────────────────────────────────
        //  The frame
        // ───────────────────────────────────────────────────────────────

        void Update()
        {
            if (!_booted || _ticker == null) return;
            using (s_mUpdate.Auto())
            {
                if (_dying)
                {
                    UpdateDeath();
                    return;
                }

                PumpNetwork();
                if (IsSimAuthority)
                {
                    Steer(Time.deltaTime);
                    if (Time.time >= _nextSenseAt) SenseVessels();
                    if (Time.time >= _nextFeedAt) Feed();
                }
                UpkeepHitPrism();
                SeatHeart();
                PushEntries();
                using (s_mRender.Auto()) Draw(Mathf.Clamp01((Time.time - _collectedAt) / _stepInterval), null);
                if (IsSimAuthority) CheckDeath();
            }
        }

        /// <summary>Collect a finished step, apply what happened since (wounds, meals) and start the next one.</summary>
        void PumpNetwork()
        {
            if (_ticker.Error != null)
            {
                if (!_reportedError)
                {
                    _reportedError = true;
                    CSDebug.LogError($"[NcaCreature] {name}: the network step threw and the body has stopped: {_ticker.Error}");
                }
                return;
            }
            if (_ticker.Busy) return;
            if (_ticker.Collect()) OnStepPublished();
            ApplyPending();

            if (Time.time < _nextStepAt) return;
            bool bolting = Time.time < _boltUntil;
            float rate = !_threaded ? config.InlineStepsPerSecond : bolting ? config.BoltStepsPerSecond : config.StepsPerSecond;
            _stepInterval = 1f / Mathf.Max(0.5f, rate);
            _nextStepAt = Mathf.Max(_nextStepAt + _stepInterval, Time.time);
            _ticker.Kick(_threaded);
            if (!_threaded && _ticker.Collect()) OnStepPublished();
        }

        void OnStepPublished()
        {
            _collectedAt = Time.time;
            var cur = _ticker.Cur;
            float bendRate = Mathf.Abs(cur.Bend - _ticker.Prev.Bend);
            _stroke = Mathf.Lerp(_stroke, Mathf.Clamp01(bendRate / Mathf.Max(1e-3f, config.FullStrokeBendRate)), 0.5f);
            if (!_mature && cur.Alive >= config.MatureFraction * _weights.GrownVoxels) _mature = true;
            UpdateEntryShapes();
        }

        void ApplyPending()
        {
            if (_bites.Count > 0)
            {
                float vv = VoxelVolume;
                for (int k = 0; k < _bites.Count; k++)
                {
                    var b = _bites[k];
                    int removed = _ticker.Hit(b.Z, b.Y, b.X, b.R);
                    if (removed <= 0) continue;
                    _ticker.AddScar(new NcaScar { Z = b.Z, Y = b.Y, X = b.X, R = b.R, Debt = removed * vv });
                    if (!string.IsNullOrEmpty(b.Killer)) _lastKiller = b.Killer;
                }
                _bites.Clear();
                UpdateEntryShapes();
            }

            if (_pendingMeal > 0f)
            {
                float meal = _pendingMeal;
                _pendingMeal = 0f;
                float toWounds = meal * config.HealShareOfMeal;
                float unspent = _ticker.PayScars(toWounds);
                NotifyFed(meal - toWounds + unspent);
            }
        }

        void Bite(Vector3 bodyVoxels, float radius, string killer)
        {
            _weights.BodyToGrid(bodyVoxels.x, bodyVoxels.y, bodyVoxels.z, out float gz, out float gy, out float gx);
            _bites.Add(new PendingBite { Z = gz, Y = gy, X = gx, R = radius, Killer = killer ?? string.Empty });
        }

        void CheckDeath()
        {
            var cur = _ticker.Cur;
            if (cur.Extinct || (_mature && cur.Alive < config.DeathFraction * _weights.GrownVoxels))
            {
                Die(_lastKiller);
                return;
            }
            if (IsStarving) Die(StarvationKiller);
        }

        // ───────────────────────────────────────────────────────────────
        //  Swimming, sensing, feeding
        // ───────────────────────────────────────────────────────────────

        /// <summary>The nearest edible flora heart (the base goal - the cell's aggression targets - when none).</summary>
        protected override Vector3 ResolveGoal()
        {
            if (_rejectFlora == null) return base.ResolveGoal();
            var flora = FloraHeartRegistry.NearestToPoint(MouthWorld(), _rejectFlora);
            return flora ? flora.HeartTransform.position : base.ResolveGoal();
        }

        void Steer(float dt)
        {
            Vector3 pos = transform.position;
            bool bolting = Time.time < _boltUntil;
            Vector3 target = bolting ? ClampToBand(pos + _boltAway * 200f) : Goal;
            Vector3 to = target - pos;
            float dist = to.magnitude;

            if (to.sqrMagnitude > DegenerateSteeringSqr)
            {
                var want = Quaternion.LookRotation(to / dist, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, config.TurnDegreesPerSecond * dt);
            }

            float speed = bolting ? config.BoltSpeed : config.SwimSpeed;
            speed *= 1f - config.StrokeThrust + config.StrokeThrust * _stroke;
            if (!bolting && config.SlowRadius > 0f) speed *= Mathf.Lerp(0.25f, 1f, Mathf.Clamp01(dist / config.SlowRadius));
            float grown = _weights.GrownVoxels > 0 ? Mathf.Clamp01(_ticker.Cur.Alive / (float)_weights.GrownVoxels) : 1f;
            _speed = Mathf.MoveTowards(_speed, speed * grown, Mathf.Max(config.SwimSpeed, config.BoltSpeed) * dt);
            transform.position = pos + transform.forward * (_speed * dt);
        }

        /// <summary>Vessels in reach: one closing fast makes the creature bolt; one of another domain passing
        /// through the body takes a bite where it passed (the vessel is the killer).</summary>
        void SenseVessels()
        {
            _nextSenseAt = Time.time + 0.1f;
            Vector3 pos = transform.position;
            int n = Physics.OverlapSphereNonAlloc(pos, config.VesselSenseRadius, OverlapScratch, VesselSenseMask);
            var cur = _ticker.Cur;
            float vs = config.VoxelSize;
            for (int c = 0; c < n; c++)
            {
                var col = OverlapScratch[c];
                if (!col || !col.TryGetComponent(out IVesselStatus vessel)) continue;
                Vector3 vp = col.transform.position;
                Vector3 away = pos - vp;
                float d = away.magnitude;
                if (d > 1e-3f)
                {
                    float closing = Vector3.Dot(vessel.Course * vessel.Speed, away / d);
                    if (closing > config.BoltClosingSpeed)
                    {
                        _boltUntil = Time.time + config.BoltSeconds;
                        _boltAway = away / d;
                    }
                }

                if (vessel.Domain == domain || cur.Alive == 0) continue;
                int key = col.attachedRigidbody ? col.attachedRigidbody.GetInstanceID() : col.GetInstanceID();
                if (_vesselBiteAt.TryGetValue(key, out float last) && Time.time - last < config.VesselBiteCooldown) continue;

                Vector3 p = transform.InverseTransformPoint(vp) / vs;
                for (int g = 0; g < cur.Segments; g++)
                {
                    if (cur.SegN[g] == 0) continue;
                    var centre = new Vector3(cur.SegCentre[3 * g], cur.SegCentre[3 * g + 1], cur.SegCentre[3 * g + 2]);
                    Vector3 off = p - centre;
                    float r = cur.SegRadius[g];
                    if (off.magnitude > r + config.VesselBiteReachVoxels) continue;
                    _vesselBiteAt[key] = Time.time;
                    Bite(centre + Vector3.ClampMagnitude(off, r), config.BiteRadiusVoxels, vessel.PlayerName);
                    break;
                }
            }
        }

        /// <summary>One mouthful: the edible flora prisms at the mouth, suctioned in. Only while hungry or wounded.</summary>
        void Feed()
        {
            _nextFeedAt = Time.time + config.FeedInterval;
            if (_ticker.Cur.Alive == 0) return;
            bool hungry = stomachCapacity <= 0f || StomachVolume < 0.9f * stomachCapacity;
            if (!hungry && _ticker.Debt <= 0f) return;

            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            int found = index.QuerySphere(MouthWorld(), config.MouthRadius, FeedScratch);
            int taken = 0;
            float meal = 0f;
            for (int k = 0; k < found && taken < config.PrismsPerMouthful; k++)
            {
                var prism = FeedScratch[k];
                if (!IsEdible(prism)) continue;
                meal += Mathf.Max(0f, prism.Volume);   // read before the consume (the stomach is conserved)
                prism.Consume(_mouth, domain, AttributionName, true, true);
                taken++;
            }
            if (taken > 0) _pendingMeal += meal;
        }

        /// <summary>The herbivore rule, as every grazer applies it: flora (a health prism of a lifeform) or a free
        /// prism, never shielded mass, and only where the cell's diet and this creature's band allow.</summary>
        bool IsEdible(Prism prism)
        {
            if (!prism || prism.destroyed || prism == _hitPrism) return false;
            if (IsShieldedMass(prism)) return false;
            if (prism is HealthPrism hp)
                return hp.LifeForm && (cell != null ? IsPreyForMe(prism.transform.position, hp.LifeForm.domain)
                                                    : hp.LifeForm.domain != domain);
            return cell != null ? IsPreyForMe(prism.transform.position, prism.Domain) : prism.Domain != domain;
        }

        Vector3 MouthWorld()
        {
            if (_ticker == null) return transform.position;
            var cur = _ticker.Cur;
            float vs = config.VoxelSize;
            var local = new Vector3(cur.HeadX * vs, cur.HeadY * vs, cur.FrontZ * vs + config.MouthReach);
            if (_mouth) _mouth.localPosition = local;
            return transform.TransformPoint(local);
        }

        // ───────────────────────────────────────────────────────────────
        //  The heart
        // ───────────────────────────────────────────────────────────────

        /// <summary>Behind the head's front plane by the configured depth, on the head's centre line (body space).</summary>
        Vector3 HeartSeatLocal()
        {
            if (_ticker == null) return Vector3.zero;
            var cur = _ticker.Cur;
            if (cur.Alive == 0) return _heartSeat * config.VoxelSize;
            var want = new Vector3(cur.HeadX, cur.HeadY, cur.FrontZ - config.HeartSeatDepthVoxels);
            _heartSeat = _heartSeated ? Vector3.Lerp(_heartSeat, want, 0.2f) : want;
            _heartSeated = true;
            return _heartSeat * config.VoxelSize;
        }

        void SeatHeart()
        {
            var heart = LivingHeart;
            if (heart) heart.transform.localPosition = HeartSeatLocal();
        }

        // ───────────────────────────────────────────────────────────────
        //  Spatial index: one virtual entry per segment, and the one hit prism
        // ───────────────────────────────────────────────────────────────

        void BindEntries()
        {
            _index = PrismSpatialIndex.EnsureInstance();
            int s = config.Segments;
            _entryIds = new int[s];
            _entryEmpty = new bool[s];
            for (int g = 0; g < s; g++) _entryIds[g] = -1;
            if (_index == null || !_index.IsAvailable) return;
            var host = HostCell;
            for (int g = 0; g < s; g++)
            {
                Vector3 at = SegmentWorld(g);
                int id = _index.RegisterVirtual(this, g, new float3(at.x, at.y, at.z), (int)domain, 1f);
                _entryIds[g] = id;
                if (id >= 0 && host) host.BindVirtualMass(id, domain);   // the body is the cell's mass, volume-only
            }
            UpdateEntryShapes();
        }

        void ReleaseEntries()
        {
            if (_entryIds == null) return;
            if (_index != null && _index.IsAvailable)
                for (int g = 0; g < _entryIds.Length; g++)
                    if (_entryIds[g] >= 0) _index.Unregister(_entryIds[g]);
            _entryIds = null;
        }

        /// <summary>Each segment carries its share of the live body volume (the body less its unpaid wounds), so the
        /// cell's sums see exactly the mass the creature has. An empty segment leaves every query.</summary>
        void UpdateEntryShapes()
        {
            if (_entryIds == null || _index == null || !_index.IsAvailable) return;
            var cur = _ticker.Cur;
            float live = LiveBodyVolume;
            float vs = config.VoxelSize;
            for (int g = 0; g < _entryIds.Length; g++)
            {
                int id = _entryIds[g];
                if (id < 0) continue;
                bool empty = cur.Alive == 0 || cur.SegN[g] == 0;
                float vol = empty ? 0f : live * cur.SegN[g] / cur.Alive;
                _index.UpdateCellVolume(id, vol);
                _index.UpdateVolume(id, Mathf.Max(vol, 1f));
                _index.SetVirtualRadius(id, (empty ? 0f : cur.SegRadius[g]) * vs);
                if (g == _hitSlot || empty == _entryEmpty[g]) continue;
                _entryEmpty[g] = empty;
                _index.SetVirtualSuspended(id, empty);
            }
        }

        /// <summary>The movers contract: the entries (and the hit prism) are where the body is drawn, every frame.</summary>
        void PushEntries()
        {
            if (_entryIds == null || _index == null || !_index.IsAvailable) return;
            for (int g = 0; g < _entryIds.Length; g++)
                if (_entryIds[g] >= 0) _index.UpdatePosition(_entryIds[g], SegmentWorld(g));
        }

        Vector3 SegmentWorld(int g)
        {
            var cur = _ticker.Cur;
            float vs = config.VoxelSize;
            return transform.TransformPoint(new Vector3(cur.SegCentre[3 * g] * vs, cur.SegCentre[3 * g + 1] * vs,
                                                        cur.SegCentre[3 * g + 2] * vs));
        }

        Vector3 SegmentBody(int g)
        {
            var cur = _ticker.Cur;
            return new Vector3(cur.SegCentre[3 * g], cur.SegCentre[3 * g + 1], cur.SegCentre[3 * g + 2]);
        }

        /// <summary>
        /// A weapon must apply gameplay to segment <paramref name="slot"/>: one hidden HealthPrism there, creation
        /// complete, owned by this creature. Its explosion is the bite (<see cref="OnBodyPrismExploded"/>). Only one
        /// stands at a time - a forced request for another segment retires the old one (its segment is unharmed).
        /// </summary>
        Prism IVirtualPrismOwner.MaterialiseVirtualPrism(int slot)
        {
            if (_dying || _ticker == null || !config.HitPrismPrefab || slot < 0 || slot >= config.Segments) return null;
            if (_hitPrism && !_hitPrism.destroyed)
            {
                if (_hitSlot == slot) return _hitPrism;
                RetireHitPrism();
            }
            if (_ticker.Cur.SegN[slot] == 0) return null;

            var hp = Instantiate(config.HitPrismPrefab, SegmentWorld(slot), transform.rotation, transform);
            hp.transform.localScale = config.HitPrismScale;
            hp.TargetScale = config.HitPrismScale;
            hp.OwnerFauna = this;
            hp.ChangeTeam(domain);
            hp.Initialize(AttributionName);
            hp.CompleteCreationImmediately();
            hp.CompleteGrowthImmediately();
            hp.SetOwnerHidden(true);   // the body is drawn by the creature; this is only its collider and its mass
            _hitPrism = hp;
            _hitSlot = slot;
            _hitAt = Time.time;
            return hp;                 // the index suspends the segment's entry
        }

        bool IVirtualPrismBudget.HasMaterialiseBudget(int slot) => !_dying && (_hitSlot < 0 || _hitSlot == slot);

        /// <summary>The hit prism exploded: a bite at its segment, credited to whoever fired. Never the base rule
        /// (which dies when no body prism is left) - this body is the network, and it dies of its wounds.</summary>
        public override void OnBodyPrismExploded(HealthPrism prism, string killerName)
        {
            if (!prism || prism != _hitPrism || _dying) return;
            int slot = _hitSlot;
            _hitPrism = null;
            _hitSlot = -1;
            ResumeEntry(slot);
            Bite(SegmentBody(slot), config.BiteRadiusVoxels, killerName);
        }

        void UpkeepHitPrism()
        {
            if (_hitSlot < 0) return;
            if (!_hitPrism || _hitPrism.destroyed)
            {
                // taken some other way (a creature's suction, a cleanup): the segment lost that mass all the same
                int slot = _hitSlot;
                _hitPrism = null;
                _hitSlot = -1;
                ResumeEntry(slot);
                Bite(SegmentBody(slot), config.BiteRadiusVoxels, string.Empty);
                return;
            }
            if (Time.time - _hitAt > config.HitPrismSeconds)
            {
                RetireHitPrism();
                return;
            }
            _hitPrism.transform.position = SegmentWorld(_hitSlot);
            _hitPrism.NotifyPositionChanged();
        }

        /// <summary>Not a hit after all (it timed out, or another segment was asked for): the prism goes, unharmed.</summary>
        void RetireHitPrism()
        {
            int slot = _hitSlot;
            var hp = _hitPrism;
            _hitPrism = null;
            _hitSlot = -1;
            if (hp) Destroy(hp.gameObject);
            if (slot >= 0) ResumeEntry(slot);
        }

        void ResumeEntry(int slot)
        {
            if (_entryIds == null || slot < 0 || slot >= _entryIds.Length || _index == null || !_index.IsAvailable) return;
            int id = _entryIds[slot];
            if (id < 0) return;
            _index.UpdatePosition(id, SegmentWorld(slot));
            _index.SetVirtualSuspended(id, _entryEmpty[slot]);
        }

        protected override void OnTeamChanged()
        {
            if (_hitPrism) _hitPrism.ChangeTeam(domain);
            var host = HostCell;
            if (_entryIds != null && _index != null && _index.IsAvailable)
                for (int g = 0; g < _entryIds.Length; g++)
                {
                    if (_entryIds[g] < 0) continue;
                    _index.UpdateDomain(_entryIds[g], (int)domain);
                    if (host) host.BindVirtualMass(_entryIds[g], domain);
                }
            Restyle();
        }

        // ───────────────────────────────────────────────────────────────
        //  Drawing: one prism entity per visible surface voxel
        // ───────────────────────────────────────────────────────────────

        static float[] BodyPositions(NcaVoxelWeights w)
        {
            lock (s_bodyPositions)
            {
                if (s_bodyPositions.TryGetValue(w, out var cached)) return cached;
                int n = w.D * w.H * w.W, hw = w.H * w.W;
                var pos = new float[3 * n];
                for (int i = 0; i < n; i++)
                {
                    int z = i / hw, y = (i - z * hw) / w.W, x = i - z * hw - y * w.W;
                    w.GridToBody(z, y, x, out pos[3 * i], out pos[3 * i + 1], out pos[3 * i + 2]);
                }
                s_bodyPositions[w] = pos;
                return pos;
            }
        }

        Material DomainMaterial()
        {
            var theme = config.Theme;
            if (!theme) return null;
            var sets = theme.TeamMaterialSets;
            var set = sets != null && sets.TryGetValue(domain, out var painted) && painted ? painted : theme.BaseMaterialSet;
            return set ? set.BlockMaterial : null;
        }

        void BindBody()
        {
            var prefabPrism = config.HitPrismPrefab;
            _mesh = prefabPrism && prefabPrism.TryGetComponent(out MeshFilter mf) ? mf.sharedMesh : null;
            _material = DomainMaterial();
            _poolSize = Mathf.Max(1, config.MaxShown);
            if (!_mesh || !_material)
            {
                CSDebug.LogWarning($"[NcaCreature] {name}: no prism mesh or {domain} block material - the body is not " +
                                   "drawn (check the config's HitPrismPrefab and Theme).");
                return;
            }

            _matrices = new NativeArray<float4x4>(_poolSize, Allocator.Persistent);
            if (PrismRenderService.Enabled)
            {
                _handles = new NativeArray<PrismRenderHandle>(_poolSize, Allocator.Persistent);
                float4x4 parked = float4x4.TRS(transform.position, quaternion.identity, new float3(1e-4f));
                for (int k = 0; k < _poolSize; k++) _matrices[k] = parked;
                _entities = PrismRenderService.CreateBatch(_mesh, _material, gameObject.layer, _matrices, _handles);
            }
            if (!_entities) _fallbackMatrices = new Matrix4x4[_poolSize];
        }

        void ReleaseBody()
        {
            if (_handles.IsCreated)
            {
                for (int k = 0; k < _handles.Length; k++)
                {
                    var h = _handles[k];
                    PrismRenderService.Destroy(ref h);
                }
                _handles.Dispose();
            }
            if (_matrices.IsCreated) _matrices.Dispose();
            if (_fallbackMaterial) Object.Destroy(_fallbackMaterial);
            _entities = false;
            _shown = 0;
        }

        void Restyle()
        {
            var mat = DomainMaterial();
            if (!mat || mat == _material) return;
            _material = mat;
            if (_entities)
                for (int k = 0; k < _handles.Length; k++) PrismRenderService.SetMaterial(_handles[k], mat, true);
            if (_fallbackMaterial) { Object.Destroy(_fallbackMaterial); _fallbackMaterial = null; }
        }

        /// <summary>
        /// Every drawn voxel between the previous and the current step at <paramref name="blend"/>: the lab's scale -
        /// a flat prism lying on the skin (its y along the outward normal, its z trailing toward the tail), sized by
        /// how solid the voxel is, so the growing and healing fringe draws small. Interior voxels are not drawn.
        /// <paramref name="keep"/> (dying) filters and moves voxels; null draws the living body.
        /// </summary>
        void Draw(float blend, Func<Vector3, float> keep)
        {
            if (!_matrices.IsCreated) return;
            var cur = _ticker.Cur;
            var prev = _ticker.Prev;
            Matrix4x4 root = transform.localToWorldMatrix;
            float vs = config.VoxelSize;
            Vector3 shape = config.ScaleShape;
            int n = 0;

            for (int k = 0; k < cur.Count && n < _poolSize; k++)
            {
                int i = cur.List[k];
                if (cur.Interior[i] != 0) continue;
                float a = Mathf.Lerp(prev.Alpha[i], cur.Alpha[i], blend);
                Emit(i, a, cur.Normal, ref n, root, vs, shape, keep);
            }
            for (int k = 0; k < prev.Count && n < _poolSize; k++)
            {
                int i = prev.List[k];
                if (cur.Alpha[i] > 0f || prev.Interior[i] != 0) continue;   // drawn above, or hidden
                Emit(i, prev.Alpha[i] * (1f - blend), prev.Normal, ref n, root, vs, shape, keep);
            }

            if (_entities)
            {
                for (int k = n; k < _shown; k++) PrismRenderService.QueueVisible(_handles[k], false);
                for (int k = _shown; k < n; k++) PrismRenderService.QueueVisible(_handles[k], true);
                _shown = n;
                if (n > 0) PrismRenderService.SetTransformsBatch(_handles, _matrices, n);
            }
            else if (n > 0 && _fallbackMatrices != null)
            {
                if (!_fallbackMaterial) _fallbackMaterial = new Material(_material) { enableInstancing = true };
                for (int k = 0; k < n; k++) _fallbackMatrices[k] = _matrices[k];
                var rp = new RenderParams(_fallbackMaterial) { layer = gameObject.layer };
                for (int start = 0; start < n; start += 1023)
                    Graphics.RenderMeshInstanced(rp, _mesh, 0, _fallbackMatrices, Mathf.Min(1023, n - start), start);
            }
        }

        void Emit(int i, float a, float[] normals, ref int n, Matrix4x4 root, float vs, Vector3 shape,
                  Func<Vector3, float> keep)
        {
            float t = Mathf.Clamp01((a - 0.12f) / 0.43f);
            float size = vs * 1.15f * t * t * (3f - 2f * t) * (0.75f + 0.25f * a);
            var body = new Vector3(_bodyPos[3 * i], _bodyPos[3 * i + 1], _bodyPos[3 * i + 2]);
            if (keep != null) size *= keep(body);
            if (size < 1e-3f) return;

            var up = new Vector3(normals[3 * i], normals[3 * i + 1], normals[3 * i + 2]);
            var fwd = Vector3.back - up * Vector3.Dot(Vector3.back, up);   // toward the tail, on the skin
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.Cross(up, Vector3.right);
            fwd.Normalize();
            var right = Vector3.Cross(up, fwd);

            Vector3 at = body * vs;
            if (_dying && _deathStyleAtDeath == LifeformDeathStyle.Consumed)
                at = Vector3.Lerp(at, transform.InverseTransformPoint(_devourFrom), DeathProgress());

            var m = new Matrix4x4(
                right * (shape.x * size),
                up * (shape.y * size),
                fwd * (shape.z * size),
                new Vector4(at.x, at.y, at.z, 1f));
            _matrices[n++] = root * m;
        }

        // ───────────────────────────────────────────────────────────────
        //  Death (the sealed Fauna.Die runs first; this is the husk)
        // ───────────────────────────────────────────────────────────────

        protected override void OnDeath(string killerName = "")
        {
            if (_dying) return;
            _dying = true;
            _deathAt = Time.time;
            _deathStyleAtDeath = DeathStyle;
            _devourFrom = DevourTarget ? DevourTarget.position : transform.position;
            RetireHitPrism();
            ReleaseEntries();   // the body leaves the index now: what is left of it is coming apart

            // how far the body reaches from its heart - the wither's front starts there
            _deathReach = 1f;
            if (_ticker != null)
            {
                var cur = _ticker.Cur;
                for (int k = 0; k < cur.Count; k++)
                {
                    int i = cur.List[k];
                    var body = new Vector3(_bodyPos[3 * i], _bodyPos[3 * i + 1], _bodyPos[3 * i + 2]);
                    _deathReach = Mathf.Max(_deathReach, (body - _heartSeat).magnitude);
                }
            }
        }

        float DeathProgress()
        {
            float seconds = _deathStyleAtDeath == LifeformDeathStyle.Consumed ? config.DevourSeconds : config.WitherSeconds;
            return Mathf.Clamp01((Time.time - _deathAt) / Mathf.Max(0.05f, seconds));
        }

        void UpdateDeath()
        {
            if (DevourTarget) _devourFrom = DevourTarget.position;
            float u = DeathProgress();
            float front = _deathReach * (1f - u);
            float reach = _deathReach;
            Vector3 seat = _heartSeat;
            Func<Vector3, float> keep = _deathStyleAtDeath switch
            {
                // a jouster took the heart: the body unravels from the hole outward
                LifeformDeathStyle.Jousted => b => (b - seat).magnitude >= reach * u ? 1f : 0f,
                // devoured: everything shrinks as it is pulled into the eater
                LifeformDeathStyle.Consumed => _ => 1f - u,
                // withered: outside-in, the heart last
                _ => b => (b - seat).magnitude <= front ? 1f : 0f,
            };
            using (s_mRender.Auto()) Draw(1f, keep);
            if (u < 1f) return;

            ReleaseHeart();   // the wither reached the core (idempotent for the styles that freed it at once)
            ReleaseBody();
            enabled = false;
            if (!DespawnOrDestroy()) Destroy(gameObject);
        }
    }
}
