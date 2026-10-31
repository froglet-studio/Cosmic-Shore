using System.Collections;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent's seed wall: the MASS ability (design: R_VesselActions/SERPENT_SEED_WALL.md).
    ///
    /// Lives on the SEED, a trail prism left behind by the cloak or by the stopped stance. For as
    /// long as the wall is ACTIVE (<see cref="IsActive"/>) it STEALS FROM ITS EDGE: every
    /// <see cref="SeedWallActionSO.StealSweepInterval"/> it runs one sweep
    /// (<see cref="SerpentWallStealPlanner"/>) that pairs the open sites next to its bricks with
    /// the loose prisms in reach of them and pulls each into its site, reshaping it into a 2:1
    /// brick. So the wall grows toward the mass around it, along a trail it is eating, rather
    /// than as a uniform disc. It never finishes: a brick that is shot out reopens its site. An
    /// opponent's prism is stolen when it lands. Destroying the seed ends the wall: a seed shot the
    /// moment it is laid grows nothing.
    ///
    /// <para><b>Two kinds of wall, one rule for ending.</b> The CLOAK's seed is super-shielded and
    /// its wall outlives the cloak: it is active until that super-shield is broken. The STANCE's
    /// seed is never super-shielded (Garrett, 2026-10-13: the super-shield is the cloak's alone);
    /// its wall is HELD, active while the Serpent stays stopped, and <see cref="ReleaseHold"/>
    /// ends it when the Serpent flies again. Either way an ended wall stands where it is, stops
    /// stealing and stops answering crystals.</para>
    ///
    /// <para><b>Space is the wall's reach</b>, read live each sweep: the steal radius scales with
    /// the owner's Space multiplier, and the Space upgrade lets the wall take an opponent's
    /// SHIELDED prisms (without it they are left alone; super-shielded prisms never move).</para>
    ///
    /// <para><b>Cost.</b> One spatial query per sweep, a few waves of claims inside it, and
    /// per-frame work only for the prisms still in flight. A standing brick is checked once a
    /// sweep, not once a frame.</para>
    ///
    /// <para><b>The wall's SIZE is snapshotted at placement</b> (<see cref="Snapshot"/>): brick
    /// size and spacing from the Mass level. A wall keeps the size it was born with even if the
    /// pilot's Mass moves afterwards.</para>
    ///
    /// <para><b>Crystals</b> (<see cref="SerpentWallShieldByCrystalEffectSO"/>, omni and every
    /// element) draw a beam from the crystal to the seed of every active wall the collecting
    /// Serpent owns and ripple a shield through each wall, ring by ring. That is all a crystal
    /// does.</para>
    ///
    /// <para><b>Lockdown</b> (the Mass upgrade, Garrett 2026-10-29): while it is active, every
    /// square hole whose four surrounding bricks have all closed is sealed with a flat danger
    /// panel, a crack's width short of the shields around it. It no longer waits for a crystal:
    /// a hole is sealed as its fourth brick lands, and the holes already closed when the upgrade
    /// comes on are sealed at the next steal sweep.</para>
    ///
    /// <para><b>Multiplayer.</b> Like the legacy wall and the Scarab switch, the wall is rebuilt
    /// independently on every peer from replicated inputs (the cloak and stance presses, the
    /// broadcast crystal effect). Recruitment reads each peer's own prism set, so two peers can
    /// pull different loose prisms into the same site, exactly as the legacy wall could.</para>
    /// </summary>
    public sealed class SerpentWallAssembler : Assembler
    {
        /// <summary>The shape a wall is born with. Built by SeedAssemblerActionExecutor from the
        /// placing vessel's elemental state, then never changed. Lockdown is NOT part of it: it is
        /// read from the owner's replicated unlock bit at each steal sweep.</summary>
        public struct Snapshot
        {
            public SeedWallActionSO Config;
            public IVesselStatus Owner;
            public Domains Domain;
            public string PlayerName;
            public float ShortSide;
            public float Depth;
            public float MassMultiplier;
            /// <summary>The stopped stance's wall: active, without a super-shield, until
            /// <see cref="ReleaseHold"/>.</summary>
            public bool HeldByStance;
        }

        sealed class Member
        {
            public Prism Prism;
            public float BornAt;
            public Vector2Int Site;
            public bool Landed;
        }

        sealed class Panel
        {
            public Prism Prism;
            public float BornAt;
        }

        /// <summary>Every wall whose seed is alive, for the omni-crystal re-shield.</summary>
        public static readonly List<SerpentWallAssembler> Live = new();

        // A prism can belong to one wall at a time; a second wall must not pull it out of the first.
        static readonly HashSet<Prism> s_claimed = new();
        static readonly List<Prism> s_scratch = new(64);

        Snapshot _shape;
        bool _configured;
        float _pitch;
        float _seedBornAt;
        Vector3 _origin;
        Quaternion _frame;

        Quaternion _toWall;
        bool _panelsDirty;
        float _nextSweepAt;
        bool _growing;
        int _maxRing;

        readonly Dictionary<Vector2Int, Member> _members = new();
        // Sites holding a brick, standing or in flight, plus the seed's (0, 0): the planner's
        // "filled" set, kept in step with _members so a sweep never rebuilds it.
        readonly HashSet<Vector2Int> _filled = new();
        // Members still flying to their site: the only bricks touched every frame.
        readonly List<Member> _inFlight = new();

        readonly SerpentWallStealPlanner _planner = new();
        readonly List<Prism> _candidates = new();
        readonly List<Vector3> _candidateLocal = new();
        readonly List<SerpentWallStealPlanner.Claim> _claims = new();
        readonly Dictionary<Vector2Int, Panel> _panels = new();
        readonly HashSet<Vector2Int> _cellScratch = new();

        bool _locked;
        bool _held;
        Coroutine _ripple;

        public override Prism Prism { get; set; }
        public override Spindle Spindle { get; set; }
        public override int Depth { get; set; }

        public IVesselStatus Owner => _shape.Owner;
        public Snapshot Shape => _shape;

        /// <summary>
        /// Is <paramref name="p"/> part of a standing Seed Wall: a brick a wall claimed, or a live wall's seed? A
        /// question, never an error (Serpent Time asks it of every prism near a hull to charge the wall's cost).
        /// </summary>
        public static bool IsWallBrick(Prism p)
        {
            if (p == null) return false;
            if (s_claimed.Contains(p)) return true;
            for (int i = 0; i < Live.Count; i++)
                if (Live[i] && Live[i].Prism == p) return true;
            return false;
        }

        /// <summary>The seed is alive (not destroyed, not sitting in the pool), the vessel that
        /// laid it still exists, and the seed is either still super-shielded (the cloak's wall) or
        /// still held by the stopped stance. Only an active wall grows and answers a crystal.</summary>
        public bool IsActive => IsSeedAlive && IsOwnerAlive &&
                                (_held || Prism.prismProperties is { IsSuperShielded: true });

        /// <summary>A sweep reads the owner's Space level and upgrade, and a destroyed vessel's
        /// getters throw; <c>?.</c> does not see a destroyed Unity object. A wall whose pilot
        /// left (despawn, disconnect) stops growing and stands, like any ended wall.</summary>
        bool IsOwnerAlive => _shape.Owner is UnityEngine.Object o ? o != null : _shape.Owner != null;

        bool IsSeedAlive =>
            Prism && !Prism.destroyed && Prism.gameObject.activeInHierarchy &&
            Prism.prismProperties != null && Mathf.Approximately(Prism.prismProperties.TimeCreated, _seedBornAt);

        /// <summary>Never: the wall grows for as long as its seed lives.</summary>
        public override bool IsFullyBonded() => false;

        public override GrowthInfo GetGrowthInfo() => new GrowthInfo { CanGrow = false };

        /// <summary>
        /// Fix the wall's shape. Called once, after the seed is chosen and shielded and before
        /// <see cref="StartBonding"/>. A seed that is already a live wall keeps its first shape.
        /// </summary>
        public void Configure(Snapshot shape)
        {
            if (_configured && IsSeedAlive)
            {
                // The stance stopping on a live wall's seed holds that wall too.
                _held |= shape.HeldByStance;
                return;
            }

            Prism = GetComponent<Prism>();
            if (!Prism || shape.Config == null) return;

            _shape = shape;
            _configured = true;
            _pitch = SerpentWallLattice.Pitch(shape.ShortSide);
            _seedBornAt = Prism.prismProperties != null ? Prism.prismProperties.TimeCreated : 0f;
            _origin = Prism.transform.position;
            _frame = Prism.transform.rotation;
            _toWall = Quaternion.Inverse(_frame);
            _panelsDirty = false;
            _maxRing = 0;
            _members.Clear();
            _inFlight.Clear();
            _filled.Clear();
            _filled.Add(Vector2Int.zero);
            _panels.Clear();
            _locked = false;
            _held = shape.HeldByStance;

            // The seed is site (0, 0) and keeps its own long axis up; it takes the brick shape too.
            var brick = SerpentWallLattice.BrickScale(shape.ShortSide, shape.Depth);
            Prism.AdmitTargetScale(brick);
            Prism.TargetScale = brick;

            if (!Live.Contains(this)) Live.Add(this);
        }

        public override void StartBonding()
        {
            if (!_configured || !IsActive) return;
            _growing = true;
            _nextSweepAt = Time.time;
        }

        /// <summary>Deliberately nothing. The executor calls this when the cloak ends, and a cloak
        /// wall outlives it: it grows, and answers crystals, until its seed's super-shield is
        /// broken (Garrett, 2026-10-10: "prism walls should continue growing indefinitely"). The
        /// stance's wall ends through <see cref="ReleaseHold"/> instead, so the cloak finishing
        /// while the stance is held cannot end the stance's wall.</summary>
        public override void StopBonding() { }

        /// <summary>The Serpent flies again: a stance wall stops stealing and answering crystals
        /// and stands as it is, exactly like a cloak wall whose super-shield was broken. Bricks
        /// already in flight still land.</summary>
        public void ReleaseHold() => _held = false;

        void OnDisable() => Dissolve();

        void OnDestroy() => Dissolve();

        /// <summary>The seed is gone: release every brick back to the world and leave the roster.
        /// The bricks and panels stay where they are as ordinary prisms.</summary>
        void Dissolve()
        {
            Live.Remove(this);
            foreach (var m in _members.Values)
                if (HasRef(m.Prism)) s_claimed.Remove(m.Prism);
            _members.Clear();
            _inFlight.Clear();
            _filled.Clear();
            _panels.Clear();
            _growing = false;
            _configured = false;
            _held = false;
            if (_ripple != null) { StopCoroutine(_ripple); _ripple = null; }
        }

        void Update()
        {
            if (!_configured) return;

            if (!IsSeedAlive)
            {
                Dissolve();
                Destroy(this);
                return;
            }

            if (_growing && !IsActive) _growing = false;

            if (Time.time >= _nextSweepAt)
            {
                _nextSweepAt = Time.time + _shape.Config.StealSweepInterval;
                // Lockdown follows the owner's upgrade: on, every closed hole is due a panel; off,
                // no new panels (the ones laid stay as ordinary prisms).
                bool lockdown = LockdownActive();
                if (lockdown && !_locked) _panelsDirty = true;
                _locked = lockdown;
                // An ended wall steals nothing, but still lets go of bricks that were shot out, so
                // they do not stay claimed (and out of every other wall's reach) until its seed dies.
                if (_growing) Sweep();
                else ReleaseLostBricks();
            }

            // A Lockdown wall seals each hole as soon as its four bricks stand.
            if (_panelsDirty && _locked)
            {
                _panelsDirty = false;
                LayPanels();
            }

            SteerMembers();
        }

        // ---------------- Growth ----------------

        /// <summary>
        /// One steal sweep: drop the bricks that were lost since the last one, gather the loose
        /// prisms that could be in reach of the edge with a single spatial query, let the planner
        /// pair them with edge sites, and send each claimed prism flying.
        /// </summary>
        void Sweep()
        {
            ReleaseLostBricks();

            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return;

            var cfg = _shape.Config;
            float space = ElementalScaling.Multiplier(_shape.Owner, Element.Space,
                atFull: cfg.SpaceStealRadiusMultiplierAtFull, minMul: cfg.MinSpaceStealRadiusMultiplier);
            float radius = cfg.StealRadius * _shape.MassMultiplier * space;
            var abilities = _shape.Owner?.ElementalAbilityHandler;
            bool takeShielded = abilities && abilities.IsUpgradeActive(Element.Space);

            // Every edge site lies within one diagonal pitch of the outermost ring.
            float queryRadius = (_maxRing + 1) * _pitch * 1.4143f + radius;
            int n = index.QuerySphere(_origin, queryRadius, s_scratch);
            _candidates.Clear();
            _candidateLocal.Clear();
            for (int i = 0; i < n; i++)
            {
                var p = s_scratch[i];
                if (!IsRecruitable(p, takeShielded)) continue;
                Vector3 local = _toWall * (p.transform.position - _origin);
                if (Mathf.Abs(local.z) > radius) continue;
                _candidates.Add(p);
                _candidateLocal.Add(local);
            }
            if (_candidates.Count == 0) return;

            _planner.Plan(_filled, _candidateLocal, _pitch, radius,
                cfg.MaxStealsPerSweep, cfg.StealWavesPerSweep, _claims);
            foreach (var claim in _claims)
                Recruit(claim.Site, _candidates[claim.Candidate]);
        }

        void Recruit(Vector2Int site, Prism prism)
        {
            s_claimed.Add(prism);
            var brick = SerpentWallLattice.BrickScale(_shape.ShortSide, _shape.Depth);
            prism.AdmitTargetScale(brick);
            prism.TargetScale = brick;

            var member = new Member
            {
                Prism = prism,
                BornAt = prism.prismProperties != null ? prism.prismProperties.TimeCreated : 0f,
                Site = site,
                Landed = false,
            };
            _members[site] = member;
            _filled.Add(site);
            _inFlight.Add(member);
            _maxRing = Mathf.Max(_maxRing, SerpentWallLattice.Ring(site));
        }

        /// <summary>Standing bricks that were shot out (destroyed, or returned to the pool) since
        /// the last sweep: their sites reopen and become edge again. A pooled prism REUSED between
        /// two sweeps is not caught: <c>TimeCreated</c> is stamped in <c>Prism.Awake</c> only
        /// (BACKLOG.md, Seed Wall rows).</summary>
        void ReleaseLostBricks()
        {
            foreach (var m in _members.Values)
                if (HasRef(m.Prism) && m.Landed && !IsAlive(m.Prism, m.BornAt))
                    Release(m);
        }

        /// <summary>The member still holds a reference, even to a destroyed prism: Unity's
        /// <c>if (prism)</c> is false for a destroyed object, which used to skip its release and
        /// leave its site filled.</summary>
        static bool HasRef(Prism p) => (object)p != null;

        void Release(Member m)
        {
            s_claimed.Remove(m.Prism);
            _filled.Remove(m.Site);
            m.Prism = null;
            m.Landed = false;
        }

        /// <summary>
        /// Loose mass the wall may take: not a seed or anyone's super-shielded armour, not a
        /// creature, not already another wall's. An opponent's SHIELDED prism only with the Space
        /// upgrade (<paramref name="takeShielded"/>); the Serpent's own shielded prisms are its own.
        /// </summary>
        bool IsRecruitable(Prism p, bool takeShielded)
        {
            if (!p || p == Prism || p.destroyed || !p.IsCreationComplete) return false;
            if (s_claimed.Contains(p)) return false;
            // A creature's body is not loose mass: pulling it into a wall would dismember a
            // lifeform, which the ecology law forbids a vessel to do by accident.
            if (p is HealthPrism) return false;
            var props = p.prismProperties;
            if (props == null || props.IsSuperShielded) return false;   // seeds and armour stay put
            if (props.IsShielded && !takeShielded && p.Domain != _shape.Domain) return false;
            if (p.TryGetComponent<SerpentWallAssembler>(out var wall) && wall.IsActive) return false;
            return true;
        }

        void SteerMembers()
        {
            if (_inFlight.Count == 0) return;
            float dt = Time.deltaTime;
            var cfg = _shape.Config;

            for (int i = _inFlight.Count - 1; i >= 0; i--)
            {
                var m = _inFlight[i];
                if (!m.Prism || !IsAlive(m.Prism, m.BornAt))
                {
                    // Shot down in flight, or returned to the pool: the site is edge again.
                    if (HasRef(m.Prism)) Release(m);
                    RemoveInFlight(i);
                    continue;
                }

                var t = m.Prism.transform;
                Vector3 target = SitePosition(m.Site);
                Quaternion rot = SiteRotation(m.Site);
                bool own = m.Prism.Domain == _shape.Domain;
                float speed = own ? cfg.OwnPullSpeed : cfg.OpponentPullSpeed;
                t.position = Vector3.MoveTowards(t.position, target, speed * dt);
                t.rotation = Quaternion.Slerp(t.rotation, rot, Mathf.Clamp01(cfg.PullRotateRate * dt));

                if ((t.position - target).sqrMagnitude < 0.01f)
                {
                    t.SetPositionAndRotation(target, rot);
                    m.Landed = true;
                    RemoveInFlight(i);
                    if (!own) m.Prism.Steal(_shape.PlayerName, _shape.Domain, true);
                    if (_locked) _panelsDirty = true;
                }
                m.Prism.NotifyPositionChanged();
            }
        }

        void RemoveInFlight(int i)
        {
            int last = _inFlight.Count - 1;
            _inFlight[i] = _inFlight[last];
            _inFlight.RemoveAt(last);
        }

        // ---------------- Crystals ----------------

        /// <summary>
        /// Every active wall seeded by <paramref name="owner"/> (a super-shielded cloak wall, or the
        /// stance's wall while the Serpent stays stopped) draws a beam from the crystal and ripples
        /// a shield through its bricks. Returns how many walls answered.
        /// </summary>
        public static int ReshieldWallsOf(IVesselStatus owner, Vector3 crystalPosition, Color beamColour)
        {
            if (owner == null) return 0;
            int answered = 0;
            for (int i = Live.Count - 1; i >= 0; i--)
            {
                var wall = Live[i];
                if (!wall || wall.Owner != owner || !wall.IsActive) continue;
                wall.Reshield(crystalPosition, beamColour);
                answered++;
            }
            return answered;
        }

        void Reshield(Vector3 crystalPosition, Color beamColour)
        {
            // The crystal "morphs into beams": the sniper's own pooled, fading tracer, one per
            // seed, with its flare landing on the seed.
            var cfg = _shape.Config;
            SniperBeam.Fire(crystalPosition, Prism.transform.position,
                startWidth: cfg.BeamWidth, endWidth: cfg.BeamWidth * 0.5f, colour: beamColour,
                beamSeconds: cfg.BeamSeconds, flareRadius: _shape.ShortSide,
                flareSeconds: cfg.BeamSeconds, hit: true);
            if (_ripple != null) StopCoroutine(_ripple);
            _ripple = StartCoroutine(RippleAndLock());
        }

        IEnumerator RippleAndLock()
        {
            var cfg = _shape.Config;
            int maxRing = 0;
            foreach (var site in _members.Keys) maxRing = Mathf.Max(maxRing, SerpentWallLattice.Ring(site));

            for (int ring = 1; ring <= maxRing; ring++)
            {
                if (cfg.ShieldRippleStep > 0f) yield return CosmicShore.Utility.WaitCache.Seconds(cfg.ShieldRippleStep);
                if (!IsActive) yield break;
                foreach (var m in _members.Values)
                {
                    if (!m.Landed || SerpentWallLattice.Ring(m.Site) != ring) continue;
                    if (!IsAlive(m.Prism, m.BornAt) || m.Prism.Domain != _shape.Domain) continue;
                    if (m.Prism.prismProperties is { IsSuperShielded: true }) continue;
                    m.Prism.ActivateShield();
                }
            }

            _ripple = null;
        }

        /// <summary>The owner's Mass upgrade (Lockdown), read from the REPLICATED unlock bit, so
        /// every peer decides the same way and a wall laid before the upgrade still locks once it
        /// comes on.</summary>
        bool LockdownActive()
        {
            var abilities = _shape.Owner?.ElementalAbilityHandler;
            return abilities && abilities.IsUpgradeActive(Element.Mass);
        }

        /// <summary>Seal every hole whose four corner bricks are standing. Re-run on every brick
        /// landing while locked, so a panel that was shot out comes back as the wall grows.</summary>
        void LayPanels()
        {
            var channel = _shape.Config.PrismSpawnChannel;
            if (channel == null)
            {
                Debug.LogWarning("[SerpentWallAssembler] SeedWallActionSO has no Prism Spawn Channel, " +
                                 "so a Mass-5 wall cannot lay its danger panels.", _shape.Config);
                return;
            }

            // Runs on every brick landing once a wall is locked: reuse one set, no allocation.
            var cells = _cellScratch;
            cells.Clear();
            foreach (var site in _members.Keys)
                for (int dx = -1; dx <= 0; dx++)
                for (int dy = -1; dy <= 0; dy++)
                    cells.Add(new Vector2Int(site.x + dx, site.y + dy));

            foreach (var cell in cells)
            {
                if (!CornerStanding(cell.x, cell.y) || !CornerStanding(cell.x + 1, cell.y) ||
                    !CornerStanding(cell.x + 1, cell.y + 1) || !CornerStanding(cell.x, cell.y + 1))
                    continue;
                if (_panels.TryGetValue(cell, out var existing) && IsAlive(existing.Prism, existing.BornAt))
                    continue;

                var panel = LayPanel(channel, cell);
                if (panel) _panels[cell] = new Panel
                {
                    Prism = panel,
                    BornAt = panel.prismProperties != null ? panel.prismProperties.TimeCreated : 0f,
                };
            }
        }

        bool CornerStanding(int i, int j)
        {
            if (i == 0 && j == 0) return IsActive;
            return _members.TryGetValue(new Vector2Int(i, j), out var m) && m.Landed && IsAlive(m.Prism, m.BornAt);
        }

        /// <summary>Same ordering contract as ScarabSwitch.TryLay: ChangeTeam (never the Domain
        /// setter), clear stale tier flags, Initialize, THEN admit the authored scale, THEN apply
        /// the tier.</summary>
        Prism LayPanel(PrismEventChannelWithReturnSO channel, Vector2Int cell)
        {
            Vector2 c = SerpentWallLattice.CellCenter(cell.x, cell.y, _pitch);
            var (side, angle) = SerpentWallLattice.CellPanel(cell.x, cell.y, _shape.ShortSide,
                _shape.Config.LockPanelClearance * _shape.ShortSide);
            if (side <= 0f) return null;
            Vector3 pos = _origin + _frame * new Vector3(c.x, c.y, 0f);
            Quaternion rot = SerpentWallLattice.PanelRotation(_frame, angle);
            var scale = new Vector3(side, side, _shape.Config.LockPanelThickness);

            var ret = channel.RaiseEvent(new PrismEventData
            {
                ownDomain = _shape.Domain,
                Rotation = rot,
                SpawnPosition = pos,
                Scale = scale,
                Velocity = Vector3.zero,
                PrismType = PrismType.Interactive,
                TargetTransform = null,
                OnGrowCompleted = null,
            });
            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) return null;

            prism.ChangeTeam(_shape.Domain);
            prism.ownerID = $"{_shape.PlayerName}::SeedWall::{GetInstanceID()}";
            if (prism.prismProperties != null)
            {
                prism.prismProperties.IsShielded = false;
                prism.prismProperties.IsDangerous = false;
                prism.prismProperties.speedDebuffAmount = 0f;
            }
            prism.Initialize(_shape.PlayerName);
            prism.AdmitTargetScale(scale);
            prism.TargetScale = scale;
            PrismKinds.Apply(prism, PrismKind.Danger);
            return prism;
        }

        // ---------------- Geometry ----------------

        Vector3 SitePosition(Vector2Int site)
        {
            Vector2 c = SerpentWallLattice.SiteCenter(site.x, site.y, _pitch);
            return _origin + _frame * new Vector3(c.x, c.y, 0f);
        }

        Quaternion SiteRotation(Vector2Int site) =>
            SerpentWallLattice.BrickRotation(_frame, site.x, site.y);

        static bool IsAlive(Prism p, float bornAt) =>
            p && !p.destroyed && p.gameObject.activeInHierarchy &&
            p.prismProperties != null && Mathf.Approximately(p.prismProperties.TimeCreated, bornAt);
    }
}
