// Checks the SHIPPED Charge light core (ChargeArcs.cs) on the real Charge crystal and omni meshes (meshes.json, from
// dump.py):
//  1. the Charge crystal reads as 12 blooms of 5 petals; the omni as 12 pentagons;
//  2. the seam net is the dodecahedron's: 20 junctions, 30 seams, three seams at every junction, each junction among
//     three mutually neighbouring blooms;
//  3. a seam never reaches a bloom's centre (its five petal tips): it runs in the gap, beside the short sides only;
//  4. the seam walk is one continuous bolt: each seam starts where the last ended, never straight back, never a fifth
//     seam round one bloom (it would circle it) - over ten minutes of walking;
//  5. the walk lights only petals near the bolt;
//  6. the flux arcs chain: every source is the last sink, never straight back to the last source, each leap 55-125 deg;
//  7. a flux path's five strands start ON the source's corners, end ON the sink's, are one beam through the middle,
//     and are paired one to one;
//  8. a discharge lights only its source and sink pentagons;
//  9. the omni's pentagon arc ends sit on the 1.1x thicker pentagons (CrystalSpaceSquares' thickening);
// 10. the plasma (the Squirrel's skim crackle is the reference): one continuous strip per bolt with shared joints, ends
//     pinned, bending smoothly, flowing between frames without a jump; the seam trail never jumps as its tail falls away;
// 11. a flux arc passing through a raised Mass column lights it; sealed prisms are never struck; a prism planted on the
//     beam lights and one off it does not.
// Negative controls: a crystal with one petal missing is refused; a walk with the fifth-seam rule's state cleared would
// circle (checked by counting how often a plain random walk does).
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

    static Vector3 V(JsonElement e) => new(e[0].GetSingle(), e[1].GetSingle(), e[2].GetSingle());

    static int Main()
    {
        var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "../../../meshes.json"))).RootElement;
        (Vector3[] p, int[] t) Load(string key) => (doc.GetProperty(key).GetProperty("positions").EnumerateArray().Select(V).ToArray(),
                                                     doc.GetProperty(key).GetProperty("triangles").EnumerateArray().Select(x => x.GetInt32()).ToArray());
        var settings = ChargeArcSettings.Lab;

        // ── 1-3: the Charge crystal's sites and seam net ──
        var (cp, ct) = Load("charge");
        bool built = ChargeSites.TryBuild(cp, ct, 1f, out var charge, out var problem);
        Check("the Charge crystal reads as 12 blooms of 5 petals", built && charge.Kind == ChargeSiteKind.Blooms && charge.FacesOfSite.All(f => f.Length == 5), problem ?? "");
        if (!built) return Finish();
        Check("the seam net is the dodecahedron's: 20 junctions, 30 seams, 3 at each junction",
            charge.Junctions.Length == 20 && charge.Seams.Length == 30 && charge.SeamsOfJunction.All(e => e.Length == 3),
            $"{charge.Junctions.Length} / {charge.Seams.Length}");
        bool triples = charge.JunctionSites.All(j => charge.Neighbours[j[0]].Contains(j[1]) && charge.Neighbours[j[0]].Contains(j[2]) && charge.Neighbours[j[1]].Contains(j[2]));
        Check("each junction is among three mutually neighbouring blooms", triples);
        // A bloom's centre: the mean of its petals' tips, which sit within ~0.1 of each other. The seam must keep well clear.
        float clearance = float.MaxValue;
        foreach (var seam in charge.Seams)
            foreach (var q in seam.Points)
                for (int s = 0; s < ChargeSites.SiteCount; s++)
                {
                    clearance = Math.Min(clearance, (q.normalized - charge.Axes[s]).magnitude);
                }
        // the bloom's own radius on the unit sphere: its petals' farthest corner from its axis
        float bloomReach = charge.Faces.Max(f => f.Ring.Max(c => (c.normalized - charge.Axes[f.Site]).magnitude));
        Check("a seam never reaches a bloom's centre (it runs in the gap, beside the short sides)", clearance > 0.6f * bloomReach,
            $"nearest seam point {clearance:F3} from a bloom axis; a bloom reaches {bloomReach:F3}");
        float sideOk = 0f;
        foreach (var seam in charge.Seams)
            foreach (var q in seam.Points)
            {
                // the two blooms a seam runs between are the two nearest it
                var order = Enumerable.Range(0, 12).OrderByDescending(s => Vector3.Dot(q.normalized, charge.Axes[s])).Take(3).ToArray();
                if (order.Contains(seam.SiteA) && order.Contains(seam.SiteB)) sideOk++;
            }
        Check("every seam point lies between its own two blooms", sideOk == charge.Seams.Length * 9f, $"{sideOk}/{charge.Seams.Length * 9}");

        // ── 4-5: the seam walk ──
        var walker = new ChargeSeamWalker(charge, 12345, 0f);
        var history = new SortedDictionary<int, ChargeSeamWalker.Leg>();
        var writer = new ChargeArcMeshWriter();
        var light = new float[charge.Faces.Length];
        int litFar = 0, litFrames = 0, frames = 0, bolts = 0;
        for (float t = 0f; t < 600f; t += 1f / 30f)
        {
            walker.Advance(t, settings);
            foreach (var leg in walker.Legs) history[leg.Serial] = leg;
            if (frames++ % 7 != 0) continue;
            writer.Clear();
            var eye = new Vector3(0.3f, 0.2f, 1f).normalized * charge.Radius * 6f;
            bolts += ChargeArcs.SeamWalk(charge, walker, t, settings, writer, eye, light);
            // which seam points are lit now (struck, not faded)
            var live = walker.Legs.Where(l => l.Start <= t && t - l.Struck < settings.SeamFade).SelectMany(l => charge.Seams[l.Seam].Points).ToList();
            for (int f = 0; f < light.Length; f++)
            {
                if (light[f] <= 0f) continue;
                litFrames++;
                float near = live.Count == 0 ? float.MaxValue : charge.Faces[f].Back.Min(b => live.Min(q => (b - q).magnitude));
                if (near > 3f * charge.Faces[f].Sigma) litFar++;
            }
        }
        var legs = history.Values.ToList();
        int breaks = 0, backs = 0, circles = 0;
        for (int i = 1; i < legs.Count; i++)
        {
            var a = charge.Seams[legs[i - 1].Seam]; var b = charge.Seams[legs[i].Seam];
            int end = legs[i - 1].Forward ? a.B : a.A, start = legs[i].Forward ? b.A : b.B;
            if (end != start) breaks++;
            if (legs[i].Seam == legs[i - 1].Seam) backs++;
            if (i >= 4)
                for (int s = 0; s < 12; s++)
                    if (Enumerable.Range(i - 4, 5).All(j => charge.Seams[legs[j].Seam].Touches(s))) circles++;
        }
        Check("the seam walk is one continuous bolt (each seam starts where the last ended)", breaks == 0 && legs.Count > 1000, $"{legs.Count} seams, {breaks} breaks");
        Check("the seam walk never turns straight back", backs == 0, $"{backs}");
        Check("the seam walk never takes a fifth seam round one bloom", circles == 0, $"{circles}");
        Check("the seam walk lights only petals near the bolt", litFar == 0 && litFrames > 0, $"{litFrames} lit petal-frames, {litFar} far from the bolt");
        Check("the seam walk draws bolts", bolts > 0, $"{bolts}");
        // negative control: a plain random walk (no fifth-seam rule) does circle
        var rnd = new System.Random(7); int at = 0, prev = -1, plainCircles = 0; var recent = new List<int>();
        for (int i = 0; i < 5000; i++)
        {
            var opts = charge.SeamsOfJunction[at].Where(e => e != prev).ToList();
            int e = opts[rnd.Next(opts.Count)];
            recent.Add(e); if (recent.Count > 5) recent.RemoveAt(0);
            if (recent.Count == 5) for (int s = 0; s < 12; s++) if (recent.All(r => charge.Seams[r].Touches(s))) plainCircles++;
            at = charge.Seams[e].A == at ? charge.Seams[e].B : charge.Seams[e].A; prev = e;
        }
        Check("negative control: a walk without the rule circles a bloom", plainCircles > 0, $"{plainCircles} in 5000 seams");

        // ── 6-9: the omni's flux arcs ──
        var (op, ot) = Load("omni");
        built = ChargeSites.TryBuild(op, ot, 1.1f, out var omni, out problem);
        Check("the omni reads as 12 pentagons", built && omni.Kind == ChargeSiteKind.Pentagons, problem ?? "");
        if (!built) return Finish();
        var chain = new ChargeFluxChain(omni, 99);
        int chainBreaks = 0, straightBack = 0, badAngle = 0;
        ChargeFluxChain.Discharge last = default;
        for (long k = 0; k < 2000; k++)
        {
            chain.TryGet(k, settings, out var d);
            float ang = Vector3.Angle(omni.Axes[d.Source], omni.Axes[d.Sink]);
            if (ang < 55f || ang > 125f) badAngle++;
            if (k > 0 && d.Source != last.Sink) chainBreaks++;
            if (k > 0 && d.Sink == last.Source) straightBack++;
            last = d;
        }
        Check("every discharge starts from the last one's sink", chainBreaks == 0, $"{chainBreaks}");
        Check("a discharge never leaps straight back", straightBack == 0, $"{straightBack}");
        Check("every leap is 55-125 degrees", badAngle == 0, $"{badAngle}");
        float endErr = 0f, midErr = 0f; bool bijective = true;
        for (int s = 0; s < 12; s++)
            for (int d = 0; d < 12; d++)
            {
                if (s == d) continue;
                float ang = Vector3.Angle(omni.Axes[s], omni.Axes[d]);
                if (ang < 55f || ang > 125f) continue;
                var path = omni.PathOf(s, d);
                var sinkHits = new HashSet<int>();
                for (int k = 0; k < 5; k++)
                {
                    endErr = Math.Max(endErr, omni.Corners[s].Min(c => (path.Strand(k, 0f, 0.3f) - c).magnitude));
                    var atSink = path.Strand(k, 1f, 0.3f);
                    int hit = Enumerable.Range(0, 5).OrderBy(j => (omni.Corners[d][j] - atSink).magnitude).First();
                    endErr = Math.Max(endErr, (omni.Corners[d][hit] - atSink).magnitude);
                    sinkHits.Add(hit);
                    midErr = Math.Max(midErr, (path.Strand(k, 0.5f, 0.3f) - path.Centre(0.5f)).magnitude);
                }
                bijective &= sinkHits.Count == 5;
            }
        Check("each strand starts on a source corner and ends on a sink corner", endErr < 1e-4f * omni.Radius, $"worst {endErr / omni.Radius:E2} R");
        Check("through the middle the five strands are one beam", midErr < 1e-5f * omni.Radius, $"worst {midErr / omni.Radius:E2} R");
        Check("the strands pair the corners one to one", bijective);

        var omniLight = new float[omni.Faces.Length];
        int strayLit = 0, discharges = 0;
        var chain2 = new ChargeFluxChain(omni, 5);
        float gap = settings.FluxGap, gone = settings.FluxCharge + settings.FluxStrike + settings.FluxHold + settings.FluxFade;
        int beamStrips = 0;
        for (float t = 0f; t < 120f; t += 0.05f)
        {
            writer.Clear();
            discharges += ChargeArcs.Flux(omni, chain2, t, settings, writer, new Vector3(0f, 0.4f, 1f) * omni.Radius * 6f, omniLight);
            var live = new HashSet<int>();
            for (long k = (long)Math.Floor(t / gap); k >= 0 && t - k * gap <= gone; k--)
            {
                chain2.TryGet(k, settings, out var d);
                live.Add(d.Source); live.Add(d.Sink);
            }
            for (int f = 0; f < omniLight.Length; f++) if (omniLight[f] > 0f && !live.Contains(omni.Faces[f].Site)) strayLit++;
            if (writer.Positions.Any(p => float.IsNaN(p.x) || float.IsInfinity(p.x))) beamStrips = -1000000;
            beamStrips += writer.Data.Count(x => x.z == 0f && x.w > 0f);
        }
        Check("a discharge lights only its source and sink", strayLit == 0 && discharges > 0, $"{discharges} discharge-frames, {strayLit} stray");
        Check("the flux bolts are finite", beamStrips > 0, $"{beamStrips} strip vertices");

        // pentagon arc ends on the THICKER pentagons: thickening 1.1 moves a corner out along the plate normal
        built = ChargeSites.TryBuild(op, ot, 1f, out var omniThin, out _);
        float moved = 0f, along = 1f;
        for (int s = 0; s < 12; s++)
        {
            var d = omni.Corners[s][0] - omniThin.Corners[s].OrderBy(c => (c - omni.Corners[s][0]).magnitude).First();
            moved = Math.Max(moved, d.magnitude);
            along = Math.Min(along, Math.Abs(Vector3.Dot(d.normalized, omni.Axes[s])));
        }
        Check("the omni's arc ends sit on the 1.1x thicker pentagons (moved out along the plate)", built && moved > 1e-4f * omni.Radius && along > 0.95f,
            $"moved {moved / omni.Radius:E2} R, cos to the axis {along:F3}");

        // ── 10: the plasma (the Squirrel's skim crackle is the reference) ──
        var straight = new List<Vector3> { Vector3.zero, new Vector3(1f, 0f, 0f) };
        var eyeZ = new Vector3(0.5f, 0f, 10f);
        writer.Clear();
        writer.Plasma(straight, 2, null, null, 1f, 3f, 0.025f, 1, 3.3f, settings, eyeZ, 1f);
        int verts = writer.Positions.Count, ribbonTris = writer.Indices.Count / 3;
        // two ribbons (the bolt and its sub-filament), each one strip of shared vertex pairs: 2(n+1) vertices, 2n triangles
        int n1 = verts / 4 - 1;
        Check("a bolt is one continuous strip (+ its sub-filament): every joint's vertices are shared", verts % 4 == 0 && ribbonTris == 4 * n1 && n1 >= 30,
              $"{verts} vertices, {ribbonTris} triangles, {n1} pieces per ribbon");
        var mid = Enumerable.Range(0, n1 + 1).Select(k => (writer.Positions[2 * k] + writer.Positions[2 * k + 1]) * 0.5f).ToList();
        Check("its ends are pinned where it is aimed", (mid[0] - straight[0]).magnitude < 1e-5f && (mid[n1] - straight[1]).magnitude < 1e-5f,
              $"{(mid[0] - straight[0]).magnitude:E1} / {(mid[n1] - straight[1]).magnitude:E1}");
        float worstTurn = 0f;
        for (int k = 1; k < n1; k++) worstTurn = Math.Max(worstTurn, Vector3.Angle(mid[k] - mid[k - 1], mid[k + 1] - mid[k]));
        float bend = mid.Max(q => Math.Abs(q.y) + Math.Abs(q.z));
        Check("it bends smoothly (no kink between neighbouring pieces) but does bend", worstTurn < 20f && bend > 0.003f,
              $"sharpest turn {worstTurn:F1} deg, off the line by up to {bend:F3}");
        writer.Clear();
        writer.Plasma(straight, 2, null, null, 1f, 3f, 0.025f, 1, 3.3f + 1f / 60f, settings, eyeZ, 1f);
        float jump = Enumerable.Range(0, n1 + 1).Max(k => ((writer.Positions[2 * k] + writer.Positions[2 * k + 1]) * 0.5f - mid[k]).magnitude);
        Check("from one frame to the next the plasma flows, never jumps", jump < 0.004f, $"worst move {jump:E2} radii in 1/60 s");

        // the seam trail: one ribbon whose shape never jumps between frames, even as its tail falls away
        var trailWalker = new ChargeSeamWalker(charge, 99, 0f);
        List<Vector3> prevTrail = null;
        float worstTrail = 0f, worstTailStart = 0f;
        int trailFrames = 0;
        for (float t = 0f; t < 30f; t += 1f / 60f)
        {
            trailWalker.Advance(t, settings);
            writer.Clear();
            ChargeArcs.SeamWalk(charge, trailWalker, t, settings, writer, eyeZ * charge.Radius, light);
            int v = writer.Positions.Count;
            int faceVerts = 0;   // SeamWalk writes no faces; the player adds them after
            int ribbon = (v - faceVerts) / 2;   // the main ribbon is the first half
            var centres = Enumerable.Range(0, ribbon / 2).Select(k => (writer.Positions[2 * k] + writer.Positions[2 * k + 1]) * 0.5f).ToList();
            var brightness = Enumerable.Range(0, ribbon / 2).Select(k => writer.Data[2 * k].w).ToList();
            float peak = brightness.Count > 0 ? brightness.Max() : 0f;
            if (prevTrail != null && centres.Count > 2 && prevTrail.Count > 2)
            {
                // the trail BEHIND the head (the head legitimately races a whole seam in 0.07 s) lies where it lay last
                // frame: dropping the tail must not slide the plasma's shape along it
                float w = 0f; int wi = -1;
                for (int ci = 0; ci < centres.Count / 2; ci++)
                {
                    if (brightness[ci] < 0.2f * peak) continue;   // only plasma that can be seen
                    var c = centres[ci];
                    float dd = Enumerable.Range(0, prevTrail.Count - 1).Min(i => SegmentDistance(c, prevTrail[i], prevTrail[i + 1]));
                    if (dd > w) { w = dd; wi = ci; }
                }
                if (w > worstTrail && Environment.GetEnvironmentVariable("TRAIL_DEBUG") != null)
                    Console.WriteLine($"  t={t:F3} worst {w / charge.Radius:E2} R at {wi}/{centres.Count} b={brightness[Math.Max(wi,0)]/peak:F2} b1={brightness[Math.Min(1,brightness.Count-1)]/peak:F2} (prev {prevTrail.Count}), legs {string.Join(",", trailWalker.Legs.Select(l => $"{l.Serial}:{l.Start:F2}-{l.Struck:F2}"))}");
                worstTrail = Math.Max(worstTrail, w);
                trailFrames++;
            }
            prevTrail = centres;
            if (t > 2f && brightness.Count > 2 && peak > 0f) worstTailStart = Math.Max(worstTailStart, brightness[0] / peak);
        }
        Check("the visible seam trail never jumps between frames, even as its tail falls away", worstTrail < 0.005f * charge.Radius && trailFrames > 1000,
              $"worst {worstTrail / charge.Radius:E2} R over {trailFrames} frames");

        Check("the seam trail's tail always fades to nothing (no cut-off end)", worstTailStart < 0.05f, $"brightest tail end {worstTailStart:F3} of the peak");

        // ── 11: a flux arc through a raised Mass column lights it ──
        // The omni's triangular prisms (6 corners), each at the top of its column as the shader carries it (reach 3, size 0.6).
        var omniTris = TriangularPrisms(op, ot);
        Check("the omni has its 20 triangular prisms", omniTris.Count == 20, $"{omniTris.Count}");
        // A prism at column place u stands at k = lerp(3, 1, u) times its plate centre, scaled k * lerp(0.6, 1, u)
        // (OmniDepthPrismShader / CrystalDepthPrisms.ColumnBody). Fully up (u = 0) it is 3 radii out, far above the arcs
        // (they bow out at most ~1.46 R); the ones an arc can run through are part-way up, near the silhouette.
        var strike = new float[omniTris.Count];
        int struckFrames = 0, beamFrames = 0;
        float strongest = 0f;
        var topStruck = 0;
        foreach (float u in new[] { 0f, 0.6f, 0.7f, 0.8f, 0.9f })
        {
            float k = 3f + (1f - 3f) * u, size = k * (0.6f + 0.4f * u);
            var centresUp = omniTris.Select(p => p.centre * k).ToArray();
            var radiiUp = omniTris.Select(p => p.bound * size).ToArray();
            var strikeChain = new ChargeFluxChain(omni, 5);
            for (float t = 0f; t < 120f; t += 0.05f)
            {
                ChargeArcs.FluxStrikes(omni, strikeChain, t, settings, centresUp, radiiUp, centresUp.Length, strike);
                bool any = strike.Any(x => x > 0f);
                if (u == 0f) { if (any) topStruck++; continue; }
                if (any) struckFrames++;
                strongest = Math.Max(strongest, strike.Max());
                beamFrames++;
            }
        }
        Check("flux arcs do pass through Mass prisms part-way up their columns, and those frames light the prisms", struckFrames > 0 && strongest > 1f,
              $"{struckFrames} of {beamFrames} frames strike one, strongest {strongest:F2}");
        Check("a prism at the very top of its column is above every arc", topStruck == 0, $"{topStruck} frames");
        var seated = omniTris.Select(p => p.centre).ToArray();
        var seatedRadii = omniTris.Select(p => p.bound).ToArray();
        int seatedStruck = 0;
        var chain3 = new ChargeFluxChain(omni, 5);
        for (float t = 0f; t < 120f; t += 0.05f)
        {
            ChargeArcs.FluxStrikes(omni, chain3, t, settings, seated, seatedRadii, seated.Length, strike);
            if (strike.Any(x => x > 0f)) seatedStruck++;
        }
        Check("negative control: sealed in their holes, no prism is ever struck (the arcs bow over the shell)", seatedStruck == 0,
              $"{seatedStruck} frames");
        // a prism planted on the beam's middle is struck at full; moved off by twice its radius, not at all
        var plantChain = new ChargeFluxChain(omni, 8);
        plantChain.TryGet(0, settings, out var firstDischarge);
        var plantPath = omni.PathOf(firstDischarge.Source, firstDischarge.Sink);
        float tHold = settings.FluxCharge + settings.FluxStrike + 0.1f;
        var on = new[] { plantPath.Centre(0.5f) };
        var off = new[] { plantPath.Centre(0.5f) * 1.6f };
        var r1 = new[] { 0.05f * omni.Radius };
        var one = new float[1];
        ChargeArcs.FluxStrikes(omni, plantChain, tHold, settings, on, r1, 1, one);
        float onValue = one[0];
        ChargeArcs.FluxStrikes(omni, plantChain, tHold, settings, off, r1, 1, one);
        Check("a prism on the beam lights; one well off it does not", onValue > 2f && one[0] == 0f, $"{onValue:F2} / {one[0]:F2}");

        // negative control: one petal missing is refused
        var keep = new List<int>();
        var tris = ct.ToList();
        // drop every triangle that touches the first petal's vertices (the first 10 corners' welded plate)
        var first = cp[ct[0]];
        var nearFirst = new HashSet<int>(Enumerable.Range(0, cp.Length).Where(i => (cp[i] - first).magnitude < 0.12f));
        for (int i = 0; i < ct.Length; i += 3) if (!nearFirst.Contains(ct[i]) && !nearFirst.Contains(ct[i + 1]) && !nearFirst.Contains(ct[i + 2])) keep.AddRange(new[] { ct[i], ct[i + 1], ct[i + 2] });
        bool refused = !ChargeSites.TryBuild(cp, keep.ToArray(), 1f, out _, out var why);
        Check("negative control: a crystal with a petal missing is refused", refused, why ?? "accepted");
        return Finish();
    }

    static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        var ab = b - a;
        float u = Math.Clamp(Vector3.Dot(p - a, ab) / Math.Max(1e-12f, Vector3.Dot(ab, ab)), 0f, 1f);
        return (a + ab * u - p).magnitude;
    }

    /// <summary>The 6-corner plates (triangular prisms) of a mesh: centre and farthest corner from it.</summary>
    static List<(Vector3 centre, float bound)> TriangularPrisms(Vector3[] p, int[] t)
    {
        var weld = new Dictionary<(int, int, int), int>();
        var welds = new List<Vector3>();
        var of = new int[p.Length];
        for (int i = 0; i < p.Length; i++)
        {
            var key = ((int)Math.Round(p[i].x * 1e5), (int)Math.Round(p[i].y * 1e5), (int)Math.Round(p[i].z * 1e5));
            if (!weld.TryGetValue(key, out int w)) { weld[key] = w = welds.Count; welds.Add(p[i]); }
            of[i] = w;
        }
        var parent = Enumerable.Range(0, welds.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        for (int i = 0; i + 2 < t.Length; i += 3) { parent[Find(of[t[i]])] = Find(of[t[i + 1]]); parent[Find(of[t[i + 2]])] = Find(of[t[i + 1]]); }
        return Enumerable.Range(0, welds.Count).GroupBy(Find).Where(g => g.Count() == 6).Select(g =>
        {
            var c = g.Aggregate(Vector3.zero, (a, w) => a + welds[w]) / 6f;
            return (c, g.Max(w => (welds[w] - c).magnitude));
        }).ToList();
    }

    static int Finish()
    {
        Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILED");
        return failures == 0 ? 0 : 1;
    }
}
