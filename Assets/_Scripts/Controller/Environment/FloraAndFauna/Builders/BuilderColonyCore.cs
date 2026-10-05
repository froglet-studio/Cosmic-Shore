// The FORTRESS colony (and the NEST weavers, its peaceful phase) - research Tools/Ecology/builders/core.py (Colony),
// nest.py (NestWeavers) and fortress.py (Fortress), ported to plain C# over a struct-of-arrays. Same shape as the
// swarm cores: no UnityEngine, the SAME file compiles and RUNS headless (Tools/Build/builders_harness), and the glue
// (BuilderColonyFauna) converts at the boundary. Docs/BUILDERS_AND_THIEVES.md is the design record; every constant
// below quotes the research unless its comment says GAME.
//
// No species reads a blueprint. A worker FORAGES loose mass (4x preference for a pilot's trail), STEALS it on pickup
// (the prism changes hands, never leaves), CARRIES it (one transform write + NotifyPositionChanged per moving carrier
// per step - the expensive part) and DEPOSITS it on a lattice site when the LOCAL rule says so:
//   1. template Q(r) = exp(-((r - Rc) / w)^2) around the core (the queen's royal-chamber cue);
//   2. p = Q * (0.05 + C^2 / (C^2 + k^2)) on a free site touching the structure, Q * nucleate otherwise
//      (C = the cement pheromone every placed prism emits - Khuong et al. 2016's time cue);
//   3. GAP rule (fortress): p *= 1 + 2.5 * max(0, nb26 - 4) / 6 - holes close from the rim inward;
//   4. ALARM (fortress): a breached site (its prism destroyed, or stolen back) gets +1; alarm diffuses (0.5 self /
//      0.5 neighbours) and decays 5% every 3 ticks; a laden worker that smells alarm > 0.02 climbs its gradient.
//      ALARM is what makes the wound close: t50 6.4 s with alarm + gap vs 39.5 s with neither (research, 3 seeds x 3 cuts);
//   5. defence escalation (Colony.defend): one alarm LEVEL rises 0.6/s while a pilot is inside the alarm radius;
//      below 0.6 the unladen workers form a guard screen between core and pilot (the telegraph), at 0.6 they strike;
//   6. scar tissue (round 2): a slow max of alarm widens the template where the wall was cut, so it heals thicker.
// What the GAME adds (Docs/BUILDERS_AND_THIEVES.md §3): a STOMACH per worker (eat a loose prism when hungry, starve
// only when empty, breed from a full one - the ecology law), the claim-before-place reservation, a clock-stamped
// settle, the band, and contact deaths reported to the glue (every death drops the worker's heart crystal).
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>Which of the fortress's two mending cues are on (the research's ablations).</summary>
    public enum BuilderMendRule
    {
        None = 0,
        Gap = 1,
        Alarm = 2,
        Both = 3,
    }

    /// <summary>Every number a fortress / nest colony runs on. Defaults are the research fortress (fortress.py).</summary>
    public sealed class BuilderColonyParams
    {
        public int Founders = 48;                 // research n = 48 (24-48 recommended, PORT.md §2)
        public int MaxWorkers = 48;               // the cap is a backstop, not the dynamics
        public float Spacing = 8f;                // lattice s
        public int LatticeHalf = 16;              // 33^3 sites (PORT.md §1: "cut to 33^3 = 0.14 MB")
        public float Speed = 70f;
        public float Sense = 220f;                // nest.py passes sense 220 to the colony
        public int ForageFraction = 4;            // 1 in 4 workers re-forages per step
        public float TrailPreference = 0.25f;     // distance multiplier for a pilot's trail (4x preferred)
        public float PickupReach = 6f;
        public float CarryDrop = 4f;              // the carried prism hangs 4 u below its carrier
        public float Rc = 40f, W = 7f;            // fortress template (nest: 36/9; PORT §2 suggests 44/30 for a nest)
        public float KCement = 0.6f;
        public float Nucleate = 0.02f;            // fortress 0.02 (nest 0.01)
        public float CoreFraction = 0.55f;        // sites inside Rc * 0.55 are the open brood chamber
        public BuilderMendRule Mend = BuilderMendRule.Both;
        public float GapGain = 2.5f;
        public float AlarmGain = 1f;
        public float ScarGain;                    // 0 = off (the research default); round 2 ran 3
        public float AlarmRadius = 110f;
        public float AlarmRise = 0.6f, AlarmFall = 0.3f, StrikeAt = 0.6f, StingCooldown = 1.5f;
        /// <summary>0 = every unladen worker answers the alarm; 0.3 = a 30% defender caste (round 3's design dial, and the
        /// research's recommended fortress: without it defence starves repair - t50 17.8 s vs 5.6 s).</summary>
        public float DefendCaste = 0.3f;
        public float WorkerRadius = 3f;           // agent_size
        public float RamExtra = 1f;               // a pilot kills a worker inside radius + size + 1
        public float StingReach = 4f;
        /// <summary>GAME: PrismSpatialIndex.TryReserve clear radius at a deposit (claim-before-place). 0 = no claim.</summary>
        public float ReserveClear = 3.6f;
        /// <summary>PORT.md's predicate excludes the colony's own domain; the research re-picked its own loose bricks.</summary>
        public bool SkipOwnDomain = true;
        /// <summary>GAME: the settle from carrier to site is a clock-stamped flight of this long.</summary>
        public float SettleSeconds = 0.35f;
        /// <summary>Workers stay inside this radius of the cell centre (research: 0.95 x the arena radius).</summary>
        public float Containment = 1140f;
        public Vector3 CellCentre;
        /// <summary>GAME: the species band (FaunaConfigurationSO.BandInner/Outer) - mass outside it is never foraged. 0 = none.</summary>
        public float BandInner, BandOuter;
        /// <summary>GAME: stomachs. Null = the research (no eating, no births).</summary>
        public BuilderStomachParams Stomach = new();
    }

    public sealed class BuilderColonyCore
    {
        static readonly Vector3[] N26 = BuildN26();

        static Vector3[] BuildN26()
        {
            var l = new List<Vector3>(26);
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
                if (x != 0 || y != 0 || z != 0) l.Add(new Vector3(x, y, z));
            return l.ToArray();
        }

        readonly BuilderColonyParams P;
        readonly BuilderRng _rng;
        readonly IBuilderWorld _world;
        public readonly int Domain;
        public readonly int ColonyId;
        public readonly int Cap;
        public Vector3 Anchor { get; }

        // ── workers (struct of arrays) ────────────────────────────────────────────────────────────
        public readonly Vector3[] Pos, Vel;
        public readonly bool[] Alive;
        public readonly int[] Carry, Goal;
        public readonly float[] Intent, Stomach, BornAt;
        /// <summary>GAME: member k has a real proxy body (collider on) - a vessel's contact with that BODY is the platform's
        /// (its danger plate stings, its body prism breaks), so the core does not also kill it by distance. Set by the glue
        /// before each step; never set in the harness.</summary>
        public readonly bool[] PlatformBody;
        readonly Vector3[] _wander, _dirs;
        readonly float[] _cool, _theta;

        // ── the lattice ───────────────────────────────────────────────────────────────────────────
        public readonly int M;                       // 2 * half + 1
        readonly int _half;
        readonly int[] _occ;                         // site -> mass handle (-1 empty)
        readonly bool[] _blocked;
        readonly float[] _q;
        float[] _cement, _alarm, _scar, _tmp;
        readonly Dictionary<int, int> _siteOf = new();   // handle -> site (the colony's half of BuilderRegistry)
        readonly HashSet<int> _claimed = new(), _taken = new();
        readonly HashSet<int> _openBreach = new();
        readonly List<int> _scratch = new(256), _dead = new(64);
        readonly int[] _perm = new int[26];
        bool _alarmLive;

        // ── ledgers ───────────────────────────────────────────────────────────────────────────────
        public int Tick { get; private set; }
        public float Time { get; private set; }
        public int Placed, PlacedTrail, Pickups, CarryMoves, Queries, Repairs, RepairTrail, Kills, Starved, Births, Eaten;
        public float EatenVolume, Metabolised, BirthPaid;
        public float AlarmLevel { get; private set; }
        public int StingCount;
        /// <summary>(time, site) of every breach and every re-fill of a breached site (repair_stats).</summary>
        public readonly List<(float t, int site)> Cuts = new(), RepairLog = new();
        /// <summary>Deaths this tick (read and cleared by the caller).</summary>
        public readonly List<BuilderDeath> Deaths = new();
        /// <summary>Workers born this tick.</summary>
        public readonly List<int> Born = new();
        /// <summary>(vessel id) of every sting this tick.</summary>
        public readonly List<int> Stings = new();
        /// <summary>Prisms placed this tick (handles) - the glue registers them.</summary>
        public readonly List<int> PlacedThisTick = new();

        public BuilderColonyCore(IBuilderWorld world, BuilderColonyParams p, Vector3 anchor, int domain, int colonyId, int seed)
        {
            _world = world; P = p; Anchor = anchor; Domain = domain; ColonyId = colonyId;
            _rng = new BuilderRng(seed + 991);
            Cap = Math.Max(1, Math.Max(p.Founders, p.MaxWorkers));
            Pos = new Vector3[Cap]; Vel = new Vector3[Cap]; Alive = new bool[Cap];
            Carry = new int[Cap]; Goal = new int[Cap];
            Intent = new float[Cap]; Stomach = new float[Cap]; BornAt = new float[Cap]; PlatformBody = new bool[Cap];
            _wander = new Vector3[Cap]; _dirs = new Vector3[Cap]; _cool = new float[Cap]; _theta = new float[Cap];
            for (int k = 0; k < Cap; k++)
            {
                Carry[k] = -1; Goal[k] = -1;
                _wander[k] = _rng.Normal3();
                _dirs[k] = _rng.OnSphere();
                _theta[k] = p.DefendCaste > 0f ? _rng.Uniform() / Math.Max(p.DefendCaste, 1e-6f) : 0f;
            }
            for (int k = 0; k < Math.Min(p.Founders, Cap); k++)
            {
                Alive[k] = true;
                Pos[k] = anchor + _rng.Normal3(20f);
                Stomach[k] = p.Stomach != null ? p.Stomach.Capacity * (p.Stomach.FounderFill + (1f - p.Stomach.FounderFill) * _rng.Uniform()) : 0f;
                BornAt[k] = -10f;
            }

            _half = Math.Max(2, p.LatticeHalf);
            M = 2 * _half + 1;
            int n = M * M * M;
            _occ = new int[n]; _blocked = new bool[n]; _q = new float[n];
            _cement = new float[n]; _alarm = new float[n]; _scar = new float[n]; _tmp = new float[n];
            for (int i = 0; i < n; i++)
            {
                _occ[i] = -1;
                Unpack(i, out int a, out int b, out int c);
                float r = MathF.Sqrt((a - _half) * (a - _half) + (b - _half) * (b - _half) + (c - _half) * (c - _half)) * p.Spacing;
                float z = (r - p.Rc) / p.W;
                _q[i] = MathF.Exp(-z * z);
                _blocked[i] = r < p.Rc * p.CoreFraction;
            }
        }

        // ── lattice addressing ─────────────────────────────────────────────────────────────────────
        int Pack(int a, int b, int c) => (a * M + b) * M + c;

        void Unpack(int i, out int a, out int b, out int c)
        {
            c = i % M; int r = i / M; b = r % M; a = r / M;
        }

        bool Inside(int a, int b, int c) => a >= 0 && b >= 0 && c >= 0 && a < M && b < M && c < M;

        void SiteOf(Vector3 p, out int a, out int b, out int c)
        {
            var g = (p - Anchor) / P.Spacing;
            a = (int)MathF.Round(g.X) + _half; b = (int)MathF.Round(g.Y) + _half; c = (int)MathF.Round(g.Z) + _half;
        }

        /// <summary>World position of a site (anchor + s * (site - half)).</summary>
        public Vector3 SitePosition(int site)
        {
            Unpack(site, out int a, out int b, out int c);
            return Anchor + new Vector3(a - _half, b - _half, c - _half) * P.Spacing;
        }

        bool Free(int a, int b, int c)
        {
            if (!Inside(a, b, c)) return false;
            int i = Pack(a, b, c);
            return _occ[i] < 0 && !_blocked[i];
        }

        int Count26(int a, int b, int c)
        {
            int n = 0;
            for (int o = 0; o < 26; o++)
            {
                var d = N26[o];
                int x = a + (int)d.X, y = b + (int)d.Y, z = c + (int)d.Z;
                if (Inside(x, y, z) && _occ[Pack(x, y, z)] >= 0) n++;
            }
            return n;
        }

        // ── read surface ──────────────────────────────────────────────────────────────────────────
        public int Built => _siteOf.Count;
        public int AliveCount { get { int n = 0; for (int k = 0; k < Cap; k++) if (Alive[k]) n++; return n; } }
        public int OpenWounds => _openBreach.Count;
        public bool IsBuilt(int h) => _siteOf.ContainsKey(h);
        public bool IsCarried(int h) => _taken.Contains(h) && !_siteOf.ContainsKey(h);
        public float StomachTotal { get { float s = 0; for (int k = 0; k < Cap; k++) if (Alive[k]) s += Stomach[k]; return s; } }
        public IEnumerable<int> BuiltHandles => _siteOf.Keys;
        public bool Striking(int k) => Alive[k] && Intent[k] >= 1f;

        // ── the ecology LOD (round 11f-2, Docs/ECOLOGY_LOD.md §6.2) ────────────────────────────────────────
        public RoostBug RoostBug;
        /// <summary>
        /// Set by the glue while it asks to collapse: a sated idle worker drops its forage goal and takes no new one (a hungry
        /// one still eats), so the carriers finish depositing and the colony reaches <see cref="CanRoost"/>. Nothing dies.
        /// </summary>
        public bool WindDown;
        float RoostRate => P.Stomach != null ? P.Stomach.Torpor : 0f;
        /// <summary>The colony may roost: nothing carried (a carried prism is real mass in a worker's jaws), no striker.</summary>
        public bool CanRoost
        {
            get
            {
                for (int k = 0; k < Cap; k++) if (Alive[k] && (Carry[k] >= 0 || Intent[k] >= 1f)) return false;
                return true;
            }
        }
        /// <summary>Seconds until the emptiest stomach is empty while roosting.</summary>
        public float RoostSecondsLeft => BuilderRoost.SecondsLeft(Alive, Stomach, Cap, RoostRate);
        /// <summary>One macro tick of a collapsed colony: torpor only; nothing moves, builds, breeds or dies.</summary>
        public void Roost(float dt)
        {
            Time += dt;
            Metabolised += BuilderRoost.Burn(Alive, Stomach, Cap, RoostRate, dt, RoostBug);
        }

        /// <summary>A site index as lattice coordinates relative to the anchor (BuilderRegistry's integer address).</summary>
        public void SiteCoords(int site, out int x, out int y, out int z)
        {
            Unpack(site, out x, out y, out z);
            x -= _half; y -= _half; z -= _half;
        }

        /// <summary>Points of the built structure (the shape metrics, the harness's scar measure).</summary>
        public void BuiltPositions(List<Vector3> into)
        {
            into.Clear();
            foreach (var kv in _siteOf) into.Add(SitePosition(kv.Value));
        }

        // ── the one predicate (PORT.md §1 IsStealableForMe) ─────────────────────────────────────────
        /// <summary>
        /// Loose, live, unshielded, not this colony's domain, not built by ANY colony, not already being fetched,
        /// inside the band. Shielded mass is never a target: a target you cannot take is a feed-hold you never finish.
        /// </summary>
        public bool IsStealableForMe(int h)
        {
            if (!_world.Alive(h) || _world.Shielded(h)) return false;
            if (P.SkipOwnDomain && _world.Domain(h) == Domain) return false;
            if (_taken.Contains(h) || _claimed.Contains(h)) return false;
            if (!_world.Loose(h)) return false;
            if (P.BandOuter > 0f)
            {
                float r = Vector3.Distance(_world.Position(h), P.CellCentre);
                if (r < P.BandInner || r > P.BandOuter) return false;
            }
            return true;
        }

        int ForageTarget(int k)
        {
            Queries++;
            int n = _world.QuerySphere(Pos[k], P.Sense, _scratch);
            int best = -1; float bd = float.MaxValue;
            for (int j = 0; j < n; j++)
            {
                int h = _scratch[j];
                if (!IsStealableForMe(h)) continue;
                float d = Vector3.DistanceSquared(_world.Position(h), Pos[k]) * (_world.IsTrail(h) ? P.TrailPreference : 1f);
                if (d < bd) { best = h; bd = d; }
            }
            return best;
        }

        // ── the local rule ────────────────────────────────────────────────────────────────────────
        float DepositScore(int a, int b, int c)
        {
            int i = Pack(a, b, c);
            float p;
            float q = _q[i];
            int nb = Count26(a, b, c);
            if (q < 0.05f) p = 0f;
            else
            {
                float cm = _cement[i];
                p = nb == 0 ? q * P.Nucleate : q * (0.05f + cm * cm / (cm * cm + P.KCement * P.KCement));
            }
            if (P.ScarGain > 0f && _scar[i] > 0.05f && nb > 0 && !_blocked[i])
                p = MathF.Max(p, MathF.Min(1f, P.ScarGain * _scar[i]) * 0.5f);
            if (p <= 0f) return p;
            if (P.Mend == BuilderMendRule.Gap || P.Mend == BuilderMendRule.Both)
                p *= 1f + P.GapGain * Math.Max(0, nb - 4) / 6f;
            return MathF.Min(1f, p);
        }

        Vector3 Home(int k)
        {
            if (P.Mend == BuilderMendRule.Alarm || P.Mend == BuilderMendRule.Both)
            {
                SiteOf(Pos[k], out int a, out int b, out int c);
                if (Inside(a, b, c) && _alarm[Pack(a, b, c)] > 0.02f)
                {
                    // climb the local alarm gradient (N26[::3] - nine probes, one lattice step)
                    int best = -1; float bv = _alarm[Pack(a, b, c)];
                    for (int o = 0; o < 26; o += 3)
                    {
                        var d = N26[o];
                        int x = a + (int)d.X, y = b + (int)d.Y, z = c + (int)d.Z;
                        if (!Inside(x, y, z)) continue;
                        int q = Pack(x, y, z);
                        if (_alarm[q] > bv) { best = q; bv = _alarm[q]; }
                    }
                    return SitePosition(best >= 0 ? best : Pack(a, b, c));
                }
            }
            // a laden worker walks to "its" bearing on the shell; the bearing drifts (a random walk, not a plan)
            var dd = _dirs[k] + _rng.Normal3(0.05f);
            float l = dd.Length();
            _dirs[k] = l > 1e-6f ? dd / l : _dirs[k];
            return Anchor + _dirs[k] * P.Rc;
        }

        void Steer(int k, Vector3 target, float dt, float speed)
        {
            var d = target - Pos[k];
            float n = d.Length();
            var v = d / MathF.Max(n, 1e-6f) * MathF.Min(speed, n / MathF.Max(dt, 1e-6f));
            Vel[k] = 0.7f * Vel[k] + 0.3f * v;
        }

        bool TryDeposit(int k)
        {
            SiteOf(Pos[k], out int a, out int b, out int c);
            for (int i = 0; i < 26; i++) _perm[i] = i;
            int bestA = 0, bestB = 0, bestC = 0; float bp = 0f; bool found = false;
            for (int s = 0; s < 8; s++)
            {
                int j = s + _rng.Range(26 - s);
                (_perm[s], _perm[j]) = (_perm[j], _perm[s]);
                var d = N26[_perm[s]];
                int x = a + (int)d.X, y = b + (int)d.Y, z = c + (int)d.Z;
                if (!Free(x, y, z)) continue;
                float p = DepositScore(x, y, z);
                if (p > bp) { bp = p; bestA = x; bestB = y; bestC = z; found = true; }
            }
            if (!found || _rng.Uniform() >= bp) return false;
            int site = Pack(bestA, bestB, bestC);
            var at = SitePosition(site);
            // claim-before-place: a site something else already occupies (a loose prism parked there, another
            // colony's claim) is not ours this tick - the worker tries again next step
            if (P.ReserveClear > 0f && !_world.TryReserve(at, P.ReserveClear)) return false;
            int h = Carry[k];
            _world.Settle(h, _world.Position(h), at, P.SettleSeconds);
            _occ[site] = h; _siteOf[h] = site;
            _world.SetBuilt(h, ColonyId, site, true);
            PlacedThisTick.Add(h);
            Carry[k] = -1; Placed++;
            bool trail = _world.IsTrail(h);
            if (trail) PlacedTrail++;
            _cement[site] += 1f;
            if (_openBreach.Remove(site))
            {
                Repairs++; RepairLog.Add((Time, site));
                if (trail) RepairTrail++;
            }
            return true;
        }

        // ── upkeep: a breached site frees itself and deposits alarm ─────────────────────────────────
        void SweepBreaches()
        {
            _dead.Clear();
            foreach (var kv in _siteOf)
            {
                int h = kv.Key;
                // breached when its prism was destroyed OR changed hands (a vessel stole it back) - two verbs, one rule
                if (!_world.Alive(h) || _world.Domain(h) != Domain) _dead.Add(h);
            }
            for (int i = 0; i < _dead.Count; i++)
            {
                int h = _dead[i];
                int site = _siteOf[h];
                _siteOf.Remove(h); _taken.Remove(h);
                if (_occ[site] == h) _occ[site] = -1;
                _world.SetBuilt(h, ColonyId, site, false);
                Cuts.Add((Time, site)); _openBreach.Add(site);
                if (P.Mend == BuilderMendRule.Alarm || P.Mend == BuilderMendRule.Both)
                {
                    _alarm[site] += P.AlarmGain;
                    _alarmLive = true;
                }
            }
        }

        /// <summary>One 6-neighbour blur: out = keep * (self * a + nb / 6 * sum6).</summary>
        void Diffuse(ref float[] a, float self, float nbw, float keep, out float max)
        {
            var o = _tmp; max = 0f;
            int m = M, m2 = M * M;
            for (int x = 0; x < m; x++)
            for (int y = 0; y < m; y++)
            for (int z = 0; z < m; z++)
            {
                int i = (x * m + y) * m + z;
                float s = 0f;
                if (x > 0) s += a[i - m2];
                if (x < m - 1) s += a[i + m2];
                if (y > 0) s += a[i - m];
                if (y < m - 1) s += a[i + m];
                if (z > 0) s += a[i - 1];
                if (z < m - 1) s += a[i + 1];
                float v = keep * (self * a[i] + nbw / 6f * s);
                o[i] = v;
                if (v > max) max = v;
            }
            _tmp = a; a = o;
        }

        // ── defence (Colony.defend) ───────────────────────────────────────────────────────────────
        void Defend(BuilderVessel[] vessels, int vesselCount, float dt)
        {
            for (int k = 0; k < Cap; k++) { _cool[k] -= dt; Intent[k] = 0f; }
            float cap = P.DefendCaste > 0f ? 1f / P.DefendCaste : 1f;
            int inside = -1; float insideD = float.MaxValue; int any = -1; float anyD = float.MaxValue;
            for (int v = 0; v < vesselCount; v++)
            {
                float d = Vector3.Distance(vessels[v].Pos, Anchor);
                if (d < anyD) { anyD = d; any = v; }
                if (d < P.AlarmRadius && d < insideD) { insideD = d; inside = v; }
            }
            // the stimulus keeps ACCUMULATING while an intruder stays (up to 1/caste): a pass-through recruits the
            // low-threshold few, a siege recruits everyone - escalating recruitment, not a fixed caste
            AlarmLevel = inside >= 0 ? MathF.Min(cap, AlarmLevel + P.AlarmRise * dt) : MathF.Max(0f, AlarmLevel - P.AlarmFall * dt);
            if (AlarmLevel <= 0f || any < 0) return;
            var pv = vessels[inside >= 0 ? inside : any];
            var to = BuilderMath.Unit(pv.Pos - Anchor);
            float guard = P.AlarmRadius * 0.5f;
            for (int k = 0; k < Cap; k++)
            {
                if (!Alive[k] || Carry[k] >= 0) continue;
                if (Vector3.Distance(Pos[k], Anchor) >= P.AlarmRadius * 2.2f) continue;
                if (P.DefendCaste > 0f && _theta[k] >= AlarmLevel) continue;   // response thresholds
                if (AlarmLevel < P.StrikeAt)
                {
                    // screen: a loose shell between core and intruder (each worker its own offset on the screen)
                    var off = _wander[k] - to * Vector3.Dot(_wander[k], to);
                    Steer(k, Anchor + to * guard + off * guard * 0.6f, dt, P.Speed * 1.4f);
                    Intent[k] = 0.25f + 0.5f * AlarmLevel / P.StrikeAt;
                }
                else
                {
                    Steer(k, pv.Pos + pv.Vel * 0.25f, dt, P.Speed * 1.8f);
                    Intent[k] = 1f;
                    if (Vector3.Distance(Pos[k], pv.Pos) < pv.Radius + P.StingReach && _cool[k] <= 0f)
                    {
                        Stings.Add(pv.Id); StingCount++; _cool[k] = P.StingCooldown;
                    }
                }
            }
        }

        // ── deaths ────────────────────────────────────────────────────────────────────────────────
        /// <summary>
        /// Worker <paramref name="k"/> died (a vessel rammed it, the platform killed its proxy, or it starved): whatever
        /// it carried falls loose where it is (the prism stays in the colony's domain - mass conserved, nothing pops).
        /// </summary>
        public void Kill(int k, int vessel)
        {
            if (k < 0 || k >= Cap || !Alive[k]) return;
            Alive[k] = false;
            if (vessel != BuilderDeath.StarvedBy) Kills++; else Starved++;
            if (Carry[k] >= 0) { _taken.Remove(Carry[k]); _world.Drop(Carry[k]); Carry[k] = -1; }
            if (Goal[k] >= 0) { _claimed.Remove(Goal[k]); Goal[k] = -1; }
            Deaths.Add(new BuilderDeath { Agent = k, Vessel = vessel, At = Pos[k], Stomach = Stomach[k] });
        }

        void Contacts(BuilderVessel[] vessels, int vesselCount)
        {
            for (int v = 0; v < vesselCount; v++)
            {
                if (!vessels[v].Rams) continue;
                float reach = vessels[v].Radius + P.WorkerRadius + P.RamExtra;
                for (int k = 0; k < Cap; k++)
                {
                    if (!Alive[k]) continue;
                    // ramming a CARRIER (its body or the prism it holds) knocks the prism loose and kills it
                    bool hit = !PlatformBody[k] && Vector3.DistanceSquared(Pos[k], vessels[v].Pos) < reach * reach;
                    if (!hit && Carry[k] >= 0)
                        hit = Vector3.Distance(_world.Position(Carry[k]), vessels[v].Pos) < vessels[v].Radius + 2f;
                    if (hit) Kill(k, vessels[v].Id);
                }
            }
        }

        void Metabolise(int k, float dt)
        {
            var s = P.Stomach;
            if (s == null) return;
            float burn = MathF.Min(Stomach[k], s.Metabolism * dt);
            Stomach[k] -= burn; Metabolised += burn;
            if (Stomach[k] <= 0f) Kill(k, BuilderDeath.StarvedBy);   // starvation: only an EMPTY stomach kills, never a clock
        }

        bool Hungry(int k) => P.Stomach != null && Stomach[k] < P.Stomach.Capacity * P.Stomach.HungryBelow;

        void MaybeBreed(int k)
        {
            var s = P.Stomach;
            if (s == null || Stomach[k] < s.Capacity * s.BirthAbove || Stomach[k] < s.BirthCost) return;
            int slot = -1, alive = 0;
            for (int j = 0; j < Cap; j++) { if (Alive[j]) alive++; else if (slot < 0) slot = j; }
            if (slot < 0 || alive >= P.MaxWorkers) return;   // production gating at the cap (not a cull)
            Stomach[k] -= s.BirthCost; BirthPaid += s.BirthCost;
            Alive[slot] = true; Pos[slot] = Pos[k] + _rng.Normal3(2f); Vel[slot] = Vector3.Zero;
            Carry[slot] = -1; Goal[slot] = -1; Intent[slot] = 0f; _cool[slot] = 0f;
            Stomach[slot] = s.BirthCost * 0.5f;   // half the bill is the newborn's stomach, half its body
            BornAt[slot] = Time;
            Births++; Born.Add(slot);
        }

        // ── one step ──────────────────────────────────────────────────────────────────────────────
        public void Step(float dt, BuilderVessel[] vessels, int vesselCount)
        {
            Tick++; Time += dt;
            Deaths.Clear(); Born.Clear(); Stings.Clear(); PlacedThisTick.Clear();
            for (int k = 0; k < Cap; k++)
            {
                if (!Alive[k]) continue;
                bool refresh = (k + Tick) % Math.Max(1, P.ForageFraction) == 0;
                if (Carry[k] >= 0)
                {
                    int c = Carry[k];
                    if (!_world.Alive(c) || _world.Domain(c) != Domain)   // destroyed (or taken back) in our jaws
                    {
                        _taken.Remove(c); Carry[k] = -1; _world.Drop(c);
                        continue;
                    }
                    Steer(k, Home(k), dt, P.Speed);
                    TryDeposit(k);
                }
                else
                {
                    int g = Goal[k];
                    if (g >= 0 && (!_world.Alive(g) || _taken.Contains(g) || (WindDown && !Hungry(k))))
                    {
                        _claimed.Remove(g); Goal[k] = g = -1;
                    }
                    if (g < 0 && refresh && (!WindDown || Hungry(k)))
                    {
                        g = ForageTarget(k);
                        if (g >= 0) { Goal[k] = g; _claimed.Add(g); }
                    }
                    if (g >= 0)
                    {
                        var gp = _world.Position(g);
                        Steer(k, gp, dt, P.Speed);
                        if (Vector3.Distance(gp, Pos[k]) < P.PickupReach)
                        {
                            _claimed.Remove(g); Goal[k] = -1;
                            if (Hungry(k))
                            {
                                // a hungry worker EATS what it would have carried (the food web's one down-force)
                                float v = _world.Consume(g, Pos[k]);
                                if (v > 0f) { Stomach[k] += v; EatenVolume += v; Eaten++; }
                            }
                            else if (!WindDown && _world.Steal(g, Domain))   // changes hands (refused if shielded)
                            {
                                Carry[k] = g; _taken.Add(g); Pickups++;
                            }
                        }
                    }
                    else
                    {
                        if (refresh)
                        {
                            var w = _wander[k] + _rng.Normal3(0.5f);
                            float l = w.Length();
                            _wander[k] = l > 1e-6f ? w / l : _wander[k];
                        }
                        Steer(k, Pos[k] + _wander[k] * 50f, dt, P.Speed * 0.6f);
                    }
                }
            }

            // behave: upkeep, cement, defence, alarm
            SweepBreaches();
            if (Tick % 5 == 0) Diffuse(ref _cement, 0.8f, 0.2f, 0.97f, out _);
            Defend(vessels, vesselCount, dt);
            if (Tick % 3 == 0 && _alarmLive)
            {
                Diffuse(ref _alarm, 0.5f, 0.5f, 0.95f, out float max);
                _alarmLive = max > 1e-3f;
                if (P.ScarGain > 0f)
                    for (int i = 0; i < _scar.Length; i++) _scar[i] = MathF.Max(_scar[i] * 0.999f, _alarm[i]);
            }

            // integrate + containment
            float rmax = P.Containment;
            for (int k = 0; k < Cap; k++)
            {
                if (!Alive[k]) continue;
                var p = Pos[k] + Vel[k] * dt;
                var rel = p - P.CellCentre;
                float r = rel.Length();
                if (r > rmax) p = P.CellCentre + rel * (rmax / r);
                Pos[k] = p;
            }
            // carried prisms ride their carrier (live gameplay data: the mover contract, one write each)
            var drop = new Vector3(0f, -P.CarryDrop, 0f);
            for (int k = 0; k < Cap; k++)
            {
                if (!Alive[k] || Carry[k] < 0) continue;
                var tgt = Pos[k] + drop;
                if (Vector3.DistanceSquared(_world.Position(Carry[k]), tgt) > 0.25f)   // a carrier at rest costs nothing
                {
                    _world.Carry(Carry[k], tgt); CarryMoves++;
                }
            }
            Contacts(vessels, vesselCount);
            for (int k = 0; k < Cap; k++)
            {
                if (!Alive[k]) continue;
                Metabolise(k, dt);
                if (Alive[k]) MaybeBreed(k);
            }
        }

        /// <summary>
        /// The research's repair_stats: from the first cut in [cutT, cutT + 8 s) until 50% / 90% of the sites cut in that
        /// pass are re-filled by ANY prism (the colony does not remember which prism was where). NaN = never reached.
        /// </summary>
        public (int cut, int refilled, float t50, float t90) RepairStats(float cutT)
        {
            var sites = new HashSet<int>(); float t0 = float.MaxValue;
            foreach (var (t, s) in Cuts)
                if (t >= cutT && t < cutT + 8f) { sites.Add(s); t0 = MathF.Min(t0, t); }
            if (sites.Count == 0) return (0, 0, float.NaN, float.NaN);
            var filled = new Dictionary<int, float>();
            foreach (var (t, s) in RepairLog)
                if (sites.Contains(s) && !filled.ContainsKey(s) && t >= t0) filled[s] = t;
            var ts = new List<float>(filled.Values); ts.Sort();
            int n50 = (int)MathF.Ceiling(0.5f * sites.Count), n90 = (int)MathF.Ceiling(0.9f * sites.Count);
            float t50 = ts.Count >= n50 ? ts[n50 - 1] - t0 : float.NaN;
            float t90 = ts.Count >= n90 ? ts[n90 - 1] - t0 : float.NaN;
            return (sites.Count, filled.Count, t50, t90);
        }
    }
}
