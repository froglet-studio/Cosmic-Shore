using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Sea table's tilt (Rev 89 A and Rev 90, Docs/MENU_REDESIGN_B.md sections 170-172; since Rev 98 the Saga, Quests
    /// and Episodes tables only): a flat picture lying back about its near
    /// (bottom) edge and seen in perspective, with a front face standing under that edge.
    ///
    /// <para>The menu's canvas is an overlay: it has no perspective camera, so a rotated rect would only be squashed. The tilt
    /// is therefore a projection of the picture's own points: each vertex of the picture's mesh is moved to where it would
    /// be seen (<see cref="Warp"/>). A projection keeps straight lines straight, so a mesh fine enough to hide the error
    /// inside one triangle (a grid for a textured quad, <see cref="Grid"/>) reads as true perspective.</para>
    ///
    /// <para>Every length is a share of the picture's width, so the table looks the same at any size. The numbers are the
    /// board's: 56 degrees, the eye 0.584 widths above the near edge and 1.47 widths away.</para>
    /// </summary>
    public static class VoyageSlab
    {
        /// <summary>How far back the picture lies, in degrees (0: upright, 90: flat away from the eye).</summary>
        public const float Angle = 56f;
        /// <summary>The eye's height above the picture's near edge, in widths.</summary>
        public const float EyeHeight = 0.584f;
        /// <summary>The eye's distance from the picture's near edge, in widths.</summary>
        public const float EyeDistance = 1.47f;
        /// <summary>The far edge fades into the ground: fully drawn up to this share of the picture's height...</summary>
        public const float FadeFrom = 0.52f;
        /// <summary>...and gone by this share.</summary>
        public const float FadeTo = 0.96f;
        /// <summary>The picture's width over its height (the board's course map, 2196 x 1358).</summary>
        public const float TopAspect = 1.617f;
        /// <summary>The front face's width over its height (the board's edge-on view, 2196 x 438).</summary>
        public const float FaceAspect = 5.014f;

        static readonly float Sin = Mathf.Sin(Angle * Mathf.Deg2Rad);
        static readonly float Cos = Mathf.Cos(Angle * Mathf.Deg2Rad);

        /// <summary>
        /// Where <paramref name="p"/> (a point of the flat picture <paramref name="r"/>) is seen once the picture lies
        /// back. <paramref name="scale"/> is how much smaller things are drawn there (1 at the near edge).
        /// </summary>
        public static Vector2 Warp(Rect r, Vector2 p, out float scale)
        {
            float w = Mathf.Max(1e-3f, r.width);
            float up = p.y - r.yMin;
            float depth = up * Sin, height = up * Cos;
            float d = EyeDistance * w, eye = EyeHeight * w;
            scale = d / Mathf.Max(1e-3f, d + depth);
            return new Vector2(r.center.x + (p.x - r.center.x) * scale, r.yMin + eye + (height - eye) * scale);
        }

        /// <summary>How much of <paramref name="p"/> is drawn (1 near, 0 at the far edge).</summary>
        public static float Fade(Rect r, Vector2 p)
        {
            float v = r.height > 1e-3f ? (p.y - r.yMin) / r.height : 0f;
            return 1f - Mathf.Clamp01((v - FadeFrom) / (FadeTo - FadeFrom));
        }

        /// <summary>The height the lying picture takes on screen, for a picture <paramref name="width"/> wide and
        /// <see cref="TopAspect"/> in shape.</summary>
        public static float SeenHeight(float width)
        {
            var r = new Rect(0f, 0f, width, width / TopAspect);
            return Warp(r, new Vector2(0f, r.yMax), out _).y;
        }

        /// <summary>Lay every vertex of <paramref name="vh"/> back (<see cref="Warp"/>) and fade the far ones.</summary>
        public static void Apply(VertexHelper vh, Rect r)
        {
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                Vector2 flat = v.position;
                float fade = Fade(r, flat);
                v.position = Warp(r, flat, out _);
                var c = v.color;
                c.a = (byte)Mathf.RoundToInt(c.a * fade);
                v.color = c;
                vh.SetUIVertex(v, i);
            }
        }

        /// <summary>
        /// A textured rect as a grid of <paramref name="columns"/> x <paramref name="rows"/> quads, so its texture stays
        /// true when the grid is laid back: one quad would stretch along its diagonal.
        /// </summary>
        public static void Grid(VertexHelper vh, Rect r, Color32 color, Vector2 uvLo, Vector2 uvHi, int columns = 24, int rows = 16)
        {
            int start = vh.currentVertCount;
            var v = UIVertex.simpleVert;
            v.color = color;
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                float tx = x / (float)columns, ty = y / (float)rows;
                v.position = new Vector2(Mathf.Lerp(r.xMin, r.xMax, tx), Mathf.Lerp(r.yMin, r.yMax, ty));
                v.uv0 = new Vector2(Mathf.Lerp(uvLo.x, uvHi.x, tx), Mathf.Lerp(uvLo.y, uvHi.y, ty));
                vh.AddVert(v);
            }
            int stride = columns + 1;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                int i = start + y * stride + x;
                vh.AddTriangle(i, i + stride, i + stride + 1);
                vh.AddTriangle(i, i + stride + 1, i + 1);
            }
        }
    }
}
