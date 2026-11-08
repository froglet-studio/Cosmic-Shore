using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The open voyage column's rules that hold no UI (Docs/MENU_REDESIGN_B.md sections 119 and 151), so EditMode tests
    /// can hold them: stepping through the story's beats, cycling a value among options, and where a card stands on the
    /// coverflow.
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

        /// <summary>Where a card stands on the coverflow (Rev 71 C, picked): its offset across in card steps, its turn
        /// about the vertical axis in degrees, its scale and its opacity.</summary>
        public readonly struct Pose
        {
            public readonly float Step, Turn, Scale, Alpha;
            public Pose(float step, float turn, float scale, float alpha) { Step = step; Turn = turn; Scale = scale; Alpha = alpha; }
        }

        /// <summary>
        /// The pose of the card <paramref name="offset"/> places from the picked one (negative to the left): the picked
        /// card faces front at full size; each step out turns 40 degrees away and shrinks by 0.18, stopping at two steps;
        /// opacity falls 1, 0.7, 0.35 (the boards' numbers, Docs/MENU_REDESIGN_B.md section 148).
        /// </summary>
        public static Pose FlowPose(int offset)
        {
            int a = Mathf.Abs(offset);
            float turn = offset == 0 ? 0f : -Mathf.Sign(offset) * 40f;
            float scale = 1f - Mathf.Min(a, 2) * 0.18f;
            float alpha = a == 0 ? 1f : a == 1 ? 0.7f : 0.35f;
            return new Pose(offset, turn, scale, alpha);
        }
    }
}
