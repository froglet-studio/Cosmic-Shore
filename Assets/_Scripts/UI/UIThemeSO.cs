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
    /// to the correct look rather than to black.</para>
    ///
    /// <para><b>This type is 25 serialized fields and nothing else</b> — no helpers, no
    /// constants, no accessors — so "authored to §10 verbatim" stays mechanically checkable
    /// against the document's table. Read access and the fallbacks live in
    /// <see cref="UIThemeHelper"/>: <c>theme.Resolve().textBody</c>, <c>theme.Spacing(4)</c>,
    /// <c>theme.StaggerFor(i)</c>.</para>
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
        public Color surfaceVoid = UIThemeHelper.Rgb(0x07090F);
        [Tooltip("#0E131C — default panel surface")]
        public Color surfaceHull = UIThemeHelper.Rgb(0x0E131C);
        [Tooltip("#171E2A — raised surface, card, button rest")]
        public Color surfacePlate = UIThemeHelper.Rgb(0x171E2A);
        [Tooltip("#212B3A — hover surface, active row")]
        public Color surfaceRaise = UIThemeHelper.Rgb(0x212B3A);

        [Header("Borders (§2)")]
        [Tooltip("#2A3444 — hairline border, divider")]
        public Color borderRule = UIThemeHelper.Rgb(0x2A3444);
        [Tooltip("#3D4A5E — emphasised border, table head")]
        public Color borderRuleHigh = UIThemeHelper.Rgb(0x3D4A5E);

        [Header("Text ramp (§2)")]
        [Tooltip("#E8EDF5 — headings, primary values. Never pure white: it buzzes on a dark HUD over a moving arena")]
        public Color textSignal = UIThemeHelper.Rgb(0xE8EDF5);
        [Tooltip("#B9C4D2 — body copy, descriptions")]
        public Color textBody = UIThemeHelper.Rgb(0xB9C4D2);
        [Tooltip("#7C8899 — labels, secondary, captions")]
        public Color textMuted = UIThemeHelper.Rgb(0x7C8899);
        [Tooltip("#4E5A6B — disabled, placeholder, metadata")]
        public Color textFaint = UIThemeHelper.Rgb(0x4E5A6B);

        [Header("System and reserved hues (§2)")]
        [Tooltip("#4FD5E8 — focus, selection, links, chrome accent, ALL pre-team UI. Cyan is the system hue because Domains.Blue is already the codebase's neutral non-playable sentinel")]
        public Color systemAccent = UIThemeHelper.Rgb(0x4FD5E8);
        [Tooltip("#2A8A99 — inactive tab, unfilled track")]
        public Color systemDim = UIThemeHelper.Rgb(0x2A8A99);
        [Tooltip("#A67CFF — new / unclaimed / CTA badge ONLY. Sits outside the Jade/Ruby/Gold gamut deliberately")]
        public Color attention = UIThemeHelper.Rgb(0xA67CFF);
        [Tooltip("#FF5C3A — destructive FILL only, never a tint or a border. Form, not hue, separates it from Ruby")]
        public Color danger = UIThemeHelper.Rgb(0xFF5C3A);

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
    }
}
