using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Voyages table's tilt (Frogma Rev 111 Full on Rev 108's table, Docs/MENU_REDESIGN_B.md sections 201-203): the live
    /// HyperSea map lying back about its near (bottom) edge, seen in perspective, at <see cref="Angle"/> degrees (50 on the
    /// table, 0 in full screen, eased between by the board).
    ///
    /// <para>The menu's canvas is an overlay with no perspective camera, so a rotated rect would only be squashed. Like
    /// <see cref="VoyageSlab"/>, the tilt is a projection of the graphic's own points (<see cref="Seen"/>); unlike it, the
    /// map stays a map: the mesh is cut finer first (<see cref="Subdivide"/>) so its painted sea stays true, the map places
    /// its names at the projected points (<see cref="VoyageMapGraphic.SeenAt"/>), and a press is taken back to the flat map
    /// (<see cref="Flat"/>) so drag, wheel and a press on a place still land where they are seen.</para>
    ///
    /// <para>Every length is a share of the rect's width, so the table reads the same at any size. The eye is
    /// <see cref="EyeDistance"/> widths from the near edge and <see cref="EyeHeight"/> widths above it: at 50 degrees the far
    /// edge is drawn at about three quarters of the near edge's width, as the board draws it.</para>
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    public class VoyageTableTilt : BaseMeshEffect
    {
        /// <summary>The table's angle on the Voyages page (Rev 100 P1A, kept by Rev 108 and Rev 111).</summary>
        public const float TableAngle = 50f;
        /// <summary>The eye's distance from the near edge, in widths.</summary>
        public const float EyeDistance = 1.47f;
        /// <summary>The eye's height above the near edge, in widths.</summary>
        public const float EyeHeight = 0.17f;
        /// <summary>A triangle wider than this (in rect units) is cut in four before it is laid back.</summary>
        public const float MaxEdge = 48f;

        float _angle;

        /// <summary>How far back the map lies, in degrees (0: flat, as in full screen).</summary>
        public float Angle
        {
            get => _angle;
            set
            {
                value = Mathf.Clamp(value, 0f, 80f);
                if (Mathf.Approximately(value, _angle)) return;
                _angle = value;
                if (graphic) graphic.SetVerticesDirty();
            }
        }

        /// <summary>True while the map lies back at all.</summary>
        public bool Active => isActiveAndEnabled && _angle > 0.01f;

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!Active || vh.currentVertCount == 0) return;
            var r = ((RectTransform)transform).rect;
            // Clipped to the table first: a lane or a wall that runs past the table's far edge would be laid back toward the
            // horizon and drawn as a long ray across it (Shombith, 2026-11-20, white lines at Neighbourhood), and one past
            // its near edge thrown out past the eye.
            Subdivide(vh, MaxEdge, r);
            Apply(vh, r, _angle);
        }

        // ── The projection ───────────────────────────────────────────────────

        /// <summary>
        /// Where <paramref name="p"/> (a point of the flat rect <paramref name="r"/>) is seen once the rect lies back
        /// <paramref name="angle"/> degrees about its near edge. <paramref name="scale"/> is how much smaller things are
        /// drawn there (1 at the near edge).
        /// </summary>
        public static Vector2 Seen(Rect r, Vector2 p, float angle, out float scale)
        {
            float w = Mathf.Max(1e-3f, r.width);
            float a = angle * Mathf.Deg2Rad;
            float up = p.y - r.yMin;
            float depth = up * Mathf.Sin(a), height = up * Mathf.Cos(a);
            float d = EyeDistance * w, eye = EyeHeight * w;
            scale = d / Mathf.Max(1e-3f, d + depth);
            return new Vector2(r.center.x + (p.x - r.center.x) * scale, r.yMin + eye + (height - eye) * scale);
        }

        /// <summary>The flat point that is seen at <paramref name="seen"/> (the inverse of <see cref="Seen"/>). A point above
        /// the horizon comes back clamped to the far edge's height.</summary>
        public static Vector2 Flat(Rect r, Vector2 seen, float angle)
        {
            float w = Mathf.Max(1e-3f, r.width);
            float a = angle * Mathf.Deg2Rad;
            float d = EyeDistance * w, eye = EyeHeight * w;
            float y = seen.y - r.yMin;
            float below = Mathf.Cos(a) * d - (y - eye) * Mathf.Sin(a);
            float up = below > 1e-3f ? y * d / below : r.height;
            up = Mathf.Clamp(up, 0f, Mathf.Max(r.height, 0f));
            float scale = d / Mathf.Max(1e-3f, d + up * Mathf.Sin(a));
            return new Vector2(r.center.x + (seen.x - r.center.x) / scale, r.yMin + up);
        }

        /// <summary>
        /// How tall the flat rect must be, <paramref name="width"/> wide, for its far edge to be seen
        /// <paramref name="seenHeight"/> above its near edge at <paramref name="angle"/> degrees. The board sizes the map
        /// with it so the lying table fills the room it has.
        /// </summary>
        public static float FlatHeight(float width, float seenHeight, float angle)
        {
            var r = new Rect(0f, 0f, Mathf.Max(1f, width), 1e6f);
            return Flat(r, new Vector2(r.center.x, seenHeight), angle).y;
        }

        /// <summary>Lay every vertex of <paramref name="vh"/> back <paramref name="angle"/> degrees.</summary>
        public static void Apply(VertexHelper vh, Rect r, float angle)
        {
            var v = new UIVertex();
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                v.position = Seen(r, v.position, angle, out _);
                vh.SetUIVertex(v, i);
            }
        }

        static readonly List<UIVertex> s_Stream = new();
        static readonly List<UIVertex> s_Out = new();

        /// <summary>
        /// Cut every triangle of <paramref name="vh"/> with an edge longer than <paramref name="maxEdge"/> into four, until
        /// none is (at most five times), so a projection that keeps straight lines straight but bends textures across a
        /// large triangle stays true. A triangle with one uv at every corner (a solid shape) is kept whole. Colours and the
        /// first uv are carried along.
        /// </summary>
        public static void Subdivide(VertexHelper vh, float maxEdge) => Subdivide(vh, maxEdge, null);

        /// <summary><see cref="Subdivide(VertexHelper, float)"/>, keeping only what lies inside <paramref name="clip"/>
        /// (every triangle cut to it first; colours and uvs carried along).</summary>
        public static void Subdivide(VertexHelper vh, float maxEdge, Rect? clip)
        {
            s_Stream.Clear();
            vh.GetUIVertexStream(s_Stream);
            s_Out.Clear();
            float max2 = maxEdge * maxEdge;
            for (int i = 0; i + 2 < s_Stream.Count; i += 3)
            {
                UIVertex a = s_Stream[i], b = s_Stream[i + 1], c = s_Stream[i + 2];
                if (clip is not { } r || Inside(r, a) && Inside(r, b) && Inside(r, c))
                {
                    Cut(a, b, c, max2, 5);
                    continue;
                }
                s_Poly.Clear();
                s_Poly.Add(a);
                s_Poly.Add(b);
                s_Poly.Add(c);
                ClipTo(r);
                for (int k = 1; k + 1 < s_Poly.Count; k++) Cut(s_Poly[0], s_Poly[k], s_Poly[k + 1], max2, 5);
            }
            // One UI mesh holds fewer than 65,000 vertices; past that Unity draws none of it. The map budgets its sky to
            // stay well under (VoyageMapSky), and this keeps the rest from ever crossing it: the last triangles go.
            if (s_Out.Count > MaxListVertices) s_Out.RemoveRange(MaxListVertices, s_Out.Count - MaxListVertices);
            vh.Clear();
            vh.AddUIVertexTriangleStream(s_Out);
        }

        /// <summary>The most vertices the laid-back mesh carries (a whole number of triangles under Unity's 65,000).</summary>
        public const int MaxListVertices = 64995;

        static readonly List<UIVertex> s_Poly = new(), s_Clip = new();

        static bool Inside(Rect r, UIVertex v) =>
            v.position.x >= r.xMin && v.position.x <= r.xMax && v.position.y >= r.yMin && v.position.y <= r.yMax;

        /// <summary>Cut the polygon in <see cref="s_Poly"/> to <paramref name="r"/>, one side at a time (Sutherland-Hodgman).</summary>
        static void ClipTo(Rect r)
        {
            for (int side = 0; side < 4 && s_Poly.Count >= 3; side++)
            {
                s_Clip.Clear();
                int n = s_Poly.Count;
                for (int k = 0; k < n; k++)
                {
                    var p = s_Poly[k];
                    var q = s_Poly[(k + 1) % n];
                    float fp = Outside(r, side, p), fq = Outside(r, side, q);
                    if (fp <= 0f) s_Clip.Add(p);
                    if ((fp < 0f && fq > 0f) || (fp > 0f && fq < 0f)) s_Clip.Add(Lerp(p, q, fp / (fp - fq)));
                }
                s_Poly.Clear();
                s_Poly.AddRange(s_Clip);
            }
        }

        // How far past side 0-3 (left, right, bottom, top) of r the vertex lies; negative inside.
        static float Outside(Rect r, int side, UIVertex v) => side switch
        {
            0 => r.xMin - v.position.x,
            1 => v.position.x - r.xMax,
            2 => r.yMin - v.position.y,
            _ => v.position.y - r.yMax,
        };

        static void Cut(UIVertex a, UIVertex b, UIVertex c, float max2, int depth)
        {
            // A solid shape (a star, a line, a marker: every corner on the one white texel) has no texture to bend, and a
            // perspective keeps its straight edges straight: it is laid back whole. Only the painted sea is cut finer.
            if (a.uv0 == b.uv0 && b.uv0 == c.uv0)
            {
                s_Out.Add(a);
                s_Out.Add(b);
                s_Out.Add(c);
                return;
            }
            float ab = ((Vector2)(a.position - b.position)).sqrMagnitude;
            float bc = ((Vector2)(b.position - c.position)).sqrMagnitude;
            float ca = ((Vector2)(c.position - a.position)).sqrMagnitude;
            if (depth <= 0 || (ab <= max2 && bc <= max2 && ca <= max2))
            {
                s_Out.Add(a);
                s_Out.Add(b);
                s_Out.Add(c);
                return;
            }
            var mab = Mid(a, b);
            var mbc = Mid(b, c);
            var mca = Mid(c, a);
            Cut(a, mab, mca, max2, depth - 1);
            Cut(mab, b, mbc, max2, depth - 1);
            Cut(mca, mbc, c, max2, depth - 1);
            Cut(mab, mbc, mca, max2, depth - 1);
        }

        static UIVertex Mid(UIVertex a, UIVertex b) => Lerp(a, b, 0.5f);

        static UIVertex Lerp(UIVertex a, UIVertex b, float t)
        {
            var m = a;
            m.position = Vector3.Lerp(a.position, b.position, t);
            m.uv0 = Vector4.Lerp(a.uv0, b.uv0, t);
            m.uv1 = Vector4.Lerp(a.uv1, b.uv1, t);
            m.color = (Color32)Color.Lerp(a.color, b.color, t);
            m.normal = a.normal;
            m.tangent = a.tangent;
            return m;
        }
    }
}
