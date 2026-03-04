using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Core;

namespace CosmicShore
{
    /// <summary>
    /// Displays 4 vertical fill bars with element label icons, covering levels -5 to +15.
    /// Assign bars[] in the prefab/scene for full editor control of layout, scale, and positioning.
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

        [Header("Bar Bindings")]
        [SerializeField] private ElementBarBinding[] bars = new ElementBarBinding[0];

        [Header("Range")]
        [SerializeField] private int minLevel = -5;
        [SerializeField] private int maxLevel = 15;

        [Header("Colors")]
        [Tooltip("Fill color when an element is debuffed (level decreasing)")]
        [SerializeField] private Color debuffFillColor = Color.white;
        [Tooltip("Fill color when level is below zero")]
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
        private Tween _scaleTween;
        private int[] _currentLevels;
        private Color[] _barFillColors;
        private Color[] _originalLabelColors;
        private Vector3[] _originalLabelScales;
        private Tween[] _driftRotationTweens;
        private Tween[] _labelScaleTweens;
        private Tween[] _labelColorTweens;
        private Tween[] _fillColorTweens;
        private bool _built;

        public void Build()
        {
            if (_built) return;
            if (bars == null || bars.Length == 0) return;

            _rootRT = (RectTransform)transform;

            int count = bars.Length;
            _currentLevels = new int[count];
            _barFillColors = new Color[count];
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
                _barFillColors[i] = debuffFillColor;
            }

            _built = true;
            RefreshAllBars();
        }

        // ---------------------------------------------------------------
        // Runtime scale control
        // ---------------------------------------------------------------

        /// <summary>Snap to uniform scale.</summary>
        public void SetScale(float uniformScale)
        {
            _scaleTween?.Kill();
            if (_rootRT) _rootRT.localScale = Vector3.one * uniformScale;
        }

        /// <summary>Snap to non-uniform scale.</summary>
        public void SetScale(Vector3 scale)
        {
            _scaleTween?.Kill();
            if (_rootRT) _rootRT.localScale = scale;
        }

        /// <summary>Animate to uniform scale over duration.</summary>
        public void AnimateScale(float targetScale, float duration = 0.3f, Ease ease = Ease.OutBack)
        {
            if (!_rootRT) return;
            _scaleTween?.Kill();
            _scaleTween = _rootRT
                .DOScale(Vector3.one * targetScale, duration)
                .SetEase(ease);
        }

        /// <summary>Animate to non-uniform scale over duration.</summary>
        public void AnimateScale(Vector3 targetScale, float duration = 0.3f, Ease ease = Ease.OutBack)
        {
            if (!_rootRT) return;
            _scaleTween?.Kill();
            _scaleTween = _rootRT
                .DOScale(targetScale, duration)
                .SetEase(ease);
        }

        /// <summary>Current local scale of the bars root.</summary>
        public Vector3 Scale => _rootRT ? _rootRT.localScale : Vector3.one;

        // ---------------------------------------------------------------
        // Level updates
        // ---------------------------------------------------------------

        /// <summary>
        /// Set the level for an element. Pass a domainColor so the fill
        /// shows the domain color when buffing (increasing) and white when debuffing (decreasing).
        /// </summary>
        public void SetLevel(Element element, int level, Color domainColor)
        {
            int idx = GetBarIndex(element);
            if (idx < 0 || !_built) return;

            int clamped = Mathf.Clamp(level, minLevel, maxLevel);
            int prev = _currentLevels[idx];
            _currentLevels[idx] = clamped;

            // Buff = increasing, debuff = decreasing
            _barFillColors[idx] = clamped > prev ? domainColor : debuffFillColor;

            RefreshBar(idx);
        }

        /// <summary>Overload without domain color — uses white for both directions.</summary>
        public void SetLevel(Element element, int level)
        {
            SetLevel(element, level, debuffFillColor);
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
            img.color = level < 0 ? negativeFillColor : _barFillColors[idx];
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
            _scaleTween?.Kill();
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
