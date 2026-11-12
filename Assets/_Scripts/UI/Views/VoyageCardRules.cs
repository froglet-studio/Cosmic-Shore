using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The open voyage card's rules that hold no UI (Docs/MENU_REDESIGN_B.md section 119), so EditMode tests can hold
    /// them: stepping through the voyages and the story's beats, cycling a seat's setting, and framing the course on the
    /// card's ground (<see cref="VoyageRoutePoster"/>).
    /// </summary>
    public static class VoyageCardRules
    {
        /// <summary>One beat of the story level: its small head, its one sentence, and who or where it is from.</summary>
        public readonly struct Beat
        {
            public readonly string Label, Line, By;
            public Beat(string label, string line, string by) { Label = label; Line = line; By = by; }
        }

        /// <summary>The index <paramref name="step"/> away from <paramref name="index"/> among <paramref name="count"/>,
        /// wrapping at both ends (the voyages and the story's beats go round). -1 when there are none.</summary>
        public static int Wrap(int index, int step, int count)
        {
            if (count <= 0) return -1;
            int i = (index + step) % count;
            return i < 0 ? i + count : i;
        }

        /// <summary>The option after (or before) <paramref name="current"/> in <paramref name="options"/>, wrapping; the
        /// first option when <paramref name="current"/> is not among them. Default when there are no options.</summary>
        public static T Cycle<T>(IReadOnlyList<T> options, T current, int step = 1)
        {
            if (options == null || options.Count == 0) return default;
            int at = -1;
            var eq = EqualityComparer<T>.Default;
            for (int i = 0; i < options.Count; i++)
                if (eq.Equals(options[i], current)) { at = i; break; }
            return at < 0 ? options[0] : options[Wrap(at, step, options.Count)];
        }

        /// <summary>
        /// The story's beats in order (the news, the place, who brought it, the water). A beat whose sentence is empty is
        /// left out, so a voyage without news starts on its place.
        /// </summary>
        public static List<Beat> Beats(params Beat[] beats)
        {
            var out_ = new List<Beat>();
            if (beats == null) return out_;
            foreach (var b in beats)
                if (!string.IsNullOrWhiteSpace(b.Line)) out_.Add(b);
            return out_;
        }

        /// <summary>
        /// The part of the painted water (<paramref name="lo"/>..<paramref name="hi"/>, map points) the card's ground
        /// shows: centred on the course, about half again as large as it, at the card's <paramref name="aspect"/>
        /// (width / height), and never outside the water. Returns the shown corners.
        /// </summary>
        public static (Vector2 lo, Vector2 hi) Frame(Vector2 lo, Vector2 hi, IReadOnlyList<Vector2> course, float aspect,
            float margin = 1.5f)
        {
            aspect = Mathf.Max(1e-3f, aspect);
            var size = hi - lo;
            if (size.x <= 0f || size.y <= 0f) return (lo, hi);
            // The largest window of the card's shape that fits the water.
            float w = size.x, h = size.x / aspect;
            if (h > size.y) { h = size.y; w = h * aspect; }
            Vector2 centre = (lo + hi) * 0.5f;
            if (course != null && course.Count > 0)
            {
                Vector2 cmin = course[0], cmax = course[0];
                foreach (var p in course) { cmin = Vector2.Min(cmin, p); cmax = Vector2.Max(cmax, p); }
                centre = (cmin + cmax) * 0.5f;
                var span = (cmax - cmin) * margin;
                float need = Mathf.Max(span.x, span.y * aspect);
                if (need > 1e-3f && need < w)
                {
                    w = need;
                    h = w / aspect;
                }
            }
            float x0 = Mathf.Clamp(centre.x - w * 0.5f, lo.x, hi.x - w);
            float y0 = Mathf.Clamp(centre.y - h * 0.5f, lo.y, hi.y - h);
            return (new Vector2(x0, y0), new Vector2(x0 + w, y0 + h));
        }
    }
}
