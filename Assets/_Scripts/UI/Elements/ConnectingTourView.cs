using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The tour on the loading screen into a game (Rev 91 F, Docs/MENU_REDESIGN_B.md section 173): the parts of the
    /// game shown one chapter at a time while the arena builds - a picture, a counter, a title and one line - with an
    /// index of every chapter's name and the current one lit.
    ///
    /// <para>The chapter follows the LOAD, never a clock: it is the share of the bar the load has reached
    /// (<see cref="ChapterFor"/>), so a slow build holds its chapter and a fast one moves through them. The last
    /// chapter holds until the gate opens. Only the cross-fade between two chapters is timed.</para>
    ///
    /// <para>A view only: <see cref="ConnectingGateView"/> feeds it the bar's value. Every word is authored here -
    /// each chapter's title and line and the counter's template. An empty field shows nothing.</para>
    /// </summary>
    public sealed class ConnectingTourView : MonoBehaviour
    {
        [Serializable]
        public sealed class Chapter
        {
            [Tooltip("The chapter's name: its title, and its word in the index.")]
            public string title;
            [Tooltip("The one line under the title.")]
            [TextArea] public string line;
            [Tooltip("The chapter's picture.")]
            public CanvasGroup show;
            [Tooltip("The chapter's words: counter, title and line.")]
            public CanvasGroup words;
            public TMP_Text counterText;
            public TMP_Text titleText;
            public TMP_Text lineText;
            [Tooltip("The chapter's word in the index.")]
            public TMP_Text indexText;
            [Tooltip("The rule under the index word, drawn while this chapter shows.")]
            public Graphic indexRule;
        }

        [Header("Chapters")]
        [Tooltip("The tour, in order. Each is shown for an equal share of the load.")]
        [SerializeField] Chapter[] chapters = Array.Empty<Chapter>();
        [Tooltip("The small counter over a title. {n} is the chapter's number and {total} how many there are.")]
        [SerializeField] string counterTemplate = "{n} of {total}";

        [Header("Look")]
        [SerializeField] UIColorToken indexOnToken = UIColorToken.TextLight;
        [SerializeField] UIColorToken indexOffToken = UIColorToken.NeutralLightest;

        [Header("Motion")]
        [Tooltip("Seconds one chapter takes to give way to the next (unscaled).")]
        [SerializeField, Min(0.01f)] float fadeSeconds = 0.4f;

        int _current = -1;
        bool _fading;

        /// <summary>The chapter on show, or -1 before the first.</summary>
        public int Current => _current;

        /// <summary>
        /// The chapter a load value is in: <paramref name="count"/> equal shares of 0..1, the last holding at 1.
        /// Pure, so it is testable without a scene. No chapters answers 0.
        /// </summary>
        public static int ChapterFor(float value01, int count)
        {
            if (count <= 0) return 0;
            if (float.IsNaN(value01)) return 0;
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp01(value01) * count), 0, count - 1);
        }

        /// <summary>Start a load: the words written, the first chapter on show at once.</summary>
        public void Begin()
        {
            int total = chapters.Length;
            for (int i = 0; i < total; i++)
            {
                var c = chapters[i];
                if (c == null) continue;
                SetText(c.counterText, TemplateText.Fill(counterTemplate, "n", (i + 1).ToString(), "total", total.ToString()));
                SetText(c.titleText, c.title);
                SetText(c.lineText, c.line);
                SetText(c.indexText, c.title);
            }
            _current = -1;
            Go(0);
            Snap();
        }

        /// <summary>The load has reached <paramref name="value01"/>: show its chapter. The tour only moves forward.</summary>
        public void Show(float value01)
        {
            int chapter = ChapterFor(value01, chapters.Length);
            if (chapter > _current) Go(chapter);
        }

        void Go(int chapter)
        {
            if (chapters.Length == 0) return;
            _current = Mathf.Clamp(chapter, 0, chapters.Length - 1);
            _fading = true;
            var on = UITheme.Resolve(null, indexOnToken);
            var off = UITheme.Resolve(null, indexOffToken);
            for (int i = 0; i < chapters.Length; i++)
            {
                var c = chapters[i];
                if (c == null) continue;
                bool lit = i == _current;
                if (c.indexText) c.indexText.color = lit ? on : off;
                if (c.indexRule)
                {
                    c.indexRule.color = on;
                    c.indexRule.enabled = lit;
                }
            }
        }

        // Every chapter at its resting alpha, with no fade.
        void Snap()
        {
            for (int i = 0; i < chapters.Length; i++)
                SetAlpha(chapters[i], i == _current ? 1f : 0f);
            _fading = false;
        }

        void Update()
        {
            if (!_fading) return;
            float step = Time.unscaledDeltaTime / fadeSeconds;
            bool moving = false;
            for (int i = 0; i < chapters.Length; i++)
            {
                var c = chapters[i];
                if (c == null) continue;
                float target = i == _current ? 1f : 0f;
                float now = c.show ? c.show.alpha : c.words ? c.words.alpha : target;
                float next = Mathf.MoveTowards(now, target, step);
                SetAlpha(c, next);
                if (!Mathf.Approximately(next, target)) moving = true;
            }
            _fading = moving;
        }

        static void SetAlpha(Chapter c, float alpha)
        {
            if (c == null) return;
            if (c.show) c.show.alpha = alpha;
            if (c.words) c.words.alpha = alpha;
        }

        static void SetText(TMP_Text label, string text)
        {
            if (!label) return;
            label.text = text ?? string.Empty;
        }
    }
}
