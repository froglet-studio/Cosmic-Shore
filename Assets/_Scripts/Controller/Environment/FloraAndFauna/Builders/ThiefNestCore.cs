// THIEVES - research Tools/Ecology/bestiary/species/thief.py ("Top 3 to port first", item 1), ported to plain C# over
// a struct-of-arrays on the builders' shared substrate (BuilderCore.cs). No UnityEngine: the same file runs headless
// in Tools/Build/builders_harness. Docs/BUILDERS_AND_THIEVES.md §4 is the design record.
//
// Quick little magpies that nest on a plant. When a ship comes within ~700 u they fall in behind it like gulls behind
// a trawler, snatch prisms from the last 1.5 s of its trail, and fly them home at HALF speed to a visible hoard.
// Nothing is destroyed: a prism CHANGES HANDS (Prism.Steal) and is carried, so mass is conserved and your loss is
// their nest. Local rules (research constants):
//   * scout:  a free thief wants only the WARM wake (laid <= 1.5 s ago): the freshest unclaimed one within 400 u,
//             claimed in a claim book; a ship in sight (700 u) with no warm wake in reach is tailed 70 u behind;
//   * snatch: inside 5 u the prism changes hands and rides in the thief's grip;
//   * homing: laden, it flies home at 75 u/s (free 150) and drops the prism on its nest's hoard shell;
//   * timid:  a free thief with a pilot pointing at it inside 120 u veers off (bold, not brave).
// Counterplay: TURN BACK - a laden thief is slow, and knocking it down returns its prism to the pilot it was stolen
// from; weave so your wake is not where they expect; or RAID the hoard (all your stolen mass in one place).
//
// What the GAME changes (Docs/BUILDERS_AND_THIEVES.md §4.2):
//   * the warm wake is found from the TAIL of each sighted vessel (one QuerySphere per vessel per tick, shared by the
//     whole nest) instead of a scan of every trail prism in the cell;
//   * a TERRITORY leash (living cell R3): a nest only tails ships within Territory of its plant;
//   * the OPENING TRANSIENT FIX (living cell's one structural failure left: thieves seeded at full strength starved in
//     the opening flora crash, 3 of 4 seeds extinct by ~35 min). Three model changes, no dial: (1) the nest is FOUNDED
//     small and BLOOMS from its own takings (births paid from the stomach, the cap a backstop) - so the opening is
//     never a siege of a full colony on the first ship; (2) the HOARD is the LARDER - a hungry thief eats a hoarded
//     prism, so its food is the stolen trail, not the flora the herbivore boom strips; (3) a thief with no ship in
//     sight ROOSTS at a torpor metabolism, so an empty opening costs almost nothing.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    public sealed class ThiefParams
    {
        public int Founders = 6;                 // GAME: founded small (research seeded 18 at once)
        public int MaxThieves = 18;              // research n = 18 (the cap is a backstop)
        public float FreeSpeed = 150f, LadenSpeed = 75f;
        public float FreeAccel = 500f, LadenAccel = 200f;
        public float Scout = 400f;               // reach of a free thief's search for warm wake
        public float Warm = 1.5f;                // only prisms laid <= 1.5 s ago (thief wake 2.0 -> 1.5 s improved every axis)
        public float Spot = 700f;                // a ship in sight is tailed
        public float TailBack = 70f;
        public float Territory = 900f;           // GAME: living cell R3 leash
        public float SnatchReach = 5f;
        public float HomeReach = 12f;
        public float HoardBase = 8f, HoardGrow = 1.5f;   // drop radius 8 + 1.5 * cbrt(hoard + 1)
        public float RoostSpeed = 30f, RoostJitter = 30f;
        public float TimidRange = 120f, TimidDot = 0.85f;
        public float Separation = 8f, SeparationGain = 40f;
        public float Size = 2.2f, KnockExtra = 4f;
        public float RaidReach = 4f;             // a vessel this close to a hoarded prism takes it back
        public float SettleSeconds = 0.3f;
        public float Containment = 1104f;        // the soft membrane starts at 0.92 x 1200
        public float MembraneRadius = 1200f;
        public Vector3 CellCentre;
        /// <summary>Research ablations: Cold = any trail prism, never tails a ship; Bold = never veers off.</summary>
        public bool Cold, Bold;
        public BuilderStomachParams Stomach = new()
        {
            Capacity = 20f, FounderFill = 0.6f, Metabolism = 0.02f, Torpor = 0.004f,
            HungryBelow = 0.4f, BirthAbove = 0.9f, BirthCost = 8f,
        };
    }

    public sealed class ThiefNestCore
    {
        readonly ThiefParams P;
        readonly IBuilderWorld _world;
        readonly BuilderRng _rng;
        public readonly int Domain, ColonyId, Cap;
        public Vector3 Nest { get; }

        public readonly Vector3[] Pos, Vel;
        public readonly bool[] Alive;
        public readonly int[] Claim, Carry;
        public readonly float[] Intent, Stomach, BornAt;
        /// <summary>GAME: member k has a real proxy body (collider on) - a vessel's contact with that BODY is the platform's
        /// (its danger plate stings, its body prism breaks), so the core does not also kill it by distance. Set by the glue
        /// before each step; never set in the harness.</summary>
        public readonly bool[] PlatformBody;
        /// <summary>The age of the claimed prism when it was claimed (the snatch-window proof).</summary>
        public readonly float[] ClaimAge;

        readonly List<int> _hoard = new();
        readonly HashSet<int> _hoardSet = new(), _book = new();
        readonly List<int> _warm = new(128), _q = new(256);
        readonly Vector3[] _des;

        public int Tick { get; private set; }
        public float Time { get; private set; }
        public int Steals, Recaptured, Raided, Kills, Starved, Births, Eaten, Queries, CarryMoves;
        public float StolenVolume, EatenVolume, Metabolised, BirthPaid, MaxClaimAge, MaxLadenSpeed;
        public readonly List<BuilderDeath> Deaths = new();
        public readonly List<int> Born = new();
        /// <summary>Every snatch this tick: (thief, handle, vessel nearest when it happened).</summary>
        public readonly List<(int thief, int handle)> Snatched = new();

        public ThiefNestCore(IBuilderWorld world, ThiefParams p, Vector3 nest, int domain, int colonyId, int seed)
        {
            _world = world; P = p; Nest = nest; Domain = domain; ColonyId = colonyId;
            _rng = new BuilderRng(seed + 4049);
            Cap = Math.Max(1, Math.Max(p.Founders, p.MaxThieves));
            Pos = new Vector3[Cap]; Vel = new Vector3[Cap]; Alive = new bool[Cap];
            Claim = new int[Cap]; Carry = new int[Cap]; Intent = new float[Cap]; Stomach = new float[Cap];
            BornAt = new float[Cap]; ClaimAge = new float[Cap]; PlatformBody = new bool[Cap]; _des = new Vector3[Cap];
            for (int i = 0; i < Cap; i++) { Claim[i] = -1; Carry[i] = -1; }
            for (int i = 0; i < Math.Min(p.Founders, Cap); i++)
            {
                Alive[i] = true;
                Pos[i] = nest + _rng.Normal3(20f);
                Vel[i] = _rng.Normal3(5f);
                Stomach[i] = p.Stomach != null ? p.Stomach.Capacity * (p.Stomach.FounderFill + (1f - p.Stomach.FounderFill) * _rng.Uniform()) : 0f;
                BornAt[i] = -10f;
            }
        }

        public int AliveCount { get { int n = 0; for (int i = 0; i < Cap; i++) if (Alive[i]) n++; return n; } }
        public int HoardCount => _hoard.Count;
        public IReadOnlyList<int> Hoard => _hoard;
        public bool IsHoarded(int h) => _hoardSet.Contains(h);
        public float StomachTotal { get { float s = 0; for (int i = 0; i < Cap; i++) if (Alive[i]) s += Stomach[i]; return s; } }

        /// <summary>A prism a thief may claim: live, unshielded, a vessel's trail, warm, loose, not this nest's colour.</summary>
        bool Wanted(int h)
        {
            if (!_world.Alive(h) || _world.Shielded(h) || !_world.IsTrail(h)) return false;
            if (_world.Domain(h) == Domain || !_world.Loose(h)) return false;
            return P.Cold || _world.Age(h) <= P.Warm;
        }

        /// <summary>The nest's shared warm-wake book for this tick: one QuerySphere at the TAIL of each vessel in its territory.</summary>
        void GatherWarm(BuilderVessel[] vessels, int count)
        {
            _warm.Clear();
            for (int v = 0; v < count; v++)
            {
                if (Vector3.Distance(vessels[v].Pos, Nest) > P.Territory + P.Scout) continue;
                float sp = vessels[v].Vel.Length();
                float back = P.Cold ? P.Scout : sp * P.Warm * 0.5f;
                var c = vessels[v].Pos - BuilderMath.Unit(vessels[v].Vel) * back;
                Queries++;
                int n = _world.QuerySphere(c, back + vessels[v].Radius * 2f + 12f, _q);
                for (int j = 0; j < n; j++)
                    if (Wanted(_q[j])) _warm.Add(_q[j]);
            }
        }

        public void Kill(int i, int vessel)
        {
            if (i < 0 || i >= Cap || !Alive[i]) return;
            Alive[i] = false;
            if (vessel != BuilderDeath.StarvedBy) Kills++; else Starved++;
            if (Claim[i] >= 0) { _book.Remove(Claim[i]); Claim[i] = -1; }
            if (Carry[i] >= 0)
            {
                // knocked down laden: its prism goes BACK to the pilot it was stolen from (recapture), where it is
                if (vessel != BuilderDeath.StarvedBy) { _world.GiveBack(Carry[i]); Recaptured++; }
                _world.Drop(Carry[i]);
                _book.Remove(Carry[i]);
                Carry[i] = -1;
            }
            Deaths.Add(new BuilderDeath { Agent = i, Vessel = vessel, At = Pos[i], Stomach = Stomach[i] });
        }

        void DropOnHoard(int i)
        {
            int h = Carry[i];
            var at = Nest + _rng.OnSphere() * (P.HoardBase + P.HoardGrow * MathF.Cbrt(_hoard.Count + 1));
            _world.Settle(h, _world.Position(h), at, P.SettleSeconds);
            _world.SetBuilt(h, ColonyId, -(h + 1), true);
            _hoard.Add(h); _hoardSet.Add(h); _book.Remove(h);
            Carry[i] = -1;
        }

        void EatFromLarder(int i)
        {
            for (int j = _hoard.Count - 1; j >= 0; j--)
            {
                int h = _hoard[j];
                _hoard.RemoveAt(j); _hoardSet.Remove(h);
                _world.SetBuilt(h, ColonyId, -(h + 1), false);
                if (!_world.Alive(h)) continue;
                float v = _world.Consume(h, Pos[i]);
                if (v > 0f) { Stomach[i] += v; EatenVolume += v; Eaten++; return; }
            }
        }

        void UpkeepHoard(BuilderVessel[] vessels, int count)
        {
            for (int j = _hoard.Count - 1; j >= 0; j--)
            {
                int h = _hoard[j];
                bool gone = !_world.Alive(h) || _world.Domain(h) != Domain;
                if (!gone)
                {
                    // RAID: a vessel flying through the hoard takes back what it touches
                    var hp = _world.Position(h);
                    for (int v = 0; v < count && !gone; v++)
                        if (Vector3.Distance(hp, vessels[v].Pos) < vessels[v].Radius + P.RaidReach)
                        {
                            _world.Reclaim(h, vessels[v].Id); Raided++; gone = true;
                        }
                }
                if (gone)
                {
                    _hoard.RemoveAt(j); _hoardSet.Remove(h);
                    _world.SetBuilt(h, ColonyId, -(h + 1), false);
                }
            }
        }

        public void Step(float dt, BuilderVessel[] vessels, int count)
        {
            Tick++; Time += dt;
            Deaths.Clear(); Born.Clear(); Snatched.Clear();
            UpkeepHoard(vessels, count);
            GatherWarm(vessels, count);
            var s = P.Stomach;
            for (int i = 0; i < Cap; i++)
            {
                if (!Alive[i]) continue;
                // the nearest vessel (research pilot_vectors)
                int k = -1; float dist = float.MaxValue;
                for (int v = 0; v < count; v++)
                {
                    float d = Vector3.Distance(vessels[v].Pos, Pos[i]);
                    if (d < dist) { dist = d; k = v; }
                }
                bool active = false;
                if (Carry[i] >= 0)
                {
                    int c = Carry[i];
                    if (!_world.Alive(c) || _world.Domain(c) != Domain) { _book.Remove(c); Carry[i] = -1; _world.Drop(c); }
                    else
                    {
                        active = true;
                        _des[i] = BuilderMath.Unit(Nest - Pos[i]) * P.LadenSpeed;
                        if (Vector3.Distance(Nest, Pos[i]) < P.HomeReach) DropOnHoard(i);
                        if (s != null && Stomach[i] < s.Capacity * s.HungryBelow && Carry[i] < 0) EatFromLarder(i);
                        Intent[i] = 0.3f;
                        Metabolise(i, dt, active);
                        continue;
                    }
                }
                int cl = Claim[i];
                if (cl >= 0 && (!_world.Alive(cl) || !_world.IsTrail(cl) || _world.Domain(cl) == Domain
                                || (!P.Cold && _world.Age(cl) > P.Warm + 1.5f)))
                {
                    _book.Remove(cl); Claim[i] = cl = -1;
                }
                if (cl < 0 && _warm.Count > 0 && Vector3.Distance(Pos[i], Nest) < P.Territory + P.Scout)
                {
                    // the freshest unclaimed warm prism within scouting reach
                    int best = -1; float bestAge = float.MaxValue;
                    for (int j = 0; j < _warm.Count; j++)
                    {
                        int h = _warm[j];
                        if (_book.Contains(h)) continue;
                        if (Vector3.DistanceSquared(_world.Position(h), Pos[i]) > P.Scout * P.Scout) continue;
                        float age = _world.Age(h);
                        if (age < bestAge) { bestAge = age; best = h; }
                    }
                    if (best >= 0)
                    {
                        Claim[i] = cl = best; _book.Add(best); ClaimAge[i] = bestAge;
                        if (!P.Cold) MaxClaimAge = MathF.Max(MaxClaimAge, bestAge);
                    }
                }
                if (cl >= 0)
                {
                    active = true;
                    var to = _world.Position(cl) - Pos[i];
                    _des[i] = BuilderMath.Unit(to) * P.FreeSpeed;
                    if (to.Length() < P.SnatchReach && _world.Steal(cl, Domain))
                    {
                        Carry[i] = cl; Claim[i] = -1;
                        Steals++; StolenVolume += _world.Volume(cl);
                        Snatched.Add((i, cl));
                    }
                }
                else if (k >= 0 && dist < P.Spot && !P.Cold && Vector3.Distance(vessels[k].Pos, Nest) < P.Territory)
                {
                    // a ship in sight: fall in behind it, like gulls behind a trawler
                    active = true;
                    var pv = vessels[k];
                    _des[i] = BuilderMath.Unit(pv.Pos - BuilderMath.Unit(pv.Vel) * P.TailBack - Pos[i]) * P.FreeSpeed;
                }
                else
                {
                    _des[i] = BuilderMath.Unit(Nest - Pos[i] + _rng.Normal3(P.RoostJitter)) * P.RoostSpeed;
                    if (s != null && Stomach[i] < s.Capacity * s.HungryBelow && Vector3.Distance(Pos[i], Nest) < P.HomeReach * 2f)
                        EatFromLarder(i);
                }
                // timid: a pilot pointing at a free thief inside 120 u
                if (!P.Bold && k >= 0 && dist < P.TimidRange)
                {
                    var pv = vessels[k];
                    var off = pv.Pos - Pos[i];
                    if (Vector3.Dot(BuilderMath.Unit(pv.Vel), BuilderMath.Unit(-off)) > P.TimidDot)
                    {
                        var side = BuilderMath.Unit(Vector3.Cross(pv.Vel, Vector3.UnitY) + new Vector3(1e-6f));
                        float sg = MathF.Sign(Vector3.Dot(side, -off));
                        _des[i] = side * (sg == 0f ? 1f : sg) * P.FreeSpeed;
                    }
                }
                Intent[i] = cl >= 0 && dist < 300f ? 1f : (k >= 0 && dist < 300f ? 0.7f : 0f);
                Metabolise(i, dt, active);
            }

            // separation, accel-bounded steering, the soft membrane
            for (int i = 0; i < Cap; i++)
            {
                if (!Alive[i]) continue;
                var sep = Vector3.Zero;
                for (int j = 0; j < Cap; j++)
                {
                    if (j == i || !Alive[j]) continue;
                    var d = Pos[j] - Pos[i];
                    float l = d.Length();
                    if (l < P.Separation && l > 1e-6f) sep -= d / l * (1f - l / P.Separation);
                }
                var des = _des[i] + sep * P.SeparationGain;
                Vel[i] = BuilderMath.SteerAccel(Vel[i], des, Carry[i] >= 0 ? P.LadenAccel : P.FreeAccel, dt);
                var rel = Pos[i] - P.CellCentre;
                float r = rel.Length();
                float over = MathF.Max(0f, (r - P.Containment) / (0.06f * P.MembraneRadius));
                if (over > 0f) Vel[i] -= rel / MathF.Max(r, 1e-6f) * (over * 80f);
                // the load is the brake: a laden thief never flies faster than LadenSpeed, from the snatch on (the
                // free thief's 150 u/s does not carry over into the getaway - turning back must always catch it)
                if (Carry[i] >= 0) Vel[i] = BuilderMath.ClampLength(Vel[i], P.LadenSpeed);
                if (Carry[i] >= 0) MaxLadenSpeed = MathF.Max(MaxLadenSpeed, Vel[i].Length());
            }
            // the carried prism rides in the grip (set before the thief moves, as the research does)
            for (int i = 0; i < Cap; i++)
            {
                if (!Alive[i] || Carry[i] < 0) continue;
                _world.Carry(Carry[i], Pos[i] + Vel[i] * dt - BuilderMath.Unit(Vel[i]) * 3f);
                CarryMoves++;
            }
            for (int i = 0; i < Cap; i++)
                if (Alive[i]) Pos[i] += Vel[i] * dt;

            // a vessel knocks a thief down (contact): a laden thief's prism goes back to its pilot
            for (int v = 0; v < count; v++)
            {
                if (!vessels[v].Rams) continue;
                float reach = vessels[v].Radius + P.Size + P.KnockExtra;
                for (int i = 0; i < Cap; i++)
                    if (Alive[i] && !PlatformBody[i] && Vector3.DistanceSquared(Pos[i], vessels[v].Pos) < reach * reach) Kill(i, vessels[v].Id);
            }
            for (int i = 0; i < Cap; i++)
                if (Alive[i]) MaybeBreed(i);
        }

        void Metabolise(int i, float dt, bool active)
        {
            var s = P.Stomach;
            if (s == null) return;
            float burn = MathF.Min(Stomach[i], (active ? s.Metabolism : s.Torpor) * dt);
            Stomach[i] -= burn; Metabolised += burn;
            if (Stomach[i] <= 0f) Kill(i, BuilderDeath.StarvedBy);
        }

        void MaybeBreed(int i)
        {
            var s = P.Stomach;
            if (s == null || Carry[i] >= 0 || Stomach[i] < s.Capacity * s.BirthAbove || Stomach[i] < s.BirthCost) return;
            if (Vector3.Distance(Pos[i], Nest) > P.HomeReach * 3f) return;   // births happen AT the nest (controlling-domain spawn)
            int slot = -1, alive = 0;
            for (int j = 0; j < Cap; j++) { if (Alive[j]) alive++; else if (slot < 0) slot = j; }
            if (slot < 0 || alive >= P.MaxThieves) return;
            Stomach[i] -= s.BirthCost; BirthPaid += s.BirthCost;
            Alive[slot] = true; Pos[slot] = Pos[i] + _rng.Normal3(2f); Vel[slot] = Vector3.Zero;
            Claim[slot] = -1; Carry[slot] = -1; Intent[slot] = 0f;
            Stomach[slot] = s.BirthCost * 0.5f; BornAt[slot] = Time;
            Births++; Born.Add(slot);
        }
    }
}
