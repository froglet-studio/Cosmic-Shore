using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Where a phone's ability buttons go: round the right thumb, sized in MILLIMETRES so a 5" phone and a
    /// 13" tablet both get a button a thumb can hit, and kept out of the view (Garrett, 2026-10-21: "resize
    /// and position ergonomically the icons so the icon of choice is easily selected ... without impeding
    /// vision ... balanced on different mobile screen sizes").
    ///
    /// <para><b>The model.</b> A thumb pivots from the bottom-right corner of the safe area, so its easy
    /// reach is ARCS round that corner. Buttons arrive in priority order and each takes the cheapest free
    /// place for its own footprint (a totem is a tall card, an extra button a disc): the inner arc before
    /// outer ones, and on an arc the middle of the sweep (45 degrees up and left of the corner) before its
    /// ends. A place must not overlap a button already placed, must sit inside the safe area, and must stay
    /// inside a corner box of the screen (a height and a width fraction) so the middle of the view is never
    /// covered.</para>
    ///
    /// <para>If the buttons do not fit, the size steps down toward the 7 mm platform minimum, and only then
    /// does the corner box grow. Measured on the shipped defaults (TouchControlsTests): the Brittle Star's six
    /// (two discs, four totems) seat at 8.5 mm on a 6.0" 1080p phone, at the 7 mm floor on a 4.4" 720p one,
    /// and at the full 10 mm on a tablet, inside a corner about 50 x 35 mm. Pure: screen pixels in, screen
    /// pixels out.</para>
    /// </summary>
    public static class TouchButtonLayout
    {
        public struct Settings
        {
            public float ButtonMillimetres;
            public float GapMillimetres;
            public float InnerArcMillimetres;
            public float ArcSpacingMillimetres;
            public float MaxHeightFraction;
            public float MaxWidthFraction;
        }

        public struct Result
        {
            /// <summary>Button centres in screen pixels, in the order the buttons were given.</summary>
            public List<Vector2> Centres;
            /// <summary>Width of every button on the glass, in pixels (a button's height is this times its aspect).</summary>
            public float ButtonWidthPixels;
            /// <summary>The size the buttons were seated at, in millimetres.</summary>
            public float ButtonMillimetres;
            /// <summary>False when even the smallest size in the largest box could not seat them all; the
            /// centres then hold as many as fitted, in order.</summary>
            public bool Fits;
        }

        public const float MinimumButtonMillimetres = 7f;
        const float FallbackDpi = 160f;
        const float SweetAngleDegrees = 135f;
        const int Rings = 10;
        const float AngleStepDegrees = 1f;

        /// <summary>
        /// Lays out one button per entry of <paramref name="aspects"/> (height over width: about 1.75 for a
        /// totem, 1 for a disc), highest priority first.
        /// </summary>
        public static Result Arrange(IReadOnlyList<float> aspects, Vector2 screenPixels, Rect safeArea, float dpi,
                                     Settings settings)
        {
            int count = aspects?.Count ?? 0;
            var result = new Result { Centres = new List<Vector2>(count), Fits = true };
            if (count == 0) return result;

            float pxPerMm = (dpi > 1f ? dpi : FallbackDpi) / 25.4f;
            float heightFraction = Mathf.Clamp01(settings.MaxHeightFraction);
            float widthFraction = Mathf.Clamp01(settings.MaxWidthFraction);
            float size = Mathf.Max(MinimumButtonMillimetres, settings.ButtonMillimetres);

            for (int attempt = 0; attempt < 32; attempt++)
            {
                bool fits = TryPlace(aspects, screenPixels, safeArea, pxPerMm, size, heightFraction, widthFraction,
                                     settings, result.Centres);
                result.ButtonMillimetres = size;
                result.ButtonWidthPixels = size * pxPerMm;
                result.Fits = fits;
                if (fits) return result;

                if (size > MinimumButtonMillimetres + 0.01f)
                    size = Mathf.Max(MinimumButtonMillimetres, size * 0.92f);
                else if (heightFraction < 0.9f || widthFraction < 0.6f)
                {
                    heightFraction = Mathf.Min(0.9f, heightFraction + 0.1f);
                    widthFraction = Mathf.Min(0.6f, widthFraction + 0.06f);
                }
                else break;
            }
            return result;
        }

        static bool TryPlace(IReadOnlyList<float> aspects, Vector2 screen, Rect safe, float pxPerMm, float sizeMm,
                             float heightFraction, float widthFraction, Settings settings, List<Vector2> centres)
        {
            centres.Clear();
            float w = sizeMm * pxPerMm;
            float gap = Mathf.Max(0f, settings.GapMillimetres) * pxPerMm;
            float ringStep = Mathf.Max(1f, settings.ArcSpacingMillimetres * pxPerMm);
            var pivot = new Vector2(safe.xMax, safe.yMin);
            float boxLeft = Mathf.Max(safe.xMin, screen.x * (1f - widthFraction));
            float boxTop = Mathf.Min(safe.yMax, screen.y * heightFraction);

            for (int b = 0; b < aspects.Count; b++)
            {
                float h = w * Mathf.Max(0.25f, aspects[b]);
                float minX = boxLeft + w * 0.5f, maxX = safe.xMax - w * 0.5f;
                float minY = safe.yMin + h * 0.5f, maxY = boxTop - h * 0.5f;
                if (maxX < minX || maxY < minY) return false;

                bool found = false;
                float bestCost = float.MaxValue;
                Vector2 best = default;
                float radius = Mathf.Max(settings.InnerArcMillimetres * pxPerMm, Mathf.Sqrt(w * w + h * h) * 0.5f);

                for (int ring = 0; ring < Rings; ring++, radius += ringStep)
                {
                    for (float degrees = 90f; degrees <= 180f; degrees += AngleStepDegrees)
                    {
                        float cost = ring * 0.8f + Mathf.Abs(degrees - SweetAngleDegrees) / 45f * 0.6f;
                        if (cost >= bestCost) continue;
                        float rad = degrees * Mathf.Deg2Rad;
                        var c = pivot + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * radius;
                        if (c.x < minX || c.x > maxX || c.y < minY || c.y > maxY) continue;
                        if (Overlaps(centres, aspects, w, b, c, h, gap)) continue;
                        bestCost = cost;
                        best = c;
                        found = true;
                    }
                }

                if (!found) return false;
                centres.Add(best);
            }
            return true;
        }

        static bool Overlaps(List<Vector2> placed, IReadOnlyList<float> aspects, float w, int count, Vector2 c, float h,
                             float gap)
        {
            for (int i = 0; i < placed.Count && i < count; i++)
            {
                float ph = w * Mathf.Max(0.25f, aspects[i]);
                var d = placed[i] - c;
                if (Mathf.Abs(d.x) < w + gap && Mathf.Abs(d.y) < (h + ph) * 0.5f + gap) return true;
            }
            return false;
        }
    }
}
