// TraceDriver.cs - runs the SHIPPED OrcaCrewCore (Assets/_Scripts/Controller/Vessel/Crew/OrcaCrewCore.cs) over the scripted
// scenarios parity.cjs also runs through the Crew of Four Lab page, and prints the trace as JSON. Every input is a formula
// of the step index, so both sides compute the same script with no shared data file. Vectors are Unity's (left-handed);
// parity.cjs mirrors x to compare them with the page's right-handed three.js frame.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CosmicShore.Gameplay;
using V3 = System.Numerics.Vector3;

public static class TraceDriver
{
    const float Dt = 1f / 60f;
    static readonly StringBuilder Sb = new();
    static void Num(float v) => Sb.Append(v.ToString("R", CultureInfo.InvariantCulture));
    static void Vec(V3 v) { Sb.Append('['); Num(v.X); Sb.Append(','); Num(v.Y); Sb.Append(','); Num(v.Z); Sb.Append(']'); }

    public static int Main(string[] args)
    {
        float horizon = args.Length > 0 ? float.Parse(args[0], CultureInfo.InvariantCulture) : 3f;
        float missileTurn = args.Length > 1 ? float.Parse(args[1], CultureInfo.InvariantCulture) : 150f;
        float openDeg = args.Length > 2 ? float.Parse(args[2], CultureInfo.InvariantCulture) : 45f;
        Sb.Append('{');

        // A. a stabilized turret (person aiming) and an AI turret, on a hull that yaws, pitches and rolls; the lagged pod
        var hull = new OrcaFrame(V3.UnitZ, V3.UnitY);
        var pod = hull;
        var person = new OrcaTurret(); person.Reset(hull, new V3(0.3f, 0.1f, 1f));
        var ai = new OrcaTurret(); ai.Reset(hull, V3.UnitZ);
        Sb.Append("\"turret\":[");
        for (int i = 0; i < 600; i++)
        {
            hull.Turn(1.2f * MathF.Sin(i * 0.031f) * Dt, 0.8f * MathF.Cos(i * 0.023f) * Dt, 1.5f * MathF.Sin(i * 0.017f) * Dt);
            pod.Follow(hull, Dt, 0.35f);
            person.TurnBy(0.4f * MathF.Sin(i * 0.05f) * Dt, 0.3f * MathF.Cos(i * 0.043f) * Dt);
            float sway = person.HoldHorizon(hull.U, Dt, horizon);
            var want = V3.Normalize(new V3(MathF.Sin(i * 0.02f), 0.3f * MathF.Cos(i * 0.017f), MathF.Cos(i * 0.02f)));
            ai.SlewToward(want, Dt, 160f);
            if (i > 0) Sb.Append(',');
            Sb.Append('['); Vec(person.Aim); Sb.Append(','); Vec(person.Up); Sb.Append(','); Num(sway); Sb.Append(','); Vec(pod.F); Sb.Append(','); Vec(pod.U); Sb.Append(','); Vec(ai.Aim); Sb.Append(']');
        }
        Sb.Append("],");

        // B. a flown missile: launched along an aim, steered by a scripted thumb
        var m = new OrcaMissile();
        m.Launch(new V3(2f, 5f, -1f), V3.Normalize(new V3(0.2f, 0.1f, 1f)), V3.UnitY, V3.UnitX);
        Sb.Append("\"missile\":[");
        for (int i = 0; i < 300; i++)
        {
            m.StickYaw = MathF.Sin(i * 0.04f); m.StickPitch = 0.5f * MathF.Cos(i * 0.03f); m.StickRoll = 0.2f * MathF.Sin(i * 0.07f);
            m.Step(Dt, 180f, missileTurn);
            if (i > 0) Sb.Append(',');
            Sb.Append('['); Vec(m.Position); Sb.Append(','); Vec(m.Frame.F); Sb.Append(','); Vec(m.Frame.U); Sb.Append(']');
        }
        Sb.Append("],");

        // C. the AI's stick toward a direction, over 60 scripted frames and directions (no noise)
        Sb.Append("\"steer\":[");
        for (int i = 0; i < 60; i++)
        {
            var f = new OrcaFrame(V3.UnitZ, V3.UnitY);
            f.Turn(i * 0.37f, MathF.Sin(i * 0.5f) * 0.9f, i * 0.21f);
            var d = V3.Normalize(new V3(MathF.Cos(i * 1.3f), MathF.Sin(i * 0.7f), MathF.Sin(i * 1.3f + 0.4f)));
            var (yaw, pitch, roll) = OrcaCrewCore.SteerToward(f, d, 0f);
            if (i > 0) Sb.Append(',');
            Sb.Append('['); Num(yaw); Sb.Append(','); Num(pitch); Sb.Append(','); Num(roll); Sb.Append(']');
        }
        Sb.Append("],");

        // D. the shield's shape (hull-local, so no mirror)
        var dirs = new List<V3>();
        OrcaCrewCore.ShieldDirections(144, openDeg, dirs);
        Sb.Append("\"shield\":[");
        for (int i = 0; i < dirs.Count; i++) { if (i > 0) Sb.Append(','); Vec(dirs[i]); }
        Sb.Append("]}");
        Console.WriteLine(Sb.ToString());
        return 0;
    }
}
