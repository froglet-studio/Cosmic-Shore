using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// THE chrome vocabulary for every menu screen, modal, panel and HUD frame —
    /// authored verbatim from <c>Docs/STYLE_FOUNDATION.md</c> §10 (the field map).
    /// Surfaces, borders, text ramp, system hues, the 8px spacing scale, the one
    /// chamfer, stroke weights, and the motion/stagger tokens.
    ///
    /// <para><b>Team colours are deliberately NOT here.</b> Jade / Ruby / Gold live in
    /// <see cref="CosmicShore.ScriptableObjects.SO_ColorSet"/> and are read through
    /// <c>GetDomainUIColor</c> / <c>GetDomainSignalColor</c> / <c>GetDomainUIAccentColor</c>.
    /// That separation is what enforces the team-colour contract in §3: chrome is neutral,
    /// and a coloured pixel is always telling the player something. A team colour that
    /// leaked into this asset would make a Ruby player's whole interface read as a
    /// permanent error state.</para>
    ///
    /// <para>Follows the <see cref="HUDAnimationSettingsSO"/> pattern: every value has a
    /// hardcoded default equal to the shipped token, so an unassigned reference degrades
    /// to the correct look rather than to black. Call sites read
    /// <c>theme ? theme.textBody : UIThemeSO.Fallback.textBody</c> — or simply
    /// <c>UIThemeSO.Resolve(theme).textBody</c>.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "UITheme",
        menuName = "ScriptableObjects/UI/UI Theme")]
    public class UIThemeSO : ScriptableObject
    {
        // ---------------------------------------------------------------- colour
        // Style Foundation §2. Hex is preserved in the Rgb() literals so these can be
        // diffed against the document by eye. UI colours are sRGB, so byte/255 is
        // exactly what ColorUtility.TryParseHtmlString would produce.

        [Header("Surface ramp (§2)")]
        [Tooltip("#07090F — scrims, modal backdrop, deepest field")]
        public Color surfaceVoid = Rgb(0x07090F);
        [Tooltip("#0E131C — default panel surface")]
        public Color surfaceHull = Rgb(0x0E131C);
        [Tooltip("#171E2A — raised surface, card, button rest")]
        public Color surfacePlate = Rgb(0x171E2A);
        [Tooltip("#212B3A — hover surface, active row")]
        public Color surfaceRaise = Rgb(0x212B3A);

        [Header("Borders (§2)")]
        [Tooltip("#2A3444 — hairline border, divider")]
        public Color borderRule = Rgb(0x2A3444);
        [Tooltip("#3D4A5E — emphasised border, table head")]
        public Color borderRuleHigh = Rgb(0x3D4A5E);

        [Header("Text ramp (§2)")]
        [Tooltip("#E8EDF5 — headings, primary values. Never pure white: it buzzes on a dark HUD over a moving arena")]
        public Color textSignal = Rgb(0xE8EDF5);
        [Tooltip("#B9C4D2 — body copy, descriptions")]
        public Color textBody = Rgb(0xB9C4D2);
        [Tooltip("#7C8899 — labels, secondary, captions")]
        public Color textMuted = Rgb(0x7C8899);
        [Tooltip("#4E5A6B — disabled, placeholder, metadata")]
        public Color textFaint = Rgb(0x4E5A6B);

        [Header("System and reserved hues (§2)")]
        [Tooltip("#4FD5E8 — focus, selection, links, chrome accent, ALL pre-team UI. Cyan is the system hue because Domains.Blue is already the codebase's neutral non-playable sentinel")]
        public Color systemAccent = Rgb(0x4FD5E8);
        [Tooltip("#2A8A99 — inactive tab, unfilled track")]
        public Color systemDim = Rgb(0x2A8A99);
        [Tooltip("#A67CFF — new / unclaimed / CTA badge ONLY. Sits outside the Jade/Ruby/Gold gamut deliberately")]
        public Color attention = Rgb(0xA67CFF);
        [Tooltip("#FF5C3A — destructive FILL only, never a tint or a border. Form, not hue, separates it from Ruby")]
        public Color danger = Rgb(0xFF5C3A);

        // ------------------------------------------------------- space & geometry

        [Header("Spacing scale (§5) — 8px base at the 1920 reference")]
        [Tooltip("s1..s9. Every margin, padding and gap is a step on this scale. Read it through Spacing(step), which is 1-based to match the s1..s9 token names")]
        public float[] spacing = { 4f, 8f, 12f, 16f, 24f, 32f, 48f, 64f, 96f };

        [Header("Geometry (§5)")]
        [Tooltip("14px, top-right only — panels, cards, modals. One 9-slice sprite serves all")]
        public float chamferLarge = 14f;
        [Tooltip("10px, top-right only — buttons, chips, badges. Border radius is 0 everywhere; the chamfer is the only corner treatment")]
        public float chamferSmall = 10f;
        [Tooltip("1px — all borders and dividers")]
        public float hairline = 1f;
        [Tooltip("2px — focus ring, selected state, own-chip border")]
        public float stroke = 2f;

        // ---------------------------------------------------------------- motion

        [Header("Motion (§8)")]
        [Tooltip("120ms, OutQuad — hover, tint, focus move")]
        public float durMicro = 0.12f;
        [Tooltip("200ms, OutCubic — button press, toggle, tab change")]
        public float durStd = 0.20f;
        [Tooltip("320ms, OutQuint — modal in/out, screen slide, toast entry")]
        public float durPanel = 0.32f;
        [Tooltip("500ms+, OutBack — quest claim and end-game reveal, nothing else")]
        public float durCeremony = 0.50f;

        [Header("Stagger (§8)")]
        [Tooltip("40ms per item")]
        public float staggerStep = 0.04f;
        [Tooltip("Capped at 8 items — beyond this the delay stops growing, so a long list can never stall behind its own entrance")]
        public int staggerCap = 8;

        // ------------------------------------------------------------- accessors

        /// <summary>Number of steps on the spacing scale (s1..s9).</summary>
        public const int SpacingSteps = 9;

        static readonly float[] DefaultSpacing = { 4f, 8f, 12f, 16f, 24f, 32f, 48f, 64f, 96f };

        bool _warnedSpacing;

        /// <summary>
        /// The 1-based spacing token: <c>Spacing(1)</c> is <c>s1</c> (4px),
        /// <c>Spacing(9)</c> is <c>s9</c> (96px). Out-of-range steps clamp to the scale.
        ///
        /// <para>Falls back to the shipped scale — loudly, once — if the serialized array
        /// has been resized in the inspector. A silent fallback here would be
        /// indistinguishable from a theme that never applied.</para>
        /// </summary>
        public float Spacing(int step)
        {
            int i = Mathf.Clamp(step, 1, SpacingSteps) - 1;

            if (spacing == null || spacing.Length != SpacingSteps)
            {
                if (!_warnedSpacing)
                {
                    _warnedSpacing = true;
                    Debug.LogWarning(
                        $"[UIThemeSO] '{name}' has {(spacing == null ? 0 : spacing.Length)} spacing " +
                        $"entries, expected {SpacingSteps} (s1..s9). Falling back to the shipped scale. " +
                        "Restore the array length in the inspector.", this);
                }
                return DefaultSpacing[i];
            }

            return spacing[i];
        }

        /// <summary>
        /// Entrance delay for the <paramref name="index"/>-th item in a staggered list,
        /// honouring the §8 cap. The two fields are meaningless apart — the current hangar
        /// grid runs 80ms across an unbounded list, which is exactly what the cap prevents.
        /// </summary>
        public float StaggerFor(int index) =>
            Mathf.Min(Mathf.Max(index, 0), Mathf.Max(staggerCap, 0)) * staggerStep;

        // ------------------------------------------------------------- fallbacks

        static UIThemeSO _fallback;

        /// <summary>
        /// A throwaway instance carrying nothing but the hardcoded defaults above, so an
        /// unassigned theme reference resolves to the shipped tokens instead of to black.
        /// Prefer <see cref="Resolve"/> at call sites.
        /// </summary>
        public static UIThemeSO Fallback
        {
            get
            {
                if (_fallback == null)
                {
                    _fallback = CreateInstance<UIThemeSO>();
                    _fallback.name = "UITheme (defaults)";
                    _fallback.hideFlags = HideFlags.HideAndDontSave;
                }
                return _fallback;
            }
        }

        /// <summary>
        /// <c>UIThemeSO.Resolve(theme).textBody</c> — the authored asset when one is wired,
        /// the hardcoded defaults when it is not. Never returns null.
        /// </summary>
        public static UIThemeSO Resolve(UIThemeSO theme) => theme ? theme : Fallback;

        /// <summary>0xRRGGBB, opaque. Keeps the document's hex visible in the source.</summary>
        static Color Rgb(int hex) => new Color(
            ((hex >> 16) & 0xFF) / 255f,
            ((hex >> 8) & 0xFF) / 255f,
            (hex & 0xFF) / 255f,
            1f);
    }
}
