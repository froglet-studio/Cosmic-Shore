using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Core;

namespace CosmicShore
{
    /// <summary>
    /// Renders 4 vertical fill bars with element label icons, covering levels -5 to +15.
    /// Each bar has a background, a fill image, and a zero-line marker.
    /// Subscribes to ResourceSystem.OnElementLevelChange to stay in sync.
    /// Provides juice methods for crystal collection, jousting, and drifting.
    /// </summary>
    public class ElementalBarsView : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private ElementalBarsConfigSO config;

        [Header("Container")]
        [Tooltip("RectTransform that holds all bar columns.")]
        [SerializeField] private RectTransform container;

        static readonly Element[] ColumnOrder = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        // Runtime references
        private Image[] _fillImages;
        private Image[] _labelImages;
        private RectTransform[] _labelTransforms;
        private int[] _currentLevels;
        private Color[] _originalLabelColors;
        private Vector3[] _originalLabelScales;
        private bool _built;

        // Drift state
        private Tween[] _driftRotationTweens;
        private Tween[] _labelScaleTweens;
        private Tween[] _labelColorTweens;
        private Tween[] _fillColorTweens;

        // Double drift sprites (optional override)
        [Header("Drift Icon Override")]
        [SerializeField] private Sprite doubleDriftSprite;

        private Sprite[] _normalLabelSprites;
        private bool _isDoubleDrifting;

        const int MinLevel = -5;
        const int MaxLevel = 15;

        public void Build()
        {
            if (_built || !config || !container) return;

            int cols = ColumnOrder.Length;
            _fillImages = new Image[cols];
            _labelImages = new Image[cols];
            _labelTransforms = new RectTransform[cols];
            _currentLevels = new int[cols];
            _originalLabelColors = new Color[cols];
            _originalLabelScales = new Vector3[cols];
            _driftRotationTweens = new Tween[cols];
            _labelScaleTweens = new Tween[cols];
            _labelColorTweens = new Tween[cols];
            _fillColorTweens = new Tween[cols];
            _normalLabelSprites = new Sprite[cols];

            float totalWidth = (cols - 1) * config.columnSpacing;
            float startX = -totalWidth * 0.5f;
            float zeroFraction = (float)(-config.minLevel) / (config.maxLevel - config.minLevel);

            for (int c = 0; c < cols; c++)
            {
                var element = ColumnOrder[c];
                float xPos = startX + c * config.columnSpacing;

                // Column parent
                var colGO = new GameObject($"ElementBar_{element}", typeof(RectTransform));
                var colRT = (RectTransform)colGO.transform;
                colRT.SetParent(container, false);
                colRT.anchorMin = colRT.anchorMax = new Vector2(0.5f, 0f);
                colRT.pivot = new Vector2(0.5f, 0f);
                colRT.anchoredPosition = new Vector2(xPos, 0f);
                colRT.sizeDelta = new Vector2(config.barSize.x, 0f);

                // Label icon at the bottom
                var labelGO = new GameObject($"Label_{element}", typeof(RectTransform), typeof(Image));
                var labelRT = (RectTransform)labelGO.transform;
                labelRT.SetParent(colRT, false);
                labelRT.anchorMin = labelRT.anchorMax = new Vector2(0.5f, 0f);
                labelRT.pivot = new Vector2(0.5f, 0f);
                labelRT.anchoredPosition = Vector2.zero;
                labelRT.sizeDelta = config.labelIconSize;

                var labelImg = labelGO.GetComponent<Image>();
                labelImg.sprite = config.GetLabelSprite(element);
                labelImg.color = config.positiveFillColor;
                labelImg.preserveAspect = true;
                labelImg.raycastTarget = false;
                _labelImages[c] = labelImg;
                _labelTransforms[c] = labelRT;
                _originalLabelColors[c] = labelImg.color;
                _originalLabelScales[c] = labelRT.localScale;
                _normalLabelSprites[c] = labelImg.sprite;

                float barBaseY = config.labelIconSize.y + config.labelGap;

                // Bar background
                var bgGO = new GameObject($"BarBG_{element}", typeof(RectTransform), typeof(Image));
                var bgRT = (RectTransform)bgGO.transform;
                bgRT.SetParent(colRT, false);
                bgRT.anchorMin = bgRT.anchorMax = new Vector2(0.5f, 0f);
                bgRT.pivot = new Vector2(0.5f, 0f);
                bgRT.anchoredPosition = new Vector2(0f, barBaseY);
                bgRT.sizeDelta = config.barSize;

                var bgImg = bgGO.GetComponent<Image>();
                bgImg.color = config.barBackgroundColor;
                bgImg.raycastTarget = false;

                // Fill image (bottom-to-top vertical fill)
                var fillGO = new GameObject($"Fill_{element}", typeof(RectTransform), typeof(Image));
                var fillRT = (RectTransform)fillGO.transform;
                fillRT.SetParent(colRT, false);
                fillRT.anchorMin = fillRT.anchorMax = new Vector2(0.5f, 0f);
                fillRT.pivot = new Vector2(0.5f, 0f);
                fillRT.anchoredPosition = new Vector2(0f, barBaseY);
                fillRT.sizeDelta = config.barSize;

                var fillImg = fillGO.GetComponent<Image>();
                fillImg.sprite = config.GetFillSprite(element);
                fillImg.type = Image.Type.Filled;
                fillImg.fillMethod = Image.FillMethod.Vertical;
                fillImg.fillOrigin = (int)Image.OriginVertical.Bottom;
                fillImg.fillAmount = zeroFraction; // Start at zero level
                fillImg.color = config.positiveFillColor;
                fillImg.raycastTarget = false;
                _fillImages[c] = fillImg;

                // Zero-line marker
                var zeroGO = new GameObject($"ZeroLine_{element}", typeof(RectTransform), typeof(Image));
                var zeroRT = (RectTransform)zeroGO.transform;
                zeroRT.SetParent(colRT, false);
                zeroRT.anchorMin = zeroRT.anchorMax = new Vector2(0.5f, 0f);
                zeroRT.pivot = new Vector2(0.5f, 0.5f);

                float zeroY = barBaseY + zeroFraction * config.barSize.y;
                zeroRT.anchoredPosition = new Vector2(0f, zeroY);
                zeroRT.sizeDelta = new Vector2(config.barSize.x + 6f, config.zeroLineHeight);

                var zeroImg = zeroGO.GetComponent<Image>();
                zeroImg.color = config.zeroLineColor;
                zeroImg.raycastTarget = false;

                _currentLevels[c] = 0;
            }

            _built = true;
            RefreshAllBars();
        }

        /// <summary>
        /// Called by controller when an element level changes.
        /// </summary>
        public void SetLevel(Element element, int level)
        {
            int col = GetColumnIndex(element);
            if (col < 0 || !_built) return;

            _currentLevels[col] = Mathf.Clamp(level, MinLevel, MaxLevel);
            RefreshBar(col);
        }

        public void RefreshAllBars()
        {
            if (!_built) return;
            for (int c = 0; c < ColumnOrder.Length; c++)
                RefreshBar(c);
        }

        void RefreshBar(int col)
        {
            int level = _currentLevels[col];
            int range = config.maxLevel - config.minLevel; // 20
            float fillFraction = (float)(level - config.minLevel) / range;

            var img = _fillImages[col];
            if (!img) return;

            img.fillAmount = Mathf.Clamp01(fillFraction);

            // Color: negative region = negativeFillColor, positive = positiveFillColor
            img.color = level < 0 ? config.negativeFillColor : config.positiveFillColor;
        }

        // ---------------------------------------------------------------
        // Juice: Crystal Collection — scale up icon + tween to domain color
        // ---------------------------------------------------------------
        public void JuiceCrystalCollected(Color domainColor)
        {
            if (!_built) return;
            for (int c = 0; c < ColumnOrder.Length; c++)
                PunchIconWithColor(c, domainColor);
        }

        // ---------------------------------------------------------------
        // Juice: Joust — scale up icon + tween to red
        // ---------------------------------------------------------------
        public void JuiceJoust()
        {
            if (!_built) return;
            for (int c = 0; c < ColumnOrder.Length; c++)
                PunchIconWithColor(c, config.joustFlashColor);
        }

        // ---------------------------------------------------------------
        // Juice: Drift — rotate icons left/right, color change
        // ---------------------------------------------------------------
        public void JuiceDriftStart(bool isLeft, bool isDoubleDrift)
        {
            if (!_built) return;

            _isDoubleDrifting = isDoubleDrift;
            float targetAngle = isLeft ? config.driftRotationAngle : -config.driftRotationAngle;

            for (int c = 0; c < ColumnOrder.Length; c++)
            {
                // Swap to double drift sprite if available
                if (isDoubleDrift && doubleDriftSprite)
                    _labelImages[c].sprite = doubleDriftSprite;

                // Rotate icon
                _driftRotationTweens[c]?.Kill();
                _driftRotationTweens[c] = _labelTransforms[c]
                    .DOLocalRotate(new Vector3(0, 0, targetAngle), config.driftRotationDuration)
                    .SetEase(Ease.OutBack);

                // Subtle color shift
                _labelColorTweens[c]?.Kill();
                _labelColorTweens[c] = _labelImages[c]
                    .DOColor(new Color(0.7f, 0.9f, 1f, 1f), config.driftRotationDuration)
                    .SetEase(Ease.OutQuad);
            }
        }

        public void JuiceDriftEnd()
        {
            if (!_built) return;

            for (int c = 0; c < ColumnOrder.Length; c++)
            {
                // Restore normal sprite
                _labelImages[c].sprite = _normalLabelSprites[c];

                // Rotate back
                _driftRotationTweens[c]?.Kill();
                _driftRotationTweens[c] = _labelTransforms[c]
                    .DOLocalRotate(Vector3.zero, config.driftRotationDuration)
                    .SetEase(Ease.OutQuad);

                // Color back
                _labelColorTweens[c]?.Kill();
                _labelColorTweens[c] = _labelImages[c]
                    .DOColor(_originalLabelColors[c], config.colorTweenDuration)
                    .SetEase(Ease.OutQuad);
            }

            _isDoubleDrifting = false;
        }

        // ---------------------------------------------------------------
        // Juice: Overtake penalty — flash red across all bars
        // ---------------------------------------------------------------
        public void JuiceOvertakePenalty()
        {
            if (!_built) return;

            for (int c = 0; c < ColumnOrder.Length; c++)
            {
                // Quick red flash on fill bars
                _fillColorTweens[c]?.Kill();
                var fillImg = _fillImages[c];
                fillImg.color = Color.red;
                _fillColorTweens[c] = fillImg
                    .DOColor(config.negativeFillColor, 0.5f)
                    .SetEase(Ease.OutQuad);

                // Shake the icons
                _labelScaleTweens[c]?.Kill();
                _labelScaleTweens[c] = _labelTransforms[c]
                    .DOShakeScale(0.4f, 0.3f, 10, 90f)
                    .OnComplete(() => { });
            }
        }

        // ---------------------------------------------------------------
        // Internal juice helpers
        // ---------------------------------------------------------------
        void PunchIconWithColor(int col, Color flashColor)
        {
            var labelRT = _labelTransforms[col];
            var labelImg = _labelImages[col];

            // Scale punch
            _labelScaleTweens[col]?.Kill();
            labelRT.localScale = _originalLabelScales[col];
            _labelScaleTweens[col] = labelRT
                .DOScale(_originalLabelScales[col] * config.iconPunchScale, config.iconPunchDuration * 0.3f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    _labelScaleTweens[col] = labelRT
                        .DOScale(_originalLabelScales[col], config.iconPunchDuration * 0.7f)
                        .SetEase(Ease.OutBounce);
                });

            // Color flash
            _labelColorTweens[col]?.Kill();
            labelImg.color = flashColor;
            _labelColorTweens[col] = labelImg
                .DOColor(_originalLabelColors[col], config.colorTweenDuration)
                .SetEase(Ease.OutQuad);
        }

        void OnDestroy()
        {
            if (_driftRotationTweens != null)
                foreach (var t in _driftRotationTweens) t?.Kill();
            if (_labelScaleTweens != null)
                foreach (var t in _labelScaleTweens) t?.Kill();
            if (_labelColorTweens != null)
                foreach (var t in _labelColorTweens) t?.Kill();
            if (_fillColorTweens != null)
                foreach (var t in _fillColorTweens) t?.Kill();
        }

        static int GetColumnIndex(Element element) => element switch
        {
            Element.Charge => 0,
            Element.Mass   => 1,
            Element.Space  => 2,
            Element.Time   => 3,
            _              => -1
        };
    }
}
