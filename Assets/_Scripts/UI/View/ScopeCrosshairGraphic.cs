using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Four radial POSTS pointing inward at a reticle, GENERATED rather than sprited — the
    /// locator half of an optical sight, whose measuring half is <see cref="ScopeRingGraphic"/>.
    ///
    /// <para><b>It exists because a reticle drawn at a TRUE angular size is, for a narrow weapon,
    /// a few pixels.</b> The Serpent's sniper cone is a half-angle of <b>0.5°</b>, so the ring
    /// that states it honestly works out to roughly 6–24 px inside the scope's eyepiece and ~8 px
    /// over the flight view — a 2 px hairline about 1% of the window across, on top of a magnified
    /// render of a lit arena. That is the right MEASUREMENT and it is below the threshold of being
    /// noticed, which is exactly how it was reported: <i>"still no reticle in sight."</i></para>
    ///
    /// <para><b>The split is the whole idea: the posts LOCATE and the ring MEASURES.</b> The posts
    /// sit at a FIXED pixel length, so the mark is always the same findable size whatever the
    /// weapon's angle or the view's magnification; their inner ends are placed just outside the
    /// ring, so they point AT the measurement and visibly open up as the pilot zooms in and the
    /// ring grows. Nothing about the ring changes — the number the instrument states is exactly
    /// the number it stated before this existed.</para>
    ///
    /// <para>Antialiasing is baked into the geometry as a zero-alpha feather along each post's
    /// two long sides, for the reason <see cref="ScopeRingGraphic"/> and
    /// <see cref="TrapezoidGraphic"/> both record: a UGUI canvas gives generated geometry none.
    /// The ENDS are left hard — a post is a mark with a deliberate stop at each end, and a faded
    /// inner end would blur the gap that is the whole reason the pilot can see through to the
    /// target.</para>
    ///
    /// <para>Raycasting is off by default and should stay off: this draws over the middle of the
    /// screen, and a hit target there would eat presses meant for the world.</para>
    /// </summary>
    [AddComponentMenu("UI/Scope Crosshair Graphic", 15)]
    public class ScopeCrosshairGraphic : MaskableGraphic
    {
        [Tooltip("Where each post's INNER end sits, in canvas units from the rect's centre. Set " +
                 "this to the reticle ring's radius plus a gap, so the posts point at the ring " +
                 "and the ring stays readable inside them.")]
        [SerializeField, Min(0f)] private float radius = 14f;

        [Tooltip("How far each post reaches OUTWARD from that inner end, in canvas units. Fixed " +
                 "rather than derived: this is the part that has to stay findable at any zoom.")]
        [SerializeField, Min(1f)] private float armLength = 16f;

        [Tooltip("Post width in canvas units.")]
        [SerializeField, Min(0.5f)] private float thickness = 2f;

        [Tooltip("Width of the zero-alpha feather along each side of a post. This IS the " +
                 "antialiasing - a canvas provides none for generated geometry.")]
        [SerializeField, Min(0f)] private float feather = 1f;

        public float Radius
        {
            get => radius;
            set { if (!Mathf.Approximately(radius, value)) { radius = value; SetVerticesDirty(); } }
        }

        public float ArmLength
        {
            get => armLength;
            set { if (!Mathf.Approximately(armLength, value)) { armLength = value; SetVerticesDirty(); } }
        }

        public float Thickness
        {
            get => thickness;
            set { if (!Mathf.Approximately(thickness, value)) { thickness = value; SetVerticesDirty(); } }
        }

        /// <summary>The four compass directions, in the order the posts are emitted.</summary>
        static readonly Vector2[] Directions =
        {
            new(0f, 1f), new(1f, 0f), new(0f, -1f), new(-1f, 0f),
        };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (armLength <= 0f || thickness <= 0f) return;

            Vector2 centre = GetPixelAdjustedRect().center;

            Color32 solid = color;
            Color32 clear = new Color(color.r, color.g, color.b, 0f);

            float half = thickness * 0.5f;
            float outer = radius + armLength;

            for (int i = 0; i < Directions.Length; i++)
            {
                Vector2 dir = Directions[i];
                // The post's own width axis. A 90 degree turn of the direction, which for these
                // four is exact in floating point.
                Vector2 side = new(-dir.y, dir.x);

                Vector2 innerEnd = centre + dir * radius;
                Vector2 outerEnd = centre + dir * outer;

                // Four columns across the width - clear, solid, solid, clear - at each end, so the
                // strip below carries the feather on both long sides in one pass.
                vh.AddVert(innerEnd - side * (half + feather), clear, Vector2.zero);
                vh.AddVert(innerEnd - side * half, solid, Vector2.zero);
                vh.AddVert(innerEnd + side * half, solid, Vector2.zero);
                vh.AddVert(innerEnd + side * (half + feather), clear, Vector2.zero);

                vh.AddVert(outerEnd - side * (half + feather), clear, Vector2.zero);
                vh.AddVert(outerEnd - side * half, solid, Vector2.zero);
                vh.AddVert(outerEnd + side * half, solid, Vector2.zero);
                vh.AddVert(outerEnd + side * (half + feather), clear, Vector2.zero);

                int a = i * 8;
                int b = a + 4;
                for (int band = 0; band < 3; band++)
                {
                    vh.AddTriangle(a + band, b + band, b + band + 1);
                    vh.AddTriangle(a + band, b + band + 1, a + band + 1);
                }
            }
        }
    }
}
