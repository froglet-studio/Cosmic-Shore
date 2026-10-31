using System;
using System.Collections;
using System.Globalization;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Rev 33 "cell gate" loading screen's look (Docs/MENU_REDESIGN_B.md section 99, board A): the cell-change
    /// screen grown into the mode entry. It COVERS the arena - the sea over a solid base, both cut by a
    /// <see cref="HexApertureGraphic"/> - so nothing renders the world while it builds. The Omni crystal fills as the
    /// arena builds, the four real build steps tick, the mode's brief sits under ENTERING, and at the end the crystal
    /// flashes and its hex opens into the arena: the hole starts as the crystal, glides to the centre and grows past
    /// the screen while the intro's bars come in.
    ///
    /// <para>A view only: <see cref="ConnectingPanelController"/> owns the load and feeds <see cref="Tick"/> the
    /// build's state each frame. Every word is authored - the steps, ENTERING, LOADING and TIP come from the
    /// cell-change screen's own content asset, so the two screens can never disagree.</para>
    /// </summary>
    public sealed class ConnectingGateView : MonoBehaviour
    {
        [Serializable]
        public sealed class StepRow
        {
            [Tooltip("The row's group: pending rows are dimmed, the running one is full.")]
            public CanvasGroup group;
            [Tooltip("The little hex that fills while its step runs.")]
            public LoadCrystalGraphic tick;
            [Tooltip("The mark drawn on the filled hex once its step is done.")]
            public Graphic check;
            public TMP_Text number;
            public TMP_Text label;
            [Tooltip("A filled Image under the row that grows across while the step runs.")]
            public Image underline;
        }

        [Header("Words")]
        [Tooltip("The cell-change screen's words: ENTERING, LOADING, TIP and the four build steps.")]
        [SerializeField] EnvironmentLoadVeilContentSO veilWords;
        [Tooltip("The intensity chip's label.")]
        [SerializeField] string intensityWord = "Intensity";
        [Tooltip("The steps panel's count. {done} and {total} are filled. Empty shows nothing.")]
        [SerializeField] string prismsTemplate = "{done} / {total} prisms";
        [Tooltip("The bar's percentage. {pct} is filled.")]
        [SerializeField] string percentTemplate = "{pct}%";

        [Header("Backdrop")]
        [Tooltip("The cover: the solid base and the sea over it. The hole is opened in all of them together.")]
        [SerializeField] HexApertureGraphic[] cover = Array.Empty<HexApertureGraphic>();

        [Header("Crystal")]
        [Tooltip("The crystal's rect; its centre is where the gate opens.")]
        [SerializeField] RectTransform crystal;
        [SerializeField] CanvasGroup crystalGroup;
        [Tooltip("The hex: glass, spokes and outline.")]
        [SerializeField] LoadCrystalGraphic crystalHex;
        [Tooltip("The Omni crystal, faint: what is still to build.")]
        [SerializeField] Image crystalDim;
        [Tooltip("The Omni crystal, filled from the bottom (Image Filled, Vertical, origin Bottom): what is built.")]
        [SerializeField] Image crystalBuilt;
        [Tooltip("The bright line at the fill's top.")]
        [SerializeField] RectTransform levelLine;
        [Tooltip("A hex of glass the same size, flashed white as the gate opens.")]
        [SerializeField] LoadCrystalGraphic crystalFlash;
        [Tooltip("The hole's edge while the gate opens (an outline-only hex, under the same parent as the cover).")]
        [SerializeField] LoadCrystalGraphic gateRing;

        [Header("Brief")]
        [SerializeField] TMP_Text enteringText;
        [SerializeField] TMP_Text taglineText;
        [SerializeField] GameObject objectiveChip;
        [SerializeField] TMP_Text objectiveText;
        [SerializeField] GameObject intensityChip;
        [SerializeField] TMP_Text intensityLabel;
        [SerializeField] TMP_Text intensityValue;
        [SerializeField] Image[] intensityPips = Array.Empty<Image>();

        [Header("Steps")]
        [SerializeField] StepRow[] steps = Array.Empty<StepRow>();
        [SerializeField] TMP_Text prismsText;

        [Header("Footer")]
        [SerializeField] GameObject tipRow;
        [SerializeField] TMP_Text tipLabel;
        [SerializeField] TMP_Text tipText;
        [SerializeField] TMP_Text loadingLabel;
        [SerializeField] TMP_Text percentText;

        [Header("Look")]
        [SerializeField] UIColorToken stepActiveToken = UIColorToken.TextLight;
        [SerializeField] UIColorToken stepIdleToken = UIColorToken.NeutralLightest;
        [Tooltip("A step tick's fill.")]
        [SerializeField] UIColorToken stepFillToken = UIColorToken.TextLight;
        [SerializeField, Range(0f, 1f)] float stepPendingAlpha = 0.56f;
        [SerializeField, Range(0f, 1f)] float stepDoneAlpha = 0.78f;
        [SerializeField] UIColorToken pipOnToken = UIColorToken.TextLight;
        [SerializeField, Range(0f, 1f)] float pipOffAlpha = 0.25f;

        [Header("Motion")]
        [Tooltip("Everything but the backdrop and the crystal: fades out as the gate starts.")]
        [SerializeField] CanvasGroup content;
        [Tooltip("Seconds from the arena being ready to the hole having grown past the screen.")]
        [SerializeField, Min(0.3f)] float gateSeconds = 1.7f;
        [Tooltip("The hole's radius once it has glided to the centre, before it bursts.")]
        [SerializeField, Min(10f)] float gateSettleRadius = 452f;

        // Where a step's share of the crystal starts and ends (EnvironmentLoadVeil.CrystalLevel's bands).
        static readonly float[] StepBands = { 0f, 0.7f, 0.95f, 0.97f, 1f };

        int _step = -2;
        float _crystalRadius;
        Color _restOutline = Color.white;
        string _prismsShown;
        int _pctShown = -1;
        Coroutine _gate;

        /// <summary>True while the gate is opening.</summary>
        public bool IsOpening => _gate != null;

        void Awake()
        {
            if (crystalHex)
            {
                _crystalRadius = LoadCrystalGraphic.RadiusFor(crystalHex.rectTransform.rect.size);
                _restOutline = crystalHex.Outline;
            }
            if (gateRing) gateRing.enabled = false;
            if (crystalFlash) crystalFlash.Glass = Color.clear;
            var fill = UITheme.Resolve(null, stepFillToken);
            foreach (var row in steps)
                if (row?.tick)
                    row.tick.SetFill(fill);
        }

        void OnDisable()
        {
            if (_gate == null) return;
            StopCoroutine(_gate);
            _gate = null;
        }

        /// <summary>The mode's brief for this load. An empty line hides its piece.</summary>
        public void Bind(string tagline, string objective, int intensity, string tip)
        {
            if (enteringText) enteringText.text = veilWords ? veilWords.enteringWord : string.Empty;
            SetText(taglineText, tagline);
            if (objectiveChip) objectiveChip.SetActive(!string.IsNullOrEmpty(objective));
            SetText(objectiveText, objective);

            if (intensityChip) intensityChip.SetActive(intensity > 0);
            SetText(intensityLabel, intensityWord);
            SetText(intensityValue, intensity > 0 ? intensity.ToString() : string.Empty);
            var on = UITheme.Resolve(null, pipOnToken);
            var off = on;
            off.a *= pipOffAlpha;
            for (int i = 0; i < intensityPips.Length; i++)
                if (intensityPips[i]) intensityPips[i].color = i < intensity ? on : off;

            if (tipRow) tipRow.SetActive(!string.IsNullOrEmpty(tip));
            SetText(tipLabel, veilWords ? veilWords.tipLabel : string.Empty);
            SetText(tipText, tip);
            SetText(loadingLabel, veilWords ? veilWords.loadingWord : string.Empty);
            for (int i = 0; i < steps.Length; i++)
            {
                var row = steps[i];
                if (row == null) continue;
                SetText(row.number, (i + 1).ToString("00"));
                SetText(row.label, veilWords ? veilWords.StepLabel(i) : string.Empty);
            }
        }

        /// <summary>Start a load: everything at rest, the cover whole, the entrances replayed.</summary>
        public void BeginHold()
        {
            if (_gate != null)
            {
                StopCoroutine(_gate);
                _gate = null;
            }
            foreach (var c in cover)
                if (c) c.SetAperture(Vector2.zero, 0f);
            if (content) content.alpha = 1f;
            if (crystalGroup) crystalGroup.alpha = 1f;
            if (gateRing) gateRing.enabled = false;
            if (crystalFlash) crystalFlash.Glass = Color.clear;
            if (crystalHex) crystalHex.Outline = _restOutline;
            _step = -2;
            _prismsShown = null;
            _pctShown = -1;
            Tick(-1, 0f, 0f, 0, 0);
            if (content) KitEntrance.ReplayUnder(content.transform);
        }

        /// <summary>
        /// One frame of the build. <paramref name="step"/> is <see cref="EnvironmentLoadVeil.StepFor"/>'s (-1 before the
        /// build, 0 laying, 1 growing, 2 the all-clear hold, 3 released); <paramref name="level"/> is
        /// <see cref="EnvironmentLoadVeil.CrystalLevel"/>'s; <paramref name="bar"/> the eased 0-1 progress.
        /// </summary>
        public void Tick(int step, float level, float bar, int done, int total)
        {
            DrawCrystal(level);

            for (int i = 0; i < steps.Length; i++)
            {
                var row = steps[i];
                if (row == null) continue;
                bool doneStep = step > i || step == 3;
                bool active = step == i && step < 3;
                float local = doneStep ? 1f : active ? Mathf.InverseLerp(StepBands[i], StepBands[i + 1], level) : 0f;
                if (row.tick) row.tick.Level = local;
                if (row.underline) row.underline.fillAmount = active ? local : 0f;
                if (step != _step)
                {
                    if (row.check) row.check.enabled = doneStep;
                    if (row.group) row.group.alpha = active ? 1f : doneStep ? stepDoneAlpha : stepPendingAlpha;
                    if (row.number) row.number.color = UITheme.Resolve(null, active ? stepActiveToken : stepIdleToken);
                }
            }
            _step = step;

            if (prismsText)
            {
                string prisms = total > 0
                    ? TemplateText.Fill(prismsTemplate, "done", done.ToString("N0", CultureInfo.InvariantCulture),
                                      "total", total.ToString("N0", CultureInfo.InvariantCulture))
                    : string.Empty;
                if (prisms != _prismsShown)
                {
                    _prismsShown = prisms;
                    prismsText.text = prisms;
                }
            }

            int pct = Mathf.RoundToInt(Mathf.Clamp01(bar) * 100f);
            if (percentText && pct != _pctShown)
            {
                _pctShown = pct;
                percentText.text = TemplateText.Fill(percentTemplate, "pct", pct.ToString());
            }
        }

        void DrawCrystal(float level)
        {
            if (crystalHex) crystalHex.Level = 0f;   // the built crystal shows the level, not the bands
            if (_crystalRadius <= 0f && crystalHex)
                _crystalRadius = LoadCrystalGraphic.RadiusFor(crystalHex.rectTransform.rect.size);
            float r = _crystalRadius;
            float y = -r + 2f * r * Mathf.Clamp01(level);
            if (crystalBuilt)
            {
                float half = crystalBuilt.rectTransform.rect.height * 0.5f;
                crystalBuilt.fillAmount = half > 0f ? Mathf.InverseLerp(-half, half, y) : level;
            }
            if (levelLine)
            {
                bool show = level > 0.005f && level < 0.995f;
                if (levelLine.gameObject.activeSelf != show) levelLine.gameObject.SetActive(show);
                if (show)
                {
                    // The hex's width at that height: full between the side corners, narrowing to the points.
                    float a = Mathf.Abs(y);
                    float half = a <= r * 0.5f ? r * 0.8660254f : (r - a) * 1.7320508f;
                    levelLine.anchoredPosition = new Vector2(0f, y);
                    levelLine.sizeDelta = new Vector2(Mathf.Max(0f, 2f * half - 4f), levelLine.sizeDelta.y);
                }
            }
        }

        /// <summary>
        /// The arena is ready: the brief leaves, the crystal flashes, and its hex opens into the arena - the hole
        /// glides to the centre and bursts past the screen. <paramref name="done"/> runs when the screen is fully
        /// open (or at once when this view is inactive).
        /// </summary>
        public void Open(Action done)
        {
            if (!isActiveAndEnabled)
            {
                done?.Invoke();
                return;
            }
            if (_gate != null) StopCoroutine(_gate);
            _gate = StartCoroutine(Gate(done));
        }

        IEnumerator Gate(Action done)
        {
            float d = gateSeconds;
            float tExit = 0.15f * d, tFlash = 0.12f * d, tOpen = 0.19f * d, tSet = 0.65f * d;
            Vector2 start = ApertureStart();
            float t = 0f;
            var white = Color.white;
            var outline = crystalHex ? crystalHex.Outline : white;
            if (gateRing)
            {
                gateRing.enabled = false;
                gateRing.Spokes = false;
                gateRing.Glass = Color.clear;
            }

            while (t < d)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

                // The brief and the panels leave first.
                if (content) content.alpha = 1f - KitMotion.Ease(KitEase.OutCubic, Mathf.Clamp01(t / tExit));

                // The crystal charges to white and flashes, then gives way to the hole.
                if (crystalHex) crystalHex.Outline = Color.Lerp(outline, white, Mathf.Clamp01(t / tFlash));
                if (crystalFlash)
                {
                    float f = t < tFlash ? t / tFlash : 1f - Mathf.Clamp01((t - tFlash) / (tOpen * 1.6f - tFlash));
                    crystalFlash.Glass = new Color(1f, 1f, 1f, 0.85f * Mathf.Clamp01(f));
                }
                if (crystalGroup) crystalGroup.alpha = 1f - Mathf.Clamp01((t - tOpen) / (0.1f * d));

                // The hole: opens as the crystal's hex, glides to the centre while it grows, then bursts.
                Vector2 c;
                float radius;
                if (t < tOpen)
                {
                    c = start;
                    radius = 0f;
                }
                else if (t < tOpen + 0.1f * d)
                {
                    c = start;
                    radius = _crystalRadius * KitMotion.Ease(KitEase.OutQuint, (t - tOpen) / (0.1f * d));
                }
                else if (t < tSet)
                {
                    float u = KitMotion.Ease(KitEase.InOutSine, (t - tOpen - 0.1f * d) / (tSet - tOpen - 0.1f * d));
                    c = Vector2.Lerp(start, Vector2.zero, u);
                    radius = Mathf.Lerp(_crystalRadius, gateSettleRadius, u);
                }
                else
                {
                    float u = Mathf.Clamp01((t - tSet) / (d - tSet));
                    c = Vector2.zero;
                    radius = Mathf.Lerp(gateSettleRadius, CoverRadius(), u * u * u);
                }
                foreach (var g in cover)
                    if (g) g.SetAperture(c, radius);
                DrawRing(c, radius, t, tSet, d);
                yield return null;
            }

            foreach (var g in cover)
                if (g) g.SetAperture(Vector2.zero, CoverRadius());
            if (gateRing) gateRing.enabled = false;
            _gate = null;
            done?.Invoke();
        }

        void DrawRing(Vector2 centre, float radius, float t, float tSet, float d)
        {
            if (!gateRing) return;
            bool show = radius > 1f;
            if (gateRing.enabled != show) gateRing.enabled = show;
            if (!show) return;
            var rt = gateRing.rectTransform;
            rt.anchoredPosition = centre;
            float size = LoadCrystalGraphic.SizeFor(radius);
            rt.sizeDelta = new Vector2(size, size);
            float fade = t < tSet ? 1f : 1f - Mathf.Clamp01((t - tSet) / (d - tSet));
            gateRing.Outline = new Color(1f, 1f, 1f, fade);
        }

        // The crystal's centre in the cover's space (its offset from the cover's centre).
        Vector2 ApertureStart()
        {
            if (!crystal || cover.Length == 0 || !cover[0]) return Vector2.zero;
            var rt = cover[0].rectTransform;
            Vector2 local = rt.InverseTransformPoint(crystal.TransformPoint(crystal.rect.center));
            return local - rt.rect.center;
        }

        // A radius whose hex covers the whole cover rect from its centre.
        float CoverRadius()
        {
            if (cover.Length == 0 || !cover[0]) return 4000f;
            var size = cover[0].rectTransform.rect.size;
            // The hex's inscribed radius is r*sqrt(3)/2; it must reach the rect's corner.
            return size.magnitude * 0.5f / 0.8660254f + 8f;
        }

        static void SetText(TMP_Text label, string text)
        {
            if (!label) return;
            label.text = text ?? string.Empty;
        }
    }
}
