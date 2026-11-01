// TraceDriver.cs - replays the hive scenarios parity.cjs built in the Ratel Raid Lab through the SHIPPED HiveCore.cs and
// prints the same strings the page recorded (Tools/Build/hive_lab/parity.cjs compares them). Not part of the game.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using CosmicShore.Gameplay;

static class HiveTraceDriver
{
    static int Int(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;
    static double Dbl(JsonElement o, string k) => o.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
    static string J(IEnumerable<int> a) => string.Join(",", a);
    static string Sol(List<int[]> s) => s == null ? "null" : string.Join(";", s.Select(p => J(p)));
    static string Q(string s) => JsonSerializer.Serialize(s);

    static HiveSettings Settings(JsonElement j) => new()
    {
        MinChain = Int(j, "minChain"), SnakeMin = Int(j, "snakeMin"), SnakeMax = Int(j, "snakeMax"), HiveHoles = Int(j, "hiveHoles"),
        HiveDangerMin = Int(j, "hiveDangerMin"), RewardDynamo = Int(j, "rewardDynamo"), RewardBattery = Int(j, "rewardBattery"),
        HiveOtherShare = Dbl(j, "hiveOtherShare"), HiveCross = Dbl(j, "hiveCross"), HiveArmour = Dbl(j, "hiveArmour"),
        StingSeconds = Dbl(j, "stingSeconds"), RaidSeconds = Dbl(j, "raidSeconds"),
    };

    static HiveCell[] Cells(JsonElement a) => a.EnumerateArray().Select(x => new HiveCell { Dom = x[0].GetInt32(), Hits = x[1].GetInt32(), Danger = x[2].GetInt32() != 0 }).ToArray();

    public static int Main(string[] args)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(args[0]));
        var root = doc.RootElement;
        var sb = new StringBuilder("{\"games\":[");
        bool firstG = true;
        foreach (var g in root.GetProperty("games").EnumerateArray())
        {
            var p = Settings(g.GetProperty("P"));
            int own = Int(g, "own");
            var rows = new List<string>();
            var hive = HiveCore.MakeHive(DynamoCore.Mulberry32(Int(g, "seed")), p, own);
            rows.Add(hive == null ? "null" : HiveCore.Digest(hive) + "#" + Sol(hive.Solution) + "#" + hive.Regenerated + "#" + J(hive.Total));
            var ai = new HiveCore.AIPlan();
            double t = 0;
            if (hive != null)
                foreach (var op in g.GetProperty("ops").EnumerateArray())
                {
                    string k = op.GetProperty("op").GetString(), r;
                    switch (k)
                    {
                        case "start": r = HiveCore.ChainStart(hive, Int(op, "cell"), p, own).ToString(); break;
                        case "add": r = HiveCore.ChainAdd(hive, Int(op, "cell"), p).ToString(); break;
                        case "end":
                        {
                            var res = HiveCore.ChainEnd(hive, p, own);
                            r = res == null ? "null" : res.Colour + ":" + res.Broken + ":" + (res.Completed ? 1 : 0) + ":" + string.Join(",", res.Steps.Select(s => s[0] + "/" + s[1]));
                            break;
                        }
                        case "tick": { double dt = Dbl(op, "dt"); t += dt; HiveCore.Tick(hive, dt); r = HiveCore.EndReason(hive, t, p, own); break; }
                        case "ai":
                        {
                            var path = HiveCore.AINextChain(hive, ai, op.GetProperty("solver").GetBoolean(), Dbl(op, "sloppy"), DynamoCore.Mulberry32(Int(op, "seed")), p, own);
                            r = path == null ? "null" : J(path);
                            break;
                        }
                        default: r = "?"; break;
                    }
                    rows.Add(r + "|" + HiveCore.Digest(hive));
                }
            if (!firstG) sb.Append(',');
            firstG = false;
            sb.Append('[').Append(string.Join(",", rows.Select(Q))).Append(']');
        }
        sb.Append("],\"boards\":[");
        bool firstB = true;
        var landed = new List<HiveCore.Landed>();
        foreach (var b in root.GetProperty("boards").EnumerateArray())
        {
            var cells = b.GetProperty("cells").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var lc = HiveCore.LargestCrystal(cells);
            var rewards = b.GetProperty("rewards").EnumerateArray().Select(x => x.GetInt32()).ToList();
            var after = HiveCore.PlaceRewards(cells, rewards, Int(b, "per"), landed);
            var next = HiveCore.CrystalNext(cells, after);
            string s = J(lc.Perm) + "/" + J(lc.Cells) + "/" + lc.Size + "#" + string.Join(",", landed.Select(x => x.Cell + ":" + x.Colour + ":" + x.Tier)) + "#" + J(cells) + "#" +
                       J(after.Perm) + "/" + J(after.Cells) + "/" + after.Size + "#" + string.Join(",", next.Select(x => x[0] + ":" + x[1]));
            if (!firstB) sb.Append(',');
            firstB = false;
            sb.Append(Q(s));
        }
        sb.Append("],\"solves\":[");
        bool firstS = true;
        foreach (var c in root.GetProperty("solves").EnumerateArray())
        {
            var cells = Cells(c.GetProperty("cells"));
            int own = Int(c, "own"), min = Int(c, "minChain");
            string s = Sol(HiveCore.SolveHive(cells, min, own)) + "#" + (HiveCore.AnyChain(cells, min, own) ? 1 : 0) + "#" +
                       string.Join(";", HiveCore.ChainsNow(cells, min, 6, own).Take(40).Select(p => J(p)));
            if (!firstS) sb.Append(',');
            firstS = false;
            sb.Append(Q(s));
        }
        sb.Append("]}");
        Console.WriteLine(sb.ToString());
        return 0;
    }
}
