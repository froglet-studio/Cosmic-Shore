using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A ring — or an arc of one — GENERATED rather than sprited, in the same spirit as
    /// <see cref="TrapezoidGraphic"/> and <see cref="BlastProfileGraphic"/>.
    ///
    /// <para>It is generated because its RADIUS is a live measurement, not a layout value: the
    /// Serpent's scope draws its reticle at the sniper cone's TRUE angular size for whatever field
    /// of view the camera is running this frame, and that changes continuously with the zoom and
    /// with the speed tunnel. A sprite would have to be scaled to match, which turns a crisp
    /// hairline into a soft one at exactly the magnifications the scope exists for.</para>
    ///
    /// <para><b>The sweep starts at TOP and runs CLOCKWISE</b>, which is the same direction the
    /// fleet's ability cooldown veil sweeps (<c>VesselHUDView.SetAbilityCooldown</c>) — an arc that
    /// filled the other way would read as the opposite of every other recharge in the game. Here
    /// the arc FILLS as the weapon recharges, so it is a progress bar rather than a veil.</para>
    ///
    /// <para>Antialiasing is baked into the geometry as a zero-alpha feather on each side of the
    /// band, for the reason <see cref="TrapezoidGraphic"/> records: a UGUI canvas gives a generated
    /// curve none, and a 2 px ring without help is stair-steps.</para>
    ///
    /// <para>Raycasting is off by default and should stay off — this is a readout drawn over the
    /// middle of the screen, and a hit target there would eat presses meant for the world.</para>
    /// </summary>
    [AddComponentMenu("UI/Scope Ring Graphic", 14)]
    public class ScopeRingGraphic : MaskableGraphic
    {
        [Tooltip("Outer radius in canvas units, measured from the rect's centre.")]
        [SerializeField, Min(0f)] private float radius = 40f;

        [Tooltip("Band thickness in canvas units. A thickness at or above the radius draws a " +
                 "solid disc, which is how the centre dot is made from this same component.")]
        [SerializeField, Min(0.5f)] private float thickness = 2f;

        [Tooltip("How much of the ring is drawn, clockwise from the top. 1 is a closed ring.")]
        [SerializeField, Range(0f, 1f)] private float sweep01 = 1f;

        [Tooltip("Segments in a full turn. Enough that the curve reads as one at scope " +
                 "magnifications; a partial sweep uses proportionally fewer.")]
        [SerializeField, Range(12, 256)] private int segments = 96;

        [Tooltip("Width of the zero-alpha feather on each side of the band, in canvas units. " +
                 "This IS the antialiasing - a canvas provides none for generated geometry.")]
        [SerializeField, Min(0f)] private float feather = 1f;

        public float Radius
        {
            get => radius;
            set { if (!Mathf.Approximately(radius, value)) { radius = value; SetVerticesDirty(); } }
        }

        public float Thickness
        {
            get => thickness;
            set { if (!Mathf.Approximately(thickness, value)) { thickness = value; SetVerticesDirty(); } }
        }

        public float Sweep01
        {
            get => sweep01;
            set
            {
                value = Mathf.Clamp01(value);
                if (!Mathf.Approximately(sweep01, value)) { sweep01 = value; SetVerticesDirty(); }
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (radius <= 0f || sweep01 <= 0f) return;

            var rect = GetPixelAdjustedRect();
            Vector2 centre = rect.center;

            float outer = radius;
            float inner = Mathf.Max(0f, radius - thickness);
            bool disc = inner <= 0.001f;

            int steps = Mathf.Max(2, Mathf.CeilToInt(segments * sweep01));
            float sweepRad = sweep01 * Mathf.PI * 2f;

            Color32 solid = color;
            Color32 clear = new Color(color.r, color.g, color.b, 0f);

            // A disc has no inner feather to draw and no inner edge to antialias; emitting one
            // would put a transparent seam through the middle of the centre dot.
            float innerFeather = disc ? 0f : feather;

            for (int i = 0; i <= steps; i++)
            {
                // Start at the TOP (+90 degrees) and go CLOCKWISE, i.e. subtract the angle.
                float a = Mathf.PI * 0.5f - sweepRad * (i / (float)steps);
                Vector2 dir = new(Mathf.Cos(a), Mathf.Sin(a));

                vh.AddVert(centre + dir * Mathf.Max(0f, inner - innerFeather), clear, Vector2.zero);
                vh.AddVert(centre + dir * inner, solid, Vector2.zero);
                vh.AddVert(centre + dir * outer, solid, Vector2.zero);
                vh.AddVert(centre + dir * (outer + feather), clear, Vector2.zero);
            }

            for (int i = 0; i < steps; i++)
            {
                int a = i * 4;
                int b = (i + 1) * 4;
                for (int band = 0; band < 3; band++)
                {
                    // The inner feather band is degenerate on a disc (both radii are 0), so it
                    // emits zero-area triangles rather than a wrong shape - cheaper than branching
                    // the strip.
                    vh.AddTriangle(a + band, b + band, b + band + 1);
                    vh.AddTriangle(a + band, b + band + 1, a + band + 1);
                }
            }
        }
    }
}
