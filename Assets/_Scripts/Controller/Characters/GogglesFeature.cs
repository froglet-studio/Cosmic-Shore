using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Pilot goggles — two rimmed lenses joined by a bridge on a strap that runs around the
    /// head — either pushed UP onto the forehead or worn ON over the eyes, per
    /// <see cref="GearKind"/>. Built in HEAD space off the surface (the strap follows any
    /// skull), Gear slot: frames and strap wear the leather region of the gear texture, the
    /// lenses its glass region. This is the file for "the goggles are wrong".
    /// </summary>
    public static class GogglesFeature
    {
        const int StrapSteps = 40, LensSegs = 28, LensRings = 4;

        public static void Generate(FeatureContext ctx)
        {
            bool on = ctx.Gear == GearKind.GogglesOn;
            if (ctx.Gear == GearKind.None) return;
            var part = ctx.Begin(on ? "GogglesOn" : "GogglesUp", CharacterMaterialSlot.Gear);
            part.IsHeadSpace = true;
            float spread = ctx.Shape.Clamped(HeadAxis.OrbitalSpacing);
            float orbit = GeometryKit.Dial(ctx.Shape.Clamped(HeadAxis.OrbitalSize), 0.85f, 1.2f);

            // Where the band sits: the eye line when worn, the hairline/forehead when pushed up.
            float bandTheta = on ? 90.5f : 52f;
            float lensPhi = on ? 22.5f + 5f * spread : 20f + 4f * spread;
            float lensR = (on ? 0.115f : 0.105f) * orbit;
            float lift = 0.012f;

            // Strap: a ribbon around the head at the band's polar angle (skipping the front,
            // where the frames are), standing a little off the surface.
            for (int k = 0; k <= StrapSteps; k++)
            {
                float phi = Mathf.Lerp(0.42f, GeometryKit.Tau - 0.42f, k / (float)StrapSteps);   // from left of the left lens, round the back
                Vector3 dir = GeometryKit.Dir(bandTheta * Mathf.Deg2Rad, phi);
                Vector3 surf = ctx.Surface.Sample(dir);
                Vector3 n = dir;
                Vector3 upT = Vector3.up - n * Vector3.Dot(Vector3.up, n);
                upT = upT.normalized * 0.038f;
                Vector3 o = surf + n * lift;
                part.AddVertex(o - upT, new Vector2(0.55f, k / (float)StrapSteps));
                part.AddVertex(o + upT, new Vector2(0.70f, k / (float)StrapSteps));
                part.AddVertex(o - upT + n * 0.012f, new Vector2(0.55f, k / (float)StrapSteps));
                part.AddVertex(o + upT + n * 0.012f, new Vector2(0.70f, k / (float)StrapSteps));
            }
            for (int k = 0; k < StrapSteps; k++)
            {
                int a = k * 4;
                part.AddQuad(a + 2, a + 6, a + 7, a + 3);     // outer face
                part.AddQuad(a + 3, a + 7, a + 5, a + 1);     // top edge
                part.AddQuad(a + 0, a + 4, a + 6, a + 2);     // bottom edge
            }

            // Lenses: a rimmed disc on each side, tangent to the surface and standing proud.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 dir = GeometryKit.Dir(bandTheta * Mathf.Deg2Rad, side * lensPhi * Mathf.Deg2Rad);
                Vector3 surf = ctx.Surface.Sample(dir);
                Vector3 n = HeadSiteResolver.SurfaceNormal(ctx.Surface, dir);
                GeometryKit.Frame(n, Vector3.up, out var right, out var up);
                Vector3 c = surf + n * (on ? 0.045f : 0.028f);
                Lens(part, c, right, up, n, lensR);
            }
            // Bridge between the lenses.
            {
                Vector3 dirL = GeometryKit.Dir(bandTheta * Mathf.Deg2Rad, lensPhi * Mathf.Deg2Rad * 0.35f);
                Vector3 s2 = ctx.Surface.Sample(dirL);
                Vector3 n = dirL;
                Vector3 c = s2 + n * (on ? 0.05f : 0.035f);
                Vector3 up = (Vector3.up - n * Vector3.Dot(Vector3.up, n)).normalized * 0.014f;
                Vector3 rt = Vector3.Cross(up.normalized, n).normalized * (lensR * 0.55f);
                int f = part.Verts.Count;
                part.AddVertex(c - rt - up, new Vector2(0.56f, 0.2f));
                part.AddVertex(c + rt - up, new Vector2(0.69f, 0.2f));
                part.AddVertex(c + rt + up, new Vector2(0.69f, 0.4f));
                part.AddVertex(c - rt + up, new Vector2(0.56f, 0.4f));
                part.AddQuad(f, f + 1, f + 2, f + 3);
                part.AddQuad(f + 3, f + 2, f + 1, f);
            }
            GeometryKit.RecalculateNormals(part);
            part.MakeDoubleSided();   // the strap and bridge are thin; never let a winding hide them
        }

        /// <summary>A lens: glass disc (u ≥ 0.76 of the gear texture) inside a raised leather rim.</summary>
        static void Lens(MeshPart part, Vector3 c, Vector3 right, Vector3 up, Vector3 n, float r)
        {
            int centre = part.AddVertex(c + n * 0.006f, new Vector2(0.88f, 0.5f));
            int first = part.Verts.Count;
            // Ring 0: glass edge (slightly domed), ring 1: rim inner top, ring 2: rim outer top, ring 3: rim outer bottom (into the face).
            float[] radii = { r * 0.74f, r * 0.76f, r, r * 1.03f };
            float[] heights = { 0.010f, 0.034f, 0.034f, -0.06f };
            for (int ring = 0; ring < LensRings; ring++)
            {
                for (int k = 0; k <= LensSegs; k++)
                {
                    float a = GeometryKit.Tau * k / LensSegs;
                    Vector3 v = c + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radii[ring] + n * heights[ring];
                    Vector2 uv = ring == 0
                        ? new Vector2(0.88f + 0.11f * Mathf.Cos(a), 0.5f + 0.45f * Mathf.Sin(a))
                        : new Vector2(0.56f + 0.13f * (k / (float)LensSegs), ring == 3 ? 0.9f : 0.6f);
                    part.AddVertex(v, uv);
                }
            }
            int cols = LensSegs + 1;
            for (int k = 0; k < LensSegs; k++) part.AddTri(centre, first + k, first + k + 1);           // glass fan, facing +n
            for (int ring = 0; ring < LensRings - 1; ring++)
                for (int k = 0; k < LensSegs; k++)
                {
                    int a = first + ring * cols + k, b = a + 1, cc = a + cols + 1, d = a + cols;
                    part.AddQuad(a, d, cc, b);
                }
        }
    }
}
