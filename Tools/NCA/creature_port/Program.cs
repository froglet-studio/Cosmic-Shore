// Replays Python shell cases (creature_port_check.py) through CreatureShell and reports the worst difference.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

static class Program
{
    static Vector3 V(JsonElement a) => new Vector3(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());

    static int Main(string[] args)
    {
        var doc = JsonDocument.Parse(File.ReadAllText(args[0]));
        var cases = doc.RootElement.GetProperty("cases");
        double worstPos = 0, worstSt = 0, worstThr = 0, sumPos = 0; int nCase = 0; long ticks = 0; int ntadAvg = 0;
        foreach (var cs in cases.EnumerateArray())
        {
            var pos = cs.GetProperty("pos").EnumerateArray().Select(V).ToArray();
            int n = pos.Length;
            var elem = cs.GetProperty("elem").EnumerateArray().Select(x => x.GetInt32()).ToArray();
            var act = cs.GetProperty("active").EnumerateArray().Select(x => x.GetBoolean()).ToArray();
            var hat = cs.GetProperty("hatched").EnumerateArray().Select(x => x.GetBoolean()).ToArray();
            var sh = new CreatureShell(n, new ShellCfg { TellJitter = 0f });
            var off = cs.GetProperty("off").EnumerateArray().Select(V).ToArray();
            var st = cs.GetProperty("startle").EnumerateArray().Select(x => x.GetSingle()).ToArray();
            Array.Copy(off, sh.Off, n); Array.Copy(st, sh.Startle, n);
            sh.Threat = cs.GetProperty("threat").GetSingle(); sh.Swell = cs.GetProperty("swell").GetSingle();
            sh.TellLeft = cs.GetProperty("tell").GetSingle(); sh.Maj = cs.GetProperty("maj").GetInt32(); sh.T = cs.GetProperty("t").GetInt32();
            var vs = new List<Vessel>();
            foreach (var v in cs.GetProperty("vessels").EnumerateArray())
                vs.Add(new Vessel { C = new Vector3(v[0].GetSingle(), v[1].GetSingle(), v[2].GetSingle()), R = v[3].GetSingle(),
                                    V = new Vector3(v[4].GetSingle(), v[5].GetSingle(), v[6].GetSingle()) });
            var sw = Stopwatch.StartNew();
            sh.Step(pos, elem, act, hat, vs);
            ticks += sw.ElapsedTicks;
            var opos = cs.GetProperty("out_pos").EnumerateArray().Select(V).ToArray();
            var ost = cs.GetProperty("out_startle").EnumerateArray().Select(x => x.GetSingle()).ToArray();
            for (int i = 0; i < n; i++)
            {
                if (!act[i]) continue;
                double dp = (pos[i] - opos[i]).Length(); worstPos = Math.Max(worstPos, dp); sumPos += dp;
                worstSt = Math.Max(worstSt, Math.Abs(sh.Startle[i] - ost[i]));
            }
            worstThr = Math.Max(worstThr, Math.Abs(sh.Threat - cs.GetProperty("out_threat").GetSingle()));
            nCase++; ntadAvg += act.Count(x => x);
        }
        double ms = ticks * 1000.0 / Stopwatch.Frequency / nCase;
        Console.WriteLine(JsonSerializer.Serialize(new { cases = nCase, worst_pos = worstPos, mean_pos_err = sumPos / Math.Max(1, ntadAvg),
            worst_startle = worstSt, worst_threat = worstThr, ms_per_step = ms, mean_active = ntadAvg / (double)nCase }));
        return 0;
    }
}
