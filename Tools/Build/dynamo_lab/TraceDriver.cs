// TraceDriver.cs - runs the SHIPPED DynamoCore (compiled from its real path by trace.sh) over the scenarios parity.cjs
// writes, and prints the same rows the Honeyguide Lab's own core produces: per step, whether the input was legal, the
// state digest and the events (an Engineer-AI step first prints the input the C# AI chose). Also builds the port sets from
// the engines' printed rows and runs every slip case through DynamoCore.SlipDestination.
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
    static readonly string[] EffectKeys = { "burn", "purge", "free", "bonus", "rewind", "unspin", "trio", "scatter", "charge", "drain", "drainOne" };
    static readonly Dictionary<string, DynamoInputKind> Kinds = new()
    {
        ["tinker"] = DynamoInputKind.Tinker, ["refuel"] = DynamoInputKind.Refuel, ["activate"] = DynamoInputKind.Activate, ["remove"] = DynamoInputKind.Remove,
        ["pick"] = DynamoInputKind.Pick,
    };

    static int Int(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
    static int[][] Pairs(JsonElement a) => a.EnumerateArray().Select(p => p.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();

    static string Ev(DynamoEvent e) => e.Kind switch
    {
        DynamoEventKind.Tinker => $"T{e.From}>{e.To}",
        DynamoEventKind.Refuel => $"R{e.Cell}:{e.Colour}",
        DynamoEventKind.Place => $"P{e.Cell}:{e.Colour}",
        DynamoEventKind.Ability => $"A{e.Slot}:{e.Pid}:{e.Use}",
        DynamoEventKind.Charge => $"C{e.Colour}",
        DynamoEventKind.Replaced => $"X{e.Colour}",
        DynamoEventKind.Pick => $"K{e.Slot}:{e.Pid}:{e.Cell}:{e.Colour}",
        DynamoEventKind.Retire => $"Q{e.Slot}:{e.Old}:{e.Pid}",
        DynamoEventKind.Shuffle => $"S{DynamoCore.AbilityKeys[(int)e.Ability]}:{e.PortSet}",
        DynamoEventKind.Ports => $"p{e.PortSet}",
        DynamoEventKind.Resume => "r",
        DynamoEventKind.Effect => $"F{EffectKeys[(int)e.Effect]}:{e.Count}",
        DynamoEventKind.Tick => $"t{e.Count}:{e.Ap}",
        DynamoEventKind.Remove => $"D{e.Cell}:{e.Colour}",
        DynamoEventKind.Offer => $"O{string.Join(",", e.Offers.Select(a => DynamoCore.AbilityKeys[(int)a]))}:{e.PortSet}",
        _ => "?",
    };

    static string InpStr(DynamoInput? i) => i == null ? "i-" : i.Value.Kind switch
    {
        DynamoInputKind.Tinker => $"itinker:{i.Value.From}>{i.Value.To}",
        DynamoInputKind.Refuel => $"irefuel:{i.Value.Cell}:{i.Value.Colour}",
        DynamoInputKind.Pick => $"ipick:{i.Value.Slot}",
        _ => $"iactivate:{i.Value.Slot}",
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
            string fixedAbility = j.GetProperty("fixedAbility").GetString();
            var s = new DynamoSettings
            {
                TickSeconds = j.GetProperty("tickSeconds").GetDouble(), RevealAbility = j.GetProperty("revealAbility").GetDouble(),
                RevealPorts = j.GetProperty("revealPorts").GetDouble(),
                ApPerTick = Int(j, "apPerTick"), ApBank = Int(j, "apBank"), BonusApCap = Int(j, "bonusApCap"), BatteryCap = Int(j, "batteryCap"),
                SlotCount = Int(j, "slotCount"), UsesPerPattern = Int(j, "usesPerPattern"), PatternsPerShuffle = Int(j, "patternsPerShuffle"),
                AllowRotatedMatches = j.GetProperty("allowRotatedMatches").GetBoolean(), PortsCount = j.GetProperty("portsCount").GetBoolean(),
                StartFilled = j.GetProperty("startFilled").GetBoolean(),
                FixedAbility = string.IsNullOrEmpty(fixedAbility) ? -1 : Array.IndexOf(DynamoCore.AbilityKeys, fixedAbility),
                FirstPortSet = Int(j, "firstPortSet"),
                AbilityChoices = Int(j, "abilityChoices"), PickSeconds = j.TryGetProperty("pickSeconds", out var pk) ? pk.GetDouble() : 0,
            };
            if (j.TryGetProperty("abilities", out var pool) && pool.ValueKind == JsonValueKind.Array)
                s.Abilities = pool.EnumerateArray().Select(a => Array.IndexOf(DynamoCore.AbilityKeys, a.GetString())).ToArray();
            foreach (var ps in j.GetProperty("portSets").EnumerateArray()) s.PortSets.Add(Pairs(ps));
            foreach (var p in j.GetProperty("patterns").EnumerateArray())
                s.Patterns.Add(new DynamoPattern { Cells = Pairs(p.GetProperty("cells")), Parked = p.GetProperty("parked").GetBoolean() });
            var core = new DynamoCore(s, scn.GetProperty("seed").GetInt32());
            var memo = new DynamoCore.AIMemo();
            if (!firstScn) sb.Append(',');
            firstScn = false;
            sb.Append("[{\"ok\":true,\"d\":\"").Append(core.Digest()).Append("\",\"e\":[]}");
            foreach (var st in scn.GetProperty("steps").EnumerateArray())
            {
                bool ok = true;
                string pre = null;
                if (st.TryGetProperty("dt", out var dt)) core.Advance(s, dt.GetDouble());
                else if (st.TryGetProperty("spend", out var sp)) ok = core.Spend(sp.GetInt32());
                else if (st.TryGetProperty("fx", out var fx)) ok = core.BoardEffect(s, (DynamoBoardEffect)Array.IndexOf(EffectKeys, fx.GetString()), Int(st, "arg")) >= 0;
                else if (st.TryGetProperty("ai", out var ai))
                {
                    var prm = new DynamoAISkill { Top = Int(ai, "top"), Blunder = ai.GetProperty("blunder").GetDouble() };
                    var inp = core.AIPlan(s, prm, DynamoCore.Mulberry32(st.GetProperty("seed").GetInt32()), memo);
                    if (inp != null && inp.Value.Kind == DynamoInputKind.Activate) memo.Key = "";
                    pre = InpStr(inp);
                    ok = inp != null && core.Apply(s, inp.Value);
                }
                else
                {
                    var i = st.GetProperty("inp");
                    var inp = new DynamoInput
                    {
                        Kind = Kinds[i.GetProperty("k").GetString()], From = Int(i, "from"), To = Int(i, "to"), Cell = Int(i, "cell"), Colour = Int(i, "colour"),
                        Slot = Int(i, "slot"),
                    };
                    ok = core.Apply(s, inp);
                }
                var evs = core.Events.Select(e => "\"" + Ev(e) + "\"").ToList();
                if (pre != null) evs.Insert(0, "\"" + pre + "\"");
                sb.Append(",{\"ok\":").Append(ok ? "true" : "false").Append(",\"d\":\"").Append(core.Digest()).Append("\",\"e\":[");
                sb.Append(string.Join(",", evs));
                sb.Append("],\"b\":\"").Append(string.Join("/", core.Built(s).Select(c => c == null ? "-" : string.Join(".", c)))).Append("\"}");
                core.Events.Clear();
            }
            sb.Append(']');
        }
        sb.Append("],\"portSets\":[");
        var engines = doc.RootElement.GetProperty("engines").EnumerateArray()
            .Select(e => DynamoCore.ParseEngine(e.EnumerateArray().Select(x => x.GetString()).ToList())).ToList();
        sb.Append(string.Join(",", DynamoCore.BuildPortSets(engines).Select(set => "[" + string.Join(",", set.Select(p => $"[{p[0]},{p[1]}]")) + "]")));
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
