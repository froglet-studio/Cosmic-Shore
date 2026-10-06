using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A full-screen dim with rectangular cut-outs. Drawn as the cells of the grid the cut-outs'
    /// edges make, skipping cells inside a cut-out, so any number of cut-outs costs a handful of
    /// quads. Blocks every press except inside a cut-out.
    /// </summary>
    public class SpotlightDimGraphic : MaskableGraphic, ICanvasRaycastFilter
    {
        /// <summary>
        /// Cut-outs in THIS graphic's local space (owned by <see cref="MenuSpotlight"/>). Local, not
        /// screen pixels: the mesh is built in local space, and reading <c>Screen.width</c> inside a
        /// mesh rebuild is not safe in the Editor, where a rebuild can run while another window is
        /// the one being painted - which is how the first build drew no dim at all in Unity.
        /// </summary>
        public List<Rect> Holes;

        static readonly List<float> Xs = new();
        static readonly List<float> Ys = new();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var bounds = rectTransform.rect;
            float x0b = bounds.xMin, x1b = bounds.xMax, y0b = bounds.yMin, y1b = bounds.yMax;

            Xs.Clear(); Ys.Clear();
            Xs.Add(x0b); Xs.Add(x1b); Ys.Add(y0b); Ys.Add(y1b);
            if (Holes != null)
                foreach (var r in Holes)
                {
                    Xs.Add(Mathf.Clamp(r.xMin, x0b, x1b)); Xs.Add(Mathf.Clamp(r.xMax, x0b, x1b));
                    Ys.Add(Mathf.Clamp(r.yMin, y0b, y1b)); Ys.Add(Mathf.Clamp(r.yMax, y0b, y1b));
                }
            Xs.Sort(); Ys.Sort();

            var c = color;
            for (int i = 0; i + 1 < Xs.Count; i++)
            for (int j = 0; j + 1 < Ys.Count; j++)
            {
                float x0 = Xs[i], x1 = Xs[i + 1], y0 = Ys[j], y1 = Ys[j + 1];
                if (x1 - x0 < 0.01f || y1 - y0 < 0.01f) continue;
                if (InHole(new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f))) continue;

                int v = vh.currentVertCount;
                vh.AddVert(new Vector3(x0, y0), c, Vector2.zero);
                vh.AddVert(new Vector3(x0, y1), c, Vector2.zero);
                vh.AddVert(new Vector3(x1, y1), c, Vector2.zero);
                vh.AddVert(new Vector3(x1, y0), c, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v, v + 2, v + 3);
            }
        }

        bool InHole(Vector2 p)
        {
            if (Holes == null) return false;
            foreach (var r in Holes)
                if (r.Contains(p)) return true;
            return false;
        }

        /// <summary>A press inside a cut-out is not ours - it falls through to the control beneath.</summary>
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) =>
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local)
            || !InHole(local);
    }
}
