// Dumps the SHIPPED converter core's poses for a fixed play so parity_core.cjs can hold combineCore.js to them.
// Usage: dotnet run -- <OmniCombineData.json> <out.json>
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CosmicShore.Utility;

static class Program
{
    static OmniCombineParams Play() => new OmniCombineParams
    {
        Seed = 1234567, Lead = 2.7, Layout = 2, Spread = 3.05, LayoutYaw = 1.1,
        Flight = 4.9, Stagger = 4.9 * 1.01, Ease = 2, Arc = 0.41, Swirl = 0.07, Tumble = 0.27,
        MorphStart = 0.1, MorphLength = 0.8, SiteSpread = 0.52, Suck = 0.36, EffectFade = 0.7, EffectPhase = 41.3,
        SocketSpacing = 0.16, SocketSeconds = 0.9, MassRaise = 0.6, ApproachAboveCamera = 0,
        ShaderDepth = true, IdleTumble = false, GameMotion = true,
    };

    static int Main(string[] args)
    {
        var data = JsonSerializer.Deserialize<OmniCombineData>(File.ReadAllText(args[0]), new JsonSerializerOptions { IncludeFields = true });
        var core = new OmniCombineCore(data);
        var p = Play();
        double az = 2.3, rate = 5 * Math.PI / 180;
        core.FaceCamera(ref p, az, rate, 0);
        var outp = new Dictionary<string, object>();
        outp["approachTurn"] = p.ApproachTurn;
        outp["approachElevation"] = p.ApproachElevation;
        var frames = new List<object>();
        var pos = new float[4][];
        var morph = new float[4][];
        for (int k = 0; k < 4; k++) { pos[k] = new float[core.VertexCount(k) * 3]; morph[k] = new float[128]; }
        var eff = new float[4];
        var cen = new float[12];
        foreach (double t in new[] { 0.5, 3.0, 4.4, 6.1, 7.6, 9.0, 11.3, 13.0, 15.5, 18.2, 20.0, 22.7, 24.4, 26.0 })
        {
            p.CameraAzimuth = az + rate * t;
            p.CameraElevation = 18 * Math.PI / 180;
            var cam = new D3(Math.Sin(p.CameraAzimuth) * Math.Cos(p.CameraElevation), Math.Sin(p.CameraElevation), Math.Cos(p.CameraAzimuth) * Math.Cos(p.CameraElevation));
            core.Pose(t, p, cam, pos, eff, cen, morph);
            frames.Add(new Dictionary<string, object> {
                ["t"] = t, ["camAz"] = p.CameraAzimuth, ["camEl"] = p.CameraElevation,
                ["pos"] = new[] { (float[])pos[0].Clone(), (float[])pos[1].Clone(), (float[])pos[2].Clone(), (float[])pos[3].Clone() },
                ["effect"] = (float[])eff.Clone(), ["centres"] = (float[])cen.Clone(),
                ["massPlace"] = OmniCombineCore.MassColumnPlace(t, p),
            });
        }
        outp["frames"] = frames;

        // The woken omni: swell, cadence turns, cued waves.
        OmniCombineMotion.TryFit(data, (float)core.OmniRadius, 1.1f, out var motion, out var problem);
        var cadence = new OmniCadence(1.733f, 1.8f, 2.4f, 0.15f);
        var omni = new List<object>();
        var rest = new float[data.omni.verts.Length];
        var op = new float[data.omni.verts.Length];
        foreach (double beats in new[] { -0.3, 0.4, 1.2, 2.5, 3.9, 4.3, 5.6, 7.0, 7.9, 9.1, 12.0 })
        {
            double fx = beats < 0 ? 0.6 : 1;
            motion.Swell(data.omni.verts, (float)fx, rest);
            var turn = SquareTurn.None; float progress = 0; double waveSeconds = -1; long wave = -1;
            if (beats >= 0)
            {
                turn = cadence.SquaresAt((float)beats, out progress);
                if (cadence.WaveAt((float)beats, out float secondsIn, out long index)) { wave = index; waveSeconds = data.flip.first + secondsIn; }
            }
            core.OmniPose(30 + beats, p, fx, new D3(0, 0.3, 0.95).Unit, op, rest, waveSeconds, wave);
            if (turn != SquareTurn.None)
                motion.TurnSquares(rest, op, turn == SquareTurn.Pentagon, OmniCadence.Smoother(progress), 0.14f * (float)core.OmniRadius, 1.5f, 1.5f);
            omni.Add(new Dictionary<string, object> { ["beats"] = beats, ["fx"] = fx, ["pos"] = (float[])op.Clone() });
        }
        outp["omni"] = omni;
        File.WriteAllText(args[1], JsonSerializer.Serialize(outp));
        Console.WriteLine("wrote " + args[1]);
        return 0;
    }
}
