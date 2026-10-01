using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A SWARM - a population of tadpole fauna that grows the body plan of its majority element
    /// (Mass whale, Space jellyfish, Charge pufferfish, Time dragonfly), swims, reacts to vessels,
    /// and MORPHS into another animal when players kill enough of its majority. Docs/SWARM_FAUNA.md.
    ///
    /// This is the population's ANCHOR and brain, in the shape the worm colony established
    /// (Docs/ECOSYSTEM.md §23.3): it is lineage-registered through the ordinary spawner (so the
    /// cell's seed floor, cap and cleanup all apply), it carries NO heart and NO body, and it cannot
    /// be preyed on. The lifeforms are its MEMBERS (<see cref="SwarmTadpoleFauna"/>), each with its
    /// own heart and body prism, each dying through the sealed fauna death path.
    ///
    /// The behaviour is ONE simulation over a struct-of-arrays run at a fixed tick - the config picks
    /// which (<see cref="SwarmFaunaConfigSO.Model"/>): <see cref="SwarmFieldCore"/> (designed fields,
    /// every tadpole owns a slot) or <see cref="SwarmGridCore"/> (the grid morphogen, nothing assigns a
    /// place). Members are posed by interpolating between ticks, so motion is smooth at any frame rate
    /// and no member runs an Update of its own.
    ///
    /// Invariants it touches (Docs/SWARM_FAUNA.md §2): mass is conserved (every egg is PAID for out of
    /// eaten flora volume, 1:1), there is no imposed death (members only die to vessels, predators or
    /// starvation), one colour (every member wears this anchor's domain - the cell's controlling one),
    /// every member drops its crystal, and nothing pops (births bloom, molts re-form, deaths wither).
    /// </summary>
    public class SwarmFauna : Fauna
    {
        [Header("Swarm")]
        [SerializeField] SwarmFaunaConfigSO config;

        ISwarmCore _core;
        SwarmTadpoleFauna[] _members;
        SVector3[] _prevPos, _prevFace;
        float[] _birthTime, _heartFactor;
        bool[] _danger;
        SwarmPlanData[] _plans;
        Vector3[] _defaultHalf;          // per research element: a typical prism of that element
        Vector3 _centre;                 // the host cell's centre (sim origin)
        float _dt, _acc;
        int _biteCursor;
        float _lastFedTime, _lastShedTime, _extinctSince = -1f;
        Element _startElement = Element.Mass;
        bool _seeded;
        string _eaterName;
        FMODUnity.StudioEventEmitter _loop;
        readonly SwarmPredator[] _preds = new SwarmPredator[8];
        int _predCount;
        readonly List<IVesselStatus> _seen = new(8);
        Flora _goalPlant;
        float _atPlantSince = -1f;
        readonly Dictionary<Flora, float> _barren = new();

        /// <summary>The live sim (null before the first frame).</summary>
        public ISwarmCore Core => _core;
        public SwarmFaunaConfigSO Config => config;

        /// <summary>The body plan the swarm is currently growing ("mass", "space", "charge", "time").</summary>
        public string CurrentPlan => _core != null ? _core.Plan.Kind : "";

        public override float CurrentSpeed => config ? config.Cruise * config.UnitScale * config.TickHz : 0f;

        /// <summary>A member's swim speed in world units/s - what a jouster has to outrun.</summary>
        public float MemberSpeed(int i) =>
            _core != null && i >= 0 && i < _core.Cap ? _core.Vel[i].Length() * config.UnitScale * config.TickHz : 0f;

        /// <summary>The anchor is a population, not an animal: nothing eats a swarm whole.</summary>
        public override bool Predated(string predatorName, Transform devourTarget) => false;

        /// <summary>
        /// Element-as-data for a POPULATION: the anchor is heartless, so a species config's element
        /// does not provision a crystal here - it names the body the swarm hatches as (its starting
        /// majority). Members carry the hearts.
        /// </summary>
        protected override void ProvisionHeart(Element element)
        {
            if (element is >= Element.Charge and <= Element.Time) _startElement = element;
        }

        protected override void Start()
        {
            base.Start();
            Seed();
        }

        // ───────────────────────────────────────────────────────────────── seeding

        void Seed()
        {
            if (_seeded) return;
            var host = HostCell;
            if (!config || !config.TadpolePrefab || !host)
            {
                CSDebug.LogWarning($"{name}: swarm has no config, tadpole prefab or host cell - it will not hatch.");
                return;
            }
            _plans = SwarmPlanLibrary.Load(config);
            if (_plans == null) return;
            _seeded = true;

            _defaultHalf = SwarmPlanLibrary.TypicalHalfExtents(_plans);
            _centre = host.transform.position;
            _dt = 1f / config.TickHz;
            _eaterName = "swarm";

            _core = config.Model == SwarmModel.Grid ? BuildGridCore(host) : BuildFieldCore(host);
            int n = _core.Cap;
            _members = new SwarmTadpoleFauna[n];
            _prevPos = new SVector3[n]; _prevFace = new SVector3[n];
            _birthTime = new float[n]; _heartFactor = new float[n];
            _danger = new bool[n];

            var anchor = ToSim(transform.position);
            var radial = transform.position - _centre;
            var tangent = Vector3.Cross(radial.sqrMagnitude > 1f ? radial.normalized : Vector3.forward, Random.onUnitSphere);
            if (tangent.sqrMagnitude < 1e-4f) tangent = Vector3.right;
            _core.Seed(SwarmFaunaConfigSO.ToIndex(_startElement), config.SeedMembers, anchor, Sim(tangent.normalized));
            _core.SwimTarget = anchor;
            for (int i = 0; i < n; i++)
                if (_core.Alive[i]) SpawnMember(i, -1);

            _lastFedTime = Time.time;
            StartLoop();
            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[Swarm] {name} ({config.Model}) hatched as {_core.Plan.Kind} with {_core.AliveCount} tadpoles at r={radial.magnitude:F0}");
        }

        // MembraneRadius reads 0 until the membrane has spawned (Docs/CONNECTING_PANEL.md); the
        // standard membrane's 1200 is the fallback rather than a 0 that would pen the body in a point
        float SimMembrane(Cell host) => (host.MembraneRadius > 1f ? host.MembraneRadius : 1200f) * 0.97f / config.UnitScale;

        int PlanCap => Mathf.Max(_plans[0].N, Mathf.Max(_plans[1].N, Mathf.Max(_plans[2].N, _plans[3].N)));

        bool TryBand(out float inner, out float outer)
        {
            inner = outer = 0f;
            var cfg = SourceConfig;
            if (!cfg || cfg.BandOuterRadius <= 0f) return false;
            inner = Mathf.Min(cfg.BandInnerRadius, cfg.BandOuterRadius) / config.UnitScale;
            outer = Mathf.Max(cfg.BandInnerRadius, cfg.BandOuterRadius) / config.UnitScale;
            return true;
        }

        ISwarmCore BuildFieldCore(Cell host)
        {
            var p = new SwarmFieldParams
            {
                LayRate = config.LayRate, LayMax = config.LayMax,
                Cruise = config.Cruise, Turn = config.TurnPerStep,
                Membrane = SimMembrane(host),
                CrossCost = config.CrossElementCost,
                Cap = PlanCap,
            };
            for (int e = 0; e < 4; e++) p.EggCost[e] = SwarmFaunaConfigSO.Of(config.EggVolume, SwarmFaunaConfigSO.ToElement(e));
            if (TryBand(out float lo, out float hi)) { p.BandInner = lo; p.BandOuter = hi; }
            return new SwarmFieldCore(_plans, p, Random.Range(1, int.MaxValue));
        }

        /// <summary>
        /// The grid morphogen in its GAME settings (Docs/SWARM_FAUNA.md §8): one domain, funded laying,
        /// hunger that never kills on its own (it only picks the starvation victim), an oriented body
        /// that swims, and a plan lock.
        /// </summary>
        ISwarmCore BuildGridCore(Cell host)
        {
            var p = new SwarmGridParams
            {
                G = config.GridSize, Cell = config.GridCell, Quant = false,
                KClass = config.GridKClass, KTotal = config.GridKTotal, Persist = config.GridPersist,
                Noise = config.GridNoise, PLay = config.GridLayChance, PCross = config.GridCrossChance,
                LayMaxPerStep = config.GridLayMaxPerStep, KFine = config.GridKFine, Sigma = config.GridSigma,
                KFF = config.GridFeedForward, Lock = config.GridPlanLock,
                Periods = new[]
                {
                    Mathf.Max(1, Mathf.RoundToInt(config.GridFramePeriod.x)), Mathf.Max(1, Mathf.RoundToInt(config.GridFramePeriod.y)),
                    Mathf.Max(1, Mathf.RoundToInt(config.GridFramePeriod.z)), Mathf.Max(1, Mathf.RoundToInt(config.GridFramePeriod.w)),
                },
                DomainSlots = false, HungerKills = false, Funded = true, Oriented = true,
                Cruise = config.Cruise, Turn = config.TurnPerStep,
                Membrane = SimMembrane(host), CrossCost = config.CrossElementCost, Cap = PlanCap,
            };
            for (int e = 0; e < 4; e++) p.EggCost[e] = SwarmFaunaConfigSO.Of(config.EggVolume, SwarmFaunaConfigSO.ToElement(e));
            if (TryBand(out float lo, out float hi)) { p.BandInner = lo; p.BandOuter = hi; }
            return new SwarmGridCore(_plans, p, Random.Range(1, int.MaxValue));
        }

        void StartLoop()
        {
            if (config.SwarmLoopEvent.IsNull) return;
            _loop = gameObject.AddComponent<FMODUnity.StudioEventEmitter>();
            _loop.EventReference = config.SwarmLoopEvent;
            gameObject.AddComponent<CosmicShore.Gameplay.Audio.EmitterSfxVolumeBinder>();
            _loop.Play();
        }

        // ───────────────────────────────────────────────────────────────── members

        void SpawnMember(int i, int parent)
        {
            var e = SwarmFaunaConfigSO.ToElement(_core.Elem[i]);
            var pos = ToWorld(_core.Pos[i]);
            var face = Face(_core.Facing[i]);
            var member = Instantiate(config.TadpolePrefab, pos, face);
            member.Bind(HostCell, this, i, e, SwarmFaunaConfigSO.Of(config.HeartWorldScale, e),
                        ShapeFor(i, _core.Elem[i]), PrismZ(i, _core.Elem[i]));
            HostCell.RegisterSpawnedObject(member.gameObject);
            // The replication seam every fauna producer reaches. A swarm member carries no
            // NetworkObject (members are client-local, like every freestyle creature), so this
            // only neutralizes; it is here so the seam gate holds for this producer too.
            FaunaNetworkSync.ServerSpawn(member);

            // continuity of existence: a newborn grows in from nothing
            member.transform.localScale = Vector3.one * 0.001f;
            _members[i] = member;
            _birthTime[i] = Time.time;
            _heartFactor[i] = 1f;
            _danger[i] = false;
            _prevPos[i] = _core.Pos[i];
            _prevFace[i] = _core.Facing[i];
        }

        /// <summary>A member died (any path). The body re-solves its homes around the hole.</summary>
        public void HandleMemberDeath(SwarmTadpoleFauna member)
        {
            if (_core == null || !member) return;
            int i = member.Index;
            if (i < 0 || i >= _members.Length || _members[i] != member) return;
            _core.Kill(i);
            _members[i] = null;
        }

        /// <summary>Body prism shape for member i as element e: the core's look for it (the field core:
        /// its home slot's prism when the slot is of its own element; the grid core: the prism its look
        /// state has eased to), else a typical prism of that element. Local scale, long axis +z.</summary>
        Vector3 ShapeFor(int i, int e)
        {
            Vector3 h = _defaultHalf[e];
            if (_core.TryGetLook(i, e, out var s, out _)) h = new Vector3(s.X, s.Y, s.Z);
            float m = 2f * config.UnitScale * config.PrismScale;
            return new Vector3(h.y * m, h.z * m, h.x * m);
        }

        float PrismZ(int i, int e) =>
            -(SwarmFaunaConfigSO.Of(config.HeartWorldScale, SwarmFaunaConfigSO.ToElement(e))
              + config.HeartPrismGap + 0.5f * ShapeFor(i, e).z);

        // ───────────────────────────────────────────────────────────────── the clock

        void Update()
        {
            if (_core == null) return;

            _acc += Time.deltaTime;
            int steps = 0;
            while (_acc >= _dt && steps < config.MaxStepsPerFrame)
            {
                Tick();
                _acc -= _dt;
                steps++;
            }
            if (steps == config.MaxStepsPerFrame && _acc > _dt) _acc = _dt;   // drop time, never spiral

            Render(Mathf.Clamp01(_acc / _dt));
        }

        void Tick()
        {
            int n = _core.Cap;
            for (int i = 0; i < n; i++) { _prevPos[i] = _core.Pos[i]; _prevFace[i] = _core.Facing[i]; }

            SenseVessels();
            _core.SwimTarget = ToSim(Goal);
            _core.Step(new System.ReadOnlySpan<SwarmPredator>(_preds, 0, _predCount));

            var events = _core.Events;
            for (int q = 0; q < events.Count; q++)
            {
                var ev = events[q];
                switch (ev.Kind)
                {
                    case SwarmEventKind.Laid:
                        SpawnMember(ev.Index, ev.Other);
                        break;
                    case SwarmEventKind.MoltBegan:
                        if (_members[ev.Index]) _members[ev.Index].SetShape(ShapeFor(ev.Index, ev.Other), PrismZ(ev.Index, ev.Other));
                        break;
                    case SwarmEventKind.Switched:
                        OnMorph(ev.Index, ev.Other);
                        break;
                    case SwarmEventKind.Starved:
                        // only a research-mode grid core kills by itself; the game never builds one
                        // (BuildGridCore sets HungerKills = false). Kept so the death is never silent.
                        if (_members[ev.Index]) _members[ev.Index].Starve();
                        break;
                }
            }
            events.Clear();

            // shapes and tiers follow the homes, at the homes' own cadence
            bool reshape = _core.Clock % 8 == 0;
            for (int i = 0; i < n; i++)
            {
                var m = _members[i];
                if (!m || !_core.Alive[i]) continue;
                int e = _core.EffectiveElement(i);
                if (reshape && _core.Molt[i] <= 0f) m.SetShape(ShapeFor(i, e), PrismZ(i, e));
                UpdateTier(i, m, e);
            }

            Feed();
            Starvation();
            Extinction();
        }

        void OnMorph(int fromPlan, int toPlan)
        {
            CSDebug.LogVerbose(CSLogChannel.Ecology,
                $"[Swarm] {name} morphs {_plans[fromPlan].Kind} -> {_plans[toPlan].Kind} ({_core.AliveCount} tadpoles)");
            if (!config.MorphEvent.IsNull && AudioSystem.Instance)
                AudioSystem.Instance.PlaySFXEvent(config.MorphEvent, transform.position);
        }

        void UpdateTier(int i, SwarmTadpoleFauna m, int e)
        {
            if (e != 0) { m.SetTier(false, false); _danger[i] = false; return; }   // only Charge changes state
            float st = _core.Startle[i];
            if (_danger[i]) { if (st < config.DangerExit) _danger[i] = false; }
            else if (st > config.DangerEnter) _danger[i] = true;
            bool shield = _core.TryGetLook(i, 0, out _, out int tier) && tier == 2;
            m.SetTier(_danger[i], shield);
        }

        // ───────────────────────────────────────────────────────────────── render

        void Render(float alpha)
        {
            float now = Time.time, dt = Time.deltaTime;
            float bloom = config.BirthBloomSeconds, moltRate = dt / config.MoltHeartSeconds;
            for (int i = 0; i < _core.Cap; i++)
            {
                var m = _members[i];
                if (!m || !_core.Alive[i]) continue;

                var p = SVector3.Lerp(_prevPos[i], _core.Pos[i], alpha);
                var f = SVector3.Lerp(_prevFace[i], _core.Facing[i], alpha);
                m.transform.SetPositionAndRotation(ToWorld(p), Face(f));

                float age = now - _birthTime[i];
                if (age < bloom)
                {
                    float a = Mathf.Clamp01(age / bloom);
                    m.transform.localScale = Vector3.one * Mathf.Max(0.001f, a * a * (3f - 2f * a));
                }
                else if (m.transform.localScale.x != 1f) m.transform.localScale = Vector3.one;

                // MOLT, made visible: the heart shrinks away, re-forms as the new element, grows back.
                var want = SwarmFaunaConfigSO.ToElement(_core.Molt[i] >= 0.5f ? _core.MoltTo[i] : _core.Elem[i]);
                float h = _heartFactor[i];
                if (m.HeartElement != want)
                {
                    h -= moltRate;
                    if (h <= 0f) { h = 0f; m.ReformHeart(want, SwarmFaunaConfigSO.Of(config.HeartWorldScale, want)); }
                    m.SetHeartDisplay(h);
                }
                else if (h < 1f)
                {
                    h = Mathf.Min(1f, h + moltRate);
                    m.SetHeartDisplay(h);
                }
                _heartFactor[i] = h;

                m.SyncBodyToIndex();
            }
            transform.position = ToWorld(_core.Anchor);
        }

        // ───────────────────────────────────────────────────────────────── vessels

        void SenseVessels()
        {
            _predCount = 0;
            _seen.Clear();
            float radius = _core.Plan.Radius * config.UnitScale * 1.6f + config.SenseMargin;
            int hits = Physics.OverlapSphereNonAlloc(transform.position, radius, OverlapScratch, NonPrismOverlapMask);
            float toSimVel = 1f / (config.UnitScale * config.TickHz);
            for (int h = 0; h < hits && _predCount < _preds.Length; h++)
            {
                var col = OverlapScratch[h];
                if (!col) continue;
                if (!col.TryGetComponent(out IVesselStatus status))
                    status = col.GetComponentInParent<IVesselStatus>();
                if (status == null || _seen.Contains(status) || status is not Component c || !c) continue;
                _seen.Add(status);
                var v = status.Course * status.Speed;
                _preds[_predCount++] = new SwarmPredator
                {
                    C = ToSim(c.transform.position),
                    V = Sim(v) * toSimVel,
                    R = config.VesselRadius / config.UnitScale,
                };
            }
        }

        // ───────────────────────────────────────────────────────────────── food

        /// <summary>
        /// Grazing. A few members per step take one bite each of the nearest edible FLORA prism within
        /// reach; the prism is consumed (suctioned into the biter - the food web's sanctioned
        /// down-force on mass) and its volume is banked in the stomach under the PLANT's element,
        /// which is what later pays for eggs. A full stomach stops grazing: a grown body does not
        /// strip its feeding ground for nothing.
        /// </summary>
        void Feed()
        {
            float capacity = config.StomachEggs * (config.EggVolume.x + config.EggVolume.y + config.EggVolume.z + config.EggVolume.w) * 0.25f;
            float banked = _core.Stomach[0] + _core.Stomach[1] + _core.Stomach[2] + _core.Stomach[3];
            if (banked >= capacity) { _lastFedTime = Time.time; return; }

            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;

            int n = _core.Cap, bitten = 0;
            for (int tries = 0; tries < n && bitten < config.BitersPerStep; tries++)
            {
                _biteCursor = (_biteCursor + 1) % n;
                int i = _biteCursor;
                var m = _members[i];
                if (!m || !_core.Alive[i]) continue;
                bitten++;
                Vector3 at = m.transform.position;
                int found = index.QuerySphere(at, config.BiteRadius, FeedScratch);
                for (int q = 0; q < found; q++)
                {
                    var prism = FeedScratch[q];
                    if (!IsFood(prism, out int e)) continue;
                    float volume = Mathf.Max(0.001f, prism.Volume);
                    prism.Consume(m.transform, domain, _eaterName, false, true);
                    _core.Stomach[e] += volume;
                    _lastFedTime = Time.time;
                    _atPlantSince = -1f;
                    break;
                }
            }
        }

        /// <summary>
        /// The swarm's diet: living FLORA mass only, through the platform's one edibility rule
        /// (<see cref="Fauna.IsPreyForMe"/> = the cell's spatial diet + this species' band) and the
        /// shielded-mass rule (<see cref="Fauna.IsShieldedMass"/> - which is why a Charge plant, whose
        /// leaves are armoured, is no food at all). Returns the plant's element as a research index.
        /// </summary>
        bool IsFood(Prism prism, out int element)
        {
            element = -1;
            if (!prism || prism.destroyed || prism is not HealthPrism hp) return false;
            if (hp.ResolveOwnerFauna() != null) return false;          // another creature's body
            if (hp.LifeForm is not Flora plant || !plant) return false;  // flora only
            if (IsShieldedMass(prism)) return false;
            if (!IsPreyForMe(prism.transform.position, prism.Domain)) return false;
            element = SwarmFaunaConfigSO.ToIndex(plant.Element);
            return element >= 0;
        }

        /// <summary>
        /// Where to swim: the nearest living plant inside this swarm's band that it has not just
        /// found grazed bare; else a fresh point in the band. Runs on the base class's goal clock.
        /// </summary>
        protected override Vector3 ResolveGoal()
        {
            if (_core == null || !HostCell) return Goal;
            Vector3 here = transform.position;
            float now = Time.time;

            // a plant the body has hovered over for a while without a single bite is grazed out
            if (_goalPlant && _atPlantSince >= 0f && now - _atPlantSince > 10f)
            {
                _barren[_goalPlant] = now;
                _goalPlant = null;
                _atPlantSince = -1f;
            }

            var plant = FloraHeartRegistry.NearestToPoint(here, f =>
                f.IsDying || !IsInsideBand(f.HeartTransform.position) ||
                HostCell.IsInsideNucleus(f.HeartTransform.position) ||
                (_barren.TryGetValue(f, out float t) && now - t < 45f));
            if (plant)
            {
                if (plant != _goalPlant) { _goalPlant = plant; _atPlantSince = -1f; }
                Vector3 target = plant.HeartTransform.position;
                if (_atPlantSince < 0f && (target - here).sqrMagnitude < 60f * 60f) _atPlantSince = now;
                return target;
            }

            _goalPlant = null;
            if ((Goal - here).sqrMagnitude > 40f * 40f) return Goal;   // still travelling to the last point
            Vector3 radial = here - _centre;
            Vector3 wander = here + Random.onUnitSphere * config.WanderReach;
            // keep the wander on this swarm's shell; the Goal setter clamps it into the band
            if (radial.sqrMagnitude > 1f) wander = _centre + (wander - _centre).normalized * radial.magnitude;
            return wander;
        }

        // ───────────────────────────────────────────────────────────────── starvation

        void Starvation()
        {
            float now = Time.time;
            if (now - _lastFedTime < config.StarvationSeconds) return;
            if (now - _lastShedTime < config.ShedIntervalSeconds) return;
            _lastShedTime = now;

            // shed the member the body needs least - the core decides who (the field core: one of the
            // most-surplus element; the grid core: the hungriest misplaced-surplus member)
            int victim = _core.StarvationVictim();
            if (victim >= 0 && _members[victim] && _core.Alive[victim]) _members[victim].Starve();
        }

        void Extinction()
        {
            if (_core.AliveCount > 0) { _extinctSince = -1f; return; }
            if (_extinctSince < 0f) { _extinctSince = Time.time; return; }
            if (Time.time - _extinctSince < config.ExtinctLingerSeconds) return;
            // The anchor has no body and no heart - removing it pops nothing - and the cell's seeder
            // hatches a fresh swarm in its place (extinction recovery, the seeder's sanctioned job).
            if (DespawnOrDestroy()) return;
            Destroy(gameObject);
        }

        // ───────────────────────────────────────────────────────────────── units

        SVector3 ToSim(Vector3 world) => Sim((world - _centre) / config.UnitScale);
        Vector3 ToWorld(SVector3 sim) => _centre + Uni(sim) * config.UnitScale;
        static SVector3 Sim(Vector3 v) => new(v.x, v.y, v.z);
        static Vector3 Uni(SVector3 v) => new(v.X, v.Y, v.Z);

        Quaternion Face(SVector3 f)
        {
            var fw = Uni(f);
            if (fw.sqrMagnitude < 1e-6f) fw = Uni(_core.BX);
            var up = Uni(_core.BY);
            if (Mathf.Abs(Vector3.Dot(fw.normalized, up)) > 0.98f) up = Uni(_core.BZ);
            return Quaternion.LookRotation(fw, up);
        }
    }
}
