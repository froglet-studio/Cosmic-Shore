#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Thresher launch sight's window outline (<see cref="TimePetalGeometry"/>), checked against
    /// what was measured off <c>Resources/ElementPetals/time_petal.png</c>: a rhombus with 72° tips
    /// and a width / height of tan 36° (traced 71.49 / 98.35).
    /// </summary>
    public class TimePetalGeometryTests
    {
        [Test]
        public void Outline_IsTheTracedRhombus()
        {
            Assert.AreEqual(71.49f / 98.35f, TimePetalGeometry.Aspect, 0.002f, "the sprite's own proportion");
            var o = TimePetalGeometry.Outline;
            Assert.AreEqual(4, o.Length);
            // Counter-clockwise (positive signed area) in true proportions, as the fan expects.
            float twice = 0f;
            for (int i = 0; i < o.Length; i++)
            {
                Vector2 a = new(o[i].x * TimePetalGeometry.Aspect, o[i].y);
                Vector2 b = new(o[(i + 1) % 4].x * TimePetalGeometry.Aspect, o[(i + 1) % 4].y);
                twice += a.x * b.y - b.x * a.y;
            }
            Assert.Greater(twice, 0f);
            // Both tips are the flower's 72°.
            Vector2 apex = new(0.5f * TimePetalGeometry.Aspect, 0f);
            Vector2 left = new(0f, 0.5f), right = new(TimePetalGeometry.Aspect, 0.5f);
            Assert.AreEqual(72f, Vector2.Angle(left - apex, right - apex), 0.05f);
        }

        [Test]
        public void Inradius_IsTheRhombusOwn_AndTheMiterKeepsTheFeatherEven()
        {
            float a = 0.5f * TimePetalGeometry.Aspect, b = 0.5f;   // half-diagonals, height units
            Assert.AreEqual(a * b / Mathf.Sqrt(a * a + b * b), TimePetalGeometry.Inradius01, 1e-5f);

            // Offsetting a corner along its miter by w moves both adjacent edges out by exactly w.
            for (int i = 0; i < 4; i++)
            {
                Vector2 m = TimePetalGeometry.OutwardMiter(i);
                Vector2 here = Prop(TimePetalGeometry.Outline[i]);
                Vector2 next = Prop(TimePetalGeometry.Outline[(i + 1) % 4]);
                Vector2 e = (next - here).normalized;
                Vector2 n = new(e.y, -e.x);
                Assert.AreEqual(1f, Vector2.Dot(m, n), 1e-4f, $"corner {i}");
            }
        }

        static Vector2 Prop(Vector2 p) => new(p.x * TimePetalGeometry.Aspect, p.y);
    }
}
#endif
