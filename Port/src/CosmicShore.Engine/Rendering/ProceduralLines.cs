using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Port hook: camera-facing ribbons submitted from code for ONE frame, drawn by the render
    /// backend like a LineRenderer (additive or alpha, vertex-coloured). The approximate VFX Graph
    /// effects draw through it (Compat VisualEffect). <see cref="Tick"/> is the per-frame slot the
    /// game loop raises after Update, so components outside the MonoBehaviour lifecycle can submit.
    /// </summary>
    public static class ProceduralLines
    {
        public sealed class Line
        {
            public Vector3[] Points;
            public float Width;
            public Color Color;
            public bool Additive;
            public int Layer;
        }

        static readonly List<Line> s_lines = new();

        /// <summary>Raised once per frame by the game loop (after Update and the animators).</summary>
        public static event Action Tick;

        /// <summary>This frame's lines (read by the render backend).</summary>
        public static IReadOnlyList<Line> Current => s_lines;

        /// <summary>Adds a world-space polyline for this frame.</summary>
        public static void Add(Vector3[] points, float width, Color color, bool additive = true, int layer = 0)
        {
            if (points == null || points.Length < 2 || width <= 0f) return;
            s_lines.Add(new Line { Points = points, Width = width, Color = color, Additive = additive, Layer = layer });
        }

        /// <summary>The game loop's frame slot: last frame's lines go, then the producers submit this frame's.</summary>
        internal static void RunFrame()
        {
            s_lines.Clear();
            var t = Tick;
            if (t == null) return;
            foreach (Action handler in t.GetInvocationList())
            {
                try { handler(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
