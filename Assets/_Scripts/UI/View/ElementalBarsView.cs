using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Core;
using CosmicShore.Data;

namespace CosmicShore.UI
{
    /// <summary>
    /// Displays four element "flowers". Each element supplies a single crisp white petal sprite
    /// (charge = rounded lobe, mass = pie-slice triangle, space = kite, time = rhombus) whose pivot
    /// sits at the flower centre; the view stacks five copies rotated 72°·n into a 5-fold flower.
    ///
    /// The element level is an integer in [-5, 15] (ResourceSystem.GetLevel = floor(level·10),
    /// level ∈ [-0.5, 1.5]). That total is distributed round-robin across the five petals, so each
    /// petal holds a per-tick value in {-1,0,1,2,3} mapped to {fire, grey, white, blue, lime}:
    ///
    ///   all fire = -5 | all grey = 0 | all white = 5 | all blue = 10 | all lime = 15
    ///
    /// At any total at most two adjacent colours are visible (e.g. 3 → 3 white + 2 grey). Because the
    /// petals are pure white, a single multiply-tint reproduces every spec colour exactly — no
    /// hue-shifting required. Each petal is recoloured and scale-popped independently as it changes.
    /// </summary>
    public class ElementalBarsView : MonoBehaviour
    {
        [Serializable]
        public struct ElementBarBinding
        {
            [Tooltip("The element this flower represents")]
            public Element element;

            [Tooltip("Square container the 5 rotated petal Images are created under at runtime.")]
            public RectTransform petalRoot;

            [Tooltip("One crisp white petal silhouette, pivot-centred on the flower. Rotated 72°·n " +
                     "to build the 5-fold flower; tinted to the element-tick colours at runtime.")]
            public Sprite petalSprite;

            [Tooltip("Label/icon image below the flower")]
            public Image labelIcon;

            [Tooltip("Normal sprite for the label (restored after drift)")]
            public Sprite normalLabelSprite;
        }

        [Header("Bar Bindings")]
        [SerializeField] private ElementBarBinding[] bars = new ElementBarBinding[0];

        [Header("Element Tick Colors (per-petal value)")]
        [Tooltip("-1 : fire")]  [SerializeField] private Color fireColor  = new(1f,    0.33f, 0.10f, 1f);
        [Tooltip(" 0 : grey")]  [SerializeField] private Color greyColor  = new(0.51f, 0.51f, 0.54f, 1f);
        [Tooltip(" 1 : white")] [SerializeField] private Color whiteColor = new(0.96f, 0.96f, 1f,    1f);
        [Tooltip(" 2 : blue")]  [SerializeField] private Color blueColor  = new(0.22f, 0.51f, 1f,    1f);
        [Tooltip(" 3 : lime")]  [SerializeField] private Color limeColor  = new(0.59f, 0.92f, 0.16f, 1f);
        [Tooltip("Color flash on a petal that downgrades, before it settles to its tick color")]
        [SerializeField] private Color debuffFlashColor = new(1f, 0.2f, 0.2f, 1f);

        [Header("Juice — Petal Transitions")]
        [SerializeField] private float buffPopScale        = 1.3f;
        [SerializeField] private float buffPopDuration     = 0.22f;
        [SerializeField] private float debuffShakeDuration = 0.20f;
        [SerializeField] private float debuffShakeStrength = 8f;

        [Header("Juice — Haptics")]
        [SerializeField] private bool  hapticOnDebuff        = true;
        [SerializeField] private float debuffHapticAmplitude = 0.6f;
        [SerializeField] private float debuffHapticFrequency = 0.5f;
        [SerializeField] private float debuffHapticDuration  = 0.15f;

        [Header("Juice — General")]
        [SerializeField] private float iconPunchDuration  = 0.25f;
        [SerializeField] private float iconPunchScale     = 1.4f;
        [SerializeField] private float colorTweenDuration = 0.35f;

        [Header("Juice — Joust")]
        [SerializeField] private Color joustFlashColor = Color.red;

        [Header("Juice — Drift")]
        [SerializeField] private float  driftRotationAngle    = 15f;
        [SerializeField] private float  driftRotationDuration = 0.2f;
        [SerializeField] private Sprite doubleDriftSprite;

        // Runtime state
        private RectTransform _rootRT;
        private Tween     _scaleTween;
        private int[]     _currentLevels;
        private Image[][] _petals;       // [barIndex][0..4]
        private int[][]   _petalValues;  // [barIndex][0..4] current per-petal tick value
        private Tween[][] _petalTweens;  // [barIndex][0..4]
        private Color[]   _originalLabelColors;
        private Vector3[] _originalLabelScales;
        private Tween[]   _driftRotationTweens;
        private Tween[]   _labelScaleTweens;
        private Tween[]   _labelColorTweens;
        private bool _built;

        // Scratch buffer reused by PetalValues to avoid per-call allocations.
        private readonly int[] _tmpVals = new int[PetalCount];

        public bool IsBuilt => _built;

        private const int   PetalCount   = 5;
        private const int   MinLevel     = -PetalCount;     // -5  (all fire)
        private const int   MaxLevel     =  PetalCount * 3; //  15 (all lime)
        private const float PetalSpacing = 360f / PetalCount;

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
            _petals              = new Image[count][];
            _petalValues         = new int[count][];
            _petalTweens         = new Tween[count][];
            _originalLabelColors = new Color[count];
            _originalLabelScales = new Vector3[count];
            _driftRotationTweens = new Tween[count];
            _labelScaleTweens    = new Tween[count];
            _labelColorTweens    = new Tween[count];

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

                _currentLevels[i] = 0;
                _petalValues[i]   = new int[PetalCount];
                _petalTweens[i]   = new Tween[PetalCount];
                _petals[i]        = BuildPetals(ref bar);

                PetalValues(0, _petalValues[i]); // baseline: all grey
                ApplyPetalColorsImmediate(i);
            }

            _built = true;
        }

        Image[] BuildPetals(ref ElementBarBinding bar)
        {
            if (!bar.petalRoot || !bar.petalSprite)
            {
                Debug.LogError($"[ElementalBarsView] Element '{bar.element}' is missing its petalRoot or " +
                               "petalSprite — run Tools > Cosmic Shore > Wire Elemental Petal Bars.", this);
                return null;
            }

            var petals = new Image[PetalCount];
            for (int p = 0; p < PetalCount; p++)
            {
                var go = new GameObject($"Petal{p}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(bar.petalRoot, false);
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot     = new Vector2(0.5f, 0.5f);             // == flower centre
                rt.localScale = Vector3.one;
                rt.localRotation = Quaternion.Euler(0f, 0f, -PetalSpacing * p);

                var img = go.GetComponent<Image>();
                img.sprite = bar.petalSprite;
                img.raycastTarget = false;
                img.preserveAspect = true;
                petals[p] = img;
            }
            return petals;
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

        /// <param name="domainColor">
        /// Retained for API compatibility. Petal colours follow the fixed element-tick spec
        /// (fire/grey/white/blue/lime), not the team domain colour, so this argument is ignored.
        /// </param>
        public void SetLevel(Element element, int level, Color domainColor) => SetLevel(element, level);

        public void SetLevel(Element element, int level)
        {
            int idx = GetBarIndex(element);
            if (idx < 0 || !_built) return;

            int clamped = Mathf.Clamp(level, MinLevel, MaxLevel);
            int prev = _currentLevels[idx];
            _currentLevels[idx] = clamped;
            RefreshBar(idx, prev);
        }

        public void RefreshAllBars()
        {
            if (!_built) return;
            for (int i = 0; i < bars.Length; i++)
                RefreshBar(i, _currentLevels[i]);
        }

        void RefreshBar(int idx, int previousLevel)
        {
            var petals = _petals[idx];
            if (petals == null) return;

            int level = _currentLevels[idx];
            PetalValues(level, _tmpVals);

            if (level < previousLevel && hapticOnDebuff)
                HapticController.PlayConstant(debuffHapticAmplitude, debuffHapticFrequency, debuffHapticDuration);

            var oldVals = _petalValues[idx];

            for (int p = 0; p < PetalCount; p++)
            {
                var img = petals[p];
                if (!img) continue;

                int newV = _tmpVals[p];
                int oldV = oldVals[p];
                Color target = ColorForTick(newV);

                _petalTweens[idx][p]?.Kill();
                var rt = img.rectTransform;

                if (newV > oldV)            // petal upgraded -> pop in new colour
                {
                    img.color = target;
                    rt.localScale = Vector3.one;
                    int bi = idx, pi = p;
                    _petalTweens[bi][pi] = rt
                        .DOScale(Vector3.one * buffPopScale, buffPopDuration * 0.4f)
                        .SetEase(Ease.OutQuad)
                        .OnComplete(() =>
                        {
                            _petalTweens[bi][pi] = rt
                                .DOScale(Vector3.one, buffPopDuration * 0.6f)
                                .SetEase(Ease.OutBounce);
                        });
                }
                else if (newV < oldV)       // petal downgraded -> flash + shake then settle
                {
                    img.color = debuffFlashColor;
                    _petalTweens[idx][p] = DOTween.Sequence()
                        .Append(rt.DOShakePosition(debuffShakeDuration, debuffShakeStrength, 20, 90f, false, false))
                        .Join(img.DOColor(target, debuffShakeDuration + 0.1f).SetEase(Ease.OutQuad));
                }
                else                        // unchanged
                {
                    img.color = target;
                    rt.localScale = Vector3.one;
                }

                oldVals[p] = newV;
            }
        }

        void ApplyPetalColorsImmediate(int idx)
        {
            var petals = _petals[idx];
            if (petals == null) return;
            var vals = _petalValues[idx];
            for (int p = 0; p < PetalCount; p++)
            {
                if (!petals[p]) continue;
                petals[p].color = ColorForTick(vals[p]);
                petals[p].rectTransform.localScale = Vector3.one;
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
        // Internal
        // ---------------------------------------------------------------

        /// <summary>
        /// Distributes a total level in [-5, 15] round-robin across <see cref="PetalCount"/> petals.
        /// Each petal value lands in {-1,0,1,2,3}; the first <c>extra</c> petals take the higher of
        /// the two adjacent colours, the rest the lower — exactly the spec fill order.
        /// </summary>
        static void PetalValues(int level, int[] dst)
        {
            int inc    = Mathf.Clamp(level, MinLevel, MaxLevel) - MinLevel; // 0..20
            int rounds = inc / PetalCount;   // 0..4
            int extra  = inc % PetalCount;   // 0..4
            for (int i = 0; i < PetalCount; i++)
                dst[i] = -1 + rounds + (i < extra ? 1 : 0);
        }

        Color ColorForTick(int v) => v switch
        {
            <= -1 => fireColor,
            0     => greyColor,
            1     => whiteColor,
            2     => blueColor,
            _     => limeColor,   // >= 3
        };

        void PunchIconWithColor(int idx, Color flashColor)
        {
            var label = bars[idx].labelIcon;
            if (!label) return;

            var rt        = label.rectTransform;
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
                if (bars[i].element == element)
                    return i;
            return -1;
        }

        void OnDestroy()
        {
            _scaleTween?.Kill();
            if (_driftRotationTweens != null) foreach (var t in _driftRotationTweens) t?.Kill();
            if (_labelScaleTweens    != null) foreach (var t in _labelScaleTweens)    t?.Kill();
            if (_labelColorTweens    != null) foreach (var t in _labelColorTweens)    t?.Kill();
            if (_petalTweens != null)
                foreach (var row in _petalTweens)
                    if (row != null)
                        foreach (var t in row) t?.Kill();
        }
    }
}
