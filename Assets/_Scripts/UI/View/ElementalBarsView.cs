using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// Shared elemental-bar HUD widget for every vessel. Renders four element "flowers"; each element
    /// is five copies of one crisp white petal sprite (charge = irregular pentagon, mass = triangle,
    /// space = kite, time = rhombus), pivot-centred and rotated 72°·n into a 5-fold flower.
    ///
    /// The element level is an integer in [-5, 15] (ResourceSystem.GetLevel = floor(level·10),
    /// level ∈ [-0.5, 1.5]) distributed round-robin across the five petals, so each petal holds a
    /// per-tick value in {-1,0,1,2,3} → {fire, grey, white, blue, lime}:
    ///
    ///   all fire = -5 | all grey = 0 | all white = 5 | all blue = 10 | all lime = 15
    ///
    /// At any total at most two adjacent colours show. Petals are pure white, so a single multiply-tint
    /// reproduces every spec colour exactly (no hue-shifting). Each petal recolours + scale-pops about
    /// the flower centre (outward bloom) as it changes.
    ///
    /// All shared look/feel (colours, petal sprites, juice timings) lives in <see cref="ElementalBarsConfigSO"/>
    /// so every vessel stays consistent. Wiring is optional: with no config assigned the view loads the
    /// default from <c>Resources/ElementalBarsConfig</c>, auto-creates a centred flower per element, and
    /// loads petal sprites from <c>Resources/ElementPetals/{element}_petal</c>.
    /// </summary>
    public class ElementalBarsView : MonoBehaviour
    {
        [Serializable]
        public struct ElementBarBinding
        {
            [Tooltip("The element this flower represents.")]
            public Element element;

            [Tooltip("Optional square container the 5 rotated petals live under. Auto-created when empty.")]
            public RectTransform petalRoot;

            [Tooltip("Optional per-bar petal sprite override. Falls back to the config / Resources sprite.")]
            public Sprite petalSpriteOverride;

            [Tooltip("Optional label/icon image below the flower (drift/joust/crystal juice target).")]
            public Image labelIcon;

            [Tooltip("Normal sprite for the label (restored after drift). Defaults to the label's sprite.")]
            public Sprite normalLabelSprite;
        }

        [Header("Config (shared spec - single source of truth)")]
        [Tooltip("Colours, petal sprites and juice timings. Loaded from Resources/ElementalBarsConfig when empty.")]
        [SerializeField] private ElementalBarsConfigSO config;

        [Header("Bar Bindings")]
        [SerializeField] private ElementBarBinding[] bars = new ElementBarBinding[0];

        [Header("Auto-created Petal Root")]
        [Tooltip("Size (px) of a flower container when one is auto-created.")]
        [SerializeField] private float autoRootSize = 88f;
        [Tooltip("Horizontal spacing (px) between auto-created flower centres.")]
        [SerializeField] private float autoRootSpacing = 104f;
        [Tooltip("Extra vertical offset (px) for the auto-created row, after centring in the container.")]
        [SerializeField] private float autoRootVerticalOffset = 0f;

        [Tooltip("Resources path of the default config, used when none is assigned in the inspector.")]
        [SerializeField] private string configResourcePath = "ElementalBarsConfig";
        [Tooltip("Resources path of per-element petal sprites; '{0}' = element name lowercased.")]
        [SerializeField] private string petalResourceFormat = "ElementPetals/{0}_petal";

        // Runtime state
        private RectTransform _rootRT;
        private Tween     _scaleTween;
        private int[]     _currentLevels;
        private Image[][] _petals;       // [barIndex][0..4]
        private int[][]   _petalValues;  // [barIndex][0..4] current per-petal tick value
        private Tween[][] _petalTweens;  // [barIndex][0..4]
        private Vector3[][] _petalRestPos; // [barIndex][0..4] clean rest localPosition (flower arrangement)
        private Color[]   _originalLabelColors;
        private Vector3[] _originalLabelScales;
        private Tween[]   _driftRotationTweens;
        private Tween[]   _labelScaleTweens;
        private Tween[]   _labelColorTweens;
        private bool _built;

        // Scratch buffer reused by DistributePetalValues to avoid per-call allocations.
        private readonly int[] _tmpVals = new int[ElementalBarsConfigSO.PetalCount];

        public bool IsBuilt => _built;

        private const int PetalCount = ElementalBarsConfigSO.PetalCount;
        private const int MinLevel   = ElementalBarsConfigSO.MinLevel;
        private const int MaxLevel   = ElementalBarsConfigSO.MaxLevel;

        void Start()
        {
            Build();
        }

        public void Build()
        {
            if (_built) return;

            // The four-element display is a REQUIRED fleet-wide system: a view with no
            // authored bindings self-populates the standard four flowers instead of
            // silently building nothing (petal roots auto-create; sprites come from
            // Resources; placement from the shared config). Authored bindings (label
            // icons, sprite overrides) take precedence when present.
            EnsureDefaultBars();

            if (!config)
                config = Resources.Load<ElementalBarsConfigSO>(configResourcePath);
            if (!config)
            {
                Debug.LogError($"[ElementalBarsView] No ElementalBarsConfigSO assigned and none found at " +
                               $"Resources/{configResourcePath}. Bars cannot be built.", this);
                return;
            }

            _rootRT = (RectTransform)transform;

            // Fleet-wide standard placement applies ONLY to runtime-built widgets: when every
            // flower container is authored in the prefab (the baked/authored state), the
            // designer's container rect is the source of truth and the config must not stamp
            // over it on play. Unauthored/auto-created widgets still get the standard rect so
            // a vessel can never silently ship with the display in a random spot.
            bool allFlowersAuthored = true;
            for (int i = 0; i < bars.Length; i++)
                if (!bars[i].petalRoot) { allFlowersAuthored = false; break; }

            if (config.enforceStandardPlacement && !allFlowersAuthored)
            {
                _rootRT.anchorMin        = config.standardAnchorMin;
                _rootRT.anchorMax        = config.standardAnchorMax;
                _rootRT.pivot            = config.standardPivot;
                _rootRT.anchoredPosition = config.standardAnchoredPosition;
                _rootRT.sizeDelta        = config.standardSize;
            }

            int count = bars.Length;
            _currentLevels       = new int[count];
            _petals              = new Image[count][];
            _petalValues         = new int[count][];
            _petalTweens         = new Tween[count][];
            _petalRestPos        = new Vector3[count][];
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
                _petalRestPos[i]  = new Vector3[PetalCount];
                _petals[i]        = BuildPetals(ref bar, i);

                // Capture each petal's clean rest position so an interrupted shake can always be
                // snapped back to the correct flower arrangement.
                var builtPetals = _petals[i];
                if (builtPetals != null)
                    for (int p = 0; p < PetalCount; p++)
                        if (builtPetals[p]) _petalRestPos[i][p] = builtPetals[p].rectTransform.localPosition;

                ElementalBarsConfigSO.DistributePetalValues(0, _petalValues[i]); // baseline: all grey
                ApplyPetalColorsImmediate(i);
            }

            _built = true;
        }

        void EnsureDefaultBars()
        {
            if (bars is { Length: > 0 }) return;
            bars = new[]
            {
                new ElementBarBinding { element = Element.Charge },
                new ElementBarBinding { element = Element.Mass },
                new ElementBarBinding { element = Element.Space },
                new ElementBarBinding { element = Element.Time },
            };
        }

        /// <summary>
        /// The container this element's petals live under, when one is authored in the prefab (or
        /// has already been auto-created). The ability lockup asks for this FIRST so it can re-home
        /// an authored flower into its card instead of leaving it stranded at the old row position
        /// and building a second set of petals in the card.
        /// </summary>
        public bool TryGetPetalRoot(Element element, out RectTransform root)
        {
            root = null;
            if (bars == null) return false;
            for (int i = 0; i < bars.Length; i++)
            {
                if (bars[i].element != element || !bars[i].petalRoot) continue;
                root = bars[i].petalRoot;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Points one element's flower at an externally supplied container, before the petals are
        /// built. This is how <see cref="AbilityLockupView"/> docks the flowers INTO the ability
        /// lockup cards instead of leaving them in a separate row - same petals, same ladder, same
        /// juice, just parented into the card that the element upgrades.
        ///
        /// Refused once the petals exist: re-homing a built flower would orphan five live images.
        /// Supplying every root also stands the shared config's standard placement down, which is
        /// correct - the socket is now the authored position.
        /// </summary>
        public bool TrySetPetalRoot(Element element, RectTransform root)
        {
            if (_built || !root) return false;

            EnsureDefaultBars();
            for (int i = 0; i < bars.Length; i++)
            {
                if (bars[i].element != element) continue;
                bars[i].petalRoot = root;
                return true;
            }
            return false;
        }

        Image[] BuildPetals(ref ElementBarBinding bar, int index)
        {
            var root   = ResolvePetalRoot(ref bar, index);
            var sprite = ResolvePetalSprite(in bar);
            if (!root || !sprite)
            {
                Debug.LogError($"[ElementalBarsView] Could not resolve a petal root/sprite for element " +
                               $"'{bar.element}'. Check the config's petal sprites or " +
                               $"Resources/{string.Format(petalResourceFormat, bar.element.ToString().ToLowerInvariant())}.", this);
                return null;
            }

            var petals = new Image[PetalCount];
            int runtimeCreated = 0;
            for (int p = 0; p < PetalCount; p++)
            {
                // Reuse a petal authored in the prefab ("Petal{p}") if present, else create one.
                var existing = root.Find($"Petal{p}");
                Image img = existing ? existing.GetComponent<Image>() : null;
                if (!img)
                {
                    var go = new GameObject($"Petal{p}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(root, false);
                    img = go.GetComponent<Image>();
                    runtimeCreated++;
                }
                ConfigurePetal(img, sprite, p);
                petals[p] = img;
            }

            // Authored-in-prefab is the norm (FrogletTools > Vessels > Wire Elemental Petal Bars, or
            // the bake-all variant). Runtime creation is a fallback so the fleet-required display
            // can never silently ship missing - but it should be loud, not invisible.
            if (runtimeCreated > 0)
                Debug.LogWarning($"[ElementalBarsView] Created {runtimeCreated} petal(s) for '{bar.element}' at " +
                                 "RUNTIME. Author them into the prefab instead: FrogletTools > Vessels > " +
                                 "Bake Elemental Petal Bars Into All Vessel HUDs.", this);
            return petals;
        }

        /// <summary>
        /// Applies the shared petal layout: stretch to fill the flower container, pivot on the flower
        /// centre, rotate 72°·index, assign the sprite. Public so a petal authored in the prefab as
        /// "Petal{index}" matches exactly what the runtime builds (and is reused, not duplicated).
        /// </summary>
        public static void ConfigurePetal(Image img, Sprite sprite, int petalIndex)
        {
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.Euler(0f, 0f, -ElementalBarsConfigSO.PetalSpacing * petalIndex);

            img.sprite = sprite;
            img.raycastTarget = false;     // decorative - never block touches
            img.preserveAspect = true;
        }

        RectTransform ResolvePetalRoot(ref ElementBarBinding bar, int index)
        {
            if (bar.petalRoot) return bar.petalRoot;

            Debug.LogWarning($"[ElementalBarsView] Auto-creating the '{bar.element}' flower container at " +
                             "RUNTIME - no petalRoot is authored in the prefab. Run FrogletTools > Vessels > " +
                             "Bake Elemental Petal Bars Into All Vessel HUDs to author it.", this);
            var go = new GameObject($"{bar.element}_Flower", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.sizeDelta = new Vector2(autoRootSize, autoRootSize);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            // Centre the row inside the container regardless of the container's own pivot, then apply
            // the manual offset. anchoredPosition of a centre-anchored child is measured from the
            // container centre, so we only need to correct for a non-centre pivot.
            float x = (index - (bars.Length - 1) * 0.5f) * autoRootSpacing;
            float pivotYCorrection = (0.5f - _rootRT.pivot.y) * _rootRT.rect.height;
            rt.anchoredPosition = new Vector2(x, pivotYCorrection + autoRootVerticalOffset);
            bar.petalRoot = rt;
            return rt;
        }

        Sprite ResolvePetalSprite(in ElementBarBinding bar)
        {
            if (bar.petalSpriteOverride) return bar.petalSpriteOverride;
            var fromConfig = config.GetPetalSprite(bar.element);
            if (fromConfig) return fromConfig;
            return Resources.Load<Sprite>(string.Format(petalResourceFormat, bar.element.ToString().ToLowerInvariant()));
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
        /// Retained for API compatibility. Petal colours follow the fixed element-tick spec, not the
        /// team domain colour, so this argument is ignored.
        /// </param>
        public void SetLevel(Element element, int level, Color domainColor) => SetLevel(element, level);

        public void SetLevel(Element element, int level)
        {
            int idx = GetBarIndex(element);
            if (idx < 0 || !_built) return;

            int clamped = Mathf.Clamp(level, MinLevel, MaxLevel);
            int prev = _currentLevels[idx];
            if (clamped == prev) return;           // nothing to redraw

            _currentLevels[idx] = clamped;
            RefreshBar(idx, prev);
        }

        /// <summary>Re-applies every bar's current level to its petals immediately (no juice). Useful
        /// after a config swap or a manual sprite/colour change.</summary>
        public void RefreshAllBars()
        {
            if (!_built) return;
            for (int i = 0; i < bars.Length; i++)
            {
                ElementalBarsConfigSO.DistributePetalValues(_currentLevels[i], _petalValues[i]);
                ApplyPetalColorsImmediate(i);
            }
        }

        void RefreshBar(int idx, int previousLevel)
        {
            var petals = _petals[idx];
            if (petals == null) return;

            int level = _currentLevels[idx];
            ElementalBarsConfigSO.DistributePetalValues(level, _tmpVals);

            if (level < previousLevel && config.hapticOnDebuff)
                HapticController.PlayConstant(config.debuffHapticAmplitude, config.debuffHapticFrequency, config.debuffHapticDuration);

            var oldVals = _petalValues[idx];
            var tweens  = _petalTweens[idx];

            for (int p = 0; p < PetalCount; p++)
            {
                var img = petals[p];
                if (!img) continue;

                int newV = _tmpVals[p];
                int oldV = oldVals[p];
                if (newV == oldV) continue;        // this petal didn't change colour

                Color target = config.ColorForTick(newV);
                tweens[p]?.Kill();
                var rt = img.rectTransform;

                // Buff pops scale; debuff shakes position - different transform channels. If one
                // interrupts the other mid-flight, the killed tween leaves its channel dirty (scale
                // stuck > 1, or an off-centre shake offset) and the flower looks mis-arranged. Snap
                // both channels back to rest first so the arrangement is always clean before animating.
                rt.localScale    = Vector3.one;
                rt.localPosition = _petalRestPos[idx][p];

                if (newV > oldV)                   // upgraded -> pop in new colour
                {
                    img.color = target;
                    int bi = idx, pi = p;
                    tweens[p] = rt
                        .DOScale(Vector3.one * config.buffPopScale, config.buffPopDuration * 0.4f)
                        .SetEase(Ease.OutQuad)
                        .SetLink(img.gameObject)
                        .OnComplete(() =>
                        {
                            _petalTweens[bi][pi] = rt
                                .DOScale(Vector3.one, config.buffPopDuration * 0.6f)
                                .SetEase(Ease.OutBounce)
                                .SetLink(img.gameObject);
                        });
                }
                else                               // downgraded -> flash + shake then settle
                {
                    img.color = config.debuffFlashColor;
                    tweens[p] = DOTween.Sequence()
                        .Append(rt.DOShakePosition(config.debuffShakeDuration, config.debuffShakeStrength, 20, 90f, false, false))
                        .Join(img.DOColor(target, config.debuffShakeDuration + 0.1f).SetEase(Ease.OutQuad))
                        .SetLink(img.gameObject);
                }

                oldVals[p] = newV;
            }
        }

        void ApplyPetalColorsImmediate(int idx)
        {
            var petals = _petals[idx];
            if (petals == null) return;
            var vals = _petalValues[idx];
            var rest = _petalRestPos != null ? _petalRestPos[idx] : null;
            for (int p = 0; p < PetalCount; p++)
            {
                if (!petals[p]) continue;
                petals[p].color = config.ColorForTick(vals[p]);
                var rt = petals[p].rectTransform;
                rt.localScale = Vector3.one;
                if (rest != null) rt.localPosition = rest[p];   // snap arrangement back to rest
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
                PunchIconWithColor(i, config.joustFlashColor);
        }

        // ---------------------------------------------------------------
        // Juice: Drift
        // ---------------------------------------------------------------
        public void JuiceDriftStart(bool isLeft, bool isDoubleDrift)
        {
            if (!_built) return;

            float targetAngle = isLeft ? config.driftRotationAngle : -config.driftRotationAngle;

            for (int i = 0; i < bars.Length; i++)
            {
                var label = bars[i].labelIcon;
                if (!label) continue;

                if (isDoubleDrift && config.doubleDriftSprite)
                    label.sprite = config.doubleDriftSprite;

                _driftRotationTweens[i]?.Kill();
                _driftRotationTweens[i] = label.rectTransform
                    .DOLocalRotate(new Vector3(0, 0, targetAngle), config.driftRotationDuration)
                    .SetEase(Ease.OutBack)
                    .SetLink(label.gameObject);

                _labelColorTweens[i]?.Kill();
                _labelColorTweens[i] = label
                    .DOColor(config.driftTintColor, config.driftRotationDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLink(label.gameObject);
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
                    .DOLocalRotate(Vector3.zero, config.driftRotationDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLink(label.gameObject);

                _labelColorTweens[i]?.Kill();
                _labelColorTweens[i] = label
                    .DOColor(_originalLabelColors[i], config.colorTweenDuration)
                    .SetEase(Ease.OutQuad)
                    .SetLink(label.gameObject);
            }
        }

        // ---------------------------------------------------------------
        // Internal
        // ---------------------------------------------------------------

        void PunchIconWithColor(int idx, Color flashColor)
        {
            var label = bars[idx].labelIcon;
            if (!label) return;

            var rt        = label.rectTransform;
            var origScale = _originalLabelScales[idx];

            int bi = idx;
            _labelScaleTweens[idx]?.Kill();
            rt.localScale = origScale;
            _labelScaleTweens[idx] = rt
                .DOScale(origScale * config.iconPunchScale, config.iconPunchDuration * 0.3f)
                .SetEase(Ease.OutQuad)
                .SetLink(label.gameObject)
                .OnComplete(() =>
                {
                    _labelScaleTweens[bi] = rt
                        .DOScale(origScale, config.iconPunchDuration * 0.7f)
                        .SetEase(Ease.OutBounce)
                        .SetLink(label.gameObject);
                });

            _labelColorTweens[idx]?.Kill();
            label.color = flashColor;
            _labelColorTweens[idx] = label
                .DOColor(_originalLabelColors[idx], config.colorTweenDuration)
                .SetEase(Ease.OutQuad)
                .SetLink(label.gameObject);
        }

        int GetBarIndex(Element element)
        {
            for (int i = 0; i < bars.Length; i++)
                if (bars[i].element == element)
                    return i;
            return -1;
        }

        // Kill in-flight tweens and snap everything to its rest pose. Because Build() is guarded by
        // _built, a disable/re-enable cycle (e.g. a pooled or toggled HUD) won't rebuild - so we leave
        // the petals at the correct colour/scale and labels at rest rather than mid-tween.
        void OnDisable()
        {
            KillAllTweens();
            if (!_built) return;

            for (int i = 0; i < bars.Length; i++)
            {
                ApplyPetalColorsImmediate(i);      // restores tick colour + scale 1 per petal
                var label = bars[i].labelIcon;
                if (label)
                {
                    label.color = _originalLabelColors[i];
                    label.rectTransform.localScale = _originalLabelScales[i];
                    label.rectTransform.localRotation = Quaternion.identity;
                    if (bars[i].normalLabelSprite) label.sprite = bars[i].normalLabelSprite;
                }
            }
        }

        void OnDestroy() => KillAllTweens();

        void KillAllTweens()
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
