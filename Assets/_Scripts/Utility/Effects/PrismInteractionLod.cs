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
    /// <item><b>Destruction</b> — one spectrum, coarse to fine, by how big the prism is on screen: the ordinary
    /// EXPLOSION (small, off screen, over budget) → SHATTER (≥ <see cref="PrismInteractionLodConfigSO.ShatterMinScreen"/>)
    /// → GLITTER (≥ <see cref="PrismInteractionLodConfigSO.GlitterMinScreen"/>). Every finer level is the explosion
    /// squinted at, so where the line falls changes only how much detail a near viewer gets.</item>
    /// <item><b>Stealing</b> — the GRADE (the prism's 0.8 s colour lerp) when small, a SHED (bloom, unravel or flake)
    /// when the prism is at least <see cref="PrismInteractionLodConfigSO.ShedMinScreen"/> of the screen. A shed is the
    /// grade squinted at.</item>
    /// </list>
    ///
    /// "Size on screen" is the prism's projected DIAMETER over the screen height through
    /// <see cref="CameraManager.ViewCamera"/> (<see cref="ScreenFraction"/>), so the same numbers hold on every
    /// camera, field of view and resolution. A prism behind the camera or well off screen always gets the
    /// ordinary effect — the budget is spent where it is seen. The choice is local and cosmetic: two peers may
    /// draw one death at different levels, never a different outcome. Every refusal returns false and the caller
    /// draws the ordinary effect, so an interaction never loses its visual.
    /// </summary>
    public static class PrismInteractionLod
    {
        /// <summary>Destruction's levels, coarse to fine. Explicit values: a tuning vocabulary.</summary>
        public enum DestructionLevel
        {
            Explosion = 0,
            Shatter = 1,
            Glitter = 2,
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

        /// <summary>The destruction level for a prism covering <paramref name="fraction"/> of the screen height.</summary>
        public static DestructionLevel PickDestruction(float fraction, float shatterMin, float glitterMin) =>
            fraction >= glitterMin ? DestructionLevel.Glitter
            : fraction >= shatterMin ? DestructionLevel.Shatter
            : DestructionLevel.Explosion;

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
        /// A DESTRUCTION at its detail level: a shatter or a glitter when the prism is big enough on screen and the
        /// budget has room, drawn on the explosion's own flight and palette. False means the explosion — the
        /// caller (<c>PrismFactory.SpawnExplosion</c>) draws it.
        /// </summary>
        public static bool TryDestruction(Vector3 position, Quaternion rotation, Vector3 scale, Color bright, Color dark,
            Vector3 impactVelocity, float speedLimitOverride, PrismKind tier)
        {
            var cfg = PrismInteractionLodConfigSO.Instance;
            if (cfg == null || !cfg.DestructionEnabled || !PrismCollisionBurst.CanAccept(cfg)) return false;
            if (!TryMeasure(position, scale, out float fraction)) return false;

            var level = PickDestruction(fraction, cfg.ShatterMinScreen, cfg.GlitterMinScreen);
            if (level == DestructionLevel.Explosion) return false;
            int kind = level == DestructionLevel.Glitter ? PrismCollisionBurst.Glitter : PrismCollisionBurst.Shatter;
            return PrismCollisionBurst.TryRequestDestruction(position, rotation, scale, bright, dark,
                impactVelocity, speedLimitOverride, tier, kind);
        }

        /// <summary>
        /// A STEAL at its detail level: when the prism is big enough on screen and the budget has room, its old
        /// colours shed off it as a skin (bloom, unravel or flake, picked by weight from the pose) over a grade of
        /// <paramref name="gradeSeconds"/>. True means the caller must give the prism its new colour AT ONCE (the
        /// skin is the transition); false means the caller grades as usual.
        /// </summary>
        public static bool TryShed(Vector3 position, Quaternion rotation, Vector3 scale,
            Color oldBright, Color oldDark, Color newBright, float gradeSeconds)
        {
            var cfg = PrismInteractionLodConfigSO.Instance;
            if (cfg == null || !cfg.ShedEnabled || !PrismCollisionBurst.CanAccept(cfg)) return false;
            if (!TryMeasure(position, scale, out float fraction) || fraction < cfg.ShedMinScreen) return false;

            float roll = Mathf.Repeat(PrismCollisionBurst.SeedOf(position) * 7.919f + 0.371f, 1f);
            int kind = PrismCollisionBurst.PickShed(roll, cfg.BloomWeight, cfg.UnravelWeight, cfg.FlakeWeight);
            return PrismCollisionBurst.TryRequestShed(position, rotation, scale, oldBright, oldDark, newBright,
                gradeSeconds, kind);
        }
    }
}
