using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// How a prism is DRAWN in the Pistol's world (the duo brief §4.3: "prisms may have to distort or be misrepresented
    /// in order to preserve that reality"). The unroll is a map projection with a cut: a point the Goby's path circles is
    /// beside the path at EVERY arc length of the lap, so its honest image is stretched along x by R / r (R the lap's
    /// radius, r the point's distance from the lap's centre), and a prism the Goby spins round stretches out into a bar.
    /// We take that image from the unroll itself: a prism's eight box corners are each mapped through the ribbon
    /// (<see cref="PistolRibbon.MapNear"/>, a window round its centre's segment wide enough for that stretch), and its
    /// mesh is deformed trilinearly inside the warped box, so any prism mesh warps consistently; a prism that holds the
    /// lap's centre is drawn as the bar of the whole run of path it is beside (<see cref="PistolRibbon.LapExtent"/>).
    /// Then the point is laid on the screen plane with 2.5D parallax about the PISTOL: a layer at depth d draws at
    /// D / (D + d) of its size (the lab's <c>pParallax</c>: "layers this deep draw at half size"), which is what makes a
    /// sidescroller read as deep without the perspective distortion a wide camera would add, and keeps the Pistol's
    /// end-on body one screen point at every depth. Pure; <c>PistolWorldMathTests</c> / <c>PistolViewMathTests</c>
    /// hold the stretch, the zones and the projection to their formulas.
    /// </summary>
    public static class PistolWarp
    {
        /// <summary>How a prism is drawn: corners mapped (Straight / Bend), or the whole run of the lap it sits inside (Bar).</summary>
        public enum Kind { Straight = 0, Bend = 1, Bar = 2 }

        const int Slack = 2;

        /// <summary>Corner i of a box: bit 0 = +ax, bit 1 = +ay, bit 2 = +az (else the minus side).</summary>
        public static void BoxCorners(Vector3 centre, Vector3 ax, Vector3 ay, Vector3 az, Vector3[] into8)
        {
            for (int i = 0; i < 8; i++)
                into8[i] = centre + ((i & 1) != 0 ? ax : -ax) + ((i & 2) != 0 ? ay : -ay) + ((i & 4) != 0 ? az : -az);
        }

        /// <summary>How the prism is drawn and, for corner mapping, how many segments either side of the centre's segment
        /// the corners may map to. On a straight path a corner is within a segment or two of the centre; inside a bend
        /// of radius ρ the corners of a box at distance r from the bend's centre subtend asin(hd / r), so they spread
        /// ρ·asin(hd / r) along the path; a box that holds the centre (r ≤ hd + half a sample) is beside the whole lap.</summary>
        public static Kind Classify(float halfDiagonal, float curvature, float d, float sampleGap, int max, out int window)
        {
            window = Mathf.Min(Slack, Mathf.Max(0, max));
            float k = Mathf.Abs(curvature);
            if (k < 1e-6f || sampleGap <= 0f) return Kind.Straight;
            float rho = 1f / k, centre = curvature > 0f ? rho : -rho;   // the centre of curvature's d
            float r = Mathf.Abs(centre - d);
            if (r <= halfDiagonal + 0.5f * sampleGap) { window = max; return Kind.Bar; }
            float arc = rho * Mathf.Asin(Mathf.Min(1f, halfDiagonal / r));
            window = Mathf.Clamp(Mathf.CeilToInt(arc / sampleGap) + Slack, Slack, Mathf.Max(Slack, max));
            return window <= Slack ? Kind.Straight : Kind.Bend;
        }

        /// <summary>The corner window alone (the lab's <c>cornerWindow</c>; <see cref="Classify"/>'s number).</summary>
        public static int CornerWindow(float halfDiagonal, float curvature, float d, float sampleGap, int max)
        {
            Classify(halfDiagonal, curvature, d, sampleGap, max, out int w); return w;
        }

        /// <summary>The distance to the bend's centre from a point at lateral d (∞ on a straight).</summary>
        public static float CentreDistance(float curvature, float d)
        {
            float k = Mathf.Abs(curvature); if (k < 1e-6f) return float.PositiveInfinity;
            float rho = 1f / k; return Mathf.Abs((curvature > 0f ? rho : -rho) - d);
        }

        /// <summary>Map the eight corners through the ribbon round the centre's segment. False if a corner cannot be mapped
        /// (the ribbon has no segment for it in the window).</summary>
        public static bool WarpBox(PistolRibbon rb, int kCentre, Vector3[] corners8, float xLo, float xHi, int halfWindow, Vector3[] xhd8, bool beyondEnd = false)
        {
            for (int i = 0; i < 8; i++)
            {
                var m = rb.MapNear(corners8[i], kCentre, halfWindow, xLo, xHi, beyondEnd);
                if (!m.Ok) return false;
                xhd8[i] = new Vector3(m.X, m.H, m.D);
            }
            return true;
        }

        /// <summary>The widest x-range corner mapping may honestly give a box of half-diagonal hd at distance r inside a
        /// bend of radius ρ; past it a corner landed on another pass of the path inside the window (a fold), and the
        /// prism is drawn rigid instead (<see cref="RigidCorners"/>).</summary>
        public static float TearBound(float halfDiagonal, float curvature, float d, float sampleGap)
        {
            float r = CentreDistance(curvature, d);
            float stretch = float.IsInfinity(r) ? 2f * halfDiagonal : 2f * halfDiagonal * (1f / Mathf.Abs(curvature)) / Mathf.Max(r, 1e-3f);
            return stretch + 4f * sampleGap + 8f;
        }

        public static float XRange(Vector3[] xhd8)
        {
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            for (int i = 0; i < 8; i++) { lo = Mathf.Min(lo, xhd8[i].x); hi = Mathf.Max(hi, xhd8[i].x); }
            return hi - lo;
        }

        /// <summary>A box drawn RIGID at its mapped centre: the half-axes projected on the ribbon's frame there.</summary>
        public static void RigidCorners(Vector3 xhdCentre, Vector3 ax, Vector3 ay, Vector3 az, Vector3 T, Vector3 U, Vector3 R, Vector3[] xhd8)
        {
            Vector3 a = new(Vector3.Dot(ax, T), Vector3.Dot(ax, U), Vector3.Dot(ax, R));
            Vector3 b = new(Vector3.Dot(ay, T), Vector3.Dot(ay, U), Vector3.Dot(ay, R));
            Vector3 c = new(Vector3.Dot(az, T), Vector3.Dot(az, U), Vector3.Dot(az, R));
            BoxCorners(xhdCentre, a, b, c, xhd8);
        }

        /// <summary>The bar a prism holding a bend's centre is drawn as: the run [xA, xB] of path it is beside, its
        /// height and depth from its half-axes in the frame, grown in about the run's midpoint by <paramref name="grow"/> (0.5 → 1).</summary>
        public static void BarCorners(float xA, float xB, Vector3 xhdCentre, Vector3 ax, Vector3 ay, Vector3 az, Vector3 U, Vector3 R, float grow, Vector3[] xhd8)
        {
            float hU = Mathf.Abs(Vector3.Dot(ax, U)) + Mathf.Abs(Vector3.Dot(ay, U)) + Mathf.Abs(Vector3.Dot(az, U));
            float hR = Mathf.Abs(Vector3.Dot(ax, R)) + Mathf.Abs(Vector3.Dot(ay, R)) + Mathf.Abs(Vector3.Dot(az, R));
            float mid = 0.5f * (xA + xB), half = 0.5f * (xB - xA) * Mathf.Clamp(grow, 0.5f, 1f);
            BoxCorners(new Vector3(mid, xhdCentre.y, xhdCentre.z), new Vector3(half, 0f, 0f), new Vector3(0f, hU, 0f), new Vector3(0f, 0f, hR), xhd8);
        }

        /// <summary>The bar's run [xA, xB] for a box of corners <paramref name="corners8"/> whose centre maps to segment
        /// <paramref name="kCentre"/> inside a bend of radius ρ: the run of path within ρ + hd + 2 of the centre
        /// (<see cref="PistolRibbon.LapExtent"/>); an end that stopped on the distance (an exit from the lap, where the
        /// threshold overshoots the honest image by up to ~22 u) is tightened to the corners' own x there.</summary>
        public static void BarExtent(PistolRibbon rb, Vector3 centre, int kCentre, float rho, float halfDiagonal, Vector3[] corners8, float xLo, float xHi, out float xA, out float xB)
        {
            rb.LapExtent(centre, kCentre, rho + halfDiagonal + 2f, xLo, xHi, out int kLo, out int kHi, out bool loByDist, out bool hiByDist);
            xA = rb.X(kLo); xB = rb.X(Mathf.Min(rb.Count - 1, kHi + 1));
            if (loByDist)
            {
                float lo = float.PositiveInfinity;
                for (int i = 0; i < 8; i++) { var m = rb.MapNear(corners8[i], kLo, 6, xLo, xHi); if (m.Ok) lo = Mathf.Min(lo, m.X); }
                if (!float.IsInfinity(lo)) xA = Mathf.Max(xA, lo);
            }
            if (hiByDist)
            {
                float hi = float.NegativeInfinity;
                for (int i = 0; i < 8; i++) { var m = rb.MapNear(corners8[i], kHi, 6, xLo, xHi); if (m.Ok) hi = Mathf.Max(hi, m.X); }
                if (!float.IsInfinity(hi)) xB = Mathf.Min(xB, hi);
            }
            if (xB < xA + 1f) { float mid = 0.5f * (xA + xB); xA = mid - 0.5f; xB = mid + 0.5f; }
        }

        /// <summary>The point at (u, v, w) ∈ [0,1]³ of the warped box (u along the box's first axis, as <see cref="BoxCorners"/> ordered it).</summary>
        public static Vector3 Trilinear(Vector3[] xhd8, float u, float v, float w)
        {
            Vector3 c00 = Vector3.LerpUnclamped(xhd8[0], xhd8[1], u), c10 = Vector3.LerpUnclamped(xhd8[2], xhd8[3], u);
            Vector3 c01 = Vector3.LerpUnclamped(xhd8[4], xhd8[5], u), c11 = Vector3.LerpUnclamped(xhd8[6], xhd8[7], u);
            return Vector3.LerpUnclamped(Vector3.LerpUnclamped(c00, c10, v), Vector3.LerpUnclamped(c01, c11, v), w);
        }

        /// <summary>The x-extent of a warped box over its longest world extent: 1 on a straight path, R / r inside a bend.</summary>
        public static float Stretch(Vector3[] xhd8, Vector3[] corners8)
        {
            float span = 0f;
            for (int i = 0; i < 8; i++) for (int j = i + 1; j < 8; j++) span = Mathf.Max(span, (corners8[i] - corners8[j]).magnitude);
            return span > 1e-6f ? XRange(xhd8) / span : 1f;
        }

        // ---- the screen

        /// <summary>A layer at depth d draws at this scale (the lab's pParallax: half size at d = D).</summary>
        public static float Scale(float d, float D) => D / Mathf.Max(D + d, 0.05f * D);

        /// <summary>2.5D parallax about the PISTOL: the point (x, h, d) on the screen plane for a camera centred at
        /// (scX, scY), with the parallax centre (cx, cy) = the Pistol's x and the camera's height less its lift. Every
        /// point of the Pistol's end-on axis then draws at one screen x whatever its depth, and a far layer's screen y
        /// moves only with the eased camera, never with the Pistol's jump. z = d.</summary>
        public static Vector3 Parallax(float x, float h, float d, float scX, float scY, float cx, float cy, float D)
        {
            float s = Scale(d, D);
            return new Vector3((cx - scX) + (x - cx) * s, cy + (h - cy) * s, d);
        }

        /// <summary>The camera-centred form (the lab's first parallax, kept for the tests that hold the halving).</summary>
        public static Vector3 Parallax(float x, float h, float d, float scX, float scY, float D) => Parallax(x, h, d, scX, scY, scX, scY, D);

        /// <summary>The side camera's off-axis frustum at its near plane: the eye at (cx, cy, -D) looking into the depth, the
        /// plane d = 0 showing W × W / aspect centred on (scX, scY). Its projection IS <see cref="Parallax"/> - a real
        /// perspective camera draws the lab's 2.5D parallax about the Pistol exactly, so the world can be drawn as itself.</summary>
        public static void Frustum(float scX, float scY, float cx, float cy, float D, float W, float aspect, float near,
            out float left, out float right, out float bottom, out float top)
        {
            float H = W / Mathf.Max(0.2f, aspect), k = near / Mathf.Max(1f, D);
            left = (scX - 0.5f * W - cx) * k; right = (scX + 0.5f * W - cx) * k;
            bottom = (scY - 0.5f * H - cy) * k; top = (scY + 0.5f * H - cy) * k;
        }

        /// <summary>A world transform (a prism's or a renderer's localToWorld) taken into the Pistol's flat world: its rotation and
        /// scale seen from the ribbon's frame (rows T, U, R: x along the path, h up, d across), placed at <paramref name="xhd"/>.
        /// The columns are what <see cref="RigidCorners"/> does to a box's half-axes, so a prism drawn with its OWN mesh through
        /// this matrix lands exactly where its rigid box did.</summary>
        public static Matrix4x4 FlatTransform(in Matrix4x4 localToWorld, Vector3 T, Vector3 U, Vector3 R, Vector3 xhd)
        {
            var m = Matrix4x4.identity;
            for (int i = 0; i < 3; i++)
            {
                Vector3 c = localToWorld.GetColumn(i);
                m.SetColumn(i, new Vector4(Vector3.Dot(c, T), Vector3.Dot(c, U), Vector3.Dot(c, R), 0f));
            }
            m.SetColumn(3, new Vector4(xhd.x, xhd.y, xhd.z, 1f));
            return m;
        }

        /// <summary>A ground tile's transform: the block's box (<see cref="TileCorners"/>) as a matrix for its own mesh - axis-aligned
        /// on the line at x, tilted to the slope, at its depth; <paramref name="extents"/> and <paramref name="centre"/> are the
        /// mesh's local bounds, so the mesh's box lands on the tile's.</summary>
        public static Matrix4x4 TileTransform(float x, float groundY, float slope, float d, Vector3 ax, Vector3 ay, Vector3 az,
            Vector3 T, Vector3 U, Vector3 R, Vector3 extents, Vector3 centre)
        {
            float hT = Mathf.Abs(Vector3.Dot(ax, T)) + Mathf.Abs(Vector3.Dot(ay, T)) + Mathf.Abs(Vector3.Dot(az, T));
            float hU = Mathf.Abs(Vector3.Dot(ax, U)) + Mathf.Abs(Vector3.Dot(ay, U)) + Mathf.Abs(Vector3.Dot(az, U));
            float hR = Mathf.Abs(Vector3.Dot(ax, R)) + Mathf.Abs(Vector3.Dot(ay, R)) + Mathf.Abs(Vector3.Dot(az, R));
            float th = Mathf.Atan(slope), cs = Mathf.Cos(th), sn = Mathf.Sin(th);
            float sx = hT / Mathf.Max(1e-6f, extents.x), sy = hU / Mathf.Max(1e-6f, extents.y), sz = hR / Mathf.Max(1e-6f, extents.z);
            var m = Matrix4x4.identity;
            m.SetColumn(0, new Vector4(cs * sx, sn * sx, 0f, 0f));
            m.SetColumn(1, new Vector4(-sn * sy, cs * sy, 0f, 0f));
            m.SetColumn(2, new Vector4(0f, 0f, sz, 0f));
            Vector3 at = new Vector3(x, groundY, d) - (Vector3)(m * new Vector4(centre.x, centre.y, centre.z, 0f));
            m.SetColumn(3, new Vector4(at.x, at.y, at.z, 1f));
            return m;
        }

        /// <summary>Where a prism at (x, h, d) is drawn on a W × (W / aspect) screen, with its size allowed for.</summary>
        public static bool OnScreen(float x, float h, float d, float scX, float scY, float cx, float cy, float D, float W, float aspect, float size)
        {
            var p = Parallax(x, h, d, scX, scY, cx, cy, D); float s = Scale(d, D);
            return Mathf.Abs(p.x) <= 0.5f * W + size * s && Mathf.Abs(p.y - scY) <= 0.5f * W / aspect + size * s;
        }

        /// <summary>How a prism at depth d is drawn: what the Pistol can reach is vivid and solid, the near field behind the
        /// plane fogs a little, the deep background is a rigid box sinking into the fog, the foreground is see-through.</summary>
        public struct Zone { public bool Drawn, Warped, Solid; public float Reach, Fog, Alpha, D; }

        public static Zone ZoneOf(float d, float band, float vision, float D, bool primed)
        {
            float near = 3f * vision, dLo = -0.5f * D, dHi = 3f * D, ad = Mathf.Abs(d);
            var z = new Zone { Drawn = true, Warped = true, Solid = true, D = d, Reach = 1f - Mathf.Clamp01((ad - band) / 12f) };
            if (d < dLo || d > dHi)
            {
                if (!primed) { z.Drawn = false; return z; }
                z.D = d < dLo ? dLo : dHi; z.Warped = false; z.Solid = false; z.Reach = 0f; z.Alpha = 0.35f; z.Fog = 0.55f; return z;
            }
            if (ad <= band) { z.Fog = 0f; z.Alpha = 1f; }
            else if (d > 0f && d <= near) { z.Fog = 0.35f * (d - band) / Mathf.Max(1f, near - band); z.Alpha = 1f; }
            else if (d > near) { float f = (d - near) / Mathf.Max(1f, dHi - near); z.Fog = 0.35f + 0.5f * f; z.Alpha = 1f - 0.55f * f; z.Warped = false; z.Solid = false; }
            else { float f = (-d - band) / Mathf.Max(1f, -dLo - band); z.Fog = 0.15f * f; z.Alpha = Mathf.Lerp(0.6f, 0.08f, f); z.Solid = false; }
            return z;
        }

        /// <summary>The forecast: a prism past the Goby's x is not laid yet — it sinks into the fog and thins.</summary>
        public static void Forecast(ref Zone z) { z.Fog = Mathf.Max(z.Fog, 0.55f); z.Alpha *= 0.6f; z.Solid = false; }

        /// <summary>A ground tile (the Goby's own trail) is drawn rigid, ON the line at its x, tilted to the slope, at its own depth.</summary>
        public static void TileCorners(float x, float groundY, float slope, float d, Vector3 ax, Vector3 ay, Vector3 az, Vector3 T, Vector3 U, Vector3 R, Vector3[] xhd8)
        {
            float hT = Mathf.Abs(Vector3.Dot(ax, T)) + Mathf.Abs(Vector3.Dot(ay, T)) + Mathf.Abs(Vector3.Dot(az, T));
            float hU = Mathf.Abs(Vector3.Dot(ax, U)) + Mathf.Abs(Vector3.Dot(ay, U)) + Mathf.Abs(Vector3.Dot(az, U));
            float hR = Mathf.Abs(Vector3.Dot(ax, R)) + Mathf.Abs(Vector3.Dot(ay, R)) + Mathf.Abs(Vector3.Dot(az, R));
            float th = Mathf.Atan(slope), cs = Mathf.Cos(th), sn = Mathf.Sin(th);
            BoxCorners(new Vector3(x, groundY, d), new Vector3(cs, sn, 0f) * hT, new Vector3(-sn, cs, 0f) * hU, new Vector3(0f, 0f, hR), xhd8);
        }

        /// <summary>How warm the ground line is drawn: downslopes (the dives Tiny Wings is about) toward the Pistol's orange.</summary>
        public static float DiveTint(float slope) => 0.6f * Mathf.Clamp01(-slope / 0.5f);

        /// <summary>The lab's sideCam: a fifth of a screen ahead of the Pistol, a little above the ground or the Pistol.</summary>
        public static void SideCamTarget(float px, float py, float groundY, float W, float aspect, out float tx, out float ty)
        {
            tx = px + 0.2f * W; ty = Mathf.Max(py, groundY) + 0.08f * (W / aspect);
        }

        /// <summary>Where the off-screen Goby's arrow goes: the right edge, at the ground's end height clamped to the screen; false when it is on screen.</summary>
        public static bool ArrowPlace(float gobyX, float endY, float scX, float scY, float W, float aspect, out float x, out float y)
        {
            x = scX + 0.5f * W - 6f; float halfH = 0.5f * W / aspect - 8f;
            y = Mathf.Clamp(endY, scY - halfH, scY + halfH);
            return gobyX - scX > 0.5f * W - 8f;
        }

        /// <summary>The flat frame as a rotation: x → T, y → U, z → R (what turns the sky with the Goby's heading).</summary>
        public static Quaternion FlatFrame(Vector3 T, Vector3 U, Vector3 R)
        {
            Vector3 fwd = R.sqrMagnitude > 1e-8f ? R : Vector3.forward, up = U.sqrMagnitude > 1e-8f ? U : Vector3.up;
            return Quaternion.LookRotation(fwd, up);
        }

        /// <summary>The vortex ring's tube radius as a share of its radius (the lab's <c>TorusGeometry(1, 0.16, ...)</c>).</summary>
        public const float VortexTube = 0.16f;

        /// <summary>Vertices <see cref="VortexRingFacets"/> writes: three per triangle, two triangles per facet.</summary>
        public static int VortexRingVertexCount(int seg, int side) => seg * side * 6;

        /// <summary>
        /// The vortex ring as a FACETED volume (nothing in the world looks flat, LOCKED 2026-10-18,
        /// <c>Docs/claude/SHADERS_AND_PERFORMANCE.md</c>; the lab's <c>TorusGeometry(1, tube, side, seg)</c> drawn
        /// <c>flatShading</c>): a unit torus in the xy plane, its axis +z, <paramref name="seg"/> facets round the ring and
        /// <paramref name="side"/> round the tube. Every triangle has its own three vertices and its face normal on each,
        /// so it shades flat, and the faces look out of the tube. Fills <paramref name="v"/> and <paramref name="n"/>
        /// (<see cref="VortexRingVertexCount"/> each); triangle k is vertices 3k, 3k + 1, 3k + 2.
        /// </summary>
        public static void VortexRingFacets(float tube, int seg, int side, Vector3[] v, Vector3[] n)
        {
            int k = 0;
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                {
                    Vector3 p00 = RingPoint(tube, seg, side, i, j), p10 = RingPoint(tube, seg, side, i + 1, j);
                    Vector3 p11 = RingPoint(tube, seg, side, i + 1, j + 1), p01 = RingPoint(tube, seg, side, i, j + 1);
                    Face(v, n, ref k, p00, p10, p11); Face(v, n, ref k, p00, p11, p01);
                }
        }

        static Vector3 RingPoint(float tube, int seg, int side, int i, int j)
        {
            float a = i / (float)seg * Mathf.PI * 2f, b = j / (float)side * Mathf.PI * 2f, rr = 1f + tube * Mathf.Cos(b);
            return new Vector3(rr * Mathf.Cos(a), rr * Mathf.Sin(a), tube * Mathf.Sin(b));
        }

        static void Face(Vector3[] v, Vector3[] n, ref int k, Vector3 a, Vector3 b, Vector3 c)
        {
            var f = Vector3.Cross(b - a, c - a).normalized;
            v[k] = a; v[k + 1] = b; v[k + 2] = c; n[k] = n[k + 1] = n[k + 2] = f; k += 3;
        }
    }
}