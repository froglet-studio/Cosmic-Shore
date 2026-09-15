using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Hair as THREE things: a thin scalp cap inside the hairline (so no skin shows through),
    /// a field of STRAND CARDS combed from a parting and falling with gravity over the scalp,
    /// temples and forehead, and eyebrow cards along the brow ridge. A helmet cap reads as a
    /// mannequin; the strands are what make it hair. Hair slot; samples the head SURFACE
    /// directly (not a site), so it follows any head shape. Every card is two-sided.
    /// This is the file for "the hairline is wrong", "the hair is a helmet", "the brows are wrong".
    /// </summary>
    public static class HairCapFeature
    {
        const int CapRows = 22, CapCols = 64;
        const int StrandSegments = 9;
        const int BrowSegments = 4;
        const float BrowUvBand = 0.90f;   // u ≥ this is painted as brow in the hair texture

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Hair;
            float volume = Mathf.Clamp01(ctx.HairVolume);
            float thickness = p.Thickness * (0.3f + 0.7f * volume);
            Cap(ctx, p, thickness);
            if (p.StrandCount > 0) Strands(ctx, p, volume, thickness);
            if (p.BrowCards > 0) Brows(ctx, p);
        }

        static float Hairline(HairParams p, float phi, int seed)
        {
            float front = p.FrontHairlineDeg * Mathf.Deg2Rad;
            float side = p.SideHairlineDeg * Mathf.Deg2Rad;
            float back = p.BackHairlineDeg * Mathf.Deg2Rad;
            float cp = Mathf.Cos(phi);
            float wf = Mathf.Max(0f, cp); wf *= wf;
            float wb = Mathf.Max(0f, -cp); wb *= wb;
            float ws = Mathf.Max(0f, 1f - wf - wb);
            // Temple recession: the front hairline dips back a little each side of the centre.
            float recess = 1f + 0.06f * GeometryKit.Bell((Mathf.Abs(phi) - 0.55f) / 0.35f) * Mathf.Max(0f, cp);
            return (front * wf * recess + side * ws + back * wb) * (1f + 0.04f * (Noise.Value(phi * 3f, 0f, seed + 3) - 0.5f));
        }

        // ------------------------------------------------------------------ cap

        static void Cap(FeatureContext ctx, HairParams p, float thickness)
        {
            var part = ctx.Begin("HairCap", CharacterMaterialSlot.Hair);
            part.IsHeadSpace = true;   // sampled straight off the surface, not through the site
            var pts = new Vector3[CapRows + 1, CapCols + 1];
            var uvs = new Vector2[CapRows + 1, CapCols + 1];
            float capThick = thickness * 0.55f;
            for (int c = 0; c <= CapCols; c++)
            {
                float phi = (c / (float)CapCols - 0.5f) * GeometryKit.Tau;
                float hairline = Hairline(p, phi, 0);
                for (int r = 0; r <= CapRows; r++)
                {
                    float t = r / (float)CapRows;
                    float theta = hairline * t;
                    Vector3 dir = GeometryKit.Dir(theta, phi);
                    Vector3 surf = ctx.Surface.Sample(dir);
                    float taper = 1f - GeometryKit.Smooth((t - 0.80f) / 0.20f);
                    float clump = 1f + p.Clumping * 0.4f * (Noise.Fbm(phi * 2f + 10f, theta * 4f, 3, 2f, 0.5f, 11) - 0.5f);
                    pts[r, c] = surf + dir * (capThick * taper * clump);
                    uvs[r, c] = new Vector2(c / (float)CapCols * BrowUvBand, 1f - t);
                }
            }
            GeometryKit.Grid(part, pts, uvs);
            part.FlipWinding();
            GeometryKit.RecalculateNormals(part);
        }

        // ------------------------------------------------------------------ strands

        /// <summary>The combed flow direction on the scalp at a surface point.</summary>
        static Vector3 Flow(Vector3 dir, Vector3 normal, float partPhi)
        {
            GeometryKit.ToAngles(dir, out float theta, out float phi);
            Vector3 ePhi = new Vector3(Mathf.Cos(phi), 0f, -Mathf.Sin(phi));       // tangent along +φ
            float away = GeometryKit.WrapAngle(phi - partPhi);
            float side = Mathf.Sign(away) * Mathf.Min(1f, Mathf.Abs(away) / 0.25f);   // soft sign at the parting
            float sideStrength = (0.25f + 0.85f * GeometryKit.Bell((theta - 0.25f) / 0.95f)) * (1f - 0.8f * GeometryKit.Smooth((theta - 0.9f) / 0.45f));
            Vector3 flow = Vector3.down + ePhi * (side * sideStrength);
            flow -= normal * Vector3.Dot(flow, normal);
            return flow.sqrMagnitude > 1e-8f ? flow.normalized : Vector3.down;
        }

        static void Strands(FeatureContext ctx, HairParams p, float volume, float thickness)
        {
            var part = ctx.Begin("HairStrands", CharacterMaterialSlot.Hair);
            part.IsHeadSpace = true;
            var rng = ctx.Rng;
            int count = Mathf.RoundToInt(p.StrandCount * (0.55f + 0.45f * volume));
            float length = p.StrandLength * (0.45f + 0.75f * volume);
            float partPhi = p.PartDeg * Mathf.Deg2Rad;
            var pts = new Vector3[StrandSegments + 1];
            var nrm = new Vector3[StrandSegments + 1];
            var fwd = new Vector3[StrandSegments + 1];
            for (int i = 0; i < count; i++)
            {
                // Stratified seeds over the scalp: golden spiral in (√u, φ) with jitter.
                float u = (i + 0.5f) / count;
                float t = Mathf.Sqrt(u) * 0.985f;
                float phi = GeometryKit.WrapAngle(i * 2.399963f + rng.Range(-0.15f, 0.15f));
                float hairline = Hairline(p, phi, 0);
                float theta = hairline * t;
                Vector3 dir = GeometryKit.Dir(theta, phi);
                float lift = thickness * rng.Range(0.75f, 1.45f);
                float len = length * rng.Range(0.8f, 1.15f);
                float cp = Mathf.Cos(phi);
                if (cp > 0.45f && theta > hairline * 0.6f) len *= Mathf.Lerp(0.45f, 1f, p.Fringe);   // the front falls shorter
                float halfW = p.StrandWidth * 0.5f * rng.Range(0.7f, 1.3f);
                float ds = len / StrandSegments;

                // The scalp is close enough to a sphere about the centre that the ray direction
                // serves as its normal; a finite-difference normal here would triple the cost of
                // the most expensive feature on the head for no visible gain.
                Vector3 pos = ctx.Surface.Sample(dir);
                Vector3 n = dir;
                pos += n * lift;
                Vector3 flow = Flow(dir, n, partPhi);
                float wobble = rng.Range(-0.35f, 0.35f);
                pts[0] = pos; nrm[0] = n; fwd[0] = flow;
                for (int k = 1; k <= StrandSegments; k++)
                {
                    float s = k / (float)StrandSegments;
                    pos += flow * ds;
                    Vector3 d2 = pos - ctx.Surface.Centre;
                    Vector3 surf = ctx.Surface.Sample(d2);
                    Vector3 n2 = d2.normalized;
                    float rSurf = (surf - ctx.Surface.Centre).magnitude, rPos = d2.magnitude;
                    float clingLift = lift * (1f + 0.4f * s);
                    if (rPos < rSurf + clingLift) pos = surf + n2 * clingLift;                 // never inside the head
                    else if (rPos > rSurf + clingLift * 2.2f) pos = Vector3.Lerp(pos, surf + n2 * clingLift, 0.4f);
                    Vector3 f2 = Flow(d2.normalized, n2, partPhi);
                    // Sideways wobble and gravity: the further from the scalp seed, the more it hangs.
                    Vector3 across = Vector3.Cross(f2, n2).normalized;
                    f2 = (f2 + across * (wobble * 0.25f * Mathf.Sin(s * 3.1f)) + Vector3.down * (0.45f * s)).normalized;
                    flow = (flow * 0.45f + f2 * 0.55f).normalized;
                    pts[k] = pos; nrm[k] = n2; fwd[k] = flow;
                }
                Ribbon(part, pts, nrm, fwd, StrandSegments, halfW, s => 1f - 0.6f * s * s, 0f, BrowUvBand * 0.98f, rng.Range(0f, 0.6f));
            }
            GeometryKit.RecalculateNormals(part);
            DoubleSide(part);
        }

        // ------------------------------------------------------------------ brows

        static void Brows(FeatureContext ctx, HairParams p)
        {
            var part = ctx.Begin("Brows", CharacterMaterialSlot.Hair);
            part.IsHeadSpace = true;
            var rng = ctx.Rng;
            float spread = ctx.Shape.Clamped(HeadAxis.OrbitalSpacing);
            float ridge = ctx.Shape.Clamped(HeadAxis.BrowRidge);
            float thick = 0.75f + 0.5f * ctx.HairVolume;
            var pts = new Vector3[BrowSegments + 1];
            var nrm = new Vector3[BrowSegments + 1];
            var fwd = new Vector3[BrowSegments + 1];
            for (int side = -1; side <= 1; side += 2)
            {
                for (int j = 0; j < p.BrowCards; j++)
                {
                    float tt = (j + 0.5f) / p.BrowCards;                       // 0 inner … 1 outer
                    float jitter = rng.Range(-0.03f, 0.03f);
                    // The brow arc: rises over the inner two thirds, falls at the tail.
                    float arch = -4.0f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(tt * 0.85f + 0.08f)) + 2.5f * Mathf.Max(0f, tt - 0.7f) / 0.3f;
                    float thetaDeg = 80.5f + arch - 1.2f * ridge + jitter * 25f;
                    float phiDeg = side * (6f + 27f * tt + 4f * spread);
                    Vector3 dir = GeometryKit.Dir(thetaDeg * Mathf.Deg2Rad, (phiDeg + jitter * 40f) * Mathf.Deg2Rad);
                    Vector3 surf = ctx.Surface.Sample(dir);
                    Vector3 n = HeadSiteResolver.SurfaceNormal(ctx.Surface, dir);
                    // Card direction: along the arc, outward; the inner cards point up, the tail flat.
                    Vector3 dirNext = GeometryKit.Dir((thetaDeg - 1.5f * (1f - tt)) * Mathf.Deg2Rad, (phiDeg + side * 2f) * Mathf.Deg2Rad);
                    Vector3 along = ctx.Surface.Sample(dirNext) - surf;
                    along -= n * Vector3.Dot(along, n);
                    Vector3 up = Vector3.up - n * Vector3.Dot(Vector3.up, n);
                    Vector3 f = (along.normalized + up.normalized * (0.9f * (1f - tt) - 0.3f)).normalized;
                    float len = 0.040f * (1.2f - 0.5f * tt) * thick;
                    float halfW = 0.0028f * (1.25f - 0.6f * tt) * thick;
                    Vector3 pos = surf + n * 0.003f - f * len * 0.5f;
                    for (int k = 0; k <= BrowSegments; k++)
                    {
                        float s = k / (float)BrowSegments;
                        Vector3 q = pos + f * (len * s);
                        Vector3 d2 = q - ctx.Surface.Centre;
                        Vector3 onHead = ctx.Surface.Sample(d2);
                        Vector3 n2 = HeadSiteResolver.SurfaceNormal(ctx.Surface, d2);
                        pts[k] = onHead + n2 * (0.003f + 0.004f * s);
                        nrm[k] = n2; fwd[k] = f;
                    }
                    Ribbon(part, pts, nrm, fwd, BrowSegments, halfW, s => 1f - 0.35f * s, BrowUvBand + 0.01f, 0.99f, rng.Range(0f, 0.5f));
                }
            }
            GeometryKit.RecalculateNormals(part);
            DoubleSide(part);
        }

        // ------------------------------------------------------------------ ribbons

        /// <summary>A card along a polyline lying flat against the surface (its width across the flow, in the tangent plane).</summary>
        static void Ribbon(MeshPart part, Vector3[] pts, Vector3[] nrm, Vector3[] fwd, int segments, float halfW,
                           System.Func<float, float> widthProfile, float u0, float u1, float v0)
        {
            int first = part.Verts.Count;
            for (int k = 0; k <= segments; k++)
            {
                float s = k / (float)segments;
                Vector3 across = Vector3.Cross(fwd[k], nrm[k]);
                if (across.sqrMagnitude < 1e-10f) across = Vector3.right;
                across = across.normalized * (halfW * widthProfile(s));
                float v = v0 + s * 0.4f;
                part.AddVertex(pts[k] - across, new Vector2(u0, v));
                part.AddVertex(pts[k] + across, new Vector2(u1, v));
            }
            for (int k = 0; k < segments; k++)
            {
                int a = first + k * 2, b = a + 1, c = a + 3, d = a + 2;
                part.AddQuad(a, b, c, d);
            }
        }

        /// <summary>Duplicate every triangle with the opposite winding and mirrored normals, so the card draws from behind too.</summary>
        static void DoubleSide(MeshPart part)
        {
            int n = part.Verts.Count;
            for (int i = 0; i < n; i++) part.AddVertex(part.Verts[i], part.Uvs[i]);
            for (int i = 0; i < n; i++) part.Normals[n + i] = -part.Normals[i];
            int tris = part.Tris.Count;
            for (int i = 0; i < tris; i += 3)
            {
                part.Tris.Add(part.Tris[i] + n);
                part.Tris.Add(part.Tris[i + 2] + n);
                part.Tris.Add(part.Tris[i + 1] + n);
            }
        }
    }
}
