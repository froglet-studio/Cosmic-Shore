using System;
using System.Collections.Generic;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>The Ratel's abilities (one held at a time, redrawn every few patterns). Values are the lab's
    /// <c>ABILITIES</c> order and are serialized on the Honeyguide config's ability table, so each member keeps its value.</summary>
    public enum DynamoAbility
    {
        Overclock = 0,
        LongReach = 1,
        Printer = 2,
        Swap = 3,
        FreeFire = 4,
        WildCore = 5,
        Capacitor = 6,
        Mirror = 7,
        Refund = 8,
    }

    /// <summary>What a pattern can do to the Dynamo itself (the Ratel-side cards): the lab's <c>boardEffect</c> kinds.</summary>
    public enum DynamoBoardEffect
    {
        Burn = 0,
        Purge = 1,
        Free = 2,
        Bonus = 3,
        Rewind = 4,
        Unspin = 5,
        Trio = 6,
        Scatter = 7,
        Charge = 8,
        Drain = 9,
        DrainOne = 10,
    }

    /// <summary>One pattern card's shape as (q, r, colour) cells in the card's printed orientation, r running down, and
    /// whether it is parked (never drawn). Colours: 1 Ruby, 2 Jade, 3 Gold.</summary>
    public sealed class DynamoPattern
    {
        public int[][] Cells;
        public bool Parked;
    }

    /// <summary>The Dynamo numbers and tables the core reads (from the Honeyguide's config). A plain class so the core and
    /// its tests need no ScriptableObject. Field names are the lab's <c>dynamoSettings</c> keys.</summary>
    public sealed class DynamoSettings
    {
        public double TickSeconds, RevealAbility, RevealPorts;
        public int ApPerTick, ApBank, BonusApCap, BatteryCap, SlotCount = 3, UsesPerPattern, PatternsPerShuffle;
        public bool AllowRotatedMatches, PortsCount, StartFilled;
        /// <summary>-1 draws the ability at random (the shipped rule); otherwise every draw is this ability.</summary>
        public int FixedAbility = -1;
        /// <summary>The abilities a random draw picks from (<see cref="DynamoAbility"/> values), or null for all of them (the
        /// lab's <c>abilities</c>).</summary>
        public int[] Abilities;
        /// <summary>Round 7 (Garrett 2026-10-29): how many abilities each draw OFFERS; the Ratel picks one (<see
        /// cref="DynamoInputKind.Pick"/>). 1 or less is the old random draw.</summary>
        public int AbilityChoices;
        /// <summary>Seconds the Ratel has to pick before the first offer is taken; 0 or less waits for the pick.</summary>
        public double PickSeconds;
        /// <summary>-1 draws the first port pattern at random.</summary>
        public int FirstPortSet = -1;
        /// <summary>Every port pattern as (cell index, colour) pairs; <see cref="DynamoCore.BuildPortSets"/> makes them.</summary>
        public List<int[][]> PortSets = new();
        public List<DynamoPattern> Patterns = new();
        /// <summary>The slots' pattern indices at launch, or null to draw them.</summary>
        public int[] Loadout;
    }

    public enum DynamoInputKind
    {
        Tinker = 0,
        Refuel = 1,
        Activate = 2,
        Remove = 3,
        /// <summary>Round 7: take offer <see cref="DynamoInput.Slot"/> of the abilities the reveal offers.</summary>
        Pick = 4,
    }

    /// <summary>One Engineer input. A human's tap and the AI's choice are the same value. <see cref="Colour"/> on a Refuel
    /// spends the first free prism held (<see cref="DynamoCore.Free"/>; it must name that prism's domain) on any empty cell.
    /// A Remove deletes the prism on <see cref="Cell"/> for 1 AP (a right-click).</summary>
    public struct DynamoInput
    {
        public DynamoInputKind Kind;
        public int From, To, Cell, Colour, Slot;

        public static DynamoInput Tinker(int from, int to) => new() { Kind = DynamoInputKind.Tinker, From = from, To = to };
        public static DynamoInput Refuel(int cell, int colour = 0) => new() { Kind = DynamoInputKind.Refuel, Cell = cell, Colour = colour };
        public static DynamoInput Activate(int slot) => new() { Kind = DynamoInputKind.Activate, Slot = slot };
        public static DynamoInput Remove(int cell) => new() { Kind = DynamoInputKind.Remove, Cell = cell };
        public static DynamoInput Pick(int offer) => new() { Kind = DynamoInputKind.Pick, Slot = offer };
    }

    public enum DynamoEventKind
    {
        Tinker = 0,
        Refuel = 1,
        Place = 2,
        Ability = 3,
        Charge = 4,
        Replaced = 5,
        Pick = 6,
        Retire = 7,
        Shuffle = 8,
        Ports = 9,
        Resume = 10,
        Effect = 11,
        Tick = 12,
        Remove = 13,
        /// <summary>Round 7: the draw offers <see cref="DynamoEvent.Offers"/>; the Shuffle event follows the pick.</summary>
        Offer = 14,
    }

    public struct DynamoEvent
    {
        public DynamoEventKind Kind;
        public int Slot, Pid, Old, Cell, From, To, Colour, Count, Use, PortSet, Ap;
        public DynamoAbility Ability;
        public DynamoBoardEffect Effect;
        /// <summary>The abilities an <see cref="DynamoEventKind.Offer"/> offers, in offer order.</summary>
        public DynamoAbility[] Offers;
    }

    /// <summary>A point for the slip's pure math (double, as the lab is).</summary>
    public struct SlipVec
    {
        public double X, Y, Z;
        public SlipVec(double x, double y, double z) { X = x; Y = y; Z = z; }
    }

    /// <summary>Where a slip lands: which end (0 oldest, 1 head), the trail index it lands by, whether the reach cap clipped
    /// it, the outward heading there and the landing point (just past the block).</summary>
    public struct SlipLanding
    {
        public int End, Index;
        public bool Clipped;
        public SlipVec Heading, Point;
    }

    /// <summary>The Engineer AI's skill: how many placements it weighs (1 = the cheapest only) and the chance of a random
    /// legal move. Difficulty changes belief and pace, never the rules (/vessel-ai).</summary>
    public struct DynamoAISkill
    {
        public int Top;
        public double Blunder;
    }

    /// <summary>
    /// The Ratel's hex puzzle (the Engineer seat of the Honeyguide), engine-free: its only inputs are seat inputs and a
    /// clock. The Honeyguide Lab's <c>DynamoCore</c> (<c>Docs/Studios/HoneyguideLab.html</c>, the <c>dynamo-core</c> script
    /// block, round 4) ported line for line and held to it input by input - the Engineer AI included - by
    /// <c>Tools/Build/dynamo_lab/parity.cjs</c>. It knows nothing about prisms, hulls or screens.
    ///
    /// <para>Round 2-4 rules (Garrett, 2026-10-21): the injection ports turn as one rigid pattern, 60 degrees clockwise a
    /// spin; an empty port counts as a prism of its colour for completing a pattern (a real prism must still be in it, for
    /// the battery); patterns match in any of the six rotations; a made pattern sends one of its real prisms, drawn at
    /// random, to the battery; a pattern made for the third time is replaced by a random one; every 4 patterns the Ratel
    /// draws a new ability and a new port pattern, revealed one after the other while the spin clock stands still.</para>
    ///
    /// <para>Round 7 (Garrett 2026-10-29: "lets have the ap cap go to 6. lets do the 3 ability pick as well"): with
    /// <see cref="DynamoSettings.AbilityChoices"/> above 1 the draw OFFERS that many abilities and the Ratel picks one; the
    /// clock stays stopped while it chooses, and after <see cref="DynamoSettings.PickSeconds"/> the first offer is taken.
    /// The AI picks by <see cref="AIPickOrder"/>.</para>
    /// </summary>
    public sealed class DynamoCore
    {
        public const int Radius = 3;
        static readonly int[][] Dirs = { new[] { 1, 0 }, new[] { 1, -1 }, new[] { 0, -1 }, new[] { -1, 0 }, new[] { -1, 1 }, new[] { 0, 1 } };
        public static readonly int[] Q, R;
        public static readonly int CellCount, Centre;
        static readonly int[] Lut = new int[49];
        public static readonly int[][] Neighbours;
        /// <summary>The lab's ability keys, in <see cref="DynamoAbility"/> order (the digest prints them).</summary>
        public static readonly string[] AbilityKeys = { "overclock", "longReach", "printer", "swap", "freeFire", "wildCore", "capacitor", "mirror", "refund" };
        public static readonly int AbilityCount = AbilityKeys.Length;

        static DynamoCore()
        {
            var q = new List<int>();
            var r = new List<int>();
            for (int i = 0; i < Lut.Length; i++) Lut[i] = -1;
            for (int rr = -Radius; rr <= Radius; rr++)
                for (int qq = Math.Max(-Radius, -rr - Radius); qq <= Math.Min(Radius, -rr + Radius); qq++)
                {
                    Lut[(qq + 3) * 7 + (rr + 3)] = q.Count;
                    q.Add(qq);
                    r.Add(rr);
                }
            Q = q.ToArray();
            R = r.ToArray();
            CellCount = Q.Length;
            Centre = Index(0, 0);
            Neighbours = new int[CellCount][];
            for (int i = 0; i < CellCount; i++)
            {
                var n = new List<int>();
                foreach (var d in Dirs) { int j = Index(Q[i] + d[0], R[i] + d[1]); if (j >= 0) n.Add(j); }
                Neighbours[i] = n.ToArray();
            }
        }

        public static int Index(int q, int r) => q < -3 || q > 3 || r < -3 || r > 3 ? -1 : Lut[(q + 3) * 7 + (r + 3)];
        public static int RingOf(int i) => Math.Max(Math.Abs(Q[i]), Math.Max(Math.Abs(R[i]), Math.Abs(Q[i] + R[i])));
        /// <summary>60 degrees clockwise on screen about the centre.</summary>
        public static int Rot60(int i) => Index(-R[i], Q[i] + R[i]);
        public static bool IsNeighbour(int a, int b) => Array.IndexOf(Neighbours[a], b) >= 0;

        // ------------------------------------------------------------------ pattern shapes

        static int[][][] Rotations(int[][] cells)
        {
            var outShapes = new int[6][][];
            var cur = new int[cells.Length][];
            for (int c = 0; c < cells.Length; c++) cur[c] = new[] { cells[c][0], cells[c][1], cells[c][2] };
            for (int k = 0; k < 6; k++)
            {
                var shape = new int[cur.Length][];
                for (int c = 0; c < cur.Length; c++) shape[c] = new[] { cur[c][0] - cur[0][0], cur[c][1] - cur[0][1], cur[c][2] };
                outShapes[k] = shape;
                var next = new int[cur.Length][];
                for (int c = 0; c < cur.Length; c++) next[c] = new[] { -cur[c][1], cur[c][0] + cur[c][1], cur[c][2] };
                cur = next;
            }
            return outShapes;
        }

        /// <summary>A pattern's shapes as offsets from its first cell: the six rotations and the six rotations of the mirror
        /// image (q &lt;-&gt; r); only the printed one of each when rotated matches are off.</summary>
        public sealed class PatternShapes
        {
            public int[][][] Rot, Mir;
        }

        public static PatternShapes Shapes(int[][] cells, bool allowRotated)
        {
            var mirrored = new int[cells.Length][];
            for (int c = 0; c < cells.Length; c++) mirrored[c] = new[] { cells[c][1], cells[c][0], cells[c][2] };
            var rot = Rotations(cells);
            var mir = Rotations(mirrored);
            return new PatternShapes { Rot = allowRotated ? rot : new[] { rot[0] }, Mir = allowRotated ? mir : new[] { mir[0] } };
        }

        // ------------------------------------------------------------------ port patterns

        /// <summary>An engine's printed rows (top row first, tokens M / C / S for Ruby / Jade / Gold, anything else no
        /// port) as (cell, colour) ports: the lab's <c>parseEngine</c>.</summary>
        public static int[][] ParseEngine(IReadOnlyList<string> rows)
        {
            var ports = new List<int[]>();
            for (int ri = 0; ri < rows.Count; ri++)
            {
                int r = ri - 3, q0 = Math.Max(-3, -3 - r);
                var toks = rows[ri].Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                for (int k = 0; k < toks.Length; k++)
                {
                    int col = toks[k] == "M" ? 1 : toks[k] == "C" ? 2 : toks[k] == "S" ? 3 : 0;
                    if (col != 0) ports.Add(new[] { Index(q0 + k, r), col });
                }
            }
            return ports.ToArray();
        }

        /// <summary>Three-fold colour symmetry (Garrett, 2026-10-21): a 120 degree turn maps the ports onto the ports, each
        /// colour onto one other colour. The lab's <c>symmetricPorts</c>.</summary>
        public static bool SymmetricPorts(int[][] ports)
        {
            var at = new Dictionary<int, int>();
            foreach (var p in ports) at[p[0]] = p[1];
            var map = new int[4];
            foreach (var p in ports)
            {
                if (!at.TryGetValue(Rot60(Rot60(p[0])), out int t) || t == p[1] || (map[p[1]] != 0 && map[p[1]] != t)) return false;
                map[p[1]] = t;
            }
            return true;
        }

        static readonly int[][] Perms = { new[] { 1, 2, 3 }, new[] { 1, 3, 2 }, new[] { 2, 1, 3 }, new[] { 2, 3, 1 }, new[] { 3, 1, 2 }, new[] { 3, 2, 1 } };

        /// <summary>Every distinct three-fold colour-symmetric port pattern the engines make, turned and recoloured, in the
        /// lab's <c>PORT_SETS</c> order (engine, turn, colour permutation).</summary>
        public static List<int[][]> BuildPortSets(IEnumerable<int[][]> engines)
        {
            var outSets = new List<int[][]>();
            var seen = new HashSet<string>();
            var keyBuf = new System.Text.StringBuilder(64);
            string[] keys = null;
            foreach (var e in engines)
                for (int k = 0; k < 6; k++)
                    foreach (var pm in Perms)
                    {
                        var ports = new int[e.Length][];
                        for (int p = 0; p < e.Length; p++)
                        {
                            int c = e[p][0];
                            for (int j = 0; j < k; j++) c = Rot60(c);
                            ports[p] = new[] { c, pm[e[p][1] - 1] };
                        }
                        if (keys == null || keys.Length != ports.Length) keys = new string[ports.Length];
                        for (int p = 0; p < ports.Length; p++) keys[p] = PortKey(ports[p][0], ports[p][1]);
                        Array.Sort(keys, string.CompareOrdinal);
                        keyBuf.Clear();
                        for (int p = 0; p < keys.Length; p++) { if (p > 0) keyBuf.Append(','); keyBuf.Append(keys[p]); }
                        string key = keyBuf.ToString();
                        if (!seen.Contains(key) && SymmetricPorts(ports)) { seen.Add(key); outSets.Add(ports); }
                    }
            return outSets;
        }

        // "cell:colour" for the port-set dedupe key; cached so board generation doesn't re-format the same few hundred pairs.
        static readonly Dictionary<int, string> s_portKeys = new();
        static string PortKey(int cell, int colour)
        {
            int id = cell * 8 + colour;
            if (!s_portKeys.TryGetValue(id, out var k)) s_portKeys[id] = k = cell + ":" + colour;
            return k;
        }

        // ------------------------------------------------------------------ state

        /// <summary>The reveal: stage 1 shows the new ability (the old ports stay), stage 2 the new ports.</summary>
        public sealed class Reveal
        {
            public int Stage, PortSet;
            public double T;
            /// <summary>Round 7: the abilities on offer while the Ratel picks (stage 1), or null.</summary>
            public DynamoAbility[] Offers;
        }

        public int[] Cells = new int[CellCount];
        /// <summary>Each port as (cell, colour); the cells turn on every spin.</summary>
        public int[][] Ports = new int[0][];
        public int PortSet = -1;
        public int Ap, BonusAp, Ticks, Made, Wasted;
        /// <summary>The free prisms held, first to place first: each one's domain (1 Ruby, 2 Jade, 3 Gold), drawn when granted.</summary>
        public readonly List<int> Free = new();
        public int FreePlace => Free.Count;
        public double Clock;
        public readonly List<int> Battery = new();
        public int[] Slots, Uses;
        public Reveal RevealState;
        public DynamoAbility Ability;
        public int Rng;
        public readonly List<DynamoEvent> Events = new();
        readonly PatternShapes[] shapes;

        public DynamoCore(DynamoSettings s, int seed)
        {
            Ap = s.ApPerTick < s.ApBank ? s.ApPerTick : s.ApBank;
            Rng = seed;
            shapes = new PatternShapes[s.Patterns.Count];
            for (int p = 0; p < s.Patterns.Count; p++) shapes[p] = Shapes(s.Patterns[p].Cells, s.AllowRotatedMatches);
            Slots = new int[s.SlotCount];
            Uses = new int[s.SlotCount];
            for (int k = 0; k < Slots.Length; k++) Slots[k] = -1;
            for (int k = 0; k < s.SlotCount; k++) Slots[k] = s.Loadout != null && k < s.Loadout.Length ? s.Loadout[k] : DrawPattern(s);
            SetPorts(s, s.FirstPortSet >= 0 ? s.FirstPortSet : (int)Math.Floor(NextRandom() * s.PortSets.Count));
            var pool = Pool(s);
            Ability = s.FixedAbility >= 0 ? (DynamoAbility)s.FixedAbility : (DynamoAbility)pool[(int)Math.Floor(NextRandom() * pool.Length)];
            if (Ability == DynamoAbility.Printer) GrantFree(1);
            if (s.StartFilled) foreach (var p in Ports) Cells[p[0]] = p[1];
        }

        /// <summary>Free prisms (the Printer ability, Universal Translator, 5D Printer) each come with a random domain, drawn when
        /// granted (Garrett 2026-10-21: "a random domain and it should follow the cursor").</summary>
        void GrantFree(int n)
        {
            for (int k = 0; k < n; k++) Free.Add(1 + (int)Math.Floor(NextRandom() * 3));
        }

        /// <summary>mulberry32 over the state's own stream, so a replay of the same inputs draws the same cards.</summary>
        double NextRandom()
        {
            unchecked
            {
                uint a = (uint)Rng + 0x6D2B79F5u;
                Rng = (int)a;
                return Mulberry(a);
            }
        }

        static double Mulberry(uint a)
        {
            unchecked
            {
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>A mulberry32 stream as the lab's <c>mulberry32(seed)</c> makes one (the AI's dice).</summary>
        public static Func<double> Mulberry32(int seed)
        {
            uint a = unchecked((uint)seed);
            return () =>
            {
                unchecked { a += 0x6D2B79F5u; }
                return Mulberry(a);
            };
        }

        static readonly int[] AllAbilities = Enumerable.Range(0, AbilityCount).ToArray();

        // the abilities a random draw picks from: the settings' list, or every ability
        static int[] Pool(DynamoSettings s) => s.Abilities is { Length: > 0 } ? s.Abilities : AllAbilities;

        int PickOther(int n, int not)
        {
            if (n <= 1) return 0;
            int k = (int)Math.Floor(NextRandom() * (n - 1));
            if (k >= not) k++;
            return k;
        }

        // Round 7: up to n distinct abilities from the pool, never the one held (unless it is all the pool has), in draw order.
        DynamoAbility[] DrawOffers(int[] pool, int n)
        {
            var from = new List<int>();
            foreach (int a in pool) if (a != (int)Ability) from.Add(a);
            if (from.Count == 0) from.AddRange(pool);
            var offers = new List<DynamoAbility>();
            while (offers.Count < n && from.Count > 0)
            {
                int k = (int)Math.Floor(NextRandom() * from.Count);
                offers.Add((DynamoAbility)from[k]);
                from.RemoveAt(k);
            }
            return offers.ToArray();
        }

        /// <summary>The AI's pick (a proposal): the first of these an offer holds. Overclock and Refund add AP, Free Fire saves
        /// it; Mirror and Swap made no measurable difference in the lab's round 2; Printer is retired from the shipped draw.
        /// The lab's <c>AI_PICK_ORDER</c>.</summary>
        public static readonly DynamoAbility[] AIPickOrder =
        {
            DynamoAbility.Overclock, DynamoAbility.Refund, DynamoAbility.FreeFire, DynamoAbility.LongReach, DynamoAbility.Capacitor,
            DynamoAbility.WildCore, DynamoAbility.Swap, DynamoAbility.Mirror, DynamoAbility.Printer,
        };

        public static int AIPick(DynamoAbility[] offers)
        {
            int best = 0;
            for (int k = 1; k < offers.Length; k++)
                if (Array.IndexOf(AIPickOrder, offers[k]) < Array.IndexOf(AIPickOrder, offers[best])) best = k;
            return best;
        }

        // Take offer k: the new ability (and its Printer prism), announced as the old draw was; the reveal goes on to the ports.
        void TakeOffer(int k)
        {
            var rv = RevealState;
            Ability = rv.Offers[k];
            rv.Offers = null;
            if (Ability == DynamoAbility.Printer && Free.Count < 1) GrantFree(1);
            Events.Add(new DynamoEvent { Kind = DynamoEventKind.Shuffle, Ability = Ability, PortSet = rv.PortSet });
        }

        // a random pattern not already in a slot
        int DrawPattern(DynamoSettings s)
        {
            var cand = new List<int>();
            for (int p = 0; p < s.Patterns.Count; p++) if (!s.Patterns[p].Parked && Array.IndexOf(Slots, p) < 0) cand.Add(p);
            return cand.Count > 0 ? cand[(int)Math.Floor(NextRandom() * cand.Count)] : -1;
        }

        void SetPorts(DynamoSettings s, int k)
        {
            PortSet = k;
            var set = s.PortSets[k];
            Ports = new int[set.Length][];
            for (int p = 0; p < set.Length; p++) Ports[p] = new[] { set[p][0], set[p][1] };
        }

        public int PortAt(int cell)
        {
            foreach (var p in Ports) if (p[0] == cell) return p[1];
            return 0;
        }

        /// <summary>What a cell counts as for completing a pattern: its prism, or (empty) its port's colour.</summary>
        public int Shows(DynamoSettings s, int i) => Cells[i] != 0 ? Cells[i] : s.PortsCount ? PortAt(i) : 0;

        public int BatteryCap(DynamoSettings s) => s.BatteryCap + (Ability == DynamoAbility.Capacitor ? 2 : 0);

        /// <summary>The ports one spin from now (the Ratel's warning rings).</summary>
        public int[][] NextPorts()
        {
            var next = new int[Ports.Length][];
            for (int k = 0; k < next.Length; k++) next[k] = new[] { Rot60(Ports[k][0]), Ports[k][1] };
            return next;
        }

        /// <summary>The cells of the first placement of pattern <paramref name="pid"/> the board shows, or null. A placement
        /// needs at least one real prism (it goes to the battery). Order: rotations, then mirror images (the Mirror ability),
        /// each by anchor cell index.</summary>
        public List<int> Match(DynamoSettings s, int pid)
        {
            var sets = Ability == DynamoAbility.Mirror ? new[] { shapes[pid].Rot, shapes[pid].Mir } : new[] { shapes[pid].Rot };
            bool wild = Ability == DynamoAbility.WildCore;
            foreach (var sh in sets)
                for (int k = 0; k < sh.Length; k++)
                {
                    var shape = sh[k];
                    for (int a = 0; a < CellCount; a++)
                    {
                        var cells = new List<int>(shape.Length);
                        bool ok = true, real = false;
                        for (int c = 0; c < shape.Length; c++)
                        {
                            int i = Index(Q[a] + shape[c][0], R[a] + shape[c][1]);
                            if (i < 0) { ok = false; break; }
                            if (Shows(s, i) == shape[c][2] || (wild && i == Centre)) { cells.Add(i); if (Cells[i] != 0) real = true; }
                            else { ok = false; break; }
                        }
                        if (ok && real) return cells;
                    }
                }
            return null;
        }

        /// <summary>Each slot's matching cells, or null (the cards that light).</summary>
        public List<int>[] Built(DynamoSettings s)
        {
            var built = new List<int>[Slots.Length];
            for (int k = 0; k < Slots.Length; k++) built[k] = Slots[k] >= 0 ? Match(s, Slots[k]) : null;
            return built;
        }

        bool SpendAp(int n)
        {
            if (Ap + BonusAp < n) return false;
            int fromAp = Ap < n ? Ap : n;
            Ap -= fromAp;
            BonusAp -= n - fromAp;
            return true;
        }

        void Charge(DynamoSettings s, int colour)
        {
            int cap = BatteryCap(s);
            if (cap < 1) return;
            while (Battery.Count >= cap)
            {
                int old = Battery[0];
                Battery.RemoveAt(0);
                Events.Add(new DynamoEvent { Kind = DynamoEventKind.Replaced, Colour = old });
            }
            Battery.Add(colour);
            Events.Add(new DynamoEvent { Kind = DynamoEventKind.Charge, Colour = colour });
        }

        // A made pattern sends one of its real prisms, drawn at random, to the battery.
        void TakeRandom(DynamoSettings s, int slot, int pid, List<int> cells)
        {
            var real = new List<int>();
            foreach (int c in cells) if (Cells[c] != 0) real.Add(c);
            int cell = real[(int)Math.Floor(NextRandom() * real.Count)], colour = Cells[cell];
            Cells[cell] = 0;
            Events.Add(new DynamoEvent { Kind = DynamoEventKind.Pick, Slot = slot, Pid = pid, Cell = cell, Colour = colour });
            Charge(s, colour);
        }

        /// <summary>Empty cells reachable from <paramref name="from"/> through empty cells (the Long Reach ability's move).</summary>
        public bool[] Reach(int from)
        {
            var seen = new bool[CellCount];
            var q = new List<int> { from };
            seen[from] = true;
            for (int h = 0; h < q.Count; h++)
                foreach (int v in Neighbours[q[h]])
                    if (!seen[v] && Cells[v] == 0) { seen[v] = true; q.Add(v); }
            seen[from] = false;
            return seen;
        }

        static bool InBoard(int i) => i >= 0 && i < CellCount;

        /// <summary>One seat input. True when it was legal and applied; an illegal input changes nothing.</summary>
        public bool Apply(DynamoSettings s, DynamoInput inp)
        {
            if (inp.Kind == DynamoInputKind.Pick)   // round 7: the Ratel picks one of the offered abilities; then the new ports
            {
                if (RevealState?.Offers == null || inp.Slot < 0 || inp.Slot >= RevealState.Offers.Length) return false;
                TakeOffer(inp.Slot);
                RevealStep(s);
                return true;
            }
            if (RevealState != null) return false;   // the clock is stopped while the new ability and ports are shown
            switch (inp.Kind)
            {
                case DynamoInputKind.Tinker:
                {
                    if (!InBoard(inp.From) || !InBoard(inp.To) || inp.From == inp.To || Cells[inp.From] == 0) return false;
                    bool full = Cells[inp.To] != 0;
                    if (full && !(Ability == DynamoAbility.Swap && IsNeighbour(inp.From, inp.To))) return false;
                    if (!full && !IsNeighbour(inp.From, inp.To) && !(Ability == DynamoAbility.LongReach && Reach(inp.From)[inp.To])) return false;
                    if (!SpendAp(1)) return false;
                    int a = Cells[inp.From];
                    Cells[inp.From] = Cells[inp.To];
                    Cells[inp.To] = a;
                    Events.Add(new DynamoEvent { Kind = DynamoEventKind.Tinker, From = inp.From, To = inp.To });
                    return true;
                }
                case DynamoInputKind.Refuel:
                {
                    if (!InBoard(inp.Cell) || Cells[inp.Cell] != 0) return false;
                    if (Free.Count > 0 && inp.Colour >= 1 && inp.Colour <= 3)   // a free prism: any empty cell, its own domain, no AP
                    {
                        if (inp.Colour != Free[0]) return false;
                        Free.RemoveAt(0);
                        Cells[inp.Cell] = inp.Colour;
                        Events.Add(new DynamoEvent { Kind = DynamoEventKind.Place, Cell = inp.Cell, Colour = inp.Colour });
                        return true;
                    }
                    int pc = PortAt(inp.Cell);
                    if (pc == 0 || !SpendAp(1)) return false;
                    Cells[inp.Cell] = pc;
                    Events.Add(new DynamoEvent { Kind = DynamoEventKind.Refuel, Cell = inp.Cell, Colour = pc });
                    return true;
                }
                case DynamoInputKind.Remove:   // Garrett 2026-10-21: right-click a prism to delete it, for 1 AP
                {
                    if (!InBoard(inp.Cell) || Cells[inp.Cell] == 0 || !SpendAp(1)) return false;
                    int colour = Cells[inp.Cell];
                    Cells[inp.Cell] = 0;
                    Events.Add(new DynamoEvent { Kind = DynamoEventKind.Remove, Cell = inp.Cell, Colour = colour });
                    return true;
                }
                case DynamoInputKind.Activate:
                {
                    if (inp.Slot < 0 || inp.Slot >= Slots.Length || Slots[inp.Slot] < 0) return false;
                    int pid = Slots[inp.Slot], cost = Ability == DynamoAbility.FreeFire ? 0 : 1;
                    var cells = Match(s, pid);
                    if (cells == null || Ap + BonusAp < cost) return false;
                    SpendAp(cost);
                    Uses[inp.Slot]++;
                    Made++;
                    Events.Add(new DynamoEvent { Kind = DynamoEventKind.Ability, Slot = inp.Slot, Pid = pid, Use = Uses[inp.Slot] });
                    if (Ability == DynamoAbility.Refund) BonusAp = BonusAp + 1 > s.BonusApCap ? s.BonusApCap : BonusAp + 1;
                    TakeRandom(s, inp.Slot, pid, cells);
                    if (s.UsesPerPattern > 0 && Uses[inp.Slot] >= s.UsesPerPattern)   // made for the third time: a new pattern
                    {
                        int old = Slots[inp.Slot];
                        Slots[inp.Slot] = -1;
                        Slots[inp.Slot] = DrawPattern(s);
                        Uses[inp.Slot] = 0;
                        if (Slots[inp.Slot] < 0) Slots[inp.Slot] = old;
                        Events.Add(new DynamoEvent { Kind = DynamoEventKind.Retire, Slot = inp.Slot, Old = old, Pid = Slots[inp.Slot] });
                    }
                    if (s.PatternsPerShuffle > 0 && Made % s.PatternsPerShuffle == 0)   // a new ability, then a new port pattern
                    {
                        bool pick = s.FixedAbility < 0 && s.AbilityChoices > 1;
                        DynamoAbility[] offers = null;
                        if (pick) offers = DrawOffers(Pool(s), s.AbilityChoices);
                        else if (s.FixedAbility < 0)
                        {
                            var pool = Pool(s);
                            Ability = (DynamoAbility)pool[PickOther(pool.Length, Array.IndexOf(pool, (int)Ability))];
                        }
                        if (!pick && Ability == DynamoAbility.Printer && Free.Count < 1) GrantFree(1);
                        int next = s.PortSets.Count > 1 ? PickOther(s.PortSets.Count, PortSet) : PortSet;
                        if (pick)   // round 7: the clock stays stopped until the Ratel picks (or PickSeconds runs out)
                        {
                            Events.Add(new DynamoEvent { Kind = DynamoEventKind.Offer, Offers = (DynamoAbility[])offers.Clone(), PortSet = next });
                            RevealState = new Reveal { Stage = 1, T = s.PickSeconds, PortSet = next, Offers = offers };
                        }
                        else
                        {
                            Events.Add(new DynamoEvent { Kind = DynamoEventKind.Shuffle, Ability = Ability, PortSet = next });
                            RevealState = new Reveal { Stage = 1, T = s.RevealAbility, PortSet = next };
                            if (!(s.RevealAbility > 0)) RevealStep(s);
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        List<int> EmptyCells()
        {
            var e = new List<int>();
            for (int i = 0; i < CellCount; i++) if (Cells[i] == 0) e.Add(i);
            return e;
        }

        /// <summary>A pattern's effect on the Dynamo itself (the Ratel-side cards). Returns how many prisms or charges it
        /// touched. Runs on the core's own stream, so a replay matches.</summary>
        public int BoardEffect(DynamoSettings s, DynamoBoardEffect kind, int arg)
        {
            int n = 0;
            switch (kind)
            {
                case DynamoBoardEffect.Burn:
                    for (int k = 0; k < arg; k++)
                    {
                        var full = new List<int>();
                        for (int i = 0; i < CellCount; i++) if (Cells[i] != 0) full.Add(i);
                        if (full.Count == 0) break;
                        Cells[full[(int)Math.Floor(NextRandom() * full.Count)]] = 0;
                        n++;
                    }
                    break;
                case DynamoBoardEffect.Purge:
                    for (int i = 0; i < CellCount; i++) if (Cells[i] == arg) { Cells[i] = 0; n++; }
                    break;
                case DynamoBoardEffect.Free: GrantFree(arg); n = arg; break;
                case DynamoBoardEffect.Bonus: BonusAp += arg; n = arg; break;
                case DynamoBoardEffect.Rewind: Clock = Clock > arg ? Clock - arg : 0; break;
                case DynamoBoardEffect.Unspin:
                    foreach (var p in Ports) for (int k = 0; k < 5; k++) p[0] = Rot60(p[0]);
                    Clock = 0;
                    break;
                case DynamoBoardEffect.Trio:   // a Ruby, a Jade and a Gold prism on three empty cells that touch each other
                {
                    var tris = new List<int[]>();
                    foreach (int a in EmptyCells())
                        foreach (int b in Neighbours[a])
                            if (b > a && Cells[b] == 0)
                                foreach (int c in Neighbours[b])
                                    if (c > b && Cells[c] == 0 && IsNeighbour(a, c)) tris.Add(new[] { a, b, c });
                    if (tris.Count == 0) break;
                    var t = tris[(int)Math.Floor(NextRandom() * tris.Count)];
                    int first = 1 + (int)Math.Floor(NextRandom() * 3);
                    for (int k = 0; k < 3; k++) Cells[t[k]] = 1 + (first - 1 + k) % 3;
                    n = 3;
                    break;
                }
                case DynamoBoardEffect.Scatter:
                    for (int k = 0; k < arg; k++)
                    {
                        var empty = EmptyCells();
                        var e = new List<int>(empty.Count);
                        foreach (int i in empty) if (PortAt(i) == 0) e.Add(i);
                        var pool = e.Count > 0 ? e : empty;
                        if (pool.Count == 0) break;
                        int cell = pool[(int)Math.Floor(NextRandom() * pool.Count)];
                        int colour = 1 + (int)Math.Floor(NextRandom() * 3);
                        Cells[cell] = colour;
                        n++;
                    }
                    break;
                case DynamoBoardEffect.Charge: Charge(s, arg); n = 1; break;
                case DynamoBoardEffect.Drain: n = Battery.Count; Battery.Clear(); break;
                case DynamoBoardEffect.DrainOne:
                    if (Battery.Count > 0) { Battery.RemoveAt(0); n = 1; }
                    break;
            }
            Events.Add(new DynamoEvent { Kind = DynamoEventKind.Effect, Effect = kind, Count = n });
            return n;
        }

        /// <summary>The battery is shared with the pilot: a slip spends one charge of its colour.</summary>
        public bool Spend(int colour)
        {
            int i = Battery.IndexOf(colour);
            if (i < 0) return false;
            Battery.RemoveAt(i);
            return true;
        }

        /// <summary>Spin now, as the clock would (the lab's <c>tick</c>): the hive mode's "the ratel ends their current
        /// timer" (Garrett 2026-10-31) completes the spin at once.</summary>
        public void SpinNow(DynamoSettings s) => Tick(s);

        // The spin: the port pattern turns 60 degrees clockwise as one piece; the prisms stay where they are.
        void Tick(DynamoSettings s)
        {
            foreach (var p in Ports) p[0] = Rot60(p[0]);
            int over = Ability == DynamoAbility.Overclock ? 1 : 0, gain = s.ApPerTick + over, cap = s.ApBank + over;
            int before = Ap;
            Ap = before + gain > cap ? (before > cap ? before : cap) : before + gain;
            Wasted += before + gain - Ap;
            if (Ability == DynamoAbility.Printer && Free.Count < 2) GrantFree(1);
            Ticks++;
            Events.Add(new DynamoEvent { Kind = DynamoEventKind.Tick, Count = Ticks, Ap = gain });
        }

        // The reveal: stage 1 shows the new ability (the old ports stay), stage 2 the new ports; then the spin clock restarts.
        void RevealStep(DynamoSettings s)
        {
            var rv = RevealState;
            if (rv.Stage == 1)
            {
                if (rv.Offers != null) TakeOffer(0);   // round 7: the pick ran out of time
                SetPorts(s, rv.PortSet);
                Events.Add(new DynamoEvent { Kind = DynamoEventKind.Ports, PortSet = rv.PortSet });
                rv.Stage = 2;
                rv.T = s.RevealPorts;
                if (rv.T > 0) return;
            }
            RevealState = null;
            Clock = 0;
            Events.Add(new DynamoEvent { Kind = DynamoEventKind.Resume });
        }

        /// <summary>Advance the Dynamo clock by <paramref name="dt"/> seconds of match time. It stands still during a
        /// reveal.</summary>
        public void Advance(DynamoSettings s, double dt)
        {
            if (RevealState != null)
            {
                if (RevealState.Offers != null && !(s.PickSeconds > 0)) return;   // round 7: no time limit on the pick
                RevealState.T -= dt;
                while (RevealState != null && RevealState.T <= 0) RevealStep(s);
                return;
            }
            Clock += dt;
            while (Clock >= s.TickSeconds)
            {
                Clock -= s.TickSeconds;
                Tick(s);
            }
        }

        // ------------------------------------------------------------------ state copy (a Ratel on another machine)

        /// <summary>
        /// Every field that is the board's state, as ints (a double as its two halves), for a Ratel played on another
        /// machine: the hull's machine runs the Dynamo and sends this; the rider's copy reads it with
        /// <see cref="ReadState"/>. The events and the pattern shapes are not state (shapes come from the settings).
        /// </summary>
        public void WriteState(List<int> into)
        {
            into.Clear();
            into.Add(Cells.Length);
            into.AddRange(Cells);
            into.Add(Ports.Length);
            foreach (var p in Ports) { into.Add(p[0]); into.Add(p[1]); }
            into.Add(PortSet); into.Add(Ap); into.Add(BonusAp); into.Add(Ticks); into.Add(Made); into.Add(Wasted);
            into.Add(Free.Count); into.AddRange(Free);
            AddDouble(into, Clock);
            into.Add(Battery.Count); into.AddRange(Battery);
            into.Add(Slots.Length); into.AddRange(Slots);
            into.Add(Uses.Length); into.AddRange(Uses);
            into.Add(RevealState != null ? 1 : 0);
            if (RevealState != null)
            {
                into.Add(RevealState.Stage); into.Add(RevealState.PortSet); AddDouble(into, RevealState.T);
                into.Add(RevealState.Offers?.Length ?? -1);
                if (RevealState.Offers != null) foreach (var a in RevealState.Offers) into.Add((int)a);
            }
            into.Add((int)Ability);
            into.Add(Rng);
        }

        /// <summary>Overwrite this board with a state <see cref="WriteState"/> wrote. False (and the board untouched past
        /// the point it failed) when the data is not one: a copy from another build.</summary>
        public bool ReadState(int[] d) => ReadState(d, out _);

        /// <summary><see cref="ReadState(int[])"/>, also giving the index just past the board's ints (the hive mode's state
        /// follows them in the same message).</summary>
        public bool ReadState(int[] d, out int end)
        {
            end = 0;
            int i = 0;
            bool Has(int n) => d != null && i + n <= d.Length;
            if (!Has(1) || d[i] != Cells.Length || !Has(1 + Cells.Length)) return false;
            i++;
            Array.Copy(d, i, Cells, 0, Cells.Length); i += Cells.Length;
            if (!Has(1)) return false;
            int ports = d[i++];
            if (ports < 0 || !Has(ports * 2 + 6)) return false;
            Ports = new int[ports][];
            for (int p = 0; p < ports; p++) { Ports[p] = new[] { d[i], d[i + 1] }; i += 2; }
            PortSet = d[i++]; Ap = d[i++]; BonusAp = d[i++]; Ticks = d[i++]; Made = d[i++]; Wasted = d[i++];
            if (!ReadList(d, ref i, Free) || !Has(2)) return false;
            Clock = ReadDouble(d, ref i);
            if (!ReadList(d, ref i, Battery)) return false;
            if (!Has(1) || d[i] != Slots.Length || !Has(1 + Slots.Length)) return false;
            i++; Array.Copy(d, i, Slots, 0, Slots.Length); i += Slots.Length;
            if (!Has(1) || d[i] != Uses.Length || !Has(1 + Uses.Length)) return false;
            i++; Array.Copy(d, i, Uses, 0, Uses.Length); i += Uses.Length;
            if (!Has(1)) return false;
            if (d[i++] == 1)
            {
                if (!Has(4)) return false;
                RevealState ??= new Reveal();
                RevealState.Stage = d[i++]; RevealState.PortSet = d[i++]; RevealState.T = ReadDouble(d, ref i);
                if (!Has(1)) return false;
                int offers = d[i++];
                if (offers > DynamoCore.AbilityCount || !Has(Math.Max(0, offers))) return false;
                RevealState.Offers = offers < 0 ? null : new DynamoAbility[offers];
                for (int k = 0; k < offers; k++) RevealState.Offers[k] = (DynamoAbility)d[i++];
            }
            else RevealState = null;
            if (!Has(2)) return false;
            Ability = (DynamoAbility)d[i++];
            Rng = d[i++];
            end = i;
            return true;
        }

        static void AddDouble(List<int> into, double v)
        {
            long bits = BitConverter.DoubleToInt64Bits(v);
            into.Add((int)(bits & 0xffffffffL));
            into.Add((int)(bits >> 32));
        }

        static double ReadDouble(int[] d, ref int i)
        {
            long bits = (uint)d[i] | ((long)d[i + 1] << 32);
            i += 2;
            return BitConverter.Int64BitsToDouble(bits);
        }

        static bool ReadList(int[] d, ref int i, List<int> into)
        {
            if (i >= d.Length) return false;
            int n = d[i++];
            if (n < 0 || i + n > d.Length) return false;
            into.Clear();
            for (int k = 0; k < n; k++) into.Add(d[i + k]);
            i += n;
            return true;
        }

        /// <summary>The same digest the lab's <c>DynamoCore.digest</c> prints; the parity gate compares the two.</summary>
        public string Digest()
        {
            var sb = new System.Text.StringBuilder();
            foreach (int c in Cells) sb.Append(c);
            sb.Append('|');
            for (int p = 0; p < Ports.Length; p++) { if (p > 0) sb.Append(','); sb.Append(Ports[p][0]).Append(':').Append(Ports[p][1]); }
            sb.Append('|').Append(Ap).Append('+').Append(BonusAp).Append('|');
            foreach (int b in Battery) sb.Append(b);
            sb.Append('|').Append(string.Join(",", Slots)).Append('/').Append(string.Join(",", Uses)).Append('|');
            if (RevealState == null) sb.Append('-');
            else
            {
                sb.Append(RevealState.Stage).Append(':').Append(RevealState.PortSet);
                if (RevealState.Offers != null) sb.Append(':').Append(string.Join(",", RevealState.Offers.Select(a => AbilityKeys[(int)a])));
            }
            sb.Append('|');
            foreach (int c in Free) sb.Append(c);
            sb.Append('|').Append(AbilityKeys[(int)Ability]).Append('|');
            sb.Append(Ticks).Append('|').Append(Made).Append('|').Append(Wasted).Append('|').Append(unchecked((uint)Rng));
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the Engineer AI (input-only)

        /// <summary>One way to make a pattern: its cells, the AP it still needs and the first input of each fix.</summary>
        public sealed class Placement
        {
            public List<int> Cells;
            public double Total;
            public List<Fix> Fixes;
            public int Pid;
            public string Key;
        }

        public struct Fix
        {
            public double Cost;
            public DynamoInput First;
        }

        /// <summary>The placement the AI is building, kept between its thoughts so it does not swap plans (and move the same
        /// prism back and forth) over a small saving.</summary>
        public sealed class AIMemo
        {
            public string Key = "";
        }

        Placement PlacementCost(DynamoSettings s, int[][] shape, int a, bool[] locked)
        {
            var cells = new List<int>(shape.Length);
            var need = new List<int>(shape.Length);
            for (int c = 0; c < shape.Length; c++)
            {
                int i = Index(Q[a] + shape[c][0], R[a] + shape[c][1]);
                if (i < 0) return null;
                cells.Add(i);
                need.Add(shape[c][2]);
            }
            double total = 0;
            bool real = false;
            var fixes = new List<Fix>();
            var onPorts = new List<int>();
            for (int c = 0; c < cells.Count; c++)
            {
                int i = cells[c], want = need[c];
                if (Cells[i] == want || (Ability == DynamoAbility.WildCore && i == Centre)) { if (Cells[i] != 0) real = true; continue; }
                if (Cells[i] == 0 && s.PortsCount && PortAt(i) == want) { onPorts.Add(i); continue; }
                var fix = CellFix(s, i, want, cells, locked);
                if (fix == null) return null;
                total += fix.Value.Cost;
                fixes.Add(fix.Value);
                real = true;
            }
            // An empty port only counts until the spin moves it. If the pattern cannot be finished (and made) before the
            // spin, inject at those ports now so the prisms stay when the ports turn away.
            int apNow = Ap + BonusAp;
            double actions = total + (Ability == DynamoAbility.FreeFire ? 0 : 1), timeLeft = s.TickSeconds - Clock;
            bool durable = onPorts.Count > 0 && (actions > apNow || actions * 0.5 > timeLeft);
            if (durable)
                foreach (int i in onPorts) { fixes.Add(new Fix { Cost = 1, First = DynamoInput.Refuel(i) }); total += 1; real = true; }
            if (!real)   // every cell is an empty port: put one real prism on one of them
            {
                if (onPorts.Count == 0) return null;
                fixes.Add(new Fix { Cost = 1, First = DynamoInput.Refuel(onPorts[0]) });
                total += 1;
            }
            return new Placement { Cells = cells, Total = total, Fixes = fixes };
        }

        // The cheapest way to make cell i show colour `want`, and that way's first input.
        Fix? CellFix(DynamoSettings s, int i, int want, List<int> inPlacement, bool[] locked)
        {
            int extra = 0;
            DynamoInput? clear = null;
            if (Cells[i] != 0)   // a wrong prism sits there: move it aside first (or swap a right one in)
            {
                if (Ability == DynamoAbility.Swap)
                    foreach (int n in Neighbours[i])
                        if (Cells[n] == want && !locked[n]) return new Fix { Cost = 1, First = DynamoInput.Tinker(n, i) };
                extra = 1;
                foreach (int n in Neighbours[i]) if (Cells[n] == 0 && !inPlacement.Contains(n)) { clear = DynamoInput.Tinker(i, n); break; }
                if (clear == null) foreach (int n in Neighbours[i]) if (Cells[n] == 0) { clear = DynamoInput.Tinker(i, n); break; }
                if (clear == null) return null;
                if (s.PortsCount && PortAt(i) == want) return new Fix { Cost = 1, First = clear.Value };   // cleared, the port shows the colour
            }
            if (Free.Count > 0 && Free[0] == want && Cells[i] == 0) return new Fix { Cost = 0.5, First = DynamoInput.Refuel(i, want) };
            var dist = new int[CellCount];
            for (int k = 0; k < CellCount; k++) dist[k] = -1;
            var queue = new List<int> { i };
            dist[i] = 0;
            for (int h = 0; h < queue.Count; h++)
            {
                int u = queue[h];
                foreach (int v in Neighbours[u]) if (dist[v] < 0 && Cells[v] == 0) { dist[v] = dist[u] + 1; queue.Add(v); }
            }
            bool longReach = Ability == DynamoAbility.LongReach;
            Fix? best = null;
            if (PortAt(i) == want) best = new Fix { Cost = 1, First = DynamoInput.Refuel(i) };
            for (int m = 0; m < CellCount; m++)
            {
                if (Cells[m] != want || m == i || locked[m]) continue;
                int bn = -1;
                foreach (int n in Neighbours[m]) if (dist[n] >= 0 && (bn < 0 || dist[n] < dist[bn])) bn = n;
                if (bn < 0) continue;
                double cost = longReach ? 1 : dist[bn] + 1;
                if (best == null || cost < best.Value.Cost)
                    best = new Fix { Cost = cost, First = DynamoInput.Tinker(m, longReach && Cells[i] == 0 ? i : bn) };
            }
            foreach (var p in Ports)
            {
                if (p[1] != want || p[0] == i || dist[p[0]] < 0 || Cells[p[0]] != 0) continue;
                double cost = 1 + (longReach ? 1 : dist[p[0]]);
                if (best == null || cost < best.Value.Cost) best = new Fix { Cost = cost, First = DynamoInput.Refuel(p[0]) };
            }
            if (best == null) return null;
            return new Fix { Cost = best.Value.Cost + extra, First = clear ?? best.Value.First };
        }

        /// <summary>Every way to make a pattern in a slot, cheapest first (ties by pattern index, then scan order).</summary>
        public List<Placement> BestPlacements(DynamoSettings s, int top)
        {
            var found = new List<Placement>();
            for (int sl = 0; sl < Slots.Length; sl++)
            {
                int pid = Slots[sl];
                if (pid < 0) continue;
                var sets = Ability == DynamoAbility.Mirror ? shapes[pid].Rot.Concat(shapes[pid].Mir).ToArray() : shapes[pid].Rot;
                for (int k = 0; k < sets.Length; k++)
                    for (int a = 0; a < CellCount; a++)
                    {
                        var shape = sets[k];
                        var locked = new bool[CellCount];
                        bool inBoard = true;
                        foreach (var c in shape)
                        {
                            int i = Index(Q[a] + c[0], R[a] + c[1]);
                            if (i < 0) { inBoard = false; break; }
                            if (Cells[i] == c[2]) locked[i] = true;
                        }
                        if (!inBoard) continue;
                        var pl = PlacementCost(s, shape, a, locked);
                        if (pl != null) { pl.Pid = pid; pl.Key = pid + ":" + k + ":" + a; found.Add(pl); }
                    }
            }
            // a STABLE sort, as the lab's Array.prototype.sort is
            var sorted = found.OrderBy(x => x.Total).ThenBy(x => x.Pid).ToList();
            return top > 0 && sorted.Count > top ? sorted.GetRange(0, top) : sorted;
        }

        public List<DynamoInput> LegalInputs()
        {
            var legal = new List<DynamoInput>();
            for (int i = 0; i < CellCount; i++)
                if (Cells[i] != 0)
                    foreach (int n in Neighbours[i]) if (Cells[n] == 0) legal.Add(DynamoInput.Tinker(i, n));
            foreach (var p in Ports) if (Cells[p[0]] == 0) legal.Add(DynamoInput.Refuel(p[0]));
            return legal;
        }

        /// <summary>The Engineer AI: input-only. ONE input (or null) from the state it sees; the caller paces it.
        /// <paramref name="rnd"/> is the AI's own dice; <paramref name="memo"/> (optional) keeps the placement it is
        /// building.</summary>
        public DynamoInput? AIPlan(DynamoSettings s, DynamoAISkill prm, Func<double> rnd, AIMemo memo)
        {
            if (RevealState != null) return RevealState.Offers != null ? DynamoInput.Pick(AIPick(RevealState.Offers)) : null;
            bool free = Ability == DynamoAbility.FreeFire;
            if (Ap + BonusAp < 1 && Free.Count < 1 && !free) return null;
            if (free || Ap + BonusAp >= 1)
                for (int sl = 0; sl < Slots.Length; sl++)
                    if (Slots[sl] >= 0 && Match(s, Slots[sl]) != null) return DynamoInput.Activate(sl);
            if (Ap + BonusAp < 1 && Free.Count < 1) return null;
            if (prm.Blunder > 0 && rnd() < prm.Blunder)
            {
                var legal = LegalInputs();
                if (legal.Count > 0) return legal[(int)Math.Floor(rnd() * legal.Count)];
            }
            var all = BestPlacements(s, 0);
            if (all.Count == 0) return null;
            Placement pl = null;
            if (memo != null && !string.IsNullOrEmpty(memo.Key))
                foreach (var x in all)
                    if (x.Key == memo.Key) { if (x.Total <= all[0].Total + 1.5) pl = x; break; }
            if (pl == null)
            {
                int n = Math.Min(all.Count, Math.Max(1, prm.Top));
                pl = all[prm.Top > 1 ? (int)Math.Floor(rnd() * n) : 0];
            }
            if (memo != null) memo.Key = pl.Key;
            Fix? fix = null;
            foreach (var f in pl.Fixes) if (fix == null || f.Cost < fix.Value.Cost) fix = f;
            if (fix != null && Ap + BonusAp < 1 && !(fix.Value.First.Kind == DynamoInputKind.Refuel && fix.Value.First.Colour != 0)) return null;
            return fix?.First;
        }

        // ------------------------------------------------------------------ the Slip

        static SlipVec Sub(SlipVec a, SlipVec b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        static double Dot(SlipVec a, SlipVec b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        static double Len(SlipVec a) => Math.Sqrt(Dot(a, a));
        static SlipVec Norm(SlipVec a)
        {
            double l = Len(a);
            return l > 1e-9 ? new SlipVec(a.X / l, a.Y / l, a.Z / l) : new SlipVec(0, 0, 0);
        }

        /// <summary>
        /// The Slip's destination: of the trail's two ends (points oldest first), the one the hull is closer to pointing at,
        /// landed <paramref name="exitOffset"/> past it along the trail's outward heading. Beyond <paramref name="reach"/>,
        /// the farthest block within reach walking from the contact toward that end. Null for a trail of fewer than 2 points.
        /// </summary>
        public static SlipLanding? SlipDestination(IReadOnlyList<SlipVec> pts, int contact, SlipVec pos, SlipVec fwd, double reach, double exitOffset)
        {
            int n = pts.Count;
            if (n < 2) return null;
            double d0 = Dot(fwd, Norm(Sub(pts[0], pos))), d1 = Dot(fwd, Norm(Sub(pts[n - 1], pos)));
            int end = d1 >= d0 ? 1 : 0, step = end == 1 ? 1 : -1;
            int at = end == 1 ? n - 1 : 0;
            bool clipped = false;
            if (Len(Sub(pts[at], pos)) > reach)
            {
                clipped = true;
                at = contact;
                for (int j = contact; j >= 0 && j < n; j += step) if (Len(Sub(pts[j], pos)) <= reach) at = j;
            }
            int prev = at - step >= 0 && at - step < n ? at - step : at + step;
            var heading = Norm(Sub(pts[at], pts[prev]));
            if (prev == at + step) heading = new SlipVec(-heading.X, -heading.Y, -heading.Z);
            var p = pts[at];
            return new SlipLanding
            {
                End = end, Index = at, Clipped = clipped, Heading = heading,
                Point = new SlipVec(p.X + heading.X * exitOffset, p.Y + heading.Y * exitOffset, p.Z + heading.Z * exitOffset),
            };
        }
    }
}
