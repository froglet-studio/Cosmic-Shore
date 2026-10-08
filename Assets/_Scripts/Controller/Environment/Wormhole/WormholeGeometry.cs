using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The pure maths of a <see cref="WormholeMouth"/> pair — no scene, no rendering, so every rule
    /// here is covered by edit-mode tests (<c>WormholeGeometryTests</c>) and the shader and the
    /// runtime cannot disagree about what a wormhole IS.
    ///
    /// <para><b>The model: two balls with ONE shared interior.</b> The inside of mouth A is the
    /// inside of mouth B, displaced by a pure translation <c>Δ = B − A</c>. So:</para>
    /// <list type="bullet">
    /// <item><b>A transit is a translation.</b> A pilot whose step enters ball A is moved by Δ and
    /// is then inside ball B, at exactly the place relative to it that they were relative to A —
    /// heading, speed and rotation untouched (<see cref="Through"/>).</item>
    /// <item><b>The view through A is the world seen from the viewer's own vantage carried by
    /// Δ</b>, with everything between that vantage and ball B removed. The removal is laid as one
    /// oblique near plane, tangent to ball B at its point nearest the vantage
    /// (<see cref="TryNearCapPlane"/>): all of ball B lies beyond it, so a ship that has just been
    /// carried into B is visible through A on the very frame it moved — the transit has no gap.
    /// The approximation is a sliver beside B's near cap (outside the ball but beyond the plane);
    /// it only matters for something hugging the far mouth on the viewer's side.</item>
    /// <item><b>It holds from every side</b> because nothing about the model has a facing: the
    /// view is re-derived from wherever the viewer is, every frame.</item>
    /// </list>
    ///
    /// <para>The second half of the picture — a mouth seen by a camera that is NOT the player's
    /// (or one too far away to be worth an exact render) — is a <b>panorama</b>: each mouth's
    /// attached camera captures its surroundings in all six directions, and the partner's surface
    /// projects that capture in all directions (<see cref="FaceOf"/>, <see cref="FaceUV"/>,
    /// <see cref="ParallaxDirection"/>). The shader carries the same face table, written out.</para>
    /// </summary>
    public static class WormholeGeometry
    {
        /// <summary>Smallest distance outside a mouth at which a camera is treated as already IN
        /// it, world units — see <see cref="Clearance"/>.</summary>
        public const float MinClearance = 0.5f;

        /// <summary>
        /// How close a camera may come to a mouth's surface before it counts as inside the mouth.
        /// A camera nearer than its own near clip would cut a hole in the sphere and show the near
        /// side through it for a frame, so the camera carry hands over at this distance and the
        /// surface stops drawing for a camera within it. ONE number for both, so the hand-over
        /// lands the camera exactly where the far mouth has just stopped drawing.
        /// </summary>
        public static float Clearance(float nearClip) => Mathf.Max(MinClearance, nearClip * 3f);

        /// <summary>The far mouth's position for a point relative to the near one: a pure translation.</summary>
        public static Vector3 Through(Vector3 point, Vector3 nearCentre, Vector3 farCentre) =>
            point + (farCentre - nearCentre);

        /// <summary>
        /// Did a step from <paramref name="from"/> to <paramref name="to"/> ENTER the ball? It must
        /// start outside, and the segment must reach the ball — so a step that clips the ball and
        /// leaves it again in one frame counts (it went through the shared interior), while a step
        /// that begins inside never does (a pilot just carried into the far mouth must fly out of it
        /// before it can take them back).
        /// </summary>
        public static bool SegmentEntersBall(Vector3 from, Vector3 to, Vector3 centre, float radius)
        {
            if (radius <= 0f) return false;
            if ((from - centre).sqrMagnitude <= radius * radius) return false;
            return SegmentHitsBall(from, to, centre, radius);
        }

        /// <summary>Does the closed segment <paramref name="a"/>→<paramref name="b"/> come within
        /// <paramref name="radius"/> of <paramref name="centre"/>?</summary>
        public static bool SegmentHitsBall(Vector3 a, Vector3 b, Vector3 centre, float radius)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 1e-12f ? Mathf.Clamp01(Vector3.Dot(centre - a, ab) / len2) : 0f;
            return (a + ab * t - centre).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// The point on the ball's surface nearest <paramref name="point"/> — where a ribbon is cut
        /// on the way in. A point exactly at the centre has no nearest point; any direction will do.
        /// </summary>
        public static Vector3 NearestSurfacePoint(Vector3 point, Vector3 centre, float radius)
        {
            Vector3 rel = point - centre;
            float m = rel.magnitude;
            return m > 1e-6f ? centre + rel * (radius / m) : centre + Vector3.forward * radius;
        }

        /// <summary>
        /// The clip plane for the view through a mouth: tangent to the FAR ball at its point
        /// nearest <paramref name="eye"/> (the viewer's vantage already carried through), with its
        /// normal pointing AWAY from the eye — so what is kept is ball B and everything beyond it.
        /// False when the eye is inside the ball, where there is nothing in front of it to remove.
        /// </summary>
        public static bool TryNearCapPlane(Vector3 eye, Vector3 farCentre, float radius,
                                           out Vector3 normal, out Vector3 point)
        {
            Vector3 toCentre = farCentre - eye;
            float d = toCentre.magnitude;
            if (d <= radius || d < 1e-6f)
            {
                normal = Vector3.forward;
                point = farCentre;
                return false;
            }
            normal = toCentre / d;
            point = farCentre - normal * radius;
            return true;
        }

        /// <summary>
        /// Narrow a projection to a sub-rectangle of its viewport (0..1), the window's own
        /// footprint: x' = (x − c·w) / h for the rectangle's NDC centre c and half-extent h — a
        /// rewrite of rows 0 and 1 against the w row, so depth and an oblique near plane are
        /// untouched and culling follows the crop. (The arithmetic the Butterfly's retired fold-gate
        /// window introduced.)
        /// </summary>
        public static Matrix4x4 Crop(Matrix4x4 projection, Rect footprint)
        {
            if (footprint.width <= 0f || footprint.height <= 0f) return projection;
            float cx = footprint.xMin + footprint.xMax - 1f, hx = footprint.width;
            float cy = footprint.yMin + footprint.yMax - 1f, hy = footprint.height;
            Vector4 row3 = projection.GetRow(3);
            projection.SetRow(0, (projection.GetRow(0) - row3 * cx) / hx);
            projection.SetRow(1, (projection.GetRow(1) - row3 * cy) / hy);
            return projection;
        }

        // ---- sizing the exact view's render target -------------------------------------------

        /// <summary>The exact target is sized in steps of this many texels, so a footprint growing
        /// a few pixels a frame does not reallocate it every frame.</summary>
        public const int TexelQuantum = 32;

        /// <summary>Headroom a reallocated target takes over the footprint it was sized for, so an
        /// approaching mouth grows into it instead of reallocating at every step.</summary>
        const float GrowHeadroom = 1.25f;

        /// <summary>A target this many times larger than the footprint needs (on both axes) is
        /// reallocated smaller — the receding mouth stops paying for the size it had up close.</summary>
        const float ShrinkSlack = 1.6f;

        static int Quantize(float texels) =>
            Mathf.Max(TexelQuantum, Mathf.CeilToInt(texels / TexelQuantum) * TexelQuantum);

        /// <summary>The side a freshly allocated target takes for a footprint needing
        /// <paramref name="need"/> texels: quantized, with grow headroom, never above the cap.</summary>
        public static int TargetSize(int need, int cap) => Mathf.Min(cap, Quantize(need * GrowHeadroom));

        /// <summary>
        /// Whether an existing target still serves a footprint: it covers it, and is not grossly
        /// larger on both axes. The shrink bound is QUANTIZED exactly as the allocation is — a raw
        /// <c>need * ShrinkSlack</c> bound rejected the target <see cref="TargetSize"/> had just made
        /// for the same footprint (need 32 → a 64 target → 64 > 51.2), so a distant window, whose
        /// footprint sits on the 32-texel floor, reallocated its target every frame. Since Quantize
        /// only rounds up, <c>TargetSize(n) ≤ Quantize(n × ShrinkSlack)</c> for every n, so a fresh
        /// target always fits the footprint it was sized for. (Carried over from the Butterfly's
        /// retired fold-gate window, with its tests.)
        /// </summary>
        public static bool TargetFits(int width, int height, int needW, int needH) =>
            width >= needW && height >= needH
            && (width <= Quantize(needW * ShrinkSlack) || height <= Quantize(needH * ShrinkSlack));

        // ---- the panorama: six 90° faces, one table shared with Wormhole.shader ------------------

        /// <summary>Faces in the panorama's texture-array order.</summary>
        public const int FaceCount = 6;

        static readonly Vector3[] FaceForward =
        {
            Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
        };

        static readonly Vector3[] FaceUp =
        {
            Vector3.up, Vector3.up, Vector3.back, Vector3.forward, Vector3.up, Vector3.up,
        };

        /// <summary>The rotation the panorama camera takes to render <paramref name="face"/>.</summary>
        public static Quaternion FaceRotation(int face) =>
            Quaternion.LookRotation(FaceForward[face], FaceUp[face]);

        /// <summary>Which face a world direction falls on: its largest component, ties to x then y.</summary>
        public static int FaceOf(Vector3 dir)
        {
            float ax = Mathf.Abs(dir.x), ay = Mathf.Abs(dir.y), az = Mathf.Abs(dir.z);
            if (ax >= ay && ax >= az) return dir.x >= 0f ? 0 : 1;
            if (ay >= az) return dir.y >= 0f ? 2 : 3;
            return dir.z >= 0f ? 4 : 5;
        }

        /// <summary>
        /// Where <paramref name="dir"/> lands in <paramref name="face"/>'s image, 0..1 with (0,0)
        /// bottom-left: its camera-space x and y over its depth, for a 90° square frustum.
        /// <c>right = up × forward</c>, which is the camera's own right for
        /// <c>LookRotation(forward, up)</c>.
        /// </summary>
        public static Vector2 FaceUV(int face, Vector3 dir)
        {
            Vector3 f = FaceForward[face], u = FaceUp[face];
            Vector3 r = Vector3.Cross(u, f);
            float z = Vector3.Dot(dir, f);
            if (z <= 1e-6f) return new Vector2(0.5f, 0.5f);
            return new Vector2(Vector3.Dot(dir, r) / z * 0.5f + 0.5f,
                               Vector3.Dot(dir, u) / z * 0.5f + 0.5f);
        }

        /// <summary>
        /// The direction to sample a panorama captured at the far mouth's centre, for a view ray
        /// that enters the near mouth at <paramref name="relative"/> (the entry point relative to
        /// the near centre — the same offset from the far centre, since the map is a translation)
        /// travelling along <paramref name="viewDir"/>.
        ///
        /// <para>A panorama has no depth, so the ray is assumed to end on a proxy sphere of
        /// <paramref name="proxyRadius"/> about the capture point, and the sample is the direction
        /// of that end point. At an infinite proxy this is the view direction itself (exact for the
        /// distant world); a finite proxy restores the parallax of things that far away.</para>
        /// </summary>
        public static Vector3 ParallaxDirection(Vector3 relative, Vector3 viewDir, float proxyRadius)
        {
            float b = Vector3.Dot(relative, viewDir);
            float c = relative.sqrMagnitude - proxyRadius * proxyRadius;
            float t = -b + Mathf.Sqrt(Mathf.Max(b * b - c, 0f));
            return relative + viewDir * Mathf.Max(t, 0f);
        }
    }
}
