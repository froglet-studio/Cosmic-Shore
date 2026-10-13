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
    /// JS twin: <c>depthAdvance()</c> (mode 2) and <c>seatOf()</c> in <c>Docs/Studios/OmniShepardLab.html</c>.
    /// Change one, change both.
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
        public static Column Step(Column c, float depth, float seat, float dt, float reseatDepth, float reseatFadeSeconds)
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
            else if (depth < reseatDepth)
            {
                c.LeavingPlace = c.Place;
                c.Fade = 0f;
                c.Place = seat;
            }
            return c;
        }
    }
}
