using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Read access for <see cref="UIThemeSO"/>, and the hardcoded fallbacks that make an
    /// unassigned theme reference degrade to the shipped tokens rather than to black.
    ///
    /// <para>This lives beside the asset rather than on it deliberately: <c>UIThemeSO</c> is
    /// <b>25 serialized fields and nothing else</b>, which is what makes "authored to
    /// <c>Docs/STYLE_FOUNDATION.md</c> §10 verbatim" a checkable claim rather than a
    /// judgement call. Anything that is not a token belongs here.</para>
    ///
    /// <para>Call sites read <c>theme.Resolve().textBody</c>, <c>theme.Spacing(4)</c>,
    /// <c>theme.StaggerFor(i)</c> — all safe on an unassigned reference, since an extension
    /// method may be invoked on a null <c>this</c>.</para>
    /// </summary>
    public static class UIThemeHelper
    {
        /// <summary>Steps on the §5 spacing scale (<c>s1</c>..<c>s9</c>).</summary>
        public const int SpacingSteps = 9;

        static readonly float[] DefaultSpacing = { 4f, 8f, 12f, 16f, 24f, 32f, 48f, 64f, 96f };

        static readonly HashSet<int> WarnedSpacing = new HashSet<int>();

        static UIThemeSO _fallback;

        /// <summary>
        /// A throwaway instance carrying nothing but <see cref="UIThemeSO"/>'s hardcoded
        /// defaults. Prefer <see cref="Resolve"/>.
        /// </summary>
        public static UIThemeSO Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = ScriptableObject.CreateInstance<UIThemeSO>();
                    _fallback.name = "UITheme (defaults)";
                    _fallback.hideFlags = HideFlags.HideAndDontSave;
                }
                return _fallback;
            }
        }

        /// <summary>
        /// The authored asset when one is wired, the hardcoded defaults when it is not.
        /// Never returns null, so a call site never has to restate a token value —
        /// which is how <see cref="HUDAnimationSettingsSO"/>'s defaults ended up duplicated
        /// inline at <c>ScoreNumberAnimator.cs:131-132</c>.
        /// </summary>
        public static UIThemeSO Resolve(this UIThemeSO theme) => theme ? theme : Fallback;

        /// <summary>
        /// The 1-based spacing token: <c>Spacing(1)</c> is <c>s1</c> (4px), <c>Spacing(9)</c>
        /// is <c>s9</c> (96px). Out-of-range steps clamp to the scale.
        ///
        /// <para>Falls back to the shipped scale — loudly, once per asset — if the serialized
        /// array has been resized in the inspector. A silent fallback here would be
        /// indistinguishable from a theme that never applied.</para>
        /// </summary>
        public static float Spacing(this UIThemeSO theme, int step)
        {
            var t = theme.Resolve();
            int i = Mathf.Clamp(step, 1, SpacingSteps) - 1;
            var scale = t.spacing;

            if (scale == null || scale.Length != SpacingSteps)
            {
                if (WarnedSpacing.Add(t.GetInstanceID()))
                    Debug.LogWarning(
                        $"[UIThemeSO] '{t.name}' has {(scale == null ? 0 : scale.Length)} spacing " +
                        $"entries, expected {SpacingSteps} (s1..s9). Falling back to the shipped " +
                        "scale. Restore the array length in the inspector.", t);

                return DefaultSpacing[i];
            }

            return scale[i];
        }

        /// <summary>
        /// Entrance delay for the <paramref name="index"/>-th item in a staggered list,
        /// honouring §8's cap. The two fields are meaningless apart — the current hangar grid
        /// runs 80ms across an unbounded list, which is exactly what the cap prevents.
        /// </summary>
        public static float StaggerFor(this UIThemeSO theme, int index)
        {
            var t = theme.Resolve();
            return Mathf.Min(Mathf.Max(index, 0), Mathf.Max(t.staggerCap, 0)) * t.staggerStep;
        }

        /// <summary>
        /// 0xRRGGBB, opaque. Keeps <c>Docs/STYLE_FOUNDATION.md</c> §10's hex visible in
        /// <see cref="UIThemeSO"/>'s field initialisers, so the defaults can be diffed against
        /// the document by eye.
        /// </summary>
        public static Color Rgb(int hex) => new Color(
            ((hex >> 16) & 0xFF) / 255f,
            ((hex >> 8) & 0xFF) / 255f,
            (hex & 0xFF) / 255f,
            1f);
    }
}
