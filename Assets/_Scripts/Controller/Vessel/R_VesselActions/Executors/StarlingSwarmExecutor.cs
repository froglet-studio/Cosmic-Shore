using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Starlings: a passive swarm card vessel (design: <c>R_VesselActions/STARLING.md</c>, <c>Docs/Studios/SWARM_CARD_VESSEL.md</c>;
    /// played first in the Starlings Lab, <c>Docs/Studios/STARLINGS_LAB.md</c>). The pilot never flies anything. The swarm of starlings
    /// roams the cell on its own after the objective of the moment, the phase cycle (<see cref="StarlingRules"/>) moves the
    /// objective on, and the pilot turns the viewing plane and picks one card from each hand that is dealt. The cards are
    /// the build: every rule below that is not the bare swarm is a card, and every number is the card's own at its held
    /// level (<see cref="StarlingCardDeckSO"/>, generated from <c>cards.json</c>).
    ///
    /// <para><b>This is a port of the lab's <c>simStep</c></b>, the same order and the same numbers, with the lab's
    /// stand-ins swapped for the game's own things: its crystals are the cell's crystals (collected through this vessel's
    /// own skimmer and vessel impactor, so every crystal effect, score and fusion runs as for a hull that flew into them),
    /// its enemy prisms are the arena's prisms of another domain (<see cref="PrismSpatialIndex"/>), its rivals are the
    /// match's other vessels, its fauna are the cell's fauna. Members are vessel agents like the Termite's drones, not
    /// lifeforms: they have no ecology and are drawn from this hull's own model.</para>
    ///
    /// <para><b>PvP is petals only (LOCKED).</b> Every Charge effect and every nip on a rival vessel is a scored combat hit
    /// that takes petals (<see cref="CombatHitDrain"/>), and that is all it does. The lab's EMP "silence" is a scored
    /// Debuff hit here (nothing silences a vessel in the game), and Short Circuit's "crystals knocked loose" is the same hit
    /// class, which ejects the petals as crystals. No card stuns or takes control of a vessel.</para>
    ///
    /// <para><b>Who decides.</b> Every peer moves the members (they are a picture), but only the machine that decides this
    /// vessel's hits (<see cref="ElementalTransfer.IsDecidedHere"/>) touches the world, ticks the phases and deals hands.
    /// The picks are replicated presses, so they reach every peer; a peer that has no hand ignores them. The swarm is
    /// built for single player first; a networked Starling shows its owner's results, not a replicated swarm.</para>
    /// </summary>
    public sealed class StarlingSwarmExecutor : ShipActionExecutorBase
    {
        [Header("Wiring (serialized on the prefab so a missing wire shows in the inspector)")]
        [Tooltip("Every number the Starlings reads (the Starlings Lab's SHIPPED, then the game's own), and the card deck.")]
        [SerializeField] StarlingConfigSO config;
        [Tooltip("The fleet's shared turn-end channel: the run starts over.")]
        [SerializeField] ScriptableEventNoParam OnMiniGameTurnEnd;
        [Tooltip("Drag Event_CombatHitStats.asset - every nip, sting, zap and EMP on a rival is a scored hit (PvP is petals only).")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;
        [Tooltip("The carrier hull's visual (StarlingHull). Hidden while the swarm flies: the carrier is the view, not a body.")]
        [SerializeField] Transform hullVisual;
        [Tooltip("The starling model under the hull (Starling.fbx). Each member is a copy of it.")]
        [SerializeField] Transform memberModel;
        [Tooltip("The carrier's own colliders (its body and hit box). Switched off: nothing should hit an invisible box.")]
        [SerializeField] Collider[] carrierColliders = System.Array.Empty<Collider>();

        /// <summary>A member of the swarm: the lab's <c>{ p, v, shells, shellT, alive, sw, laserT }</c> and its picture.</summary>
        sealed class Member
        {
            public Vector3 P, V;
            public int Shells, Group;
            public float ShellT, LaserT;
            public Transform View;
            public SkinnedMeshRenderer Renderer;
        }

        /// <summary>What the swarm has done to one rival: its share of a petal still owed, and the per-rival timers.</summary>
        sealed class Rival
        {
            public float Nip, Sting, ShortCircuitT;
        }

        enum Form { None = 0, Dragonfly = 1, Snowball = 2 }

        const int MaxGroups = 4;
        const float VesselScanInterval = 1f, FaunaScanInterval = 1f, CellScanInterval = 2f, MaterialSyncInterval = 1f;
        const float ChainLightningReach = 120f, TeslaReach = 300f, ShortCircuitReach = 40f, DangerReach = 14f;
        const float EmpEvery = 20f, DragonflyEvery = 15f, SnowballEvery = 20f, SplitEvery = 30f, ShortCircuitRest = 3f;
        const int BlackHoleMinPrisms = 10;

        IVesselStatus _status;
        StarlingVesselTransformer _transformer;
        StarlingRules _rules;
        VesselImpactor _vesselImpactor;
        SkimmerImpactor _skimmerImpactor;
        Transform _swarmRoot;
        SkinnedMeshRenderer _modelRenderer;

        readonly List<Member> _members = new();
        readonly Stack<Member> _pool = new();
        readonly List<IVesselStatus> _rivals = new();
        readonly Dictionary<IVesselStatus, Rival> _rivalState = new();
        readonly List<Fauna> _fauna = new();
        readonly Dictionary<Crystal, CrystalImpactor> _crystalImpactor = new();
        readonly List<Prism> _prismHits = new();
        readonly Dictionary<Prism, int> _bites = new();
        readonly List<Prism> _snowballPrisms = new();
        readonly Dictionary<string, float> _cooldowns = new();
        readonly Vector3[] _centroid = new Vector3[MaxGroups];
        readonly Vector3[] _target = new Vector3[MaxGroups];
        readonly int[] _groupSize = new int[MaxGroups];
        readonly Vector3[] _sum = new Vector3[MaxGroups];

        // The separation grid: members bucketed into SeparationReach-sized cells, so each member checks its 27 cells instead
        // of the whole swarm (a murmuration is 120-230 members; the all-pairs loop was 24-46 when it was written).
        const float SeparationReach = 20f;
        readonly Dictionary<long, int> _cellHead = new();
        Vector3[] _snap = System.Array.Empty<Vector3>();
        int[] _next = System.Array.Empty<int>();

        bool _started;
        uint _seed, _draws;
        float _contactOwed, _vesselScanAt, _faunaScanAt, _cellScanAt, _materialSyncAt, _aiPickAt;
        Vector3 _cellCenter, _swarmCentroid, _wander;
        float _cellRadius;
        Form _form;
        float _formT, _splitT;
        bool _blackHole;
        Vector3 _blackHoleAt;
        float _blackHoleT;
        Prism _massTarget;
        Material[] _memberMaterials;
        bool _warnedNoChannel;

        // ---- what the HUD reads ----

        /// <summary>The phase cycle and the hand (null until the vessel is initialized).</summary>
        public StarlingRules Rules => _rules;
        public int MemberCount => _members.Count;
        public StarlingPhase Phase => _rules?.Phase ?? StarlingPhase.Omnicrystal;
        public StarlingConfigSO Config => config;

        /// <summary>The element whose card details the pilot is holding open (a trigger held, or a long press), else None.</summary>
        public Element DetailsElement => _press.Details;

        /// <summary>Raised on every peer when a pick lands: the element picked.</summary>
        public event System.Action<Element> Picked;

        /// <summary>Raised when the pilot sets the element the next hand is drawn from.</summary>
        public event System.Action<Element> DrawElementChosen;

        readonly StarlingPickPress _press = new();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _transformer = _status != null ? _status.VesselTransformer as StarlingVesselTransformer : null;
            if (!config)
            {
                CSDebug.LogError($"[Starling] {name}: StarlingSwarmExecutor has no StarlingConfig - the swarm cannot run. Wire " +
                                 "Assets/_SO_Assets/VesselActions/Starling/StarlingConfig.asset.");
                return;
            }
            if (!config.Deck)
                CSDebug.LogError($"[Starling] {name}: StarlingConfig has no card deck, so no hand can be dealt. Wire StarlingCardDeck.asset " +
                                 "(Tools/Build/author_starling_assets.py generates it).");
            if (_status != null && !_transformer)
                CSDebug.LogWarning($"[Starling] {name}: the vessel's transformer is not a StarlingVesselTransformer, so the view " +
                                   "will not follow the swarm.");
            if (_transformer) _transformer.Configure(config);
            _rules ??= new StarlingRules(config);
            _vesselImpactor = _status?.Transform ? _status.Transform.GetComponentInChildren<VesselImpactor>(true) : null;
            var skimmer = _status?.NearFieldSkimmer;
            _skimmerImpactor = skimmer ? skimmer.GetComponentInChildren<SkimmerImpactor>(true) : null;
            StopSwarm();
        }

        void OnEnable() => OnMiniGameTurnEnd.OnRaised += OnTurnEnd;

        void OnDisable()
        {
            OnMiniGameTurnEnd.OnRaised -= OnTurnEnd;
            StopSwarm();
        }

        void OnTurnEnd() => StopSwarm();

        /// <summary>A pick button went down (A = Mass, B = Time, X = Charge, Y = Space). Nothing is picked yet: a quick
        /// release picks, a long hold or a pulled trigger shows the card's details instead (<see cref="StarlingPickPress"/>).</summary>
        public void PressPick(Element element) => _press.Begin(element, Time.unscaledTime, TriggerHeld());

        /// <summary>
        /// The pick button came up. Unless its details were shown, a quick press means (Garrett, 2026-11-01):
        /// <list type="bullet">
        /// <item>a spread hand up: pick that element's card, as before;</item>
        /// <item>an element hand up: X (Charge) and B (Time) move the highlight left and right, A (Mass) takes the
        /// highlighted card (<see cref="ElementHandButton"/>);</item>
        /// <item>no hand up: draw the next hand from that element (it keeps the old one until you choose).</item>
        /// </list>
        /// </summary>
        public void ReleasePick(Element element)
        {
            if (!_press.End(element) || _rules == null) return;
            if (!_rules.HasHand) ChooseDrawElement(element);
            else if (!_rules.IsElementHand) Pick(element);
            else
            {
                int step = ElementHandButton(element);
                if (step == 0) PickFocus();
                else if (step != int.MinValue) _rules.MoveFocus(step);
            }
        }

        /// <summary>What a pick button does in an element hand: -1 / +1 move the highlight (X = Charge left, B = Time
        /// right, the pad's west and east), 0 takes the highlighted card (A = Mass, south), and Y (Space) does nothing
        /// (<see cref="int.MinValue"/>).</summary>
        public static int ElementHandButton(Element element) => element switch
        {
            Element.Charge => -1,
            Element.Time => 1,
            Element.Mass => 0,
            _ => int.MinValue,
        };

        bool TriggerHeld()
        {
            var input = _status?.Player != null ? _status.InputStatus : null;
            float threshold = config ? config.DetailsTrigger : 0.5f;
            return input != null && Mathf.Max(input.LeftTriggerAnalog, input.RightTriggerAnalog) >= threshold;
        }

        /// <summary>Pick the spread hand's card of <paramref name="element"/> (A = Mass, B = Time, X = Charge, Y = Space).</summary>
        public void Pick(Element element)
        {
            if (_rules == null || !_rules.Pick(element)) return;
            Picked?.Invoke(element);
        }

        /// <summary>Take card <paramref name="index"/> of the hand up now (a tap on the HUD's hand row).</summary>
        public void PickAt(int index)
        {
            if (_rules == null) return;
            var card = _rules.HandCard(index);
            if (card.IsEmpty || !_rules.PickAt(index)) return;
            Picked?.Invoke(card.Card.Element);
        }

        /// <summary>Take the highlighted card of an element hand.</summary>
        public void PickFocus()
        {
            if (_rules != null && _rules.IsElementHand) PickAt(_rules.Focus);
        }

        /// <summary>Draw the next hand from <paramref name="element"/> (only with no hand up).</summary>
        public void ChooseDrawElement(Element element)
        {
            if (_rules == null || !_rules.ChooseDrawElement(element)) return;
            DrawElementChosen?.Invoke(element);
        }

        // =============================================================================================== the run

        void StartSwarm()
        {
            _started = true;
            _seed = (uint)(_status?.PlayerName?.GetHashCode() ?? 7) ^ 0x6B72696Cu;
            _draws = 0;
            _rules.Reset(_seed);
            _cooldowns.Clear();
            _form = Form.None;
            _formT = _splitT = 0f;
            _blackHole = false;
            _massTarget = null;
            _contactOwed = 0f;
            _vesselScanAt = _faunaScanAt = _cellScanAt = _materialSyncAt = -1f;
            _aiPickAt = 0f;

            if (hullVisual) hullVisual.gameObject.SetActive(false);
            foreach (var c in carrierColliders) if (c) c.enabled = false;
            if (!_swarmRoot)
            {
                _swarmRoot = new GameObject("StarlingSwarm").transform;
                _swarmRoot.SetParent(transform, false);
            }
            if (memberModel) _modelRenderer = memberModel.GetComponentInChildren<SkinnedMeshRenderer>(true);

            Vector3 at = _status?.Transform ? _status.Transform.position : transform.position;
            RefreshCell(at, true);
            for (int i = 0; i < config.StartSize; i++) AddMember(at + InBall(60f));
            _swarmCentroid = at;
        }

        void StopSwarm()
        {
            for (int i = _members.Count - 1; i >= 0; i--) Retire(_members[i]);
            _members.Clear();
            _rivalState.Clear();
            _bites.Clear();
            _snowballPrisms.Clear();
            _started = false;
            if (hullVisual) hullVisual.gameObject.SetActive(true);
            foreach (var c in carrierColliders) if (c) c.enabled = true;
            if (_transformer) _transformer.FollowTarget = null;
            _press.Clear();
        }

        void AddMember(Vector3 p)
        {
            var m = _pool.Count > 0 ? _pool.Pop() : new Member();
            m.P = p;
            m.V = new Vector3(Rnd() - 0.5f, Rnd() - 0.5f, Rnd() - 0.5f) * 40f;
            m.Shells = 0;
            m.ShellT = 0f;
            m.LaserT = Rnd();
            m.Group = 0;
            if (!m.View && memberModel && _swarmRoot)
            {
                var copy = Instantiate(memberModel.gameObject, _swarmRoot);
                copy.name = "Starling";
                m.View = copy.transform;
                m.View.localScale = memberModel.localScale * config.MemberScale;
                m.Renderer = copy.GetComponentInChildren<SkinnedMeshRenderer>(true);
                if (m.Renderer)
                {
                    m.Renderer.updateWhenOffscreen = true;
                    if (_memberMaterials != null) m.Renderer.sharedMaterials = _memberMaterials;
                }
            }
            if (m.View)
            {
                m.View.gameObject.SetActive(true);
                m.View.position = p;
            }
            _members.Add(m);
        }

        /// <summary>Snapshots every member's position at the start of the step and buckets it into the separation grid.</summary>
        void BuildSeparationGrid(int n)
        {
            if (_snap.Length < n)
            {
                _snap = new Vector3[Mathf.NextPowerOfTwo(n)];
                _next = new int[_snap.Length];
            }
            _cellHead.Clear();
            for (int i = 0; i < n; i++)
            {
                Vector3 p = _members[i].P;
                _snap[i] = p;
                long key = CellKey(GridCell(p.x), GridCell(p.y), GridCell(p.z));
                _next[i] = _cellHead.TryGetValue(key, out int head) ? head : -1;
                _cellHead[key] = i;
            }
        }

        /// <summary>Member <paramref name="i"/>'s push away from same-group members closer than SeparationReach.</summary>
        Vector3 Separation(int i, int group, float push)
        {
            Vector3 p = _snap[i], sum = Vector3.zero;
            int cx = GridCell(p.x), cy = GridCell(p.y), cz = GridCell(p.z);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!_cellHead.TryGetValue(CellKey(cx + dx, cy + dy, cz + dz), out int j)) continue;
                for (; j >= 0; j = _next[j])
                {
                    if (j == i || _members[j].Group != group) continue;
                    Vector3 away = p - _snap[j];
                    float dd = away.sqrMagnitude;
                    if (dd < SeparationReach * SeparationReach && dd > 0f) sum += away * (push / Mathf.Sqrt(dd));
                }
            }
            return sum;
        }

        static int GridCell(float v) => Mathf.FloorToInt(v / SeparationReach);

        /// <summary>A cell's exact key (21 bits an axis, so no two cells within a million cells of the origin share one).</summary>
        static long CellKey(int x, int y, int z) =>
            ((long)(x & 0x1FFFFF) << 42) | ((long)(y & 0x1FFFFF) << 21) | (uint)(z & 0x1FFFFF);

        void Retire(Member m)
        {
            if (m.View) m.View.gameObject.SetActive(false);
            _pool.Push(m);
        }

        void Kill(int index)
        {
            Retire(_members[index]);
            _members.RemoveAt(index);
        }

        bool Decides => _status != null && ElementalTransfer.IsDecidedHere(_status) && !(_status.Player is { IsNetworkOwner: false });

        void Update()
        {
            if (_status == null || !config || _rules == null || !config.Deck) return;
            if (!_started) StartSwarm();
            if (config) _press.Tick(Time.unscaledTime, TriggerHeld(), config.DetailsHoldTime);
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            bool decides = Decides;
            float now = Time.time;
            if (now >= _vesselScanAt) { _vesselScanAt = now + VesselScanInterval; ScanVessels(); }
            if (now >= _faunaScanAt) { _faunaScanAt = now + FaunaScanInterval; ScanFauna(); }
            if (now >= _cellScanAt) { _cellScanAt = now + CellScanInterval; RefreshCell(_swarmCentroid, false); }

            Steer(dt);

            if (decides)
            {
                float step = 1f / config.ContactHz;
                _contactOwed = Mathf.Min(_contactOwed + dt, step * 3f);
                while (_contactOwed >= step)
                {
                    _contactOwed -= step;
                    Contacts(step);
                    Cards(step);
                }
                _rules.Tick(dt);
                AutoPickForAI(now);
            }

            Draw(now);
            if (_transformer) _transformer.FollowTarget = _swarmCentroid;
        }

        // =============================================================================================== the world

        void ScanVessels()
        {
            _rivals.Clear();
            foreach (var v in FindObjectsByType<VesselStatus>(FindObjectsSortMode.None))
            {
                IVesselStatus rival = v;
                if (!v || ReferenceEquals(rival, _status) || rival.Player == null) continue;
                if (rival.Domain == _status.Domain) continue;
                _rivals.Add(rival);
            }
        }

        void ScanFauna()
        {
            _fauna.Clear();
            foreach (var f in FindObjectsByType<Fauna>(FindObjectsSortMode.None))
                if (f && f.isActiveAndEnabled) _fauna.Add(f);
        }

        void RefreshCell(Vector3 near, bool force)
        {
            var cell = Cell.FindNearestActiveCell(near);
            if (cell)
            {
                _cellCenter = cell.transform.position;
                float r = cell.MembraneRadius;
                _cellRadius = r > 1f ? r : config.FallbackCellRadius;
            }
            else if (force)
            {
                _cellCenter = near;
                _cellRadius = config.FallbackCellRadius;
            }
        }

        // =============================================================================================== movement

        /// <summary>The lab's split, targets, Skip Ahead and member steering (its <c>simStep</c> up to the contacts).</summary>
        void Steer(float dt)
        {
            int n = _members.Count;
            if (n == 0) return;

            // ---- forms (one at a time): Dragonfly, Snowball ----
            if (_form != Form.None)
            {
                _formT -= dt;
                if (_formT <= 0f)
                {
                    if (_form == Form.Snowball && _rules.Level(StarlingCardIds.Snowball) >= 10)
                        foreach (var q in _snowballPrisms) if (q && !q.destroyed) q.ActivateShield();
                    _snowballPrisms.Clear();
                    _form = Form.None;
                }
            }
            if (_form == Form.None && _rules.Has(StarlingCardIds.Dragonfly) && Cooldown("dragonfly", DragonflyEvery, dt))
            {
                _form = Form.Dragonfly;
                _formT = _rules.Value(StarlingCardIds.Dragonfly, 1);
            }
            else if (_form == Form.None && _rules.Has(StarlingCardIds.Snowball) && Cooldown("snowball", SnowballEvery, dt))
            {
                _form = Form.Snowball;
                _formT = _rules.Value(StarlingCardIds.Snowball, 0);
            }
            float dart = _form == Form.Dragonfly ? _rules.Value(StarlingCardIds.Dragonfly, 0) : 1f;

            // ---- split ----
            int splitLevel = _rules.Level(StarlingCardIds.Split);
            if (splitLevel > 0 && splitLevel < 5)
            {
                if (_splitT > 0f) _splitT -= dt;
                else if (Cooldown("split", SplitEvery, dt)) _splitT = 8f + 4f * (splitLevel - 1);
            }
            int groups = Mathf.Clamp(_rules.SwarmCount(_splitT > 0f), 1, MaxGroups);
            for (int s = 0; s < MaxGroups; s++) { _sum[s] = Vector3.zero; _groupSize[s] = 0; }
            // A SAMPLE of members per group, as the lab's centroidOf: at most 12, evenly strided.
            for (int i = 0; i < n; i++) _members[i].Group = i % groups;
            for (int s = 0; s < groups; s++)
            {
                int size = (n - s + groups - 1) / groups;
                int stride = Mathf.Max(1, size / 12);
                for (int k = 0, i = s; i < n; i += groups, k++)
                {
                    if (k % stride != 0) continue;
                    _sum[s] += _members[i].P;
                    _groupSize[s]++;
                }
                _centroid[s] = _groupSize[s] > 0 ? _sum[s] / _groupSize[s] : _swarmCentroid;
                _target[s] = TargetFor((StarlingPhase)(((int)_rules.Phase + s) % 4), _centroid[s]);
            }
            {
                int stride = Mathf.Max(1, n / 12), k = 0;
                Vector3 c = Vector3.zero;
                for (int i = 0; i < n; i += stride) { c += _members[i].P; k++; }
                _swarmCentroid = c / Mathf.Max(1, k);
            }

            // ---- Skip Ahead: the swarm skips a moment of travel toward its objective ----
            if (_rules.Has(StarlingCardIds.SkipAhead) && Cooldown("skip", _rules.Value(StarlingCardIds.SkipAhead, 0), dt))
            {
                float dist = _rules.Value(StarlingCardIds.SkipAhead, 1);
                for (int s = 0; s < groups; s++)
                {
                    Vector3 dv = _target[s] - _centroid[s];
                    float l = dv.magnitude;
                    if (l < 30f) continue;
                    Vector3 hop = dv * (Mathf.Min(dist, l - 20f) / l);
                    foreach (var m in _members) if (m.Group == s) m.P += hop;
                }
            }

            // ---- members move: seek the objective, flock, wander; turn-rate limited ----
            float quick = _rules.ValueOr(StarlingCardIds.Quickening, 0, 0f) / 100f;
            float turn = Mathf.Max(config.Turn, _rules.ValueOr(StarlingCardIds.Agility, 0, 0f)) * Mathf.Deg2Rad;
            bool sting = _rules.Has(StarlingCardIds.Sting);
            float stingReach = sting ? _rules.Value(StarlingCardIds.Sting, 1) : 0f;
            float tight = _form == Form.Snowball ? 3f : _form == Form.Dragonfly ? 1.5f : 0.35f;
            float push = _form == Form.Snowball ? 0.3f : 1f;
            float maxA = turn * dt * (_form != Form.None ? 2f : 1f);
            float edge = _cellRadius * 0.95f;
            BuildSeparationGrid(n);
            for (int i = 0; i < n; i++)
            {
                var m = _members[i];
                Vector3 goal = _target[m.Group];
                if (sting && NearestRival(m.P, stingReach, out var prey)) goal = prey.Transform.position;
                Vector3 toGoal = goal - m.P;
                float lg = Mathf.Max(toGoal.magnitude, 1e-3f);
                Vector3 want = toGoal / lg;
                Vector3 toC = _centroid[m.Group] - m.P;
                float lc = Mathf.Max(toC.magnitude, 1e-3f);
                want += toC / lc * (tight * Mathf.Min(1f, lc / 80f));
                want += Separation(i, m.Group, push);
                want += new Vector3(Rnd() - 0.5f, Rnd() - 0.5f, Rnd() - 0.5f) * 0.6f;
                Vector3 fromCell = m.P - _cellCenter;
                float r0 = fromCell.magnitude;
                if (r0 > edge) want -= fromCell / r0 * 2f;
                float arrive = Mathf.Min(1f, 0.35f + lg / 150f);   // members ease in at the objective instead of overshooting
                float speed = config.Cruise * (1f + quick) * dart * arrive;
                Vector3 dir = want.sqrMagnitude > 1e-8f ? want.normalized : m.V.normalized;
                Vector3 cur = m.V.sqrMagnitude > 1e-8f ? m.V.normalized : dir;
                float ang = Mathf.Acos(Mathf.Clamp(Vector3.Dot(cur, dir), -1f, 1f));
                Vector3 nd = dir;
                if (ang > maxA)
                {
                    float t = maxA / ang;
                    nd = (cur + (dir - cur) * t).normalized;
                    if (nd.sqrMagnitude < 1e-8f) nd = dir;
                }
                m.V = nd * speed;
                m.P += m.V * dt;
            }
        }

        Vector3 TargetFor(StarlingPhase phase, Vector3 at)
        {
            switch (phase)
            {
                case StarlingPhase.Omnicrystal:
                    return NearestCrystal(at, omni: true, out var omni) ? omni.transform.position : Wander(at);
                case StarlingPhase.ElementalCrystals:
                    return NearestCrystal(at, omni: false, out var crystal) ? crystal.transform.position : Wander(at);
                case StarlingPhase.OpposingVessels:
                    return NearestRival(at, float.PositiveInfinity, out var rival) ? rival.Transform.position : Wander(at);
                default:
                    if (!_massTarget || _massTarget.destroyed || _massTarget.Domain == _status.Domain) _massTarget = NearestEnemyPrism(at);
                    return _massTarget ? _massTarget.transform.position : Wander(at);
            }
        }

        /// <summary>No objective in the cell: drift to a wander point, picked anew when reached.</summary>
        Vector3 Wander(Vector3 at)
        {
            if ((_wander - at).sqrMagnitude < 60f * 60f || (_wander - _cellCenter).sqrMagnitude > _cellRadius * _cellRadius)
                _wander = _cellCenter + InBall(_cellRadius * 0.8f);
            return _wander;
        }

        bool NearestCrystal(Vector3 at, bool omni, out Crystal best)
        {
            best = null;
            float bd = float.PositiveInfinity;
            var active = Crystal.Active;
            for (int i = 0; i < active.Count; i++)
            {
                var c = active[i];
                if (!Collectable(c, omni)) continue;
                float d = (c.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        bool Collectable(Crystal c, bool omni)
        {
            if (!c || !c.isActiveAndEnabled || c.IsExploding || c.IsEmbedded) return false;
            if (!c.CanBeCollected(_status.Domain)) return false;
            var impactor = ImpactorOf(c);
            if (!impactor) return false;
            if (omni) return impactor is OmniCrystalImpactor;
            return impactor is ElementalCrystalImpactor e && !e.HasBeenCollected;
        }

        CrystalImpactor ImpactorOf(Crystal c)
        {
            if (_crystalImpactor.TryGetValue(c, out var impactor) && impactor) return impactor;
            impactor = c.GetComponentInChildren<CrystalImpactor>(true);
            _crystalImpactor[c] = impactor;
            return impactor;
        }

        bool NearestRival(Vector3 at, float reach, out IVesselStatus best)
        {
            best = null;
            float bd = reach * reach;
            foreach (var r in _rivals)
            {
                if (!Alive(r)) continue;
                float d = (r.Transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; best = r; }
            }
            return best != null;
        }

        Prism NearestEnemyPrism(Vector3 at)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null) return null;
            for (float radius = 150f; radius <= _cellRadius * 2f; radius *= 2f)
            {
                index.QuerySphere(at, radius, _prismHits);
                Prism best = null;
                float bd = float.PositiveInfinity;
                foreach (var q in _prismHits)
                {
                    if (!IsEnemyPrism(q)) continue;
                    float d = (q.transform.position - at).sqrMagnitude;
                    if (d < bd) { bd = d; best = q; }
                }
                if (best) return best;
            }
            return null;
        }

        bool IsEnemyPrism(Prism q) => q && !q.destroyed && q is not HealthPrism && q.Domain != _status.Domain;

        // =============================================================================================== contacts

        /// <summary>The lab's contacts: crystals, the omnicrystal, enemy prisms, rivals, and danger prisms on members.</summary>
        void Contacts(float dt)
        {
            int n = _members.Count;
            if (n == 0) return;
            bool split = _rules.SwarmCount(_splitT > 0f) > 1;
            float crystalReach2 = config.CrystalReach * config.CrystalReach;
            float omniReach2 = config.OmniReach * config.OmniReach;
            float rivalReach2 = config.RivalReach * config.RivalReach;
            float reach = config.PrismReach * (_rules.Has(StarlingCardIds.WideBite) ? _rules.Value(StarlingCardIds.WideBite, 0) / 100f : 1f);
            float holeRate = _rules.ValueOr(StarlingCardIds.HolePunch, 0, 0f);
            float stealRate = _rules.ValueOr(StarlingCardIds.Annex, 0, 0f);
            int bitesNeed = _rules.Has(StarlingCardIds.ShieldEaters) ? Mathf.Max(1, (int)_rules.Value(StarlingCardIds.ShieldEaters, 0)) : int.MaxValue;
            bool sting = _rules.Has(StarlingCardIds.Sting);
            int stingNeed = sting ? Mathf.Max(1, (int)_rules.Value(StarlingCardIds.Sting, 0)) : 0;
            var index = PrismSpatialIndex.Instance;
            var active = Crystal.Active;

            for (int i = n - 1; i >= 0; i--)
            {
                if (i >= _members.Count) continue;
                var m = _members[i];

                // crystals: an elemental crystal hatches one starling; the omnicrystal ends its phase or deals a hand
                for (int c = active.Count - 1; c >= 0; c--)
                {
                    if (c >= active.Count) continue;
                    var crystal = active[c];
                    if (!crystal) continue;
                    float d = (crystal.transform.position - m.P).sqrMagnitude;
                    if (d > crystalReach2) continue;
                    var impactor = ImpactorOf(crystal);
                    if (impactor is ElementalCrystalImpactor elemental && Collectable(crystal, false))
                    {
                        if (_skimmerImpactor && elemental.CollectBy(_skimmerImpactor))
                        {
                            _rules.OnElementalCollected(split);
                            if (_members.Count < _rules.CapNow) AddMember(crystal.transform.position);
                        }
                    }
                    else if (impactor is OmniCrystalImpactor omni && d <= omniReach2 && Collectable(crystal, true) && _vesselImpactor)
                    {
                        // The two halves a physics contact would dispatch (CrystalRaceRing's seam).
                        _vesselImpactor.AcceptImpacteeFromSweep(omni);
                        omni.AcceptImpacteeFromSweep(_vesselImpactor);
                        _rules.OnOmniCollected();
                    }
                }

                // enemy prisms: bite, steal (Annex), crack shields (Shield Eaters); a danger prism of theirs kills a member
                if (index != null)
                {
                    index.QuerySphere(m.P, Mathf.Max(reach, DangerReach), _prismHits);
                    bool killed = false;
                    foreach (var q in _prismHits)
                    {
                        if (!IsEnemyPrism(q)) continue;
                        float d = (q.transform.position - m.P).magnitude;
                        if (q.prismProperties.IsDangerous && d <= DangerReach && !killed) killed = TryHarmMember(i);
                        if (d > reach || killed) continue;
                        if (q.prismProperties.IsShielded)
                        {
                            if (bitesNeed == int.MaxValue || Rnd() >= dt * 2f) continue;
                            // A silenced swarm cracks no rival shield (VesselSilence): the bite wobbles off.
                            if (q.DeflectsSilenced(_status.PlayerName, m.V.magnitude)) continue;
                            _bites.TryGetValue(q, out int bites);
                            if (++bites >= bitesNeed) { q.DeactivateShields(); _bites.Remove(q); }
                            else _bites[q] = bites;
                            continue;
                        }
                        if (stealRate > 0f && Rnd() < stealRate * dt)
                        {
                            q.Steal(_status.PlayerName, _status.Domain);
                            _rules.OnPrismRemoved();
                            continue;
                        }
                        if (Rnd() < (config.BaseBite + holeRate) * dt)
                        {
                            q.Damage(m.V, _status.Domain, _status.PlayerName);
                            _rules.OnPrismRemoved();
                        }
                    }
                    if (killed) continue;
                }

                // rivals: a bare member nips (a twentieth of a petal a second); Sting stings instead
                foreach (var r in _rivals)
                {
                    if (!Alive(r)) continue;
                    if ((r.Transform.position - m.P).sqrMagnitude > rivalReach2) continue;
                    var state = StateOf(r);
                    if (sting)
                    {
                        state.Sting += dt * 2f;
                        if (state.Sting >= stingNeed) { state.Sting = 0f; Hurt(r, config.PetalsPerHit, CombatHitClass.Strike, ElementalDebuffSources.VesselContact, m.V); }
                    }
                    else Hurt(r, config.NipRate * dt, CombatHitClass.Strike, ElementalDebuffSources.VesselContact, m.V);
                }
            }
        }

        /// <summary>A member is harmed: never at or below the protected minimum, a shell takes it first, Blink Step may dodge.</summary>
        bool TryHarmMember(int index)
        {
            if (_members.Count <= _rules.MinNow) return false;
            float dodge = _rules.ValueOr(StarlingCardIds.BlinkStep, 0, 0f) / 100f;
            if (Rnd() < dodge) return false;
            var m = _members[index];
            if (m.Shells > 0) { m.Shells--; return false; }
            Kill(index);
            return true;
        }

        /// <summary>A rival still in the match: its hull exists and has a pilot.</summary>
        static bool Alive(IVesselStatus r) => r is Object o && o && r.Player != null && r.Transform;

        Rival StateOf(IVesselStatus r)
        {
            if (!_rivalState.TryGetValue(r, out var state)) _rivalState[r] = state = new Rival();
            return state;
        }

        /// <summary>The lab's <c>hurtRival</c>: petals owed accumulate, and each whole hit's worth is one SCORED hit through
        /// the fleet's one gate (PvP is petals only), which is also what ends the Opposing vessels phase.</summary>
        void Hurt(IVesselStatus victim, float petals, CombatHitClass hitClass, ElementalDebuffSources source, Vector3 velocity)
        {
            var state = StateOf(victim);
            state.Nip += petals;
            if (state.Nip < config.PetalsPerHit) return;
            state.Nip = 0f;
            Hit(victim, hitClass, source, velocity);
        }

        bool Hit(IVesselStatus victim, CombatHitClass hitClass, ElementalDebuffSources source, Vector3 velocity)
        {
            if (!Alive(victim) || victim.Domain == _status.Domain) return false;
            if (!CombatHitDrain.TryAdmit(victim, _status, hitClass, config.HitCooldown, source, out int superseded)) return false;
            CombatHitDrain.ApplyPriced(victim, _status, hitClass, superseded, velocity, source);
            onCombatHitLanded.Raise(new CombatHitStats
            {
                ShooterName = _status.PlayerName,
                VictimName = victim.PlayerName,
                HitClass = hitClass,
                SupersededRank = superseded,
            });
            _rules.OnDebuff();
            return true;
        }

        // =============================================================================================== the cards

        /// <summary>The lab's Mass, Charge and Space card blocks.</summary>
        void Cards(float dt)
        {
            int n = _members.Count;
            if (n == 0) return;

            // ---- Mass: lay prisms, danger drip, the snowball, shells, breeding ----
            if (_rules.Has(StarlingCardIds.MoundBuilders) && Rnd() < _rules.Value(StarlingCardIds.MoundBuilders, 0) * dt)
                LayPrism(_members[(int)(Rnd() * n) % n].P, shielded: _rules.Level(StarlingCardIds.MoundBuilders) >= 15, danger: false, null);
            if (_rules.Has(StarlingCardIds.DangerDrip) && Rnd() < _rules.Value(StarlingCardIds.DangerDrip, 0) * dt)
                LayPrism(_members[(int)(Rnd() * n) % n].P, shielded: false, danger: true, null);
            if (_form == Form.Snowball && Rnd() < _rules.Value(StarlingCardIds.Snowball, 1) * dt)
                LayPrism(_swarmCentroid, shielded: false, danger: false, _snowballPrisms);
            if (_rules.Has(StarlingCardIds.Carapace))
            {
                int level = _rules.Level(StarlingCardIds.Carapace);
                int maxShells = level >= 15 ? 3 : level >= 8 ? 2 : 1;
                float regrow = _rules.Value(StarlingCardIds.Carapace, 0);
                foreach (var m in _members)
                {
                    if (m.Shells >= maxShells) continue;
                    m.ShellT += dt;
                    if (m.ShellT >= regrow) { m.ShellT = 0f; m.Shells++; }
                }
            }
            if (_rules.Has(StarlingCardIds.Brood) && _members.Count < _rules.CapNow && Cooldown("brood", _rules.Value(StarlingCardIds.Brood, 0), dt))
            {
                int hatch = _rules.Level(StarlingCardIds.Brood) >= 15 ? 2 : 1;
                for (int i = 0; i < hatch && _members.Count < _rules.CapNow; i++) AddMember(_swarmCentroid);
            }

            // ---- Charge: lasers, the tesla coil, the EMP, short circuit; chain lightning arcs a fauna kill on ----
            int jumps = _rules.Has(StarlingCardIds.ChainLightning) ? (int)_rules.Value(StarlingCardIds.ChainLightning, 0) : 0;
            if (_rules.Has(StarlingCardIds.Lasers))
            {
                float armed = Mathf.Min(1f, _rules.Value(StarlingCardIds.Lasers, 0) / 100f);
                float range = _rules.Value(StarlingCardIds.Lasers, 1);
                for (int i = 0; i < _members.Count; i++)
                {
                    if ((float)i / _members.Count >= armed) break;
                    var m = _members[i];
                    m.LaserT -= dt;
                    if (m.LaserT > 0f) continue;
                    m.LaserT = 1f;
                    var f = NearestFauna(m.P, range);
                    NearestRival(m.P, range, out var r);
                    bool faunaFirst = f && (r == null || (f.transform.position - m.P).sqrMagnitude < (r.Transform.position - m.P).sqrMagnitude);
                    if (faunaFirst) Zap(f, jumps);
                    else if (r != null) Hurt(r, 0.1f, CombatHitClass.Bullet, ElementalDebuffSources.Other, m.V);
                }
            }
            if (_rules.Has(StarlingCardIds.TeslaCoil) && Cooldown("tesla", _rules.Value(StarlingCardIds.TeslaCoil, 1), dt))
            {
                int zaps = (int)_rules.Value(StarlingCardIds.TeslaCoil, 0);
                for (int z = 0; z < zaps; z++)
                {
                    var f = NearestFauna(_swarmCentroid, TeslaReach);
                    if (!f) break;
                    Zap(f, jumps);
                }
            }
            if (_rules.Has(StarlingCardIds.Emp) && Cooldown("emp", EmpEvery, dt))
            {
                float radius = _rules.Value(StarlingCardIds.Emp, 1);
                foreach (var r in _rivals)
                    if (Alive(r) && (r.Transform.position - _swarmCentroid).sqrMagnitude < radius * radius)
                        Hit(r, CombatHitClass.Debuff, ElementalDebuffSources.Explosion, Vector3.zero);
            }
            if (_rules.Has(StarlingCardIds.ShortCircuit))
            {
                int need = (int)_rules.Value(StarlingCardIds.ShortCircuit, 0);
                foreach (var r in _rivals)
                {
                    if (!Alive(r)) continue;
                    var state = StateOf(r);
                    state.ShortCircuitT -= dt;
                    if (state.ShortCircuitT > 0f) continue;
                    int near = 0;
                    Vector3 at = r.Transform.position;
                    foreach (var m in _members) if ((m.P - at).sqrMagnitude < ShortCircuitReach * ShortCircuitReach) near++;
                    if (near < need) continue;
                    if (Hit(r, CombatHitClass.Debuff, ElementalDebuffSources.Explosion, Vector3.zero)) state.ShortCircuitT = ShortCircuitRest;
                }
            }

            // ---- Space: Black Hole, once per cycle in the mass phase when prisms are near ----
            if (_rules.Has(StarlingCardIds.BlackHole))
            {
                float radius = _rules.Value(StarlingCardIds.BlackHole, 1);
                var index = PrismSpatialIndex.Instance;
                if (_blackHole)
                {
                    _blackHoleT -= dt;
                    if (index != null)
                    {
                        index.QuerySphere(_blackHoleAt, radius, _prismHits);
                        foreach (var q in _prismHits)
                        {
                            if (!IsEnemyPrism(q) || q.prismProperties.IsShielded || Rnd() >= dt * 3f) continue;
                            q.Damage((_blackHoleAt - q.transform.position).normalized * 20f, _status.Domain, _status.PlayerName);
                            _rules.OnPrismRemoved();
                        }
                    }
                    if (_blackHoleT <= 0f) _blackHole = false;
                }
                else if (!_rules.BlackHoleUsed && _rules.Phase == StarlingPhase.OpposingMass && index != null)
                {
                    index.QuerySphere(_swarmCentroid, radius, _prismHits);
                    int enemies = 0;
                    foreach (var q in _prismHits) if (IsEnemyPrism(q)) enemies++;
                    if (enemies >= BlackHoleMinPrisms)
                    {
                        _blackHole = true;
                        _blackHoleAt = _swarmCentroid;
                        _blackHoleT = _rules.Value(StarlingCardIds.BlackHole, 0);
                        _rules.BlackHoleUsed = true;
                    }
                }
            }
        }

        Fauna NearestFauna(Vector3 at, float reach)
        {
            Fauna best = null;
            float bd = reach * reach;
            foreach (var f in _fauna)
            {
                if (!f || !f.isActiveAndEnabled) continue;
                float d = (f.transform.position - at).sqrMagnitude;
                if (d < bd) { bd = d; best = f; }
            }
            return best;
        }

        /// <summary>A Charge hit on a creature (Charge's job: kill fauna), arcing on through Chain Lightning.</summary>
        void Zap(Fauna target, int jumps)
        {
            if (!target) return;
            Vector3 at = target.transform.position;
            target.Predated(_status.PlayerName);
            while (jumps-- > 0)
            {
                var next = NearestFauna(at, ChainLightningReach);
                if (!next || next == target) break;
                at = next.transform.position;
                next.Predated(_status.PlayerName);
                target = next;
            }
        }

        /// <summary>Lay one of this team's prisms where a member is (the Rhino afterimage's pipeline).</summary>
        void LayPrism(Vector3 at, bool shielded, bool danger, List<Prism> into)
        {
            var prisms = _status.VesselPrismController;
            var channel = prisms ? prisms.PrismSpawnChannel : null;
            if (!channel)
            {
                if (!_warnedNoChannel)
                {
                    _warnedNoChannel = true;
                    CSDebug.LogWarning($"[Starling] {name}: the vessel has no prism spawn channel (VesselPrismController), so the " +
                                       "Mass cards cannot lay prisms.", this);
                }
                return;
            }
            Vector3 scale = prisms.TrailBaseScale;
            var ret = channel.RaiseEvent(new PrismEventData
            {
                ownDomain = _status.Domain,
                Rotation = Quaternion.LookRotation(new Vector3(Rnd() - 0.5f, Rnd() - 0.5f, Rnd() - 0.5f).normalized + Vector3.forward * 0.01f),
                SpawnPosition = at,
                Scale = scale,
                PrismType = prisms.SpawnPrismType,
            });
            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) return;
            prism.TargetScale = scale;
            prism.ownerID = _status.PlayerName;
            prism.ChangeTeam(_status.Domain);
            prism.waitTime = 0.5f;
            if (danger) prism.prismProperties.IsDangerous = true;
            prism.Initialize(_status.PlayerName);
            prism.AdmitTargetScale(scale);
            prism.TargetScale = scale;
            if (shielded) prism.ActivateShield();
            into?.Add(prism);
        }

        // =============================================================================================== the AI

        /// <summary>An AI Starling picks a card a moment after a hand arrives (the lab's random picker), then draws the next
        /// hand from a random element (which keeps the old one a quarter of the time); a human always picks.</summary>
        void AutoPickForAI(float now)
        {
            if (!_rules.HasHand || _status.Player == null || !_status.IsInitializedAsAI) { _aiPickAt = 0f; return; }
            if (_aiPickAt <= 0f) { _aiPickAt = now + 2f; return; }
            if (now < _aiPickAt) return;
            _aiPickAt = 0f;
            if (_rules.IsElementHand) PickAt((int)(Rnd() * _rules.HandSize) % _rules.HandSize);
            else Pick(StarlingRules.HandOrder[(int)(Rnd() * 4f) % 4]);
            ChooseDrawElement(StarlingRules.HandOrder[(int)(Rnd() * 4f) % 4]);
        }

        // =============================================================================================== the picture

        void Draw(float now)
        {
            if (now >= _materialSyncAt)
            {
                _materialSyncAt = now + MaterialSyncInterval;
                SyncMemberLook();
            }
            Vector3 up = _status?.Transform ? _status.Transform.up : Vector3.up;
            foreach (var m in _members)
            {
                if (!m.View) continue;
                Quaternion facing = m.V.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(m.V, up) : m.View.rotation;
                m.View.SetPositionAndRotation(m.P, facing);
            }
        }

        /// <summary>Members wear the hull model's current paint (the domain repaint lands on the hidden model) and its
        /// element morphs (the base animation drives them on the model by name).</summary>
        void SyncMemberLook()
        {
            if (!_modelRenderer) return;
            var materials = _modelRenderer.sharedMaterials;
            bool changed = _memberMaterials == null || _memberMaterials.Length != materials.Length;
            for (int i = 0; !changed && i < materials.Length; i++) changed = _memberMaterials[i] != materials[i];
            if (changed) _memberMaterials = materials;
            var mesh = _modelRenderer.sharedMesh;
            int shapes = mesh ? mesh.blendShapeCount : 0;
            foreach (var m in _members)
            {
                if (!m.Renderer) continue;
                if (changed) m.Renderer.sharedMaterials = _memberMaterials;
                for (int s = 0; s < shapes; s++) m.Renderer.SetBlendShapeWeight(s, _modelRenderer.GetBlendShapeWeight(s));
            }
        }

        // =============================================================================================== helpers

        float Rnd() => StarlingRules.Hash01(_seed ^ 0xA511E9B3u, _draws++);

        Vector3 InBall(float r)
        {
            for (int k = 0; k < 16; k++)
            {
                var p = new Vector3(Rnd() * 2f - 1f, Rnd() * 2f - 1f, Rnd() * 2f - 1f);
                if (p.sqrMagnitude <= 1f) return p * r;
            }
            return Vector3.zero;
        }

        /// <summary>The lab's <c>cooldown</c>: true once every <paramref name="every"/> seconds, the first after a full wait.</summary>
        bool Cooldown(string key, float every, float dt)
        {
            if (float.IsNaN(every) || every <= 0f) return false;
            if (!_cooldowns.TryGetValue(key, out float left)) left = every;
            left -= dt;
            bool fire = left <= 0f;
            if (fire) left += every;
            _cooldowns[key] = left;
            return fire;
        }
    }
}
