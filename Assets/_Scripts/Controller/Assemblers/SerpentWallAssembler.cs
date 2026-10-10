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
    /// Lives on the SEED, a super-shielded trail prism left behind by the cloak or by the stopped
    /// stance. While the seed bonds it claims lattice sites outward from itself
    /// (<see cref="SerpentWallLattice.GrowthOrder"/>) and pulls the nearest loose prism into each
    /// one, reshaping it into a 2:1 brick. An opponent's prism is stolen when it lands, which is
    /// the wall stealing along its edge. Destroying the seed ends the wall: a seed shot the moment
    /// it is laid grows nothing.
    ///
    /// <para><b>Everything about the wall's shape is snapshotted at placement</b>
    /// (<see cref="Snapshot"/>): brick size and spacing from the Mass level, and whether the wall
    /// carries the Mass-5 Lockdown. A wall keeps the shape it was born with even if the pilot's
    /// Mass moves afterwards.</para>
    ///
    /// <para><b>Omni crystals</b> (<see cref="SerpentWallShieldByCrystalEffectSO"/>) draw a beam
    /// from the crystal to every live super-shielded seed the collecting Serpent owns and ripple a
    /// shield through each wall, ring by ring. A Lockdown wall also twists every brick clockwise
    /// on its first crystal, which opens one parity of lattice cell and closes the other, and
    /// seals the open cells with flat danger panels.</para>
    ///
    /// <para><b>Multiplayer.</b> Like the legacy wall and the Scarab switch, the wall is rebuilt
    /// independently on every peer from replicated inputs (the cloak and stance presses, the
    /// broadcast crystal effect). Recruitment reads each peer's own prism set, so two peers can
    /// pull different loose prisms into the same site, exactly as the legacy wall could.</para>
    /// </summary>
    public sealed class SerpentWallAssembler : Assembler
    {
        /// <summary>The shape a wall is born with. Built by SeedAssemblerActionExecutor from the
        /// placing vessel's elemental state, then never changed.</summary>
        public struct Snapshot
        {
            public SeedWallActionSO Config;
            public IVesselStatus Owner;
            public Domains Domain;
            public string PlayerName;
            public float ShortSide;
            public float Depth;
            public float MassMultiplier;
            public bool Lockdown;
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

        List<Vector2Int> _order;
        int _nextSite;
        float _nextClaimAt;
        bool _growing;

        readonly Dictionary<Vector2Int, Member> _members = new();
        readonly Dictionary<Vector2Int, Panel> _panels = new();

        float _twist, _twistFrom, _twistTo, _twistStartedAt;
        bool _twisting, _locked;
        int _openParity = -1;
        float _panelSide, _panelAngle;
        Coroutine _ripple;

        public override Prism Prism { get; set; }
        public override Spindle Spindle { get; set; }
        public override int Depth { get; set; }

        public IVesselStatus Owner => _shape.Owner;
        public Snapshot Shape => _shape;

        /// <summary>The seed is alive (not destroyed, not recycled by the pool) and still
        /// super-shielded. Only an active wall grows and answers an omni crystal.</summary>
        public bool IsActive => IsSeedAlive && Prism.prismProperties is { IsSuperShielded: true };

        bool IsSeedAlive =>
            Prism && !Prism.destroyed && Prism.gameObject.activeInHierarchy &&
            Prism.prismProperties != null && Mathf.Approximately(Prism.prismProperties.TimeCreated, _seedBornAt);

        public override bool IsFullyBonded() => _order != null && _nextSite >= _order.Count;

        public override GrowthInfo GetGrowthInfo() => new GrowthInfo { CanGrow = false };

        /// <summary>
        /// Fix the wall's shape. Called once, after the seed is chosen and shielded and before
        /// <see cref="StartBonding"/>. A seed that is already a live wall keeps its first shape.
        /// </summary>
        public void Configure(Snapshot shape)
        {
            if (_configured && IsSeedAlive) return;

            Prism = GetComponent<Prism>();
            if (!Prism || shape.Config == null) return;

            _shape = shape;
            _configured = true;
            _pitch = SerpentWallLattice.Pitch(shape.ShortSide);
            _seedBornAt = Prism.prismProperties != null ? Prism.prismProperties.TimeCreated : 0f;
            _origin = Prism.transform.position;
            _frame = Prism.transform.rotation;
            _order = SerpentWallLattice.GrowthOrder(Mathf.Max(0, Depth));
            _nextSite = 0;
            _members.Clear();
            _panels.Clear();
            _twist = 0f;
            _twisting = _locked = false;
            _openParity = -1;

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
            _nextClaimAt = Time.time;
        }

        /// <summary>Stops GROWTH only. The wall keeps its bricks and still answers omni crystals
        /// for as long as its seed lives - that is what makes old walls worth re-shielding.</summary>
        public override void StopBonding() => _growing = false;

        void OnDisable() => Dissolve();

        void OnDestroy() => Dissolve();

        /// <summary>The seed is gone: release every brick back to the world and leave the roster.
        /// The bricks and panels stay where they are as ordinary prisms.</summary>
        void Dissolve()
        {
            Live.Remove(this);
            foreach (var m in _members.Values)
                if (m.Prism) s_claimed.Remove(m.Prism);
            _members.Clear();
            _panels.Clear();
            _growing = false;
            _configured = false;
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

            if (_growing && Time.time >= _nextClaimAt)
            {
                _nextClaimAt = Time.time + _shape.Config.SiteClaimInterval;
                ClaimNextSite();
            }

            if (_twisting)
            {
                float d = _shape.Config.LockTwistSeconds;
                float t = d <= 0f ? 1f : Mathf.Clamp01((Time.time - _twistStartedAt) / d);
                _twist = Mathf.Lerp(_twistFrom, _twistTo, t);
                if (t >= 1f) _twisting = false;
                Prism.transform.rotation = SiteRotation(Vector2Int.zero);
                Prism.NotifyPositionChanged();
            }

            SteerMembers();
        }

        // ---------------- Growth ----------------

        void ClaimNextSite()
        {
            if (_order == null) return;
            while (_nextSite < _order.Count)
            {
                var site = _order[_nextSite++];
                if (_members.ContainsKey(site)) continue;

                var candidate = FindRecruit(SitePosition(site));
                if (!candidate) continue;   // nothing loose near this site: move on, never stall

                s_claimed.Add(candidate);
                var brick = SerpentWallLattice.BrickScale(_shape.ShortSide, _shape.Depth);
                candidate.AdmitTargetScale(brick);
                candidate.TargetScale = brick;

                _members[site] = new Member
                {
                    Prism = candidate,
                    BornAt = candidate.prismProperties != null ? candidate.prismProperties.TimeCreated : 0f,
                    Site = site,
                    Landed = false,
                };
                return;
            }
            _growing = false;
        }

        Prism FindRecruit(Vector3 site)
        {
            var index = PrismSpatialIndex.EnsureInstance();
            if (index == null || !index.IsAvailable) return null;

            // The loose mass is the Serpent's own trail, which runs back from the SEED along the
            // wall's normal, while the outer sites sit several shielded pitches (13.5 x Mass) out
            // in the wall's plane. A radius about the site alone would leave every site further
            // than that from the trail with nothing to pull and the wall would stop after one
            // ring, so the reach grows with the site's distance from the seed: anything a site
            // could reach, the seed's own neighbourhood included, is a candidate.
            float radius = _shape.Config.RecruitRadius * _shape.MassMultiplier
                           + Vector3.Distance(site, _origin);
            int n = index.QuerySphere(site, radius, s_scratch);
            Prism best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var p = s_scratch[i];
                if (!IsRecruitable(p)) continue;
                float sqr = (p.transform.position - site).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
            return best;
        }

        bool IsRecruitable(Prism p)
        {
            if (!p || p == Prism || p.destroyed || !p.IsCreationComplete) return false;
            if (s_claimed.Contains(p)) return false;
            // A creature's body is not loose mass: pulling it into a wall would dismember a
            // lifeform, which the ecology law forbids a vessel to do by accident.
            if (p is HealthPrism) return false;
            var props = p.prismProperties;
            if (props == null || props.IsSuperShielded) return false;   // seeds and armour stay put
            if (p.TryGetComponent<SerpentWallAssembler>(out var wall) && wall.IsActive) return false;
            return true;
        }

        void SteerMembers()
        {
            if (_members.Count == 0) return;
            float dt = Time.deltaTime;
            var cfg = _shape.Config;

            foreach (var m in _members.Values)
            {
                if (!m.Prism) continue;
                if (!IsAlive(m.Prism, m.BornAt))
                {
                    // Shot out, or recycled by the pool into someone else's prism: let it go.
                    s_claimed.Remove(m.Prism);
                    m.Prism = null;
                    continue;
                }

                var t = m.Prism.transform;
                Vector3 target = SitePosition(m.Site);
                Quaternion rot = SiteRotation(m.Site);

                if (m.Landed)
                {
                    if (_twisting)
                    {
                        t.rotation = rot;
                        m.Prism.NotifyPositionChanged();
                    }
                    continue;
                }

                bool own = m.Prism.Domain == _shape.Domain;
                float speed = own ? cfg.OwnPullSpeed : cfg.OpponentPullSpeed;
                t.position = Vector3.MoveTowards(t.position, target, speed * dt);
                t.rotation = Quaternion.Slerp(t.rotation, rot, Mathf.Clamp01(cfg.PullRotateRate * dt));

                if ((t.position - target).sqrMagnitude < 0.01f)
                {
                    t.SetPositionAndRotation(target, rot);
                    m.Landed = true;
                    if (!own) m.Prism.Steal(_shape.PlayerName, _shape.Domain, true);
                }
                m.Prism.NotifyPositionChanged();
            }
        }

        // ---------------- Omni crystal ----------------

        /// <summary>
        /// Every live, super-shielded wall seeded by <paramref name="owner"/> draws a beam from the
        /// crystal and ripples a shield through its bricks. Returns how many walls answered.
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
                if (cfg.ShieldRippleStep > 0f) yield return new WaitForSeconds(cfg.ShieldRippleStep);
                if (!IsActive) yield break;
                foreach (var m in _members.Values)
                {
                    if (!m.Landed || SerpentWallLattice.Ring(m.Site) != ring) continue;
                    if (!IsAlive(m.Prism, m.BornAt) || m.Prism.Domain != _shape.Domain) continue;
                    if (m.Prism.prismProperties is { IsSuperShielded: true }) continue;
                    m.Prism.ActivateShield();
                }
            }

            if (_shape.Lockdown && IsActive)
            {
                if (!_locked) yield return Twist();
                LayPanels();
            }
            _ripple = null;
        }

        IEnumerator Twist()
        {
            var cfg = _shape.Config;
            _locked = true;
            _twistFrom = _twist;
            _twistTo = cfg.LockTwistDegrees;
            _twistStartedAt = Time.time;
            _twisting = true;

            _openParity = SerpentWallLattice.OpeningParity(_shape.ShortSide, _twistTo);
            if (_openParity >= 0)
            {
                var cell = _openParity == 0 ? Vector2Int.zero : new Vector2Int(1, 0);
                (_panelSide, _panelAngle) =
                    SerpentWallLattice.LargestClearSquare(cell.x, cell.y, _shape.ShortSide, _twistTo);
            }

            while (_twisting) yield return null;
        }

        /// <summary>Seal every open cell whose four corner bricks are standing. Re-run on each
        /// crystal, so a panel that was shot out comes back with the next omni.</summary>
        void LayPanels()
        {
            if (_openParity < 0 || _panelSide <= 0f) return;
            var channel = _shape.Config.PrismSpawnChannel;
            if (channel == null)
            {
                Debug.LogWarning("[SerpentWallAssembler] SeedWallActionSO has no Prism Spawn Channel, " +
                                 "so a Mass-5 wall cannot lay its danger panels.", _shape.Config);
                return;
            }

            var cells = new HashSet<Vector2Int>();
            foreach (var site in _members.Keys)
                for (int dx = -1; dx <= 0; dx++)
                for (int dy = -1; dy <= 0; dy++)
                    cells.Add(new Vector2Int(site.x + dx, site.y + dy));

            foreach (var cell in cells)
            {
                if (SerpentWallLattice.CellParity(cell.x, cell.y) != _openParity) continue;
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
            Vector3 fwd = _frame * Vector3.forward;
            Vector3 pos = _origin + _frame * new Vector3(c.x, c.y, 0f);
            // The lattice maths is counter-clockwise-positive; Unity's AngleAxis about forward is
            // clockwise-positive seen from the seat, hence the sign.
            Quaternion rot = Quaternion.AngleAxis(-_panelAngle, fwd) * _frame;
            // A hair under the clear size, so a panel seals the cell without grinding its corners.
            float side = _panelSide * 0.98f;
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

        Quaternion SiteRotation(Vector2Int site)
        {
            // Local y is the brick's long axis. A long-axis-RIGHT site turns the seed's frame a
            // quarter about its own forward.
            Quaternion local = SerpentWallLattice.IsLongAxisUp(site.x, site.y)
                ? Quaternion.identity
                : Quaternion.AngleAxis(90f, Vector3.forward);
            Vector3 fwd = _frame * Vector3.forward;
            return Quaternion.AngleAxis(_twist, fwd) * (_frame * local);
        }

        static bool IsAlive(Prism p, float bornAt) =>
            p && !p.destroyed && p.gameObject.activeInHierarchy &&
            p.prismProperties != null && Mathf.Approximately(p.prismProperties.TimeCreated, bornAt);
    }
}
