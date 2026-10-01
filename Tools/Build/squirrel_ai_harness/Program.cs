// Races the SHIPPED Squirrel skim-racing brain and prints what it did.
//
//   run.sh                                  the standard report (every intensity, solo + a 3-AI field)
//   run.sh --intensity 2 --racers 3 --seeds 20 [--profile expert|tier] [--fps 30] [--trace]
//
// Exit code is non-zero when the standard report's acceptance gates fail (see Gates()).
using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using UnityEngine;

namespace SquirrelAiHarness
{
    static class Program
    {
        static int Main(string[] args)
        {
            string cfgPath = Arg(args, "--config", null) ?? throw new ArgumentException("--config <path> is required");
            var cfg = Config.Load(cfgPath);
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "--set")
                {
                    var kv = args[i + 1].Split('=');
                    _tweaks.Add((kv[0], float.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture)));
                }

            SkimRacingLine.ExperimentLegacySmoothing = args.Contains("--legacy-lane");
            if (args.Contains("--dump-track"))
            {
                // Tooling: print the ribbon's prisms around an index range, with the roll of each
                // plate relative to its predecessor and the tangent's vertical component.
                int ti = int.Parse(Arg(args, "--dump-track", "2"));
                int from = int.Parse(Arg(args, "--from", "0"));
                int to = int.Parse(Arg(args, "--to", "20"));
                var prisms = TrackBuilder.Build(cfg, cfg.Tracks[ti - 1]);
                Console.WriteLine($"I{ti}: {prisms.Count} prisms");
                for (int i = Math.Max(0, from); i <= Math.Min(prisms.Count - 1, to); i++)
                {
                    var tp = prisms[i];
                    var prev = prisms[(i - 1 + prisms.Count) % prisms.Count];
                    Vector3 f = tp.Rotation * Vector3.forward, u = tp.Rotation * Vector3.up;
                    Vector3 pf = prev.Rotation * Vector3.forward, pu = prev.Rotation * Vector3.up;
                    float roll = Vector3.SignedAngle(pu - f * Vector3.Dot(pu, f), u, f);
                    Console.WriteLine($"  {i,4}{(tp.Marker ? "*" : " ")} pos=({tp.Position.x,8:F1},{tp.Position.y,8:F1},{tp.Position.z,8:F1}) " +
                                      $"fwd.y={f.y,6:F3} turn={Vector3.Angle(pf, f),5:F1} roll={roll,6:F1} step={Vector3.Distance(prev.Position, tp.Position),5:F1} semi=({tp.ShellSemi.x:F1},{tp.ShellSemi.y:F2},{tp.ShellSemi.z:F1})");
                }
                return 0;
            }

            if (args.Contains("--crystal-stats"))
            {
                // Tooling: where the crystals land in the ribbon's cross-section. For every anchor,
                // many jitter draws (anchor + Jitter x a random unit vector, as the CrystalManager
                // places them): the crystal's offset (a across the plates, b along their up) and how
                // far the ship must leave the skim band to pass within PassRadius of it.
                int ti = int.Parse(Arg(args, "--crystal-stats", "2"));
                float pass = float.Parse(Arg(args, "--pass", "18"));
                var def = cfg.Tracks[ti - 1];
                var prisms = TrackBuilder.Build(cfg, def);
                var list = new List<SkimRoutePrism>();
                foreach (var tp in prisms) list.Add(new SkimRoutePrism(tp.Position, tp.Rotation, tp.ShellSemi, tp.Marker));
                var route = new SkimRoute(list, closed: true);
                var rng = new System.Random(7);
                Console.WriteLine($"I{ti}: jitter {cfg.Jitter}, pass radius {pass}");
                var all = new List<float>();
                for (int k = 0; k < def.Anchors.Count; k++)
                {
                    Vector3 anchor = def.Anchors[k];
                    float sa = route.ProjectGlobal(anchor);
                    var need = new List<float>();
                    var rs = new List<float>();
                    for (int n = 0; n < 4000; n++)
                    {
                        double z = rng.NextDouble() * 2 - 1, ang = rng.NextDouble() * Math.PI * 2;
                        double rr = Math.Sqrt(Math.Max(0, 1 - z * z));
                        Vector3 c = anchor + new Vector3((float)(rr * Math.Cos(ang)), (float)(rr * Math.Sin(ang)), (float)z) * cfg.Jitter;
                        float sc = route.Project(c, sa, 200f, 200f);
                        route.Frame(sc, out Vector3 cc, out _, out Vector3 rr3, out Vector3 uu);
                        Vector3 d = c - cc;
                        float a = Vector3.Dot(d, rr3), b = Vector3.Dot(d, uu);
                        route.Envelope(sc, 0f, out float hw, out float hh);
                        // Distance from the crystal to the skim band: the region between the hull's
                        // clearance and the skimmer's reach around the plates' box.
                        float reachSkim = cfg.SkimRadius - 0.5f;
                        float hullReach = Mathf.Sqrt(cfg.HullHalf.x * cfg.HullHalf.x + cfg.HullHalf.y * cfg.HullHalf.y) + 1f;
                        float best = float.MaxValue;
                        for (int q = 0; q < 720; q++)
                        {
                            float th = q * Mathf.PI / 360f;
                            float sn = Mathf.Sin(th), cs = Mathf.Cos(th);
                            float byX = Mathf.Abs(sn) > 1e-4f ? (hw + hullReach) / Mathf.Abs(sn) : float.MaxValue;
                            float byU = Mathf.Abs(cs) > 1e-4f ? (hh + hullReach) / Mathf.Abs(cs) : float.MaxValue;
                            float lo = Mathf.Min(byX, byU);
                            byX = Mathf.Abs(sn) > 1e-4f ? (hw + reachSkim) / Mathf.Abs(sn) : float.MaxValue;
                            byU = Mathf.Abs(cs) > 1e-4f ? (hh + reachSkim) / Mathf.Abs(cs) : float.MaxValue;
                            float hi = Mathf.Max(lo, Mathf.Min(byX, byU));
                            for (float rho = lo; rho <= hi; rho += 0.5f)
                            {
                                float pa = rho * sn, pb = rho * cs;
                                float dd = Mathf.Sqrt((pa - a) * (pa - a) + (pb - b) * (pb - b));
                                if (dd < best) best = dd;
                            }
                        }
                        need.Add(Mathf.Max(0f, best - pass));
                        rs.Add(Mathf.Sqrt(a * a + b * b));
                    }
                    need.Sort(); rs.Sort();
                    all.AddRange(need);
                    int zero = need.Count(x => x <= 0f);
                    Console.WriteLine($"  anchor {k,2} s={sa,7:F0}  |offset| median {rs[rs.Count / 2],5:F1} [{rs[0],5:F1}..{rs[^1],5:F1}]  " +
                                      $"excursion beyond band: in-band {100f * zero / need.Count,3:F0}%  median {need[need.Count / 2],5:F1}  p90 {need[(int)(need.Count * 0.9)],5:F1}  max {need[^1],5:F1}");
                }
                all.Sort();
                Console.WriteLine($"  all: in-band {100f * all.Count(x => x <= 0f) / all.Count:F0}%  median {all[all.Count / 2]:F1}  p90 {all[(int)(all.Count * 0.9)]:F1}  max {all[^1]:F1}");
                return 0;
            }

            if (args.Contains("--opt-test"))
            {
                // Tooling: one SkimPathOptimizer solve on a fixed scenario, printed point by point:
                // the ship on the top face's band at --s, flying along the ribbon at --v, a crystal
                // --ahead u on at offset (--ca, --cb).
                int ti = int.Parse(Arg(args, "--opt-test", "2"));
                var prisms = TrackBuilder.Build(cfg, cfg.Tracks[ti - 1]);
                var list = new List<SkimRoutePrism>();
                foreach (var tp in prisms) list.Add(new SkimRoutePrism(tp.Position, tp.Rotation, tp.ShellSemi, tp.Marker));
                var route = new SkimRoute(list, closed: true);
                float s0 = float.Parse(Arg(args, "--s", "200"));
                float v = float.Parse(Arg(args, "--v", "300"));
                float ahead = float.Parse(Arg(args, "--ahead", "500"));
                float ca = float.Parse(Arg(args, "--ca", "40")), cb = float.Parse(Arg(args, "--cb", "-10"));
                float h0 = float.Parse(Arg(args, "--b0", "6.5"));
                int iters = int.Parse(Arg(args, "--iters", "20"));
                var prof = Tweak(SkimRacerProfile.Expert());
                float reach = Mathf.Sqrt(cfg.HullHalf.x * cfg.HullHalf.x + cfg.HullHalf.y * cfg.HullHalf.y);
                var set = new SkimPathOptimizer.Settings
                {
                    Budget = prof.PlanAuthority * cfg.YawDps * Mathf.Deg2Rad * prof.RollBonus,
                    FollowRate = cfg.FollowRate,
                    HingeWeight = prof.OptHingeWeight,
                    DemandWeight = prof.OptDemandWeight,
                    BandWeight = float.Parse(Arg(args, "--band", "0.1")),
                    Rho = float.Parse(Arg(args, "--rho", "1")),
                    ClearMargin = prof.ClearanceMargin, SkimReach = cfg.SkimRadius - 0.5f, ObstacleMargin = prof.ObstacleMargin,
                };
                var opt = new SkimPathOptimizer();
                int nc = Math.Max(4, (int)Math.Round(ahead / float.Parse(Arg(args, "--grid", "12"))));
                float step = ahead / nc;
                int count = nc + (int)Math.Ceiling(240f / step) + 1;
                opt.Setup(route, s0, step, count, cfg.HullHalf.z + prof.ClearanceMargin, reach, set);
                for (int i = 0; i < opt.Count; i++) { opt.SetPoint(i, 0f, h0); opt.SetSpeed(i, v); }
                // Polar seed toward the crystal, like the brain does.
                float phiT = Mathf.Atan2(ca, cb), rhoT = Mathf.Sqrt(ca * ca + cb * cb);
                for (int i = 3; i < opt.Count; i++)
                {
                    float t = Mathf.Clamp01((i - 2f) / (nc - 2f));
                    float w = t * t * (3f - 2f * t);
                    float phi = phiT * w, rho = h0 + (rhoT - 20f - h0) * w;
                    if (i > nc) { phi = phiT; rho = rhoT - 20f; }
                    rho = Mathf.Max(rho, 22f * (Mathf.Abs(Mathf.Sin(phi)) > 0.5f ? 1f : 0.2f));
                    opt.SetPoint(i, rho * Mathf.Sin(phi), rho * Mathf.Cos(phi));
                }
                opt.SetCrystal(nc, ca, cb, 20f);
                if (args.Contains("--show-init"))
                {
                    // The bare ribbon at the start height: what the base geometry alone asks for.
                    var keep = new List<(float, float)>();
                    for (int i = 0; i < opt.Count; i++) { keep.Add((opt.A(i), opt.B(i))); opt.SetPoint(i, 0f, h0); }
                    var dl = new List<string>();
                    for (int i = 1; i < opt.Count - 2; i++) dl.Add($"{opt.Demand(i):F2}");
                    Console.WriteLine("  bare ribbon demand: " + string.Join(" ", dl));
                    for (int i = 0; i < opt.Count; i++) opt.SetPoint(i, keep[i].Item1, keep[i].Item2);
                }
                var sw = System.Diagnostics.Stopwatch.StartNew();
                opt.DebugLog = Console.WriteLine;
                opt.Solve(iters);
                Console.WriteLine($"  {iters} iterations in {sw.Elapsed.TotalMilliseconds:F1} ms, {opt.Count} points, step {step:F2}");
                for (int i = 0; i < opt.Count; i++)
                    Console.WriteLine($"   {i,3} s={s0 + i * step,7:F1} a={opt.A(i),7:F2} b={opt.B(i),7:F2} demand={opt.Demand(i),6:F2} band={(opt.InBand(i) ? "in" : "--")} {opt.Describe(i)}{(i == nc ? "  <- crystal" : "")}");
                return 0;
            }

            if (args.Contains("--dump-lane"))
            {
                // Tooling: the racing line's offsets around a lap, both faces, every 48 u.
                int ti = int.Parse(Arg(args, "--dump-lane", "2"));
                var prisms = TrackBuilder.Build(cfg, cfg.Tracks[ti - 1]);
                var list = new List<SkimRoutePrism>();
                foreach (var tp in prisms) list.Add(new SkimRoutePrism(tp.Position, tp.Rotation, tp.ShellSemi, tp.Marker));
                var route = new SkimRoute(list, closed: true);
                var prof = Tweak(SkimRacerProfile.Expert());
                float reach = Mathf.Sqrt(cfg.HullHalf.x * cfg.HullHalf.x + cfg.HullHalf.y * cfg.HullHalf.y);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var line = new SkimRacingLine(route, prof.NominalHeight, prof.LaneWidth, reach, prof.LaneMargin,
                    cfg.SkimRadius, cfg.HullHalf.z + prof.ClearanceMargin, cfg.ThrottleScaler * cfg.BoostMax, cfg.FollowRate, prof.BandWeight);
                Console.WriteLine($"solved in {sw.ElapsedMilliseconds} ms");
                float from = float.Parse(Arg(args, "--from", "0")), to = float.Parse(Arg(args, "--to", "1e9"));
                for (float x = 0f; x < route.Length; x += 48f)
                {
                    if (x < from || x > to) continue;
                    line.Offset(0, x, out float a0, out float b0);
                    line.Offset(1, x, out float a1, out float b1);
                    route.Envelope(x, 0f, out float hw, out float hh);
                    Console.WriteLine($"  s={x,6:F0}  top ({a0,6:F2},{b0,6:F2})  bottom ({a1,6:F2},{b1,6:F2})  plate hh {hh:F2}");
                }
                return 0;
            }

            if (args.Contains("--profile-route"))
            {
                // Tooling: the stick rate (rad/s) the bare skimming line needs around a whole lap at a
                // given speed, against the ship's 120 deg/s (2.09 rad/s).
                int ti = int.Parse(Arg(args, "--profile-route", "2"));
                float v = float.Parse(Arg(args, "--v", "300"));
                var prisms = TrackBuilder.Build(cfg, cfg.Tracks[ti - 1]);
                var list = new List<SkimRoutePrism>();
                foreach (var tp in prisms) list.Add(new SkimRoutePrism(tp.Position, tp.Rotation, tp.ShellSemi, tp.Marker));
                var route = new SkimRoute(list, closed: true);
                var brain = new SkimRacerBrain(route, Tweak(SkimRacerProfile.Expert()), 1);
                route.Frame(0f, out Vector3 c0, out Vector3 t0, out Vector3 r0, out Vector3 u0);
                var sensors = new SkimRacerSensors
                {
                    Time = 0f, DeltaTime = 1f / 60f, Position = c0 + u0 * 7.5f,
                    Rotation = Quaternion.LookRotation(t0, u0), CommandedRotation = Quaternion.LookRotation(t0, u0),
                    Course = t0, Speed = v, BoostMultiplier = cfg.BoostMax, MaxBoost = cfg.BoostMax,
                    ThrottleScaler = cfg.ThrottleScaler, PitchRateDegrees = cfg.PitchDps, YawRateDegrees = cfg.YawDps,
                    RollRateDegrees = cfg.RollDps, FollowRate = cfg.FollowRate, SkimRadius = cfg.SkimRadius,
                    HullHalfExtents = cfg.HullHalf, HasCrystal = false, CrystalRadius = cfg.CaptureRadius,
                };
                brain.Tick(sensors, null);
                if (args.Contains("--plan-at"))
                    foreach (float x in new[] { 0f, 30f, 100f, 480f, 528f, 576f })
                    {
                        brain.DescribePlan(brain.RouteS + x, out float pr, out float pp);
                        brain.RacingLine.Offset(0, brain.RouteS + x, out float la, out float lb);
                        Console.WriteLine($"  plan at s={x}: ({pr * Mathf.Sin(pp):F2},{pr * Mathf.Cos(pp):F2}) lane ({la:F2},{lb:F2}) keys={brain.KeyCount} choice={brain.LastChoice}");
                    }
                var rates = new List<float>();
                var turns = new List<float>();
                // The plan starts where the brain picked the ship up, which on a closed lap may be
                // the seam's far side (s = L rather than 0); profile one lap from there.
                brain.ProfilePlan(brain.RouteS, brain.RouteS + route.Length, v, rates, turns);
                if (args.Contains("--turn-only")) rates = turns;
                if (args.Contains("--plane"))
                {
                    // Split the centre line's curvature into in-plane (along the plates' right) and
                    // out-of-plane (along their up) parts, per 120 u, as turn rate at v.
                    for (float s0 = 0f; s0 < route.Length; s0 += 120f)
                    {
                        float inPlane = 0f, outPlane = 0f;
                        for (float x = s0; x < s0 + 120f; x += 6f)
                        {
                            Vector3 pm = route.Position(x - 24f), p0 = route.Position(x), pp = route.Position(x + 24f);
                            Vector3 k = ((pp - p0).normalized - (p0 - pm).normalized) / 24f;
                            route.Frame(x, out _, out _, out Vector3 rr, out Vector3 uu);
                            inPlane = Math.Max(inPlane, Math.Abs(Vector3.Dot(k, rr)) * v);
                            outPlane = Math.Max(outPlane, Math.Abs(Vector3.Dot(k, uu)) * v);
                        }
                        Console.WriteLine($"  s={s0,6:F0}  in-plane {inPlane:F2}  out-of-plane {outPlane:F2} rad/s");
                    }
                    return 0;
                }
                if (args.Contains("--detail"))
                {
                    float a0 = float.Parse(Arg(args, "--from", "0")), a1 = float.Parse(Arg(args, "--to", "600"));
                    for (int i = 0; i < rates.Count; i++)
                    {
                        float si = i * 6f;
                        if (si < a0 || si > a1) continue;
                        Console.WriteLine($"  s={si,7:F1} demand={rates[i],5:F2} turn={turns[i],5:F2}");
                    }
                    return 0;
                }
                const float bucket = 120f;
                Console.WriteLine($"I{ti} lap {route.Length:F0} u, {route.Count} prisms, stick rate needed at {v} u/s (max per {bucket} u):");
                float worst = 0f;
                for (int b = 0; b * bucket < route.Length; b++)
                {
                    float m = 0f;
                    for (int i = 0; i < rates.Count; i++)
                    {
                        float si = i * 6f;
                        if (si >= b * bucket && si < (b + 1) * bucket) m = Math.Max(m, rates[i]);
                    }
                    worst = Math.Max(worst, m);
                    Console.Write($"{m,5:F2}{((b + 1) % 12 == 0 ? "\n" : " ")}");
                }
                Console.WriteLine($"\nworst {worst:F2} rad/s  ({worst / (cfg.PitchDps * Mathf.Deg2Rad) * 100f:F0}% of stick)");
                return 0;
            }

            if (args.Contains("--intensity"))
            {
                int intensity = int.Parse(Arg(args, "--intensity", "2"));
                int racers = int.Parse(Arg(args, "--racers", "1"));
                int seeds = int.Parse(Arg(args, "--seeds", "10"));
                string profile = Arg(args, "--profile", "expert");
                float fps = float.Parse(Arg(args, "--fps", "60"));
                bool trace = args.Contains("--trace");
                _segStats = args.Contains("--segments");
                _ghost = args.Contains("--ghost");
                _traceFrom = float.Parse(Arg(args, "--trace-from", "0"));
                _traceTo = float.Parse(Arg(args, "--trace-to", "1e9"));
                _traceDt = float.Parse(Arg(args, "--trace-dt", "0.25"));
                int firstSeed = int.Parse(Arg(args, "--seed", "1"));
                var res = Batch(cfg, intensity, racers, seeds, profile, fps, trace, printEach: true, firstSeed);
                Print($"I{intensity} x{racers} {profile} @{fps}fps", res);
                return 0;
            }

            return StandardReport(cfg);
        }

        static readonly List<(string field, float value)> _tweaks = new();

        /// <summary>Applies every <c>--set Field=Value</c> to a profile — for sweeping the brain's
        /// parameters without editing it.</summary>
        static SkimRacerProfile Tweak(SkimRacerProfile p)
        {
            foreach (var (field, value) in _tweaks)
            {
                var f = typeof(SkimRacerProfile).GetField(field) ?? throw new ArgumentException($"no profile field {field}");
                f.SetValue(p, value);
            }
            return p;
        }

        static string Arg(string[] a, string k, string d)
        {
            int i = Array.IndexOf(a, k);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : d;
        }

        sealed class Result
        {
            public readonly List<float> Times = new();
            public int Dnf, RibbonTouches, RailTouches, Racers, Reanchors, Unresolved;
            public double MeanCross;
            public float MaxCross;
            public float FirstFull;
        }

        static float _traceFrom, _traceTo = float.MaxValue, _traceDt = 0.25f;
        static bool _segStats, _ghost;
        static readonly List<(float dt, float ds, bool face, float vmin, float gap, float bm0)> _segRows = new();

        static Result Batch(Config cfg, int intensity, int racers, int seeds, string profile, float fps, bool trace, bool printEach, int firstSeed = 1)
        {
            var res = new Result();
            double crossSum = 0; int crossN = 0; double firstFullSum = 0; int firstFullN = 0;
            for (int seed = firstSeed; seed < firstSeed + seeds; seed++)
            {
                var opts = new RaceOptions
                {
                    Intensity = intensity,
                    Racers = racers,
                    Seed = seed,
                    Dt = 1f / fps,
                    DtJitter = fps < 59f ? 0.15f : 0f,
                    Trace = trace,
                    Ghost = _ghost,
                    TraceFrom = _traceFrom,
                    TraceTo = _traceTo,
                    TraceDt = _traceDt,
                    Profile = _ => Tweak(profile == "tier" ? SkimRacerProfile.ForIntensity(intensity) : SkimRacerProfile.Expert()),
                };
                var race = new Race(cfg, opts);
                race.Run();
                foreach (var r in race.Racers)
                {
                    var s = r.Stats;
                    res.Racers++;
                    _segRows.AddRange(r.Stats.Segments);
                    if (s.FinishTime < 0f) res.Dnf++; else res.Times.Add(s.FinishTime);
                    res.RibbonTouches += s.RibbonTouches;
                    res.RailTouches += s.RailTouches;
                    res.Reanchors += r.Brain.Reanchors;
                    res.Unresolved += r.Brain.UnresolvedConflicts;
                    crossSum += s.CrossTrackSum; crossN += s.CrossTrackSamples;
                    res.MaxCross = Math.Max(res.MaxCross, s.MaxCrossTrack);
                    if (s.TimeToFirstFullBoost >= 0f) { firstFullSum += s.TimeToFirstFullBoost; firstFullN++; }
                    if (printEach)
                    {
                        Console.WriteLine($"  seed {seed,3} racer {r.Id}: {(s.FinishTime < 0 ? "DNF" : $"{s.FinishTime,6:F1}s")} " +
                            $"crystals {s.Crystals,2}/{race.Target}  ribbon {s.RibbonTouches} rail {s.RailTouches}  " +
                            $"skims {s.SkimHits}+{s.RailSkimHits}  full-boost {s.TimeAtFullBoost,5:F1}s (first {s.TimeToFirstFullBoost,5:F1}s)  " +
                            $"xtrack mean {s.CrossTrackSum / Math.Max(1, s.CrossTrackSamples):F2} max {s.MaxCrossTrack:F1}  " +
                            $"reanchor {r.Brain.Reanchors} avoid {r.Brain.Avoidances} unresolved {r.Brain.UnresolvedConflicts} faces {r.Brain.FaceChanges}  " +
                            $"lost: throttle {s.LostThrottle:F1}s boost {s.LostBoost:F1}s contact {s.LostContact:F1}s other {s.LostOther:F1}s  laps {LapSummary(s)}");
                        if (trace || s.RibbonTouches + s.RailTouches > 0)
                            foreach (var e in s.Events.Take(trace ? 4000 : 12)) Console.WriteLine("      " + e);
                    }
                }
            }
            res.MeanCross = crossSum / Math.Max(1, crossN);
            if (printEach && _segStats)
            {
                // Crystal-to-crystal segments after the first lap: seconds per 1000 u of route, by
                // whether the brain changed face for that crystal.
                foreach (bool face in new[] { false, true })
                {
                    var rows = _segRows.Where(x => x.face == face && x.bm0 > 4f).ToList();
                    if (rows.Count == 0) continue;
                    double secPerKu = rows.Sum(x => x.dt) / Math.Max(1, rows.Sum(x => x.ds)) * 1000.0;
                    Console.WriteLine($"  segments {(face ? "WITH face change" : "same face      ")}: n={rows.Count,3}  {secPerKu:F2} s per 1000 u " +
                                      $"(ideal {1000.0 / 300:F2})  mean vmin {rows.Average(x => x.vmin):F0}  mean peak gap {rows.Average(x => x.gap):F0}deg");
                }
                _segRows.Clear();
            }
            res.FirstFull = firstFullN > 0 ? (float)(firstFullSum / firstFullN) : -1f;
            return res;
        }

        static string LapSummary(RacerStats s)
        {
            var parts = new List<string>();
            float prev = 0f;
            for (int i = 0; i < s.LapTimes.Count; i++)
            {
                float d = s.LapDuration[i];
                parts.Add($"{s.LapTimes[i] - prev:F1}s/{100f * s.InBand[i] / Math.Max(1e-3f, d):F0}%");
                prev = s.LapTimes[i];
            }
            return string.Join(" ", parts);
        }

        static void Print(string label, Result r)
        {
            var t = r.Times.OrderBy(x => x).ToList();
            string times = t.Count == 0 ? "no finishers"
                : $"mean {t.Average():F1}s  median {t[t.Count / 2]:F1}s  best {t[0]:F1}s  worst {t[^1]:F1}s";
            Console.WriteLine($"{label,-28} {times}  DNF {r.Dnf}/{r.Racers}  ribbon touches {r.RibbonTouches}  " +
                              $"rail touches {r.RailTouches}  reanchors {r.Reanchors}  unresolved {r.Unresolved}  " +
                              $"x-track mean {r.MeanCross:F2} max {r.MaxCross:F1}  first 5x at {r.FirstFull:F1}s");
        }

        static int StandardReport(Config cfg)
        {
            Console.WriteLine("== Squirrel skim racer, raced offline against the shipped flight model ==");
            int failures = 0;
            for (int i = 1; i <= cfg.Tracks.Count; i++)
            {
                var solo = Batch(cfg, i, 1, 10, "expert", 60f, false, printEach: false);
                Print($"I{i} solo expert", solo);
                var field = Batch(cfg, i, 3, 6, "expert", 60f, false, printEach: false);
                Print($"I{i} 3-AI field expert", field);
                var tier = Batch(cfg, i, 3, 6, "tier", 60f, false, printEach: false);
                Print($"I{i} 3-AI field tier", tier);
                if (i == 2)
                {
                    var slow = Batch(cfg, i, 3, 6, "tier", 30f, false, printEach: false);
                    Print($"I{i} 3-AI field tier @30fps", slow);
                    failures += Gate("intensity 2: every tier racer finishes", tier.Dnf == 0);
                    float mean = tier.Times.Count > 0 ? tier.Times.Average() : 999f;
                    failures += Gate($"intensity 2: tier mean {mean:F1}s is within 64..76s (target ~70s)", mean >= 64f && mean <= 76f);
                    failures += Gate("intensity 2 @30fps: every tier racer finishes", slow.Dnf == 0);
                }
                failures += Gate($"intensity {i}: every solo expert finishes", solo.Dnf == 0);
            }
            Console.WriteLine(failures == 0 ? "ALL GATES PASS" : $"{failures} GATE(S) FAILED");
            return failures == 0 ? 0 : 1;
        }

        static int Gate(string what, bool ok)
        {
            Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
            return ok ? 0 : 1;
        }
    }
}
