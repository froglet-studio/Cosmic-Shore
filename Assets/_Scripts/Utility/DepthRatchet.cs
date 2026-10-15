using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The omni and Mass crystals' "sealed by depth" triangles, as pure math (Omni Shepard Lab round 7,
    /// <c>Docs/Studios/OMNI_SHEPARD_LAB.md</c>). Every triangular prism sits in a column over its own hole,
    /// and WHERE in the column is a function of its depth from the camera, not of a clock:
    /// <c>d = plateDirection · cameraDirection</c> in the crystal's own space (+1 faces the camera, 0 is on
    /// the silhouette, −1 is straight behind). With no clock in the rule, the far side stands open at the
    /// tops of its columns and the near side is sealed whichever way the crystal faces.
    ///
    /// The shipped mode is the RATCHET: in view a prism only ever moves DOWN. Coming toward the camera it
    /// rides <see cref="Seat"/> down into its hole; going away it stays where it got to; once it is behind
    /// the reseat depth (hidden by the body), the old prism fades out in its hole while a new one fades in
    /// at the top of the same column. That is the Shepard tone on a crystal you can turn any way.
    ///
    /// JS twins: <c>depthAdvance()</c> (mode 2) and <c>seatOf()</c> in <c>Docs/Studios/OmniShepardLab.html</c>, and
    /// <c>depthStep()</c>, <c>sealOf()</c>, <c>columnTop()</c> and <c>hiddenBehindBody()</c> in
    /// <c>Docs/Studios/OmniMotionLab.html</c> (rise while hidden, seal at the horizon). Change one, change both.
    /// </summary>
    public static class DepthRatchet
    {
        /// <summary>A prism's place in its column, 0 at the top, 1 sealed in its hole:
        /// <c>smoothstep(open, seal, d) ^ power</c>.</summary>
        public static float Seat(float depth, float openDepth, float sealDepth, float power)
        {
            float e = Mathf.Clamp01((depth - openDepth) / Mathf.Max(sealDepth - openDepth, 1e-3f));
            return Mathf.Pow(e * e * (3f - 2f * e), power);
        }

        /// <summary>
        /// One column's state: <see cref="Place"/> is the arriving prism's place (0 top .. 1 sealed),
        /// <see cref="Fade"/> its fade-in (1 = solid; the leaving prism shows <c>1 − Fade</c>), and
        /// <see cref="LeavingPlace"/> where the leaving prism sits while it fades.
        /// </summary>
        public struct Column
        {
            public float Place;
            public float Fade;
            public float LeavingPlace;

            /// <summary>A column that has never been stepped: seated where its depth puts it, solid.</summary>
            public static Column At(float seat) => new() { Place = seat, Fade = 1f, LeavingPlace = 1f };
        }

        /// <summary>
        /// Advances one column by <paramref name="dt"/> seconds at depth <paramref name="depth"/> (whose seat is
        /// <paramref name="seat"/>). The rule, in order: a column mid-reseat finishes its fade and follows its
        /// depth; a column coming toward the camera follows its depth down; a column going away and now behind
        /// <paramref name="reseatDepth"/> reseats; any other column holds.
        /// </summary>
        public static Column Step(Column c, float depth, float seat, float dt, float reseatDepth, float reseatFadeSeconds) =>
            Step(c, depth, seat, dt, reseatDepth, reseatFadeSeconds, false);

        /// <summary>
        /// <see cref="Step(Column, float, float, float, float, float)"/> with RISE WHILE HIDDEN (Omni Motion Lab round 3):
        /// a column whose seat is above its place may also climb to <see cref="RiseTarget"/> when
        /// <paramref name="riseTargetHidden"/> - the caller's test that the column top at that target is hidden behind
        /// the crystal's body (<see cref="HiddenBehindBody"/>). A prism is never seen rising, every hidden prism stands
        /// out, and the ones coming round the edge come in from the top; the reseat becomes a fallback.
        /// Order: fading, following the seat down, rising while hidden, reseating, holding.
        /// </summary>
        public static Column Step(Column c, float depth, float seat, float dt, float reseatDepth, float reseatFadeSeconds,
            bool riseTargetHidden, float riseTarget = 0f)
        {
            if (c.Fade < 1f)
            {
                c.Fade = Mathf.Min(1f, c.Fade + dt / Mathf.Max(reseatFadeSeconds, 1e-3f));
                c.Place = seat;
            }
            else if (seat >= c.Place)
            {
                c.Place = seat;
            }
            else if (riseTargetHidden)
            {
                c.Place = Mathf.Max(seat, riseTarget);
            }
            else if (depth < reseatDepth)
            {
                c.LeavingPlace = c.Place;
                c.Fade = 0f;
                c.Place = seat;
            }
            return c;
        }

        /// <summary>Where a column rising at <paramref name="riseRate"/> column-heights a second gets to in
        /// <paramref name="dt"/>, never above its seat (place 0 is the top).</summary>
        public static float RiseTarget(Column c, float seat, float dt, float riseRate) => Mathf.Max(seat, c.Place - riseRate * dt);

        /// <summary>
        /// The seal depth at the camera's HORIZON (Omni Motion Lab round 1): a plate at radius
        /// <paramref name="plateRadius"/> faces a camera at distance <paramref name="cameraDistance"/> (both in the
        /// crystal's space) once its depth passes r / D, so sealing there, less <paramref name="margin"/>, puts every
        /// prism the camera can see flush in its hole.
        /// </summary>
        public static float HorizonSealDepth(float plateRadius, float cameraDistance, float margin) =>
            Mathf.Min(0.999f, plateRadius / Mathf.Max(cameraDistance, plateRadius * 1.01f) - margin);

        /// <summary>
        /// The top of a prism standing at <paramref name="place"/> in its column (crystal space): the plate centre carried
        /// out to <c>lerp(reach, 1, place)</c> times itself, plus the prism's own top face, which grows from
        /// <paramref name="sizeAtTop"/> to full size as it seals - the shader's column (<c>OmniDepthPrismShader</c>).
        /// </summary>
        public static Vector3 ColumnTop(Vector3 plateCentre, Vector3 direction, float topHeight, float place, float reach, float sizeAtTop) =>
            plateCentre * (1f + (reach - 1f) * (1f - place)) + direction * (topHeight * (sizeAtTop + (1f - sizeAtTop) * place));

        /// <summary>
        /// Is <paramref name="point"/> hidden from <paramref name="eye"/> by a sphere of <paramref name="bodyRadius"/> at
        /// the origin (the crystal's body, as the lab models it)? True when the segment eye → point enters the sphere
        /// before it reaches the point.
        /// </summary>
        public static bool HiddenBehindBody(Vector3 point, Vector3 eye, float bodyRadius)
        {
            var d = point - eye;
            float length = d.magnitude;
            if (length < 1e-6f) return false;
            var u = d / length;
            float b = Vector3.Dot(eye, u), c = Vector3.Dot(eye, eye) - bodyRadius * bodyRadius, disc = b * b - c;
            if (disc <= 0f) return false;
            float enter = -b - Mathf.Sqrt(disc);
            return enter > 0f && enter < length - 1e-6f;
        }
    }
}
