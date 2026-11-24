using CosmicShore.Data;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A pilot's mood as the Milestone 2 boards draw it (Frogma Rev 133 <c>mood_mark</c>, kept on Rev 134 and 138): three
    /// rising bars read left to right, three lit for Keen, two for Steady, one for Reluctant; a struck circle for Refuses.
    /// Drawn as a mesh on the board's 20 x 20 box, in <see cref="Lit"/>, <see cref="Unlit"/> and <see cref="Refuse"/>,
    /// which the caller traces through the colour set.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class VoyageMoodMark : MaskableGraphic
    {
        const float Box = 20f, Stroke = 2f;
        const int RingSegments = 24;

        PilotMood _mood;
        Color _lit, _unlit, _refuse;

        public void Set(PilotMood mood, Color lit, Color unlit, Color refuse)
        {
            _mood = mood;
            _lit = lit;
            _unlit = unlit;
            _refuse = refuse;
            SetVerticesDirty();
        }

        /// <summary>How many of the three bars are lit for <paramref name="mood"/> (none for Refuses and None).</summary>
        public static int Bars(PilotMood mood) => mood switch
        {
            PilotMood.Keen => 3,
            PilotMood.Steady => 2,
            PilotMood.Reluctant => 1,
            _ => 0,
        };

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (_mood == PilotMood.None) return;
            var r = GetPixelAdjustedRect();
            if (_mood == PilotMood.Refuses)
            {
                // A circle r 8 at (10, 10) and the stroke M4.5 15.5 L15.5 4.5.
                for (int i = 0; i < RingSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / RingSegments, b = (i + 1) * Mathf.PI * 2f / RingSegments;
                    Segment(vh, Map(r, 10f + 8f * Mathf.Cos(a), 10f + 8f * Mathf.Sin(a)),
                        Map(r, 10f + 8f * Mathf.Cos(b), 10f + 8f * Mathf.Sin(b)), _refuse);
                }
                Segment(vh, Map(r, 4.5f, 15.5f), Map(r, 15.5f, 4.5f), _refuse);
                return;
            }
            // Three bars 4 wide, 6 apart, 4, 8 and 12 tall, standing on y 18.
            int lit = Bars(_mood);
            for (int i = 0; i < 3; i++)
            {
                float x = 2f + i * 6f, top = 14f - i * 4f, h = 4f + i * 4f;
                Quad(vh, Map(r, x, top + h), Map(r, x + 4f, top), i < lit ? _lit : _unlit);
            }
        }

        static Vector2 Map(Rect r, float x, float y) => new(r.xMin + x / Box * r.width, r.yMax - y / Box * r.height);

        static void Quad(VertexHelper vh, Vector2 lo, Vector2 hi, Color c)
        {
            int i = vh.currentVertCount;
            vh.AddVert(new Vector2(lo.x, lo.y), c, Vector2.zero);
            vh.AddVert(new Vector2(lo.x, hi.y), c, Vector2.zero);
            vh.AddVert(new Vector2(hi.x, hi.y), c, Vector2.zero);
            vh.AddVert(new Vector2(hi.x, lo.y), c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        static void Segment(VertexHelper vh, Vector2 a, Vector2 b, Color c)
        {
            var d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            d.Normalize();
            var n = new Vector2(-d.y, d.x) * (Stroke * 0.5f);
            int i = vh.currentVertCount;
            vh.AddVert(a - n, c, Vector2.zero);
            vh.AddVert(a + n, c, Vector2.zero);
            vh.AddVert(b + n, c, Vector2.zero);
            vh.AddVert(b - n, c, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }
    }
}
