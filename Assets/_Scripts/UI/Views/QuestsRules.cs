using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The arithmetic of the Quests window (<see cref="QuestsModal"/>, Frogma Rev 107 D, Docs/MENU_REDESIGN_B.md
    /// sections 188 and 200): where each quest stands on the route, which of a quest's goals are done, and the colour a
    /// reward's prism is drawn in. Pure, so it is tested without a scene (QuestsRulesTests).
    /// </summary>
    public static class QuestsRules
    {
        /// <summary>How far the route's stations step up and down from its middle line, in canvas units (the board's
        /// 410 / 470).</summary>
        public const float RouteSwing = 30f;

        /// <summary>
        /// Where station <paramref name="index"/> of <paramref name="count"/> stands on a route
        /// <paramref name="width"/> wide: spread evenly with half a step at each end, every other one
        /// <see cref="RouteSwing"/> above the middle line (positive y is up), the first below it.
        /// </summary>
        public static Vector2 Station(int index, int count, float width)
        {
            if (count <= 0) return Vector2.zero;
            float x = (index + 0.5f) / count * width - width * 0.5f;
            float y = index % 2 == 0 ? -RouteSwing : RouteSwing;
            return new Vector2(x, y);
        }

        /// <summary>The bend of the course from station <paramref name="index"/> to the next (the board's 30, -30, ...):
        /// the control point of a quadratic, <paramref name="bend"/> off the chord's middle to its left.</summary>
        public static Vector2 Control(Vector2 a, Vector2 b, float bend)
        {
            Vector2 mid = (a + b) * 0.5f, d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return mid;
            // The board's y runs down; the canvas's runs up, so its left normal is (d.y, -d.x) here.
            return mid + new Vector2(d.y, -d.x) / len * bend;
        }

        public static float BendOf(int segment) => segment % 2 == 0 ? 30f : -30f;

        public static Vector2 Quadratic(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * c + t * t * b;
        }

        /// <summary>
        /// How many of a game quest's goals are done. The goals are its two intensity steps: the first is done once
        /// Intensity 3 is open, the second once Intensity 4 is; a quest ready to claim or claimed has both. A feature
        /// quest (the Vessel Hangar) has one goal, done when it is ready to claim or claimed.
        /// </summary>
        public static int GoalsDone(QuestItemState state, int goalCount, int maxUnlockedIntensity)
        {
            if (state == QuestItemState.ReadyToClaim || state == QuestItemState.Claimed) return goalCount;
            if (state == QuestItemState.Locked || goalCount <= 1) return 0;
            int done = maxUnlockedIntensity >= 4 ? 2 : maxUnlockedIntensity >= 3 ? 1 : 0;
            return Mathf.Min(done, goalCount);
        }

        /// <summary>A quest's goals, in order: a game quest's two intensity goals (each shown only when authored), a
        /// feature quest's own description.</summary>
        public static List<string> Goals(SO_UnlockData quest)
        {
            var goals = new List<string>(2);
            if (!quest) return goals;
            if (quest.FeatureKind == FeatureKind.GameMode)
            {
                if (!string.IsNullOrWhiteSpace(quest.Intensity3GoalDescription)) goals.Add(quest.Intensity3GoalDescription);
                if (!string.IsNullOrWhiteSpace(quest.Intensity4GoalDescription)) goals.Add(quest.Intensity4GoalDescription);
            }
            if (goals.Count == 0 && !string.IsNullOrWhiteSpace(quest.Description)) goals.Add(quest.Description);
            return goals;
        }

        /// <summary>
        /// A set's colour as a UI swatch: its HDR value brought into range by its brightest channel (the hue and its
        /// balance kept, as the Profile's colour set cards read them), fully opaque.
        /// </summary>
        public static Color Swatch(Color hdr)
        {
            float max = Mathf.Max(hdr.r, Mathf.Max(hdr.g, hdr.b));
            var c = max > 1f ? new Color(hdr.r / max, hdr.g / max, hdr.b / max) : new Color(hdr.r, hdr.g, hdr.b);
            c.a = 1f;
            return c;
        }

        /// <summary>The prism's edge in a domain: the colour the game's trails draw in (the Profile's rim).</summary>
        public static Color Rim(DomainColorSet d) => d != null ? Swatch(d.TrailHighlightColor) : Color.clear;

        /// <summary>The prism's body in a domain: the outer block colour (the Profile's face).</summary>
        public static Color Face(DomainColorSet d) => d != null ? Swatch(d.OutsideBlockColor) : Color.clear;
    }
}
