// Runs the shipped OmniCombineCore against the Omni Combine Lab's own poses (lab_poses.cjs) on the
// lab-frame export of the same data (author_omni_combine_asset.py --frame lab), and fails on any vertex
// that disagrees by more than TOL. Usage: dotnet run -- <lab-frame data.json> <lab poses.json>
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using CosmicShore.Utility;

static class Driver
{
    const double TOL = 2e-4;   // float storage of a double-precision pose; the art units are ~1

    static int Main(string[] args)
    {
        var opts = new JsonSerializerOptions { IncludeFields = true };
        var data = JsonSerializer.Deserialize<OmniCombineData>(File.ReadAllText(args[0]), opts);
        using var lab = JsonDocument.Parse(File.ReadAllText(args[1]));
        var root = lab.RootElement;
        var core = new OmniCombineCore(data);
        var p = OmniCombineParams.LabDefault;
        var cam = root.GetProperty("camDir");
        var camDir = new D3(cam[0].GetDouble(), cam[1].GetDouble(), cam[2].GetDouble());
        int fails = 0;
        double worst = 0;

        double me = OmniCombineCore.MorphEnds(p), le = root.GetProperty("morphEnds").GetDouble();
        if (Math.Abs(me - le) > 1e-9) { Console.WriteLine($"FAIL morphEnds {me} vs lab {le}"); fails++; }

        foreach (var rig in root.GetProperty("rigs").EnumerateArray()) { }
        for (int o = 0; o < 2; o++)
        {
            int n = core.StartVertexCount(o == 1);
            var sizes = string.Join("/", core.RingSizes(o == 1, 0));
            Console.WriteLine($"{(o == 1 ? "omni" : "time")} flip rig: {n} start vertices, rings {sizes}");
            if (n != 12 || sizes != "5/5/10/5/5") { Console.WriteLine("FAIL flip rig"); fails++; }
        }

        var pos = new float[4][];
        for (int k = 0; k < 4; k++) pos[k] = new float[3 * core.VertexCount(k)];
        var fx = new float[4];
        foreach (var pose in root.GetProperty("poses").EnumerateArray())
        {
            double t = pose.GetProperty("t").GetDouble();
            core.Pose(t, p, camDir, pos, fx);
            for (int k = 0; k < 4; k++)
            {
                var want = pose.GetProperty("crystals").GetProperty(OmniCombineCore.Order[k]);
                double w = 0;
                int i = 0;
                foreach (var x in want.EnumerateArray()) w = Math.Max(w, Math.Abs(x.GetDouble() - pos[k][i++]));
                if (i != pos[k].Length) { Console.WriteLine($"FAIL t={t} {OmniCombineCore.Order[k]}: {i} vs {pos[k].Length} floats"); fails++; }
                worst = Math.Max(worst, w);
                if (w > TOL) { Console.WriteLine($"FAIL t={t} {OmniCombineCore.Order[k]} off by {w:E2}"); fails++; }
            }
        }
        var omni = new float[data.omni.verts.Length];
        foreach (var e in root.GetProperty("omni").EnumerateArray())
        {
            double t = e.GetProperty("t").GetDouble(), f = e.GetProperty("fx").GetDouble();
            core.OmniPose(t, p, f, camDir, omni);
            double w = 0;
            int i = 0;
            foreach (var x in e.GetProperty("pos").EnumerateArray()) w = Math.Max(w, Math.Abs(x.GetDouble() - omni[i++]));
            worst = Math.Max(worst, w);
            if (w > TOL) { Console.WriteLine($"FAIL omni t={t} fx={f} off by {w:E2}"); fails++; }
        }
        Console.WriteLine(fails == 0 ? $"PASS: OmniCombineCore matches the lab (worst {worst:E2})" : $"{fails} FAILURES (worst {worst:E2})");

        // The SHIPPED asset (Unity's axes, a reflection of the lab's): every crystal must still land on its
        // target and on the omni - which is what proves the generator's axis and quaternion conversion.
        if (args.Length > 2) fails += Shipped(JsonSerializer.Deserialize<OmniCombineData>(File.ReadAllText(args[2]), opts));
        return fails == 0 ? 0 : 1;
    }

    static int Shipped(OmniCombineData data)
    {
        int fails = 0;
        var core = new OmniCombineCore(data);
        var p = OmniCombineParams.LabDefault;
        var cam = new D3(0.35, 0.42, 0.837).Unit;
        var pos = new float[4][];
        for (int k = 0; k < 4; k++) pos[k] = new float[3 * core.VertexCount(k)];
        var fx = new float[4];
        core.Pose(OmniCombineCore.MorphEnds(p), p, cam, pos, fx);
        var omni = data.omni.verts;
        for (int k = 0; k < 4; k++)
        {
            var final = data.crystals[k].final;
            double toFinal = 0, toOmni = 0;
            for (int i = 0; i < pos[k].Length / 3; i++)
            {
                double best = double.MaxValue;
                for (int a = 0; a < 3; a++) toFinal = Math.Max(toFinal, Math.Abs(pos[k][3 * i + a] - final[3 * i + a]));
                for (int j = 0; j < omni.Length; j += 3)
                {
                    double dx = pos[k][3 * i] - omni[j], dy = pos[k][3 * i + 1] - omni[j + 1], dz = pos[k][3 * i + 2] - omni[j + 2];
                    best = Math.Min(best, Math.Sqrt(dx * dx + dy * dy + dz * dz));
                }
                toOmni = Math.Max(toOmni, best);
            }
            // Space and Time land on omni VERTICES; Charge's and Mass's wedges put their tips at a face's centre and
            // their backs along its sides (the generator proves those tile each face, in its plane).
            bool wedge = k == OmniCombineCore.Charge || k == OmniCombineCore.Mass;
            bool ok = toFinal < 1e-4 && (wedge || toOmni < 0.012);
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")} shipped {OmniCombineCore.Order[k]} lands: {toFinal:E1} off its target" +
                              (wedge ? " (a wedge: on the omni's faces)" : $", {toOmni:E1} off the omni"));
            if (!ok) fails++;
        }
        // Waiting never sets off; CONVERT after a long wait is the same frame (the press only moves the lead).
        var wait = p; wait.Lead = 1e9;
        var go = wait; go.Lead = 300.3;
        var a1 = new float[4][]; var b1 = new float[4][];
        for (int k = 0; k < 4; k++) { a1[k] = new float[pos[k].Length]; b1[k] = new float[pos[k].Length]; }
        core.Pose(300.0, wait, cam, a1, fx);
        core.Pose(300.0, go, cam, b1, fx);
        double jump = 0;
        for (int k = 0; k < 4; k++) for (int i = 0; i < a1[k].Length; i++) jump = Math.Max(jump, Math.Abs(a1[k][i] - b1[k][i]));
        Console.WriteLine($"{(jump < 1e-4 ? "PASS" : "FAIL")} CONVERT after a 300 s wait jumps {jump:E1}");
        if (jump >= 1e-4) fails++;
        // After a long wait Charge's flight must not spin it back for laps: no vertex moves more than 0.08 omni
        // radii in a 60 Hz frame (the lab's own flight peaks near 0.03; unwinding 1000 s of tumble would be ~1).
        var prev = new float[pos[1].Length];
        var cur = new float[4][];
        for (int k = 0; k < 4; k++) cur[k] = new float[pos[k].Length];
        var fly = wait; fly.Lead = 1000.0;
        double t0 = OmniCombineCore.Departs(OmniCombineCore.Charge, fly), step = 0;
        core.Pose(t0, fly, cam, cur, fx);
        Array.Copy(cur[1], prev, prev.Length);
        for (double t = t0 + 1.0 / 60; t <= t0 + fly.Flight; t += 1.0 / 60)
        {
            core.Pose(t, fly, cam, cur, fx);
            for (int i = 0; i < prev.Length; i += 3)
            {
                double dx = cur[1][i] - prev[i], dy = cur[1][i + 1] - prev[i + 1], dz = cur[1][i + 2] - prev[i + 2];
                step = Math.Max(step, Math.Sqrt(dx * dx + dy * dy + dz * dz));
            }
            Array.Copy(cur[1], prev, prev.Length);
        }
        // The converter hands the Mass depth effect to the game's shader: the CPU pose must then ignore the camera,
        // and at landing every crystal's centre is the omni's and every plate has fully morphed.
        var sd = OmniCombineParams.LabDefault; sd.ShaderDepth = true;
        var m1 = new float[4][]; var m2 = new float[4][]; var morph = new float[4][];
        for (int k = 0; k < 4; k++) { m1[k] = new float[pos[k].Length]; m2[k] = new float[pos[k].Length]; morph[k] = new float[data.crystals[k].plate.Max() + 1]; }
        core.Pose(1.0, sd, cam, m1, fx);
        core.Pose(1.0, sd, new D3(-0.6, 0.1, -0.79).Unit, m2, fx);
        double camGap = 0; for (int i = 0; i < m1[2].Length; i++) camGap = Math.Max(camGap, Math.Abs(m1[2][i] - m2[2][i]));
        Console.WriteLine($"{(camGap == 0 ? "PASS" : "FAIL")} with ShaderDepth the Mass pose ignores the camera ({camGap:E1})");
        if (camGap != 0) fails++;
        var centres = new float[12];
        core.Pose(OmniCombineCore.MorphEnds(sd), sd, cam, m1, fx, centres, morph);
        double off = centres.Max(Math.Abs), least = morph.Min(a => a.Min());
        Console.WriteLine($"{(off < 1e-6 && least == 1 ? "PASS" : "FAIL")} at landing every centre is the omni's ({off:E1}) and every plate has morphed ({least})");
        if (!(off < 1e-6 && least == 1)) fails++;
        // The converter turns the Charge crystal's idle turn off: waiting, it must not move at all.
        var still = wait; still.IdleTumble = false;
        var s0 = new float[4][]; var s1 = new float[4][];
        for (int k = 0; k < 4; k++) { s0[k] = new float[pos[k].Length]; s1[k] = new float[pos[k].Length]; }
        core.Pose(0.0, still, cam, s0, fx);
        core.Pose(37.0, still, cam, s1, fx);
        double drift = 0; for (int i = 0; i < s0[1].Length; i++) drift = Math.Max(drift, Math.Abs(s0[1][i] - s1[1][i]));
        Console.WriteLine($"{(drift == 0 ? "PASS" : "FAIL")} without IdleTumble the waiting Charge crystal holds still ({drift:E1})");
        if (drift != 0) fails++;
        bool calm = step < 0.08 * core.OmniRadius;
        Console.WriteLine($"{(calm ? "PASS" : "FAIL")} Charge after a 1000 s wait moves at most {step / core.OmniRadius:F3} omni radii a frame");
        if (!calm) fails++;
        return fails;
    }
}
