using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A vertebrate eye: eyeball (Eye slot, its own iris texture) + upper and lower LIDS with a
    /// visible margin (Skin slot, UVs projected onto the head so the lash line paints onto
    /// them). The lids are what makes an eye read as alive rather than as a marble in a hole:
    /// the almond opening, the lid THICKNESS, and the canthal tilt carry more than the iris.
    /// This is the file for "the eyes look dead", "the lids are wrong", "the eye is too big".
    /// Site-local: +z outward, +x toward the character's outer canthus, +y up. Head units.
    /// </summary>
    public static class VertebrateEyeFeature
    {
        const int BallRows = 18, BallSegs = 28;
        const int LidCols = 19, LidRows = 6;

        public static void Generate(FeatureContext ctx)
        {
            var p = ctx.Params.Eye;
            float orbit = GeometryKit.Dial(ctx.Shape.Clamped(HeadAxis.OrbitalSize), 0.80f, 1.28f);
            float r = p.Radius * orbit;
            Vector3 centre = new Vector3(0f, 0f, -0.52f * r);

            // Eyeball: front pole toward +z (the iris sits at the pole in the texture).
            var ball = ctx.Begin("Eyeball", CharacterMaterialSlot.Eye);
            GeometryKit.Sphere(ball, centre, r, BallRows, BallSegs, Vector3.forward, Vector3.up);
            GeometryKit.RecalculateNormals(ball);

            // Lids.
            float open = Mathf.Clamp(p.LidOpen, 0.15f, 1f);
            float tilt = p.CanthalTiltDeg * Mathf.Deg2Rad;
            float w = r * 0.96f;                       // half-width of the palpebral fissure
            float upperH = r * 0.82f * open;           // upper lid edge height at centre
            float lowerH = r * 0.50f * open;           // lower lid edge depth at centre
            float rLid = r * 1.06f;
            float rInner = r * 1.005f;

            var lids = ctx.Begin("Lids", CharacterMaterialSlot.Skin, projectUvs: true);
            BuildLid(lids, centre, r, rLid, rInner, w, upperH, tilt, +1f, p.LidThickness);
            BuildLid(lids, centre, r, rLid, rInner, w, lowerH, tilt, -1f, p.LidThickness);
            GeometryKit.RecalculateNormals(lids);
        }

        /// <summary>
        /// One lid as a grid: rows run from the inner margin edge (touching the ball) through the
        /// outer margin edge, over the shell, to a skirt that turns back into the socket.
        /// <paramref name="sign"/> +1 upper, −1 lower.
        /// </summary>
        static void BuildLid(MeshPart part, Vector3 centre, float rBall, float rLid, float rInner,
                             float w, float edgeH, float tilt, float sign, float thickness)
        {
            var pts = new Vector3[LidRows, LidCols];
            var uvs = new Vector2[LidRows, LidCols];
            float ct = Mathf.Cos(tilt), st = Mathf.Sin(tilt);
            for (int c = 0; c < LidCols; c++)
            {
                float u = c / (float)(LidCols - 1);
                float x = -w + 2f * w * u;
                float edge = sign * edgeH * GeometryKit.SafePow(1f - (x / w) * (x / w), sign > 0f ? 0.55f : 0.7f);
                // Rows: 0 inner margin, 1 outer margin, 2..4 shell, 5 skirt.
                for (int rr = 0; rr < LidRows; rr++)
                {
                    float y, z, radius;
                    switch (rr)
                    {
                        case 0: y = edge - sign * thickness * 0.35f; radius = rInner; break;
                        case 1: y = edge; radius = rLid; break;
                        default:
                        {
                            float t = (rr - 1) / (float)(LidRows - 2);           // 0 at margin → 1 at skirt
                            float ySpan = sign * rLid * 1.02f - edge;
                            y = edge + ySpan * GeometryKit.Smooth(t);
                            radius = rLid;
                            break;
                        }
                    }
                    float x2 = x * x, y2 = y * y;
                    float zz = radius * radius - x2 - y2;
                    z = zz > 0f ? Mathf.Sqrt(zz) : 0f;
                    if (rr == LidRows - 1) z = -0.22f * rBall;                    // skirt turns into the socket
                    // Canthal tilt rotates the opening about z.
                    float xr = x * ct - y * st, yr = x * st + y * ct;
                    pts[rr, c] = centre + new Vector3(xr, yr, z);
                    uvs[rr, c] = new Vector2(u, rr / (float)(LidRows - 1));
                }
            }
            int triStart = part.Tris.Count;
            GeometryKit.Grid(part, pts, uvs);
            if (sign < 0f)
            {
                // Lower lid rows run downward: flip winding so the shell faces outward.
                for (int i = triStart; i < part.Tris.Count; i += 3)
                {
                    int t = part.Tris[i + 1]; part.Tris[i + 1] = part.Tris[i + 2]; part.Tris[i + 2] = t;
                }
            }
        }
    }
}
