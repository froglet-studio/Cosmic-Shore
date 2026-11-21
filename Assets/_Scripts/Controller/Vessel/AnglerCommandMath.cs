using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Angler's command arithmetic, ported LINE FOR LINE from the <c>CORE</c> block of the Angler Command Lab
    /// (<c>Docs/Studios/AnglerCommandLab.html</c>, round 2: the Sling painter). The lab's model calls those functions and
    /// nothing else for these decisions, and <c>Tools/Build/angler_command_parity</c> runs the page's functions and these
    /// on one table of inputs, so the game and the lab cannot drift apart without a gate going red. Keep the page's
    /// names; change a number or a formula in the page first, then here.
    ///
    /// <para>Pure: no state, no scene, no time. <see cref="AnglerCommandExecutor"/> owns everything that changes.</para>
    /// </summary>
    public static class AnglerCommandMath
    {
        /// <summary>Each claim already on a prism costs this much squared distance (30 u), so a squad spreads over a
        /// painted set instead of piling onto its nearest prism.</summary>
        public const float ClaimPenalty = 900f;

        /// <summary>The golden angle (radians) that spaces the school round the hull.</summary>
        public const float GoldenAngle = 2.399963f;

        /// <summary>Trigger depth (0.5 = the press threshold, 1 = a full pull) -> share of the full brush. A light
        /// squeeze paints <paramref name="brushMin"/> of it; a full pull, all of it.</summary>
        public static float BrushScale(float depth, float brushMin) =>
            brushMin + (1f - brushMin) * Mathf.Clamp((depth - 0.5f) / 0.5f, 0f, 1f);

        /// <summary>Is <paramref name="p"/> inside the brush from <paramref name="o"/> along the unit <paramref name="f"/>:
        /// ahead of the nose, within <paramref name="reach"/> along the axis, and no further from the axis than the cone's
        /// radius there (half-angle <paramref name="coneDeg"/>) or <paramref name="nearR"/>, whichever is wider - so
        /// close-up prisms count.</summary>
        public static bool InBrush(Vector3 o, Vector3 f, Vector3 p, float coneDeg, float reach, float nearR)
        {
            float dx = p.x - o.x, dy = p.y - o.y, dz = p.z - o.z, along = dx * f.x + dy * f.y + dz * f.z;
            if (along < 0f || along > reach) return false;
            float perp2 = Mathf.Max(0f, dx * dx + dy * dy + dz * dz - along * along);
            float r = Mathf.Max(nearR, along * Mathf.Tan(coneDeg * Mathf.Deg2Rad));
            return perp2 <= r * r;
        }

        /// <summary>The index of the candidate nearest <paramref name="from"/>, each claim on a candidate costing
        /// <see cref="ClaimPenalty"/>; -1 for an empty list.</summary>
        public static int PickTarget(Vector3 from, IReadOnlyList<Vector3> candidates, IReadOnlyList<int> claims)
        {
            int best = -1;
            float bd = float.PositiveInfinity;
            for (int i = 0; i < candidates.Count; i++)
            {
                Vector3 c = candidates[i];
                float dx = c.x - from.x, dy = c.y - from.y, dz = c.z - from.z;
                float d = dx * dx + dy * dy + dz * dz + claims[i] * ClaimPenalty;
                if (d < bd) { bd = d; best = i; }
            }
            return best;
        }

        /// <summary>Creature <paramref name="i"/>'s place in the school: a golden-angle spiral round the hull, its ring
        /// widening with the square root of the index, <paramref name="formBack"/> (plus 0.6 u a creature) behind it.</summary>
        public static Vector3 FormationSlot(int i, Vector3 p, Vector3 f, Vector3 r, Vector3 u, float formBack)
        {
            float ring = 6f + Mathf.Sqrt(i) * 3.2f, th = i * GoldenAngle, bk = formBack + i * 0.6f;
            float c = Mathf.Cos(th) * ring, s = Mathf.Sin(th) * ring;
            return new Vector3(p.x - f.x * bk + r.x * c + u.x * s,
                               p.y - f.y * bk + r.y * c + u.y * s,
                               p.z - f.z * bk + r.z * c + u.z * s);
        }

        /// <summary>A follower's speed: the hull's, fed forward, plus 1.5/s of its gap beyond 4 u, between half its
        /// minimum and its cap. (Speed proportional to the gap alone settled ~27 u behind its slot.)</summary>
        public static float FollowSpeed(float hullSpeed, float gap, float minSpeed, float cap) =>
            Mathf.Clamp(hullSpeed + (gap - 4f) * 1.5f, minSpeed * 0.5f, cap);

        /// <summary>A steady swimmer's follow cap: its top speed at the commanded aggression, towed by the leash.</summary>
        public static float LightFollowCap(float maxSpeed, float aggressionSpeed, float tow) => maxSpeed * aggressionSpeed * tow;

        /// <summary>A dashing forager's follow cap (the Boid's arrival-capped hunt, Boid.huntSpeedMultiplier), towed.</summary>
        public static float TadFollowCap(float maxSpeed, float hunt, float tick, float gap, float tow) =>
            Mathf.Min(maxSpeed * hunt, Mathf.Max(maxSpeed, gap / Mathf.Max(0.05f, tick))) * tow;

        /// <summary>How hard the ball pulls a gathering creature (u/s).</summary>
        public static float GatherSpeed(float d, float minSpeed, float top, float hullSpeed) =>
            Mathf.Clamp(d * 2f, minSpeed, Mathf.Max(top, hullSpeed * 1.2f));

        /// <summary>Taming: fills at 1/<paramref name="tameTime"/> per second while held in the light, fades at
        /// <paramref name="decay"/> per second outside it (never below 0).</summary>
        public static float TameStep(float tame, bool inHold, float dt, float tameTime, float decay) =>
            inHold ? tame + dt / tameTime : Mathf.Max(0f, tame - decay * dt);

        /// <summary>Where the esca hangs: <paramref name="ahead"/> along the nose and 2.5 u up.</summary>
        public static Vector3 LurePoint(Vector3 p, Vector3 f, Vector3 u, float ahead) =>
            new(p.x + f.x * ahead + u.x * 2.5f, p.y + f.y * ahead + u.y * 2.5f, p.z + f.z * ahead + u.z * 2.5f);

        /// <summary>A painted order's pick, regardless of distance: the candidate with the fewest
        /// <paramref name="claims"/>, then the most recently painted (largest <paramref name="stamps"/>), then the lowest
        /// index - so a squad fans out over the newest paint first. -1 for an empty list.</summary>
        public static int PickRecent(IReadOnlyList<float> stamps, IReadOnlyList<int> claims)
        {
            int best = -1;
            for (int i = 0; i < stamps.Count; i++)
                if (best < 0 || claims[i] < claims[best] || (claims[i] == claims[best] && stamps[i] > stamps[best])) best = i;
            return best;
        }

        /// <summary>The Time ability's landing point: <paramref name="frac"/> of the way from <paramref name="p"/> to the
        /// school's centroid <paramref name="c"/>.</summary>
        public static Vector3 DashPoint(Vector3 p, Vector3 c, float frac) =>
            new(p.x + (c.x - p.x) * frac, p.y + (c.y - p.y) * frac, p.z + (c.z - p.z) * frac);
    }
}
