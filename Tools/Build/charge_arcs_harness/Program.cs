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
// 10. the mesh writer: a thick beam is three strips (halo, body, core), a thin bolt two; nothing non-finite.
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

        // ── 10: the writer ──
        writer.Clear();
        var line = new List<Vector3> { Vector3.zero, new Vector3(0.001f, 0f, 0f) };
        writer.Bolt(line, 2, 1f, 0f, 3f, 1, new Vector3(0, 0, 10), 1f, 0.012f, 0);
        int thick = writer.Positions.Count;
        writer.Clear();
        writer.Bolt(line, 2, 1f, 0f, 2f, 1, new Vector3(0, 0, 10), 1f, 0.012f, 0);
        int thin = writer.Positions.Count;
        Check("a thick beam is three strips (halo, body, core); a thin bolt two", thick == 12 && thin == 8, $"{thick} / {thin} vertices");

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

    static int Finish()
    {
        Console.WriteLine(failures == 0 ? "ALL PASS" : $"{failures} FAILED");
        return failures == 0 ? 0 : 1;
    }
}
