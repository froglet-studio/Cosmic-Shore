using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Core;

namespace CosmicShore
{
    /// <summary>
    /// Displays 4 vertical fill bars with element label icons, covering levels -5 to +15.
    ///
    /// Two modes:
    /// 1. Pre-placed: assign bars[] in the prefab for full editor control of layout/scale.
    /// 2. Auto-populate: assign a pipsConfig (ElementPipsConfigSO) and a container RectTransform.
    ///    Build() creates fill bars + label icons from the config sprites. You control overall
    ///    scale/position by adjusting the container's RectTransform in the editor.
    ///
    /// If bars[] has valid entries, pre-placed mode wins. Otherwise auto-populate kicks in.
    /// </summary>
    public class ElementalBarsView : MonoBehaviour
    {
        [Serializable]
        public struct ElementBarBinding
        {
            [Tooltip("The element this bar represents")]
            public Element element;

            [Tooltip("Fill image (Image.Type = Filled, Vertical, Bottom origin)")]
            public Image fillImage;

            [Tooltip("Label/icon image below the bar")]
            public Image labelIcon;

            [Tooltip("Normal sprite for the label (restored after drift)")]
            public Sprite normalLabelSprite;
        }

        [Header("Pre-placed Bindings (optional — takes priority)")]
        [SerializeField] private ElementBarBinding[] bars = new ElementBarBinding[0];

        [Header("Auto-populate from Pips Config")]
        [Tooltip("Assign the existing ElementPipsConfigSO to auto-create bars at runtime")]
        [SerializeField] private ElementPipsConfigSO pipsConfig;

        [Tooltip("Parent RectTransform for auto-generated bars. Scale this to control overall size.")]
        [SerializeField] private RectTransform container;

        [Header("Auto-populate Layout")]
        [SerializeField] private float columnSpacing = 32f;
        [SerializeField] private Vector2 barSize = new(18f, 120f);
        [SerializeField] private Vector2 labelIconSize = new(28f, 28f);
        [SerializeField] private float labelGap = 6f;
        [SerializeField] private Color barBackgroundColor = new(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color zeroLineColor = new(1f, 1f, 1f, 0.5f);
        [SerializeField] private float zeroLineHeight = 2f;

        [Header("Range")]
        [SerializeField] private int minLevel = -5;
        [SerializeField] private int maxLevel = 15;

        [Header("Colors")]
        [SerializeField] private Color positiveFillColor = Color.white;
        [SerializeField] private Color negativeFillColor = new(1f, 0.3f, 0.3f, 0.8f);

        [Header("Juice — General")]
        [SerializeField] private float iconPunchDuration = 0.25f;
        [SerializeField] private float iconPunchScale = 1.4f;
        [SerializeField] private float colorTweenDuration = 0.35f;

        [Header("Juice — Joust")]
        [SerializeField] private Color joustFlashColor = Color.red;

        [Header("Juice — Drift")]
        [SerializeField] private float driftRotationAngle = 15f;
        [SerializeField] private float driftRotationDuration = 0.2f;
        [SerializeField] private Sprite doubleDriftSprite;

        // Runtime state
        private RectTransform _rootRT;
        private int[] _currentLevels;
        private Color[] _originalLabelColors;
        private Vector3[] _originalLabelScales;
        private Tween[] _driftRotationTweens;
        private Tween[] _labelScaleTweens;
        private Tween[] _labelColorTweens;
        private Tween[] _fillColorTweens;
        private bool _built;

        static readonly Element[] DefaultOrder = { Element.Charge, Element.Mass, Element.Space, Element.Time };

        public void Build()
        {
            if (_built) return;

            // Decide mode: pre-placed bindings or auto-populate
            bool hasPrePlaced = bars is { Length: > 0 } && bars[0].fillImage != null;

            if (!hasPrePlaced && pipsConfig && container)
                AutoPopulateFromPipsConfig();

            if (bars == null || bars.Length == 0) return;

            // Cache the root transform for runtime scaling
            _rootRT = container ? container : (RectTransform)transform;

            int count = bars.Length;
            _currentLevels = new int[count];
            _originalLabelColors = new Color[count];
            _originalLabelScales = new Vector3[count];
            _driftRotationTweens = new Tween[count];
            _labelScaleTweens = new Tween[count];
            _labelColorTweens = new Tween[count];
            _fillColorTweens = new Tween[count];

            for (int i = 0; i < count; i++)
            {
                ref var bar = ref bars[i];

                if (bar.labelIcon)
                {
                    _originalLabelColors[i] = bar.labelIcon.color;
                    _originalLabelScales[i] = bar.labelIcon.rectTransform.localScale;

                    if (!bar.normalLabelSprite)
                        bar.normalLabelSprite = bar.labelIcon.sprite;
                }

                if (bar.fillImage)
                {
                    bar.fillImage.type = Image.Type.Filled;
                    bar.fillImage.fillMethod = Image.FillMethod.Vertical;
                    bar.fillImage.fillOrigin = (int)Image.OriginVertical.Bottom;
                }

                _currentLevels[i] = 0;
            }

            _built = true;
            RefreshAllBars();
        }

        // ---------------------------------------------------------------
        // Runtime scale control
        // ---------------------------------------------------------------

        /// <summary>Uniform scale (1 = default). Scales the container or this transform.</summary>
        public void SetScale(float uniformScale)
        {
            if (_rootRT) _rootRT.localScale = Vector3.one * uniformScale;
        }

        /// <summary>Non-uniform scale for independent X/Y control.</summary>
        public void SetScale(Vector3 scale)
        {
            if (_rootRT) _rootRT.localScale = scale;
        }

        /// <summary>Current local scale of the bars root.</summary>
        public Vector3 Scale => _rootRT ? _rootRT.localScale : Vector3.one;

        // ---------------------------------------------------------------
        // Auto-populate: creates bars from ElementPipsConfigSO sprites
        // Scale/position by adjusting the container RectTransform in editor
        // ---------------------------------------------------------------
        void AutoPopulateFromPipsConfig()
        {
            int cols = DefaultOrder.Length;
            bars = new ElementBarBinding[cols];

            float totalWidth = (cols - 1) * columnSpacing;
            float startX = -totalWidth * 0.5f;
            float zeroFraction = (float)(-minLevel) / (maxLevel - minLevel);

            for (int c = 0; c < cols; c++)
            {
                var element = DefaultOrder[c];
                float xPos = startX + c * columnSpacing;

                // Column parent
                var colGO = new GameObject($"ElementBar_{element}", typeof(RectTransform));
                var colRT = (RectTransform)colGO.transform;
                colRT.SetParent(container, false);
                colRT.anchorMin = colRT.anchorMax = new Vector2(0.5f, 0f);
                colRT.pivot = new Vector2(0.5f, 0f);
                colRT.anchoredPosition = new Vector2(xPos, 0f);
                colRT.sizeDelta = new Vector2(barSize.x, 0f);

                // Label icon
                var labelGO = new GameObject($"Label_{element}", typeof(RectTransform), typeof(Image));
                var labelRT = (RectTransform)labelGO.transform;
                labelRT.SetParent(colRT, false);
                labelRT.anchorMin = labelRT.anchorMax = new Vector2(0.5f, 0f);
                labelRT.pivot = new Vector2(0.5f, 0f);
                labelRT.anchoredPosition = Vector2.zero;
                labelRT.sizeDelta = labelIconSize;

                var labelImg = labelGO.GetComponent<Image>();
                labelImg.sprite = pipsConfig.GetLabelSprite(element);
                labelImg.color = positiveFillColor;
                labelImg.preserveAspect = true;
                labelImg.raycastTarget = false;

                float barBaseY = labelIconSize.y + labelGap;

                // Bar background
                var bgGO = new GameObject($"BarBG_{element}", typeof(RectTransform), typeof(Image));
                var bgRT = (RectTransform)bgGO.transform;
                bgRT.SetParent(colRT, false);
                bgRT.anchorMin = bgRT.anchorMax = new Vector2(0.5f, 0f);
                bgRT.pivot = new Vector2(0.5f, 0f);
                bgRT.anchoredPosition = new Vector2(0f, barBaseY);
                bgRT.sizeDelta = barSize;
                var bgImg = bgGO.GetComponent<Image>();
                bgImg.color = barBackgroundColor;
                bgImg.raycastTarget = false;

                // Fill image
                var fillGO = new GameObject($"Fill_{element}", typeof(RectTransform), typeof(Image));
                var fillRT = (RectTransform)fillGO.transform;
                fillRT.SetParent(colRT, false);
                fillRT.anchorMin = fillRT.anchorMax = new Vector2(0.5f, 0f);
                fillRT.pivot = new Vector2(0.5f, 0f);
                fillRT.anchoredPosition = new Vector2(0f, barBaseY);
                fillRT.sizeDelta = barSize;

                var fillImg = fillGO.GetComponent<Image>();
                fillImg.sprite = pipsConfig.GetPipSprite(element);
                fillImg.color = positiveFillColor;
                fillImg.raycastTarget = false;

                // Zero-line marker
                var zeroGO = new GameObject($"ZeroLine_{element}", typeof(RectTransform), typeof(Image));
                var zeroRT = (RectTransform)zeroGO.transform;
                zeroRT.SetParent(colRT, false);
                zeroRT.anchorMin = zeroRT.anchorMax = new Vector2(0.5f, 0f);
                zeroRT.pivot = new Vector2(0.5f, 0.5f);
                float zeroY = barBaseY + zeroFraction * barSize.y;
                zeroRT.anchoredPosition = new Vector2(0f, zeroY);
                zeroRT.sizeDelta = new Vector2(barSize.x + 6f, zeroLineHeight);
                var zeroImg = zeroGO.GetComponent<Image>();
                zeroImg.color = zeroLineColor;
                zeroImg.raycastTarget = false;

                bars[c] = new ElementBarBinding
                {
                    element = element,
                    fillImage = fillImg,
                    labelIcon = labelImg,
                    normalLabelSprite = labelImg.sprite,
                };
            }
        }

        // ---------------------------------------------------------------
        // Level updates
        // ---------------------------------------------------------------
        public void SetLevel(Element element, int level)
        {
            int idx = GetBarIndex(element);
            if (idx < 0 || !_built) return;

            _currentLevels[idx] = Mathf.Clamp(level, minLevel, maxLevel);
            RefreshBar(idx);
        }

        public void RefreshAllBars()
        {
            if (!_built) return;
            for (int i = 0; i < bars.Length; i++)
                RefreshBar(i);
        }

        void RefreshBar(int idx)
        {
            int level = _currentLevels[idx];
            int range = maxLevel - minLevel;
            float fillFraction = (float)(level - minLevel) / range;

            var img = bars[idx].fillImage;
            if (!img) return;

            img.fillAmount = Mathf.Clamp01(fillFraction);
            img.color = level < 0 ? negativeFillColor : positiveFillColor;
        }

        // ---------------------------------------------------------------
        // Juice: Crystal Collection
        // ---------------------------------------------------------------
        public void JuiceCrystalCollected(Color domainColor)
        {
            if (!_built) return;
            for (int i = 0; i < bars.Length; i++)
                PunchIconWithColor(i, domainColor);
        }

        // ---------------------------------------------------------------
        // Juice: Joust
        // ---------------------------------------------------------------
        public void JuiceJoust()
        {
            if (!_built) return;
            for (int i = 0; i < bars.Length; i++)
                PunchIconWithColor(i, joustFlashColor);
        }

        // ---------------------------------------------------------------
        // Juice: Drift
        // ---------------------------------------------------------------
        public void JuiceDriftStart(bool isLeft, bool isDoubleDrift)
        {
            if (!_built) return;

            float targetAngle = isLeft ? driftRotationAngle : -driftRotationAngle;

            for (int i = 0; i < bars.Length; i++)
            {
                var label = bars[i].labelIcon;
                if (!label) continue;

                if (isDoubleDrift && doubleDriftSprite)
                    label.sprite = doubleDriftSprite;

                _driftRotationTweens[i]?.Kill();
                _driftRotationTweens[i] = label.rectTransform
                    .DOLocalRotate(new Vector3(0, 0, targetAngle), driftRotationDuration)
                    .SetEase(Ease.OutBack);

                _labelColorTweens[i]?.Kill();
                _labelColorTweens[i] = label
                    .DOColor(new Color(0.7f, 0.9f, 1f, 1f), driftRotationDuration)
                    .SetEase(Ease.OutQuad);
            }
        }

        public void JuiceDriftEnd()
        {
            if (!_built) return;

            for (int i = 0; i < bars.Length; i++)
            {
                var label = bars[i].labelIcon;
                if (!label) continue;

                if (bars[i].normalLabelSprite)
                    label.sprite = bars[i].normalLabelSprite;

                _driftRotationTweens[i]?.Kill();
                _driftRotationTweens[i] = label.rectTransform
                    .DOLocalRotate(Vector3.zero, driftRotationDuration)
                    .SetEase(Ease.OutQuad);

                _labelColorTweens[i]?.Kill();
                _labelColorTweens[i] = label
                    .DOColor(_originalLabelColors[i], colorTweenDuration)
                    .SetEase(Ease.OutQuad);
            }
        }

        // ---------------------------------------------------------------
        // Juice: Overtake penalty
        // ---------------------------------------------------------------
        public void JuiceOvertakePenalty()
        {
            if (!_built) return;

            for (int i = 0; i < bars.Length; i++)
            {
                var fillImg = bars[i].fillImage;
                if (fillImg)
                {
                    _fillColorTweens[i]?.Kill();
                    fillImg.color = Color.red;
                    _fillColorTweens[i] = fillImg
                        .DOColor(negativeFillColor, 0.5f)
                        .SetEase(Ease.OutQuad);
                }

                var label = bars[i].labelIcon;
                if (label)
                {
                    _labelScaleTweens[i]?.Kill();
                    _labelScaleTweens[i] = label.rectTransform
                        .DOShakeScale(0.4f, 0.3f, 10, 90f);
                }
            }
        }

        // ---------------------------------------------------------------
        // Internal
        // ---------------------------------------------------------------
        void PunchIconWithColor(int idx, Color flashColor)
        {
            var label = bars[idx].labelIcon;
            if (!label) return;

            var rt = label.rectTransform;
            var origScale = _originalLabelScales[idx];

            _labelScaleTweens[idx]?.Kill();
            rt.localScale = origScale;
            _labelScaleTweens[idx] = rt
                .DOScale(origScale * iconPunchScale, iconPunchDuration * 0.3f)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    _labelScaleTweens[idx] = rt
                        .DOScale(origScale, iconPunchDuration * 0.7f)
                        .SetEase(Ease.OutBounce);
                });

            _labelColorTweens[idx]?.Kill();
            label.color = flashColor;
            _labelColorTweens[idx] = label
                .DOColor(_originalLabelColors[idx], colorTweenDuration)
                .SetEase(Ease.OutQuad);
        }

        int GetBarIndex(Element element)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                if (bars[i].element == element)
                    return i;
            }
            return -1;
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
    }
}
