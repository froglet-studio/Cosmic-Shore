// Runs every [Test] of the compiled SHIPPED Slip Line tests (SlipLineCourseTests) by reflection and prints a pass/fail line
// per test. Exit 0 = all passed. Two more modes read the shipped C#, never a copy:
//   --sweep        the ladder per intensity over 200 seeds, and the race clock of three Honeyguides whose battery is fed by
//                  the SHIPPED Ratel AI (DynamoCore.AIPlan on HoneyguideConfig.asset's patterns, engines and skills, paced
//                  as HoneyguideCrew paces it): the FLIER (never slips), the GREEDY rider (rides the best rail of a colour
//                  it holds on every leg) and the READER (SlipLineCourse.ChooseRail: holds a charge for a better ride ahead
//                  unless the battery is full). Airframe 60 u/s, turning 90 deg/s.
//   --trace I seed one course's ride log for the greedy rider and the reader.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CosmicShore.Gameplay;
using UnityEngine;

public static class Runner
{
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        if (args.Length > 0 && args[0] == "--sweep") { Sweep(); return 0; }
        if (args.Length > 1 && args[0] == "--why") { Why(int.Parse(args[1])); return 0; }
        if (args.Length > 2 && args[0] == "--trace") { Trace(int.Parse(args[1]), int.Parse(args[2])); return 0; }

        int pass = 0, fail = 0;
        var asm = Assembly.GetExecutingAssembly();
        foreach (var type in asm.GetTypes().Where(t => t.Namespace == "CosmicShore.Tests").OrderBy(t => t.Name))
            foreach (var m in type.GetMethods().Where(m => m.GetCustomAttribute<NUnit.Framework.TestAttribute>() != null))
            {
                try { m.Invoke(Activator.CreateInstance(type), null); pass++; Console.WriteLine($"PASS {type.Name}.{m.Name}"); }
                catch (TargetInvocationException e) { fail++; Console.WriteLine($"FAIL {type.Name}.{m.Name}: {e.InnerException?.Message}"); }
            }
        Console.WriteLine($"{pass} passed, {fail} failed");
        return fail == 0 && pass > 0 ? 0 : 1;
    }

    // ── The Ratel: the shipped Dynamo and its AI, on the shipped asset ──

    static DynamoSettings _dyn;
    static (double think, int top, double blunder)[] _skills;

    static DynamoSettings Dynamo()
    {
        if (_dyn != null) return _dyn;
        string t = File.ReadAllText(Path.Combine("Assets", "_SO_Assets", "VesselActions", "Honeyguide", "HoneyguideConfig.asset"));
        double Num(string k) => double.Parse(Regex.Match(t, $@"^  {k}: ([0-9.]+)", RegexOptions.Multiline).Groups[1].Value, CultureInfo.InvariantCulture);
        var s = new DynamoSettings
        {
            TickSeconds = Num("tickSeconds"), ApPerTick = (int)Num("apPerTick"), ApBank = (int)Num("apBank"), BonusApCap = (int)Num("bonusApCap"),
            BatteryCap = (int)Num("batteryCap"), RevealAbility = Num("revealAbility"), RevealPorts = Num("revealPorts"),
            AllowRotatedMatches = Num("rotatable") != 0, PortsCount = Num("portsCount") != 0, AbilityChoices = (int)Num("abilityChoices"),
            PickSeconds = Num("pickSeconds"), SlotCount = 3, StartFilled = true, UsesPerPattern = (int)Num("usesPerPattern"),
            PatternsPerShuffle = (int)Num("patternsPerShuffle"),
        };
        var retired = Regex.Match(t, @"^  retiredAbilities:\n((?:  - \d+\n)*)", RegexOptions.Multiline).Groups[1].Value;
        var gone = Regex.Matches(retired, @"- (\d+)").Select(m => int.Parse(m.Groups[1].Value)).ToHashSet();
        if (gone.Count > 0) s.Abilities = Enumerable.Range(0, DynamoCore.AbilityCount).Where(a => !gone.Contains(a)).ToArray();
        foreach (Match m in Regex.Matches(t, @"^    shape: ([^\n]*)\n(?:.*\n)*?    parked: (\d)", RegexOptions.Multiline))
            s.Patterns.Add(new DynamoPattern { Cells = Shape(m.Groups[1].Value), Parked = m.Groups[2].Value == "1" });
        int e = t.IndexOf("  engines:", StringComparison.Ordinal);
        var engines = new List<int[][]>();
        foreach (Match m in Regex.Matches(t.Substring(e), @"    rows:\n((?:    - '[^']*'\n)+)"))
            engines.Add(DynamoCore.ParseEngine(Regex.Matches(m.Groups[1].Value, @"- '([^']*)'").Select(x => x.Groups[1].Value).ToList()));
        s.PortSets = DynamoCore.BuildPortSets(engines);
        var skills = new List<(double, int, double)>();
        int k = t.IndexOf("  engineerSkills:", StringComparison.Ordinal);
        foreach (Match m in Regex.Matches(t.Substring(k, 400), @"- think: ([0-9.]+)\n    top: (\d+)\n    blunder: ([0-9.]+)"))
            skills.Add((double.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), double.Parse(m.Groups[3].Value)));
        _skills = skills.ToArray();
        return _dyn = s;
    }

    static int[][] Shape(string shape)
    {
        var cells = new List<int[]>();
        foreach (var tok in shape.Split((char[])null, StringSplitOptions.RemoveEmptyEntries))
        {
            int col = tok[0] == 'M' ? 1 : tok[0] == 'C' ? 2 : tok[0] == 'S' ? 3 : 0;
            var qr = tok.Substring(1).Split(',');
            if (col != 0 && qr.Length == 2) cells.Add(new[] { int.Parse(qr[0]), int.Parse(qr[1]), col });
        }
        return cells.ToArray();
    }

    /// <summary>The Ratel's side of the clock: its Dynamo, its AI, advanced in the AI's own steps.</summary>
    sealed class Ratel
    {
        readonly DynamoSettings _s;
        readonly DynamoCore _core;
        readonly System.Random _dice;
        readonly DynamoCore.AIMemo _memo = new();
        readonly (double think, int top, double blunder) _skill;
        double _think;
        public int Charges;

        public Ratel(int seed, int level)
        {
            _s = Dynamo();
            _core = new DynamoCore(_s, seed);
            _dice = new System.Random(seed * 31 + 7);
            _skill = _skills[Math.Clamp(level, 0, _skills.Length - 1)];
        }

        public List<int> Battery => _core.Battery;
        public int Cap => _s.BatteryCap;
        public bool Spend(int colour) => _core.Spend(colour);

        public void Advance(double seconds)
        {
            const double dt = 1.0 / 30.0;
            for (double t = 0; t < seconds; t += dt)
            {
                _think -= dt;
                if (_think <= 0)
                {
                    _think = _skill.think;
                    var inp = _core.AIPlan(_s, new DynamoAISkill { Top = _skill.top, Blunder = _skill.blunder }, () => _dice.NextDouble(), _memo);
                    if (inp != null && inp.Value.Kind == DynamoInputKind.Activate) _memo.Key = "";
                    if (inp != null) _core.Apply(_s, inp.Value);
                }
                _core.Advance(_s, dt);
                foreach (var e in _core.Events) if (e.Kind == DynamoEventKind.Charge) Charges++;
                _core.Events.Clear();
            }
        }
    }

    // ── The race clock ───────────────────────────────────────────────

    enum Pilot { Flier, Greedy, Reader }
    const float TurnRate = 90f;
    static bool _trace;

    static float Clock(List<RaceGate> rings, List<Rail> rails, Pilot pilot, int seed, int ratelLevel, out int rides)
    {
        var ratel = new Ratel(seed, ratelLevel);
        Vector3 pos = SlipLineCourse.StartPoses(1)[0].position, fwd = SlipLineCourse.StartAxis;
        float t = 0f;
        int count = 0;
        void Fly(Vector3 to)
        {
            Vector3 d = to - pos;
            if (d.sqrMagnitude < 1e-4f) return;
            float s = Vector3.Angle(fwd, d) / TurnRate + d.magnitude / SlipLineCourse.CruiseSpeed;
            ratel.Advance(s);
            t += s;
            fwd = d.normalized;
            pos = to;
        }
        void Ride(in Rail r)
        {
            ratel.Spend(r.Colour);
            float s = SlipLineCourse.RideSeconds(r.Length);
            ratel.Advance(s);
            t += s;
            pos = r.End + r.Direction * SlipLineCourse.ExitOffset;
            fwd = r.Direction;
            count++;
        }
        bool Holds(int colour) => ratel.Battery.Contains(colour);

        for (int k = 0; k < rings.Count; k++)
        {
            int pick = -1;
            if (pilot == Pilot.Reader) pick = SlipLineCourse.ChooseRail(pos, rings, rails, k, ratel.Battery, ratel.Cap);
            else if (pilot == Pilot.Greedy)
            {
                float best = 0f;
                Vector3 after = k + 1 < rings.Count ? rings[k + 1].Position : rings[k].Position;
                for (int i = 0; i < rails.Count; i++)
                {
                    if (rails[i].Ring != k || rails[i].Chained || !Holds(rails[i].Colour)) continue;
                    float save = SlipLineCourse.Saving(pos, rails[i], rings[k], after);
                    if (save > best) { best = save; pick = i; }
                }
            }
            if (pick < 0)
            {
                Fly(rings[k].Position);
                if (_trace) Console.WriteLine($"  t {t,6:F1}  fly    ring {k,2}   battery [{string.Join(",", ratel.Battery)}]");
                continue;
            }
            var r = rails[pick];
            Fly(r.Start);
            Ride(r);
            if (_trace) Console.WriteLine($"  t {t,6:F1}  RIDE   ring {k,2}   colour {r.Colour} {r.Length:F0} u   battery [{string.Join(",", ratel.Battery)}]");
            if (pick + 1 < rails.Count && rails[pick + 1].Chained && Holds(rails[pick + 1].Colour))
            {
                Ride(rails[pick + 1]);
                k++;
                if (_trace) Console.WriteLine($"  t {t,6:F1}  CHAIN  ring {k,2}   colour {rails[pick].Colour}");
            }
        }
        rides = count;
        return t;
    }

    static void Trace(int intensity, int seed)
    {
        var s = SlipLineCourse.ForIntensity(intensity);
        var rings = SlipLineCourse.Generate(seed, s);
        var rails = new List<Rail>();
        SlipLineCourse.RailsFor(rings, s, rails);
        _trace = true;
        foreach (var p in new[] { Pilot.Greedy, Pilot.Reader })
        {
            Console.WriteLine($"{p}:");
            Console.WriteLine($"  = {Clock(rings, rails, p, seed, 2, out _):F1} s");
        }
    }

    /// <summary>Why the courses of intensity I fail, tallied over the walks of 50 seeds (a tuning aid).</summary>
    static void Why(int intensity)
    {
        var s = SlipLineCourse.ForIntensity(intensity);
        var tally = new Dictionary<string, int>();
        var walk = typeof(SlipLineCourse).GetMethod("TryWalk", BindingFlags.NonPublic | BindingFlags.Static);
        for (int seed = 1; seed <= 50; seed++)
            for (int attempt = 0; attempt < 16; attempt++)
            {
                object rng = new RaceCourseGeometry.Rng(unchecked(seed * 7919 + attempt * 104729 + 53));
                var p = new List<Vector3>(); var a = new List<Vector3>();
                var argv = new object[] { rng, s, p, a };
                if (!(bool)walk.Invoke(null, argv)) { tally["walk"] = tally.GetValueOrDefault("walk") + 1; continue; }
                var rings = new List<RaceGate>();
                for (int i = 0; i < p.Count; i++) rings.Add(new RaceGate(p[i], a[i], s.RingRadius));
                SlipLineCourse.Valid(s, rings, out string why);
                string key = why == null ? "OK" : Regex.Replace(why, @"\d+", "#");
                tally[key] = tally.GetValueOrDefault(key) + 1;
            }
        foreach (var kv in tally.OrderByDescending(x => x.Value)) Console.WriteLine($"{kv.Value,5}  {kv.Key}");
    }

    static void Sweep()
    {
        Dynamo();
        const int seeds = 200;
        for (int i = 1; i <= 4; i++)
        {
            var s = SlipLineCourse.ForIntensity(i);
            int fails = 0;
            var legs = new List<float>();
            var fly = new List<float>(); var greedy = new List<float>(); var reader = new List<float>(); var save = new List<float>();
            var easy = new List<float>();
            int ridesG = 0, ridesR = 0, n = 0;
            for (int seed = 1; seed <= seeds; seed++)
            {
                var rings = SlipLineCourse.Generate(seed, s);
                if (rings == null) { fails++; continue; }
                var rails = new List<Rail>();
                SlipLineCourse.RailsFor(rings, s, rails);
                for (int k = 1; k < rings.Count; k++) legs.Add((rings[k].Position - rings[k - 1].Position).magnitude);
                float f = Clock(rings, rails, Pilot.Flier, seed, 2, out _);
                float g = Clock(rings, rails, Pilot.Greedy, seed, 2, out int rg);
                float r = Clock(rings, rails, Pilot.Reader, seed, 2, out int rr);
                float re = Clock(rings, rails, Pilot.Reader, seed, 0, out _);
                fly.Add(f); greedy.Add(g); reader.Add(r); easy.Add(re); save.Add(1f - r / g);
                ridesG += rg; ridesR += rr; n++;
            }
            float Med(List<float> xs) { var o = xs.OrderBy(x => x).ToList(); return o.Count == 0 ? 0f : o[o.Count / 2]; }
            float Mean(List<float> xs) => xs.Count == 0 ? 0f : xs.Average();
            Console.WriteLine($"I{i}: fails {fails}/{seeds}  rings {s.RingCount}  radius {s.RingRadius}  legs {legs.Min():F0}-{legs.Max():F0}  " +
                              $"lanes {s.Lanes}  short {s.ShortShare:F1}  lean {s.DetourMin:F0}-{s.DetourMax:F0}  chain every {s.ChainEvery}");
            Console.WriteLine($"     clock (Hard Ratel AI): flier {Med(fly):F1} s, greedy {Med(greedy):F1} s ({(float)ridesG / n:F1} rides), " +
                              $"reader {Med(reader):F1} s ({(float)ridesR / n:F1} rides); the reader saves {100f * Mean(save):F1}% on the greedy rider " +
                              $"(wins {save.Count(x => x > 0.001f)}, loses {save.Count(x => x < -0.001f)} of {n}); with the Easy Ratel AI the reader takes {Med(easy):F1} s");
        }
    }
}
