// Runs every [Test] of the compiled SHIPPED Spearline tests (SpearlineSealsTests) by reflection and prints a pass/fail
// line per test. Exit 0 = all passed. One more mode reads the shipped C#, never a copy:
//   --export <intensity> <seed>...   one JSON line per course: its lap (gates, turns, which are sealed and armoured) and,
//                                    for every sealed ring, how many passes each pilot policy takes to get through it
//                                    under the worn seal, with the line it crossed on - what
//                                    Tools/Build/spearline_race_model.py times.
//
// The policies (each pass is one crossing; a closed pass leaves its cut, cracks the shields it hit and wears the need):
//   hull                     the centre, no stroke
//   centre_{left,right,up}   the centre, one fixed stroke (relative to a pilot banked into the corner)
//   line_{left,right,up}     the corner's inside line (half the seal's radius toward the turn), one fixed stroke
//   reader                   SpearlineSeals.Read from the line, every direction a pilot can roll to (8)
//   ai_{easy,medium,hard,legendary}  the AI coach: Read from the centre with its three strokes (left, right, up of its
//                            banked roll), the controller's miss-read chance and lane error by difficulty
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Runner
{
    const float Inner = 480f, Outer = 1080f;

    // SpearlineController's serialized defaults (aiMissReadByDifficulty, aiLaneErrorByDifficulty); the race model's
    // --self-test holds these against the controller source.
    static readonly float[] MissRead = { 0.3f, 0.15f, 0.05f, 0f };
    static readonly float[] LaneError = { 10f, 7f, 4f, 2f };
    static readonly string[] Difficulty = { "easy", "medium", "hard", "legendary" };

    static List<RaceGate> Lap(int intensity, int seed)
    {
        var s = HeadlongCircuitSettings.ForIntensity(intensity);
        s.RingRadius = SpearlineSeals.ForIntensity(intensity).MouthRadius;
        s.InnerRadius = Inner;
        s.OuterRadius = Outer;
        s.FirstGateDirection = Vector3.up;
        return HeadlongCircuit.Generate(seed, s);
    }

    static string F(float v) => v.ToString("0.###", CultureInfo.InvariantCulture);
    static string V(Vector3 v) => $"[{F(v.x)},{F(v.y)},{F(v.z)}]";

    sealed class Pane
    {
        public RaceGate Gate;
        public List<Vector3> At = new();
        public int[][] Adj;
        public bool[] Armour;
        public Vector3 U, V, Axis, Inside, Right;
        public float SealR;
    }

    static Pane Build(List<RaceGate> lap, int ring, SpearlineSealSettings s, bool armoured)
    {
        var g = lap[ring];
        var p = new Pane { Gate = g, Axis = g.Axis.normalized };
        SpearlineSeals.Lay(g, g.Radius, p.At);
        p.Adj = SpearlineSeals.Adjacency(p.At, 0, p.At.Count);
        SpearlineSeals.Basis(g.Axis, out p.U, out p.V);
        p.SealR = g.Radius - SpearlineSeals.RimInset - 0.5f * SpearlineSeals.PrismSize;
        float ang = SpearlineSeals.ArmourAngle(ring, g.Position);
        p.Armour = new bool[p.At.Count];
        for (int i = 0; i < p.At.Count; i++)
        {
            Vector3 d = p.At[i] - g.Position;
            p.Armour[i] = armoured && SpearlineSeals.InArmour(Vector3.Dot(d, p.U), Vector3.Dot(d, p.V), ang, s.ArmourDegrees);
        }
        int n = lap.Count;
        Vector3 inside = (lap[(ring - 1 + n) % n].Position - g.Position).normalized + (lap[(ring + 1) % n].Position - g.Position).normalized;
        inside -= p.Axis * Vector3.Dot(inside, p.Axis);
        p.Inside = inside.sqrMagnitude > 1e-4f ? inside.normalized : p.U;   // a straight ring: any way is "in"
        p.Right = Vector3.Cross(p.Inside, p.Axis).normalized;
        return p;
    }

    /// <summary>The pilot's three strokes banked into the corner: left, right, up (= toward the inside).</summary>
    static List<Vector3> Banked(Pane p) => new() { -p.Right, p.Right, p.Inside };

    static List<Vector3> AllRolls(Pane p)
    {
        var l = new List<Vector3>();
        for (int k = 0; k < 8; k++)
        {
            float a = 2f * Mathf.PI * k / 8f;
            l.Add(p.Inside * Mathf.Cos(a) + p.Right * Mathf.Sin(a));
        }
        return l;
    }

    /// <summary>One crossing: what it cuts and cracks, and whether it opens under <paramref name="need"/>.</summary>
    static bool Pass(Pane p, Vector3 c, Vector3 band, bool[] gone, bool[] cracked, int need)
    {
        int count = p.At.Count;
        var breaks = new bool[count];
        var after = new bool[count];
        for (int i = 0; i < count; i++)
        {
            bool cuttable = !p.Armour[i] || cracked[i];
            breaks[i] = gone[i] || cuttable;
            after[i] = gone[i] || (cuttable && SpearlineSeals.StrokeCuts(p.At[i], c, p.Axis, band,
                                    SpearlineSeals.ModelBladeLength, SpearlineSeals.ModelStrokeReach));
        }
        int hole = SpearlineSeals.HoleSize(c, p.Axis, p.At, 0, p.Adj, after, breaks, new bool[count], new List<int>());
        // What the pass leaves: the stroke's cut and the hull's disc gone where breakable, cracked where armour.
        for (int i = 0; i < count; i++)
        {
            bool inHull = SpearlineSeals.PlaneDistanceSqr(p.At[i], c, p.Axis) <= SpearlineSeals.HullReach * SpearlineSeals.HullReach;
            bool inStroke = SpearlineSeals.StrokeCuts(p.At[i], c, p.Axis, band, SpearlineSeals.ModelBladeLength, SpearlineSeals.ModelStrokeReach);
            if (!(inHull || inStroke)) continue;
            if (!p.Armour[i] || cracked[i]) gone[i] = true;
            else cracked[i] = true;
        }
        return hole >= need;
    }

    /// <summary>Passes until through, for a policy that picks (crossing, band) from the seal as it stands.</summary>
    static int Passes(Pane p, SpearlineSealSettings s, Func<bool[], bool[], int, (Vector3 c, Vector3 band)> pick, out float dev, Vector3 line)
    {
        var gone = new bool[p.At.Count];
        var cracked = new bool[p.At.Count];
        dev = 0f;
        for (int pass = 0; pass < 20; pass++)
        {
            int need = SpearlineSeals.NeedAfter(s, pass);
            var (c, band) = pick(gone, cracked, pass);
            if (pass == 0) dev = Mathf.Sqrt(SpearlineSeals.PlaneDistanceSqr(c, line, p.Axis));
            if (Pass(p, c, band, gone, cracked, need)) return pass + 1;
        }
        return 99;
    }

    static (Vector3, Vector3) ReadPick(Pane p, SpearlineSealSettings s, List<Vector3> strokes, Vector3 preferred,
                                       bool[] gone, bool[] cracked, int pass)
    {
        int count = p.At.Count;
        var breaks = new bool[count];
        var cut = new bool[count];
        for (int i = 0; i < count; i++)
        {
            bool cuttable = !p.Armour[i] || cracked[i];
            breaks[i] = gone[i] || cuttable;
            cut[i] = !gone[i] && cuttable;
        }
        var plan = SpearlineSeals.Read(p.Gate, p.Gate.Radius, preferred, p.At, 0, p.Adj, gone, breaks, cut, strokes,
                                       SpearlineSeals.NeedAfter(s, pass), 1, SpearlineSeals.ModelBladeLength,
                                       SpearlineSeals.ModelStrokeReach, new bool[count], new bool[count], new List<int>());
        return (plan.Crossing, plan.Stroke >= 0 ? strokes[plan.Stroke] : Vector3.zero);
    }

    static string Export(int intensity, int seed)
    {
        var s = SpearlineSeals.ForIntensity(intensity);
        var lap = Lap(intensity, seed);
        var sealedRings = SpearlineSeals.SealedRings(lap, s);
        var sb = new StringBuilder();
        sb.Append($"{{\"intensity\":{intensity},\"seed\":{seed},\"gates\":[");
        for (int i = 0; i < lap.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"{{\"p\":{V(lap[i].Position)},\"r\":{F(lap[i].Radius)},\"turn\":{F(SpearlineSeals.TurnDegrees(lap, i))},\"sealed\":{(sealedRings[i] ? 1 : 0)}}}");
        }
        sb.Append("],\"seals\":[");
        int ordinal = 0;
        bool first = true;
        for (int ring = 0; ring < lap.Count; ring++)
        {
            if (!sealedRings[ring]) continue;
            bool armoured = SpearlineSeals.IsArmoured(ordinal++, s);
            var p = Build(lap, ring, s, armoured);
            Vector3 centre = p.Gate.Position;
            Vector3 line = centre + p.Inside * (0.5f * p.SealR);
            var banked = Banked(p);
            var rolls = AllRolls(p);
            var res = new List<string>();
            void Add(string name, int passes, float dev) => res.Add($"\"{name}\":[{passes},{F(dev)}]");
            float dv;
            Add("hull", Passes(p, s, (g, c, k) => (centre, Vector3.zero), out dv, line), dv);
            string[] side = { "left", "right", "up" };
            for (int b = 0; b < 3; b++)
            {
                var band = banked[b];
                Add($"centre_{side[b]}", Passes(p, s, (g, c, k) => (centre, band), out dv, line), dv);
                Add($"line_{side[b]}", Passes(p, s, (g, c, k) => (line, band), out dv, line), dv);
            }
            Add("reader", Passes(p, s, (g, c, k) => ReadPick(p, s, rolls, line, g, c, k), out dv, line), dv);
            for (int d = 0; d < 4; d++)
            {
                int dd = d;
                int seat = 1 + d;
                int raceIndex = ring;   // one lap's worth: the draw per seal (the controller's is per threading)
                Add($"ai_{Difficulty[d]}", Passes(p, s, (g, c, k) =>
                {
                    bool miss = SpearlineSeals.Hash01(seat, raceIndex + 8 * k, 37) < MissRead[dd];
                    var pick = miss ? (centre, banked[1]) : ReadPick(p, s, banked, centre, g, c, k);
                    float ang = 2f * Mathf.PI * SpearlineSeals.Hash01(seat, raceIndex + 8 * k, 11);
                    float r = LaneError[dd] * SpearlineSeals.Hash01(seat, raceIndex + 8 * k, 23);
                    return (pick.Item1 + (p.U * Mathf.Cos(ang) + p.V * Mathf.Sin(ang)) * r, pick.Item2);
                }, out dv, centre), dv);
            }
            if (!first) sb.Append(',');
            first = false;
            sb.Append($"{{\"ring\":{ring},\"armoured\":{(armoured ? 1 : 0)},\"prisms\":{p.At.Count},\"policies\":{{{string.Join(",", res)}}}}}");
        }
        sb.Append("]}");
        return sb.ToString();
    }

    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--export")
        {
            int intensity = int.Parse(args[1]);
            for (int k = 2; k < args.Length; k++) Console.WriteLine(Export(intensity, int.Parse(args[k])));
            return 0;
        }
        int pass = 0, fail = 0;
        var asm = Assembly.GetExecutingAssembly();
        foreach (var type in asm.GetTypes().Where(t => t.Namespace == "CosmicShore.Tests").OrderBy(t => t.Name))
            foreach (var m in type.GetMethods().Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null))
            {
                try { m.Invoke(Activator.CreateInstance(type), null); pass++; Console.WriteLine($"PASS {type.Name}.{m.Name}"); }
                catch (TargetInvocationException e) { fail++; Console.WriteLine($"FAIL {type.Name}.{m.Name}: {e.InnerException?.Message}"); }
            }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 ? 0 : 1;
    }
}
