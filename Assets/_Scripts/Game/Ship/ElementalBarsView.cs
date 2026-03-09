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
    /// All pips start inactive. Buffs enable pips upward through 3 color zones:
    /// normal (first 5), domain (next 5), and super (last 5).
    /// Debuffs animate reverse deactivation with red coloring, then recover
    /// through baseline with red before swapping sprites and resuming normal fill.
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

            [Tooltip("Sprite shown when pip is inactive")]
            public Sprite inactiveSprite;

            [Tooltip("Active sprites (5 fill stages, cycled per color zone)")]
            public Sprite[] activeSprites;
        }

        [Header("Bar Bindings")]
        [SerializeField] private ElementBarBinding[] bars = new ElementBarBinding[0];

        [Header("Color Zones")]
        [Tooltip("Color for pips in the normal zone (levels 1-5, normalized 0.0-0.5)")]
        [SerializeField] private Color normalZoneColor = Color.white;
        [Tooltip("Color for pips in the super zone (levels 11-15, normalized 1.0-1.5)")]
        [SerializeField] private Color superZoneColor = new(0.3f, 0.6f, 1f, 1f);
        [Tooltip("Color applied to pips during debuff/overtake recovery")]
        [SerializeField] private Color debuffColor = new(1f, 0.2f, 0.2f, 1f);

        const int PipsPerZone = 5;

        [Header("Juice — Pip Transitions")]
        [Tooltip("Scale multiplier when a pip appears (buff)")]
        [SerializeField] private float buffPopScale = 1.5f;
        [Tooltip("Duration of the pop-in tween per pip")]
        [SerializeField] private float buffPopDuration = 0.18f;
        [Tooltip("Stagger delay between each pip appearing")]
        [SerializeField] private float buffStaggerDelay = 0.04f;
        [Tooltip("Duration of the shrink-out tween per pip on debuff")]
        [SerializeField] private float debuffShrinkDuration = 0.12f;
        [Tooltip("Stagger delay between each pip disappearing on debuff")]
        [SerializeField] private float debuffStaggerDelay = 0.03f;

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

                // All pips start inactive
                if (bar.fillPips != null)
                {
                    for (int p = 0; p < bar.fillPips.Length; p++)
                    {
                        var pip = bar.fillPips[p];
                        if (!pip) continue;
                        pip.gameObject.SetActive(false);
                        if (bar.inactiveSprite) pip.sprite = bar.inactiveSprite;
                        pip.color = Color.white;
                        pip.rectTransform.localScale = Vector3.one;
                    }
                }

                _currentLevels[i] = 0;
                _barDomainColors[i] = normalZoneColor;
            }

            _built = true;
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
        /// Set the level for an element with a domain color override.
        /// Level 0 = all pips inactive. Levels 1-15 activate pips through 3 color zones.
        /// Only overtake penalty can push below zero.
        /// </summary>
        public void SetLevel(Element element, int level, Color domainColor)
        {
            int idx = GetBarIndex(element);
            if (idx < 0 || !_built) return;

            // Floor at 0 unless overtake is active
            int clamped = _overtakeActive ? level : Mathf.Max(0, level);

            _barDomainColors[idx] = domainColor;
            int prev = _currentLevels[idx];
            _currentLevels[idx] = clamped;

            RefreshBar(idx, prev);
        }

        /// <summary>
        /// Set the level for an element, preserving the current domain color.
        /// </summary>
        public void SetLevel(Element element, int level)
        {
            int idx = GetBarIndex(element);
            if (idx < 0 || !_built) return;

            int clamped = _overtakeActive ? level : Mathf.Max(0, level);
            int prev = _currentLevels[idx];
            _currentLevels[idx] = clamped;

            RefreshBar(idx, prev);
        }

        /// <summary>
        /// Set the domain color for an element bar (used in the domain zone, pips 5-9).
        /// </summary>
        public void SetDomainColor(Element element, Color color)
        {
            int idx = GetBarIndex(element);
            if (idx < 0) return;
            _barDomainColors[idx] = color;
            if (_built) RefreshBar(idx, _currentLevels[idx]);
        }

        /// <summary>
        /// Enter overtake mode — allows levels to go negative.
        /// Call EndOvertake() when recovery is complete.
        /// </summary>
        public void BeginOvertake()
        {
            _overtakeActive = true;
        }

        /// <summary>
        /// Exit overtake mode — transitions red recovery pips out, resumes normal fill.
        /// </summary>
        public void EndOvertake()
        {
            _overtakeActive = false;

            // Deactivate any remaining recovery pips (swap sprite, clear color)
            for (int i = 0; i < bars.Length; i++)
            {
                ref var bar = ref bars[i];
                if (bar.fillPips == null) continue;

                for (int p = 0; p < bar.fillPips.Length; p++)
                {
                    var pip = bar.fillPips[p];
                    if (!pip || !pip.gameObject.activeSelf) continue;

                    _pipTweens[i][p]?.Kill();

                    if (bar.inactiveSprite) pip.sprite = bar.inactiveSprite;
                    pip.color = Color.white;
                    pip.gameObject.SetActive(false);
                    pip.rectTransform.localScale = Vector3.one;
                }
            }
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
            bool isRecovering = _overtakeActive && level <= 0;
            bool wasRecovering = _overtakeActive && previousLevel <= 0;

            // Calculate enabled pip count based on mode
            int enabledCount = CalculateEnabledCount(level, isRecovering, pipCount);
            int prevEnabledCount = CalculateEnabledCount(previousLevel, wasRecovering, pipCount);

            bool isIncreasing = enabledCount > prevEnabledCount;
            bool isDecreasing = enabledCount < prevEnabledCount;

            // Haptic on debuff
            if (isDecreasing && hapticOnDebuff)
            {
                HapticController.PlayConstant(debuffHapticAmplitude, debuffHapticFrequency, debuffHapticDuration);
            }

            // Handle decreasing pips — reverse stagger from top down
            if (isDecreasing)
            {
                int removedPipIndex = 0;
                for (int p = prevEnabledCount - 1; p >= enabledCount; p--)
                {
                    if (p < 0 || p >= pipCount) continue;
                    var pip = bar.fillPips[p];
                    if (!pip) continue;

                    _pipTweens[idx][p]?.Kill();
                    pip.color = debuffColor;

                    var rt = pip.rectTransform;
                    float delay = removedPipIndex * debuffStaggerDelay;

                    _pipTweens[idx][p] = rt
                        .DOScale(Vector3.zero, debuffShrinkDuration)
                        .SetDelay(delay)
                        .SetEase(Ease.InBack)
                        .OnComplete(() =>
                        {
                            if (bar.inactiveSprite) pip.sprite = bar.inactiveSprite;
                            pip.gameObject.SetActive(false);
                            rt.localScale = Vector3.one;
                        });
                    removedPipIndex++;
                }
            }

            // Handle increasing and steady-state pips
            int newPipIndex = 0;
            for (int p = 0; p < pipCount; p++)
            {
                var pip = bar.fillPips[p];
                if (!pip) continue;

                bool shouldBeOn = p < enabledCount;
                bool wasOn = p < prevEnabledCount;

                if (shouldBeOn)
                {
                    Color pipColor = isRecovering ? debuffColor : GetZoneColor(p, idx);

                    // Set sprite
                    if (isRecovering)
                    {
                        if (bar.inactiveSprite) pip.sprite = bar.inactiveSprite;
                    }
                    else if (bar.activeSprites is { Length: > 0 })
                    {
                        int spriteIdx = p % Mathf.Min(bar.activeSprites.Length, PipsPerZone);
                        pip.sprite = bar.activeSprites[spriteIdx];
                    }

                    if (!wasOn && isIncreasing)
                    {
                        // Newly activated pip — staggered pop-in
                        _pipTweens[idx][p]?.Kill();
                        pip.gameObject.SetActive(true);
                        pip.color = pipColor;

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
                    else if (wasOn)
                    {
                        // Already active — update color and sprite only
                        pip.gameObject.SetActive(true);
                        pip.color = pipColor;
                        pip.rectTransform.localScale = Vector3.one;
                    }
                }
                else if (!shouldBeOn && !wasOn)
                {
                    // Was off and should stay off
                    pip.gameObject.SetActive(false);
                    pip.rectTransform.localScale = Vector3.one;
                }
                // shouldBeOn=false, wasOn=true is handled by the decreasing loop above
            }
        }

        int CalculateEnabledCount(int level, bool recovering, int pipCount)
        {
            if (recovering)
            {
                // During overtake recovery: levels -5..0 map to 0..5 pips
                return Mathf.Clamp(level + PipsPerZone, 0, PipsPerZone);
            }

            // Normal: levels 0..15 map to 0..15 pips
            return Mathf.Clamp(level, 0, pipCount);
        }

        Color GetZoneColor(int pipIndex, int barIdx)
        {
            if (pipIndex < PipsPerZone)
                return normalZoneColor;
            if (pipIndex < PipsPerZone * 2)
                return _barDomainColors[barIdx];
            return superZoneColor;
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

            // Label shake for visual feedback
            for (int i = 0; i < bars.Length; i++)
            {
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
