using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The chrome half of the style foundation (<c>Docs/STYLE_FOUNDATION.md</c> §11), as data.
    ///
    /// Serialized fields only — every accessor lives on the static <see cref="UITheme"/> helper,
    /// which also carries the hardcoded fallbacks so a null theme reference degrades to the
    /// authored §11 value rather than to <c>default</c>.
    ///
    /// TEAM COLOURS ARE DELIBERATELY ABSENT. Jade, Ruby and Gold stay in <c>SO_ColorSet</c>:
    /// §3 makes team colour DATA and green the interactive hue, and the only structural way to
    /// keep a designer from tinting a button with a team colour is to give this asset no team
    /// field to reach for. Do not add one.
    /// </summary>
    [CreateAssetMenu(
        fileName = "UITheme",
        menuName = "ScriptableObjects/UI/UI Theme")]
    public class UIThemeSO : ScriptableObject
    {
        // ── Colour ────────────────────────────────────────────────────────────────────
        // §11 values as authored. Hex in the tooltip is the source of truth for review.

        [Header("Text")]
        [Tooltip("E6E9FF — all text except player names, buttons, emphasis; active selections; bounding boxes")]
        public Color textLight = new Color32(0xE6, 0xE9, 0xFF, 0xFF);
        [Tooltip("25262D — inactive text on inactive buttons")]
        public Color textInactive = new Color32(0x25, 0x26, 0x2D, 0xFF);

        [Header("Inactive")]
        [Tooltip("5C5F70 — inactive selections, buttons, regions")]
        public Color inactiveLight = new Color32(0x5C, 0x5F, 0x70, 0xFF);

        [Header("Surface")]
        [Tooltip("00010A — popup background, used at varying opacity")]
        public Color surfaceBlack = new Color32(0x00, 0x01, 0x0A, 0xFF);
        [Tooltip("00041F — Neutral (Very Dark)")]
        public Color surfaceVeryDark = new Color32(0x00, 0x04, 0x1F, 0xFF);
        [Tooltip("222645 — Neutral (Dark); panel borders and chrome")]
        public Color surfaceDark = new Color32(0x22, 0x26, 0x45, 0xFF);
        [Tooltip("434C89 — Neutral (Light)")]
        public Color surfaceLight = new Color32(0x43, 0x4C, 0x89, 0xFF);
        [Tooltip("747BAD — Neutral (Lightest); generic VICTORY/DEFEAT banner, purchased-card button")]
        public Color neutralLightest = new Color32(0x74, 0x7B, 0xAD, 0xFF);

        [Header("Signal")]
        [Tooltip("99FF80 — call to action; primary buttons, focus, selection, online status, app shell before a team exists")]
        public Color cta = new Color32(0x99, 0xFF, 0x80, 0xFF);
        [Tooltip("FF4B3A — destructive / danger. Approved in v0.3.2. Full-bleed fill only.")]
        public Color danger = new Color32(0xFF, 0x4B, 0x3A, 0xFF);

        // ── Spacing ───────────────────────────────────────────────────────────────────

        [Header("Spacing")]
        [Tooltip("§5 — 8px base scale. Index with UITheme.Spacing(theme, step).")]
        public float[] spacing = { 4f, 8f, 12f, 16f, 24f, 32f, 48f, 64f, 96f };

        // ── Geometry ──────────────────────────────────────────────────────────────────

        [Header("Geometry")]
        [Tooltip("§5 — corner sliver on large surfaces (cards, popups, nav tiles)")]
        public float sliverLarge = 14f;
        [Tooltip("§5 — corner sliver on buttons and chips")]
        public float sliverSmall = 10f;
        [Tooltip("§5 — hairline border")]
        public float hairline = 1f;
        [Tooltip("§5 — emphasis stroke")]
        public float stroke = 2f;

        // ── Motion ────────────────────────────────────────────────────────────────────

        [Header("Motion")]
        [Tooltip("§7 micro — hover, tint, focus (OutQuad)")]
        public float durMicro = 0.12f;
        [Tooltip("§7 std — press, toggle, tab change (OutCubic)")]
        public float durStd = 0.20f;
        [Tooltip("§7 panel — modal, screen slide, toast (OutQuint)")]
        public float durPanel = 0.32f;
        [Tooltip("§7 ceremony — quest claim, end-game reveal ONLY (OutBack)")]
        public float durCeremony = 0.50f;
        [Tooltip("§7 — delay per successive item in a staggered sequence")]
        public float staggerStep = 0.04f;
        [Tooltip("§7 — stagger index ceiling; item 9 and later share item 8's delay")]
        public int staggerCap = 8;
    }
}
