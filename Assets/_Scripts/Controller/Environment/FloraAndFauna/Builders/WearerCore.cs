// The WEARER (Docs/BUILDERS_AND_THIEVES.md §10; research Tools/Ecology/builders/wearers.py, PORT.md §4 "the showpiece",
// DISCOVERIES "Species 4 - wearers" + round 2 "satiation moult"): a colony of small HEARTS that steal prisms - mostly a
// pilot's trail - and WEAR them as a body. Every theft makes a body bigger; bodies that touch FUSE into one creature;
// past a size the thing skulking behind your trail turns round, REARS (the body contracts - the telegraph) and LUNGES
// with its body's prisms turned to danger. A creature made of what it took.
//
// Pure C# over System.Numerics (no UnityEngine): the same file compiles and RUNS in Tools/Build/builders_harness.
//
// Port shape (PORT.md §4):
//   * body growth is a local attachment rule on the creature's own lattice (s = 6 u, the heart at the origin): a new
//     prism attaches to a free site touching the body with weight nb^-alpha, times a CONTACT term - it sticks near
//     where it touched (research round 1 negative: drawn from the whole frontier, every silhouette was one blob);
//   * a worn prism is parented under ONE body per creature (IWearWorld.Wear) - the body moves as ONE pose per tick
//     (PoseBody), and the glue moves every worn prism's index point and render matrix in one batched pass a frame;
//   * a body CAP (150 prisms): over it the creature sheds its outermost prisms where they hang into a static LAIR
//     (a built structure never moves - cheap) and the shed mass pays for a newborn heart (production gating, not a cull);
//   * the HURT MOULT (game): a body that loses a sixth of itself in a couple of seconds sheds its outer layer BACK to
//     whoever it was stolen from and slinks off - hurting it returns your mass;
//   * contact is the body itself: a ramming pilot STRIPS every worn prism it touches back to its own domain (it falls
//     loose, nothing destroyed); a heart with fewer than 3 body prisms around it is exposed and dies to a touch.
//
// Ecology laws: no prism is created or destroyed here (eating is Consume; stripping and moulting change hands); a heart
// dies to a vessel, a weapon, a predator, or an EMPTY stomach (it eats its own body first); births are paid in volume.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>What a wearer needs beyond <see cref="IBuilderWorld"/>: a body that carries prisms as ONE thing.</summary>
    public interface IWearWorld : IBuilderWorld
    {
        /// <summary>Prism <paramref name="h"/> joins creature <paramref name="creature"/>'s body at <paramref name="local"/>
        /// (body-frame offset, unsquashed). A prism already worn by another body moves to this one (a fusion).</summary>
        void Wear(int h, int creature, Vector3 local);
        /// <summary>The prism leaves its body where it hangs now (the next owner decides its domain).</summary>
        void Unwear(int h);
        /// <summary>Once per tick per creature with a body: ONE pose (position, frame rows, squash) - the whole body follows.</summary>
        void PoseBody(int creature, Vector3 pos, Vector3 right, Vector3 up, Vector3 forward, float squash);
        /// <summary>The body's prisms turn to the danger tier (the lunge) or back. Event-driven, never per frame.</summary>
        void SetBodyDanger(int creature, bool danger);
    }

    public sealed class WearerParams
    {
        public int Founders = 16;               // GAME: research seeded 40; one creature for the demo cell
        public int MaxHearts = 24;              // backstop (births stop at it; nothing is culled)
        public float S = 6f;                    // body lattice spacing
        public float Alpha = 1f;                // attachment exponent nb^-alpha (spiky arms > 0 > compact blob)
        public float Contact = 0.5f;            // contact-attachment temperature (run_mixed wearers_v3: contact 0.5)
        public int HuntAt = 60;                 // GAME: body prisms at which a thief becomes a hunter (research V_hunt 600 vol / ~10 per worn prism)
        public float Speed = 95f, Slow = 0.15f; // speed falls with size: Speed * (1 + n / HuntAt)^-Slow
        public float Windup = 1f, Lunge = 2.2f, RearAt = 140f;
        public bool Intercept = true;
        public float Sense = 180f;
        public float TrailPreference = 0.15f;   // squared-distance multiplier for a pilot's trail (research 0.15)
        public float KeepOff = 90f;             // never steal closer than this to the pilot (a THIEF skulks)
        public float FleeRange = 70f;           // a thief turned on flees
        public int ExposedAt = 3;
        public float StripReach = 5f;           // a ramming pilot strips worn prisms within its radius + this
        public bool Fuse = true;
        public bool Oriented = true;            // heading-aligned body frame (research: reads better, free)
        public int BodyCap = 150;               // research v3 (satiation moult)
        public int WornCap = 300;               // GAME: worn prisms across the whole colony (the moving-prism budget; thieves stop stealing at it)
        public float Keep = 0.6f;               // a satiation moult keeps keep * cap prisms
        public float HurtWindow = 2f;           // GAME: the hurt moult's memory (s)
        public float HurtFraction = 1f / 6f;    // GAME: losing this share of the body inside the window ...
        public float HurtShed = 0.3f;           // ... sheds this share of what is left back to its owners
        public float HeartSize = 2f;
        public float Containment = 1140f;
        public Vector3 CellCentre;
        public BuilderStomachParams Stomach = new()
        {
            Capacity = 30f, FounderFill = 0.6f, Metabolism = 0.01f, Torpor = 0.01f,
            HungryBelow = 0.3f, BirthAbove = 0.9f, BirthCost = 10f,
        };
    }

    public sealed class WearerCore
    {
        public const int Thief = 0, Approach = 1, Rear = 2, LungePhase = 3, Recover = 4;

        readonly WearerParams P;
        readonly IWearWorld _world;
        readonly BuilderRng _rng;
        public readonly int Domain, ColonyId, Cap;

        // hearts (struct of arrays)
        public readonly Vector3[] Pos, Vel;
        public readonly bool[] Alive;
        public readonly int[] Leader, Goal, Phase;
        public readonly float[] Intent, Stomach, BornAt, Squash;
        /// <summary>GAME: heart k has a real proxy (collider on) - the platform takes a vessel's contact with it.</summary>
        public readonly bool[] PlatformBody;
        readonly Vector3[] _offset, _wander, _right, _up, _fwd, _lungeDir;
        readonly float[] _ptime, _hurt;
        readonly bool[] _danger;

        // bodies: leader -> (site -> handle), frontier (site -> occupied-neighbour count); handle -> (leader, site)
        readonly Dictionary<int, int>[] _body, _frontier;
        readonly Dictionary<int, (int leader, int site)> _wornBy = new();
        readonly HashSet<int> _claimed = new();
        readonly List<int> _lair = new();
        readonly List<int> _q = new(256), _scratch = new(256), _sites = new(1024);
        readonly List<float> _w = new(1024);

        public int Tick { get; private set; }
        public float Time { get; private set; }
        // ledgers
        public int WornSteals, WornTrail, Fusions, Splits, Kills, Starved, Births, Eaten, Stripped, Queries;
        public int SatiationMoults, HurtMoults, Shed, Returned, Lunges, Hits;
        public float StrippedVolume, EatenVolume, ReturnedVolume, Metabolised, BirthPaid, MaxTelegraph, MinTelegraph = float.MaxValue;
        public int BodyPoses, MaxBody;
        public readonly List<BuilderDeath> Deaths = new();
        public readonly List<int> Born = new();
        /// <summary>(heart, vessel id) of every lunge that touched its pilot this tick.</summary>
        public readonly List<(int heart, int vessel)> Struck = new();

        public WearerCore(IWearWorld world, WearerParams p, Vector3 anchor, int domain, int colonyId, int seed)
        {
            _world = world; P = p; Domain = domain; ColonyId = colonyId;
            _rng = new BuilderRng(seed + 7331);
            Cap = Math.Max(1, Math.Max(p.Founders, p.MaxHearts));
            Pos = new Vector3[Cap]; Vel = new Vector3[Cap]; Alive = new bool[Cap];
            Leader = new int[Cap]; Goal = new int[Cap]; Phase = new int[Cap];
            Intent = new float[Cap]; Stomach = new float[Cap]; BornAt = new float[Cap]; Squash = new float[Cap];
            PlatformBody = new bool[Cap];
            _offset = new Vector3[Cap]; _wander = new Vector3[Cap]; _right = new Vector3[Cap]; _up = new Vector3[Cap];
            _fwd = new Vector3[Cap]; _lungeDir = new Vector3[Cap];
            _ptime = new float[Cap]; _hurt = new float[Cap]; _danger = new bool[Cap];
            _body = new Dictionary<int, int>[Cap]; _frontier = new Dictionary<int, int>[Cap];
            for (int k = 0; k < Cap; k++)
            {
                _body[k] = new Dictionary<int, int>();
                _frontier[k] = new Dictionary<int, int>();
                Leader[k] = k; Goal[k] = -1; Squash[k] = 1f;
                _right[k] = Vector3.UnitX; _up[k] = Vector3.UnitY; _fwd[k] = Vector3.UnitZ;
                _wander[k] = _rng.OnSphere();
                ResetFrontier(k);
            }
            for (int k = 0; k < Math.Min(p.Founders, Cap); k++)
            {
                Alive[k] = true;
                Pos[k] = anchor + _rng.Normal3(40f);
                Stomach[k] = p.Stomach.Capacity * (p.Stomach.FounderFill + (1f - p.Stomach.FounderFill) * _rng.Uniform());
                BornAt[k] = -10f;
            }
        }

        // ── read surface ──────────────────────────────────────────────────────────────────────────
        public int AliveCount { get { int n = 0; for (int k = 0; k < Cap; k++) if (Alive[k]) n++; return n; } }
        public float StomachTotal { get { float s = 0f; for (int k = 0; k < Cap; k++) if (Alive[k]) s += Stomach[k]; return s; } }
        public bool IsLeader(int k) => Alive[k] && Leader[k] == k;
        public int BodyCount(int k) => _body[k].Count;
        public int WornTotal => _wornBy.Count;
        public int LairCount => _lair.Count;
        public IReadOnlyList<int> Lair => _lair;
        public bool IsWorn(int h) => _wornBy.ContainsKey(h);
        /// <summary>A lunging creature's hearts strike (drawn in the danger tier, like a fortress striker).</summary>
        public bool Striking(int k) => Alive[k] && Phase[Leader[k]] == LungePhase;

        // ── the ecology LOD (round 11f-2, Docs/ECOLOGY_LOD.md §6.2) ────────────────────────────────────────
        public RoostBug RoostBug;
        float RoostRate => P.Stomach != null ? P.Stomach.Torpor : 0f;
        /// <summary>The wearers may roost only as THIEVES: no creature approaching, rearing, lunging or recovering (a body in
        /// a hunt is moving its worn prisms at a pilot). Worn prisms simply hold still on a still body.</summary>
        public bool CanRoost
        {
            get
            {
                for (int k = 0; k < Cap; k++) if (Alive[k] && Phase[Leader[k]] != Thief) return false;
                return true;
            }
        }
        public float RoostSecondsLeft => BuilderRoost.SecondsLeft(Alive, Stomach, Cap, RoostRate);
        /// <summary>One macro tick of collapsed wearers: torpor only; no body grows, moults, fuses, splits or dies.</summary>
        public void Roost(float dt)
        {
            Time += dt;
            Metabolised += BuilderRoost.Burn(Alive, Stomach, Cap, RoostRate, dt, RoostBug);
        }
        public int Creatures { get { int n = 0; for (int k = 0; k < Cap; k++) if (IsLeader(k) && _body[k].Count > 0) n++; return n; } }
        public int Largest { get { int b = 0; for (int k = 0; k < Cap; k++) if (IsLeader(k)) b = Math.Max(b, _body[k].Count); return b; } }
        public float Radius(int k) => P.S * (1f + MathF.Cbrt(_body[k].Count));
        /// <summary>Every worn handle of leader k (diagnostics, the harness's checks).</summary>
        public IEnumerable<int> BodyOf(int k) => _body[k].Values;

        // ── lattice ───────────────────────────────────────────────────────────────────────────────
        static int PackSite(int x, int y, int z) => (x + 64) | ((y + 64) << 7) | ((z + 64) << 14);
        static void UnpackSite(int s, out int x, out int y, out int z) { x = (s & 127) - 64; y = ((s >> 7) & 127) - 64; z = ((s >> 14) & 127) - 64; }
        static readonly int[] N26 = BuildN26();

        static int[] BuildN26()
        {
            var n = new int[78]; int i = 0;
            for (int a = -1; a <= 1; a++) for (int b = -1; b <= 1; b++) for (int c = -1; c <= 1; c++)
                    if (a != 0 || b != 0 || c != 0) { n[i++] = a; n[i++] = b; n[i++] = c; }
            return n;
        }

        bool Occupied(int k, int site) => _body[k].ContainsKey(site) || site == PackSite(0, 0, 0);

        void ResetFrontier(int k)
        {
            var f = _frontier[k]; f.Clear();
            for (int i = 0; i < 78; i += 3) f[PackSite(N26[i], N26[i + 1], N26[i + 2])] = 1;   // touching the heart
        }

        void Occupy(int k, int site, int h)
        {
            _body[k][site] = h;
            _frontier[k].Remove(site);
            UnpackSite(site, out int x, out int y, out int z);
            for (int i = 0; i < 78; i += 3)
            {
                int ax = x + N26[i], ay = y + N26[i + 1], az = z + N26[i + 2];
                if (ax < -63 || ay < -63 || az < -63 || ax > 63 || ay > 63 || az > 63) continue;
                int q = PackSite(ax, ay, az);
                if (Occupied(k, q)) continue;
                _frontier[k].TryGetValue(q, out int nb);
                _frontier[k][q] = nb + 1;
            }
        }

        void Vacate(int k, int site)
        {
            if (!_body[k].Remove(site)) return;
            UnpackSite(site, out int x, out int y, out int z);
            int own = 0;
            for (int i = 0; i < 78; i += 3)
            {
                int q = PackSite(x + N26[i], y + N26[i + 1], z + N26[i + 2]);
                if (Occupied(k, q)) { own++; continue; }
                if (_frontier[k].TryGetValue(q, out int nb)) { if (nb <= 1) _frontier[k].Remove(q); else _frontier[k][q] = nb - 1; }
            }
            if (own > 0) _frontier[k][site] = own;
        }

        Vector3 Local(int site)
        {
            UnpackSite(site, out int x, out int y, out int z);
            return new Vector3(x, y, z) * P.S;
        }

        /// <summary>World position of a worn site (heart + frame * site * s * squash) - the research's wear_pos.</summary>
        public Vector3 WearPos(int k, int site)
        {
            var l = Local(site) * Squash[k];
            return Pos[k] + _right[k] * l.X + _up[k] * l.Y + _fwd[k] * l.Z;
        }

        /// <summary>Attach prism h to leader k's body: a frontier site with weight nb^-alpha x exp(-(d - dmin)/contact),
        /// d = the site's squared distance (in sites) from where the prism touched (DLA's arrival point).</summary>
        void Attach(int k, int h)
        {
            var f = _frontier[k];
            var rel = _world.Position(h) - Pos[k];
            var r = new Vector3(Vector3.Dot(rel, _right[k]), Vector3.Dot(rel, _up[k]), Vector3.Dot(rel, _fwd[k])) / P.S;
            _sites.Clear(); _w.Clear();
            float dmin = float.MaxValue;
            foreach (var kv in f)
            {
                UnpackSite(kv.Key, out int x, out int y, out int z);
                float d = Vector3.DistanceSquared(new Vector3(x, y, z), r);
                dmin = MathF.Min(dmin, d);
                _sites.Add(kv.Key); _w.Add(d);
            }
            if (_sites.Count == 0) return;
            float sum = 0f;
            for (int i = 0; i < _sites.Count; i++)
            {
                int nb = f[_sites[i]];
                float w = MathF.Pow(nb, -P.Alpha) * (P.Contact > 0f ? MathF.Exp(-(_w[i] - dmin) / P.Contact) : 1f);
                _w[i] = w; sum += w;
            }
            float u = _rng.Uniform() * sum;
            int pick = _sites.Count - 1;
            for (int i = 0; i < _sites.Count; i++) { u -= _w[i]; if (u <= 0f) { pick = i; break; } }
            int site = _sites[pick];
            Occupy(k, site, h);
            _wornBy[h] = (k, site);
            _world.Wear(h, k, Local(site));
            MaxBody = Math.Max(MaxBody, _body[k].Count);
        }

        /// <summary>Prism h leaves its body (stripped, shed, eaten, or dead) - the lattice and the world both forget it.</summary>
        void Detach(int h, bool unwear = true)
        {
            if (!_wornBy.TryGetValue(h, out var w)) return;
            _wornBy.Remove(h);
            Vacate(w.leader, w.site);
            if (unwear) _world.Unwear(h);
        }

        // ── the one predicate (BuilderColonyCore.IsStealableForMe, without a band) ───────────────────
        bool Stealable(int h) =>
            _world.Alive(h) && !_world.Shielded(h) && _world.Domain(h) != Domain && !_claimed.Contains(h) && _world.Loose(h);

        // ── step ──────────────────────────────────────────────────────────────────────────────────
        public void Step(float dt, BuilderVessel[] vessels, int count)
        {
            Tick++; Time += dt;
            Deaths.Clear(); Born.Clear(); Struck.Clear();
            SweepBodies(dt);
            for (int k = 0; k < Cap; k++)
            {
                if (!IsLeader(k)) continue;
                int n = _body[k].Count;
                float sp = P.Speed * MathF.Pow(1f + n / (float)Math.Max(1, P.HuntAt), -P.Slow);
                int t = Nearest(k, vessels, count, out float dist);
                float R = Radius(k);
                _ptime[k] += dt;
                bool hunting = n >= P.HuntAt && t >= 0;
                if (hunting && Phase[k] == Thief) { Phase[k] = Approach; _ptime[k] = 0f; }
                // a rear whose target left (or whose body fell below HuntAt) stands down - before 11f-2 it held Rear forever
                if (!hunting && (Phase[k] == Approach || Phase[k] == Recover || Phase[k] == Rear)) Phase[k] = Thief;
                Vector3 v;
                switch (Phase[k])
                {
                    case Thief:
                        v = Thieve(k, t >= 0 ? vessels[t] : default, t >= 0, dist, sp, R);
                        break;
                    case Approach:
                    {
                        var pv = vessels[t];
                        float lead = P.Intercept ? MathF.Min(3f, dist / MathF.Max(sp, 1e-6f)) : 0.3f;
                        v = BuilderMath.Unit(pv.Pos + pv.Vel * lead - Pos[k]) * sp;
                        Intent[k] = Math.Clamp(1f - (dist - R) / 250f, 0f, 0.45f);
                        if (dist < R + P.RearAt) { Phase[k] = Rear; _ptime[k] = 0f; }
                        break;
                    }
                    case Rear:
                    {
                        v = Vel[k] * 0.3f;
                        float f = MathF.Min(1f, _ptime[k] / P.Windup);
                        Intent[k] = 0.5f + 0.5f * f;
                        Squash[k] = 1f - 0.3f * f;
                        if (_ptime[k] >= P.Windup && t >= 0)
                        {
                            var pv = vessels[t];
                            _lungeDir[k] = BuilderMath.Unit(pv.Pos + pv.Vel * 0.4f - Pos[k]);
                            MaxTelegraph = MathF.Max(MaxTelegraph, _ptime[k]); MinTelegraph = MathF.Min(MinTelegraph, _ptime[k]);
                            Phase[k] = LungePhase; _ptime[k] = 0f; Lunges++;
                            SetDanger(k, true);
                        }
                        break;
                    }
                    case LungePhase:
                    {
                        v = _lungeDir[k] * sp * P.Lunge * 1.8f;
                        Squash[k] = 1.25f; Intent[k] = 1f;
                        if (t >= 0 && dist < R * 2.5f + vessels[t].Radius && Touches(k, vessels[t]))
                        {
                            Hits++; Struck.Add((k, vessels[t].Id));
                            Phase[k] = Recover; _ptime[k] = 0f; SetDanger(k, false);
                        }
                        else if (_ptime[k] > 0.9f) { Phase[k] = Recover; _ptime[k] = 0f; SetDanger(k, false); }
                        break;
                    }
                    default:
                        v = Vel[k] * 0.8f; Intent[k] = 0f;
                        Squash[k] = 1f + 0.25f * MathF.Max(0f, 1f - _ptime[k] / 0.6f);
                        if (_ptime[k] > 2.5f) { Phase[k] = Approach; _ptime[k] = 0f; }
                        break;
                }
                if (Phase[k] == Thief || Phase[k] == Approach || Phase[k] == Recover) Squash[k] += (1f - Squash[k]) * 0.2f;
                Vel[k] = 0.75f * Vel[k] + 0.25f * v;
            }
            // integrate leaders (soft containment), followers ride their leader's frame
            for (int k = 0; k < Cap; k++)
            {
                if (!IsLeader(k)) continue;
                Pos[k] += Vel[k] * dt;
                var rel = Pos[k] - P.CellCentre;
                float r = rel.Length();
                if (r > P.Containment) Pos[k] = P.CellCentre + rel * (P.Containment / r);
            }
            UpdateFrames();
            for (int k = 0; k < Cap; k++)
            {
                if (!Alive[k] || Leader[k] == k) continue;
                int L = Leader[k];
                var o = _offset[k] * Squash[L];
                Pos[k] = Pos[L] + _right[L] * o.X + _up[L] * o.Y + _fwd[L] * o.Z;
                Vel[k] = Vel[L]; Intent[k] = Intent[L];
            }
            if (P.Fuse) DoFuse();
            for (int k = 0; k < Cap; k++)
                if (IsLeader(k) && _body[k].Count > P.BodyCap && (Phase[k] == Thief || Phase[k] == Approach)) SatiationMoult(k);
            Fight(vessels, count);
            for (int k = 0; k < Cap; k++)
            {
                if (!IsLeader(k)) continue;
                if (_hurt[k] >= MathF.Max(3f, P.HurtFraction * (_body[k].Count + _hurt[k])) && _body[k].Count > 0) HurtMoult(k);
            }
            for (int k = 0; k < Cap; k++) if (Alive[k]) Feed(k, dt);
            // one pose per creature with a body: the whole body follows (the glue batches the per-prism work)
            for (int k = 0; k < Cap; k++)
            {
                if (!IsLeader(k) || _body[k].Count == 0) continue;
                _world.PoseBody(k, Pos[k], _right[k], _up[k], _fwd[k], Squash[k]);
                BodyPoses++;
            }
        }

        int Nearest(int k, BuilderVessel[] vessels, int count, out float dist)
        {
            int best = -1; dist = float.MaxValue;
            for (int v = 0; v < count; v++)
            {
                float d = Vector3.Distance(vessels[v].Pos, Pos[k]);
                if (d < dist) { dist = d; best = v; }
            }
            return best;
        }

        Vector3 Thieve(int k, BuilderVessel tgt, bool has, float dist, float sp, float R)
        {
            Intent[k] = 0f;
            if (has && dist < P.FleeRange) return BuilderMath.Unit(Pos[k] - tgt.Pos) * sp * 1.2f;   // turned on: flee
            int g = Goal[k];
            if (g >= 0 && (!_world.Alive(g) || _world.Domain(g) == Domain || _world.Shielded(g) || !_world.Loose(g)))
            {
                _claimed.Remove(g); Goal[k] = g = -1;
            }
            if (g < 0 && (k + Tick) % 4 == 0 && _wornBy.Count < P.WornCap)
            {
                Queries++;
                int n = _world.QuerySphere(Pos[k], P.Sense, _q);
                int best = -1; float bd = float.MaxValue;
                for (int j = 0; j < n; j++)
                {
                    int i = _q[j];
                    if (!Stealable(i)) continue;
                    var ip = _world.Position(i);
                    if (has && Vector3.Distance(ip, tgt.Pos) < P.KeepOff) continue;   // never closer than ~90 u to the pilot
                    float d = Vector3.DistanceSquared(ip, Pos[k]) * (_world.IsTrail(i) ? P.TrailPreference : 1f);
                    if (d < bd) { bd = d; best = i; }
                }
                if (best >= 0) { Goal[k] = g = best; _claimed.Add(best); }
            }
            if (g >= 0)
            {
                var d = _world.Position(g) - Pos[k];
                if (d.Length() < R + 3f)
                {
                    if (_world.Steal(g, Domain))
                    {
                        bool trail = _world.IsTrail(g);
                        Attach(k, g); WornSteals++; if (trail) WornTrail++;
                    }
                    _claimed.Remove(g); Goal[k] = -1;
                }
                return BuilderMath.Unit(d) * sp;
            }
            if (has && dist > 160f) return BuilderMath.Unit(tgt.Pos - tgt.Vel * 0.8f - Pos[k]) * sp * 0.7f;   // skulk in the wake
            var w = _wander[k] + _rng.Normal3(0.3f);
            _wander[k] = BuilderMath.Unit(w);
            return _wander[k] * sp * 0.5f;
        }

        void UpdateFrames()
        {
            if (!P.Oriented) return;
            for (int k = 0; k < Cap; k++)
            {
                if (!IsLeader(k)) continue;
                var v = Vel[k]; float n = v.Length();
                if (n < 5f) continue;
                var f = BuilderMath.Unit(0.85f * _fwd[k] + 0.15f * v / n);
                var up = _up[k] - f * Vector3.Dot(_up[k], f);
                if (up.Length() < 1e-3f) up = Vector3.Cross(f, Vector3.UnitX);
                up = BuilderMath.Unit(up);
                _fwd[k] = f; _up[k] = up; _right[k] = Vector3.Cross(up, f);
            }
        }

        bool Touches(int k, in BuilderVessel p)
        {
            if (Vector3.Distance(Pos[k], p.Pos) < P.S + p.Radius) return true;
            float r2 = (p.Radius + 0.6f * P.S) * (p.Radius + 0.6f * P.S);
            foreach (var kv in _body[k]) if (Vector3.DistanceSquared(WearPos(k, kv.Key), p.Pos) < r2) return true;
            return false;
        }

        void SetDanger(int k, bool on)
        {
            if (_danger[k] == on) return;
            _danger[k] = on;
            _world.SetBodyDanger(k, on);
        }

        // ── bodies meet ─────────────────────────────────────────────────────────────────────────────
        void DoFuse()
        {
            for (int i = 0; i < Cap; i++)
            {
                if (!IsLeader(i) || _body[i].Count == 0) continue;
                for (int j = i + 1; j < Cap; j++)
                {
                    if (!IsLeader(i) || !IsLeader(j) || _body[j].Count == 0) continue;
                    if (Vector3.Distance(Pos[i], Pos[j]) >= Radius(i) + Radius(j)) continue;
                    int big = _body[i].Count >= _body[j].Count ? i : j, small = big == i ? j : i;
                    _scratch.Clear();
                    foreach (var h in _body[small].Values) _scratch.Add(h);
                    foreach (int h in _scratch) { Detach(h, unwear: false); Attach(big, h); }   // Wear moves it to the big body
                    SetDanger(small, false);
                    ResetFrontier(small);
                    // the small heart and its riders become members inside the big body (each keeps its heart)
                    float rb = Radius(big);
                    for (int k = 0; k < Cap; k++)
                    {
                        if (!Alive[k] || (k != small && Leader[k] != small)) continue;
                        Leader[k] = big;
                        _offset[k] = _rng.Normal3(0.4f) * rb;
                    }
                    Fusions++;
                }
            }
        }

        // ── contact ─────────────────────────────────────────────────────────────────────────────────
        void Fight(BuilderVessel[] vessels, int count)
        {
            for (int v = 0; v < count; v++)
            {
                if (!vessels[v].Rams) continue;
                var p = vessels[v];
                for (int k = 0; k < Cap; k++)
                {
                    if (!IsLeader(k) || _body[k].Count == 0) continue;
                    if (Vector3.Distance(Pos[k], p.Pos) > Radius(k) * 2f + 20f) continue;
                    _scratch.Clear();
                    float r2 = (p.Radius + P.StripReach) * (p.Radius + P.StripReach);
                    foreach (var kv in _body[k]) if (Vector3.DistanceSquared(WearPos(k, kv.Key), p.Pos) < r2) _scratch.Add(kv.Value);
                    foreach (int h in _scratch)
                    {
                        float vol = _world.Volume(h);
                        Detach(h);
                        _world.Reclaim(h, p.Id);   // stripped BACK to the pilot: it falls loose in the pilot's domain
                        Stripped++; StrippedVolume += vol; _hurt[k] += 1f;
                    }
                }
                for (int k = 0; k < Cap; k++)
                {
                    if (!Alive[k] || PlatformBody[k] || Vector3.Distance(Pos[k], p.Pos) > p.Radius + P.HeartSize + 1f) continue;
                    int L = Leader[k], near = 0;
                    float r2 = 2.2f * P.S * 2.2f * P.S;
                    foreach (var kv in _body[L]) if (Vector3.DistanceSquared(WearPos(L, kv.Key), Pos[k]) < r2 && ++near >= P.ExposedAt) break;
                    if (near < P.ExposedAt) Kill(k, p.Id);
                }
            }
        }

        /// <summary>Worn prisms the platform took (a weapon destroyed it, a pilot's ability stole it back, a shield came up on
        /// it - shielded mass is never worn) leave the body;
        /// every loss counts toward the hurt moult. The hurt memory decays.</summary>
        void SweepBodies(float dt)
        {
            float decay = MathF.Exp(-dt / MathF.Max(P.HurtWindow, 1e-3f));
            for (int k = 0; k < Cap; k++) _hurt[k] *= decay;
            _scratch.Clear();
            foreach (var kv in _wornBy)
                if (!_world.Alive(kv.Key) || _world.Domain(kv.Key) != Domain || _world.Shielded(kv.Key)) _scratch.Add(kv.Key);
            foreach (int h in _scratch)
            {
                int L = _wornBy[h].leader;
                Detach(h);
                _hurt[L] += 1f;
            }
        }

        /// <summary>SATIATION MOULT (research v3): over the cap the body sheds its OUTERMOST prisms where they hang into a
        /// static LAIR (a structure: never moves, never loot). The first BirthCost of shed volume is EATEN and pays for a
        /// newborn heart at the lair (feeding pays out as population; nothing is removed but what is eaten).</summary>
        void SatiationMoult(int k)
        {
            int keep = (int)(P.Keep * P.BodyCap);
            Outermost(k, _body[k].Count - keep);
            var s = P.Stomach;
            int slot = FreeSlot();
            float paid = 0f;
            var at = Pos[k];
            foreach (int h in _scratch)
            {
                at = _world.Position(h);
                Detach(h);
                if (slot >= 0 && paid < s.BirthCost)
                {
                    float v = _world.Consume(h, at);
                    paid += v; Eaten++; EatenVolume += v;
                    continue;
                }
                _world.SetBuilt(h, ColonyId, -(h + 1), true);
                _lair.Add(h); Shed++;
            }
            SatiationMoults++;
            if (slot >= 0 && paid > 0f) Birth(slot, at, paid);
        }

        /// <summary>HURT MOULT (game): a body that lost a sixth of itself inside the window sheds the outer layer of what is
        /// left BACK to whoever it was stolen from (Steal back - nothing destroyed) and slinks off as a thief.</summary>
        void HurtMoult(int k)
        {
            int n = Math.Max(1, (int)(P.HurtShed * _body[k].Count));
            Outermost(k, n);
            foreach (int h in _scratch)
            {
                float v = _world.Volume(h);
                Detach(h);
                _world.GiveBack(h);
                Returned++; ReturnedVolume += v;
            }
            HurtMoults++;
            _hurt[k] = 0f;
            SetDanger(k, false);
            Phase[k] = Thief; _ptime[k] = 0f; Squash[k] = 1f;
            Vel[k] = -BuilderMath.Unit(Vel[k] + new Vector3(1e-3f)) * P.Speed;   // recoil
        }

        /// <summary>The n worn prisms furthest from the heart (in sites) into _scratch.</summary>
        void Outermost(int k, int n)
        {
            _scratch.Clear();
            if (n <= 0) return;
            var all = new List<(float d, int h)>(_body[k].Count);
            foreach (var kv in _body[k]) all.Add((Local(kv.Key).LengthSquared(), kv.Value));
            all.Sort((a, b) => b.d.CompareTo(a.d));
            for (int i = 0; i < Math.Min(n, all.Count); i++) _scratch.Add(all[i].h);
        }

        int FreeSlot()
        {
            if (AliveCount >= P.MaxHearts) return -1;
            for (int j = 0; j < Cap; j++) if (!Alive[j]) return j;
            return -1;
        }

        void Birth(int j, Vector3 at, float stomach)
        {
            Alive[j] = true; Leader[j] = j; Goal[j] = -1; Phase[j] = Thief; Intent[j] = 0f; Squash[j] = 1f;
            Pos[j] = at + _rng.Normal3(8f); Vel[j] = Vector3.Zero;
            _right[j] = Vector3.UnitX; _up[j] = Vector3.UnitY; _fwd[j] = Vector3.UnitZ;
            _body[j].Clear(); ResetFrontier(j); _hurt[j] = 0f; _danger[j] = false; PlatformBody[j] = false;
            Stomach[j] = MathF.Min(stomach, P.Stomach.Capacity);
            Metabolised += MathF.Max(0f, stomach - Stomach[j]);   // a stomach can hold only so much; the rest is spent
            BornAt[j] = Time; Births++; BirthPaid += stomach;
            Born.Add(j);
        }

        // ── stomach ─────────────────────────────────────────────────────────────────────────────────
        /// <summary>A hungry heart eats the OUTERMOST prism of its creature's body (its own larder); it starves only when
        /// its stomach is empty - never on a clock.</summary>
        void Feed(int k, float dt)
        {
            var s = P.Stomach;
            int L = Leader[k];
            if (Stomach[k] < s.Capacity * s.HungryBelow && _body[L].Count > 0)
            {
                Outermost(L, 1);
                if (_scratch.Count > 0)
                {
                    int h = _scratch[0];
                    var at = _world.Position(h);
                    Detach(h);
                    float v = _world.Consume(h, at);
                    float room = s.Capacity - Stomach[k];
                    Stomach[k] += MathF.Min(v, room);
                    Metabolised += MathF.Max(0f, v - room);
                    Eaten++; EatenVolume += v;
                }
            }
            float burn = MathF.Min(Stomach[k], s.Metabolism * dt);
            Stomach[k] -= burn; Metabolised += burn;
            if (Stomach[k] <= 0f) Kill(k, BuilderDeath.StarvedBy);
        }

        // ── deaths ──────────────────────────────────────────────────────────────────────────────────
        /// <summary>Heart k died. A LEADER's body falls loose where it hangs (still the colony's domain - mass conserved)
        /// and its riders split off as creatures of their own.</summary>
        public void Kill(int k, int vessel)
        {
            if (k < 0 || k >= Cap || !Alive[k]) return;
            Alive[k] = false;
            if (vessel == BuilderDeath.StarvedBy) Starved++; else Kills++;
            if (Goal[k] >= 0) { _claimed.Remove(Goal[k]); Goal[k] = -1; }
            if (Leader[k] == k)
            {
                SetDanger(k, false);
                _scratch.Clear();
                foreach (var h in _body[k].Values) _scratch.Add(h);
                foreach (int h in _scratch) Detach(h);
                ResetFrontier(k);
                for (int j = 0; j < Cap; j++)
                {
                    if (!Alive[j] || Leader[j] != k) continue;
                    Leader[j] = j; Phase[j] = Thief; Squash[j] = 1f; Splits++;
                    _right[j] = _right[k]; _up[j] = _up[k]; _fwd[j] = _fwd[k];
                    Vel[j] = _rng.Normal3(40f);
                }
            }
            Deaths.Add(new BuilderDeath { Agent = k, Vessel = vessel, At = Pos[k] });
        }
    }
}
