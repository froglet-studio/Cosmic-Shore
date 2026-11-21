// FeaturesHarness.cs - runs RegattaCourseFeaturesTests offline and writes features.json, the fixture the Python port
// (Tools/Compute/evaluators/regatta_features.py --self-test) is held to. Compiled by features.sh.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

public static class FeaturesHarness
{
    static readonly CultureInfo CI = CultureInfo.InvariantCulture;
    static string F1(float v) => v.ToString("0.#", CI);
    static string F3(float v) => v.ToString("0.###", CI);
    static string F4(float v) => v.ToString("0.####", CI);
    static string V(Vector3 v) => "[" + F1(v.x) + "," + F1(v.y) + "," + F1(v.z) + "]";
    static string A(Vector3 v) => "[" + F3(v.x) + "," + F3(v.y) + "," + F3(v.z) + "]";

    /// <summary>The fixture's feature sets. Their specs are written into features.json, so the port reads them from
    /// there rather than retyping them.</summary>
    static List<(string name, RegattaCourseFeatures features)> Sets()
    {
        var busy = new RegattaCourseFeatures();
        busy.Sections.Add(new RegattaRailSection(-1, 0.3f, 0.7f, neutral: true));
        busy.Sections.Add(new RegattaRailSection(2, 0.4f, 0.6f, RegattaRailKind.Gap));
        busy.Sections.Add(new RegattaRailSection(5, 0.2f, 0.8f, RegattaRailKind.Plain, detour: 120f, detourBearing: 30f));
        busy.Sections.Add(new RegattaRailSection(6, 0.1f, 0.5f, RegattaRailKind.Shielded));
        busy.AlternateGates.Add(new RegattaAlternateGate(3, 200f, 0f, 0.8f));
        busy.AlternateGates.Add(new RegattaAlternateGate(6, 180f, 90f));
        busy.Crystals = new RegattaLineCrystals { PerLap = 3, Phase = 0.1f };

        var detour = new RegattaCourseFeatures();
        detour.Sections.Add(new RegattaRailSection(-1, 0.15f, 0.85f, detour: 90f, detourBearing: 45f));

        var gaps = new RegattaCourseFeatures();
        gaps.Sections.Add(new RegattaRailSection(1, 0.35f, 0.65f, RegattaRailKind.Gap));
        gaps.Sections.Add(new RegattaRailSection(4, 0.35f, 0.65f, RegattaRailKind.Gap));
        gaps.Sections.Add(new RegattaRailSection(7, 0.5f, 0.5025f, RegattaRailKind.Gap));   // too short: laid as rail
        gaps.AlternateGates.Add(new RegattaAlternateGate(0, 160f, 200f, 1.2f));
        gaps.Crystals = new RegattaLineCrystals { PerLap = 9, Phase = 1.6f };              // capped; phase wraps

        return new List<(string, RegattaCourseFeatures)> { ("empty", RegattaCourseFeatures.None), ("busy", busy),
                                                           ("detour", detour), ("gaps", gaps) };
    }

    static string Spec(RegattaCourseFeatures f)
    {
        var sb = new StringBuilder("{\"Sections\":[");
        for (int i = 0; i < f.Sections.Count; i++)
        {
            var s = f.Sections[i];
            sb.Append((i > 0 ? "," : "") + "{\"Leg\":" + s.Leg + ",\"From\":" + s.From.ToString("R", CI) + ",\"To\":" +
                      s.To.ToString("R", CI) + ",\"Kind\":" + (int)s.Kind + ",\"Neutral\":" + (s.Neutral ? "true" : "false") +
                      ",\"Detour\":" + s.Detour.ToString("R", CI) + ",\"DetourBearing\":" + s.DetourBearing.ToString("R", CI) + "}");
        }
        sb.Append("],\"AlternateGates\":[");
        for (int i = 0; i < f.AlternateGates.Count; i++)
        {
            var a = f.AlternateGates[i];
            sb.Append((i > 0 ? "," : "") + "{\"Ring\":" + a.Ring + ",\"Offset\":" + a.Offset.ToString("R", CI) + ",\"Bearing\":" +
                      a.Bearing.ToString("R", CI) + ",\"RadiusScale\":" + a.RadiusScale.ToString("R", CI) + "}");
        }
        sb.Append("],\"Crystals\":{\"PerLap\":" + f.Crystals.PerLap + ",\"Phase\":" + f.Crystals.Phase.ToString("R", CI) + "}}");
        return sb.ToString();
    }

    static char Code(RegattaRailPlan plan, int k)
    {
        if (plan.Gap[k]) return '_';
        char c = plan.Kinds[k] == PrismKind.Plain ? 'p' : plan.Kinds[k] == PrismKind.Shielded ? 'h' : 's';
        return plan.Neutral[k] ? char.ToUpperInvariant(c) : c;
    }

    static int RunTests()
    {
        var type = typeof(CosmicShore.Tests.RegattaCourseFeaturesTests);
        var instance = Activator.CreateInstance(type);
        int pass = 0, fail = 0;
        foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (m.GetCustomAttribute<NUnit.Framework.TestAttribute>() == null) continue;
            try { m.Invoke(instance, null); pass++; Console.Error.WriteLine("  pass  " + m.Name); }
            catch (TargetInvocationException e)
            {
                fail++;
                Console.Error.WriteLine("  FAIL  " + m.Name + ": " + e.InnerException?.Message);
            }
        }
        Console.Error.WriteLine($"RegattaCourseFeaturesTests: {pass} passed, {fail} failed");
        return fail;
    }

    public static int Main()
    {
        int failures = RunTests();
        var sb = new StringBuilder();
        sb.Append("{\n  \"_readme\": \"GENERATED by Tools/Build/regatta_course_harness/features.sh from RegattaCourse.BuildRails + " +
                  "RegattaCourseFeatures (the shipped C#). Per feature set and intensity: every 4th lane point and leg param, every prism's " +
                  "plan code (s/h/p = super-shielded/shielded/plain, upper case = neutral colour, _ = gap), the runs, " +
                  "the alternate rings and the line crystals. regatta_features.py --self-test must reproduce it. Do not edit.\",\n");
        sb.Append("  \"sets\": {\n");
        var sets = Sets();
        for (int si = 0; si < sets.Count; si++)
        {
            var (name, f) = sets[si];
            sb.Append("    \"" + name + "\": {\"features\": " + Spec(f) + ",\n");
            for (int i = 1; i <= 4; i++)
            {
                var gates = RegattaCourse.BuildGates(RegattaCourse.DefaultSeed, i);
                var rails = RegattaCourse.BuildRails(gates, RegattaCourse.DefaultRails, f);
                sb.Append("      \"" + i + "\": {\"spine\": " + F1(rails.SpineLength) + ", \"laneLengths\": [");
                for (int l = 0; l < rails.LaneLengths.Count; l++) sb.Append((l > 0 ? "," : "") + F1(rails.LaneLengths[l]));
                sb.Append("], \"lanes\": [");
                for (int l = 0; l < rails.LanePositions.Count; l++)
                {
                    var plan = RegattaRailPlan.ForLane(rails.LaneLegParams[l], gates.Count, f);
                    var pos = rails.LanePositions[l];
                    var par = rails.LaneLegParams[l];
                    sb.Append((l > 0 ? ",\n        " : "\n        ") + "{\"count\": " + pos.Length + ", \"every4\": [");
                    for (int k = 0; k < pos.Length; k += 4) sb.Append((k > 0 ? "," : "") + F1(pos[k].x) + "," + F1(pos[k].y) + "," + F1(pos[k].z));
                    sb.Append("], \"params4\": [");
                    for (int k = 0; k < par.Length; k += 4) sb.Append((k > 0 ? "," : "") + F4(par[k]));
                    sb.Append("], \"plan\": \"");
                    for (int k = 0; k < pos.Length; k++) sb.Append(Code(plan, k));
                    sb.Append("\", \"runs\": [");
                    for (int r = 0; r < plan.Runs.Count; r++)
                        sb.Append((r > 0 ? "," : "") + "[" + plan.Runs[r].Start + "," + plan.Runs[r].Count + "," + (plan.Runs[r].Closed ? 1 : 0) + "]");
                    sb.Append("]}");
                }
                sb.Append("],\n        \"alternates\": [");
                bool first = true;
                for (int g = 0; g < gates.Count; g++)
                {
                    if (!f.TryAlternate(gates, g, out var alt)) continue;
                    sb.Append((first ? "" : ",") + "{\"ring\":" + g + ",\"p\":" + V(alt.Position) + ",\"a\":" + A(alt.Axis) + ",\"r\":" + F1(alt.Radius) + "}");
                    first = false;
                }
                sb.Append("], \"crystals\": [");
                var cr = f.LineCrystalPoints(rails);
                for (int c = 0; c < cr.Count; c++) sb.Append((c > 0 ? "," : "") + V(cr[c]));
                sb.Append("]}" + (i < 4 ? ",\n" : "\n"));
            }
            sb.Append("    }" + (si < sets.Count - 1 ? ",\n" : "\n"));
        }
        sb.Append("  }\n}\n");
        Console.Out.Write(sb.ToString());
        return failures == 0 ? 0 : 1;
    }
}
