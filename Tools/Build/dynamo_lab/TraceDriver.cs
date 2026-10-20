// TraceDriver.cs - runs the SHIPPED DynamoCore (compiled from its real path by trace.sh) over the scenarios parity.cjs
// writes, and prints the same rows the Honeyguide Lab's own core produces: per step, whether the input was legal, the
// state digest and the events. Also runs every slip case through DynamoCore.SlipDestination.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CosmicShore.Gameplay;

public static class TraceDriver
{
    static readonly Dictionary<string, DynamoPatternEffect> Effects = new()
    {
        [""] = DynamoPatternEffect.None, ["amanita"] = DynamoPatternEffect.AmanitaFurnace, ["bigred"] = DynamoPatternEffect.BigRedButton,
        ["saddle"] = DynamoPatternEffect.DragonSaddle, ["thruster"] = DynamoPatternEffect.ThrusterSwarm, ["detangler"] = DynamoPatternEffect.RealityDetangler,
        ["condenser"] = DynamoPatternEffect.EpitaxialCondenser, ["anode"] = DynamoPatternEffect.AnodeRay, ["printer"] = DynamoPatternEffect.FiveDPrinter,
        ["blink"] = DynamoPatternEffect.BlinkDrive, ["rosen"] = DynamoPatternEffect.RosenBridge, ["monte"] = DynamoPatternEffect.MonteCannon,
        ["chrono"] = DynamoPatternEffect.ChronoCompressor,
    };
    static readonly Dictionary<string, DynamoInputKind> Kinds = new()
    {
        ["tinker"] = DynamoInputKind.Tinker, ["refuel"] = DynamoInputKind.Refuel, ["activate"] = DynamoInputKind.Activate, ["pick"] = DynamoInputKind.Pick,
        ["research"] = DynamoInputKind.Research, ["keep"] = DynamoInputKind.Keep, ["discard"] = DynamoInputKind.Discard,
    };

    static int Int(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
    static int[][] Pairs(JsonElement a) => a.EnumerateArray().Select(p => p.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();

    static string Ev(DynamoEvent e) => e.Kind switch
    {
        DynamoEventKind.Tinker => $"T{e.From}>{e.To}",
        DynamoEventKind.Refuel => $"R{e.Cell}:{e.Colour}",
        DynamoEventKind.Place => $"P{e.Cell}:{e.Colour}",
        DynamoEventKind.Ability => $"A{e.Slot}:{e.Pid}",
        DynamoEventKind.Charge => $"C{e.Colour}",
        DynamoEventKind.Replaced => $"X{e.Colour}",
        DynamoEventKind.Pick => $"K{e.Slot}:{e.Pid}:{e.Cell}:{e.Colour}:{(e.Auto ? 1 : 0)}",
        DynamoEventKind.Destroyed => $"D{e.Colour}:{e.Count}",
        DynamoEventKind.Burned => $"B{e.Cell}:{e.Colour}",
        DynamoEventKind.Offer => $"O{e.Family}:{string.Join(".", e.Offer)}",
        DynamoEventKind.Keep => $"E{e.Slot}:{e.Pid}",
        DynamoEventKind.Tick => $"t{e.Count}",
        _ => "?",
    };

    static string Num(double v) => v.ToString("R", CultureInfo.InvariantCulture);
    static string Vec(SlipVec v) => $"[{Num(v.X)},{Num(v.Y)},{Num(v.Z)}]";
    static SlipVec V(JsonElement a) => new(a[0].GetDouble(), a[1].GetDouble(), a[2].GetDouble());

    public static int Main(string[] args)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(args[0]));
        var sb = new StringBuilder("{\"scenarios\":[");
        bool firstScn = true;
        foreach (var scn in doc.RootElement.GetProperty("scenarios").EnumerateArray())
        {
            var j = scn.GetProperty("S");
            var s = new DynamoSettings
            {
                TickSeconds = j.GetProperty("tickSeconds").GetDouble(),
                ApPerTick = Int(j, "apPerTick"), ApBank = Int(j, "apBank"), BonusApCap = Int(j, "bonusApCap"), BatteryCap = Int(j, "batteryCap"),
                ResearchCost = Int(j, "researchCost"), PickTimeout = j.GetProperty("pickTimeout").GetDouble(),
                RotateWholeEngine = j.GetProperty("rotateWholeEngine").GetBoolean(), AllowRotatedMatches = j.GetProperty("allowRotatedMatches").GetBoolean(),
                CentreWild = j.GetProperty("centreWild").GetBoolean(), StartFilled = j.GetProperty("startFilled").GetBoolean(),
                Ports = Pairs(j.GetProperty("ports")),
                Loadout = j.GetProperty("loadout").EnumerateArray().Select(x => x.GetInt32()).ToArray(),
            };
            foreach (var p in j.GetProperty("patterns").EnumerateArray())
                s.Patterns.Add(new DynamoPattern { Family = p.GetProperty("family").GetInt32(), Effect = Effects[p.GetProperty("effect").GetString()], Cells = Pairs(p.GetProperty("cells")) });
            var core = new DynamoCore(s, scn.GetProperty("seed").GetInt32());
            if (!firstScn) sb.Append(',');
            firstScn = false;
            sb.Append("[{\"ok\":true,\"d\":\"").Append(core.Digest()).Append("\",\"e\":[]}");
            foreach (var st in scn.GetProperty("steps").EnumerateArray())
            {
                bool ok = true;
                if (st.TryGetProperty("dt", out var dt)) core.Advance(s, dt.GetDouble());
                else if (st.TryGetProperty("spend", out var sp)) ok = core.Spend(sp.GetInt32());
                else if (st.TryGetProperty("drain", out _)) core.DrainAll();
                else
                {
                    var i = st.GetProperty("inp");
                    var inp = new DynamoInput
                    {
                        Kind = Kinds[i.GetProperty("k").GetString()], From = Int(i, "from"), To = Int(i, "to"), Cell = Int(i, "cell"), Colour = Int(i, "colour"),
                        Slot = Int(i, "slot"), Family = Int(i, "family"), Choice = Int(i, "choice"),
                    };
                    ok = core.Apply(s, inp);
                }
                sb.Append(",{\"ok\":").Append(ok ? "true" : "false").Append(",\"d\":\"").Append(core.Digest()).Append("\",\"e\":[");
                sb.Append(string.Join(",", core.Events.Select(e => "\"" + Ev(e) + "\"")));
                sb.Append("]}");
                core.Events.Clear();
            }
            sb.Append(']');
        }
        sb.Append("],\"slips\":[");
        bool firstSlip = true;
        foreach (var c in doc.RootElement.GetProperty("slips").EnumerateArray())
        {
            var pts = c.GetProperty("pts").EnumerateArray().Select(V).ToList();
            var r = DynamoCore.SlipDestination(pts, c.GetProperty("contact").GetInt32(), V(c.GetProperty("pos")), V(c.GetProperty("fwd")), c.GetProperty("reach").GetDouble(), c.GetProperty("exit").GetDouble());
            if (!firstSlip) sb.Append(',');
            firstSlip = false;
            if (r == null) { sb.Append("null"); continue; }
            var l = r.Value;
            sb.Append($"{{\"end\":{l.End},\"index\":{l.Index},\"clipped\":{(l.Clipped ? "true" : "false")},\"heading\":{Vec(l.Heading)},\"point\":{Vec(l.Point)}}}");
        }
        sb.Append("]}");
        Console.WriteLine(sb.ToString());
        return 0;
    }
}
