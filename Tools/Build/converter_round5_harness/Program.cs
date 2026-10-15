// Checks the Elemental Converter's round-5 pieces on the SHIPPED combine data (OmniCombineData.json, Unity frame):
//  1. OmniCombineMotion fits all 60 square plates to the data's three keys (pentagon, Step1, Step2) as rigid turns;
//  2. at full swell every square, rhombus and pentagon is the game's 1.1x thickness; the triangle plates do not move;
//  3. a finished pentagon / triangle turn lands the thickened squares CONGRUENT with rest (the snap back is invisible),
//     and every square stays rigid mid-turn;
//  4. OmniCombineCore's game-motion OmniPose: with no wave cued nothing moves (no squares weave, no flip); a cued wave
//     run to its end lands every rhombus congruent with rest, so the converter's reset between waves is invisible;
//  5. CrystalFrame reproduces Pose for every unmorphed crystal vertex, composed as the player composes it in Unity
//     (AngleAxis(tumble) * AngleAxis(spin), then the centre) - with the idle spin ON, so the order is exercised;
//  6. the Charge light reads both combine crystals (12 blooms; 12 pentagons) and the flux arcs' ends sit on the
//     pentagons as OmniCombineMotion swells them;
//  7. the lab path is untouched: GameMotion off gives the lab's OmniPose (the parity harness holds the lab itself);
//  8. every crystal but the first comes in with a bit of extra spacing, held until it is centred on the omni and then
//     settled to nothing over SocketSeconds, and arrives from the camera's side (FaceCamera), so the rhombi that must
//     cross the omni do it behind it;
//  9. the Mass prisms ride raised up their columns (OmniCombineConfig.massRaise, on MassDepthPrisms' own column) all the
//     way in and come down them only once the crystal is centred (OmniCombineCore.MassColumnPlace): raised, every thick
//     prism is wholly clear of the landed shell, socketing it comes straight down through its own hole, and it never
//     reaches the waiting Time crystal.
// Negative controls: the square keys out of order are refused; composing the frame the other way round fails check 5;
// arriving straight in (no FaceCamera), Time's camera-facing rhombi come in from inside; the Mass prisms sealed as the
// crystal sets off (the behaviour before 2026-10-14) cross the shell beside their holes; at the very top of their column
// (raise 1) they reach into the waiting Time crystal.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CosmicShore.Utility;
using UnityEngine;

static class Program
{
    static int failures;
    static void Check(string what, bool ok, string detail = "") { Console.WriteLine((ok ? "PASS  " : "FAIL  ") + what + (detail.Length > 0 ? "  (" + detail + ")" : "")); if (!ok) failures++; }
    static Vector3 V(float[] a, int i) => new(a[3 * i], a[3 * i + 1], a[3 * i + 2]);
    static Vector3 V(D3 d) => new((float)d.X, (float)d.Y, (float)d.Z);

    static int Main(string[] args)
    {
        var data = JsonSerializer.Deserialize<OmniCombineData>(File.ReadAllText(args[0]), new JsonSerializerOptions { IncludeFields = true });
        var core = new OmniCombineCore(data);
        float R = (float)core.OmniRadius;
        var ov = data.omni.verts;
        int n = ov.Length / 3;

        // 1
        bool fit = OmniCombineMotion.TryFit(data, R, 1.1f, out var motion, out var problem);
        Check("all 60 squares fit the data's keys as rigid turns", fit && motion.Squares.Length == 60 && motion.SquareCorners.All(c => c.Length == 8),
              fit ? $"{motion.Squares.Length} squares" : problem);
        if (!fit) return Finish();

        // 2
        var swelled = new float[ov.Length];
        motion.Swell(ov, 1f, swelled);
        var plates = Enumerable.Range(0, n).GroupBy(i => data.omni.plate[i]).ToList();
        float worstRatio = 0f, triMoved = 0f;
        foreach (var g in plates)
        {
            var idx = g.ToList();
            if (data.omni.plateOwner[g.Key] == OmniCombineCore.Mass) { foreach (int i in idx) triMoved = Math.Max(triMoved, (V(swelled, i) - V(ov, i)).magnitude); continue; }
            float Thick(float[] p)
            {
                var pts = idx.Select(i => V(p, i)).ToList();
                float lo = pts.Min(x => x.magnitude), hi = pts.Max(x => x.magnitude), mid = (lo + hi) / 2;
                var o = pts.Where(x => x.magnitude > mid).ToList(); var inn = pts.Where(x => x.magnitude <= mid).ToList();
                var oc = o.Aggregate(Vector3.zero, (a, b) => a + b) / o.Count; var ic = inn.Aggregate(Vector3.zero, (a, b) => a + b) / inn.Count;
                return (oc - ic).magnitude;
            }
            worstRatio = Math.Max(worstRatio, Math.Abs(Thick(swelled) / Thick(ov) - 1.1f));
        }
        Check("at full swell squares, rhombi and pentagons are 1.1x thick", worstRatio < 1e-4f, $"worst ratio error {worstRatio:E2}");
        Check("the triangle plates do not swell", triMoved == 0f, $"{triMoved:E2}");

        // 3
        var pose = new float[ov.Length];
        float worstLand = 0f, worstRigid = 0f;
        foreach (bool pentagon in new[] { true, false })
        {
            Array.Copy(swelled, pose, pose.Length);
            motion.TurnSquares(swelled, pose, pentagon, 1f, 0.14f * R, 1.5f, 1.5f);
            var restCorners = motion.SquareCorners.SelectMany(c => c).Select(i => V(swelled, i)).ToList();
            foreach (var c in motion.SquareCorners) foreach (int i in c) worstLand = Math.Max(worstLand, restCorners.Min(r => (r - V(pose, i)).magnitude));
            Array.Copy(swelled, pose, pose.Length);
            motion.TurnSquares(swelled, pose, pentagon, 0.5f, 0.14f * R, 1.5f, 1.5f);
            foreach (var c in motion.SquareCorners)
                for (int a = 0; a < c.Length; a++) for (int b = a + 1; b < c.Length; b++)
                    worstRigid = Math.Max(worstRigid, Math.Abs((V(pose, c[a]) - V(pose, c[b])).magnitude - (V(swelled, c[a]) - V(swelled, c[b])).magnitude));
        }
        Check("a finished turn lands the thickened squares congruent with rest", worstLand < 1e-4f * R, $"worst {worstLand / R:E2} R");
        Check("every square stays rigid mid-turn", worstRigid < 1e-4f * R, $"worst {worstRigid / R:E2} R");

        // 4
        var p = OmniCombineParams.LabDefault;
        p.GameMotion = true;
        p.ShaderDepth = true;   // as the converter plays it: the depth shader, not the CPU, raises the triangles
        var cam = new D3(0.3, 0.4, 1).Unit;
        var outPose = new float[ov.Length];
        core.OmniPose(50, p, 1, cam, outPose, swelled, -1, -1);
        float still = 0f;
        for (int i = 0; i < ov.Length; i++) still = Math.Max(still, Math.Abs(outPose[i] - swelled[i]));
        Check("game motion with no wave cued: nothing moves (no weave, no flip)", still == 0f, $"{still:E2}");
        float active = data.flip.loop - data.flip.first;
        float worstFlip = 0f, moved = 0f;
        for (long wave = 0; wave < 12; wave++)
        {
            core.OmniPose(50, p, 1, cam, outPose, swelled, data.flip.first + active, wave);
            var mid = new float[ov.Length];
            core.OmniPose(50, p, 1, cam, mid, swelled, data.flip.first + 0.6, wave);
            foreach (var g in plates.Where(g => data.omni.plateOwner[g.Key] == OmniCombineCore.Time))
            {
                var restSet = g.Select(i => V(swelled, i)).ToList();
                foreach (int i in g)
                {
                    worstFlip = Math.Max(worstFlip, restSet.Min(r => (r - V(outPose, i)).magnitude));
                    moved = Math.Max(moved, (V(mid, i) - V(swelled, i)).magnitude);
                }
            }
        }
        Check("a cued wave run to its end lands every rhombus congruent with rest", worstFlip < 1e-4f * R && moved > 0.05f * R,
              $"worst {worstFlip / R:E2} R; mid-wave a rhombus moves {moved / R:F2} R");

        // 5
        var q = OmniCombineParams.LabDefault;   // IdleTumble ON: the spin is non-zero while Charge waits
        q.GameMotion = true;
        var positions = Enumerable.Range(0, 4).Select(k => new float[3 * core.VertexCount(k)]).ToArray();
        var effect = new float[4];
        float worstFrame = 0f, worstSwapped = 0f;
        foreach (double t in new[] { 0.5, 1.7, OmniCombineCore.Departs(OmniCombineCore.Charge, q) + 0.2 })
        {
            core.Pose(t, q, cam, positions, effect);
            for (int k = 0; k < 4; k++)
            {
                if (k == OmniCombineCore.Mass) continue;   // Mass's idle pose has the depth seat folded in on the CPU in the lab path
                if (k == OmniCombineCore.Time || k == OmniCombineCore.Space) continue;   // their idle effects move vertices
                core.CrystalFrame(k, t, q, out var c, out var spinAxis, out double spin, out var tumbleAxis, out double tumble);
                var rot = Quaternion.AngleAxis((float)(tumble * Mathf.Rad2Deg), V(tumbleAxis)) * Quaternion.AngleAxis((float)(spin * Mathf.Rad2Deg), V(spinAxis));
                var swapped = Quaternion.AngleAxis((float)(spin * Mathf.Rad2Deg), V(spinAxis)) * Quaternion.AngleAxis((float)(tumble * Mathf.Rad2Deg), V(tumbleAxis));
                var basis = core.CrystalData(k).basis;
                for (int i = 0; i < basis.Length / 3; i++)
                {
                    var want = V(positions[k], i);
                    worstFrame = Math.Max(worstFrame, (rot * V(basis, i) + V(c) - want).magnitude);
                    worstSwapped = Math.Max(worstSwapped, (swapped * V(basis, i) + V(c) - want).magnitude);
                }
            }
        }
        Check("CrystalFrame, composed as the player composes it, reproduces Pose for the unmorphed Charge crystal", worstFrame < 1e-4f * R,
              $"worst {worstFrame / R:E2} R");
        Check("negative control: the frame composed the other way round does not", worstSwapped > 1e-2f * R, $"worst {worstSwapped / R:E2} R");

        // 6
        var charge = core.CrystalData(OmniCombineCore.Charge);
        bool blooms = ChargeSites.TryBuild(Enumerable.Range(0, charge.basis.Length / 3).Select(i => V(charge.basis, i)).ToArray(), charge.tris, 1f, out var cs, out problem);
        Check("the Charge light reads the combine's Charge crystal as 12 blooms with the dodecahedral seam net",
              blooms && cs.Kind == ChargeSiteKind.Blooms && cs.Seams.Length == 30, problem ?? "");
        bool pents = ChargeSites.TryBuild(Enumerable.Range(0, n).Select(i => V(ov, i)).ToArray(), data.omni.tris, 1.1f, out var os, out problem);
        Check("the Charge light reads the combine's omni as 12 pentagons", pents && os.Kind == ChargeSiteKind.Pentagons, problem ?? "");
        if (pents)
        {
            var sw = Enumerable.Range(0, n).Select(i => V(swelled, i)).ToList();
            float worstEnd = 0f;
            for (int s = 0; s < 12; s++)
                foreach (var c in os.Corners[s])
                {
                    var lifted = c - os.Axes[s] * (0.012f * os.Radius);
                    worstEnd = Math.Max(worstEnd, sw.Min(v => (v - lifted).magnitude));
                }
            Check("the flux arcs end on the pentagons as the converter swells them", worstEnd < 2e-3f * R, $"worst {worstEnd / R:E2} R (the lift is along the plate normal, not the axis)");
        }

        // 7
        var lab = OmniCombineParams.LabDefault;
        var a1 = new float[ov.Length]; var a2 = new float[ov.Length];
        core.OmniPose(13.3, lab, 1, cam, a1);
        core.OmniPose(13.3, lab, 1, cam, a2, swelled, 0.5, 3);
        Check("GameMotion off ignores the game-motion arguments (the lab path)", a1.SequenceEqual(a2));

        // 8: a bit of extra spacing that settles, and every crystal but the first arriving from the camera's side
        var sp = OmniCombineParams.LabDefault;
        sp.GameMotion = true; sp.ShaderDepth = true; sp.IdleTumble = false; sp.SocketSpacing = 0.16; sp.SocketSeconds = 0.9; sp.Stagger = sp.Flight;
        {
            // the spacing: a plate is pushed out along its slot by w * spacing * |slot| until it settles, and lands exactly
            var none = sp; none.SocketSpacing = 0;
            var (pushErr, landed, spaceMoved) = SpacingStats(core, sp, none);
            Check("the extra spacing pushes each plate out along its slot by exactly w x spacing, held until the crystal is centred, then settles", pushErr < 1e-4f * R, $"worst {pushErr / R:E2} R");
            Check("every plate still lands exactly in its slot", landed < 1e-4f * R, $"worst {landed / R:E2} R");
            Check("the first crystal (Space) is untouched by the spacing", spaceMoved == 0f, $"{spaceMoved:E2}");
        }
        {
            // the approach: aimed where the camera will be, starting from exactly where the crystal idles
            var rng = new System.Random(5);
            float worstAim = 0f, worstStart = 0f, worstSpaceTurn = 0f, worstFollow = 0f, worstFrozen = 0f, worstHeight = 0f, straightHeight = 0f;
            float[] faced = new float[4], straightIn = new float[4];
            for (int play = 0; play < 24; play++)
            {
                var pq = sp; pq.LayoutYaw = rng.NextDouble() * 2 * Math.PI; pq.Swirl = rng.NextDouble() * 0.3 - 0.15; pq.Seed = rng.Next();
                double orbit0 = rng.NextDouble() * 2 * Math.PI, rate = 5 * Math.PI / 180;
                D3 Cam(double t) => CameraAt(orbit0 + rate * t);
                var aimed = pq;
                double az0 = Azimuth(Cam(0));
                core.FaceCamera(ref aimed, az0, rate, 0);
                // the camera the player hands it every frame: unwrapped, on the same count FaceCamera was aimed with
                OmniCombineParams Live(OmniCombineParams q0, double t, double actualRate)
                {
                    q0.CameraAzimuth = az0 + actualRate * t;
                    q0.CameraElevation = 18 * Math.PI / 180;   // ElementalConverterView's pitch
                    return q0;
                }
                worstSpaceTurn = Math.Max(worstSpaceTurn, (float)Math.Abs(aimed.ApproachTurn[0]));
                for (int k = 1; k < 4; k++)
                {
                    double t0 = OmniCombineCore.Departs(k, pq);
                    core.CrystalFrame(k, t0, pq, out var c0, out _, out _, out _, out _);
                    core.CrystalFrame(k, t0, Live(aimed, t0, rate), out var c1, out _, out _, out _, out _);
                    worstStart = Math.Max(worstStart, (float)(c1 - c0).Length);
                    double tA = t0 + 0.97 * pq.Flight;
                    core.CrystalFrame(k, tA, Live(aimed, tA, rate), out var cA, out _, out _, out _, out _);
                    worstAim = Math.Max(worstAim, (float)Math.Abs(OmniCombineCore.WrapAngle(Azimuth(cA) - Azimuth(Cam(tA)))));
                    // and at the camera's height, not dropping in from above it
                    double elevation = Math.Asin(cA.Y / cA.Length);
                    worstHeight = Math.Max(worstHeight, (float)Math.Abs(elevation - 18 * Math.PI / 180));
                    core.CrystalFrame(k, tA, pq, out var cS, out _, out _, out _, out _);
                    straightHeight = Math.Max(straightHeight, (float)(Math.Asin(cS.Y / cS.Length) - 18 * Math.PI / 180));
                    // the camera circling twice as fast as predicted, or stopping: the approach still moves with it
                    foreach (double actual in new[] { 2 * rate, 0.0 })
                    {
                        core.CrystalFrame(k, tA, Live(aimed, tA, actual), out var cB, out _, out _, out _, out _);
                        worstFollow = Math.Max(worstFollow, (float)Math.Abs(OmniCombineCore.WrapAngle(Azimuth(cB) - (az0 + actual * tA))));
                    }
                    // negative control: aimed once, not following (the camera's travel since the aim left out)
                    var frozen = aimed; frozen.CameraAzimuth = az0;
                    core.CrystalFrame(k, tA, frozen, out var cF, out _, out _, out _, out _);
                    worstFrozen = Math.Max(worstFrozen, (float)Math.Abs(OmniCombineCore.WrapAngle(Azimuth(cF) - (az0 + 2 * rate * tA))));
                }
                var h1 = HiddenStats(core, aimed, Cam, t => az0 + rate * t); var h0 = HiddenStats(core, pq, Cam, t => az0 + rate * t);
                for (int k = 1; k < 4; k++) { faced[k] += h1[k] / 24; straightIn[k] += h0[k] / 24; }
            }
            Check("every crystal but the first comes in from the camera's side", worstAim * 180 / Math.PI < 12f, $"worst {worstAim * 180 / Math.PI:F1} deg off the camera's azimuth as it arrives");
            Check("the turn starts at nothing: a crystal sets off from exactly where it idles", worstStart < 1e-9f * R, $"{worstStart:E2}");
            Check("every crystal but the first arrives at the camera's height, not from above it", worstHeight * 180 / Math.PI < 8f,
                  $"worst {worstHeight * 180 / Math.PI:F1} deg off the camera's 18 deg");
            Check("negative control: on its own path a crystal drops in from well above the camera (the reported 'from the top')",
                  straightHeight * 180 / Math.PI > 25f, $"up to {straightHeight * 180 / Math.PI:F0} deg above it");
            Check("the approach moves with the camera: twice as fast as predicted, or stopped, it still arrives from the camera's side",
                  worstFollow * 180 / Math.PI < 12f, $"worst {worstFollow * 180 / Math.PI:F1} deg");
            Check("negative control: aimed once without following, a faster camera leaves it behind", worstFrozen * 180 / Math.PI > 30f,
                  $"{worstFrozen * 180 / Math.PI:F0} deg off");
            Check("Space keeps its path", worstSpaceTurn == 0f);
            Check("rhombi under camera-facing holes are almost never inside the omni late in their morph",
                  faced[1] < 0.04f && faced[2] < 0.08f && faced[3] < 0.04f,
                  $"Charge {faced[1]:P1}, Mass {faced[2]:P1}, Time {faced[3]:P1} of late frames");
            Check("negative control: arriving straight in, they are (the reported 'came in from inside')", straightIn[3] > 0.12f,
                  $"Charge {straightIn[1]:P1}, Mass {straightIn[2]:P1}, Time {straightIn[3]:P1}");
        }

        // 9: the Mass prisms raised up their columns all the way in, and down them only once the crystal is centred
        {
            // The shipped dials: the Mass crystal's own column (MassDepthPrisms.mat) and the raise (OmniCombineConfig.asset).
            string root = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(args[0])))));
            float reach = Saved(Path.Combine(root, "_Graphics/Materials/CrystalMaterials/MassDepthPrisms.mat"), "_Reach");
            float sizeStart = Saved(Path.Combine(root, "_Graphics/Materials/CrystalMaterials/MassDepthPrisms.mat"), "_PlateScaleStart");
            float raise = Saved(Path.Combine(root, "_SO_Assets/Environment/OmniCombine/OmniCombineConfig.asset"), "massRaise");
            if (Environment.GetEnvironmentVariable("RAISE") is string forced) raise = float.Parse(forced);
            var shellOuter = new[] { OmniCombineCore.Space, OmniCombineCore.Charge }
                .Max(k => Enumerable.Range(0, core.VertexCount(k)).Max(i => V(core.CrystalData(k).final, i).magnitude));
            var rng = new System.Random(9);
            float worstPlace = 0f, worstCentred = 0f, worstOnSlot = 0f, worstSocketed = 0f, newSocket = 0f;
            float lowestBottom = float.MaxValue, nearestTime = float.MaxValue, nearestTimeSealed = float.MaxValue, nearestTimeTop = float.MaxValue;
            double newOff = 0, oldOff = 0, newOffSeen = 0, oldOffSeen = 0, prismFrames = 0;
            for (int play = 0; play < 24; play++)
            {
                var pq = sp; pq.LayoutYaw = rng.NextDouble() * 2 * Math.PI; pq.Swirl = rng.NextDouble() * 0.3 - 0.15; pq.Seed = rng.Next();
                pq.SocketSeconds = 0.8 + 0.2 * rng.NextDouble();
                pq.Stagger = pq.Flight * (0.95 + 0.1 * rng.NextDouble());
                pq.Spread = 2.7 + 0.6 * rng.NextDouble();
                pq.Arc = 0.25 + 0.3 * rng.NextDouble();
                pq.MassRaise = raise;
                // as the converter plays it: from the camera's side, following the camera round at the converter's rate
                double orbit0 = rng.NextDouble() * 2 * Math.PI, rate = 5 * Math.PI / 180;
                D3 Cam(double t) => CameraAt(orbit0 + rate * t);
                double az0 = Azimuth(Cam(0));
                core.FaceCamera(ref pq, az0, rate, 0);
                pq.CameraElevation = 18 * Math.PI / 180;
                double centred = OmniCombineCore.Centred(OmniCombineCore.Mass, pq);
                // Held up its column from the moment the crystal sets off until it is centred.
                for (double t = OmniCombineCore.Departs(OmniCombineCore.Mass, pq); t < centred; t += 1.0 / 30)
                    worstPlace = Math.Max(worstPlace, (float)Math.Abs(OmniCombineCore.MassColumnPlace(t, pq) - (1 - raise)));
                worstSocketed = Math.Max(worstSocketed, (float)(1 - OmniCombineCore.MassColumnPlace(centred + pq.SocketSeconds, pq)));
                pq.CameraAzimuth = az0 + rate * centred;
                core.CrystalFrame(OmniCombineCore.Mass, centred, pq, out var at, out _, out _, out _, out _);
                worstCentred = Math.Max(worstCentred, (float)at.Length);
                foreach (var c in MassColumns(core, pq, centred, reach, OmniCombineCore.MassColumnPlace(centred, pq), sizeStart, data))
                {
                    worstOnSlot = Math.Max(worstOnSlot, Vector3.Angle(c.column, c.slot));
                    lowestBottom = Math.Min(lowestBottom, c.body.Min(v => v.magnitude));
                }
                // Through the flight and the socket:
                //  - every prism at the shell's radius (its top within 0.85-1.15 of its slot's) and more than 2 deg off its own
                //    hole's axis is going through the plates beside the hole, not the hole;
                //  - how near any raised prism comes to the Time crystal (waiting, then setting off).
                for (double t = OmniCombineCore.Departs(OmniCombineCore.Mass, pq) + pq.EffectFade; t <= centred + pq.SocketSeconds; t += 1.0 / 30)
                {
                    pq.CameraAzimuth = az0 + rate * t;
                    var eye = V(Cam(t));
                    var now = MassColumns(core, pq, t, reach, OmniCombineCore.MassColumnPlace(t, pq), sizeStart, data);
                    var sealedNow = MassColumns(core, pq, t, reach, 1, sizeStart, data);
                    var topNow = MassColumns(core, pq, t, reach, OmniCombineCore.SocketIn(OmniCombineCore.Mass, t, pq), sizeStart, data);
                    var time = TimePose(core, pq, t);
                    for (int j = 0; j < now.Count; j++)
                    {
                        prismFrames++;
                        if (AtShell(now[j], out float off) && t >= centred) newSocket = Math.Max(newSocket, off);
                        if (AtShell(now[j], out off) && off > 2f) { newOff++; if (Vector3.Dot(now[j].top.normalized, eye) > 0) newOffSeen++; }
                        if (AtShell(sealedNow[j], out off) && off > 2f) { oldOff++; if (Vector3.Dot(sealedNow[j].top.normalized, eye) > 0) oldOffSeen++; }
                        if ((now[j].top - time.centre).magnitude < time.radius + 2 * R)
                            foreach (var v in now[j].body) foreach (var w in time.verts) nearestTime = Math.Min(nearestTime, (v - w).magnitude);
                        if ((sealedNow[j].top - time.centre).magnitude < time.radius + 2 * R)
                            foreach (var v in sealedNow[j].body) foreach (var w in time.verts) nearestTimeSealed = Math.Min(nearestTimeSealed, (v - w).magnitude);
                        if ((topNow[j].top - time.centre).magnitude < time.radius + 2 * R)
                            foreach (var v in topNow[j].body) foreach (var w in time.verts) nearestTimeTop = Math.Min(nearestTimeTop, (v - w).magnitude);
                    }
                }
            }
            Console.WriteLine($"INFO  the Mass column: reach {reach}, size at the top {sizeStart}; raise {raise}; the landed shell's outer radius {shellOuter / R:F3} R");
            Check("the Mass prisms stay raised up their columns until the crystal is centred", worstPlace < 1e-6f, $"worst {worstPlace:E2} off the raise");
            Check("the Mass crystal is centred on the omni when its prisms start down", worstCentred < 1e-9f * R, $"{worstCentred / R:E2} R off");
            Check("and every column then runs straight down its own triangle's axis", worstOnSlot < 0.05f, $"worst {worstOnSlot:F3} deg");
            Check("raised, centred, every thick Mass prism stands wholly clear of the landed shell, so all its sideways motion was above it",
                  lowestBottom > shellOuter + 0.02f * R, $"deepest point {lowestBottom / R:F3} R, shell to {shellOuter / R:F3} R");
            Check("the prisms are seated once the crystal has socketed in", worstSocketed < 1e-9f, $"{worstSocketed:E2}");
            Check("socketing in, every Mass prism comes down straight through its own hole", newSocket < 0.5f, $"worst {newSocket:F2} deg off its hole's axis at the shell");
            Check("a Mass prism is at the shell beside its hole far less often than when it sealed in flight", newOff < 0.35 * oldOff,
                  $"{newOff / prismFrames:P2} of prism-frames ({newOffSeen / prismFrames:P2} on the camera's half), all mid-flight, as prisms bound for the far " +
                  $"side pass through the omni; none while socketing");
            Check("negative control: sealed as the crystal sets off (before 2026-10-14) they slide along the shell into their holes", oldOff > 0,
                  $"{oldOff / prismFrames:P2} of prism-frames ({oldOffSeen / prismFrames:P2} on the camera's half)");
            Check("a raised Mass prism never reaches the Time crystal", nearestTime > 0.05f * R,
                  $"nearest {nearestTime / R:F2} R (sealed in flight: {nearestTimeSealed / R:F2} R)");
            Check("negative control: at the very top of the column (raise 1) a prism reaches into the waiting Time crystal", nearestTimeTop < 0.05f * R,
                  $"nearest {nearestTimeTop / R:F2} R");
        }

        // negative control: keys out of order
        var swappedData = JsonSerializer.Deserialize<OmniCombineData>(File.ReadAllText(args[0]), new JsonSerializerOptions { IncludeFields = true });
        (swappedData.squares.keys[0], swappedData.squares.keys[1]) = (swappedData.squares.keys[1], swappedData.squares.keys[0]);
        Check("negative control: square keys out of order are refused", !OmniCombineMotion.TryFit(swappedData, R, 1.1f, out _, out var why), why ?? "accepted");
        return Finish();
    }

    static D3 CameraAt(double orbit)
    {
        // ElementalConverterView: Quaternion.Euler(18, orbit, 0) * Vector3.back, the direction from the stage to the camera
        double pitch = 18 * Math.PI / 180;
        return new D3(-Math.Sin(orbit) * Math.Cos(pitch), Math.Sin(pitch), -Math.Cos(orbit) * Math.Cos(pitch));
    }

    static double Azimuth(D3 v) => Math.Atan2(v.X, v.Z);

    static IEnumerable<(int k, int q, Vector3 centre, Vector3 slot, float w)> PlateCentres(OmniCombineCore core, float[][] pos, float[][] morph)
    {
        for (int k = 1; k < 4; k++)
        {
            var d = core.CrystalData(k);
            foreach (var g in Enumerable.Range(0, d.plate.Length).GroupBy(i => d.plate[i]))
            {
                var c = g.Aggregate(Vector3.zero, (acc, i) => acc + V(pos[k], i)) / g.Count();
                var slot = g.Aggregate(Vector3.zero, (acc, i) => acc + V(d.final, i)) / g.Count();
                yield return (k, g.Key, c, slot, morph[k][g.Key]);
            }
        }
    }

    /// <summary>The Mass prisms at <paramref name="t"/>, as OmniCombinePlayer.WriteMass and OmniDepthPrismShader draw them, in
    /// the omni's frame: each plate's column (its centre sliding to its triangle's as it morphs, from the crystal's flight
    /// centre), where its top puts the prism at column place <paramref name="place"/> on the Mass crystal's own column, the
    /// prism's body there (the thin plate thickened inward from its top face by the thickness blending from the Mass prisms'
    /// to the omni triangles' with the mean morph, then carried up the column and scaled, as DepthPrismPosition does; the
    /// face split, at most a few hundredths of R, is left out), and its triangle's slot.</summary>
    static List<(Vector3 column, Vector3 top, List<Vector3> body, Vector3 slot)> MassColumns(OmniCombineCore core, OmniCombineParams p, double t,
                                                                                         float reach, double place, float sizeStart, OmniCombineData data)
    {
        var pos = Enumerable.Range(0, 4).Select(k => new float[3 * core.VertexCount(k)]).ToArray();
        var morph = Enumerable.Range(0, 4).Select(k => new float[core.CrystalData(k).plateSite.Length]).ToArray();
        var centres = new float[12];
        core.Pose(Math.Min(t, OmniCombineCore.MorphEnds(p)), p, new D3(0.2, 0.5, 1).Unit, pos, new float[4], centres, morph);
        int mk = OmniCombineCore.Mass;
        var d = core.CrystalData(mk);
        var centre = new Vector3(centres[3 * mk], centres[3 * mk + 1], centres[3 * mk + 2]);
        int plates = d.plateSite.Length;
        var sum = new Vector3[plates]; var cnt = new int[plates]; var slotSum = new Vector3[plates];
        for (int i = 0; i < d.plate.Length; i++) { sum[d.plate[i]] += V(pos[mk], i) - centre; slotSum[d.plate[i]] += V(d.final, i); cnt[d.plate[i]]++; }
        float w = morph[mk].Average();
        float thickness = data.depth.massThickness + (data.depth.omniThickness - data.depth.massThickness) * w;
        float k = (float)(reach + (1 - reach) * place), ls = k * (float)(sizeStart + (1 - sizeStart) * place);
        var outList = new List<(Vector3, Vector3, List<Vector3>, Vector3)>();
        for (int q = 0; q < plates; q++)
        {
            Vector3 site = Vector3.zero, slot = Vector3.zero; int n = 0;
            for (int s = 0; s < plates; s++) if (d.plateSite[s] == d.plateSite[q]) { site += sum[s]; slot += slotSum[s]; n += cnt[s]; }
            site /= n; slot /= n;
            var own = sum[q] / cnt[q];
            var c = Vector3.Lerp(own, site, morph[mk][q]);
            var dir = c.normalized;
            // the thin plate's top face: its farthest point along the column, above the column centre
            float topH = 0f;
            for (int i = 0; i < d.plate.Length; i++) if (d.plate[i] == q) topH = Math.Max(topH, Vector3.Dot(V(pos[mk], i) - centre - c, dir));
            var body = new List<Vector3>();
            for (int i = 0; i < d.plate.Length; i++)
            {
                if (d.plate[i] != q) continue;
                var local = V(pos[mk], i) - centre - c;
                local += dir * ((Vector3.Dot(local, dir) - topH) * (thickness - 1));
                body.Add(centre + dir * (c.magnitude * k) + local * ls);
            }
            outList.Add((c, centre + c * k, body, slot));
        }
        return outList;
    }

    /// <summary>The Time crystal's vertices at <paramref name="t"/>, and the sphere round them.</summary>
    static (List<Vector3> verts, Vector3 centre, float radius) TimePose(OmniCombineCore core, OmniCombineParams p, double t)
    {
        var pos = Enumerable.Range(0, 4).Select(k => new float[3 * core.VertexCount(k)]).ToArray();
        core.Pose(Math.Min(t, OmniCombineCore.MorphEnds(p)), p, new D3(0.2, 0.5, 1).Unit, pos, new float[4]);
        var verts = Enumerable.Range(0, core.VertexCount(OmniCombineCore.Time)).Select(i => V(pos[OmniCombineCore.Time], i)).ToList();
        var centre = verts.Aggregate(Vector3.zero, (a, b) => a + b) / verts.Count;
        return (verts, centre, verts.Max(v => (v - centre).magnitude));
    }

    /// <summary>A saved float on a Unity asset (a material's m_Floats row or a MonoBehaviour field).</summary>
    static float Saved(string path, string name)
    {
        var m = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(path), $@"^\s*-?\s*{name}: ([-0-9.eE]+)\s*$",
                                                           System.Text.RegularExpressions.RegexOptions.Multiline);
        if (!m.Success) throw new Exception($"{path}: no {name}");
        return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Whether a Mass prism's top is at the omni's shell (0.85-1.15 of its slot's radius), and how far off its own
    /// hole's axis it is there (deg).</summary>
    static bool AtShell((Vector3 column, Vector3 top, List<Vector3> body, Vector3 slot) c, out float off)
    {
        float r = c.top.magnitude / c.slot.magnitude;
        off = Vector3.Angle(c.top, c.slot);
        return r > 0.85f && r < 1.15f;
    }

    /// <summary>Over a play: how far each plate's centre is from (the pose without spacing + its slot direction pushed out by
    /// w x spacing x (1 - socketed in), carried by the crystal's turn), the worst landing error, and how far Space moved.</summary>
    static (float pushErr, float landed, float spaceMoved) SpacingStats(OmniCombineCore core, OmniCombineParams p, OmniCombineParams none)
    {
        var pos = Enumerable.Range(0, 4).Select(k => new float[3 * core.VertexCount(k)]).ToArray();
        var pos0 = Enumerable.Range(0, 4).Select(k => new float[3 * core.VertexCount(k)]).ToArray();
        var morph = Enumerable.Range(0, 4).Select(k => new float[core.CrystalData(k).plateSite.Length]).ToArray();
        var effect = new float[4];
        var cam = new D3(0.2, 0.5, 1).Unit;
        float pushErr = 0f, landed = 0f, spaceMoved = 0f;
        double end = OmniCombineCore.MorphEnds(p);
        for (double t = 0; t <= end + 0.01; t += 1.0 / 30)
        {
            double tt = Math.Min(t, end);
            core.Pose(tt, p, cam, pos, effect, null, morph);
            core.Pose(tt, none, cam, pos0, effect);
            for (int i = 0; i < pos[0].Length; i++) spaceMoved = Math.Max(spaceMoved, Math.Abs(pos[0][i] - pos0[0][i]));
            var plain = PlateCentres(core, pos0, morph).ToList();
            int j = 0;
            foreach (var pc in PlateCentres(core, pos, morph))
            {
                core.CrystalFrame(pc.k, tt, p, out _, out var spinAxis, out double spin, out var tumbleAxis, out double tumble);
                double w = pc.w, settle = OmniCombineCore.SocketIn(pc.k, tt, p);
                var push = Quaternion.AngleAxis((float)(tumble * 180 / Math.PI), V(tumbleAxis)) * (Quaternion.AngleAxis((float)(spin * 180 / Math.PI), V(spinAxis))
                           * (pc.slot * (float)(w * p.SocketSpacing * (1 - settle))));
                pushErr = Math.Max(pushErr, (pc.centre - (plain[j].centre + push)).magnitude);
                if (tt >= end) landed = Math.Max(landed, (pc.centre - pc.slot).magnitude);
                j++;
            }
        }
        return (pushErr, landed, spaceMoved);
    }

    /// <summary>Per crystal, the share of late-morph frames (w 0.6..1) in which a plate whose hole faces the camera is
    /// inside the omni (more than 2% below its slot radius): the "coming in from inside" a viewer can see.</summary>
    static float[] HiddenStats(OmniCombineCore core, OmniCombineParams p, Func<double, D3> cam, Func<double, double> cameraAzimuth)
    {
        var pos = Enumerable.Range(0, 4).Select(k => new float[3 * core.VertexCount(k)]).ToArray();
        var morph = Enumerable.Range(0, 4).Select(k => new float[core.CrystalData(k).plateSite.Length]).ToArray();
        var effect = new float[4];
        var inside = new float[4]; var seen = new float[4];
        double end = OmniCombineCore.MorphEnds(p);
        for (double t = 0; t <= end; t += 1.0 / 30)
        {
            var toward = cam(t);
            p.CameraAzimuth = cameraAzimuth(t);
            core.Pose(t, p, toward, pos, effect, null, morph);
            foreach (var pc in PlateCentres(core, pos, morph))
            {
                if (pc.w < 0.6f || pc.w >= 1f || Vector3.Dot(pc.slot.normalized, V(toward)) <= 0.15f) continue;
                seen[pc.k]++;
                if (pc.centre.magnitude < 0.98f * pc.slot.magnitude) inside[pc.k]++;
            }
        }
        for (int k = 1; k < 4; k++) inside[k] /= Math.Max(1, seen[k]);
        return inside;
    }

    static int Finish()
    {
        Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILED");
        return failures == 0 ? 0 : 1;
    }
}
