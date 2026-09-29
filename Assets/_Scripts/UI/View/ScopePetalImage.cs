using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A <see cref="RawImage"/> shaped like the CHARGE petal — the Serpent scope's eyepiece.
    ///
    /// <para><b>It is a SUBCLASS rather than a new graphic, and that is the whole risk story.</b>
    /// The scope's window went missing for three playtest rounds because one change replaced its
    /// surface (a <c>RawImage</c>) with a generated <c>MaskableGraphic</c> and re-pointed its camera
    /// in the same pass, and the replacement never rendered — a fault that was never diagnosed,
    /// only backed out of (<c>R_VesselActions/SERPENT_SNIPER_SCOPE.md</c> round 7). Re-shaping the
    /// window is a second chance to make exactly that mistake, so nothing about the surface is
    /// rebuilt: this inherits <see cref="RawImage"/>'s texture property, its <c>mainTexture</c>
    /// (including the white fallback that makes a textureless instance an opaque shape), its
    /// material handling and its whole rebuild path, and overrides <b>only</b>
    /// <see cref="OnPopulateMesh"/>. <b>When a component renders and its shape is wrong, subclass
    /// it and override the geometry — do not write a new one.</b> A broken emit then reads as a
    /// wrong SHAPE, which the pilot can report, rather than as an absence, which they cannot.</para>
    ///
    /// <para><b>Positions and UVs are the same numbers</b> (<see cref="ScopePetalGeometry"/> is
    /// normalised over the petal's own bounding box), so the picture is sampled square-on with
    /// nothing squashed and nothing cropped — which is why the scope's render target is square. The
    /// parts of the picture outside the petal are simply not drawn: the shape IS the crop.</para>
    ///
    /// <para>Antialiasing is a zero-alpha feather ring outside the outline, for the reason
    /// <see cref="TrapezoidGraphic"/> records — a UGUI canvas gives generated geometry none — and it
    /// is offset along each corner's MITER rather than its bisector, because the apex is a 72°
    /// corner where the two differ by 41%.</para>
    /// </summary>
    [AddComponentMenu("UI/Scope Petal Image", 16)]
    public class ScopePetalImage : RawImage
    {
        [Tooltip("Width of the zero-alpha feather outside the outline, in canvas units. This IS " +
                 "the antialiasing.")]
        [SerializeField, Min(0f)] private float feather = 1.5f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var rect = GetPixelAdjustedRect();
            if (rect.width <= 0f || rect.height <= 0f) return;

            var outline = ScopePetalGeometry.Outline;
            int n = outline.Length;

            Color32 solid = color;
            Color32 clear = new Color(color.r, color.g, color.b, 0f);

            // The fan's hub is the bounding box centre, which is the picture's optical centre and
            // is inside the (convex) petal - so the fan is a valid tessellation and the hub samples
            // the middle of the source.
            vh.AddVert(ToRect(rect, ScopePetalGeometry.Centre), solid, ScopePetalGeometry.Centre);

            for (int i = 0; i < n; i++)
            {
                Vector2 p = outline[i];
                // The feather vertex repeats its corner's UV, so the fade is in ALPHA only and the
                // picture does not stretch into the fringe.
                Vector2 uv = p;
                Vector2 outward = ScopePetalGeometry.OutwardMiter(i);

                vh.AddVert(ToRect(rect, p), solid, uv);
                vh.AddVert(ToRect(rect, p) + outward * feather, clear, uv);
            }

            for (int i = 0; i < n; i++)
            {
                int a = 1 + i * 2;                      // corner i
                int b = 1 + ((i + 1) % n) * 2;          // corner i+1, wrapping
                vh.AddTriangle(0, a, b);                // the body
                vh.AddTriangle(a, a + 1, b + 1);        // the feather band
                vh.AddTriangle(a, b + 1, b);
            }
        }

        /// <summary>Normalised petal space → this graphic's rect.</summary>
        static Vector2 ToRect(Rect rect, Vector2 normalised) =>
            new(rect.xMin + normalised.x * rect.width,
                rect.yMin + normalised.y * rect.height);
    }
}
