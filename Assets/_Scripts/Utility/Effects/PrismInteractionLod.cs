using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The DETAIL LEVEL of the prism's two fundamental interactions, decided once, at the instant of the event,
    /// for every prism in the game (Docs/PRISM_ANIMATION.md §4.11; config <see cref="PrismInteractionLodConfigSO"/>):
    ///
    /// <list type="table">
    /// <item><b>Destruction</b> — a Sierpinski cut scaled by recursion DEPTH (<see cref="PrismSierpinskiTiling"/>):
    /// depth 0 is the ordinary EXPLOSION (its four wedges a face); each deeper depth splits the corner triangles at
    /// their edge midpoints and keeps the middles whole. A shallower depth is the deeper one with its finest fragments
    /// merged back into their parents, so from far away it looks like the small ones are simply too small to see.</item>
    /// <item><b>Stealing</b> — the GRADE (the prism's 0.8 s colour lerp) → a SHED (bloom, unravel or flake) at three
    /// mesh resolutions. Every shed is the grade squinted at.</item>
    /// </list>
    ///
    /// Both ladders are graded by DISTANCE — the prism's projected diameter over the screen height through
    /// <see cref="CameraManager.ViewCamera"/> (<see cref="ScreenFraction"/>), against each level's line — and by
    /// AMOUNT — how full the shared budget already is (<see cref="PrismCollisionBurst.Fill"/>): crowded, one level
    /// coarser; packed, two (<see cref="CrowdSteps"/>). A level whose mesh does not fit the triangle budget steps
    /// down until one does. A prism behind the camera or well off screen gets the ordinary effect — the budget is
    /// spent where it is seen. The choice is local and cosmetic: two peers may draw one event at different levels,
    /// never a different outcome. Every refusal returns false and the caller draws the ordinary effect.
    /// </summary>
    public static class PrismInteractionLod
    {
        /// <summary>Stealing's levels, coarse to fine. Explicit values: a tuning vocabulary.</summary>
        public enum StealLevel
        {
            Grade = 0,
            Shed = 1,
            ShedFine = 2,
            ShedFinest = 3,
        }

        /// <summary>How far outside the screen (viewport units) a prism may be and still earn its detail —
        /// the camera turns during a 7.5 s death.</summary>
        public const float OffScreenMargin = 0.25f;

        /// <summary>
        /// The prism's projected DIAMETER as a fraction of the screen HEIGHT: a prism of half-diagonal
        /// <paramref name="radius"/> at <paramref name="distance"/> through a perspective camera of vertical field of
        /// view <paramref name="verticalFovDegrees"/>, or through an orthographic camera of half-height
        /// <paramref name="orthographicSize"/>. Pure, for the tests.
        /// </summary>
        public static float ScreenFraction(float radius, float distance, float verticalFovDegrees,
            bool orthographic = false, float orthographicSize = 0f)
        {
            if (orthographic) return orthographicSize > 1e-6f ? radius / orthographicSize : 0f;
            float tanHalf = Mathf.Tan(0.5f * Mathf.Clamp(verticalFovDegrees, 0.01f, 179f) * Mathf.Deg2Rad);
            return radius / (Mathf.Max(distance, 1e-4f) * tanHalf);
        }

        /// <summary>How many levels coarser the AMOUNT makes every new interaction: 0, 1 crowded, 2 packed.</summary>
        public static int CrowdSteps(float fill, float crowded, float packed) =>
            fill >= packed ? 2 : fill >= crowded ? 1 : 0;

        /// <summary>
        /// The level a size on screen earns, coarse to fine, before the amount: the finest level whose line
        /// <paramref name="fraction"/> reaches (lines ascending, level 1 first; level 0 has no line).
        /// </summary>
        public static int LevelForSize(float fraction, float line1, float line2 = float.PositiveInfinity,
            float line3 = float.PositiveInfinity, float line4 = float.PositiveInfinity) =>
            fraction >= line4 ? 4 : fraction >= line3 ? 3 : fraction >= line2 ? 2 : fraction >= line1 ? 1 : 0;

        /// <summary>
        /// The destruction DEPTH for a size on screen and an amount: the deepest depth whose line
        /// <paramref name="fraction"/> reaches, less the amount's steps, never below 0 (the explosion). Pure.
        /// </summary>
        public static int PickDepth(float fraction, int crowdSteps, PrismInteractionLodConfigSO cfg)
        {
            int depth = 0;
            for (int d = 1; d <= cfg.MaxDepth; d++)
                if (fraction >= cfg.DepthLine(d)) depth = d;
            return Mathf.Max(0, depth - crowdSteps);
        }

        /// <summary>The steal level for a size on screen and an amount. Pure, for the tests and the lab.</summary>
        public static StealLevel PickSteal(float fraction, int crowdSteps, float shed, float fine, float finest) =>
            (StealLevel)Mathf.Max(0, LevelForSize(fraction, shed, fine, finest) - crowdSteps);

        /// <summary>The mesh a death cut to <paramref name="depth"/> draws (null at 0: the explosion draws itself).</summary>
        public static Mesh MeshOfDepth(int depth) => depth <= 0 ? null : HighPolyPrismMesh.GetSierpinski(depth);

        /// <summary>The mesh steal level <paramref name="level"/> draws (null for the grade).</summary>
        public static Mesh MeshOf(StealLevel level, PrismInteractionLodConfigSO cfg) =>
            level == StealLevel.Grade ? null : HighPolyPrismMesh.GetShed(cfg.ShedSubdivisionOf((int)level));

        /// <summary>
        /// How big a prism at <paramref name="position"/> of world <paramref name="scale"/> is on the player's screen
        /// right now. False — the ordinary effect — with no camera, or when the prism is behind it or more than
        /// <see cref="OffScreenMargin"/> outside the view.
        /// </summary>
        public static bool TryMeasure(Vector3 position, Vector3 scale, out float fraction)
        {
            fraction = 0f;
            var cam = CameraManager.ViewCamera;
            if (!cam) return false;
            Vector3 vp = cam.WorldToViewportPoint(position);
            if (vp.z <= 0f) return false;
            if (vp.x < -OffScreenMargin || vp.x > 1f + OffScreenMargin ||
                vp.y < -OffScreenMargin || vp.y > 1f + OffScreenMargin) return false;
            float radius = 0.5f * scale.magnitude;
            fraction = ScreenFraction(radius, vp.z, cam.fieldOfView, cam.orthographic, cam.orthographicSize);
            return true;
        }

        /// <summary>
        /// A DESTRUCTION at its depth: the Sierpinski cut as deep as the prism's size on screen and the amount allow,
        /// each fragment a shard (the burst's glitter kind) on the explosion's own flight and palette. False means
        /// depth 0 — the caller (<c>PrismFactory.SpawnExplosion</c>) draws the explosion.
        /// </summary>
        public static bool TryDestruction(Vector3 position, Quaternion rotation, Vector3 scale, Color bright, Color dark,
            Vector3 impactVelocity, float speedLimitOverride, PrismKind tier)
        {
            var cfg = PrismInteractionLodConfigSO.Instance;
            if (cfg == null || !cfg.DestructionEnabled || !PrismCollisionBurst.CanAccept(cfg)) return false;
            if (!TryMeasure(position, scale, out float fraction)) return false;

            int steps = CrowdSteps(PrismCollisionBurst.Fill(cfg), cfg.CrowdedFill, cfg.PackedFill);
            // A depth whose mesh does not fit the triangle budget steps up until one does; depth 0 is the explosion.
            for (int depth = PickDepth(fraction, steps, cfg); depth > 0; depth--)
            {
                var mesh = MeshOfDepth(depth);
                if (!PrismCollisionBurst.FitsTriangles(cfg, PrismCollisionBurst.TrianglesOf(mesh))) continue;
                return PrismCollisionBurst.TryRequestDestruction(position, rotation, scale, bright, dark,
                    impactVelocity, speedLimitOverride, tier, PrismCollisionBurst.Glitter, mesh);
            }
            return false;
        }

        /// <summary>
        /// A STEAL at its detail level: when the prism is big enough on screen and the budget has room, its old
        /// colours shed off it as a skin (bloom, unravel or flake, picked by weight from the pose) at the resolution
        /// its size and the amount allow, over a grade of <paramref name="gradeSeconds"/>. True means the caller must
        /// give the prism its new colour AT ONCE (the skin is the transition); false means the caller grades as usual.
        /// </summary>
        public static bool TryShed(Vector3 position, Quaternion rotation, Vector3 scale,
            Color oldBright, Color oldDark, Color newBright, float gradeSeconds)
        {
            var cfg = PrismInteractionLodConfigSO.Instance;
            if (cfg == null || !cfg.ShedEnabled || !PrismCollisionBurst.CanAccept(cfg)) return false;
            if (!TryMeasure(position, scale, out float fraction)) return false;

            int steps = CrowdSteps(PrismCollisionBurst.Fill(cfg), cfg.CrowdedFill, cfg.PackedFill);
            var level = PickSteal(fraction, steps, cfg.ShedMinScreen, cfg.ShedFineScreen, cfg.ShedFinestScreen);
            float roll = Mathf.Repeat(PrismCollisionBurst.SeedOf(position) * 7.919f + 0.371f, 1f);
            int kind = PrismCollisionBurst.PickShed(roll, cfg.BloomWeight, cfg.UnravelWeight, cfg.FlakeWeight);
            for (; level > StealLevel.Grade; level--)
            {
                var mesh = MeshOf(level, cfg);
                if (!PrismCollisionBurst.FitsTriangles(cfg, PrismCollisionBurst.TrianglesOf(mesh))) continue;
                return PrismCollisionBurst.TryRequestShed(position, rotation, scale, oldBright, oldDark, newBright,
                    gradeSeconds, kind, mesh);
            }
            return false;
        }
    }
}
