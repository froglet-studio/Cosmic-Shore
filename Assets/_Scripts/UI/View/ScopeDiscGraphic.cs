using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A CIRCULAR picture: a filled disc that samples a texture, used to draw the Serpent scope's
    /// window as a round eyepiece rather than a rectangle
    /// (<c>R_VesselActions/SERPENT_SNIPER_SCOPE.md</c> round 4).
    ///
    /// <para><b>Generated, and deliberately not a <c>RawImage</c> behind a <c>Mask</c>.</b> A mask
    /// is a stencil pass — an extra graphic, an extra draw call and two stencil state changes — to
    /// produce a shape this component emits directly as geometry. It is also the same call the
    /// rest of this HUD family already makes (<see cref="ScopeRingGraphic"/>,
    /// <see cref="TrapezoidGraphic"/>): a sprited circle is crisp only at the size it was exported
    /// at, and this window is a fraction of whatever screen it is drawn on.</para>
    ///
    /// <para><b>The source texture is sampled as a SQUARE.</b> UVs are taken from the unit circle
    /// mapped straight onto [0,1]², so a non-square source would be squashed into the disc rather
    /// than cropped. The scope's RenderTexture is square for exactly that reason and its camera
    /// renders at aspect 1 — a round window wants a square picture, not a wide one with its sides
    /// thrown away.</para>
    ///
    /// <para>Antialiasing is baked into the geometry as a zero-alpha feather ring, for the reason
    /// <see cref="TrapezoidGraphic"/> records: a UGUI canvas gives generated geometry none.</para>
    /// </summary>
    [AddComponentMenu("UI/Scope Disc Graphic", 15)]
    public class ScopeDiscGraphic : MaskableGraphic
    {
        [Tooltip("Radius in canvas units, measured from the rect's centre.")]
        [SerializeField, Min(0f)] private float radius = 120f;

        [Tooltip("Segments around the circle. Enough that the rim reads as a curve at the sizes " +
                 "this window is drawn at.")]
        [SerializeField, Range(12, 256)] private int segments = 96;

        [Tooltip("Width of the zero-alpha feather outside the rim, in canvas units. This IS the " +
                 "antialiasing.")]
        [SerializeField, Min(0f)] private float feather = 1.5f;

        Texture _source;

        /// <summary>
        /// The picture to show. Null draws a flat disc in <see cref="Graphic.color"/>, which is the
        /// honest state while the scope's camera has not produced a frame yet.
        /// </summary>
        public Texture Source
        {
            get => _source;
            set
            {
                if (_source == value) return;
                _source = value;
                SetMaterialDirty();
            }
        }

        public float Radius
        {
            get => radius;
            set { if (!Mathf.Approximately(radius, value)) { radius = value; SetVerticesDirty(); } }
        }

        /// <summary>
        /// The picture. <c>Texture2D.whiteTexture</c> is the fallback rather than
        /// <c>Graphic.s_WhiteTexture</c>: this UGUI version exposes no <c>whiteTexture</c> property
        /// on <see cref="Graphic"/> at all, and the engine static is unconditionally valid — which
        /// matters because a disc drawn as a flat colour (the BACKING) never carries a source, so
        /// the fallback is the ordinary case here rather than an edge one.
        /// </summary>
        public override Texture mainTexture => _source != null ? _source : Texture2D.whiteTexture;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (radius <= 0f) return;

            var rect = GetPixelAdjustedRect();
            Vector2 centre = rect.center;

            Color32 solid = color;
            Color32 clear = new Color(color.r, color.g, color.b, 0f);

            // Centre of the fan, sampling the middle of the source.
            vh.AddVert(centre, solid, new Vector2(0.5f, 0.5f));

            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.PI * 2f * (i / (float)segments);
                Vector2 dir = new(Mathf.Cos(a), Mathf.Sin(a));

                // The rim vertex carries the UV; the feather vertex outside it repeats that UV so
                // the fade is in ALPHA only and the picture does not stretch into the fringe.
                Vector2 uv = new(0.5f + dir.x * 0.5f, 0.5f + dir.y * 0.5f);
                vh.AddVert(centre + dir * radius, solid, uv);
                vh.AddVert(centre + dir * (radius + feather), clear, uv);
            }

            for (int i = 0; i < segments; i++)
            {
                int a = 1 + i * 2;
                int b = 1 + (i + 1) * 2;
                vh.AddTriangle(0, a, b);              // the disc itself
                vh.AddTriangle(a, a + 1, b + 1);      // the feather ring
                vh.AddTriangle(a, b + 1, b);
            }
        }
    }
}
