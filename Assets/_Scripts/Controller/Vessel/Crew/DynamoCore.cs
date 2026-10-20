using System;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>What a pattern does beyond lighting up. The Engineer-side effects (Chrono-Compressor, 5D Printer, Anode Ray,
    /// Big Red Button, Amanita Furnace) run inside <see cref="DynamoCore"/>; the rest are the pilot's and are applied by the
    /// hull. Every member keeps its value: these are serialized on pattern assets.</summary>
    public enum DynamoPatternEffect
    {
        None = 0,
        AmanitaFurnace = 1,
        BigRedButton = 2,
        DragonSaddle = 3,
        ThrusterSwarm = 4,
        RealityDetangler = 5,
        EpitaxialCondenser = 6,
        AnodeRay = 7,
        FiveDPrinter = 8,
        BlinkDrive = 9,
        RosenBridge = 10,
        MonteCannon = 11,
        ChronoCompressor = 12,
    }

    /// <summary>One pattern card: its family colour (1 Mass, 2 Charge, 3 Space-Time), its effect, and its shape as
    /// (q, r, colour) cells in the card's printed orientation, r running down.</summary>
    public sealed class DynamoPattern
    {
        public int Family;
        public DynamoPatternEffect Effect;
        public int[][] Cells;
    }

    /// <summary>The Dynamo numbers the core reads (from the Honeyguide's Dynamo config). A plain class so the core and its
    /// tests need no ScriptableObject.</summary>
    public sealed class DynamoSettings
    {
        public double TickSeconds;
        public int ApPerTick, ApBank, BonusApCap, BatteryCap, ResearchCost;
        public double PickTimeout;
        public bool RotateWholeEngine, AllowRotatedMatches, CentreWild, StartFilled;
        /// <summary>Injection ports as (cell index, colour) at the start of a match.</summary>
        public int[][] Ports;
        public List<DynamoPattern> Patterns = new();
        /// <summary>The slots' pattern indices at launch (-1 = empty slot).</summary>
        public int[] Loadout;
    }

    public enum DynamoInputKind
    {
        Tinker = 0,
        Refuel = 1,
        Activate = 2,
        Pick = 3,
        Research = 4,
        Keep = 5,
        Discard = 6,
    }

    /// <summary>One Engineer input. A human's tap and the AI's choice are the same value.</summary>
    public struct DynamoInput
    {
        public DynamoInputKind Kind;
        public int From, To, Cell, Colour, Slot, Family, Choice;
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
        Destroyed = 7,
        Burned = 8,
        Offer = 9,
        Keep = 10,
        Tick = 11,
    }

    public struct DynamoEvent
    {
        public DynamoEventKind Kind;
        public int Slot, Pid, Cell, From, To, Colour, Count, Family;
        public bool Auto;
        public int[] Offer;
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

    /// <summary>
    /// The Ratel's hex puzzle (the Engineer seat of the Honeyguide), engine-free: its only inputs are seat inputs and a
    /// clock. The Honeyguide Lab's <c>DynamoCore</c> (<c>Docs/Studios/HoneyguideLab.html</c>, the <c>dynamo-core</c> script
    /// block) ported line for line and held to it input by input by <c>Tools/Build/dynamo_lab/parity.cjs</c>. It knows
    /// nothing about prisms, hulls or screens. The whole state is 37 cells, the ports, the AP, the battery and 3 slots, so
    /// it can later replicate by sending Engineer inputs to the hull owner.
    /// </summary>
    public sealed class DynamoCore
    {
        public const int Radius = 3;
        static readonly int[][] Dirs = { new[] { 1, 0 }, new[] { 1, -1 }, new[] { 0, -1 }, new[] { -1, 0 }, new[] { -1, 1 }, new[] { 0, 1 } };
        public static readonly int[] Q, R;
        public static readonly int CellCount, Centre;
        static readonly int[] Lut = new int[49];
        public static readonly int[][] Rings;
        static readonly int[] RingPos;
        public static readonly int[][] Neighbours;

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
            Rings = new int[Radius + 1][];
            Rings[0] = new[] { Centre };
            RingPos = new int[CellCount];
            int[][] legs = { new[] { 1, 0 }, new[] { 0, 1 }, new[] { -1, 1 }, new[] { -1, 0 }, new[] { 0, -1 }, new[] { 1, -1 } };
            for (int k = 1; k <= Radius; k++)
            {
                var ring = new List<int>();
                int cq = 0, cr = -k;
                foreach (var d in legs)
                    for (int s = 0; s < k; s++) { ring.Add(Index(cq, cr)); cq += d[0]; cr += d[1]; }
                Rings[k] = ring.ToArray();
                for (int p = 0; p < ring.Count; p++) RingPos[ring[p]] = p;
            }
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
        public static int StepClockwise(int i)
        {
            int k = RingOf(i);
            if (k == 0) return i;
            var ring = Rings[k];
            return ring[(RingPos[i] + 1) % ring.Length];
        }
        /// <summary>60 degrees clockwise about the centre.</summary>
        public static int Rot60(int i) => Index(-R[i], Q[i] + R[i]);
        public static bool IsNeighbour(int a, int b) => Array.IndexOf(Neighbours[a], b) >= 0;

        /// <summary>The pattern's shape in each allowed rotation, as offsets from its first cell.</summary>
        public static int[][][] Shapes(int[][] cells, bool allowRotated)
        {
            var outShapes = new List<int[][]>();
            var cur = new int[cells.Length][];
            for (int c = 0; c < cells.Length; c++) cur[c] = new[] { cells[c][0], cells[c][1], cells[c][2] };
            for (int k = 0; k < (allowRotated ? 6 : 1); k++)
            {
                var shape = new int[cur.Length][];
                for (int c = 0; c < cur.Length; c++) shape[c] = new[] { cur[c][0] - cur[0][0], cur[c][1] - cur[0][1], cur[c][2] };
                outShapes.Add(shape);
                var next = new int[cur.Length][];
                for (int c = 0; c < cur.Length; c++) next[c] = new[] { -cur[c][1], cur[c][0] + cur[c][1], cur[c][2] };
                cur = next;
            }
            return outShapes.ToArray();
        }

        public sealed class PendingPick
        {
            public int Slot, Pid;
            public List<int> Cells;
            public double Timer;
        }

        public int[] Cells = new int[CellCount];
        /// <summary>Each port as (cell, colour); the cell moves on every tick.</summary>
        public int[][] Ports;
        public int Ap, BonusAp, Ticks, Wasted, FreePlace;
        public double Clock;
        public readonly List<int> Battery = new();
        public int[] Slots;
        public PendingPick Pick;
        public int[] Offer;
        public int Rng;
        public readonly List<DynamoEvent> Events = new();
        int[][][][] shapes;

        public DynamoCore(DynamoSettings s, int seed)
        {
            Ports = new int[s.Ports.Length][];
            for (int p = 0; p < s.Ports.Length; p++) Ports[p] = new[] { s.Ports[p][0], s.Ports[p][1] };
            Ap = s.ApPerTick < s.ApBank ? s.ApPerTick : s.ApBank;
            Slots = (int[])s.Loadout.Clone();
            Rng = seed;
            shapes = new int[s.Patterns.Count][][][];
            for (int p = 0; p < s.Patterns.Count; p++) shapes[p] = Shapes(s.Patterns[p].Cells, s.AllowRotatedMatches);
            if (s.StartFilled) foreach (var p in Ports) Cells[p[0]] = p[1];
        }

        double NextRandom()
        {
            unchecked
            {
                uint a = (uint)Rng + 0x6D2B79F5u;
                Rng = (int)a;
                uint t = (a ^ (a >> 15)) * (1u | a);
                t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        public int PortAt(int cell)
        {
            foreach (var p in Ports) if (p[0] == cell) return p[1];
            return 0;
        }

        /// <summary>The cells of the first placement of pattern <paramref name="pid"/> the board shows, or null. Placement
        /// order: rotation, then the anchor cell in index order.</summary>
        public List<int> Match(DynamoSettings s, int pid)
        {
            var sh = shapes[pid];
            for (int k = 0; k < sh.Length; k++)
            {
                var shape = sh[k];
                for (int a = 0; a < CellCount; a++)
                {
                    var cells = new List<int>(shape.Length);
                    bool ok = true;
                    for (int c = 0; c < shape.Length; c++)
                    {
                        int i = Index(Q[a] + shape[c][0], R[a] + shape[c][1]);
                        if (i < 0) { ok = false; break; }
                        if (Cells[i] == shape[c][2] || (s.CentreWild && i == Centre)) cells.Add(i);
                        else { ok = false; break; }
                    }
                    if (ok) return cells;
                }
            }
            return null;
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
            if (s.BatteryCap < 1) return;
            if (Battery.Count >= s.BatteryCap)
            {
                int old = Battery[0];
                Battery.RemoveAt(0);
                Events.Add(new DynamoEvent { Kind = DynamoEventKind.Replaced, Colour = old });
            }
            Battery.Add(colour);
            Events.Add(new DynamoEvent { Kind = DynamoEventKind.Charge, Colour = colour });
        }

        void PickCell(DynamoSettings s, int cell, bool auto)
        {
            var pk = Pick;
            var pat = s.Patterns[pk.Pid];
            int colour = Cells[cell];
            Cells[cell] = 0;
            Pick = null;
            Events.Add(new DynamoEvent { Kind = DynamoEventKind.Pick, Slot = pk.Slot, Pid = pk.Pid, Cell = cell, Colour = colour, Auto = auto });
            Charge(s, colour);
            if (pat.Effect == DynamoPatternEffect.BigRedButton)
            {
                int n = 0;
                for (int i = 0; i < CellCount; i++) if (Cells[i] == colour) { Cells[i] = 0; n++; }
                Events.Add(new DynamoEvent { Kind = DynamoEventKind.Destroyed, Colour = colour, Count = n });
            }
            else if (pat.Effect == DynamoPatternEffect.AmanitaFurnace)
            {
                foreach (int c in pk.Cells)
                    if (c != cell && Cells[c] != 0)
                    {
                        Events.Add(new DynamoEvent { Kind = DynamoEventKind.Burned, Cell = c, Colour = Cells[c] });
                        Cells[c] = 0;
                        break;
                    }
            }
        }

        static bool InBoard(int i) => i >= 0 && i < CellCount;

        /// <summary>One seat input. True when it was legal and applied; an illegal input changes nothing.</summary>
        public bool Apply(DynamoSettings s, DynamoInput inp)
        {
            switch (inp.Kind)
            {
                case DynamoInputKind.Tinker:
                    if (Pick != null || !InBoard(inp.From) || !InBoard(inp.To)) return false;
                    if (Cells[inp.From] == 0 || Cells[inp.To] != 0 || !IsNeighbour(inp.From, inp.To)) return false;
                    if (!SpendAp(1)) return false;
                    Cells[inp.To] = Cells[inp.From];
                    Cells[inp.From] = 0;
                    Events.Add(new DynamoEvent { Kind = DynamoEventKind.Tinker, From = inp.From, To = inp.To });
                    return true;
                case DynamoInputKind.Refuel:
                {
                    if (Pick != null || !InBoard(inp.Cell) || Cells[inp.Cell] != 0) return false;
                    if (FreePlace > 0 && inp.Colour >= 1 && inp.Colour <= 3)
                    {
                        FreePlace--;
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
                case DynamoInputKind.Activate:
                {
                    if (Pick != null || inp.Slot < 0 || inp.Slot >= Slots.Length || Slots[inp.Slot] < 0) return false;
                    int pid = Slots[inp.Slot];
                    var cells = Match(s, pid);
                    if (cells == null || Ap + BonusAp < 1) return false;
                    SpendAp(1);
                    var pat = s.Patterns[pid];
                    Events.Add(new DynamoEvent { Kind = DynamoEventKind.Ability, Slot = inp.Slot, Pid = pid });
                    if (pat.Effect == DynamoPatternEffect.ChronoCompressor) BonusAp = BonusAp + 1 > s.BonusApCap ? s.BonusApCap : BonusAp + 1;
                    else if (pat.Effect == DynamoPatternEffect.FiveDPrinter) FreePlace += 2;
                    else if (pat.Effect == DynamoPatternEffect.AnodeRay) { Charge(s, 1); Charge(s, 2); Charge(s, 3); }
                    var live = new List<int>();
                    foreach (int c in cells) if (Cells[c] != 0) live.Add(c);
                    if (live.Count > 0) Pick = new PendingPick { Slot = inp.Slot, Pid = pid, Cells = live, Timer = s.PickTimeout };
                    return true;
                }
                case DynamoInputKind.Pick:
                    if (Pick == null || !Pick.Cells.Contains(inp.Cell) || Cells[inp.Cell] == 0) return false;
                    PickCell(s, inp.Cell, false);
                    return true;
                case DynamoInputKind.Research:
                {
                    if (Pick != null || Offer != null || inp.Family < 1 || inp.Family > 3) return false;
                    var cand = new List<int>();
                    for (int p = 0; p < s.Patterns.Count; p++) if (s.Patterns[p].Family == inp.Family && Array.IndexOf(Slots, p) < 0) cand.Add(p);
                    if (cand.Count == 0 || !SpendAp(s.ResearchCost)) return false;
                    var offer = new List<int>();
                    for (int k = 0; k < 3 && cand.Count > 0; k++)
                    {
                        int j = (int)Math.Floor(NextRandom() * cand.Count);
                        offer.Add(cand[j]);
                        cand.RemoveAt(j);
                    }
                    Offer = offer.ToArray();
                    Events.Add(new DynamoEvent { Kind = DynamoEventKind.Offer, Family = inp.Family, Offer = (int[])Offer.Clone() });
                    return true;
                }
                case DynamoInputKind.Keep:
                    if (Offer == null || inp.Choice < 0 || inp.Choice >= Offer.Length || inp.Slot < 0 || inp.Slot >= Slots.Length) return false;
                    Slots[inp.Slot] = Offer[inp.Choice];
                    Events.Add(new DynamoEvent { Kind = DynamoEventKind.Keep, Slot = inp.Slot, Pid = Offer[inp.Choice] });
                    Offer = null;
                    return true;
                case DynamoInputKind.Discard:
                    if (Offer == null) return false;
                    Offer = null;
                    return true;
            }
            return false;
        }

        /// <summary>A slip spends one charge of its colour from the battery the two seats share.</summary>
        public bool Spend(int colour)
        {
            int i = Battery.IndexOf(colour);
            if (i < 0) return false;
            Battery.RemoveAt(i);
            return true;
        }

        public int DrainAll()
        {
            int n = Battery.Count;
            Battery.Clear();
            return n;
        }

        void Tick(DynamoSettings s)
        {
            if (s.RotateWholeEngine)
            {
                var next = new int[CellCount];
                for (int i = 0; i < CellCount; i++) next[Rot60(i)] = Cells[i];
                Cells = next;
                foreach (var p in Ports) p[0] = Rot60(p[0]);
                if (Pick != null) for (int c = 0; c < Pick.Cells.Count; c++) Pick.Cells[c] = Rot60(Pick.Cells[c]);
            }
            else
            {
                foreach (var p in Ports) p[0] = StepClockwise(p[0]);
            }
            int before = Ap, cap = s.ApBank;
            Ap = before + s.ApPerTick > cap ? (before > cap ? before : cap) : before + s.ApPerTick;
            Wasted += before + s.ApPerTick - Ap;
            Ticks++;
            Events.Add(new DynamoEvent { Kind = DynamoEventKind.Tick, Count = Ticks });
        }

        /// <summary>Advance the Dynamo clock by <paramref name="dt"/> seconds of match time.</summary>
        public void Advance(DynamoSettings s, double dt)
        {
            if (Pick != null)
            {
                Pick.Timer -= dt;
                if (Pick.Timer <= 0)
                {
                    int fam = s.Patterns[Pick.Pid].Family;
                    int cell = Pick.Cells[0];
                    foreach (int c in Pick.Cells) if (Cells[c] == fam) { cell = c; break; }
                    PickCell(s, cell, true);
                }
            }
            Clock += dt;
            while (Clock >= s.TickSeconds)
            {
                Clock -= s.TickSeconds;
                Tick(s);
            }
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
            sb.Append('|').Append(string.Join(",", Slots)).Append('|');
            sb.Append(Pick != null ? Pick.Slot + ":" + string.Join(".", Pick.Cells) : "-").Append('|');
            sb.Append(FreePlace).Append('|');
            sb.Append(Offer != null ? string.Join(".", Offer) : "-").Append('|');
            sb.Append(Ticks).Append('|').Append(Wasted).Append('|').Append(unchecked((uint)Rng));
            return sb.ToString();
        }

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
