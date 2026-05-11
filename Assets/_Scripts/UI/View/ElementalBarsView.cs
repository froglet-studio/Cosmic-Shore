using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;

namespace CosmicShore.UI
{
    public class ElementalBarsView : MonoBehaviour
    {
        [Serializable]
        public struct ElementBarBinding
        {
            [Tooltip("The element this bar represents")]
            public Element element;

            [Tooltip("Single flower Image — sprite + color are set at runtime")]
            public Image flowerImage;

            [Tooltip("Sprites indexed by petal count: [0]=0 petals, [1]=1 petal … [5]=5 petals")]
            public Sprite[] petalSprites;

            [Tooltip("Label/icon image below the flower")]
            public Image labelIcon;

            [Tooltip("Normal sprite for the label (restored after drift)")]
            public Sprite normalLabelSprite;
        }

        [Header("Bar Bindings")]
        [SerializeField] private ElementBarBinding[] bars = new ElementBarBinding[0];

        [Header("Colors")]
        [SerializeField] private Color dangerColor   = new(1f,    0.2f, 0.2f, 1f);
        [SerializeField] private Color baselineColor = Color.white;
        [SerializeField] private Color buffedColor   = new(0.2f, 1f,   0.3f, 1f);
        [Tooltip("Color flash on debuff before restoring to zone color")]
        [SerializeField] private Color debuffFlashColor = new(1f, 0.2f, 0.2f, 1f);

        [Header("Juice — Flower Transitions")]
        [SerializeField] private float buffPopScale        = 1.4f;
        [SerializeField] private float buffPopDuration     = 0.25f;
        [SerializeField] private float debuffShakeDuration = 0.20f;
        [SerializeField] private float debuffShakeStrength = 8f;

        [Header("Juice — Haptics")]
        [SerializeField] private bool  hapticOnDebuff         = true;
        [SerializeField] private float debuffHapticAmplitude  = 0.6f;
        [SerializeField] private float debuffHapticFrequency  = 0.5f;
        [SerializeField] private float debuffHapticDuration   = 0.15f;

        [Header("Juice — General")]
        [SerializeField] private float iconPunchDuration = 0.25f;
        [SerializeField] private float iconPunchScale    = 1.4f;
        [SerializeField] private float colorTweenDuration = 0.35f;

        [Header("Juice — Joust")]
        [SerializeField] private Color joustFlashColor = Color.red;

        [Header("Juice — Drift")]
        [SerializeField] private float  driftRotationAngle    = 15f;
        [SerializeField] private float  driftRotationDuration = 0.2f;
        [SerializeField] private Sprite doubleDriftSprite;

        // Runtime state
        private RectTransform _rootRT;
        private Tween   _scaleTween;
        private int[]   _currentLevels;
        private Color[] _barDomainColors;
        private Color[] _originalLabelColors;
        private Vector3[] _originalLabelScales;
        private Tween[] _driftRotationTweens;
        private Tween[] _labelScaleTweens;
        private Tween[] _labelColorTweens;
        private Tween[] _flowerTweens;
        private bool _built;
        private bool _overtakeActive;

        public bool IsBuilt => _built;

        private const int BaselineLevel = 5;
        private const int MaxPetals     = 5;

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
            _currentLevels       = new int[count];
            _barDomainColors     = new Color[count];
            _originalLabelColors = new Color[count];
            _originalLabelScales = new Vector3[count];
            _driftRotationTweens = new Tween[count];
            _labelScaleTweens    = new Tween[count];
            _labelColorTweens    = new Tween[count];
            _flowerTweens        = new Tween[count];

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

                _currentLevels[i]   = BaselineLevel;
                _barDomainColors[i] = baselineColor;
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
            _scaleTween = _rootRT.DOScale(Vector3.one * targetScale, duration).SetEase(ease);
        }

        public void AnimateScale(Vector3 targetScale, float duration = 0.3f, Ease ease = Ease.OutBack)
        {
            if (!_rootRT) return;
            _scaleTween?.Kill();
            _scaleTween = _rootRT.DOScale(targetScale, duration).SetEase(ease);
        }

        public Vector3 Scale => _rootRT ? _rootRT.localScale : Vector3.one;

        // ---------------------------------------------------------------
        // Level updates
        // ---------------------------------------------------------------

        public void SetLevel(Element element, int level, Color domainColor)
        {
            int idx = GetBarIndex(element);
            if (idx < 0 || !_built) return;

            // Floor at BaselineLevel in normal gameplay so an uninitialized GetLevel(0) never
            // triggers the danger zone. Danger petals are only reachable via BeginOvertake().
            int clamped = _overtakeActive ? level : Mathf.Max(BaselineLevel, level);
            clamped = Mathf.Clamp(clamped, 0, BaselineLevel + MaxPetals * 2);

            _barDomainColors[idx] = domainColor;
            int prev = _currentLevels[idx];
            _currentLevels[idx] = clamped;

            RefreshBar(idx, prev);
        }

        public void SetLevel(Element element, int level)
        {
            SetLevel(element, level, baselineColor);
        }

        public void BeginOvertake() { _overtakeActive = true; }
        public void EndOvertake()   { _overtakeActive = false; }

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
            if (!bar.flowerImage) return;

            bool isIncreasing = level > previousLevel;
            bool isDecreasing = level < previousLevel;

            if (isDecreasing && hapticOnDebuff)
                HapticController.PlayConstant(debuffHapticAmplitude, debuffHapticFrequency, debuffHapticDuration);

            int  petalCount;
            Color targetColor;
            GetZoneInfo(idx, level, out petalCount, out targetColor);

            // Swap sprite
            if (bar.petalSprites != null && petalCount < bar.petalSprites.Length && bar.petalSprites[petalCount])
                bar.flowerImage.sprite = bar.petalSprites[petalCount];

            _flowerTweens[idx]?.Kill();
            var rt = bar.flowerImage.rectTransform;

            if (isIncreasing)
            {
                bar.flowerImage.color = targetColor;
                rt.localScale = Vector3.one;
                _flowerTweens[idx] = rt
                    .DOScale(Vector3.one * buffPopScale, buffPopDuration * 0.4f)
                    .SetEase(Ease.OutQuad)
                    .OnComplete(() =>
                    {
                        _flowerTweens[idx] = rt
                            .DOScale(Vector3.one, buffPopDuration * 0.6f)
                            .SetEase(Ease.OutBounce);
                    });
            }
            else if (isDecreasing)
            {
                bar.flowerImage.color = debuffFlashColor;
                _flowerTweens[idx] = DOTween.Sequence()
                    .Append(rt.DOShakePosition(debuffShakeDuration, debuffShakeStrength, 20, 90f, false, false))
                    .Join(bar.flowerImage.DOColor(targetColor, debuffShakeDuration + 0.1f).SetEase(Ease.OutQuad));
            }
            else
            {
                bar.flowerImage.color = targetColor;
                rt.localScale = Vector3.one;
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

            if (hapticOnDebuff)
                HapticController.PlayConstant(0.8f, 0.7f, 0.25f);

            for (int i = 0; i < bars.Length; i++)
            {
                var flower = bars[i].flowerImage;
                if (!flower) continue;

                _flowerTweens[i]?.Kill();
                GetZoneInfo(i, _currentLevels[i], out _, out var origColor);

                flower.color = Color.red;
                var rt = flower.rectTransform;
                _flowerTweens[i] = DOTween.Sequence()
                    .Append(rt.DOShakePosition(0.3f, 6f, 15, 90f, false, false))
                    .Join(flower.DOColor(origColor, 0.5f).SetEase(Ease.OutQuad));

                var label = bars[i].labelIcon;
                if (label)
                {
                    _labelScaleTweens[i]?.Kill();
                    _labelScaleTweens[i] = label.rectTransform.DOShakeScale(0.4f, 0.3f, 10, 90f);
                }
            }
        }

        // ---------------------------------------------------------------
        // Internal
        // ---------------------------------------------------------------

        void GetZoneInfo(int idx, int level, out int petalCount, out Color color)
        {
            if (level < BaselineLevel)
            {
                // Danger: level 0 → 5 petals, level 4 → 1 petal
                petalCount = BaselineLevel - level;
                color = dangerColor;
            }
            else if (level == BaselineLevel)
            {
                petalCount = 0;
                color = baselineColor;
            }
            else if (level <= BaselineLevel + MaxPetals)
            {
                // Normal: level 6 → 1 petal, level 10 → 5 petals
                petalCount = level - BaselineLevel;
                color = _barDomainColors[idx];
            }
            else
            {
                // Buffed: level 11 → 1 petal, level 15 → 5 petals
                petalCount = level - (BaselineLevel + MaxPetals);
                color = buffedColor;
            }
        }

        void PunchIconWithColor(int idx, Color flashColor)
        {
            var label = bars[idx].labelIcon;
            if (!label) return;

            var rt       = label.rectTransform;
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
            if (_driftRotationTweens != null) foreach (var t in _driftRotationTweens) t?.Kill();
            if (_labelScaleTweens   != null) foreach (var t in _labelScaleTweens)    t?.Kill();
            if (_labelColorTweens   != null) foreach (var t in _labelColorTweens)    t?.Kill();
            if (_flowerTweens       != null) foreach (var t in _flowerTweens)         t?.Kill();
        }
    }
}
