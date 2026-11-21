using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The open page on the right of the Codex (Docs/MENU_REDESIGN_B.md sections 88 and 97): the entry's picture, kicker
    /// and title, then what it does in play, its facts, its element cells and its rows, and its lore last ("gameplay info
    /// above lore") - each block copied from a hidden template line and hidden whole while it is empty. It draws a
    /// <see cref="CodexPage"/> and decides nothing.
    ///
    /// <para>Opening a page animates it (Shombith 2026-10-27: "animate in the descriptions"): the picture plays its
    /// element's entrance, the words type themselves in, and each line of facts, cells and rows grows in a beat after
    /// the one above it.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Codex Page View")]
    public class CodexPageView : MonoBehaviour
    {
        [Tooltip("The whole page: hidden while nothing is open (an empty chapter).")]
        [SerializeField] private CanvasGroup pageGroup;
        [Tooltip("The entry's picture, alive (CodexPortrait).")]
        [SerializeField] private CodexPortrait portrait;
        [Tooltip("The page's words and blocks: fade and rise in each time a page opens.")]
        [SerializeField] private CanvasGroup reveal;
        [SerializeField, Min(0.05f)] private float revealSeconds = 0.32f;
        [SerializeField, Min(0f)] private float revealRise = 12f;
        [SerializeField] private TMP_Text kickerText;
        [SerializeField] private TMP_Text titleText;
        [Tooltip("What the entry does in play, read first. Hidden while empty.")]
        [SerializeField] private TMP_Text gameplayText;
        [Tooltip("The lore block at the foot of the page: hidden while the entry has none.")]
        [SerializeField] private CanvasGroup loreBlock;
        [SerializeField] private TMP_Text loreHeading;
        [Tooltip("The entry's lore (a tagline and description).")]
        [SerializeField] private TMP_Text bodyText;
        [Tooltip("The heading over the lore. Empty: none.")]
        [SerializeField] private string loreHeadingText = "Lore";

        [Header("Typing")]
        [Tooltip("Characters a second the gameplay line and the lore type in at.")]
        [SerializeField, Min(10f)] private float typeRate = 140f;
        [Tooltip("Seconds between one line of facts, cells or rows and the next growing in.")]
        [SerializeField, Min(0f)] private float lineStagger = 0.05f;
        [Tooltip("Seconds a line takes to grow in.")]
        [SerializeField, Min(0.05f)] private float lineSeconds = 0.26f;
        [Tooltip("The scale a line grows in from (a layout group places it, so it grows rather than slides).")]
        [SerializeField, Range(0.5f, 1f)] private float lineFrom = 0.92f;
        [Tooltip("The plate's caption under the picture (the Field Guide's left page). Optional.")]
        [SerializeField] private TMP_Text plateText;
        [Tooltip("The plate's caption. {kicker} and {title} are the page's. Empty: none.")]
        [SerializeField] private string plateTemplate = "{kicker} - {title}";
        [Tooltip("Scrolled back to the top whenever a new page opens.")]
        [SerializeField] private ScrollRect scroll;

        [Header("Facts")]
        [SerializeField] private CanvasGroup factsBlock;
        [SerializeField] private TMP_Text factsHeading;
        [Tooltip("The hidden template a fact line is copied from.")]
        [SerializeField] private CodexLine factTemplate;

        [Header("Element cells")]
        [SerializeField] private CanvasGroup cellsBlock;
        [SerializeField] private TMP_Text cellsHeading;
        [SerializeField] private CodexLine cellTemplate;

        [Header("Rows")]
        [SerializeField] private CanvasGroup rowsBlock;
        [SerializeField] private TMP_Text rowsHeading;
        [SerializeField] private CodexLine rowTemplate;

        readonly List<CodexLine> _facts = new();
        readonly List<CodexLine> _cells = new();
        readonly List<CodexLine> _rows = new();
        readonly List<CodexLine> _sliding = new();
        float _reveal = 1f, _clock = -1f;
        int _gameplayChars, _loreChars;

        void Awake()
        {
            HideTemplate(factTemplate);
            HideTemplate(cellTemplate);
            HideTemplate(rowTemplate);
        }

        public void Show(CodexPage page)
        {
            KitVisibility.SetShown(pageGroup, page != null);
            if (page == null)
            {
                // Nothing open (an empty chapter, or this page's layout put away): its picture stops moving.
                if (portrait) portrait.Select(false);
                return;
            }
            if (portrait)
            {
                portrait.Show(page.Image, page.Flipbook, page.Element, page.Palette, 0f);
                portrait.Select(true);
            }
            if (kickerText) kickerText.text = page.Kicker;
            if (titleText) titleText.text = page.Title;
            if (plateText) plateText.text = TemplateText.Fill(plateTemplate, "kicker", page.Kicker, "title", page.Title);
            _gameplayChars = Words(gameplayText, page.Gameplay);
            _loreChars = Words(bodyText, page.Body);
            KitVisibility.SetShown(loreBlock, _loreChars > 0);
            if (loreHeading) loreHeading.text = loreHeadingText;

            Block(factsBlock, factsHeading, page.FactsHeading, page.Facts.Count);
            Fill(_facts, factTemplate, page.Facts.Count, (l, i) => l.Set(null, page.Facts[i].Label, page.Facts[i].Value));
            Block(cellsBlock, cellsHeading, page.CellsHeading, page.Cells.Count);
            Fill(_cells, cellTemplate, page.Cells.Count, (l, i) => l.Set(page.Cells[i].Icon, page.Cells[i].Title, page.Cells[i].Lines));
            Block(rowsBlock, rowsHeading, page.RowsHeading, page.Rows.Count);
            Fill(_rows, rowTemplate, page.Rows.Count, (l, i) => l.Set(page.Rows[i].Icon, page.Rows[i].Title, page.Rows[i].Line));

            if (scroll) scroll.verticalNormalizedPosition = 1f;
            _reveal = 0f;
            Reveal(0f);

            // Every shown line, top to bottom, grows in on the page's clock.
            _sliding.Clear();
            Collect(_facts, page.Facts.Count);
            Collect(_cells, page.Cells.Count);
            Collect(_rows, page.Rows.Count);
            _clock = 0f;
            Animate(0f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_reveal < 1f)
            {
                _reveal = Mathf.Min(1f, _reveal + dt / revealSeconds);
                Reveal(_reveal);
            }
            if (_clock >= 0f)
            {
                _clock += dt;
                if (!Animate(_clock)) _clock = -1f;
            }
        }

        /// <summary>The page's words and lines at <paramref name="t"/> seconds after it opened; false once all have landed.</summary>
        bool Animate(float t)
        {
            int typed = Mathf.FloorToInt(t * typeRate);
            if (gameplayText) gameplayText.maxVisibleCharacters = Mathf.Min(typed, _gameplayChars);
            // The lore starts typing once the lines above it have landed.
            float loreStart = _sliding.Count * lineStagger + lineSeconds;
            int loreTyped = Mathf.Max(0, Mathf.FloorToInt((t - loreStart) * typeRate));
            if (bodyText) bodyText.maxVisibleCharacters = Mathf.Min(loreTyped, _loreChars);

            bool busy = typed < _gameplayChars || loreTyped < _loreChars;
            for (int i = 0; i < _sliding.Count; i++)
            {
                float k = Mathf.Clamp01((t - i * lineStagger) / lineSeconds);
                float e = 1f - (1f - k) * (1f - k) * (1f - k);    // OutCubic
                var line = _sliding[i];
                if (!line.Group) continue;
                line.Group.alpha = e;
                line.Group.transform.localScale = Vector3.one * Mathf.LerpUnclamped(lineFrom, 1f, e);
                busy |= k < 1f;
            }
            return busy;
        }

        void Collect(List<CodexLine> pool, int count)
        {
            for (int i = 0; i < count && i < pool.Count; i++) _sliding.Add(pool[i]);
        }

        /// <summary>Sets the words, hidden, ready to type; returns how many characters they hold.</summary>
        static int Words(TMP_Text label, string words)
        {
            if (!label) return 0;
            label.text = words ?? string.Empty;
            label.maxVisibleCharacters = 0;
            KitVisibility.SetShown(label.GetComponent<CanvasGroup>(), !string.IsNullOrEmpty(words));
            return string.IsNullOrEmpty(words) ? 0 : words.Length;
        }

        void Reveal(float k)
        {
            if (!reveal) return;
            float e = 1f - (1f - k) * (1f - k) * (1f - k);    // OutCubic
            reveal.alpha = e;
            ((RectTransform)reveal.transform).anchoredPosition = new Vector2(0f, -revealRise * (1f - e));
        }

        static void Block(CanvasGroup block, TMP_Text heading, string headingText, int count)
        {
            KitVisibility.SetShown(block, count > 0);
            if (heading) heading.text = headingText ?? string.Empty;
        }

        static void HideTemplate(CodexLine template)
        {
            if (template) KitVisibility.SetShown(template.Group, false);
        }

        static void Fill(List<CodexLine> pool, CodexLine template, int count, System.Action<CodexLine, int> set)
        {
            if (!template) return;
            while (pool.Count < count)
            {
                var line = Instantiate(template, template.transform.parent);
                line.name = $"{template.name}{pool.Count + 1}";
                pool.Add(line);
            }
            for (int i = 0; i < pool.Count; i++)
            {
                bool used = i < count;
                KitVisibility.SetShown(pool[i].Group, used);
                if (used) set(pool[i], i);
            }
        }
    }
}
