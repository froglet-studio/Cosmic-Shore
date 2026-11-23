using System;
using System.Collections.Generic;
using System.Linq;

namespace CosmicShore.Gameplay
{
    /// <summary>The hive's numbers the core reads (from the Honeyguide's config). A plain class so the core and its tests need
    /// no ScriptableObject. Field names are the Ratel Raid Lab's <c>SHIPPED</c> keys.</summary>
    public sealed class HiveSettings
    {
        public int MinChain = 3, SnakeMin = 3, SnakeMax = 5, HiveHoles = 2, HiveDangerMin = 2, RewardDynamo = 2, RewardBattery = 1;
        public double HiveOtherShare = 0.45, HiveCross = 0.35, HiveArmour = 0.15, StingSeconds = 1, RaidSeconds = 15;
    }

    /// <summary>One hive cell: its domain (a Dynamo colour, 0 a hole), the hits left (a shield is one more) and whether it is
    /// a danger prism (never chained; a chain that touches one stings).</summary>
    public sealed class HiveCell
    {
        public int Dom, Hits;
        public bool Danger;
    }

    /// <summary>A hive in play: its cells, the solver's solution, the chain in the thumb, the stun after a sting, the colours
    /// completed and the rewards they earned.</summary>
    public sealed class Hive
    {
        public HiveCell[] Cells;
        public List<int[]> Solution;
        public int Regenerated, Broken, Chains, Stings;
        public readonly List<int> Chain = new(), Done = new(), Rewards = new();
        public double Stun;
        /// <summary>Each other colour's standing cells when the hive opened (index = colour).</summary>
        public readonly int[] Total = new int[4];
    }

    /// <summary>The largest planar crystal on a board: the colour permutation, its cells (ascending) and their count.</summary>
    public sealed class PlanarCrystal
    {
        public int[] Perm;
        public List<int> Cells;
        public int Size;
    }

    /// <summary>
    /// The Ratel's hive mode (Garrett 2026-10-31), engine-free: the hive's generation, the solver, the chain rules, the colour
    /// rewards, the AI's next chain, and the planar crystal the rewards land on. The Ratel Raid Lab's <c>HiveCore</c>
    /// (<c>Docs/Studios/HoneyguideRaidLab.html</c>, the <c>hive-core</c> script block) ported line for line and held to it by
    /// <c>Tools/Build/hive_lab/parity.cjs</c>. Colours are the Dynamo's (0 empty, 1 Ruby, 2 Jade, 3 Gold); <c>own</c> is the
    /// player's. Every random draw comes from the <c>rnd</c> passed in (<see cref="DynamoCore.Mulberry32"/>).
    /// </summary>
    public static class HiveCore
    {
        public static readonly int[][] Perms = { new[] { 1, 2, 3 }, new[] { 1, 3, 2 }, new[] { 2, 1, 3 }, new[] { 2, 3, 1 }, new[] { 3, 1, 2 }, new[] { 3, 2, 1 } };

        /// <summary>Every one of the 39 patterns is cut from the one 3-colouring of the hex grid: colour class k = (q - r) mod
        /// 3, and two neighbours never share a class. A crystal is a permutation perm (prism colour = perm[k]).</summary>
        public static readonly int[] Class = BuildClass();

        static int[] BuildClass()
        {
            var c = new int[DynamoCore.CellCount];
            for (int i = 0; i < c.Length; i++) c[i] = ((DynamoCore.Q[i] - DynamoCore.R[i]) % 3 + 3) % 3;
            return c;
        }

        // Cached (perf pass 2026-11-02): same contents and order as { 1, 2, 3 } minus own; callers only read them.
        static readonly int[][] s_others = { null, new[] { 2, 3 }, new[] { 1, 3 }, new[] { 1, 2 } };
        static readonly int[] s_othersAll = { 1, 2, 3 };
        public static int[] OthersOf(int own) => own >= 1 && own <= 3 ? s_others[own] : s_othersAll;

        // JavaScript's Math.round (half up), not .NET's banker's rounding.
        static int JsRound(double v) => (int)Math.Floor(v + 0.5);
        static int Floor(double v) => (int)Math.Floor(v);

        // ------------------------------------------------------------------ the planar crystal

        /// <summary>Over the six perms, the largest connected set of prisms whose colour equals perm[class]. Ties go to the
        /// earlier perm, then the lower cell. Size 0 on an empty board.</summary>
        public static PlanarCrystal LargestCrystal(int[] cells)
        {
            var best = new PlanarCrystal { Perm = Perms[0], Cells = new List<int>(), Size = 0 };
            foreach (var perm in Perms)
            {
                var seen = new bool[DynamoCore.CellCount];
                for (int s = 0; s < DynamoCore.CellCount; s++)
                {
                    if (seen[s] || cells[s] == 0 || cells[s] != perm[Class[s]]) continue;
                    var q = new List<int> { s };
                    seen[s] = true;
                    for (int h = 0; h < q.Count; h++)
                        foreach (int n in DynamoCore.Neighbours[q[h]])
                            if (!seen[n] && cells[n] != 0 && cells[n] == perm[Class[n]]) { seen[n] = true; q.Add(n); }
                    if (q.Count > best.Size)
                    {
                        var sorted = new List<int>(q);
                        sorted.Sort();
                        best = new PlanarCrystal { Perm = perm, Cells = sorted, Size = q.Count };
                    }
                }
            }
            return best;
        }

        /// <summary>Where a reward prism of colour c lands: an empty cell of c's class under the crystal's perm next to the
        /// crystal, nearest the centre then lowest index; else any empty cell of that class; else any empty cell. Tier 0 = on
        /// the crystal, 1 = its class elsewhere, 2 = anywhere. False on a full board.</summary>
        public static bool CrystalSlot(int[] cells, PlanarCrystal crystal, int c, out int cell, out int tier)
        {
            int want = Array.IndexOf(crystal.Perm, c);
            var inC = new bool[DynamoCore.CellCount];
            foreach (int i in crystal.Cells) inC[i] = true;
            var tiers = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int i = 0; i < DynamoCore.CellCount; i++)
            {
                if (cells[i] != 0) continue;
                if (Class[i] != want) { tiers[2].Add(i); continue; }
                tiers[crystal.Size != 0 && DynamoCore.Neighbours[i].Any(n => inC[n]) ? 0 : 1].Add(i);
            }
            for (int t = 0; t < 3; t++)
                if (tiers[t].Count > 0)
                {
                    tiers[t].Sort((a, b) => DynamoCore.RingOf(a) != DynamoCore.RingOf(b) ? DynamoCore.RingOf(a) - DynamoCore.RingOf(b) : a - b);
                    cell = tiers[t][0];
                    tier = t;
                    return true;
                }
            cell = -1;
            tier = -1;
            return false;
        }

        public struct Landed
        {
            public int Cell, Colour, Tier;
        }

        /// <summary>The rewards go back with no pick and place themselves (Garrett 2026-10-31): <paramref name="perColour"/>
        /// prisms of each rewarded colour, in reward order, onto the largest planar crystal (which grows as they land).
        /// Writes <paramref name="cells"/>; returns the crystal after it. The charges are the caller's (the battery).</summary>
        public static PlanarCrystal PlaceRewards(int[] cells, IReadOnlyList<int> rewards, int perColour, List<Landed> landed)
        {
            var crystal = LargestCrystal(cells);
            landed.Clear();
            foreach (int c in rewards)
                for (int k = 0; k < perColour; k++)
                {
                    if (!CrystalSlot(cells, crystal, c, out int cell, out int tier)) break;
                    cells[cell] = c;
                    landed.Add(new Landed { Cell = cell, Colour = c, Tier = tier });
                    if (tier == 0)
                    {
                        var grown = new List<int>(crystal.Cells) { cell };
                        crystal = new PlanarCrystal { Perm = crystal.Perm, Cells = grown, Size = crystal.Size + 1 };
                    }
                }
            return crystal;
        }

        /// <summary>The empty cells next to a crystal, each with the colour its class wants: where the pattern goes on (the
        /// glow). Pairs of (cell, colour).</summary>
        public static List<int[]> CrystalNext(int[] cells, PlanarCrystal crystal)
        {
            var inC = new bool[DynamoCore.CellCount];
            foreach (int i in crystal.Cells) inC[i] = true;
            var next = new List<int[]>();
            for (int i = 0; i < DynamoCore.CellCount; i++)
                if (cells[i] == 0 && DynamoCore.Neighbours[i].Any(n => inC[n])) next.Add(new[] { i, crystal.Perm[Class[i]] });
            return next;
        }

        // ------------------------------------------------------------------ the hive: snakes, crossings, danger, the solver

        /// <summary>A cell a chain of colour c may run through.</summary>
        public static bool Live(HiveCell x, int c) => x.Dom == c && !x.Danger && x.Hits > 0;

        // One snake of `colour`: through cell `through` (a crossing) or from a random free cell, grown from either end through
        // free cells. It writes nothing unless it reaches SnakeMin.
        static List<int> LaySnake(int[] dom, int[] hits, Func<double> rnd, int colour, int through, HiveSettings p)
        {
            bool Free(int i) => dom[i] < 0;
            List<int> path;
            if (through >= 0) path = new List<int> { through };
            else
            {
                var f = new List<int>();
                for (int i = 0; i < DynamoCore.CellCount; i++) if (Free(i)) f.Add(i);
                if (f.Count == 0) return null;
                path = new List<int> { f[Floor(rnd() * f.Count)] };
            }
            int len = p.SnakeMin + Floor(rnd() * (Math.Max(p.SnakeMin, p.SnakeMax) - p.SnakeMin + 1));
            while (path.Count < len)
            {
                int k = Floor(rnd() * 2);
                bool grown = false;
                for (int e = 0; e < 2 && !grown; e++)
                {
                    bool head = (k + e) % 2 == 0;
                    int end = head ? path[path.Count - 1] : path[0];
                    var next = DynamoCore.Neighbours[end].Where(n => Free(n) && !path.Contains(n)).ToList();
                    if (next.Count > 0)
                    {
                        int n = next[Floor(rnd() * next.Count)];
                        if (head) path.Add(n);
                        else path.Insert(0, n);
                        grown = true;
                    }
                }
                if (!grown) break;
            }
            if (path.Count < p.SnakeMin) return null;
            foreach (int i in path)
            {
                if (dom[i] == colour) hits[i]++;
                else { dom[i] = colour; hits[i] = 1; }
            }
            return path;
        }

        /// <summary>What every hive must hold (Garrett 2026-10-31): prisms of all three domains, a shielded prism in each,
        /// at least HiveDangerMin danger prisms.</summary>
        public static bool HiveHolds(HiveCell[] cells, HiveSettings p, out int domains, out int shielded, out int danger)
        {
            var doms = new HashSet<int>();
            var shields = new HashSet<int>();
            danger = 0;
            foreach (var x in cells)
            {
                if (x.Dom == 0) continue;
                doms.Add(x.Dom);
                if (x.Danger) danger++;
                else if (x.Hits >= 2) shields.Add(x.Dom);
            }
            domains = doms.Count;
            shielded = shields.Count;
            return domains == 3 && shielded == 3 && danger >= p.HiveDangerMin;
        }

        /// <summary>A solvable hive (the solver proves it), or null after 60 attempts. Each regeneration is counted.</summary>
        public static Hive MakeHive(Func<double> rnd, HiveSettings p, int own)
        {
            var others = OthersOf(own);
            int regenerated = 0;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                if (attempt > 0) regenerated++;
                var dom = new int[DynamoCore.CellCount];
                var hits = new int[DynamoCore.CellCount];
                for (int i = 0; i < dom.Length; i++) dom[i] = -1;
                bool ok = true;
                foreach (int c in others)   // each other colour starts with a crossing: two snakes sharing one cell, its shield
                {
                    var a = LaySnake(dom, hits, rnd, c, -1, p);
                    var b = a != null ? LaySnake(dom, hits, rnd, c, a[Floor(rnd() * a.Count)], p) : null;
                    if (b == null) { ok = false; break; }
                }
                if (!ok) continue;
                int target = JsRound(p.HiveOtherShare * DynamoCore.CellCount);
                int laid = dom.Count(d => d > 0), tries = 0, k = 0;
                while (laid < target && tries++ < 200)
                {
                    int c = others[k++ % 2];
                    var plain = new List<int>();
                    for (int i = 0; i < DynamoCore.CellCount; i++) if (dom[i] == c && hits[i] == 1) plain.Add(i);
                    int through = plain.Count > 0 && rnd() < p.HiveCross ? plain[Floor(rnd() * plain.Count)] : -1;
                    if (LaySnake(dom, hits, rnd, c, through, p) != null) laid = dom.Count(d => d > 0);
                }
                var rest = new List<int>();
                for (int i = 0; i < DynamoCore.CellCount; i++) if (dom[i] < 0) rest.Add(i);
                if (rest.Count < p.HiveDangerMin + p.HiveHoles + 1) continue;
                var danger = new bool[DynamoCore.CellCount];
                for (int d = 0; d < p.HiveDangerMin; d++)
                {
                    int at = Floor(rnd() * rest.Count), i = rest[at];
                    rest.RemoveAt(at);
                    danger[i] = true;
                    dom[i] = 1 + Floor(rnd() * 3);
                    hits[i] = 1;
                }
                for (int h = 0; h < p.HiveHoles; h++)
                {
                    int at = Floor(rnd() * rest.Count);
                    dom[rest[at]] = 0;
                    rest.RemoveAt(at);
                }
                foreach (int i in rest) { dom[i] = own; hits[i] = 1; }
                for (int a = Math.Max(1, JsRound(rest.Count * p.HiveArmour)); a > 0; a--) hits[rest[Floor(rnd() * rest.Count)]] = 2;
                var cells = new HiveCell[DynamoCore.CellCount];
                for (int i = 0; i < cells.Length; i++)
                    cells[i] = new HiveCell { Dom = dom[i] > 0 ? dom[i] : 0, Hits = dom[i] > 0 ? hits[i] : 0, Danger = danger[i] };
                if (!HiveHolds(cells, p, out _, out _, out _)) continue;
                var solution = SolveHive(cells, p.MinChain, own);
                if (solution != null) return Create(cells, solution, regenerated, own);
            }
            return null;
        }

        /// <summary>Every simple path of minChain..maxLen cells through <paramref name="set"/> (live cells of one colour, in
        /// their insertion order) that holds cell <paramref name="must"/> (any path when must &lt; 0), kept once per cell set
        /// in the order found.</summary>
        public static List<int[]> PathsThrough(IReadOnlyList<int> set, int must, int minChain, int maxLen)
        {
            var inSet = new HashSet<int>(set);
            var keys = new HashSet<string>();
            var outList = new List<int[]>();
            var path = new List<int>();
            var used = new HashSet<int>();
            void Dfs(int u)
            {
                if (path.Count >= minChain && (must < 0 || used.Contains(must)))
                {
                    var sorted = path.ToArray();
                    Array.Sort(sorted);
                    if (keys.Add(string.Join(",", sorted))) outList.Add(path.ToArray());
                }
                if (path.Count >= maxLen) return;
                foreach (int n in DynamoCore.Neighbours[u])
                    if (inSet.Contains(n) && !used.Contains(n))
                    {
                        used.Add(n); path.Add(n);
                        Dfs(n);
                        path.RemoveAt(path.Count - 1); used.Remove(n);
                    }
            }
            foreach (int s in set)
            {
                used.Add(s); path.Add(s);
                Dfs(s);
                path.RemoveAt(path.Count - 1); used.Remove(s);
            }
            return outList;
        }

        /// <summary>The solver: can every non-danger cell of the two other domains be cleared by chains of at least minChain
        /// through cells of one domain, each chain taking one hit from each of its cells (shielded 2, plain 1)? Per colour:
        /// the lowest cell still standing, every path of 3..8 live cells through it, take the hits, recurse; memo on the
        /// hits. A cell with h hits sits in exactly h chains of any solution, so the order never matters and the search is
        /// complete. Danger cells are never in a chain. The chains in play order, or null.</summary>
        public static List<int[]> SolveHive(HiveCell[] cells, int minChain, int own)
        {
            int budget = 300000;
            var all = new List<int[]>();
            var keyBuf = new System.Text.StringBuilder(64);
            foreach (int c in OthersOf(own))
            {
                var idxs = new List<int>();
                for (int i = 0; i < DynamoCore.CellCount; i++) if (Live(cells[i], c)) idxs.Add(i);
                var hits = new Dictionary<int, int>();
                foreach (int i in idxs) hits[i] = cells[i].Hits;
                var memo = new Dictionary<string, List<int[]>>();
                List<int[]> Solve()
                {
                    var standing = new List<int>(idxs.Count);
                    foreach (int i in idxs) if (hits[i] > 0) standing.Add(i);
                    if (standing.Count == 0) return new List<int[]>();
                    keyBuf.Clear();
                    foreach (int i in idxs) keyBuf.Append(hits[i]);
                    string key = keyBuf.ToString();
                    if (memo.TryGetValue(key, out var known)) return known;
                    if (--budget < 0) return null;
                    var set = new HashSet<int>(standing);
                    int lowest = standing[0];
                    var comp = new List<int> { lowest };
                    var inComp = new HashSet<int> { lowest };
                    for (int h = 0; h < comp.Count; h++)
                        foreach (int n in DynamoCore.Neighbours[comp[h]])
                            if (set.Contains(n) && inComp.Add(n)) comp.Add(n);
                    List<int[]> result = null;
                    if (comp.Count >= minChain)
                        foreach (var p in PathsThrough(comp, lowest, minChain, 8))
                        {
                            foreach (int i in p) hits[i]--;
                            var rest = Solve();
                            foreach (int i in p) hits[i]++;
                            if (rest != null)
                            {
                                result = new List<int[]> { p };
                                result.AddRange(rest);
                                break;
                            }
                        }
                    memo[key] = result;
                    return result;
                }
                var sol = Solve();
                if (sol == null) return null;
                all.AddRange(sol);
            }
            return all;
        }

        /// <summary>Is any chain left? A connected group of live cells of one other colour at least minChain big.</summary>
        public static bool AnyChain(HiveCell[] cells, int minChain, int own)
        {
            var others = OthersOf(own);
            var seen = new bool[DynamoCore.CellCount];
            for (int s = 0; s < DynamoCore.CellCount; s++)
            {
                int c = cells[s].Dom;
                if (seen[s] || Array.IndexOf(others, c) < 0 || !Live(cells[s], c)) continue;
                var q = new List<int> { s };
                seen[s] = true;
                for (int h = 0; h < q.Count; h++)
                    foreach (int n in DynamoCore.Neighbours[q[h]])
                        if (!seen[n] && Live(cells[n], c)) { seen[n] = true; q.Add(n); }
                if (q.Count >= minChain) return true;
            }
            return false;
        }

        /// <summary>Every chain the board allows now (any colour), longest first, then by first cell (a stable order).</summary>
        public static List<int[]> ChainsNow(HiveCell[] cells, int minChain, int maxLen, int own)
        {
            var outList = new List<int[]>();
            foreach (int c in OthersOf(own))
            {
                var set = new List<int>();
                for (int i = 0; i < DynamoCore.CellCount; i++) if (Live(cells[i], c)) set.Add(i);
                var inSet = new HashSet<int>(set);
                var seen = new HashSet<int>();
                foreach (int s in set)
                {
                    if (seen.Contains(s)) continue;
                    var comp = new List<int> { s };
                    var inComp = new HashSet<int> { s };
                    for (int h = 0; h < comp.Count; h++)
                        foreach (int n in DynamoCore.Neighbours[comp[h]])
                            if (inSet.Contains(n) && inComp.Add(n)) comp.Add(n);
                    foreach (int i in comp) seen.Add(i);
                    if (comp.Count < minChain) continue;
                    outList.AddRange(PathsThrough(comp, -1, minChain, maxLen));
                }
            }
            // Stable sort, longest first then by first cell, ties keep found order (exactly OrderByDescending.ThenBy).
            var order = new int[outList.Count];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            Array.Sort(order, (a, b) =>
            {
                int c = outList[b].Length.CompareTo(outList[a].Length);
                if (c == 0) c = outList[a][0].CompareTo(outList[b][0]);
                return c != 0 ? c : a.CompareTo(b);
            });
            var sorted = new List<int[]>(order.Length);
            foreach (int i in order) sorted.Add(outList[i]);
            return sorted;
        }

        // ------------------------------------------------------------------ hive mode's rules on a hive

        public static Hive Create(HiveCell[] cells, List<int[]> solution, int regenerated, int own)
        {
            var hive = new Hive { Cells = cells, Solution = solution, Regenerated = regenerated };
            foreach (int c in OthersOf(own)) hive.Total[c] = Remaining(hive, c);
            return hive;
        }

        /// <summary>A colour's cells still standing: every non-danger cell of it with a hit left (shields included).</summary>
        public static int Remaining(Hive hive, int c)
        {
            int n = 0;
            foreach (var x in hive.Cells) if (Live(x, c)) n++;
            return n;
        }

        public static int StandingAll(Hive hive, int own)
        {
            int n = 0;
            foreach (int c in OthersOf(own)) n += Remaining(hive, c);
            return n;
        }

        /// <summary>The result of a thumb step.</summary>
        public const int Refused = 0, Grew = 1, Stung = 2, Backtracked = 3;

        // A sting: the chain breaks with nothing in it destroyed, and the thumb is stunned for StingSeconds (no AP cost).
        static void Sting(Hive hive, HiveSettings p)
        {
            hive.Chain.Clear();
            hive.Stun = p.StingSeconds;
            hive.Stings++;
        }

        public static int ChainStart(Hive hive, int cell, HiveSettings p, int own)
        {
            if (hive.Stun > 0 || !(cell >= 0 && cell < DynamoCore.CellCount)) return Refused;
            var x = hive.Cells[cell];
            if (x.Danger && x.Dom != 0) { Sting(hive, p); return Stung; }
            if (Array.IndexOf(OthersOf(own), x.Dom) < 0 || !Live(x, x.Dom)) return Refused;   // our own domain is the hive's body
            hive.Chain.Clear();
            hive.Chain.Add(cell);
            return Grew;
        }

        public static int ChainAdd(Hive hive, int cell, HiveSettings p)
        {
            var ch = hive.Chain;
            if (hive.Stun > 0 || ch.Count == 0 || !(cell >= 0 && cell < DynamoCore.CellCount)) return Refused;
            int last = ch[ch.Count - 1];
            if (cell == last) return Refused;
            if (ch.Count >= 2 && cell == ch[ch.Count - 2]) { ch.RemoveAt(ch.Count - 1); return Backtracked; }
            if (!DynamoCore.IsNeighbour(last, cell) || ch.Contains(cell)) return Refused;
            var x = hive.Cells[cell];
            if (x.Danger && x.Dom != 0) { Sting(hive, p); return Stung; }
            if (!Live(x, hive.Cells[ch[0]].Dom)) return Refused;
            ch.Add(cell);
            return Grew;
        }

        /// <summary>Rewards for completing a colour (Garrett 2026-10-31): the colours done, plus our own once both other colours
        /// are done.</summary>
        public static void RewardsFor(List<int> done, int own, List<int> into)
        {
            into.Clear();
            into.AddRange(done);
            if (OthersOf(own).All(done.Contains)) into.Add(own);
        }

        public sealed class ChainResult
        {
            public int Colour, Broken;
            public bool Completed;
            /// <summary>Chain order: (cell, 1 when the prism broke, 0 when only its shield went).</summary>
            public readonly List<int[]> Steps = new();
        }

        /// <summary>Let go: a chain of at least MinChain takes one hit from each of its cells (a shield goes, or the prism
        /// breaks). Null when it was too short (it is dropped).</summary>
        public static ChainResult ChainEnd(Hive hive, HiveSettings p, int own)
        {
            var ch = hive.Chain.ToArray();
            hive.Chain.Clear();
            if (ch.Length < p.MinChain) return null;
            var res = new ChainResult { Colour = hive.Cells[ch[0]].Dom };
            foreach (int i in ch)
            {
                var x = hive.Cells[i];
                x.Hits--;
                if (x.Hits <= 0) { x.Dom = 0; x.Hits = 0; res.Broken++; res.Steps.Add(new[] { i, 1 }); }
                else res.Steps.Add(new[] { i, 0 });
            }
            hive.Broken += res.Broken;
            hive.Chains++;
            if (Remaining(hive, res.Colour) == 0 && !hive.Done.Contains(res.Colour))
            {
                hive.Done.Add(res.Colour);
                RewardsFor(hive.Done, own, hive.Rewards);
                res.Completed = true;
            }
            return res;
        }

        public const string EndClear = "clear", EndTime = "time", EndStuck = "stuck", EndLeave = "leave";

        /// <summary>Why hive mode ends now, or "" to go on: cleared, out of time, or no chain left (with none in hand).</summary>
        public static string EndReason(Hive hive, double t, HiveSettings p, int own)
        {
            if (StandingAll(hive, own) == 0) return EndClear;
            if (t >= p.RaidSeconds) return EndTime;
            if (hive.Chain.Count == 0 && !AnyChain(hive.Cells, p.MinChain, own)) return EndStuck;
            return "";
        }

        public static void Tick(Hive hive, double dt)
        {
            if (hive.Stun > 0) hive.Stun = Math.Max(0, hive.Stun - dt);
        }

        /// <summary>The AI Ratel's plan: the solver's chains still to play (null until the first call).</summary>
        public sealed class AIPlan
        {
            public List<int[]> Plan;
        }

        /// <summary>The AI Ratel's next chain (input-only: the caller drags it cell by cell). The skilled Ratel plays the
        /// solver's solution chain by chain (never touching danger); otherwise a random one of the four longest chains,
        /// sometimes cut short (it can strand cells), and a sloppy thumb sometimes slides on into a danger cell next to its
        /// end. Null when there is no chain.</summary>
        public static int[] AINextChain(Hive hive, AIPlan ai, bool solver, double sloppy, Func<double> rnd, HiveSettings p, int own)
        {
            int[] path = null;
            var cells = hive.Cells;
            bool Standing(int[] q)
            {
                foreach (int i in q) if (!Live(cells[i], cells[q[0]].Dom)) return false;
                return true;
            }
            if (solver)
            {
                ai.Plan ??= hive.Solution != null ? new List<int[]>(hive.Solution) : new List<int[]>();
                while (ai.Plan.Count > 0 && !Standing(ai.Plan[0])) ai.Plan.RemoveAt(0);
                if (ai.Plan.Count > 0) { path = ai.Plan[0]; ai.Plan.RemoveAt(0); }
            }
            if (path == null)
            {
                var all = ChainsNow(cells, p.MinChain, 6, own);
                if (all.Count == 0) return null;
                path = all[Floor(rnd() * Math.Min(4, all.Count))];
                if (!solver && path.Length > p.MinChain && rnd() < 0.5)
                {
                    var cut = new int[p.MinChain + Floor(rnd() * (path.Length - p.MinChain))];
                    Array.Copy(path, cut, cut.Length);
                    path = cut;
                }
                if (sloppy > 0 && rnd() < sloppy)
                {
                    int d = -1;
                    foreach (int n in DynamoCore.Neighbours[path[path.Length - 1]]) if (cells[n].Danger && cells[n].Dom != 0) { d = n; break; }
                    if (d >= 0) { var slid = new int[path.Length + 1]; Array.Copy(path, slid, path.Length); slid[path.Length] = d; path = slid; }
                }
            }
            return path;
        }

        /// <summary>The same digest the lab's <c>HiveCore.digest</c> prints; the parity gate compares the two.</summary>
        public static string Digest(Hive hive)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < hive.Cells.Length; i++)
            {
                if (i > 0) sb.Append('.');
                var x = hive.Cells[i];
                sb.Append(x.Dom).Append(x.Hits);
                if (x.Danger) sb.Append('d');
            }
            sb.Append('|').Append(string.Join(",", hive.Chain)).Append('|').Append((long)Math.Floor(hive.Stun * 1e6 + 0.5)).Append('|');
            foreach (int c in hive.Done) sb.Append(c);
            sb.Append('|');
            foreach (int c in hive.Rewards) sb.Append(c);
            sb.Append('|').Append(hive.Broken).Append('|').Append(hive.Chains).Append('|').Append(hive.Stings);
            return sb.ToString();
        }

        // ------------------------------------------------------------------ state copy (a Ratel on another machine)

        /// <summary>A hive's state as ints, for a Ratel played on another machine (the solution is not sent: the rider's AI
        /// never plays). <see cref="ReadState"/> reads it back.</summary>
        public static void WriteState(Hive hive, List<int> into)
        {
            into.Add(hive.Cells.Length);
            foreach (var x in hive.Cells) { into.Add(x.Dom); into.Add(x.Hits); into.Add(x.Danger ? 1 : 0); }
            into.Add(hive.Chain.Count); into.AddRange(hive.Chain);
            into.Add((int)Math.Round(hive.Stun * 1000));
            into.Add(hive.Done.Count); into.AddRange(hive.Done);
            into.Add(hive.Rewards.Count); into.AddRange(hive.Rewards);
            into.Add(hive.Broken); into.Add(hive.Chains); into.Add(hive.Stings);
            for (int c = 0; c < 4; c++) into.Add(hive.Total[c]);
        }

        /// <summary>A hive from <see cref="WriteState"/>'s ints starting at <paramref name="i"/>; null when they are not one.</summary>
        public static Hive ReadState(int[] d, ref int i)
        {
            bool Has(int n, int at) => d != null && at + n <= d.Length;
            if (!Has(1, i) || d[i] != DynamoCore.CellCount || !Has(1 + 3 * DynamoCore.CellCount, i)) return null;
            int at = i + 1;
            var cells = new HiveCell[DynamoCore.CellCount];
            for (int k = 0; k < cells.Length; k++) { cells[k] = new HiveCell { Dom = d[at], Hits = d[at + 1], Danger = d[at + 2] != 0 }; at += 3; }
            var hive = new Hive { Cells = cells };
            foreach (var list in new[] { hive.Chain })
            {
                if (!Has(1, at) || d[at] < 0 || !Has(1 + d[at], at)) return null;
                for (int k = 0; k < d[at]; k++) list.Add(d[at + 1 + k]);
                at += 1 + d[at];
            }
            if (!Has(1, at)) return null;
            hive.Stun = d[at++] / 1000.0;
            foreach (var list in new[] { hive.Done, hive.Rewards })
            {
                if (!Has(1, at) || d[at] < 0 || !Has(1 + d[at], at)) return null;
                for (int k = 0; k < d[at]; k++) list.Add(d[at + 1 + k]);
                at += 1 + d[at];
            }
            if (!Has(7, at)) return null;
            hive.Broken = d[at++]; hive.Chains = d[at++]; hive.Stings = d[at++];
            for (int c = 0; c < 4; c++) hive.Total[c] = d[at++];
            i = at;
            return hive;
        }
    }
}
