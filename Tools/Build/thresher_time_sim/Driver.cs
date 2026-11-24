// The Thresher Time model's PLANT PROBE: flies the SHIPPED ThresherChainSolver (compiled from its real path, never
// copied) the way an AI Thresher plants (ThresherSlingAIPolicySO: Plant and Winch pressed together through the
// replicated press, which every peer reads as a FULL pull), at ThresherConfig.asset's own dials (read from the asset,
// not retyped), and prints what Tools/Build/thresher_time_sim.py needs as JSON:
//   plant    per AI cruise speed: the planted orbit's speed, radius, spin and launch-view grip every 1/60 s for 6 s
//   launch   per hold: the aimed launch (ArmLaunch with the asset's launchBoost): how long it waited for its point,
//            the speed it left at, and its physics speed after (the stand-in pilot's 1.5/s ease, the ball's tug)
// The stand-in pilot is ThresherAnalogTriggerTests.Flight's: speed eases toward the throttle target at 1.5/s, the
// nose is the solver's velocity while planted. The transformer, executor and exit buff are not run (see the .py).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using CosmicShore.Gameplay;
using UnityEngine;

static class Driver
{
    const float Dt = 1f / 60f;
    const float FullPull = 0.97f, LetGo = 0.9f;

    static readonly Dictionary<string, float> Asset = new();

    static void ReadAsset(string path)
    {
        bool inDials = false;
        foreach (var raw in File.ReadAllLines(path))
        {
            if (raw.StartsWith("  dials:")) { inDials = true; continue; }
            if (inDials && !raw.StartsWith("    ")) inDials = false;
            string line = raw.Trim();
            int c = line.IndexOf(':');
            if (c <= 0) continue;
            string key = line.Substring(0, c), val = line.Substring(c + 1).Trim();
            if (!float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) continue;
            Asset[(inDials ? "dials." : "") + key] = f;
        }
    }

    static ThresherChainSettings Settings()
    {
        var dials = new ThresherDials();
        int set = 0;
        foreach (var fi in typeof(ThresherDials).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (!Asset.TryGetValue("dials." + fi.Name, out float v)) continue;
            if (fi.FieldType == typeof(float)) fi.SetValue(dials, v);
            else if (fi.FieldType == typeof(bool)) fi.SetValue(dials, v != 0f);
            else if (fi.FieldType == typeof(int)) fi.SetValue(dials, (int)v);
            else continue;
            set++;
        }
        if (set < 40) throw new Exception($"only {set} dials read from ThresherConfig.asset");
        return dials.ToSettings();
    }

    sealed class Flight
    {
        public readonly ThresherChainSolver S;
        public Vector3 P, F = Vector3.forward, Last;
        public float V, Cruise;
        public ThresherStepResult R;
        public Flight(ThresherChainSettings s, float cruise)
        {
            S = new ThresherChainSolver(s);
            Cruise = V = cruise;
            Last = F * V;
            S.Reset(P, F, Last);
        }
        public void Frame(float lt, float rt, float yawDegPerSec)
        {
            var edge = S.AnalogTriggers(lt, rt, FullPull, LetGo, P, Last, Vector3.up, Dt);
            if (edge == ThresherTriggerEdge.LetGo) S.Release(Last);
            float a = yawDegPerSec * Dt * Mathf.Deg2Rad, c = Mathf.Cos(a), sn = Mathf.Sin(a);
            F = new Vector3(F.x * c + F.z * sn, F.y, -F.x * sn + F.z * c);
            float th = V + (Cruise - V) * 1.5f * Dt;
            R = S.Step(P, F * th, false, Cruise, Dt);
            if (S.Mode == ThresherMode.Pivot || R.Launched) F = R.ShipVelocity.normalized;
            V = R.ShipVelocity.magnitude;
            P += R.ShipVelocity * Dt;
            Last = R.ShipVelocity;
        }
    }

    static string N(float f) => f.ToString("0.###", CultureInfo.InvariantCulture);

    static int Main(string[] args)
    {
        ReadAsset(args.Length > 0 ? args[0] : "Assets/_SO_Assets/VesselActions/Thresher/ThresherConfig.asset");
        var s = Settings();
        float boost = Asset.TryGetValue("launchBoost", out float b) ? b : 1f;
        float spinRate = Asset.TryGetValue("spectateSpinRate", out float sr) ? sr : 0.9f;
        var sb = new StringBuilder();
        sb.Append("{\"launchBoost\":").Append(N(boost)).Append(",\"spectateSpinRate\":").Append(N(spinRate));
        sb.Append(",\"exitMultiplier\":").Append(N(Asset.TryGetValue("launchExitSpeedMultiplier", out float em) ? em : 1f));
        sb.Append(",\"exitSeconds\":").Append(N(Asset.TryGetValue("launchExitSeconds", out float es) ? es : 0f));
        sb.Append(",\"restLength\":").Append(N(s.RestLength)).Append(",\"lockCeiling\":").Append(N(s.MaxBallSpeed)).Append(",\"plant\":{");
        // AI cruise speeds: 15 + 110 x Lerp(0.2, 0.9, intensity x 0.25), and full throttle.
        // Above 125: a plant entered still fast from a launch (the AI re-plants while its last launch bleeds), on the
        // I4 throttle target (114) - set straight onto the hull, no tow first.
        float[] cruises = { 56.25f, 75.5f, 94.75f, 114f, 125f, 200f, 300f, 450f, 700f };
        for (int k = 0; k < cruises.Length; k++)
        {
            var f = new Flight(s, Mathf.Min(cruises[k], 114f));
            if (cruises[k] > 125f) { f.V = cruises[k]; f.Last = f.F * f.V; f.S.Reset(f.P, f.F, f.Last); }
            else for (int i = 0; i < 60; i++) f.Frame(0f, 0f, 0f);   // towing, RT up (the AI presses no winch while towing)
            if (k > 0) sb.Append(',');
            sb.Append('"').Append(N(cruises[k])).Append("\":[");
            for (int i = 0; i < 360; i++)
            {
                f.Frame(1f, 1f, 0f);
                bool pivot = f.S.Mode == ThresherMode.Pivot;
                float r = pivot ? (f.P - f.S.Pivot).magnitude : 0f;
                float spin = pivot ? ThresherCameraFraming.SpinRate(f.S.LockSpeed, f.S.Extent) : 0f;
                bool grip = ThresherCameraFraming.LaunchViewGrip(true, f.S.IsBraking, 1f, f.S.Rigidity, FullPull);
                if (i > 0) sb.Append(',');
                sb.Append('[').Append(N((i + 1) * Dt)).Append(',').Append(pivot ? 1 : 0).Append(',')
                  .Append(N(f.S.LockSpeed)).Append(',').Append(N(r)).Append(',').Append(N(f.S.Extent)).Append(',')
                  .Append(N(spin)).Append(',').Append(grip && pivot ? 1 : 0).Append(',').Append(N(f.V)).Append(']');
            }
            sb.Append(']');
        }
        sb.Append("},\"launch\":[");
        bool first = true;
        foreach (float cruise in new[] { 56.25f, 114f, 200f, 300f, 450f })
            for (float hold = 0.6f; hold <= 4.01f; hold += 0.2f)
            {
                var f = new Flight(s, Mathf.Min(cruise, 114f));
                if (cruise > 125f) { f.V = cruise; f.Last = f.F * f.V; f.S.Reset(f.P, f.F, f.Last); }
                else for (int i = 0; i < 60; i++) f.Frame(0f, 0f, 0f);
                for (int i = 0; i < Mathf.RoundToInt(hold / Dt); i++) f.Frame(1f, 1f, 0f);
                if (f.S.Mode != ThresherMode.Pivot) continue;
                float lockAtArm = f.S.LockSpeed;
                // The aim a quarter lap ahead in the orbit plane (the median wait is half a lap's worth of angles).
                Vector3 radial = (f.P - f.S.Pivot).normalized;
                Vector3 aim = (radial * -1f).normalized;
                f.S.ArmLaunch(aim, boost);
                int wait = 0;
                for (; wait < 1200 && !f.R.Launched; wait++) f.Frame(0f, 0f, 0f);
                if (!f.R.Launched) continue;
                float left = f.V, lockAtLaunch = f.S.LockSpeed;
                var after = new List<float>();
                for (int i = 1; i <= 180; i++) { f.Frame(0f, 0f, 0f); if (i % 15 == 0) after.Add(f.V); }
                if (!first) sb.Append(',');
                first = false;
                sb.Append("{\"cruise\":").Append(N(cruise)).Append(",\"hold\":").Append(N(hold))
                  .Append(",\"lockAtArm\":").Append(N(lockAtArm)).Append(",\"waitSeconds\":").Append(N(wait * Dt))
                  .Append(",\"leftAt\":").Append(N(left)).Append(",\"after\":[");
                for (int i = 0; i < after.Count; i++) sb.Append(i > 0 ? "," : "").Append(N(after[i]));
                sb.Append("]}");
            }
        sb.Append("]}");
        Console.WriteLine(sb.ToString());
        return 0;
    }
}
