using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Shrike's rules as PURE code (design: <c>R_VesselActions/SHRIKE.md</c>; the studio page
    /// <c>Docs/Studios/VesselStudio/shrike.html</c> is the source, and every function here is ported line for line from
    /// the page function named in its doc). No scene and no Unity object, so <c>ShrikeTests</c> run them as-is against
    /// golden numbers taken from the page's own arithmetic.
    ///
    /// <para><b>Space.</b> The page is three.js: the nose is local −Z. Unity's nose is +Z, so every local position the
    /// page writes as (x, y, z) is (x, y, −z) here. Right and up are the same in both.</para>
    /// </summary>
    public static class ShrikeRules
    {
        /// <summary>The nest's radius and block size (page <c>buildNest</c>: <c>Rn = isDen ? 12 : 9, s = isDen ? 6 : 4.5</c>).</summary>
        public static float NestRadius(bool isDen) => isDen ? 12f : 9f;
        public static float NestBlockSize(bool isDen) => isDen ? 6f : 4.5f;

        /// <summary>
        /// Block <paramref name="index"/> of <paramref name="count"/> in the nest's cup, in the hull's local space
        /// (page <c>buildNest</c>): a golden-angle spiral down a sphere from straight behind the hull to
        /// <c>zMin</c> (−0.25 for a nest, −0.6 for the den), so the shell wraps forward past the hull's sides and leaves
        /// the nose's way out open. At the page's radius (9, the den's 12).
        /// </summary>
        public static Vector3 NestLocal(int index, int count, bool isDen) => NestLocal(index, count, isDen, NestRadius(isDen));

        /// <summary><see cref="NestLocal(int,int,bool)"/> on a sphere of <paramref name="radius"/>.</summary>
        public static Vector3 NestLocal(int index, int count, bool isDen, float radius)
        {
            float zMin = isDen ? -0.6f : -0.25f;
            float z = 1f - (index + 0.5f) / count * (1f - zMin);
            float rr = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
            float ph = index * 2.39996f;
            // page (x, y, z) with +z behind the hull -> Unity (x, y, -z)
            return new Vector3(rr * Mathf.Cos(ph) * radius, rr * Mathf.Sin(ph) * radius, -z * radius);
        }

        /// <summary>The tightest spacing between two of the spiral's blocks on a UNIT sphere (it scales with the radius).</summary>
        public static float NestMinSpacing(int count, bool isDen)
        {
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Vector3 a = NestLocal(i, count, isDen, 1f);
                for (int j = i + 1; j < count; j++)
                {
                    float d = (a - NestLocal(j, count, isDen, 1f)).magnitude;
                    if (d < best) best = d;
                }
            }
            return count > 1 ? best : 2f;
        }

        /// <summary>
        /// The nest's radius with NO two blocks touching (Garrett, 2026-10-15: "the nest prisms should not have prisms
        /// clipping into each other"). Two cubes of side s whose centres are more than s√3 apart (their bounding
        /// spheres' diameters) cannot intersect whatever their orientation, so the cup grows until its tightest pair is
        /// that far apart, times <paramref name="clearance"/> (a shield shell draws a little proud of the block). Never
        /// smaller than the page's radius: more blocks (Mass) make a bigger nest, not smaller blocks.
        /// </summary>
        public static float NestRadiusFor(int count, bool isDen, float blockSize, float clearance = 1.1f)
            => Mathf.Max(NestRadius(isDen), blockSize * Mathf.Sqrt(3f) * clearance / NestMinSpacing(count, isDen));

        /// <summary>The turret flora on top of the nest (page: <c>local = (0, Rn + 2, Rn * 0.45)</c>, behind the hull).</summary>
        public static Vector3 TurretLocal(bool isDen) => TurretLocal(NestRadius(isDen));

        /// <summary>The turret flora on top of a nest of <paramref name="radius"/>.</summary>
        public static Vector3 TurretLocal(float radius) => new(0f, radius + 2f, -radius * 0.45f);

        /// <summary>How many blocks a nest has at this Mass (page <c>mults</c>: <c>Math.round(lerp(...))</c>, which rounds
        /// halves UP — C#'s banker's rounding would not).</summary>
        public static int NestBlockCount(float lerped) => Mathf.Max(1, Mathf.FloorToInt(lerped + 0.5f));

        /// <summary>The cooldown clock's rate (page <c>cdBonus</c>): ×1 flying, ×<paramref name="dockBonus"/> docked,
        /// ×<paramref name="dockBonusShielded"/> docked in a nest or on super-shielded mass.</summary>
        public static float CooldownRate(bool docked, bool shieldedOrNest, float dockBonus, float dockBonusShielded)
            => !docked ? 1f : shieldedOrNest ? dockBonusShielded : dockBonus;

        /// <summary>The sling's speed (page <c>stepTether</c>: "a 1 second fixed boost pointing at the target"):
        /// the distance to the anchor, covered in exactly <paramref name="seconds"/>.</summary>
        public static float SlingSpeed(float distance, float seconds) => distance / Mathf.Max(0.01f, seconds);

        /// <summary>How far the sling flies: to the anchor point, less the hull's own radius (page <c>stepTether</c>).</summary>
        public static float SlingDistance(Vector3 from, Vector3 anchor, float hullRadius)
            => Vector3.Distance(anchor, from) - hullRadius;

        /// <summary>Distance from a point (in the box's local, unrotated frame) to a box of half extents
        /// <paramref name="half"/>; 0 inside (page <c>boxDist</c>).</summary>
        public static float BoxDistance(Vector3 local, Vector3 half)
        {
            float x = Mathf.Max(Mathf.Abs(local.x) - half.x, 0f);
            float y = Mathf.Max(Mathf.Abs(local.y) - half.y, 0f);
            float z = Mathf.Max(Mathf.Abs(local.z) - half.z, 0f);
            return Mathf.Sqrt(x * x + y * y + z * z);
        }

        /// <summary>The box's closest point to a local point (page <c>boxClosest</c>, before it goes back to world).</summary>
        public static Vector3 BoxClosestLocal(Vector3 local, Vector3 half) => new(
            Mathf.Clamp(local.x, -half.x, half.x),
            Mathf.Clamp(local.y, -half.y, half.y),
            Mathf.Clamp(local.z, -half.z, half.z));

        /// <summary>
        /// The entry distance of a ray (thickened by <paramref name="pad"/>) into a box, within
        /// <paramref name="maxT"/> (page <c>rayBox</c>, slab by slab). Origin and direction are in the box's local,
        /// unrotated frame; a ray starting inside enters at 0.
        /// </summary>
        public static bool TryRayBox(Vector3 localOrigin, Vector3 localDir, Vector3 half, float pad, float maxT, out float entry)
        {
            float t0 = 0f, t1 = maxT;
            entry = 0f;
            for (int axis = 0; axis < 3; axis++)
            {
                float h = half[axis] + pad, o = localOrigin[axis], d = localDir[axis];
                if (Mathf.Abs(d) < 1e-9f)
                {
                    if (o < -h || o > h) return false;
                    continue;
                }
                float a = (-h - o) / d, b = (h - o) / d;
                if (a > b) (a, b) = (b, a);
                if (a > t0) t0 = a;
                if (b < t1) t1 = b;
                if (t0 > t1) return false;
            }
            entry = t0;
            return true;
        }

        /// <summary>Does a ray (origin, unit direction, length) pass within <paramref name="radius"/> of a centre
        /// (page <c>raySphere</c>)?</summary>
        public static bool RaySphere(Vector3 origin, Vector3 dir, Vector3 centre, float radius, float length)
        {
            Vector3 oc = centre - origin;
            float t = Vector3.Dot(oc, dir);
            if (t < -radius || t > length + radius) return false;
            return oc.sqrMagnitude - t * t <= radius * radius;
        }

        /// <summary>Turn <paramref name="dir"/> toward <paramref name="want"/> by at most <paramref name="maxRadians"/>
        /// (page <c>stepMissiles</c>: <c>applyAxisAngle(cross(dir, want), min(angle, maxA))</c>). Both unit length.</summary>
        public static Vector3 TurnToward(Vector3 dir, Vector3 want, float maxRadians, Vector3 fallbackAxis)
        {
            float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(dir, want), -1f, 1f));
            if (angle <= 1e-4f) return dir;
            Vector3 axis = Vector3.Cross(dir, want);
            if (axis.sqrMagnitude < 1e-8f) axis = fallbackAxis;
            return (Quaternion.AngleAxis(Mathf.Min(angle, maxRadians) * Mathf.Rad2Deg, axis.normalized) * dir).normalized;
        }

        /// <summary>A missile's launch heading (page <c>launchMissiles</c>): up off the turret, bent toward the target.</summary>
        public static Vector3 LaunchDirection(Vector3 up, Vector3 toTarget) => (up + toTarget.normalized).normalized;

        /// <summary>
        /// One tick of a nest flora growing its missiles (Garrett, 2026-10-15: a crystal "morph[s] into a nest flora that
        /// visibly grows large missiles"): the growing missile fills over <paramref name="growSeconds"/>, twice as fast
        /// while its Shrike is docked in its nest, and joins the ready missiles when full, up to <paramref name="max"/>.
        /// <paramref name="growth"/> is the growing missile's progress in seconds (0 while the flora is full).
        /// </summary>
        public static void GrowTick(ref int ready, ref float growth, float dt, bool docked, float growSeconds, int max)
        {
            if (ready >= max) { growth = 0f; return; }
            growth += dt * (docked ? 2f : 1f);
            if (growth < growSeconds) return;
            growth = 0f;
            ready = Mathf.Min(max, ready + 1);
        }

        /// <summary>May this flora launch at the painted target? A ready missile and its clock run.</summary>
        public static bool FloraMayLaunch(int ready, float launchCooldown) => ready > 0 && launchCooldown <= 0f;

        /// <summary>What the Targeting Array locks first (Garrett, 2026-10-15: "vessels, fauna, flora, prisms").</summary>
        public enum LockClass { Vessel = 0, Fauna = 1, Flora = 2, Mass = 3, None = 4 }

        /// <summary>Is a candidate (<paramref name="kind"/>, <paramref name="angle"/> off the nose) a better lock than
        /// the best so far? A higher-priority class always wins; within a class, the one nearer the aim.</summary>
        public static bool BetterLock(LockClass kind, float angle, LockClass bestKind, float bestAngle)
            => kind < bestKind || kind == bestKind && angle < bestAngle;

        /// <summary>The element each mode is (the eyepiece wears its petal): 0 Space (the lance), 1 Charge (the array),
        /// 2 Time (the sling) — <c>ShrikeExecutor.Mode</c>'s order.</summary>
        public static Element ModeElement(int mode) => mode switch
        {
            1 => Element.Charge,
            2 => Element.Time,
            _ => Element.Space,
        };

        /// <summary>Does the Void Lance break this block? Every block, but super-shielded mass only with the Space
        /// upgrade (Garrett, 2026-10-15); below it, super-shielded mass lets the beam pass.</summary>
        public static bool LanceBreaks(bool superShielded, bool upgraded) => !superShielded || upgraded;
    }
}
