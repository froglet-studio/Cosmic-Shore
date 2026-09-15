using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A vertebrate eye: eyeball with a corneal bulge (Eye slot, its own iris texture), upper
    /// and lower LIDS with a visible margin, an upper-lid fold, and an orbital skirt that is
    /// SAMPLED OFF THE HEAD SURFACE so the lids seal to whatever socket the head sculpted (Skin
    /// slot, UVs projected onto the head so the lash line paints onto them), and a caruncle at
    /// the inner canthus. The lids are what makes an eye read as alive rather than as a marble
    /// in a hole: the almond opening, its nasal-biased peak, the lid THICKNESS and the fold
    /// carry more than the iris. This is the file for "the eyes look dead", "the eyes bulge",
    /// "the lids are wrong", "the eye is too big".
    /// Site-local: +z outward, +x toward the character's outer canthus, +y up. Head units.
    /// </summary>
    public static class VertebrateEyeFeature
    {
        const int BallRows = 24, BallSegs = 36;
        const int LidCols = 25;
        const int UpperRows = 9, LowerRows = 7;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Eye;
            float orbit = GeometryKit.Dial(ctx.Shape.Clamped(HeadAxis.OrbitalSize), 0.82f, 1.25f);
            float r = p.Radius * orbit;
            Vector3 centre = new Vector3(0f, 0f, -0.50f * r);

            // Eyeball: front pole toward +z (the iris sits at the pole in the texture), with the
            // cornea bulging over the iris.
            var ball = ctx.Begin("Eyeball", CharacterMaterialSlot.Eye);
            float irisAngle = Mathf.Asin(Mathf.Clamp(p.IrisFraction, 0.2f, 0.95f)) * 1.15f;
            GeometryKit.Frame(Vector3.forward, Vector3.up, out var bright, out var bup);
            int first = ball.Verts.Count;
            for (int rr = 0; rr <= BallRows; rr++)
            {
                float beta = Mathf.PI * rr / BallRows;
                float bulge = 1f + 0.075f * GeometryKit.Bell(beta / irisAngle);
                float sb = Mathf.Sin(beta), cb = Mathf.Cos(beta);
                for (int s = 0; s <= BallSegs; s++)
                {
                    float alpha = GeometryKit.Tau * s / BallSegs;
                    Vector3 d = Vector3.forward * cb + (bright * Mathf.Cos(alpha) + bup * Mathf.Sin(alpha)) * sb;
                    ball.AddVertex(centre + d * (r * bulge), new Vector2(s / (float)BallSegs, 1f - rr / (float)BallRows));
                }
            }
            int cols = BallSegs + 1;
            for (int rr = 0; rr < BallRows; rr++)
                for (int s = 0; s < BallSegs; s++)
                {
                    int a = first + rr * cols + s, b = a + 1, c = a + cols + 1, d = a + cols;
                    ball.AddQuad(a, d, c, b);
                }
            GeometryKit.RecalculateNormals(ball);

            // Lids.
            float open = Mathf.Clamp(p.LidOpen, 0.15f, 1f);
            float tilt = p.CanthalTiltDeg * Mathf.Deg2Rad;
            float w = r * 0.98f;                       // half-width of the palpebral fissure
            float upperH = r * 0.76f * open;           // upper lid edge height at its peak
            float lowerH = r * 0.84f * open;           // lower lid edge depth at its trough
            float t = Mathf.Max(0.004f, p.LidThickness);

            var lids = ctx.Begin("Lids", CharacterMaterialSlot.Skin, projectUvs: true);
            BuildLid(ctx, lids, centre, r, w, upperH, tilt, +1f, t, UpperRows);
            BuildLid(ctx, lids, centre, r, w, lowerH, tilt, -1f, t, LowerRows);
            GeometryKit.RecalculateNormals(lids);

            // Caruncle: the small pink mound at the inner canthus.
            var car = ctx.Begin("Caruncle", CharacterMaterialSlot.Skin, projectUvs: true);
            Vector3 inner = centre + new Vector3(-w * 0.93f, -0.06f * r, 0f);
            inner.z = centre.z + GeometryKit.SafeSqrt(r * r - (w * 0.93f) * (w * 0.93f)) * 0.9f;
            GeometryKit.Sphere(car, inner, r * 0.14f, 6, 10, Vector3.forward, Vector3.up);
            GeometryKit.RecalculateNormals(car);
        }

        /// <summary>
        /// The fissure edge at x ∈ [−w, w] (−w inner canthus). The upper lid peaks nasally and the
        /// lower lid bottoms out temporally, which is the asymmetry that makes an almond a human
        /// eye rather than a lemon.
        /// </summary>
        static float Edge(float x, float w, float h, float sign)
        {
            float u = x / w;
            float shift = sign > 0f ? -0.18f : 0.18f;
            // Piecewise: map [−1, shift] → [−1, 0] and [shift, 1] → [0, 1].
            float v = u < shift ? (u + 1f) / (shift + 1f) - 1f : (u - shift) / (1f - shift);
            float exp = sign > 0f ? 0.5f : 0.62f;
            return sign * h * GeometryKit.SafePow(1f - v * v, exp);
        }

        /// <summary>
        /// One lid as a grid. Rows run from the inner margin edge (touching the ball) through the
        /// outer margin edge, over the lid shell, through the fold (upper) and out to a skirt
        /// that lies ON the sampled head surface. <paramref name="sign"/> +1 upper, −1 lower.
        /// </summary>
        static void BuildLid(FeatureContext ctx, MeshPart part, Vector3 centre, float r, float w, float edgeH,
                             float tilt, float sign, float thickness, int rows)
        {
            var pts = new Vector3[rows, LidCols];
            var uvs = new Vector2[rows, LidCols];
            float ct = Mathf.Cos(tilt), st = Mathf.Sin(tilt);
            float rShell = r + thickness;
            float shellEnd = sign > 0f ? 0.80f * r : -0.72f * r;       // where the lid leaves the ball
            float foldY = 0.86f * r;                                    // upper lid fold height
            float skirtR = sign > 0f ? 1.55f * r : 1.35f * r;           // skirt reach from the eye centre
            for (int c = 0; c < LidCols; c++)
            {
                float u = c / (float)(LidCols - 1);
                float x = -w + 2f * w * u;
                float edge = Edge(x, w, edgeH, sign);
                float xFrac = x / w;
                for (int rr = 0; rr < rows; rr++)
                {
                    float y, z, radius;
                    bool onSurface = false;
                    float surfaceBlend = 0f;
                    if (rr == 0)          { y = edge - sign * thickness * 0.30f; radius = r * 1.003f; }
                    else if (rr == 1)     { y = edge; radius = rShell; }
                    else if (sign > 0f)
                    {
                        // Upper: rows 2..4 shell, 5 fold, 6 orbital, 7 blend, 8 skirt.
                        switch (rr)
                        {
                            case 2: case 3: case 4:
                            {
                                float k = (rr - 1) / 4f;
                                y = Mathf.Lerp(edge, shellEnd, GeometryKit.Smooth(k));
                                radius = rShell;
                                break;
                            }
                            case 5: y = foldY; radius = rShell - thickness * 0.9f; break;      // the fold dips back
                            case 6: y = 1.10f * r; radius = rShell + thickness * 0.4f; surfaceBlend = 0.35f; break;
                            case 7: y = 1.32f * r; radius = rShell; surfaceBlend = 0.85f; break;
                            default: y = skirtR; radius = rShell; onSurface = true; break;
                        }
                    }
                    else
                    {
                        // Lower: rows 2..3 shell, 4 blend, 5 blend, 6 skirt.
                        switch (rr)
                        {
                            case 2: case 3:
                            {
                                float k = (rr - 1) / 3f;
                                y = Mathf.Lerp(edge, shellEnd, GeometryKit.Smooth(k));
                                radius = rShell;
                                break;
                            }
                            case 4: y = -0.85f * r; radius = rShell; surfaceBlend = 0.4f; break;
                            case 5: y = -1.08f * r; radius = rShell; surfaceBlend = 0.85f; break;
                            default: y = -skirtR; radius = rShell; onSurface = true; break;
                        }
                    }
                    // Lateral spread: the outer rows widen beyond the fissure so the skirt covers the orbit.
                    float spread = rr <= 1 ? 1f : Mathf.Lerp(1f, 1.35f, GeometryKit.Smooth((rr - 1) / (float)(rows - 2)));
                    float xs = x * spread;
                    float zz = radius * radius - xs * xs - y * y;
                    z = zz > 0f ? Mathf.Sqrt(zz) : -0.15f * r;
                    // Canthal tilt rotates the opening about z.
                    float xr = xs * ct - y * st, yr = xs * st + y * ct;
                    Vector3 local = centre + new Vector3(xr, yr, z);
                    if (onSurface || surfaceBlend > 0f)
                    {
                        Vector3 onHead = ProjectToSurface(ctx, local);
                        local = onSurface ? onHead : Vector3.Lerp(local, onHead, surfaceBlend);
                        if (!onSurface) local.z = Mathf.Max(local.z, onHead.z);   // never dip under the skin
                    }
                    pts[rr, c] = local;
                    uvs[rr, c] = new Vector2(u, rr / (float)(rows - 1));
                }
            }
            int triStart = part.Tris.Count;
            GeometryKit.Grid(part, pts, uvs);
            if (sign < 0f)
            {
                // Lower lid rows run downward: flip winding so the shell faces outward.
                for (int i = triStart; i < part.Tris.Count; i += 3)
                {
                    int tt = part.Tris[i + 1]; part.Tris[i + 1] = part.Tris[i + 2]; part.Tris[i + 2] = tt;
                }
            }
        }

        /// <summary>A site-local point moved onto the head surface along the ray from the head centre.</summary>
        static Vector3 ProjectToSurface(FeatureContext ctx, Vector3 local)
        {
            Vector3 head = ctx.Site.ToHead(local);
            Vector3 dir = head - ctx.Surface.Centre;
            if (dir.sqrMagnitude < 1e-10f) return local;
            Vector3 onHead = ctx.Surface.Sample(dir);
            return ctx.Site.ToLocal(onHead);
        }
    }
}
