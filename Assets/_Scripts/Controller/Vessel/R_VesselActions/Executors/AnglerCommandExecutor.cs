using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Angler's command system (design: <c>R_VesselActions/ANGLER.md</c>; decided in the Angler Command Lab,
    /// <c>Docs/Studios/ANGLER_COMMAND_LAB.md</c> round 2): it LURES wild fauna with the esca, TAMES the ones it holds in
    /// the light, leads them as a school, and sends a picked squad at particular prisms with the <b>Sling painter</b>.
    ///
    /// <list type="bullet">
    /// <item><b>RT, held</b> (read as analog depth, <see cref="IInputStatus.RightTriggerAnalog"/>): paints prisms with a beam
    /// from the nose — the squeeze sets the brush (<see cref="AnglerCommandMath.BrushScale"/>) — while the picked squad
    /// balls up ahead of the nose.</item>
    /// <item><b>LT, held</b>: erases paint with a narrower beam. While LT is held RT does not paint.</item>
    /// <item><b>RT, let go</b>: the throw. Each creature in the ball is given its own painted prism
    /// (<see cref="AnglerCommandMath.PickTarget"/>) and launched at it at throw speed, homing; inside its mouth's reach
    /// the throw ends and it eats by its own mouthful rule, with the painted set as its order. Nothing painted: it throws
    /// like a plain sling, and each creature latches onto the first enemy prism it passes.</item>
    /// <item><b>X</b> (held): the lure (<see cref="BeginLure"/>); <b>A</b>: pick the squad (<see cref="CycleSquad"/>);
    /// <b>B</b>: recall (<see cref="Recall"/>).</item>
    /// </list>
    ///
    /// <para><b>Who decides what.</b> Every peer runs this executor from the vessel's REPLICATED inputs (trigger depths
    /// and presses) and pose, so the paint marks are drawn everywhere. A creature can be leashed only by the peer that
    /// simulates it (<see cref="Fauna.TryLeash"/>), so the CONSEQUENCES — which creature swims where and what it eats —
    /// happen on the creature's authority alone and replicate as the creature's own state does.</para>
    ///
    /// <para><b>The ecology's rules hold.</b> A leash only bends where a creature swims and which edible prism its next
    /// mouthful faces (<see cref="IFaunaLeash"/>): every ordered prism is re-checked by the creature's own diet, eaten by
    /// its own mouthful, and a squad that is never ordered to eat starves on the game's own clock. Tamed creatures keep
    /// the cell's colour (a cell never holds two fauna colours, <c>ECOSYSTEM_DESIGN_PRINCIPLES.md</c>).</para>
    /// </summary>
    public sealed class AnglerCommandExecutor : ShipActionExecutorBase, IFaunaLeash
    {
        /// <summary>What a creature on the leash is doing (the lab's fauna states).</summary>
        public enum LeashOrder { Lured = 0, Follow = 1, Gather = 2, Thrown = 3, Attack = 4 }

        [Header("Wiring (serialized on the prefab so a missing wire shows in the inspector)")]
        [Tooltip("Every number this executor reads (the Angler Command Lab's, cloned by studio_to_unity.py). Wired directly (vessel rule 20).")]
        [SerializeField] AnglerConfigSO config;
        [Tooltip("The fleet's shared turn-end channel: the leash lets go of every creature and the paint is cleared.")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;

        [Header("Presentation (game only)")]
        [Tooltip("The paint brush's rim and the ball (the lab's #ffd27a).")]
        [SerializeField] Color paintColor = new(1f, 0.824f, 0.478f, 1f);
        [Tooltip("The eraser's rim (the lab's #7fd7ff).")]
        [SerializeField] Color eraseColor = new(0.498f, 0.843f, 1f, 1f);
        [Tooltip("The esca, the lure light (Jade, the lab's #13fff2).")]
        [SerializeField] Color escaColor = new(0.075f, 1f, 0.949f, 1f);
        [Tooltip("A creature being tamed: its taming shows as a dot that fills from this colour to the esca's.")]
        [SerializeField] Color tamingColor = new(0.6f, 0.6f, 0.7f, 1f);
        [Tooltip("A painted prism is drawn LIT: its own colour lifted this far toward white (Prism.SetColorLift; the lab's render(): col.lerp(WHITE, 0.75)). It stays lit after the throw until it is eaten or erased.")]
        [SerializeField, Range(0f, 1f)] float paintedLift = 0.75f;
        [Tooltip("Seconds a prism takes to light when painted and to go dark when erased, so a mark never pops.")]
        [SerializeField, Min(0f)] float markFade = 0.12f;

        sealed class Member
        {
            public Fauna Creature;
            public LeashOrder Order;
            public float Tame;
            public List<Prism> Set;
            public Prism Target;
            public float FlightTime;
            public bool Homing;
            public Vector3 ThrowDirection;
            public string Species;
            public int Slot;
            public float Phase;
        }

        IVesselStatus _status;
        AnglerVesselTransformer _transformer;
        readonly Dictionary<Fauna, Member> _members = new();
        readonly List<Member> _tamed = new();               // tamed creatures in taming order (the formation's order)
        readonly List<Member> _lured = new();
        readonly Dictionary<Fauna, float> _slipped = new(); // taming left on creatures that slipped out of the light
        readonly List<string> _species = new();             // the squad pick's order: the order kinds were first tamed
        readonly List<Prism> _painted = new();
        readonly HashSet<Prism> _paintedSet = new();
        readonly HashSet<Prism> _ordered = new();            // thrown at, not yet eaten or erased: the lab's `marked`
        readonly Dictionary<Prism, int> _claims = new();
        readonly List<Prism> _query = new();
        readonly List<Vector3> _candidatePositions = new();
        readonly List<int> _candidateClaims = new();
        readonly List<Prism> _candidates = new();
        readonly List<Fauna> _scratch = new();
        readonly List<Member> _memberScratch = new();
        readonly List<Fauna> _omniWild = new();
        readonly List<Vector3> _omniPoints = new();
        readonly List<int> _omniPicks = new();
        AnglerWorldMarks _marks;
        int _squad = -1;           // -1 = the whole school, else an index into _species
        bool _luring;
        bool _cmdHeld;
        float _cmdDepth, _eraseDepth;
        float _brushOwed;
        float _keeperRetryAt;      // when the next keeper may be born (Time.time); +inf once the turn has ended
        int _throwSerial;

        // ------------------------------------------------------------------ read by the HUD

        public int TamedCount => _tamed.Count;
        public int SquadCap => config ? config.SquadCap : 0;
        public int PaintedCount => _painted.Count;
        public bool Luring => _luring;
        public bool Painting => _cmdHeld && _eraseDepth <= 0.5f;
        public bool Erasing => _eraseDepth > 0.5f;
        public int SquadIndex => _squad;
        public int SpeciesCount => _species.Count;
        /// <summary>The picked squad's name ("School" for all of them).</summary>
        public string SquadName => _squad < 0 || _squad >= _species.Count ? "School" : _species[_squad];
        public int InBall => Count(LeashOrder.Gather);
        public int Hunting => Count(LeashOrder.Attack) + Count(LeashOrder.Thrown);
        /// <summary>The furthest-along taming in the light right now, 0..1.</summary>
        public float StrongestTame01
        {
            get
            {
                float best = 0f;
                for (int i = 0; i < _lured.Count; i++) best = Mathf.Max(best, _lured[i].Tame);
                return Mathf.Clamp01(best);
            }
        }

        // ------------------------------------------------------------------ lifecycle

        public override void Initialize(IVesselStatus shipStatus)
        {
            ReleaseAll();
            _status = shipStatus;
            _transformer = _status != null ? _status.VesselTransformer as AnglerVesselTransformer : null;
            _squad = -1;
            _luring = false;
            _cmdHeld = false;
            _cmdDepth = _eraseDepth = 0f;
            _brushOwed = 0f;
            _keeperRetryAt = 0f;
            _throwSerial = 0;
            _species.Clear();
            if (!config)
                CSDebug.LogError($"[Angler] {name}: AnglerCommandExecutor has no AnglerConfig - the Angler cannot lure, tame or " +
                                 "command. Wire Assets/_SO_Assets/VesselActions/Angler/AnglerConfig.asset.");
            if (_status != null && !_transformer)
                CSDebug.LogWarning($"[Angler] {name}: the vessel's transformer is not an AnglerVesselTransformer, so holding the " +
                                   "lure cannot slow the hull (the lab's lure brake).");
        }

        void OnEnable() => OnMiniGameTurnEnd.OnRaised += OnTurnEnd;

        void OnDisable()
        {
            OnMiniGameTurnEnd.OnRaised -= OnTurnEnd;
            ReleaseAll();
            if (_transformer) _transformer.LureSpeedCap = 0f;
        }

        void OnDestroy()
        {
            _marks?.Dispose();
            _marks = null;
        }

        void OnTurnEnd()
        {
            ReleaseAll();
            _keeperRetryAt = float.PositiveInfinity;   // the turn is over: no keeper is born into a finished match
            _luring = false;
            if (_transformer) _transformer.LureSpeedCap = 0f;
        }

        // ------------------------------------------------------------------ controls (AnglerActionSO)

        public void BeginLure() => _luring = true;

        public void EndLure() => _luring = false;

        /// <summary>The whole school, then each kind in the order it was first tamed, then the school again.</summary>
        public void CycleSquad() => _squad = _squad >= _species.Count - 1 ? -1 : _squad + 1;

        /// <summary>The picked squad drops its order and comes back to the school.</summary>
        public void Recall()
        {
            for (int i = 0; i < _tamed.Count; i++)
                if (InSquad(_tamed[i])) ToFollow(_tamed[i]);
        }

        /// <summary>
        /// The OMNI crystal (<see cref="AnglerTameByCrystalEffectSO"/>; the lab's <c>omniTame</c>): every wild or lured
        /// herbivore within <c>omniTameRadius</c> of the hull is tamed at once, nearest first
        /// (<see cref="AnglerCommandMath.OmniTameOrder"/>), up to the leash capacity. A creature can be leashed only by the
        /// peer that simulates it (<see cref="Fauna.TryLeash"/>), so this tames on the creatures' authority and nowhere
        /// else, exactly as the lure does. Returns how many joined the school.
        /// </summary>
        public int OmniTame()
        {
            if (_status == null || !config || !_status.Transform) return 0;
            _omniWild.Clear();
            _omniPoints.Clear();
            var cells = Cell.ActiveCellsSnapshot;
            for (int ci = 0; ci < cells.Count; ci++)
            {
                var cell = cells[ci];
                if (!cell) continue;
                var live = cell.LiveFauna;
                for (int i = 0; i < live.Count; i++)
                {
                    var f = live[i];
                    if (!f || f.IsDying) continue;
                    // the lab's WILD or LURED: not already in the school, and not on someone else's leash
                    if (_members.TryGetValue(f, out var held) ? held.Order != LeashOrder.Lured : f.IsLeashed) continue;
                    _omniWild.Add(f);
                    _omniPoints.Add(f.transform.position);
                }
            }
            int n = AnglerCommandMath.OmniTameOrder(_status.Transform.position, _omniPoints, config.OmniTameRadius,
                config.SquadCap - _tamed.Count, _omniPicks);
            int joined = 0;
            for (int k = 0; k < n; k++)
            {
                var f = _omniWild[_omniPicks[k]];
                if (_members.TryGetValue(f, out var m)) _lured.Remove(m);
                else
                {
                    if (!f.TryLeash(this)) continue;   // a predator, a puppet on this peer
                    m = new Member { Creature = f, Species = SpeciesOf(f), Phase = f.GetInstanceID() * 0.137f };
                    _members[f] = m;
                    _slipped.Remove(f);
                }
                JoinSchool(m);
                joined++;
            }
            return joined;
        }

        /// <summary>Tamed: the creature follows the school (the lab's state IDLE) and its kind becomes a squad.</summary>
        void JoinSchool(Member m)
        {
            m.Tame = 1f;
            m.Order = LeashOrder.Follow;
            _tamed.Add(m);
            if (!_species.Contains(m.Species)) _species.Add(m.Species);
        }

        // ------------------------------------------------------------------ the step

        void Update()
        {
            if (_status == null || !config || !_status.Transform) return;
            float dt = Time.deltaTime;
            PruneDead();
            EnsureKeeper();

            var input = _status.InputStatus;
            float rt = input != null ? input.RightTriggerAnalog : 0f;
            float lt = input != null ? input.LeftTriggerAnalog : 0f;
            bool held = rt > 0.5f, erasing = lt > 0.5f, released = !held && _cmdHeld;
            _cmdHeld = held;
            _cmdDepth = rt;
            _eraseDepth = lt;

            // ===== SLING PAINTER: the lab's `slingpaint` branch, line for line (AnglerCommandLab.html commandStep) =====
            _brushOwed += dt;
            if (_brushOwed >= config.BrushInterval)
            {
                _brushOwed = 0f;
                if (erasing) BrushStroke(config.EraseCone * AnglerCommandMath.BrushScale(lt, config.BrushMin), false);
                else if (held) BrushStroke(config.PaintCone * AnglerCommandMath.BrushScale(rt, config.BrushMin), true);
            }
            if (held)
                for (int i = 0; i < _tamed.Count; i++)
                {
                    var m = _tamed[i];
                    if (InSquad(m) && (m.Order == LeashOrder.Follow || m.Order == LeashOrder.Attack)) { ReleaseClaim(m); m.Set = null; m.Order = LeashOrder.Gather; }
                }
            if (released) ThrowAtPaint();

            if (_transformer) _transformer.LureSpeedCap = _luring ? config.LureCruise : 0f;
            UpdateLure(dt);
            UpdateFlights(dt);
            UpdateOrders();
            AssignSlots();
            DrawMarks();
        }

        // ---- the brush ----

        void BrushStroke(float coneDeg, bool paint)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;
            var t = _status.Transform;
            Vector3 o = t.position, f = t.forward;
            float reach = config.PaintRange, half = reach * 0.5f, rim = Mathf.Tan(Mathf.Clamp(coneDeg, 0f, 80f) * Mathf.Deg2Rad) * reach;
            int found = index.QuerySphere(o + f * half, Mathf.Sqrt(half * half + rim * rim), _query);
            var own = _status.Domain;
            bool removed = false;
            for (int i = 0; i < found; i++)
            {
                var p = _query[i];
                if (!p || p.destroyed) continue;
                bool painted = _paintedSet.Contains(p);
                if (paint ? painted : !(painted || _ordered.Contains(p))) continue;
                if (paint && config.SpareOwn && p.Domain == own) continue;
                // Game-side filters the lab's world has no use for: shielded mass is never food, and a creature's body
                // is never a meal for another (both rejected again by every creature's own diet).
                if (paint && (Fauna.IsShieldedMass(p) || (p is HealthPrism hp && hp.ResolveOwnerFauna()))) continue;
                if (!AnglerCommandMath.InCone(o, f, p.transform.position, coneDeg, reach)) continue;
                if (paint) { _paintedSet.Add(p); _painted.Add(p); if (ShowsMarks) p.SetColorLift(paintedLift, markFade); }
                else
                {
                    _paintedSet.Remove(p);
                    removed = true;
                    if (_ordered.Remove(p)) StrikeFromOrders(p);
                    Unlight(p, markFade);
                }
            }
            if (removed) _painted.RemoveAll(p => !_paintedSet.Contains(p));
        }

        // ---- the throw (the lab's throwAtPaint) ----

        void ThrowAtPaint()
        {
            _candidates.Clear();
            for (int i = 0; i < _painted.Count; i++)
                if (_painted[i] && !_painted[i].destroyed) _candidates.Add(_painted[i]);
            _painted.Clear();
            _paintedSet.Clear();
            // The order is given, and its prisms stay lit until they are eaten or erased (the lab's `marked`).
            for (int i = 0; i < _candidates.Count; i++) _ordered.Add(_candidates[i]);
            if (_candidates.Count == 0) { SlingThrow(); return; }

            var order = new List<Prism>(_candidates);
            for (int i = 0; i < _tamed.Count; i++)
            {
                var m = _tamed[i];
                if (!InSquad(m) || m.Order == LeashOrder.Thrown) continue;
                bool gathered = m.Order == LeashOrder.Gather;
                ReleaseClaim(m);
                m.Set = order;
                m.Order = LeashOrder.Attack;
                PickTarget(m);
                if (m.Order != LeashOrder.Attack) continue;
                if (gathered)
                {
                    m.Order = LeashOrder.Thrown;
                    m.Homing = true;
                    m.FlightTime = 0f;
                    Vector3 to = m.Target.transform.position - m.Creature.transform.position;
                    m.Creature.LeashLaunch(to.sqrMagnitude > 1e-6f ? to.normalized * config.SlingSpeed : _status.Transform.forward * config.SlingSpeed);
                }
            }
        }

        /// <summary>The Pack sling's throw (the Sling painter's, when nothing is painted): along the nose, a little spread.</summary>
        void SlingThrow()
        {
            var t = _status.Transform;
            for (int i = 0; i < _tamed.Count; i++)
            {
                var m = _tamed[i];
                if (m.Order != LeashOrder.Gather) continue;
                // The lab draws a seeded Gaussian per creature (sd 0.05 rad); here a deterministic golden-angle spread of
                // the same size, so no global random state is read (vessel rule 18).
                float th = (++_throwSerial) * AnglerCommandMath.GoldenAngle;
                Vector3 dir = (t.forward + (t.right * Mathf.Cos(th) + t.up * Mathf.Sin(th)) * 0.05f).normalized;
                m.Order = LeashOrder.Thrown;
                m.Homing = false;
                m.FlightTime = 0f;
                m.ThrowDirection = dir;
                m.Creature.LeashLaunch(dir * config.SlingSpeed);
            }
        }

        // ---- flights: homing throws land, plain throws latch ----

        void UpdateFlights(float dt)
        {
            for (int i = 0; i < _tamed.Count; i++)
            {
                var m = _tamed[i];
                if (m.Order != LeashOrder.Thrown) continue;
                m.FlightTime += dt;
                var c = m.Creature;
                if (m.Homing)
                {
                    // Its prism was eaten mid-flight: home on the next one in the order, or come home if none is left.
                    if (!m.Target || m.Target.destroyed) { PickTarget(m); if (m.Order == LeashOrder.Follow) continue; }
                    Vector3 to = m.Target.transform.position - c.transform.position;
                    if (to.magnitude < c.LeashMouthReach || m.FlightTime > config.SlingTime * 2f)
                    {
                        // The throw ends inside the mouth's reach: the creature lands at its own swim speed.
                        m.Order = LeashOrder.Attack;
                        m.Homing = false;
                        c.LeashLaunch((to.sqrMagnitude > 1e-6f ? to.normalized : c.transform.forward) * c.LeashTopSpeed);
                    }
                    continue;
                }
                if (TryLatch(m)) continue;
                if (m.FlightTime > config.SlingTime) ToFollow(m);
            }
        }

        bool TryLatch(Member m)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return false;
            Vector3 pos = m.Creature.transform.position;
            int found = index.QuerySphere(pos, config.SlingCatch, _query);
            Prism hit = null;
            float best = float.PositiveInfinity;
            var own = _status.Domain;
            for (int i = 0; i < found; i++)
            {
                var p = _query[i];
                if (!p || p.destroyed || (config.SpareOwn && p.Domain == own) || Fauna.IsShieldedMass(p)) continue;
                if (p is HealthPrism hp && hp.ResolveOwnerFauna()) continue;
                float d = (p.transform.position - pos).sqrMagnitude;
                if (d < best) { best = d; hit = p; }
            }
            if (!hit) return false;
            var cluster = new List<Prism>();
            found = index.QuerySphere(hit.transform.position, config.SlingCluster, _query);
            for (int i = 0; i < found; i++)
            {
                var p = _query[i];
                if (p && !p.destroyed && p.Domain == hit.Domain && !Fauna.IsShieldedMass(p)) cluster.Add(p);
            }
            ReleaseClaim(m);
            m.Set = cluster;
            m.Order = LeashOrder.Attack;
            PickTarget(m);
            return true;
        }

        /// <summary>An erased prism leaves every order it was in, and a creature heading for it picks again (the lab's
        /// <c>strikeFromOrders</c>).</summary>
        void StrikeFromOrders(Prism p)
        {
            for (int i = 0; i < _tamed.Count; i++)
            {
                var m = _tamed[i];
                m.Set?.Remove(p);
                if (m.Target != p) continue;
                if (m.Order == LeashOrder.Attack) PickTarget(m);
                else ReleaseClaim(m);
            }
        }

        // ---- the keeper: the Angler always leads at least one creature (the designer, 2026-10-16; ANGLER.md §1b) ----

        const float KeeperRetrySeconds = 1f;

        /// <summary>
        /// With the school empty, one creature is born behind the hull, already tamed (the lab's <c>ensureKeeper</c>). The
        /// species is the cell's own first leashable herbivore, else <see cref="AnglerConfigSO.CompanionFauna"/>; it is born
        /// through the platform's one fauna spawn seam (<see cref="CellLifeSpawnerBase.SpawnFaunaWithDomain"/>: the
        /// lineage bind, the replication seam, the bloom) in the cell's controlling colour, the only colour fauna spawn
        /// in. A replicated species is born only where the ecology is simulated. The school's first creature is then its
        /// keeper and does not starve (<see cref="Sustains"/>).
        /// </summary>
        void EnsureKeeper()
        {
            if (_tamed.Count > 0 || Time.time < _keeperRetryAt) return;
            _keeperRetryAt = Time.time + KeeperRetrySeconds;
            var t = _status.Transform;
            var cell = NearestCell(t.position);
            if (!cell) return;
            var cfg = KeeperSpecies(cell);
            if (!cfg) return;
            if (cfg.NetworkSynced && !FaunaNetworkSync.IsSimAuthority) return;   // a client never originates a replicated species

            Vector3 at = AnglerCommandMath.FormationSlot(0, t.position, t.forward, t.right, t.up, config.FormBack);
            if (cell.FaunaExclusionRadius > 0f || cell.FaunaContainmentRadius > 0f) at = cell.ClampToFaunaContainment(at, at);
            var f = CellLifeSpawnerBase.SpawnFaunaWithDomain(cell, cfg.FaunaPrefab, at, cell.ControllingDomain, at, cfg);
            if (!f || !f.TryLeash(this)) return;   // born wild: an ordinary creature of the cell from here on
            var m = new Member { Creature = f, Species = SpeciesOf(f), Phase = f.GetInstanceID() * 0.137f };
            _members[f] = m;
            JoinSchool(m);
        }

        FaunaConfigurationSO KeeperSpecies(Cell cell)
        {
            var profile = cell.Config ? cell.Config.SpawnProfile : null;
            if (profile)
                for (int i = 0; i < profile.SupportedFaunas.Count; i++)
                {
                    var c = profile.SupportedFaunas[i];
                    if (c && c.FaunaPrefab && c.FaunaPrefab.Leashable) return c;
                }
            return config.CompanionFauna && config.CompanionFauna.FaunaPrefab && config.CompanionFauna.FaunaPrefab.Leashable
                ? config.CompanionFauna : null;
        }

        static Cell NearestCell(Vector3 at)
        {
            Cell best = null;
            float bd = float.PositiveInfinity;
            var cells = Cell.ActiveCellsSnapshot;
            for (int i = 0; i < cells.Count; i++)
            {
                var c = cells[i];
                if (!c) continue;
                float d = (c.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        /// <summary>The keeper: the school's first creature (the one exception to starvation, <see cref="IFaunaLeash.Sustains"/>).</summary>
        public bool Sustains(Fauna creature) => _tamed.Count > 0 && _tamed[0].Creature == creature;

        // ---- orders: an attacker moves on through its set, and comes home when the set is gone ----

        void UpdateOrders()
        {
            for (int i = 0; i < _tamed.Count; i++)
            {
                var m = _tamed[i];
                if (m.Order != LeashOrder.Attack) continue;
                if (!m.Target || m.Target.destroyed) PickTarget(m);
            }
        }

        void PickTarget(Member m)
        {
            ReleaseClaim(m);
            _candidatePositions.Clear();
            _candidateClaims.Clear();
            _candidates.Clear();
            if (m.Set != null)
                for (int i = 0; i < m.Set.Count; i++)
                {
                    var p = m.Set[i];
                    if (!p || p.destroyed) continue;
                    _candidates.Add(p);
                    _candidatePositions.Add(p.transform.position);
                    _candidateClaims.Add(_claims.TryGetValue(p, out int n) ? n : 0);
                }
            int k = AnglerCommandMath.PickTarget(m.Creature.transform.position, _candidatePositions, _candidateClaims);
            if (k < 0) { ToFollow(m); return; }
            m.Target = _candidates[k];
            _claims[m.Target] = (_claims.TryGetValue(m.Target, out int c) ? c : 0) + 1;
        }

        void ReleaseClaim(Member m)
        {
            if (m.Target && _claims.TryGetValue(m.Target, out int c))
            {
                if (c <= 1) _claims.Remove(m.Target);
                else _claims[m.Target] = c - 1;
            }
            m.Target = null;
        }

        void ToFollow(Member m)
        {
            ReleaseClaim(m);
            m.Set = null;
            m.Homing = false;
            m.Order = LeashOrder.Follow;
        }

        // ---- the lure: wild creatures in the light are leashed, held, and tamed ----

        void UpdateLure(float dt)
        {
            var t = _status.Transform;
            Vector3 lure = AnglerCommandMath.LurePoint(t.position, t.forward, t.up, config.LureAhead);

            if (_luring)
            {
                float reach2 = config.LureRadius * config.LureRadius;
                var cells = Cell.ActiveCellsSnapshot;
                for (int ci = 0; ci < cells.Count; ci++)
                {
                    var cell = cells[ci];
                    if (!cell) continue;
                    var live = cell.LiveFauna;
                    for (int i = 0; i < live.Count; i++)
                    {
                        var f = live[i];
                        if (!f || f.IsLeashed || f.IsDying) continue;
                        if ((f.transform.position - lure).sqrMagnitude > reach2) continue;
                        if (!f.TryLeash(this)) continue;   // a predator, a puppet on this peer, or someone else's
                        var m = new Member { Creature = f, Order = LeashOrder.Lured, Species = SpeciesOf(f), Phase = f.GetInstanceID() * 0.137f };
                        if (_slipped.TryGetValue(f, out float left)) { m.Tame = left; _slipped.Remove(f); }
                        _members[f] = m;
                        _lured.Add(m);
                    }
                }
            }

            for (int i = _lured.Count - 1; i >= 0; i--)
            {
                var m = _lured[i];
                var f = m.Creature;
                float dL = Vector3.Distance(f.transform.position, lure);
                if (!_luring || dL > config.LureRadius * 1.3f)
                {
                    // Slipped out of the light: back to the wild, its taming fading.
                    _lured.RemoveAt(i);
                    _members.Remove(f);
                    f.Unleash(this);
                    if (m.Tame > 0f) _slipped[f] = m.Tame;
                    continue;
                }
                m.Tame = AnglerCommandMath.TameStep(m.Tame, dL < config.TameRadius, dt, config.TameTime, config.TameDecay);
                if (m.Tame >= 1f && _tamed.Count < config.SquadCap)
                {
                    _lured.RemoveAt(i);
                    JoinSchool(m);
                }
            }

            if (_slipped.Count > 0)
            {
                _scratch.Clear();
                foreach (var kv in _slipped) _scratch.Add(kv.Key);
                for (int i = 0; i < _scratch.Count; i++)
                {
                    var f = _scratch[i];
                    float left = f ? AnglerCommandMath.TameStep(_slipped[f], false, dt, config.TameTime, config.TameDecay) : 0f;
                    if (left <= 0f) _slipped.Remove(f);
                    else _slipped[f] = left;
                }
            }
        }

        // ------------------------------------------------------------------ IFaunaLeash

        public bool TrySteer(Fauna creature, out Vector3 goal, out float speed, out float turnRate)
        {
            goal = default;
            speed = 0f;
            turnRate = 0f;
            if (!config || _status == null || !_status.Transform || !creature || !_members.TryGetValue(creature, out var m)) return false;
            var t = _status.Transform;
            Vector3 pos = creature.transform.position;
            float top = creature.LeashTopSpeed, min = creature.LeashMinSpeed, hull = _status.Speed;
            bool dashes = creature.TryGetLeashDash(out float dash, out float tick);
            if (dashes) turnRate = config.TadTurn;
            switch (m.Order)
            {
                case LeashOrder.Lured:
                {
                    // Orbit the esca (the lab's lured steer): a little circle round the light, never faster than 1.2x top.
                    Vector3 lure = AnglerCommandMath.LurePoint(t.position, t.forward, t.up, config.LureAhead);
                    float a = Time.time * 1.3f + m.Phase;
                    goal = lure + new Vector3(Mathf.Cos(a), Mathf.Sin(a * 0.7f), Mathf.Sin(a)) * 6f;
                    speed = Mathf.Min(top * 1.2f, Mathf.Max(min, Vector3.Distance(pos, lure)));
                    return true;
                }
                case LeashOrder.Follow:
                {
                    goal = AnglerCommandMath.FormationSlot(m.Slot, t.position, t.forward, t.right, t.up, config.FormBack);
                    float gap = Vector3.Distance(goal, pos);
                    float cap = dashes ? AnglerCommandMath.TadFollowCap(top, dash, tick, gap, config.LeashTow)
                                       : AnglerCommandMath.LightFollowCap(top, 1f, config.LeashTow);
                    speed = AnglerCommandMath.FollowSpeed(hull, gap, min, cap);
                    return true;
                }
                case LeashOrder.Gather:
                {
                    goal = t.position + t.forward * config.SlingAhead;
                    speed = AnglerCommandMath.GatherSpeed(Vector3.Distance(goal, pos), min, top, hull);
                    return true;
                }
                case LeashOrder.Thrown:
                {
                    if (m.Homing && m.Target)
                    {
                        goal = m.Target.transform.position;
                        speed = config.SlingSpeed;
                        turnRate = config.HomeTurn;
                    }
                    else
                    {
                        goal = pos + m.ThrowDirection * 100f;
                        speed = config.SlingSpeed * Mathf.Exp(-0.25f * m.FlightTime);
                        turnRate = 30f;
                    }
                    return true;
                }
                case LeashOrder.Attack:
                {
                    if (!m.Target) return false;
                    goal = m.Target.transform.position;
                    speed = top;
                    return true;
                }
            }
            return false;
        }

        public Prism OrderedMeal(Fauna creature) =>
            creature && _members.TryGetValue(creature, out var m) && m.Order == LeashOrder.Attack ? m.Target : null;

        public void Released(Fauna creature)
        {
            if (!_members.TryGetValue(creature, out var m)) return;
            ReleaseClaim(m);
            _members.Remove(creature);
            _tamed.Remove(m);
            _lured.Remove(m);
            _slipped.Remove(creature);
        }

        // ------------------------------------------------------------------ bookkeeping

        bool InSquad(Member m) => _squad < 0 || _squad >= _species.Count || m.Species == _species[_squad];

        int Count(LeashOrder order)
        {
            int n = 0;
            for (int i = 0; i < _tamed.Count; i++) if (_tamed[i].Order == order && InSquad(_tamed[i])) n++;
            return n;
        }

        /// <summary>A kind's squad name: its prefab's name ("Clawfish", "QuadFish", "TadPoleFauna"), so the four element
        /// configs of one species are one squad.</summary>
        static string SpeciesOf(Fauna f)
        {
            var config = f.SourceConfig;
            string n = config && config.FaunaPrefab ? config.FaunaPrefab.name : f.name.Replace("(Clone)", "").Trim();
            return string.IsNullOrEmpty(n) ? "Fauna" : n;
        }

        void AssignSlots()
        {
            int slot = 0;
            for (int i = 0; i < _tamed.Count; i++)
                if (_tamed[i].Order == LeashOrder.Follow) _tamed[i].Slot = slot++;
        }

        void PruneDead()
        {
            _memberScratch.Clear();
            foreach (var kv in _members)
                if (!kv.Key || kv.Key.IsDying) _memberScratch.Add(kv.Value);
            for (int i = 0; i < _memberScratch.Count; i++)
            {
                var m = _memberScratch[i];
                ReleaseClaim(m);
                _tamed.Remove(m);
                _lured.Remove(m);
                if (m.Creature) m.Creature.Unleash(this);
                _members.Remove(m.Creature);
            }
            if (_painted.Count > 0 && _painted.Exists(p => !p || p.destroyed))
            {
                _painted.RemoveAll(p => !p || p.destroyed);
                _paintedSet.RemoveWhere(p => !p || p.destroyed);
            }
            if (_ordered.Count > 0) _ordered.RemoveWhere(p => !p || p.destroyed);   // eaten: its lit mark goes with it
            if (_species.Count > 0 && _squad >= _species.Count) _squad = -1;
        }

        void ReleaseAll()
        {
            foreach (var kv in _members)
                if (kv.Key) kv.Key.Unleash(this);
            _members.Clear();
            _tamed.Clear();
            _lured.Clear();
            _slipped.Clear();
            _claims.Clear();
            for (int i = 0; i < _painted.Count; i++) Unlight(_painted[i], markFade);
            foreach (var p in _ordered) Unlight(p, markFade);
            _painted.Clear();
            _paintedSet.Clear();
            _ordered.Clear();
        }

        /// <summary>Only the pilot sees the paint (the marks are the pilot's instrument, and a rival's would be noise).</summary>
        bool ShowsMarks => _status != null && !_status.IsInitializedAsAI && _status.IsLocalUser;

        /// <summary>Back to the prism's own colour, if this machine lit it.</summary>
        static void Unlight(Prism p, float fade)
        {
            if (p && !p.destroyed && p.ColorLift > 0f) p.SetColorLift(0f, fade);
        }

        // ------------------------------------------------------------------ presentation

        void DrawMarks()
        {
            if (!ShowsMarks) return;
            var cam = ResolveCamera();
            if (!cam) return;
            _marks ??= new AnglerWorldMarks();
            if (!_marks.Begin(cam)) return;
            var t = _status.Transform;
            Vector3 lure = AnglerCommandMath.LurePoint(t.position, t.forward, t.up, config.LureAhead);
            _marks.Dot(lure, escaColor, _luring ? 2.4f : 1.2f, 6f);
            if (_luring) _marks.Ring(lure, t.right, t.up, config.TameRadius, 24, escaColor * new Color(1f, 1f, 1f, 0.6f), 0.6f, 2f);
            for (int i = 0; i < _lured.Count; i++)
            {
                var c = _lured[i].Creature;
                if (c) _marks.Dot(c.transform.position + t.up * 3f, Color.Lerp(tamingColor, escaColor, _lured[i].Tame), 1.2f, 4f);
            }
            // Painted prisms are not dotted here: they are drawn LIT (Prism.SetColorLift, BrushStroke). A dot at a prism's
            // centre sat inside the prism and was never seen.
            bool erasing = _eraseDepth > 0.5f;
            if (_cmdHeld || erasing)
            {
                // The brush's rim at its reach, so the pilot sees what the beam covers.
                float cone = erasing ? config.EraseCone * AnglerCommandMath.BrushScale(_eraseDepth, config.BrushMin)
                                     : config.PaintCone * AnglerCommandMath.BrushScale(_cmdDepth, config.BrushMin);
                float r = Mathf.Tan(cone * Mathf.Deg2Rad) * config.PaintRange;
                _marks.Ring(t.position + t.forward * config.PaintRange, t.right, t.up, r, 32, erasing ? eraseColor : paintColor, 1f, 3f);
            }
            if (_cmdHeld) _marks.Dot(t.position + t.forward * config.SlingAhead, paintColor, 1.4f, 5f);
            _marks.End();
        }

        static Camera ResolveCamera()
        {
            var manager = CameraManager.Instance;
            if (manager && manager.GetActiveController() is CustomCameraController controller && controller.Camera)
                return controller.Camera;
            return Camera.main;
        }
    }
}
