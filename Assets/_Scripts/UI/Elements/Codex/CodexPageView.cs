using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The open page on the right of the Codex (Docs/MENU_REDESIGN_B.md section 88): the entry's picture, kicker, title
    /// and body, then its facts, its element cells and its rows - each block copied from a hidden template line and hidden
    /// whole while it is empty. It draws a <see cref="CodexPage"/> and decides nothing.
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
        [Tooltip("The entry's own words (a tagline and description, a pilot's line). Hidden while empty.")]
        [SerializeField] private TMP_Text bodyText;
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
        float _reveal = 1f;

        void Awake()
        {
            HideTemplate(factTemplate);
            HideTemplate(cellTemplate);
            HideTemplate(rowTemplate);
        }

        public void Show(CodexPage page)
        {
            KitVisibility.SetShown(pageGroup, page != null);
            if (page == null) return;
            if (portrait) portrait.Show(page.Image, page.Element, page.Palette, 0f);
            if (kickerText) kickerText.text = page.Kicker;
            if (titleText) titleText.text = page.Title;
            if (bodyText) bodyText.text = page.Body;

            Block(factsBlock, factsHeading, page.FactsHeading, page.Facts.Count);
            Fill(_facts, factTemplate, page.Facts.Count, (l, i) => l.Set(null, page.Facts[i].Label, page.Facts[i].Value));
            Block(cellsBlock, cellsHeading, page.CellsHeading, page.Cells.Count);
            Fill(_cells, cellTemplate, page.Cells.Count, (l, i) => l.Set(page.Cells[i].Icon, page.Cells[i].Title, page.Cells[i].Lines));
            Block(rowsBlock, rowsHeading, page.RowsHeading, page.Rows.Count);
            Fill(_rows, rowTemplate, page.Rows.Count, (l, i) => l.Set(page.Rows[i].Icon, page.Rows[i].Title, page.Rows[i].Line));

            if (scroll) scroll.verticalNormalizedPosition = 1f;
            _reveal = 0f;
            Reveal(0f);
        }

        void Update()
        {
            if (_reveal >= 1f) return;
            _reveal = Mathf.Min(1f, _reveal + Time.unscaledDeltaTime / revealSeconds);
            Reveal(_reveal);
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
