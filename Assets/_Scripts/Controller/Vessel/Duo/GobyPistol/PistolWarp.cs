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
    /// mesh is deformed trilinearly inside the warped box, so any prism mesh warps consistently. Then the point is laid
    /// on the screen plane with 2.5D parallax: a layer at depth d draws at D / (D + d) of its size about the camera's
    /// centre (the lab's <c>pParallax</c>: "layers this deep draw at half size"), which is what makes a sidescroller
    /// read as deep without the perspective distortion a wide camera would add. Pure; <c>PistolWorldMathTests</c> hold
    /// the stretch to its formula.
    /// </summary>
    public static class PistolWarp
    {
        /// <summary>Corner i of a box: bit 0 = +ax, bit 1 = +ay, bit 2 = +az (else the minus side).</summary>
        public static void BoxCorners(Vector3 centre, Vector3 ax, Vector3 ay, Vector3 az, Vector3[] into8)
        {
            for (int i = 0; i < 8; i++)
                into8[i] = centre + ((i & 1) != 0 ? ax : -ax) + ((i & 2) != 0 ? ay : -ay) + ((i & 4) != 0 ? az : -az);
        }

        /// <summary>How many segments either side of the centre's segment the corners may map to. On a straight path a
        /// corner is within a segment or two of the centre; inside a bend the corners spread by R / r, and when the
        /// prism holds the bend's centre (r ≤ its half-diagonal) its image is the whole lap, so the window is everything.</summary>
        public static int CornerWindow(float halfDiagonal, float curvature, float d, float sampleGap, int max)
        {
            const int Slack = 2;
            if (max < Slack) return max;
            float k = Mathf.Abs(curvature);
            if (k < 1e-6f || sampleGap <= 0f) return Slack;
            float rho = 1f / k, centre = curvature > 0f ? rho : -rho;   // the centre of curvature's d
            float r = Mathf.Abs(centre - d);
            if (r <= halfDiagonal) return max;
            float arc = halfDiagonal / r * rho;                           // the corners' half-extent along the path
            int w = Mathf.CeilToInt(arc / sampleGap) + Slack;
            return Mathf.Clamp(w, Slack, max);
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
            float xMin = float.PositiveInfinity, xMax = float.NegativeInfinity, span = 0f;
            for (int i = 0; i < 8; i++) { xMin = Mathf.Min(xMin, xhd8[i].x); xMax = Mathf.Max(xMax, xhd8[i].x); }
            for (int i = 0; i < 8; i++) for (int j = i + 1; j < 8; j++) span = Mathf.Max(span, (corners8[i] - corners8[j]).magnitude);
            return span > 1e-6f ? (xMax - xMin) / span : 1f;
        }

        /// <summary>2.5D parallax: the point (x, h, d) on the screen plane for a camera centred at (scX, scY) whose plane
        /// is <paramref name="D"/> away: a layer at depth d draws at D / (D + d) of its size about the centre, z = d.</summary>
        public static Vector3 Parallax(float x, float h, float d, float scX, float scY, float D)
        {
            float s = D / Mathf.Max(D + d, 0.05f * D);
            return new Vector3((x - scX) * s, scY + (h - scY) * s, d);
        }
    }
}
