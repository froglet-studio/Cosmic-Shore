// Runs the SHIPPED ThresherHullForm offline: the parts the tail animates all exist, every face is
// wound outward (a parity ray test inside each closed piece — the hull has non-convex pieces: the
// hitch is a torus, the scythe a curved blade), the scythe's segments join end to end, the hitch sits
// under the tail fork, the hull is the size of the Squirrel it replaced, every element moves the hull,
// blend@1 == the extreme, and the baked bounds hold every element-weight corner.
// `--obj <dir>` also writes base.obj + one obj per element extreme (two materials: body, accent).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

static class Driver
{
    static int _fail;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "  ok   " : "  FAIL ") + what); if (!ok) _fail++; }

    static int Main(string[] args)
    {
        var s = ThresherHullForm.Defaults;
        var parts = ThresherHullForm.Generate(s);
        Console.WriteLine($"thresher hull: {parts.Count} parts, {parts.Sum(p => p.Verts.Count)} verts, " +
                          $"{parts.Sum(p => p.Body.Count + p.Accent.Count) / 3} tris");

        Check(parts[0].Kind == ThresherHullForm.PartKind.Core && parts[0].Name == "ThresherBody", "part 0 is the body");
        Check(parts.Count(p => p.Kind == ThresherHullForm.PartKind.Pectoral) == 2, "two pectorals");
        Check(parts.Count(p => p.Kind == ThresherHullForm.PartKind.TailBase) == 1, "one tail base");
        Check(parts.Count(p => p.Kind == ThresherHullForm.PartKind.Lobe) == s.LobeSegments, "one part per scythe segment");
        Check(parts.Select(p => p.Name).Distinct().Count() == parts.Count, "part names are unique (the builder resolves by name)");
        Check(parts[0].Accent.Count > 0, "the body carries accents (dorsal, eyes)");
        Check(parts.First(p => p.Kind == ThresherHullForm.PartKind.TailBase).Accent.Count > 0, "the hitch is an accent");
        Check(parts.Where(p => p.Kind == ThresherHullForm.PartKind.Lobe).All(p => p.Accent.Count > 0), "every scythe segment carries its blade");

        // Winding: a point a hair OUTSIDE each face (along its front) must be outside its closed piece —
        // an even number of crossings along a ray from it.
        int inward = 0, total = 0;
        foreach (var p in parts)
            foreach (var piece in Pieces(p))
                foreach (var t in piece)
                {
                    var a = p.Verts[t[0]]; var b = p.Verts[t[1]]; var c = p.Verts[t[2]];
                    var n = Vector3.Cross(b - a, c - a);
                    if (n.magnitude < 1e-9f) continue;
                    n = n / n.magnitude;
                    var q = (a + b + c) / 3f + n * 1e-3f;
                    var dir = (n + new Vector3(0.0123f, 0.0371f, 0.0217f)).normalized;
                    int hits = 0;
                    foreach (var u in piece)
                        if (!ReferenceEquals(u, t) && RayHits(q, dir, p.Verts[u[0]], p.Verts[u[1]], p.Verts[u[2]])) hits++;
                    if (hits % 2 == 1) inward++;
                    total++;
                }
        Check(inward == 0, $"every face wound outward ({inward} of {total} face inward)");
        Check(parts.All(p => p.Normals.All(nm => Math.Abs(nm.magnitude - 1f) < 1e-3f)), "every normal is unit length");

        // The scythe is continuous: each segment starts where the last ended.
        var lobes = parts.Where(p => p.Kind == ThresherHullForm.PartKind.Lobe).ToList();
        float gap = 0f;
        for (int k = 1; k < lobes.Count; k++)
            gap = Math.Max(gap, (lobes[k].Pivot - ThresherHullForm.LobePoint(s, k / (float)s.LobeSegments)).magnitude);
        Check(gap < 1e-4f, $"scythe segments hinge at the previous segment's end (worst {gap:E1})");
        float arc = 0f; var prev = ThresherHullForm.LobePoint(s, 0f);
        for (int i = 1; i <= 200; i++) { var pt = ThresherHullForm.LobePoint(s, i / 200f); arc += (pt - prev).magnitude; prev = pt; }
        Check(Math.Abs(arc - s.LobeLength) < 0.01f * s.LobeLength, $"the scythe's arc length is LobeLength ({arc:F3} vs {s.LobeLength})");

        // The hitch hangs under the fork, behind the body, inside the tail base's reach.
        var hitch = ThresherHullForm.HitchCenter(s);
        var fork = ThresherHullForm.ForkPoint(s);
        Check(hitch.y < fork.y && hitch.z < ThresherHullForm.PeduncleZ(s), $"the hitch is under the tail, behind the body ({hitch.x:F2}, {hitch.y:F2}, {hitch.z:F2})");

        // Size.
        var all = parts.SelectMany(p => p.Verts).ToList();
        float minZ = all.Min(v => v.z), maxZ = all.Max(v => v.z), width = all.Max(v => v.x) - all.Min(v => v.x);
        float minY = all.Min(v => v.y), maxY = all.Max(v => v.y);
        Console.WriteLine($"  length {maxZ - minZ:F2} u (snout {maxZ:F2}, tail {minZ:F2}), span {width:F2} u, height {maxY - minY:F2} u (y {minY:F2}..{maxY:F2})");
        Check(maxZ - minZ > 6f && maxZ - minZ < 9.5f, "snout-to-tail between 6 and 9.5 u");
        Check(width > 2.5f && width < 4.5f, "pectoral span between 2.5 and 4.5 u (the Squirrel's ~4 u across)");
        Check(maxZ > 0f && minZ < -s.BodyLength * 0.5f, "+Z is forward: the snout leads, the tail trails");

        var set = ThresherHullForm.BakeMorphSet(s);
        Check(true, "BakeMorphSet: all four extremes kept the topology");
        for (int e = 0; e < ThresherHullForm.MorphElements.Length; e++)
        {
            float travel = 0f;
            for (int p = 0; p < set.BaseParts.Count; p++)
                foreach (var d in set.Deltas[e][p].VertDeltas) travel = Math.Max(travel, d.magnitude);
            Check(travel > 0.05f, $"{ThresherHullForm.MorphElements[e]} moves the hull (max vertex travel {travel:F2} u)");
        }

        var verts = new List<Vector3>(); var normals = new List<Vector3>();
        float worstBlend = 0f; bool boundsOk = true;
        for (int e = 0; e < 4; e++)
        {
            var x = ThresherHullForm.Generate(ThresherHullForm.ApplyElementExtreme(s, ThresherHullForm.MorphElements[e]));
            var w = new float[4]; w[e] = 1f;
            for (int p = 0; p < x.Count; p++)
            {
                ThresherHullForm.BlendPart(set, p, w, verts, normals);
                for (int i = 0; i < verts.Count; i++) worstBlend = Math.Max(worstBlend, (verts[i] - x[p].Verts[i]).magnitude);
            }
        }
        Check(worstBlend < 1e-4f, $"blend at one weight = that extreme (worst {worstBlend:E1})");
        for (int corner = 0; corner < 16; corner++)
        {
            var w = new float[4]; for (int e = 0; e < 4; e++) w[e] = (corner >> e & 1) == 1 ? 1f : 0f;
            for (int p = 0; p < set.BaseParts.Count; p++)
            {
                ThresherHullForm.BlendPart(set, p, w, verts, normals);
                foreach (var v in verts)
                    if (v.x < set.BoundsMin[p].x - 1e-4f || v.y < set.BoundsMin[p].y - 1e-4f || v.z < set.BoundsMin[p].z - 1e-4f ||
                        v.x > set.BoundsMax[p].x + 1e-4f || v.y > set.BoundsMax[p].y + 1e-4f || v.z > set.BoundsMax[p].z + 1e-4f)
                        boundsOk = false;
            }
        }
        Check(boundsOk, "baked bounds contain all 16 element-weight corners");

        // Negative controls: a topology change is visible to the assert, and the winding test sees a flip.
        var bad = s; bad.Sides = 5;
        Check(ThresherHullForm.Generate(s).Sum(p => p.Verts.Count) != ThresherHullForm.Generate(bad).Sum(p => p.Verts.Count),
              "negative control: a topology change is visible to the assert");
        {
            var body = parts[0];
            var piece = Pieces(body).OrderByDescending(pc => pc.Count).First();
            var t = piece[0];
            var a = body.Verts[t[0]]; var b = body.Verts[t[2]]; var c = body.Verts[t[1]];   // flipped
            var n = Vector3.Cross(b - a, c - a); n = n / n.magnitude;
            var q = (a + b + c) / 3f + n * 1e-3f;
            var dir = (n + new Vector3(0.0123f, 0.0371f, 0.0217f)).normalized;
            int hits = piece.Count(u => !ReferenceEquals(u, t) && RayHits(q, dir, body.Verts[u[0]], body.Verts[u[1]], body.Verts[u[2]]));
            Check(hits % 2 == 1, "negative control: the parity test flags a flipped body face");
        }

        // The tail (ThresherSwimMath, 2026-10-19: no beat, the last links of the cord): posed as a chain it never
        // opens a joint; flying straight it never leaves its rest; a cord to the side lays it along the cord at a
        // phone's 30 fps as at 144; a cord dead ahead is not followed (no flip); a turn sways it and it settles.
        {
            var tail = ThresherSwimMath.Defaults;
            var tailBase = parts.First(p => p.Kind == ThresherHullForm.PartKind.TailBase);
            int n = 1 + lobes.Count;
            var rest = new Vector3[n]; rest[0] = tailBase.Pivot;
            for (int k = 0; k < lobes.Count; k++) rest[k + 1] = lobes[k].Pivot;
            Vector3 tipRest = ThresherHullForm.LobePoint(s, 1f);
            Vector3 restDir = tipRest - rest[0];
            float tailLength = restDir.magnitude;
            var rot = new Quaternion[n]; var pos = new Vector3[n];
            var joints = new ThresherSwimMath.Joint[n]; var yawT = new float[n]; var pitchT = new float[n];

            float worstJoint = 0f;
            for (int i = 0; i < 360; i++)
            {
                for (int j = 0; j < n; j++)
                    rot[j] = ThresherSwimMath.JointRotation(new ThresherSwimMath.Joint { Yaw = 100f * Mathf.Sin(i * 0.05f + j), Pitch = 45f * Mathf.Cos(i * 0.07f - j) });
                ThresherSwimMath.PoseChain(rest, rot, pos);
                for (int k = 0; k + 1 < n; k++)
                    worstJoint = Math.Max(worstJoint, (pos[k] + rot[k] * (rest[k + 1] - rest[k]) - pos[k + 1]).magnitude);
            }
            Check(worstJoint < 1e-4f, $"a bending tail never opens a joint (worst {worstJoint:E1} u)");

            ThresherSwimMath.CordBend(tail, restDir, restDir * 3f, tailLength, out float by, out float bp);
            ThresherSwimMath.JointTargets(by, bp, yawT, pitchT);
            float drift = 0f;
            for (int f = 0; f < 600; f++)
            {
                ThresherSwimMath.Step(tail, joints, yawT, pitchT, 0f, 0f, 0f, 1f / 60f);
                foreach (var j in joints) drift = Math.Max(drift, Math.Abs(j.Yaw) + Math.Abs(j.Pitch));
            }
            Check(drift < 1e-5f, $"flying straight, the cord behind, the tail never wiggles (worst {drift:E1} deg over 10 s)");

            Vector3 side = new Vector3(-restDir.z, 0f, restDir.x);   // the rest line turned square, to the pilot's right (+X)
            float Settle(float fps, Vector3 toCord, out float bend)
            {
                var js = new ThresherSwimMath.Joint[n];
                ThresherSwimMath.CordBend(tail, restDir, toCord, tailLength, out bend, out float pitch);
                ThresherSwimMath.JointTargets(bend, pitch, yawT, pitchT);
                for (int f = 0; f < (int)Math.Round(4f * fps); f++)
                    ThresherSwimMath.Step(tail, js, yawT, pitchT, 0f, 0f, 0f, 1f / fps);
                return js[n - 1].Yaw;
            }
            Vector3 backRight = (restDir.normalized + side.normalized) * 10f;
            float phone = Settle(30f, backRight, out float bend45), pc = Settle(144f, backRight, out _);
            Console.WriteLine($"  cord 45 deg to the right: bend {bend45:F1} deg; tip settles at {phone:F2} (30 fps) / {pc:F2} (144 fps)");
            Check(bend45 < -40f && Math.Abs(phone - bend45) < 0.5f && Math.Abs(phone - pc) < 0.5f,
                  "a cord to the right lays the tip along it, the same at 30 and 144 fps");
            float planted = Settle(60f, side * 10f, out float bend90);
            Console.WriteLine($"  planted (cord square to the right): tip at {planted:F1} deg");
            Check(planted < -60f && planted > -tail.MaxYawDegrees - 1f, "planted, the tail swings round toward the ball (fleet-scale, rule 24)");

            float ahead = ThresherSwimMath.CordBend(tail, restDir, -restDir * 3f + side * 0.01f, tailLength, out float ya, out _);
            ThresherSwimMath.CordBend(tail, restDir, -restDir * 3f - side * 0.01f, tailLength, out float yb, out _);
            Check(ahead == 0f && ya == 0f && yb == 0f, "a cord dead ahead is not followed, from either side (no flip)");

            var zero = new float[n];
            joints = new ThresherSwimMath.Joint[n];
            for (int f = 0; f < 30; f++) ThresherSwimMath.Step(tail, joints, zero, zero, 2f, 0f, 0f, 1f / 60f);
            float lag = joints[n - 1].Yaw, sway = 0f;
            for (int f = 0; f < 300; f++)
            {
                ThresherSwimMath.Step(tail, joints, zero, zero, 0f, 0f, 0f, 1f / 60f);
                sway = Math.Max(sway, joints[n - 1].Yaw);
            }
            Console.WriteLine($"  half a second of a 120 deg/s right turn: tip lags {lag:F1} deg, sways {sway:F1} past straight, settles at {joints[n - 1].Yaw:F3}");
            Check(lag < -5f && sway > 0.5f && Math.Abs(joints[n - 1].Yaw) < 0.05f, "a turn drags the tail, it sways back past straight, and settles");
        }

        // The wrecking ball (ThresherBallForm): an 80-facet core and twelve closed spikes, every face wound
        // outward, the core at radius 0.5 (the gameplay ball in the executor's scale), the tips where stated.
        {
            var bv = new List<Vector3>(); var bn = new List<Vector3>(); var bt = new List<int>();
            ThresherBallForm.Build(bv, bn, bt);
            var ball = new ThresherHullForm.Part { Name = "Ball" };
            ball.Verts.AddRange(bv); ball.Normals.AddRange(bn); ball.Body.AddRange(bt);
            var pieces = Pieces(ball);
            Console.WriteLine($"  ball: {bt.Count / 3} tris in {pieces.Count} closed pieces");
            Check(pieces.Count == 13, "the ball is a core plus twelve spikes");
            int badFaces = 0;
            foreach (var piece in pieces)
                foreach (var tr in piece)
                {
                    var a = bv[tr[0]]; var b = bv[tr[1]]; var c = bv[tr[2]];
                    var nn = Vector3.Cross(b - a, c - a); nn = nn / nn.magnitude;
                    var q = (a + b + c) / 3f + nn * 1e-4f;
                    var dir = (nn + new Vector3(0.0123f, 0.0371f, 0.0217f)).normalized;
                    if (piece.Count(u => !ReferenceEquals(u, tr) && RayHits(q, dir, bv[u[0]], bv[u[1]], bv[u[2]])) % 2 == 1) badFaces++;
                }
            Check(badFaces == 0, $"every ball face wound outward ({badFaces} inward)");
            float rmax = bv.Max(v => v.magnitude);
            float coreMax = pieces.OrderByDescending(pc => pc.Count).First().SelectMany(x => x).Max(i => bv[i].magnitude);
            Check(Math.Abs(coreMax - ThresherBallForm.CoreRadius) < 1e-4f, $"the core's radius is {ThresherBallForm.CoreRadius} (the gameplay ball)");
            Check(Math.Abs(rmax - ThresherBallForm.SpikeTipRadius) < 1e-4f, $"spike tips stand at {ThresherBallForm.SpikeTipRadius}");
            var ico = ThresherBallForm.IcosahedronVertices();
            float minSep = 180f;
            for (int i = 0; i < ico.Length; i++)
                for (int j = i + 1; j < ico.Length; j++)
                    minSep = Math.Min(minSep, Mathf.Acos(Mathf.Clamp(Vector3.Dot(ico[i], ico[j]), -1f, 1f)) * Mathf.Rad2Deg);
            Check(Math.Abs(minSep - 63.435f) < 0.01f, $"the spikes are evenly spread (nearest pair {minSep:F2} deg, an icosahedron's 63.43)");
            if (Array.IndexOf(args, "--obj") is int bo && bo >= 0 && bo + 1 < args.Length)
            {
                Directory.CreateDirectory(args[bo + 1]);
                WriteObj(Path.Combine(args[bo + 1], "ball.obj"), new List<ThresherHullForm.Part> { ball });
            }
        }

        int oi = Array.IndexOf(args, "--obj");
        if (oi >= 0 && oi + 1 < args.Length)
        {
            Directory.CreateDirectory(args[oi + 1]);
            WriteObj(Path.Combine(args[oi + 1], "base.obj"), parts);
            foreach (var el in ThresherHullForm.MorphElements)
                WriteObj(Path.Combine(args[oi + 1], el.ToString().ToLowerInvariant() + ".obj"),
                         ThresherHullForm.Generate(ThresherHullForm.ApplyElementExtreme(s, el)));
            Console.WriteLine("  wrote obj files to " + args[oi + 1]);
        }

        Console.WriteLine(_fail == 0 ? "thresher hull harness: OK" : $"thresher hull harness: {_fail} FAILURE(S)");
        return _fail == 0 ? 0 : 1;
    }

    /// <summary>Möller–Trumbore, forward hits only.</summary>
    static bool RayHits(Vector3 o, Vector3 d, Vector3 a, Vector3 b, Vector3 c)
    {
        var e1 = b - a; var e2 = c - a;
        var p = Vector3.Cross(d, e2);
        float det = Vector3.Dot(e1, p);
        if (Math.Abs(det) < 1e-12f) return false;
        float inv = 1f / det;
        var tv = o - a;
        float u = Vector3.Dot(tv, p) * inv;
        if (u < 0f || u > 1f) return false;
        var qv = Vector3.Cross(tv, e1);
        float v = Vector3.Dot(d, qv) * inv;
        if (v < 0f || u + v > 1f) return false;
        return Vector3.Dot(e2, qv) * inv > 1e-6f;
    }

    /// <summary>A part's triangles grouped into connected pieces (shared vertex POSITIONS, since faces are
    /// flat-shaded and never share an index).</summary>
    static List<List<int[]>> Pieces(ThresherHullForm.Part p)
    {
        var tris = new List<int[]>();
        foreach (var list in new[] { p.Body, p.Accent })
            for (int i = 0; i < list.Count; i += 3) tris.Add(new[] { list[i], list[i + 1], list[i + 2] });
        string Key(Vector3 v) => FormattableString.Invariant($"{Math.Round(v.x, 4)},{Math.Round(v.y, 4)},{Math.Round(v.z, 4)}");
        var parent = Enumerable.Range(0, tris.Count).ToArray();
        int Find(int x) { while (parent[x] != x) x = parent[x] = parent[parent[x]]; return x; }
        var owner = new Dictionary<string, int>();
        for (int t = 0; t < tris.Count; t++)
            foreach (var i in tris[t])
            {
                var k = Key(p.Verts[i]);
                if (owner.TryGetValue(k, out var o)) parent[Find(t)] = Find(o); else owner[k] = t;
            }
        return tris.Select((t, i) => (t, r: Find(i))).GroupBy(x => x.r).Select(g => g.Select(x => x.t).ToList()).ToList();
    }

    static void WriteObj(string path, List<ThresherHullForm.Part> parts)
    {
        using var w = new StreamWriter(path);
        int offset = 1;
        foreach (var p in parts)
        {
            w.WriteLine("o " + p.Name);
            foreach (var v in p.Verts) w.WriteLine(FormattableString.Invariant($"v {v.x} {v.y} {v.z}"));
            foreach (var n in p.Normals) w.WriteLine(FormattableString.Invariant($"vn {n.x} {n.y} {n.z}"));
            foreach (var (tris, mat) in new[] { (p.Body, "body"), (p.Accent, "accent") })
            {
                if (tris.Count == 0) continue;
                w.WriteLine("usemtl " + mat);
                for (int i = 0; i < tris.Count; i += 3)
                    w.WriteLine($"f {tris[i] + offset}//{tris[i] + offset} {tris[i + 1] + offset}//{tris[i + 1] + offset} {tris[i + 2] + offset}//{tris[i + 2] + offset}");
            }
            offset += p.Verts.Count;
        }
    }
}
