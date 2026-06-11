using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Core;
using CosmicShore.Game.IO;

namespace CosmicShore
{
    /// <summary>
    /// Displays 4 element columns, each with 15 discrete pip images.
    /// Fill pips start disabled. On Build(), first zeroLineIndex (5) are enabled as level-0 baseline.
    /// Buffs enable pips upward with staggered scale punch. Debuffs disable pips with shake + haptics.
    /// </summary>
    public class ElementalBarsView : MonoBehaviour
    {
        [Serializable]
        public struct ElementBarBinding
        {
            [Tooltip("The element this bar represents")]
            public Element element;

            [Tooltip("Background pip images (bottom to top, 15 total)")]
            public Image[] bgPips;

            [Tooltip("Fill pip images (bottom to top, 15 total)")]
            public Image[] fillPips;

            [Tooltip("Label/icon image below the bar")]
            public Image labelIcon;

            [Tooltip("Normal sprite for the label (restored after drift)")]
            public Sprite normalLabelSprite;
        }

        [Header("Bar Bindings")]
        [SerializeField] private ElementBarBinding[] bars = new ElementBarBinding[0];

        [Header("Range")]
        [Tooltip("Pip index that represents level 0 (e.g. 5 means first 5 pips are negative territory)")]
        [SerializeField] private int zeroLineIndex = 5;

        [Header("Colors")]
        [SerializeField] private Color filledColor = Color.white;
        [Tooltip("Fill color when level is below zero")]
        [SerializeField] private Color negativeFillColor = new(1f, 0.3f, 0.3f, 0.8f);
        [Tooltip("Color flash on debuff pips before they disappear")]
        [SerializeField] private Color debuffFlashColor = new(1f, 0.2f, 0.2f, 1f);

        [Header("Juice — Pip Transitions")]
        [Tooltip("Scale multiplier when a pip appears (buff)")]
        [SerializeField] private float buffPopScale = 1.5f;
        [Tooltip("Duration of the pop-in tween per pip")]
        [SerializeField] private float buffPopDuration = 0.18f;
        [Tooltip("Stagger delay between each pip appearing")]
        [SerializeField] private float buffStaggerDelay = 0.04f;
        [Tooltip("Duration of the shake-out tween per pip on debuff")]
        [SerializeField] private float debuffShakeDuration = 0.15f;
        [Tooltip("Shake strength on debuff pip removal")]
        [SerializeField] private float debuffShakeStrength = 8f;

        [Header("Juice — Haptics")]
        [Tooltip("Fire haptic on debuff (element level decrease)")]
        [SerializeField] private bool hapticOnDebuff = true;
        [Tooltip("Haptic intensity for debuff (0-1)")]
        [SerializeField] private float debuffHapticAmplitude = 0.6f;
        [Tooltip("Haptic frequency for debuff")]
        [SerializeField] private float debuffHapticFrequency = 0.5f;
        [Tooltip("Haptic duration for debuff")]
        [SerializeField] private float debuffHapticDuration = 0.15f;

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
        private Color[] _barDomainColors;
        private Color[] _originalLabelColors;
        private Vector3[] _originalLabelScales;
        private Tween[] _driftRotationTweens;
        private Tween[] _labelScaleTweens;
        private Tween[] _labelColorTweens;
        private Tween[][] _pipTweens; // [barIndex][pipIndex]
        private bool _built;
        private bool _overtakeActive;

        public bool IsBuilt => _built;

        void Start()
        {
            // Self-initialize so the baseline pips show even if the controller
            // chain hasn't called Build() yet (e.g. elementBars not wired on SilhouetteController).
            Build();
        }

        public void Build()
        {
            if (_built) return;
            if (bars == null || bars.Length == 0) return;

            _rootRT = (RectTransform)transform;

            int count = bars.Length;
            _currentLevels = new int[count];
            _barDomainColors = new Color[count];
            _originalLabelColors = new Color[count];
            _originalLabelScales = new Vector3[count];
            _driftRotationTweens = new Tween[count];
            _labelScaleTweens = new Tween[count];
            _labelColorTweens = new Tween[count];
            _pipTweens = new Tween[count][];

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

                int pipCount = bar.fillPips != null ? bar.fillPips.Length : 0;
                _pipTweens[i] = new Tween[pipCount];

                // Fill pips: first zeroLineIndex enabled (level 0 baseline), rest disabled
                if (bar.fillPips != null)
                {
                    for (int p = 0; p < bar.fillPips.Length; p++)
                    {
                        var pip = bar.fillPips[p];
                        if (!pip) continue;
                        pip.gameObject.SetActive(p < zeroLineIndex);
                        pip.color = filledColor;
                        pip.rectTransform.localScale = Vector3.one;
                    }
                }

                _currentLevels[i] = 0;
                _barDomainColors[i] = filledColor;
            }

            _built = true;
            RefreshAllBars();
        }

        // ---------------------------------------------------------------
        // Runtime scale control
        // ---------------------------------------------------------------

        public void SetScale(float uniformScale)
        {
            _scaleTween?.Kill();
            if (_rootRT) _rootRT.localScale = Vector3.one * uniformScale;
        }

        public void SetScale(Vector3 scale)
        {
            _scaleTween?.Kill();
            if (_rootRT) _rootRT.localScale = scale;
        }

        public void AnimateScale(float targetScale, float duration = 0.3f, Ease ease = Ease.OutBack)
        {
            if (!_rootRT) return;
            _scaleTween?.Kill();
            _scaleTween = _rootRT
                .DOScale(Vector3.one * targetScale, duration)
                .SetEase(ease);
        }

        public void AnimateScale(Vector3 targetScale, float duration = 0.3f, Ease ease = Ease.OutBack)
        {
            if (!_rootRT) return;
            _scaleTween?.Kill();
            _scaleTween = _rootRT
                .DOScale(targetScale, duration)
                .SetEase(ease);
        }

        public Vector3 Scale => _rootRT ? _rootRT.localScale : Vector3.one;

        // ---------------------------------------------------------------
        // Level updates
        // ---------------------------------------------------------------

        /// <summary>
        /// Set the level for an element. Level is floored at 0 (baseline 5 pips always stay on).
        /// Only overtake penalty can push below zero.
        /// </summary>
        public void SetLevel(Element element, int level, Color domainColor)
        {
            int idx = GetBarIndex(element);
            if (idx < 0 || !_built) return;

            // Floor at 0 unless overtake is active — baseline pips never decrease from normal gameplay
            int clamped = _overtakeActive ? level : Mathf.Max(0, level);

            _barDomainColors[idx] = domainColor;
            int prev = _currentLevels[idx];
            _currentLevels[idx] = clamped;

            RefreshBar(idx, prev);
        }

        public void SetLevel(Element element, int level)
        {
            SetLevel(element, level, filledColor);
        }

        /// <summary>
        /// Enter overtake mode — allows levels to go negative (below baseline 5 pips).
        /// Call EndOvertake() when recovery is complete.
        /// </summary>
        public void BeginOvertake()
        {
            _overtakeActive = true;
        }

        /// <summary>
        /// Exit overtake mode — levels are floored at 0 again.
        /// </summary>
        public void EndOvertake()
        {
            _overtakeActive = false;
        }

        public void RefreshAllBars()
        {
            if (!_built) return;
            for (int i = 0; i < bars.Length; i++)
                RefreshBar(i, _currentLevels[i]);
        }

        void RefreshBar(int idx, int previousLevel)
        {
            int level = _currentLevels[idx];
            ref var bar = ref bars[idx];
            if (bar.fillPips == null) return;

            int pipCount = bar.fillPips.Length;
            int enabledCount = Mathf.Clamp(level + zeroLineIndex, 0, pipCount);
            int prevEnabledCount = Mathf.Clamp(previousLevel + zeroLineIndex, 0, pipCount);
            bool isNegative = level < 0;
            bool isIncreasing = level > previousLevel;
            bool isDecreasing = level < previousLevel;

            // Haptic on debuff
            if (isDecreasing && hapticOnDebuff)
            {
                HapticController.PlayConstant(debuffHapticAmplitude, debuffHapticFrequency, debuffHapticDuration);
            }

            int newPipIndex = 0; // counter for stagger delay on buff

            for (int p = 0; p < pipCount; p++)
            {
                var pip = bar.fillPips[p];
                if (!pip) continue;

                bool shouldBeOn = p < enabledCount;
                bool wasOn = p < prevEnabledCount;

                _pipTweens[idx][p]?.Kill();

                if (shouldBeOn)
                {
                    pip.gameObject.SetActive(true);
                    pip.rectTransform.localScale = Vector3.one;

                    // Color: negative territory pips get negativeFillColor
                    pip.color = (p < zeroLineIndex && isNegative)
                        ? negativeFillColor
                        : _barDomainColors[idx];

                    // Staggered pop-in for newly enabled pips (buff)
                    if (isIncreasing && !wasOn)
                    {
                        var rt = pip.rectTransform;
                        rt.localScale = Vector3.zero;
                        float delay = newPipIndex * buffStaggerDelay;
                        _pipTweens[idx][p] = rt
                            .DOScale(Vector3.one * buffPopScale, buffPopDuration * 0.4f)
                            .SetDelay(delay)
                            .SetEase(Ease.OutQuad)
                            .OnComplete(() =>
                            {
                                rt.DOScale(Vector3.one, buffPopDuration * 0.6f)
                                    .SetEase(Ease.OutBounce);
                            });
                        newPipIndex++;
                    }
                }
                else
                {
                    // Shake + flash + shrink-out for newly disabled pips (debuff)
                    if (isDecreasing && wasOn)
                    {
                        var rt = pip.rectTransform;
                        pip.color = debuffFlashColor;

                        // Shake then shrink to zero, then deactivate
                        _pipTweens[idx][p] = rt
                            .DOShakePosition(debuffShakeDuration, debuffShakeStrength, 20, 90f, false, false)
                            .OnComplete(() =>
                            {
                                rt.DOScale(Vector3.zero, 0.08f)
                                    .SetEase(Ease.InBack)
                                    .OnComplete(() =>
                                    {
                                        pip.gameObject.SetActive(false);
                                        rt.localScale = Vector3.one;
                                    });
                            });
                    }
                    else
                    {
                        pip.gameObject.SetActive(false);
                        pip.rectTransform.localScale = Vector3.one;
                    }
                }
            }
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

            // Haptic burst for overtake
            if (hapticOnDebuff)
                HapticController.PlayConstant(0.8f, 0.7f, 0.25f);

            for (int i = 0; i < bars.Length; i++)
            {
                // Flash + shake all active fill pips
                ref var bar = ref bars[i];
                if (bar.fillPips != null)
                {
                    int enabledCount = Mathf.Clamp(_currentLevels[i] + zeroLineIndex, 0, bar.fillPips.Length);
                    for (int p = 0; p < enabledCount; p++)
                    {
                        var pip = bar.fillPips[p];
                        if (!pip || !pip.gameObject.activeSelf) continue;

                        _pipTweens[i][p]?.Kill();
                        pip.color = Color.red;
                        var origColor = _barDomainColors[i];

                        var rt = pip.rectTransform;
                        // Shake then color-recover
                        _pipTweens[i][p] = DOTween.Sequence()
                            .Append(rt.DOShakePosition(0.3f, 6f, 15, 90f, false, false))
                            .Join(pip.DOColor(origColor, 0.5f).SetEase(Ease.OutQuad));
                    }
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
            if (_pipTweens != null)
                foreach (var row in _pipTweens)
                    if (row != null)
                        foreach (var t in row) t?.Kill();
        }
    }
}
