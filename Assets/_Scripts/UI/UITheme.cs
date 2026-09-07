using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>Colour roles carried by <see cref="UIThemeSO"/>. Chrome only — no team colours.</summary>
    public enum UIColorToken
    {
        TextLight = 0,
        TextInactive = 1,
        InactiveLight = 2,
        SurfaceBlack = 3,
        SurfaceVeryDark = 4,
        SurfaceDark = 5,
        SurfaceLight = 6,
        NeutralLightest = 7,
        Cta = 8,
        Danger = 9,
    }

    /// <summary>Motion durations from <c>Docs/STYLE_FOUNDATION.md</c> §7.</summary>
    public enum UIMotionToken
    {
        Micro = 0,
        Std = 1,
        Panel = 2,
        Ceremony = 3,
    }

    /// <summary>
    /// Every accessor for <see cref="UIThemeSO"/>, plus the hardcoded §11 fallbacks.
    ///
    /// The SO is serialized fields only, so this is where the null-safety lives — the same shape
    /// <c>CardEntranceAnimator</c> uses against <c>HUDAnimationSettingsSO</c>
    /// (<c>settings ? settings.field : literal</c>), lifted out so it is written once instead of
    /// once per consumer. A null theme yields the authored §11 value, never <c>default</c>:
    /// an unwired reference must degrade to the spec, not to transparent black.
    /// </summary>
    public static class UITheme
    {
        // ── Fallbacks (Docs/STYLE_FOUNDATION.md §11, verbatim) ────────────────────────

        public static readonly Color TextLight = new Color32(0xE6, 0xE9, 0xFF, 0xFF);
        public static readonly Color TextInactive = new Color32(0x25, 0x26, 0x2D, 0xFF);
        public static readonly Color InactiveLight = new Color32(0x5C, 0x5F, 0x70, 0xFF);
        public static readonly Color SurfaceBlack = new Color32(0x00, 0x01, 0x0A, 0xFF);
        public static readonly Color SurfaceVeryDark = new Color32(0x00, 0x04, 0x1F, 0xFF);
        public static readonly Color SurfaceDark = new Color32(0x22, 0x26, 0x45, 0xFF);
        public static readonly Color SurfaceLight = new Color32(0x43, 0x4C, 0x89, 0xFF);
        public static readonly Color NeutralLightest = new Color32(0x74, 0x7B, 0xAD, 0xFF);
        public static readonly Color Cta = new Color32(0x99, 0xFF, 0x80, 0xFF);
        public static readonly Color Danger = new Color32(0xFF, 0x4B, 0x3A, 0xFF);

        static readonly float[] DefaultSpacing = { 4f, 8f, 12f, 16f, 24f, 32f, 48f, 64f, 96f };

        public const float SliverLarge = 14f;
        public const float SliverSmall = 10f;
        public const float Hairline = 1f;
        public const float Stroke = 2f;

        public const float DurMicro = 0.12f;
        public const float DurStd = 0.20f;
        public const float DurPanel = 0.32f;
        public const float DurCeremony = 0.50f;
        public const float StaggerStep = 0.04f;
        public const int StaggerCap = 8;

        // ── Accessors ─────────────────────────────────────────────────────────────────

        /// <summary>Resolves a chrome colour role, falling back to the §11 value when unwired.</summary>
        public static Color Resolve(UIThemeSO theme, UIColorToken token)
        {
            switch (token)
            {
                case UIColorToken.TextLight: return theme ? theme.textLight : TextLight;
                case UIColorToken.TextInactive: return theme ? theme.textInactive : TextInactive;
                case UIColorToken.InactiveLight: return theme ? theme.inactiveLight : InactiveLight;
                case UIColorToken.SurfaceBlack: return theme ? theme.surfaceBlack : SurfaceBlack;
                case UIColorToken.SurfaceVeryDark: return theme ? theme.surfaceVeryDark : SurfaceVeryDark;
                case UIColorToken.SurfaceDark: return theme ? theme.surfaceDark : SurfaceDark;
                case UIColorToken.SurfaceLight: return theme ? theme.surfaceLight : SurfaceLight;
                case UIColorToken.NeutralLightest: return theme ? theme.neutralLightest : NeutralLightest;
                case UIColorToken.Cta: return theme ? theme.cta : Cta;
                case UIColorToken.Danger: return theme ? theme.danger : Danger;
                default: return theme ? theme.textLight : TextLight;
            }
        }

        /// <summary>
        /// §5 spacing by step index (0 = 4px … 8 = 96px). Out-of-range clamps to the nearest
        /// authored step rather than throwing — a layout should never take a scene down.
        /// </summary>
        public static float Spacing(UIThemeSO theme, int step)
        {
            float[] scale = theme != null && theme.spacing != null && theme.spacing.Length > 0
                ? theme.spacing
                : DefaultSpacing;
            return scale[Mathf.Clamp(step, 0, scale.Length - 1)];
        }

        /// <summary>
        /// §7 stagger delay for the item at <paramref name="index"/>, capped so a long list does
        /// not accumulate an unbounded lead-in.
        /// </summary>
        public static float StaggerFor(UIThemeSO theme, int index)
        {
            float step = theme ? theme.staggerStep : StaggerStep;
            int cap = theme ? theme.staggerCap : StaggerCap;
            return step * Mathf.Clamp(index, 0, Mathf.Max(0, cap));
        }

        /// <summary>§7 motion duration, falling back to the authored value when unwired.</summary>
        public static float Duration(UIThemeSO theme, UIMotionToken token)
        {
            switch (token)
            {
                case UIMotionToken.Micro: return theme ? theme.durMicro : DurMicro;
                case UIMotionToken.Std: return theme ? theme.durStd : DurStd;
                case UIMotionToken.Panel: return theme ? theme.durPanel : DurPanel;
                case UIMotionToken.Ceremony: return theme ? theme.durCeremony : DurCeremony;
                default: return theme ? theme.durStd : DurStd;
            }
        }

        /// <summary>§5 corner sliver. <paramref name="large"/> = cards/popups/nav tiles; else buttons and chips.</summary>
        public static float Sliver(UIThemeSO theme, bool large) =>
            large ? (theme ? theme.sliverLarge : SliverLarge)
                  : (theme ? theme.sliverSmall : SliverSmall);

        /// <summary>§5 border weight. <paramref name="emphasis"/> = the 2px stroke; else the 1px hairline.</summary>
        public static float BorderWidth(UIThemeSO theme, bool emphasis) =>
            emphasis ? (theme ? theme.stroke : Stroke)
                     : (theme ? theme.hairline : Hairline);
    }
}
