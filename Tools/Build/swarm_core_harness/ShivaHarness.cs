// Tandava, redesigned as Shiva's Cell: the SHIPPED ShivaDirectorCore.cs (pure System.Numerics), run headless.
//   bash Tools/Build/swarm_core_harness/run.sh <plans> shiva       (run.sh writes the lab's parity fixture first)
//
// S1  parity: every circuit the lab lays (Docs/Studios/ShivaLab/sim.js layRings / danceAt) is the one the director lays
//     - six seeds, three forms, centres, normals and Nataraja's dance - to a fraction of a unit; and a planted change to
//     the C# copy (a wrong ring spacing) must FAIL it
// S2  a client's replay (RingsForForm from the seed alone) is the server's live circuit, form by form
// S3  the whole match, a perfect squad: thread -> shell cracks -> clap -> the wave leaves at the hands' meeting and runs
//     at WaveSpeed -> the cell regrows -> the next form plays; Nataraja's rings move and can still be threaded; the
//     mouth opens in the third form; the third shell is the win
// S4  the drum: no threading loses the first form at exactly its clock, and nothing happens after
// S5  TryThread refuses a miss (outside the mouth), a segment that never crosses the plane, and anything during the clap
// S6  the wave sweeps every distance from him to the membrane exactly once
// S7  a serpent: the guard rings spread along the circuit and move on two ahead; it lunges only past its cooldown, only
//     at a pilot in range and only while the drum runs; it rears, strikes, and stops at the pilot or out of reach
// S8  his figure turns with his yaw: a quarter turn carries the mouth from +z to +x
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Text.Json;
using CosmicShore.Gameplay;

static class ShivaHarness
{
    static int s_fail;
    static void Ok(bool cond, string what) { Console.WriteLine((cond ? "  [ok] " : "  [FAIL] ") + what); if (!cond) s_fail++; }

    public static int Run(string fixturePath)
    {
        Console.WriteLine("SHIVA - the shipped ShivaDirectorCore");
        var s = new ShivaDirectorSettings();
        S1(s, fixturePath);
        S2(s);
        S3(s);
        S4(s);
        S5(s);
        S6(s);
        S7(s);
        S8();
        Console.WriteLine(s_fail == 0 ? "shiva: OK" : $"shiva: {s_fail} FAILED");
        return s_fail == 0 ? 0 : 1;
    }

    static Vector3 V3(JsonElement a) => new(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle());

    // ── S1: the lab's circuits ──
    static void S1(ShivaDirectorSettings s, string path)
    {
        Console.WriteLine("S1 the circuit is the lab's");
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) { Ok(false, "no parity fixture (run.sh writes it with node)"); return; }
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var P = doc.RootElement.GetProperty("P");
        // the lab's SHIPPED keys, mapped onto the director's
        var lab = new ShivaDirectorSettings
        {
            CellRadius = P.GetProperty("cellRadius").GetSingle(), Mouth = P.GetProperty("mouth").GetSingle(),
            RingsStill = P.GetProperty("ringsP1").GetInt32(), RingsNataraja = P.GetProperty("ringsP2").GetInt32(), RingsMouth = P.GetProperty("ringsP3").GetInt32(),
            RingOuter = P.GetProperty("ringOuter").GetSingle(), RingStepDegrees = P.GetProperty("ringStepDeg").GetSingle(), RingRise = P.GetProperty("ringRise").GetSingle(),
            DanceSpeed = P.GetProperty("danceSpeed").GetSingle(), DanceBob = P.GetProperty("danceBob").GetSingle(), DanceSway = P.GetProperty("danceSway").GetSingle(),
            ShellOuter = P.GetProperty("shell1").GetSingle(), ShellMiddle = P.GetProperty("shell2").GetSingle(), ShellInner = P.GetProperty("shell3").GetSingle(),
            ClockStill = P.GetProperty("clockP1").GetSingle(), ClockNataraja = P.GetProperty("clockP2").GetSingle(), ClockMouth = P.GetProperty("clockP3").GetSingle(),
            WaveSpeed = P.GetProperty("waveSpeed").GetSingle(), ClapReach = P.GetProperty("clapReach").GetSingle(), RegrowSeconds = P.GetProperty("regrowSeconds").GetSingle(),
        };
        Ok(Same(lab, s), "the director's defaults are the lab's SHIPPED numbers (circuit, clocks, shells, clap)");
        float worst = Compare(lab, doc.RootElement, out int cases);
        Ok(cases == 18 && worst < 0.5f, $"{cases} circuits (6 seeds x 3 forms) and Nataraja's dance at 3 times match the lab: worst gap {worst:F4} u (< 0.5)");
        // negative control: a ring spacing a degree off must not pass
        var bent = Clone(lab); bent.RingStepDegrees += 1f;
        float off = Compare(bent, doc.RootElement, out _);
        Ok(off > 5f, $"negative control: a 1 degree spacing change is caught ({off:F1} u off)");
    }

    static float Compare(ShivaDirectorSettings s, JsonElement root, out int cases)
    {
        float worst = 0f; cases = 0;
        foreach (var c in root.GetProperty("cases").EnumerateArray())
        {
            uint seed = (uint)c.GetProperty("seed").GetInt64();
            int form = c.GetProperty("form").GetInt32();
            var rings = ShivaDirectorCore.RingsForForm(s, seed, form);
            var centres = c.GetProperty("centres"); var normals = c.GetProperty("normals");
            if (rings.Length != centres.GetArrayLength()) return 1e9f;
            for (int i = 0; i < rings.Length; i++)
            {
                worst = MathF.Max(worst, (rings[i].Centre - V3(centres[i])).Length());
                worst = MathF.Max(worst, 100f * (rings[i].Normal - V3(normals[i])).Length());   // a normal 0.005 off = 0.5 u at the mouth's scale
            }
            var dance = c.GetProperty("dance"); var times = new[] { 0f, 10f, 37.5f };
            for (int k = 0; k < dance.GetArrayLength(); k++)
                for (int i = 0; i < rings.Length; i++)
                    worst = MathF.Max(worst, (ShivaDirectorCore.DanceAt(s, rings[i], times[k]) - V3(dance[k][i])).Length());
            cases++;
        }
        return worst;
    }

    static bool Same(ShivaDirectorSettings a, ShivaDirectorSettings b) =>
        a.CellRadius == b.CellRadius && a.Mouth == b.Mouth && a.RingsStill == b.RingsStill && a.RingsNataraja == b.RingsNataraja &&
        a.RingsMouth == b.RingsMouth && a.RingOuter == b.RingOuter && a.RingStepDegrees == b.RingStepDegrees && a.RingRise == b.RingRise &&
        a.DanceSpeed == b.DanceSpeed && a.DanceBob == b.DanceBob && a.DanceSway == b.DanceSway && a.ShellOuter == b.ShellOuter &&
        a.ShellMiddle == b.ShellMiddle && a.ShellInner == b.ShellInner && a.ClockStill == b.ClockStill && a.ClockNataraja == b.ClockNataraja &&
        a.ClockMouth == b.ClockMouth && a.WaveSpeed == b.WaveSpeed && a.ClapReach == b.ClapReach && a.RegrowSeconds == b.RegrowSeconds;

    static ShivaDirectorSettings Clone(ShivaDirectorSettings s) => (ShivaDirectorSettings)typeof(object)
        .GetMethod("MemberwiseClone", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(s, null);

    // thread the lit ring with a short segment along its axis through the centre
    static bool ThreadLit(ShivaDirectorCore d, int pilot = 0)
    {
        if (d.RingIndex >= d.Rings.Length) return false;
        var g = d.Rings[d.RingIndex];
        return d.TryThread(pilot, g.Centre - g.Normal * 5f, g.Centre + g.Normal * 5f);
    }

    static void RunFor(ShivaDirectorCore d, float seconds, List<ShivaEvent> log, float dt = 1f / 30f)
    {
        for (float t = 0f; t < seconds; t += dt) { d.Step(dt, new Vector3(0, 0, 1000)); log.AddRange(d.Events); d.Events.Clear(); }
    }

    // ── S2: the client's replay ──
    static void S2(ShivaDirectorSettings s)
    {
        Console.WriteLine("S2 a client builds the server's circuit from the seed");
        bool same = true;
        foreach (uint seed in new uint[] { 1, 9, 77, 31337 })
        {
            var d = new ShivaDirectorCore(s, seed, new Vector3(0, 0, 1000));
            var log = new List<ShivaEvent>();
            for (int form = 0; form < 3; form++)
            {
                var replay = ShivaDirectorCore.RingsForForm(s, seed, form);
                // the circuit as laid (Nataraja's rings dance off their base once the drum runs)
                for (int i = 0; i < replay.Length; i++)
                    same &= replay.Length == d.Rings.Length && MathF.Abs(replay[i].BaseRadius - d.Rings[i].BaseRadius) < 1e-3f &&
                            MathF.Abs(replay[i].BaseTheta - d.Rings[i].BaseTheta) < 1e-5f && MathF.Abs(replay[i].BaseY - d.Rings[i].BaseY) < 1e-3f &&
                            replay[i].BobPhase == d.Rings[i].BobPhase;
                while (d.State == ShivaState.Play && d.Form == form) ThreadLit(d);
                RunFor(d, 6f, log);
            }
        }
        Ok(same, "RingsForForm(seed, form) is the director's circuit for every form (4 seeds)");
    }

    // ── S3: a perfect squad ──
    static void S3(ShivaDirectorSettings s)
    {
        Console.WriteLine("S3 the whole match");
        var d = new ShivaDirectorCore(s, 5, new Vector3(0, 0, 1000));
        var log = new List<ShivaEvent>();
        Ok(d.Rings.Length == s.RingsStill && d.StandingShell == 0, $"the Still Shiva: {d.Rings.Length} rings, the outer shell standing");
        Ok(MathF.Abs(d.Rings[^1].BaseRadius - (s.ShellOuter + s.RingInset)) < 0.5f &&
           Vector3.Dot(d.Rings[^1].Normal, -Vector3.Normalize(d.Rings[^1].Centre)) > 0.999f, "the last ring sits just outside the shell, facing him");
        bool spiral = true; for (int i = 1; i < d.Rings.Length; i++) spiral &= d.Rings[i].Centre.Length() <= d.Rings[i - 1].Centre.Length() + 1f;
        Ok(spiral, "the circuit spirals in");
        RunFor(d, 3f, log);
        while (d.State == ShivaState.Play) ThreadLit(d);
        Ok(d.State == ShivaState.Clap && !d.ShellUp[0] && log.Count == 0, "the last ring cracks the outer shell and he claps");
        log.AddRange(d.Events); d.Events.Clear();
        Ok(log.FindIndex(e => e.Kind == ShivaEventKind.ShellCracked && e.A == 0) >= 0 && log.FindIndex(e => e.Kind == ShivaEventKind.Clap) >= 0, "ShellCracked(0) and Clap");
        log.Clear();
        float waveAt = -1f, regrowAt = -1f, playAt = -1f, t = 0f, r0 = 0f;
        for (; t < 10f; t += 1f / 30f)
        {
            d.Step(1f / 30f, Vector3.UnitZ * 1000f);
            foreach (var e in d.Events)
            {
                if (e.Kind == ShivaEventKind.Wave) { waveAt = t; r0 = d.WaveRadius; }
                if (e.Kind == ShivaEventKind.Regrow) regrowAt = t;
                if (e.Kind == ShivaEventKind.Play) playAt = t;
            }
            d.Events.Clear();
            if (playAt >= 0f) break;
        }
        float expectRegrow = s.HandsMeetSeconds + (s.CellRadius + 40f - s.WaveStartRadius) / s.WaveSpeed;
        Ok(MathF.Abs(waveAt - s.HandsMeetSeconds) < 0.05f, $"the wave leaves as the hands meet ({waveAt:F2} s; {s.HandsMeetSeconds} s)");
        Ok(MathF.Abs(regrowAt - expectRegrow) < 0.1f, $"it reaches the membrane at WaveSpeed: the cell regrows at {regrowAt:F2} s ({expectRegrow:F2} s)");
        Ok(MathF.Abs(playAt - regrowAt - s.RegrowSeconds * 0.5f) < 0.1f && d.Form == 1 && d.Clock == s.ClockNataraja, $"Nataraja plays {playAt - regrowAt:F2} s later with a fresh drum ({d.Clock} s)");
        var before = d.Rings[3].Centre;
        RunFor(d, 4f, log);
        float moved = (d.Rings[3].Centre - before).Length();
        Ok(moved > 20f, $"Nataraja's rings dance: ring 4 moved {moved:F0} u in 4 s");
        int threaded = 0;
        while (d.State == ShivaState.Play && d.Form == 1) { d.Step(1f / 30f, Vector3.UnitZ * 1000f); if (ThreadLit(d)) threaded++; }
        Ok(threaded == s.RingsNataraja && !d.ShellUp[1], $"a moving circuit is threadable: {threaded} of {s.RingsNataraja}");
        RunFor(d, 6f, log);
        Ok(d.Form == 2 && d.State == ShivaState.Play, "the Devouring Mouth plays");
        RunFor(d, 6f, log);
        Ok(d.MouthOpen > 0.95f, $"his jaw opens ({d.MouthOpen:F2} after 6 s)");
        while (d.State == ShivaState.Play) ThreadLit(d);
        log.AddRange(d.Events); d.Events.Clear();
        Ok(d.Outcome == ShivaOutcome.Won && d.StandingShell < 0 && log.Exists(e => e.Kind == ShivaEventKind.Won), "the third shell breaks: Won, no shell left");
        Ok(d.FormSeconds[0] > 2.9f && d.FormSeconds[2] > 5.9f, $"each form's time is kept ({d.FormSeconds[0]:F1} / {d.FormSeconds[1]:F1} / {d.FormSeconds[2]:F1} s)");
    }

    // ── S4: the drum ──
    static void S4(ShivaDirectorSettings s)
    {
        Console.WriteLine("S4 the drum");
        var d = new ShivaDirectorCore(s, 3, new Vector3(0, 0, 1000));
        var log = new List<ShivaEvent>();
        float t = 0f, lostAt = -1f;
        for (; t < s.ClockStill + 5f; t += 1f / 30f) { d.Step(1f / 30f, Vector3.UnitZ * 1000f); if (lostAt < 0f && d.Outcome == ShivaOutcome.Lost) lostAt = t + 1f / 30f; log.AddRange(d.Events); d.Events.Clear(); }
        Ok(d.Outcome == ShivaOutcome.Lost && MathF.Abs(lostAt - s.ClockStill) < 0.05f, $"the first form is lost at its clock ({lostAt:F2} s; {s.ClockStill} s)");
        Ok(log.Count == 1 && log[0].Kind == ShivaEventKind.Lost, "and nothing happens after (one Lost event)");
        Ok(!ThreadLit(d), "a ring threaded after the loss counts for nothing");
    }

    // ── S5: what TryThread refuses ──
    static void S5(ShivaDirectorSettings s)
    {
        Console.WriteLine("S5 a miss is a miss");
        var d = new ShivaDirectorCore(s, 11, new Vector3(0, 0, 1000));
        var g = d.Rings[0];
        var side = Vector3.Normalize(Vector3.Cross(g.Normal, Vector3.UnitY));
        Ok(!d.TryThread(0, g.Centre + side * (s.Mouth + 2f) - g.Normal * 5f, g.Centre + side * (s.Mouth + 2f) + g.Normal * 5f), "outside the mouth: no");
        Ok(!d.TryThread(0, g.Centre - g.Normal * 20f, g.Centre - g.Normal * 5f), "never crossing the plane: no");
        Ok(d.TryThread(0, g.Centre + side * (s.Mouth * 1.2f) - g.Normal * 5f, g.Centre + side * (s.Mouth * 1.2f) + g.Normal * 5f, 1.3f), "a pilot whose mouth is widened (Space) threads it at 1.2 mouths");
        while (d.State == ShivaState.Play) ThreadLit(d);
        Ok(d.State == ShivaState.Clap && !ThreadLit(d), "during the clap: no");
    }

    // ── S6: the wave sweeps everything once ──
    static void S6(ShivaDirectorSettings s)
    {
        Console.WriteLine("S6 the wave");
        var d = new ShivaDirectorCore(s, 2, new Vector3(0, 0, 1000));
        while (d.State == ShivaState.Play) ThreadLit(d);
        var dist = new float[64]; var hits = new int[64];
        for (int i = 0; i < dist.Length; i++) dist[i] = s.WaveStartRadius + (s.CellRadius - s.WaveStartRadius) * i / (dist.Length - 1f);
        while (d.State == ShivaState.Clap || d.State == ShivaState.Wave)
        {
            d.Step(1f / 30f, Vector3.UnitZ * 1000f); d.Events.Clear();
            for (int i = 0; i < dist.Length; i++) if (d.WaveSwept(dist[i])) hits[i]++;
        }
        bool once = true; foreach (var h in hits) once &= h == 1;
        Ok(once, $"every one of {dist.Length} distances from {s.WaveStartRadius} u to the membrane is swept exactly once");
    }

    // ── S7: a serpent ──
    static void S7(ShivaDirectorSettings s)
    {
        Console.WriteLine("S7 a serpent");
        var ss = new ShivaSerpentSettings { LungeRange = 380f, LungeCooldown = 6f, RearSeconds = 0.6f, StrikeSeconds = 2.5f, GuardOrbit = 1.4f, Mouth = s.Mouth };
        var rings = ShivaDirectorCore.RingsForForm(s, 4, 0);
        var far = new[] { new Vector3(5000, 0, 0) }; var still = new[] { Vector3.Zero };
        var a = new ShivaSerpentBrain(0f, 2f); var b = new ShivaSerpentBrain(0f, 2f);
        a.Step(0.1f, ss, Vector3.Zero, rings, 0, 0, 2, far, still, true);
        b.Step(0.1f, ss, Vector3.Zero, rings, 0, 1, 2, far, still, true);
        Ok(a.GuardRing > 0 && b.GuardRing > a.GuardRing, $"two serpents hold rings spread along the circuit ({a.GuardRing}, {b.GuardRing} of {rings.Length})");
        float dGuard = (a.Goal - rings[a.GuardRing].Centre).Length();
        Ok(MathF.Abs(dGuard - s.Mouth * 1.4f) < 1f, $"it coils round its ring's mouth ({dGuard:F0} u from the centre)");
        int held = a.GuardRing;
        a.Step(0.1f, ss, Vector3.Zero, rings, held + 1, 0, 2, far, still, true);
        Ok(a.GuardRing == Math.Min(rings.Length - 1, held + 3), $"passed, it moves on two ahead of the lit ring ({held} -> {a.GuardRing})");
        // a pilot near, before the cooldown is spent: no lunge; after: a lunge
        var at = rings[a.GuardRing].Centre; var pilot = new[] { at + new Vector3(200, 0, 0) };
        var c = new ShivaSerpentBrain(0f, 2f);
        c.Step(1f, ss, at, rings, 0, 0, 1, pilot, still, true);
        Ok(c.Pose == ShivaSerpentPose.Guard, "a pilot in range inside the cooldown: it holds");
        c.Step(1.1f, ss, at, rings, 0, 0, 1, pilot, still, false);
        Ok(c.Pose == ShivaSerpentPose.Guard, "while he claps: it holds");
        c.Step(0.1f, ss, at, rings, 0, 0, 1, pilot, still, true);
        Ok(c.Pose == ShivaSerpentPose.Rear && c.Target == 0, "past the cooldown, in range, the drum running: it rears at the pilot");
        c.Step(0.7f, ss, at, rings, 0, 0, 1, pilot, still, true);
        Ok(c.Pose == ShivaSerpentPose.Strike && (c.Goal - pilot[0]).Length() < 1f, "after RearSeconds it strikes at the pilot");
        c.Step(0.1f, ss, pilot[0] + new Vector3(10, 0, 0), rings, 0, 0, 1, pilot, still, true);
        Ok(c.Pose == ShivaSerpentPose.Guard, "on the pilot: the strike ends and it cools down");
        c.Step(1f, ss, at, rings, 0, 0, 1, pilot, still, true);
        Ok(c.Pose == ShivaSerpentPose.Guard, "and it does not lunge again inside its cooldown");
        var e = new ShivaSerpentBrain(0f, 0f);
        e.Step(0.1f, ss, at, rings, 0, 0, 1, new[] { at + new Vector3(600, 0, 0) }, still, true);
        Ok(e.Pose == ShivaSerpentPose.Guard, "a pilot out of range draws no lunge");
    }

    // ── S8: his figure turns ──
    static void S8()
    {
        Console.WriteLine("S8 he turns to face the squad");
        var j0 = ShivaDirectorCore.Figure(0, 0f, 0f, 0f, 0f);
        var j1 = ShivaDirectorCore.Figure(0, 0f, 0f, 0f, MathF.PI / 2f);
        Ok(j0.Mouth.Z > 70f && MathF.Abs(j0.Mouth.X) < 1e-3f && j1.Mouth.X > 70f && MathF.Abs(j1.Mouth.Z) < 1e-2f, $"the mouth moves from +z ({j0.Mouth.Z:F0}) to +x ({j1.Mouth.X:F0})");
        var d = new ShivaDirectorCore(new ShivaDirectorSettings(), 1, new Vector3(1000, 0, 0));
        Ok(MathF.Abs(d.Yaw - MathF.PI / 2f) < 1e-3f, "he begins facing the squad");
        var clap = ShivaDirectorCore.Figure(0, 0.5f, 0f, 0f, 0f);
        Ok((clap.HandLL - clap.HandLR).Length() < 20f, $"mid-clap his front hands meet ({(clap.HandLL - clap.HandLR).Length():F0} u apart)");
    }
}
