// The SIEGE (Docs/SUBSTRATE_FAUNA.md §10): the lab's terrifying swarm, ported from Tools/Ecology/flight/src/70_siege.js
// on the research branch (cece/gifted-curie-x2cpd0). The design target is Garrett's: "being surrounded by them and
// having them all dive in at once".
//
// One swarm, one rhythm: a phase machine shared by every member of the population.
//   ROAM    - a loose cloud stalks the nearest pilot at ~Stalk u, working round AHEAD of its line. Harmless.
//   GATHER  - the pilot came within Detect: the cloud streams onto a SHELL of radius R0 round a centre C that trails
//             the pilot (slower than it). Members flow ALONG the shell to their slots, the side nearest the cloud
//             first, so the far side stays OPEN (the IRIS gap: the way out). Members swerve round the pilot; no bites.
//   CLOSE   - fill reached Quorum (or GATHER timed out): the shell shrinks R0 -> R1 and C nearly stops. The glow
//             starts here (GlowDelay in). The wall is a WEB: brush a seated member within Lunge and EVERY member
//             dives at once (a breach). The iris keeps closing; fly out through it and the siege scatters.
//   HOLD    - the shell sits at R1 for THold, pulsing, the gap closing to nothing.
//   DIVE    - every member at once sprints at the pilot's predicted position. Each member bites at most once.
//   SCATTER - burst outward, then ROAM until the cooldown ends. A repeating rhythm, ~15-20 s a cycle.
// Escape (outside the shell by EscMargin during GATHER/CLOSE/HOLD) also ends in SCATTER, with no bite.
//
// The port is the lab's act() line for line (same names, same order of operations), run as Substeps sub-steps of the
// substrate's 0.1 s tick, so its dynamics run at the lab's 1/30 s. Everything else is the substrate's: the members are
// ordinary agents of one population (stock = body, proxies, the danger tier on screen, index entries, eating,
// breeding, starving, the crystal on death). What the game changes, and why, is listed in the doc §10.2:
//   - the lab REGROWS a rammed member by eating a loose prism; here members are substrate agents, so they grow by
//     eating flora and split at BirthStock (the lifeform rule: every life form feeds and reproduces);
//   - hungry members drift to food while they ROAM (FoodPull), and a siege with no pilot near loiters and grazes;
//   - the lab stalks its one pilot; here the siege picks the pilot nearest its cloud and locks onto it for the
//     encounter (a lost target ends the encounter as an escape);
//   - the burn is the platform's: a member is a DANGER prism while it may bite (Dangerous), and the vessel's own
//     1 s danger-contact cooldown makes a dive one or two burns, not the lab's one per BiteGap.
//
// Pure C# (System.Numerics, no UnityEngine): compiled and RUN by Tools/Build/substrate_harness (group siege), which
// matches it step for step against the lab's own trajectories (siege_fixture.json).
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    public enum SubstrateSiegePhase
    {
        Roam = 0,
        Gather = 1,
        Close = 2,
        Hold = 3,
        Dive = 4,
        Scatter = 5,
    }

    /// <summary>
    /// The siege's tunables (lab SIEGE_DEFAULTS, plus the numbers its act() wrote inline, named). Every field below the
    /// game-only line is the lab's number verbatim; the harness asserts them against siege_fixture.json, which
    /// siege_fixture.py reads out of the lab's 70_siege.js.
    /// </summary>
    [Serializable]
    public sealed class SubstrateSiegeParams
    {
        /// <summary>This species is a siege: its population is moved by the siege phase machine, not the agent kernel.</summary>
        public bool Enabled;

        // ── the lab's SIEGE_DEFAULTS (N, SIZE and BODY are the species' N0, Regime.Size and Stock0) ──
        /// <summary>ROAM: the distance the cloud stalks at, and the distance that starts a GATHER.</summary>
        public float Stalk = 430f, Detect = 640f;
        /// <summary>Speeds (u/s): roaming cruise, flowing to a slot, the dive.</summary>
        public float Cruise = 120f, Sprint = 270f, Dive = 330f;
        /// <summary>The shell's radius at GATHER and after CLOSE.</summary>
        public float R0 = 300f, R1 = 130f;
        /// <summary>Outside the shell (or the wall's mean radius) by this much = escaped.</summary>
        public float EscMargin = 45f;
        /// <summary>A seated member this close to the pilot lunges; the pilot brushing it breaches the web.</summary>
        public float Lunge = 35f;
        /// <summary>Cosine of the angle within which a member counts as seated in its slot (the wall).</summary>
        public float SeatCos = 0.97f;
        /// <summary>A member not yet released by the iris hovers this many shell radii out (a corona, not a wall).</summary>
        public float Corona = 1.35f;
        /// <summary>Seated fraction that ends GATHER (after TGather).</summary>
        public float Quorum = 0.5f;
        /// <summary>Phase clocks (s).</summary>
        public float TGather = 3f, TGatherMax = 5f, TClose = 2.6f, THold = 1.3f, TDive = 1.6f, TScatter = 1.6f;
        /// <summary>The iris: share of the sphere filled by the end of GATHER, and by the end of CLOSE (1 at HOLD's end).</summary>
        public float PGather = 0.6f, PClose = 0.8f;
        /// <summary>Cooldown after a dive, and after an escape, before the next GATHER.</summary>
        public float TCool = 10f, TCoolEsc = 6f;
        /// <summary>Members arriving within this many seconds are one bite, not fifty.</summary>
        public float BiteGap = 0.25f;
        /// <summary>How fast the shell's centre follows the pilot during GATHER and CLOSE (u/s).</summary>
        public float FollowGather = 100f, FollowClose = 25f;
        /// <summary>ROAM: how far ahead of the pilot's line the cloud works (0 = straight behind its bearing).</summary>
        public float Lead = 0.6f;

        // ── the numbers the lab's act() writes inline, named (lab values) ──
        /// <summary>The glow (and the web) arms this long into CLOSE.</summary>
        public float GlowDelay = 0.4f;
        /// <summary>The dive aims at the pilot's position this far ahead.</summary>
        public float PredictS = 0.35f;
        /// <summary>GATHER: members inside this distance swerve away from the pilot.</summary>
        public float SwerveR = 70f;
        /// <summary>Separation radius, and its weight while shelling and while diving.</summary>
        public float SepR = 14f, SepW = 90f, SepWDive = 20f;
        /// <summary>Steering acceleration (u/s^2), and while diving.</summary>
        public float Accel = 520f, AccelDive = 900f;
        /// <summary>ROAM: per-axis jitter (u) and the pull toward the cloud's centre.</summary>
        public float RoamJitter = 30f, RoamCohesion = 0.8f;
        /// <summary>A bite: the pilot's radius + the member's size + this.</summary>
        public float ContactPad = 4f;
        /// <summary>Fewer living members than this never gather.</summary>
        public int MinMembers = 12;
        /// <summary>The cooldown a new siege starts with.</summary>
        public float FirstCool = 2f;

        // ── game only (Docs/SUBSTRATE_FAUNA.md §10.2) ──
        /// <summary>Sub-steps per substrate tick (0.1 s): 3 runs the machine at the lab's 1/30 s.</summary>
        public int Substeps = 3;
        /// <summary>ROAM: how hard a hungry member pulls toward food (u of the roam vector at full hunger).</summary>
        public float FoodPull = 150f;

        public SubstrateSiegeParams Clone() => (SubstrateSiegeParams)MemberwiseClone();

        /// <summary>(lab name, value) for every number the lab has - the fidelity gate's list.</summary>
        public void Visit(Action<string, float> f)
        {
            f("STALK", Stalk); f("DETECT", Detect); f("CRUISE", Cruise); f("SPRINT", Sprint); f("DIVE", Dive);
            f("R0", R0); f("R1", R1); f("ESC_MARGIN", EscMargin); f("LUNGE", Lunge); f("SEAT_COS", SeatCos);
            f("CORONA", Corona); f("QUORUM", Quorum); f("T_GATHER", TGather); f("T_GATHER_MAX", TGatherMax);
            f("P_GATHER", PGather); f("P_CLOSE", PClose); f("T_CLOSE", TClose); f("T_HOLD", THold); f("T_DIVE", TDive);
            f("T_SCATTER", TScatter); f("T_COOL", TCool); f("BITE_GAP", BiteGap); f("T_COOL_ESC", TCoolEsc);
            f("FOLLOW_GATHER", FollowGather); f("LEAD", Lead); f("FOLLOW_CLOSE", FollowClose);
            // inline in act(): siege_fixture.py reads these from the source lines that hold them
            f("glow_delay", GlowDelay); f("predict_s", PredictS); f("swerve_r", SwerveR); f("sep_r", SepR);
            f("sep_w", SepW); f("sep_w_dive", SepWDive); f("accel", Accel); f("accel_dive", AccelDive);
            f("roam_jitter", RoamJitter); f("roam_cohesion", RoamCohesion); f("contact_pad", ContactPad);
            f("min_members", MinMembers); f("first_cool", FirstCool);
        }
    }

    /// <summary>One siege population's shared state: the phase machine, the shell and each member's slot.</summary>
    public sealed class SubstrateSiegeState
    {
        public SubstrateSiegePhase Phase;
        /// <summary>Seconds in the current phase, and the cooldown left (double: the phase ends on the lab's step, not a
        /// float sum's).</summary>
        public double Tp, Cool;
        /// <summary>The siege's own clock (s): bite spacing and the log read it.</summary>
        public double Clock;
        /// <summary>The shell: centre, the axis from the centre toward the cloud (the iris opens opposite it), radius,
        /// seated fraction, filled share of the sphere (the iris), mean radius of the seated wall.</summary>
        public Vector3 C, Axis = new Vector3(0f, 0f, 1f);
        public float Rs, Fill, Prog, RWall;
        /// <summary>The pilot this encounter is about (its id), or -1.</summary>
        public int Target = -1;
        public long Encounters, Escapes, Dives, DiveBites, WallBites, Breaches, Strikes;
        public double LastBite = -1e9;
        /// <summary>Per member (index = agent - population start): unit slot direction on the shell, seated in the wall,
        /// has bitten this encounter, may bite now (the danger tier), intent 0..1 (the telegraph).</summary>
        public readonly Vector3[] Slot;
        public readonly bool[] Seated, Bit, Dangerous;
        public readonly float[] Intent;
        /// <summary>Phase changes with what caused them (escape, breach) - only when a reader sets it (the harness).</summary>
        public List<(double T, string What)> Log;

        internal readonly Vector3[] Des;
        internal readonly Random Rng;
        internal readonly int[] Order;
        internal readonly float[] KeyA, KeyB;
        internal readonly Vector3[] SDir;

        /// <summary>The lab's ablations (siege_eval.js --ablate), for the harness's negative controls. None in play.</summary>
        internal SubstrateSiegeAblation Ablate = SubstrateSiegeAblation.None;

        public SubstrateSiegeState(int cap, float firstCool, int seed)
        {
            Slot = new Vector3[cap]; Seated = new bool[cap]; Bit = new bool[cap]; Dangerous = new bool[cap]; Intent = new float[cap];
            Des = new Vector3[cap]; Order = new int[cap]; KeyA = new float[cap]; KeyB = new float[cap]; SDir = new Vector3[cap];
            Rng = new Random(seed);
            Cool = Math.Round(firstCool, 6); Rs = 300f;
        }

        public bool InShell => Phase == SubstrateSiegePhase.Gather || Phase == SubstrateSiegePhase.Close || Phase == SubstrateSiegePhase.Hold;
    }

    /// <summary>The lab's ablations: no iris (every slot released at once), no sync (divers at 7 speeds), no breach (the
    /// wall is not a web).</summary>
    internal enum SubstrateSiegeAblation
    {
        None = 0,
        NoIris = 1,
        NoSync = 2,
        NoBreach = 3,
    }

    /// <summary>The siege director: steps a siege population in place of the agent kernel (SubstrateCore.BeginStep).</summary>
    public static class SubstrateSiege
    {
        /// <summary>One substrate tick: the kernel's metabolism, then <see cref="SubstrateSiegeParams.Substeps"/> lab
        /// steps, then the drawing channels (phase = intent: the regime's size swell is the glow).</summary>
        internal static void Step(SubstrateCore c, SubstratePopulation pop)
        {
            var S = pop.Siege; var K = pop.P.Siege;
            float dt = c.Dt;
            for (int k = 0; k < pop.LiveCount; k++)
            {
                int i = pop.Live[k];
                c.Hunger[i] += pop.P.Metabolism * dt;   // the kernel's rule: above 1 the reserve is burning
                c.Steered[i] = true;                     // every member may ask for a bite (the owner caps bites per tick)
                c.Watched[i] = false; c.Creeping[i] = false;
            }
            int sub = Math.Max(1, K.Substeps);
            for (int s = 0; s < sub; s++) Substep(c, pop, (double)dt / sub, (double)dt * s / sub);
            for (int k = 0; k < pop.LiveCount; k++)
            {
                int i = pop.Live[k];
                float it = S.Intent[i - pop.Start];
                c.Phase[i] = it; c.Aggr[i] = it; c.QTarget[i] = it;
            }
        }

        static void Go(SubstrateCore c, SubstratePopulation pop, SubstrateSiegePhase ph, double t, string what = null, int cause = 0)
        {
            var S = pop.Siege;
            S.Phase = ph; S.Tp = 0f;
            S.Log?.Add((t, what ?? ph.ToString().ToLowerInvariant()));
            c.Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.SiegePhase, Index = pop.Start, Other = cause, Value = (int)ph });
        }

        static float PhaseIntent(SubstrateSiegeState S, SubstrateSiegeParams K)
        {
            float tp = (float)S.Tp;
            switch (S.Phase)
            {
                case SubstrateSiegePhase.Gather: return 0.15f + 0.3f * S.Fill;
                case SubstrateSiegePhase.Close: return 0.55f + 0.25f * MathF.Min(1f, tp / K.TClose);
                case SubstrateSiegePhase.Hold: return MathF.Min(1f, 0.8f + 0.2f * tp / K.THold) * (0.9f + 0.1f * MathF.Sin(tp * 18f));
                case SubstrateSiegePhase.Dive: return 1f;
                default: return 0f;
            }
        }

        /// <summary>A tunable as the decimal it was written as (2.6f is 2.5999999; the lab's clock compares with 2.6).</summary>
        static double D(float f) => Math.Round(f, 6);

        static float Smooth01(float x) { x = Math.Clamp(x, 0f, 1f); return x * x * (3f - 2f * x); }

        static float Gauss(Random r)
        {
            double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
            return (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }

        /// <summary>
        /// The pilot this sub-step is about: the locked target during an encounter, else the pilot nearest the cloud.
        /// Pilots are sensed once per tick and carried forward by their velocity inside the tick (<paramref name="lead"/>).
        /// </summary>
        static bool PickPilot(SubstrateCore c, SubstrateSiegeState S, Vector3 g, float lead, out SubstratePilot p)
        {
            var pil = c.TickPilots;
            p = default;
            int best = -1; float bd = float.MaxValue;
            for (int j = 0; j < pil.Length; j++)
            {
                if (S.Phase != SubstrateSiegePhase.Roam && S.Phase != SubstrateSiegePhase.Scatter)
                {
                    if (pil[j].Id == S.Target) { best = j; break; }
                    continue;
                }
                float d = Vector3.Distance(pil[j].Pos, g);
                if (d < bd) { bd = d; best = j; }
            }
            if (best < 0) return false;
            p = pil[best];
            p.Pos += p.Vel * lead;
            return true;
        }

        /// <summary>
        /// Slot assignment at GATHER (lab assignSlots): the near side of the shell (toward the cloud) goes to the members
        /// nearest it, banded by polar angle round the cloud->C axis and ordered by azimuth inside a band, so paths do
        /// not cross the middle. Ties keep index order (the lab's sort is stable).
        /// </summary>
        static void AssignSlots(SubstrateCore c, SubstratePopulation pop)
        {
            var S = pop.Siege; var X = c.Pos; var C = S.C;
            int m = pop.LiveCount;
            Vector3 a = Vector3.Zero;
            for (int k = 0; k < m; k++) a += X[pop.Live[k]] - C;
            float an = MathF.Max(a.Length(), 1e-9f);
            float ax = a.X / an, ay = a.Y / an, az = a.Z / an;
            S.Axis = new Vector3(ax, ay, az);
            float ux = 0f, uy = 1f, uz = 0f; if (MathF.Abs(ay) > 0.9f) { ux = 1f; uy = 0f; }
            float bx = uy * az - uz * ay, by = uz * ax - ux * az, bz = ux * ay - uy * ax; float bn = MathF.Sqrt(bx * bx + by * by + bz * bz);
            bx /= bn; by /= bn; bz /= bn;
            float cx = ay * bz - az * by, cy = az * bx - ax * bz, cz = ax * by - ay * bx;
            // slots: a fresh m-point Fibonacci lattice. S rows: (d, azimuth, dir) in SDir/KeyA/KeyB over Order
            const float ga = 2.3999632297286533f;   // PI * (3 - sqrt 5)
            var sd = new float[m]; var saz = new float[m]; var sdir = new Vector3[m];
            var md = new float[m]; var maz = new float[m]; var mi = new int[m];
            for (int s = 0; s < m; s++)
            {
                float y = 1f - 2f * (s + 0.5f) / m, r = MathF.Sqrt(1f - y * y), th = ga * s;
                float dx = MathF.Cos(th) * r, dy = y, dz = MathF.Sin(th) * r;
                sd[s] = dx * ax + dy * ay + dz * az;
                saz[s] = MathF.Atan2(dx * cx + dy * cy + dz * cz, dx * bx + dy * by + dz * bz);
                sdir[s] = new Vector3(dx, dy, dz);
            }
            for (int k = 0; k < m; k++)
            {
                int i = pop.Live[k];
                float dx = X[i].X - C.X, dy = X[i].Y - C.Y, dz = X[i].Z - C.Z, dl = MathF.Sqrt(dx * dx + dy * dy + dz * dz), dn = MathF.Max(dl, 1e-9f);
                float ex = dx / dn, ey = dy / dn, ez = dz / dn;
                md[k] = ex * ax + ey * ay + ez * az - dl / 2000f;
                maz[k] = MathF.Atan2(ex * cx + ey * cy + ez * cz, ex * bx + ey * by + ez * bz);
                mi[k] = i;
            }
            var so = Sorted(sd, m, descending: true);
            var mo = Sorted(md, m, descending: true);
            const int B = 8;
            int per = (m + B - 1) / B;
            for (int b0 = 0; b0 < m; b0 += per)
            {
                int len = Math.Min(per, m - b0);
                var ss = new int[len]; var mm = new int[len];
                for (int k = 0; k < len; k++) { ss[k] = so[b0 + k]; mm[k] = mo[b0 + k]; }
                SortBy(ss, saz); SortBy(mm, maz);
                for (int k = 0; k < len; k++) S.Slot[mi[mm[k]] - pop.Start] = sdir[ss[k]];
            }
        }

        /// <summary>Indices 0..m-1 ordered by key (stable: ties keep index order).</summary>
        static int[] Sorted(float[] key, int m, bool descending)
        {
            var o = new int[m];
            for (int k = 0; k < m; k++) o[k] = k;
            Array.Sort(o, (p, q) =>
            {
                int c = descending ? key[q].CompareTo(key[p]) : key[p].CompareTo(key[q]);
                return c != 0 ? c : p.CompareTo(q);
            });
            return o;
        }

        /// <summary>Orders <paramref name="idx"/> by key[idx] ascending, stable on position.</summary>
        static void SortBy(int[] idx, float[] key)
        {
            var pos = new int[idx.Length];
            var copy = (int[])idx.Clone();
            for (int k = 0; k < idx.Length; k++) pos[k] = k;
            Array.Sort(pos, (p, q) => { int c = key[copy[p]].CompareTo(key[copy[q]]); return c != 0 ? c : p.CompareTo(q); });
            for (int k = 0; k < idx.Length; k++) idx[k] = copy[pos[k]];
        }

        /// <summary>One lab step (Siege.act, then Herd.step's integration) of <paramref name="h"/> seconds.</summary>
        internal static void Substep(SubstrateCore c, SubstratePopulation pop, double hd, double leadD)
        {
            float h = (float)hd, lead = (float)leadD;
            var S = pop.Siege; var K = pop.P.Siege; var P = pop.P;
            var X = c.Pos; var V = c.Vel; var des = S.Des;
            int n = pop.LiveCount, st = pop.Start;
            if (n == 0) return;
            Vector3 g = Vector3.Zero;
            for (int k = 0; k < n; k++) g += X[pop.Live[k]];
            g /= n;
            double t = S.Clock;   // the lab's arena.t: the time at the start of this step
            S.Tp += hd; S.Cool -= hd; S.Clock += hd;
            bool hasP = PickPilot(c, S, g, lead, out var p);
            if (!hasP && S.Phase != SubstrateSiegePhase.Roam)
            {
                // the target left the cell (or the game): the encounter ends as an escape (game: the lab has one pilot)
                if (S.InShell || S.Phase == SubstrateSiegePhase.Dive) { S.Escapes++; S.Cool = D(K.TCoolEsc); }
                S.Target = -1;
                Go(c, pop, SubstrateSiegePhase.Roam, t, "lost", 3);
            }
            var C = S.C;
            float pd = hasP ? Vector3.Distance(p.Pos, g) : float.MaxValue;
            // ── the phase machine ──
            var ph = S.Phase;
            if (ph == SubstrateSiegePhase.Roam)
            {
                if (hasP && S.Cool <= 0f && pd < K.Detect && n >= K.MinMembers)
                {
                    S.C = C = p.Pos; S.Rs = K.R0; S.Fill = 0f;
                    Array.Clear(S.Bit, 0, S.Bit.Length);
                    S.Target = p.Id;
                    AssignSlots(c, pop); S.Encounters++; Go(c, pop, SubstrateSiegePhase.Gather, t);
                }
            }
            else if (S.InShell)
            {
                float follow = ph == SubstrateSiegePhase.Gather ? K.FollowGather : ph == SubstrateSiegePhase.Close ? K.FollowClose : 0f;
                var o = p.Pos - C; float on = o.Length();
                if (on > 1e-6f) { float s = MathF.Min(on, follow * h) / on; C += o * s; }
                // the shell must fit in the cell: its centre stays inside the membrane by most of its radius
                float cr = C.Length(), cmax = 0.92f * c.R - 0.6f * S.Rs;
                if (cr > cmax) C *= cmax / cr;
                S.C = C;
                if (ph == SubstrateSiegePhase.Close) S.Rs = K.R0 + (K.R1 - K.R0) * Smooth01((float)(S.Tp / K.TClose));
                if (Vector3.Distance(p.Pos, C) > MathF.Max(S.Rs, S.RWall) + K.EscMargin)
                {
                    S.Escapes++; Go(c, pop, SubstrateSiegePhase.Scatter, t, "escape", 1); S.Cool = D(K.TCoolEsc);
                }
                else if (ph == SubstrateSiegePhase.Gather && ((S.Tp >= D(K.TGather) && S.Fill >= K.Quorum) || S.Tp > D(K.TGatherMax))) Go(c, pop, SubstrateSiegePhase.Close, t);
                else if (ph == SubstrateSiegePhase.Close && S.Tp >= D(K.TClose)) Go(c, pop, SubstrateSiegePhase.Hold, t);
                else if (ph == SubstrateSiegePhase.Hold && S.Tp >= D(K.THold)) { S.Dives++; Go(c, pop, SubstrateSiegePhase.Dive, t); }
            }
            else if (ph == SubstrateSiegePhase.Dive)
            {
                if (S.Tp >= D(K.TDive)) { Go(c, pop, SubstrateSiegePhase.Scatter, t); S.Cool = D(K.TCool); }
            }
            else if (ph == SubstrateSiegePhase.Scatter)
            {
                if (S.Tp >= D(K.TScatter)) { S.Target = -1; Go(c, pop, SubstrateSiegePhase.Roam, t); }
            }
            var phase = S.Phase;
            float it = PhaseIntent(S, K);
            bool shell = S.InShell;
            // the IRIS: the shell fills from the cloud's side (Axis) outward; the far cap stays open and shrinks to nothing
            // at the end of HOLD. Prog = filled share of the sphere's area; front = cos of the filled cap's polar angle.
            float tpf = (float)S.Tp;
            float prog = phase == SubstrateSiegePhase.Gather ? K.PGather * MathF.Min(1f, tpf / K.TGather)
                : phase == SubstrateSiegePhase.Close ? K.PGather + (K.PClose - K.PGather) * MathF.Min(1f, tpf / K.TClose)
                : phase == SubstrateSiegePhase.Hold ? K.PClose + (1f - K.PClose) * MathF.Min(1f, tpf / K.THold) : 1f;
            S.Prog = prog;
            float front = 1f - 2f * prog, ax = S.Axis.X, ay = S.Axis.Y, az = S.Axis.Z;
            float sinF = MathF.Sqrt(MathF.Max(0f, 1f - front * front));
            // ── desired velocities ──
            var pr = p.Pos + p.Vel * K.PredictS;
            int seated = 0, nw = 0; float rw = 0f;
            float pv = MathF.Max(p.Vel.Length(), 1e-9f);
            for (int k = 0; k < n; k++)
            {
                int i = pop.Live[k], li = i - st;
                var xi = X[i];
                var dv = p.Pos - xi; float d = MathF.Max(dv.Length(), 1e-9f);
                Vector3 w;
                if (shell)
                {
                    // flow along the shell toward the slot: step <= ~35 deg round the surface per target, never through the middle
                    var u = xi - C; float un = MathF.Max(u.Length(), 1e-9f); u /= un;
                    var sv = S.Slot[li];
                    if (sv == Vector3.Zero) sv = S.Slot[li] = un > 1e-6f ? u : S.Axis;   // born mid-encounter: its own bearing
                    float sx = sv.X, sy = sv.Y, sz = sv.Z;
                    float ca = sx * ax + sy * ay + sz * az;
                    bool held = ca < front && S.Ablate != SubstrateSiegeAblation.NoIris;
                    if (held)
                    {   // not released yet: hover just OUTSIDE the iris rim at the same azimuth (a corona, not a wall)
                        float px = sx - ca * ax, py = sy - ca * ay, pz = sz - ca * az; float pn = MathF.Sqrt(px * px + py * py + pz * pz);
                        if (pn > 1e-6f) { px /= pn; py /= pn; pz /= pn; sx = ax * front + px * sinF; sy = ay * front + py * sinF; sz = az * front + pz * sinF; }
                    }
                    float cc = u.X * sx + u.Y * sy + u.Z * sz;
                    float tx = sx, ty = sy, tz = sz;
                    if (cc < 0.82f)
                    {
                        float qx = sx - cc * u.X, qy = sy - cc * u.Y, qz = sz - cc * u.Z; float qn = MathF.Sqrt(qx * qx + qy * qy + qz * qz);
                        if (qn < 1e-6f) { qx = -u.Z; qy = 0f; qz = u.X; qn = MathF.Max(MathF.Sqrt(qx * qx + qy * qy + qz * qz), 1e-9f); }
                        tx = u.X + 0.7f * qx / qn; ty = u.Y + 0.7f * qy / qn; tz = u.Z + 0.7f * qz / qn;
                        float tn = MathF.Sqrt(tx * tx + ty * ty + tz * tz); tx /= tn; ty /= tn; tz /= tn;
                    }
                    float R = held ? S.Rs * K.Corona : S.Rs;
                    var e = new Vector3(C.X + tx * R - xi.X, C.Y + ty * R - xi.Y, C.Z + tz * R - xi.Z); float en = MathF.Max(e.Length(), 1e-9f);
                    // in the wall = at its place ON THE SPHERE (angle only: the radius lags while the shell shrinks)
                    S.Seated[li] = !held && cc > K.SeatCos;
                    if (S.Seated[li]) { seated++; rw += un; nw++; }
                    float sp = MathF.Min(K.Sprint, 40f + 3f * en);
                    w = e / en * sp;
                    if (phase == SubstrateSiegePhase.Gather && d < K.SwerveR) w -= dv / d * ((K.SwerveR - d) / K.SwerveR * K.Sprint);   // swerve, no bite
                    // the wall is solid: once the glow is up a seated member within Lunge lunges at the pilot
                    if (phase != SubstrateSiegePhase.Gather && !(phase == SubstrateSiegePhase.Close && S.Tp < D(K.GlowDelay)) && d < K.Lunge && !S.Bit[li] && S.Seated[li])
                        w = dv / d * K.Dive;
                }
                else if (phase == SubstrateSiegePhase.Dive)
                {
                    if (S.Bit[li]) w = -dv / d * K.Sprint;
                    else
                    {
                        var q = pr - xi; float qn = MathF.Max(q.Length(), 1e-9f);
                        float dvs = S.Ablate == SubstrateSiegeAblation.NoSync ? K.Dive * (0.5f + ((i - st) % 7) / 6f) : K.Dive;
                        w = q / qn * dvs;
                    }
                }
                else if (phase == SubstrateSiegePhase.Scatter)
                {
                    w = -dv / d * K.Sprint;
                }
                else
                {
                    // roam: stalk at Stalk from the pilot, loose cloud, slow drift. The cloud works round AHEAD of the
                    // pilot's line (the pack's fan-ahead), so the shell forms across its path and the open cap - the way
                    // out - is behind it or to the side. No pilot: the cloud holds together where it is (game).
                    Vector3 q;
                    if (hasP)
                    {
                        var r = g - p.Pos; float rn = MathF.Max(r.Length(), 1e-9f);
                        r = r / rn + K.Lead * p.Vel / pv; rn = MathF.Max(r.Length(), 1e-9f);
                        q = p.Pos + r / rn * K.Stalk;
                    }
                    else q = g;
                    var a = q - g + (g - xi) * K.RoamCohesion
                            + new Vector3(Gauss(S.Rng) * K.RoamJitter, Gauss(S.Rng) * K.RoamJitter, Gauss(S.Rng) * K.RoamJitter);
                    // game: a hungry member drifts to food (every life form feeds; the lab regrew members instead)
                    if (K.FoodPull > 0f && c.Hunger[i] > P.EatHunger && c.GFood[i] != Vector3.Zero)
                        a += SubstrateCore.Unit(c.GFood[i]) * (K.FoodPull * MathF.Min(1f, c.Hunger[i]));
                    float an = MathF.Max(a.Length(), 1e-9f), sp = MathF.Min(K.Cruise * 1.6f, 25f + an * 0.6f);
                    w = a / an * sp;
                }
                des[li] = w;
            }
            if (shell) { S.Fill = (float)seated / n; S.RWall = nw > 0 ? rw / nw : S.Rs; }
            // separation (the lab's radius 14): strong enough that the shell reads as a lattice with holes
            float sepW = phase == SubstrateSiegePhase.Dive ? K.SepWDive : K.SepW, sr = K.SepR;
            for (int k = 0; k < n; k++)
            {
                int i = pop.Live[k];
                Vector3 sum = Vector3.Zero;
                for (int k2 = 0; k2 < n; k2++)
                {
                    if (k2 == k) continue;
                    var dd = X[pop.Live[k2]] - X[i]; float dl = dd.Length();
                    if (dl >= sr) continue;
                    sum -= dd / MathF.Max(dl, 1e-6f) * (1f - dl / sr);
                }
                des[i - st] += sum * sepW;
            }
            float adt = (phase == SubstrateSiegePhase.Dive ? K.AccelDive : K.Accel) * h;
            for (int k = 0; k < n; k++)
            {
                int i = pop.Live[k];
                var e = des[i - st] - V[i]; float en = e.Length();
                V[i] += e * MathF.Min(1f, adt / MathF.Max(en, 1e-9f));
                // the lab's soft membrane (bestiary contain)
                float r = X[i].Length(), over = MathF.Max(0f, (r - 0.92f * c.R) / (0.06f * c.R));
                if (over > 0f) V[i] -= X[i] * (over * 80f / MathF.Max(r, 1e-6f));
            }
            for (int k = 0; k < n; k++)
            {
                int i = pop.Live[k], li = i - st;
                S.Intent[li] = S.Bit[li] ? MathF.Min(it, 0.3f) : it;
            }
            // ── bites (danger contacts) ──
            // the wall is a web: brush within Lunge of a wall member once the glow is up and EVERY member dives at once
            if (hasP && ((phase == SubstrateSiegePhase.Close && S.Tp >= D(K.GlowDelay)) || phase == SubstrateSiegePhase.Hold) && S.Ablate != SubstrateSiegeAblation.NoBreach)
            {
                for (int k = 0; k < n; k++)
                {
                    int i = pop.Live[k];
                    if (!S.Seated[i - st]) continue;
                    if (Vector3.Distance(X[i], p.Pos) < K.Lunge)
                    {
                        S.Dives++; S.Breaches++; Go(c, pop, SubstrateSiegePhase.Dive, t, "breach", 2); break;
                    }
                }
            }
            if (S.Phase == SubstrateSiegePhase.Dive || (phase == SubstrateSiegePhase.Close && S.Tp >= D(K.GlowDelay)) || phase == SubstrateSiegePhase.Hold)
            {
                var pil = c.TickPilots;
                float size = P.Solitary.Size;
                for (int k = 0; k < n; k++)
                {
                    int i = pop.Live[k], li = i - st;
                    if (S.Bit[li]) continue;
                    for (int j = 0; j < pil.Length; j++)
                    {
                        var pp = pil[j].Pos + pil[j].Vel * lead;
                        if (Vector3.Distance(X[i], pp) >= pil[j].Radius + size + K.ContactPad) continue;
                        S.Bit[li] = true;
                        // one danger contact per BiteGap: the members arriving in the same instant are one bite, not fifty
                        if (t - S.LastBite < D(K.BiteGap)) break;
                        S.LastBite = t; S.Strikes++; pop.Bites++;
                        if (S.Phase == SubstrateSiegePhase.Dive) S.DiveBites++; else S.WallBites++;
                        c.Events.Add(new SubstrateEvent { Kind = SubstrateEventKind.Bite, Index = i, Other = pil[j].Id, Value = 1f });
                        break;
                    }
                }
            }
            // the danger tier: a member may bite while the glow is up (CLOSE after GlowDelay, HOLD) and in the DIVE,
            // until it has bitten
            bool armed = S.Phase == SubstrateSiegePhase.Dive || S.Phase == SubstrateSiegePhase.Hold
                         || (S.Phase == SubstrateSiegePhase.Close && S.Tp >= D(K.GlowDelay));
            for (int k = 0; k < n; k++)
            {
                int i = pop.Live[k], li = i - st;
                S.Dangerous[li] = armed && !S.Bit[li];
            }
            // Herd.step: integrate
            for (int k = 0; k < n; k++)
            {
                int i = pop.Live[k];
                X[i] += V[i] * h;
                c.ClampMembrane(i);
            }
        }
    }
}
