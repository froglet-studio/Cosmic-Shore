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
        /// <summary>Cut-outs in screen pixels (owned by <see cref="MenuSpotlight"/>).</summary>
        public List<Rect> Holes;

        static readonly List<float> Xs = new();
        static readonly List<float> Ys = new();

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            float w = Screen.width, h = Screen.height;

            Xs.Clear(); Ys.Clear();
            Xs.Add(0f); Xs.Add(w); Ys.Add(0f); Ys.Add(h);
            if (Holes != null)
                foreach (var r in Holes)
                {
                    Xs.Add(Mathf.Clamp(r.xMin, 0f, w)); Xs.Add(Mathf.Clamp(r.xMax, 0f, w));
                    Ys.Add(Mathf.Clamp(r.yMin, 0f, h)); Ys.Add(Mathf.Clamp(r.yMax, 0f, h));
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
        public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera) => !InHole(screenPoint);
    }
}
