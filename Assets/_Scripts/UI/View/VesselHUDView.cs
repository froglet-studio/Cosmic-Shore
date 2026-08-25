using System;
using System.Collections.Generic;
using CosmicShore.UI;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    public abstract class VesselHUDView : MonoBehaviour
    {
        [Serializable]
        public struct HighlightBinding
        {
            public InputEvents input;
            public Image image;
        }

        [Serializable]
        public struct AbilityIconBinding
        {
            [Tooltip("The element that upgrades this ability (per the vessel's ElementalAbilityMapSO). " +
                     "Also fixes the icon's place in the row - see AbilityDisplayOrder.")]
            public Element element;

            [Tooltip("The ability's icon Image in the lower-right row.")]
            public Image icon;

            [Tooltip("Optional authored art for the UPGRADED ability. Swapped in when this element " +
                     "reaches its unlock level and restored on re-lock. Leave empty to keep the base " +
                     "sprite - the elemental badge still marks the upgrade either way.")]
            public Sprite upgradedSprite;
        }

        /// <summary>
        /// The fleet-wide ability slot order, left to right. It is the element display order used by
        /// <see cref="ElementalBarsView"/> (charge, mass, space, time), so an ability icon always sits
        /// in the same column as the element flower that upgrades it. Do not reorder.
        /// </summary>
        public static readonly Element[] AbilityDisplayOrder =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        [Header("Button highlights")] public List<HighlightBinding> highlights = new();

        [Header("Ability icons (elemental upgrade highlight)")]
        [Tooltip("Exactly four entries - one per ability - keyed by the element that upgrades it (per " +
                 "the vessel's ElementalAbilityMapSO). The list is kept in charge/mass/space/time order " +
                 "and the icons must sit in that same order, left to right, in the lower-right row. " +
                 "Shared system - every vessel HUD wires its own four icons.")]
        public List<AbilityIconBinding> abilityIcons = new();

        [Tooltip("Tint applied to an ability icon while its elemental upgrade is active. Turn OFF on " +
                 "vessels whose icon colour is a live gameplay gauge (cooldown/heat/drift state) - the " +
                 "elemental badge and the scale bump still carry the signal there.")]
        [SerializeField] private bool tintIconOnUpgrade = true;
        [SerializeField] private Color upgradeHighlightColor = new(1f, 0.85f, 0.3f, 1f);
        [Tooltip("Persistent scale an upgraded ability icon rests at while the upgrade is active.")]
        [SerializeField] private float upgradeHighlightScale = 1.15f;
        [Tooltip("Scale punch played when an ability upgrade unlocks.")]
        [SerializeField] private float upgradePunchScale = 1.35f;
        [SerializeField] private float upgradePunchDuration = 0.35f;

        [Header("Ability upgrade badge")]
        [Tooltip("Marks an upgraded ability with that element's petal, in the level-5 white - the same " +
                 "visual language as the element flowers (all-petals-white IS level 5). Survives views " +
                 "that repaint the icon colour every frame, because it is a separate child image.")]
        [SerializeField] private bool showUpgradeBadge = true;
        [Tooltip("Petal sprites + the level-5 white. Loaded from Resources/ElementalBarsConfig when empty.")]
        [SerializeField] private ElementalBarsConfigSO elementalBarsConfig;
        [SerializeField] private string elementalBarsConfigResourcePath = "ElementalBarsConfig";
        [Tooltip("Badge size as a fraction of the icon's own size.")]
        [SerializeField, Range(0.1f, 1f)] private float badgeSizeFraction = 0.45f;
        [Tooltip("Where on the icon the badge sits (0,0 = bottom-left, 1,1 = top-right).")]
        [SerializeField] private Vector2 badgeAnchor = new(1f, 1f);
        [Tooltip("Seconds the badge takes to bloom in / wither out. Nothing pops in or out.")]
        [SerializeField, Min(0.01f)] private float badgeTransitionDuration = 0.3f;

        readonly Dictionary<Element, Color>   _abilityIconRestColors  = new();
        readonly Dictionary<Element, Sprite>  _abilityIconRestSprites = new();
        readonly Dictionary<Element, Tween>   _abilityIconTweens      = new();
        readonly Dictionary<Element, Image>   _abilityBadges          = new();
        readonly Dictionary<Element, Tween>   _abilityBadgeTweens     = new();
        readonly HashSet<Element>             _upgraded               = new();

        [Header("Ability lockup (optional)")]
        [Tooltip("Composes the totem card - plate, element flower, rim and bloom - around each " +
                 "ability icon. Resolved from this GameObject when empty; a vessel without one is " +
                 "simply unstyled, which is how the rollout stays opt-in per vessel.")]
        [SerializeField] private AbilityLockupView abilityLockups;

        [Header("Animation (optional)")]
        [SerializeField] private HUDAnimationSettingsSO animSettings;

        private CanvasGroup _canvasGroup;
        private Tween _fadeTween;

        public abstract void Initialize();

        /// <summary>Persistent rest scale an ability icon sits at while its upgrade is active.</summary>
        protected float UpgradeHighlightScale => upgradeHighlightScale;

        /// <summary>True while this element's level-5 upgrade is active on this HUD.</summary>
        public bool IsAbilityUpgraded(Element element) => _upgraded.Contains(element);

        /// <summary>
        /// The ability icon this element upgrades, if the vessel wired one. Used by the control-hint
        /// binder so an (LT)/(RT) label can find the ability it belongs to instead of being pinned to
        /// a hand-authored position.
        /// </summary>
        public bool TryGetAbilityIcon(Element element, out Image icon)
        {
            foreach (var binding in abilityIcons)
            {
                if (binding.element != element || !binding.icon) continue;
                icon = binding.icon;
                return true;
            }
            icon = null;
            return false;
        }

        /// <summary>True when this HUD authors the four-icon ability row at all (opt-in rollout).</summary>
        public bool HasAbilityIconRow => abilityIcons is { Count: > 0 };

        /// <summary>
        /// The lockup's icon KERNING - how much of the ability cell the icon fills. 1 when this HUD
        /// has no lockup, so an unstyled vessel is unaffected.
        /// </summary>
        protected float AbilityIconContentScale
        {
            get
            {
                var lockups = ResolveAbilityLockups();
                return lockups ? lockups.IconContentScale : 1f;
            }
        }

        /// <summary>
        /// The scale an ability icon should rest at right now - the lockup's content scale, times
        /// the upgrade bump when the upgrade is live. Per-vessel views that run their own scale
        /// tweens capture THIS as their icon's rest scale, so both the kerning and the persistent
        /// upgrade bump survive every tween they play.
        ///
        /// <para><b>For the ICON itself only.</b> Graphics NESTED inside an ability icon already
        /// inherit its scale by being children; resting them here too multiplies the two.</para>
        /// </summary>
        protected Vector3 AbilityIconRestScale(Element element)
            => Vector3.one * (AbilityIconContentScale * (IsAbilityUpgraded(element) ? upgradeHighlightScale : 1f));

        public void Show()
        {
            gameObject.SetActive(true);

            EnsureCanvasGroup();
            _fadeTween?.Kill();

            float duration = animSettings ? animSettings.vesselHudFadeDuration : 0.2f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            _canvasGroup.alpha = 0f;
            _fadeTween = _canvasGroup.DOFade(1f, duration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(unscaled);
        }

        public void Hide()
        {
            EnsureCanvasGroup();
            _fadeTween?.Kill();

            float duration = animSettings ? animSettings.vesselHudFadeDuration : 0.2f;
            bool unscaled = animSettings == null || animSettings.useUnscaledTime;

            _fadeTween = _canvasGroup.DOFade(0f, duration)
                .SetEase(Ease.InQuad)
                .SetUpdate(unscaled)
                .OnComplete(() => gameObject.SetActive(false));
        }

        /// <summary>
        /// Highlights (or rests) the ability icon bound to this element - called by the base
        /// VesselHUDController from the ElementalAbilityHandler's OnUpgradeStateChanged event
        /// and once at init to seed already-active upgrades. Safe no-op for unbound elements.
        ///
        /// Three layers, so the signal survives any per-vessel presentation:
        ///   1. sprite swap to the authored upgraded art (when authored),
        ///   2. the element's petal badge, blooming in / withering out,
        ///   3. an optional tint plus a persistent scale bump with a one-shot punch.
        /// </summary>
        public virtual void SetAbilityUpgraded(Element element, bool upgraded)
        {
            if (upgraded) _upgraded.Add(element);
            else          _upgraded.Remove(element);

            foreach (var binding in abilityIcons)
            {
                if (binding.element != element || !binding.icon) continue;

                if (!_abilityIconRestColors.ContainsKey(element))
                    _abilityIconRestColors[element] = binding.icon.color;
                if (!_abilityIconRestSprites.ContainsKey(element))
                    _abilityIconRestSprites[element] = binding.icon.sprite;

                if (_abilityIconTweens.TryGetValue(element, out var tween))
                    tween?.Kill();

                if (upgraded)
                {
                    // The icon itself changes when the vessel authored upgraded art for the slot.
                    if (binding.upgradedSprite)
                        binding.icon.sprite = binding.upgradedSprite;

                    if (tintIconOnUpgrade)
                        binding.icon.color = upgradeHighlightColor;

                    // Rest at the highlight scale (survives views that repaint colors per-frame),
                    // with a one-shot punch around it to telegraph the unlock. Both go through
                    // AbilityIconRestScale so the lockup's kerning is never re-derived here.
                    binding.icon.rectTransform.localScale = AbilityIconRestScale(element);
                    _abilityIconTweens[element] = binding.icon.rectTransform
                        .DOPunchScale(Vector3.one * ((upgradePunchScale - upgradeHighlightScale) * AbilityIconContentScale),
                            upgradePunchDuration, 1, 0.5f)
                        .SetUpdate(true)
                        .SetLink(binding.icon.gameObject);
                }
                else
                {
                    if (binding.upgradedSprite && _abilityIconRestSprites[element])
                        binding.icon.sprite = _abilityIconRestSprites[element];

                    if (tintIconOnUpgrade)
                        binding.icon.color = _abilityIconRestColors[element];

                    binding.icon.rectTransform.localScale = AbilityIconRestScale(element);
                }

                SetBadgeVisible(element, binding.icon, upgraded);
            }

            // The lockup carries the same signal on the CARD - rim to the level-5 white plus the
            // bloom behind the plate - which is what lets a vessel whose icons are all live gauges
            // (the Dolphin) show an upgrade without overloading a gauge colour.
            var lockups = ResolveAbilityLockups();
            if (lockups) lockups.SetUpgraded(element, upgraded);
        }

        AbilityLockupView ResolveAbilityLockups()
        {
            if (!abilityLockups) abilityLockups = GetComponent<AbilityLockupView>();
            return abilityLockups;
        }

        // ---------------------------------------------------------------
        // Upgrade badge - the element's own petal, in the level-5 white, pinned to a corner of the
        // ability icon. It is a child of the icon, so per-frame icon repaints can never stomp it,
        // and it rides along with whatever scale/position juice the vessel's view plays.
        // ---------------------------------------------------------------

        void SetBadgeVisible(Element element, Image icon, bool visible)
        {
            if (!showUpgradeBadge) return;

            // Lazy: a locked ability has nothing to hide, so no badge object is built until the
            // first unlock. Vessels that never reach level 5 never pay for one.
            if (!visible && !_abilityBadges.ContainsKey(element)) return;

            var badge = ResolveBadge(element, icon);
            if (!badge) return;

            if (_abilityBadgeTweens.TryGetValue(element, out var running))
                running?.Kill();

            var rt = badge.rectTransform;

            // Nothing pops in or out: the badge blooms open and withers closed.
            if (visible)
            {
                ApplyBadgeRect(badge, icon); // re-measure: the icon's rect may not have laid out at bind time
                badge.gameObject.SetActive(true);
                rt.localScale = Vector3.zero;
                badge.color = WithAlpha(badge.color, 0f);

                var seq = DOTween.Sequence().SetUpdate(true).SetLink(badge.gameObject);
                seq.Join(rt.DOScale(Vector3.one, badgeTransitionDuration).SetEase(Ease.OutBack));
                seq.Join(badge.DOFade(1f, badgeTransitionDuration * 0.6f).SetEase(Ease.OutQuad));
                _abilityBadgeTweens[element] = seq;
            }
            else if (badge.gameObject.activeSelf)
            {
                var seq = DOTween.Sequence().SetUpdate(true).SetLink(badge.gameObject);
                seq.Join(rt.DOScale(Vector3.zero, badgeTransitionDuration).SetEase(Ease.InBack));
                seq.Join(badge.DOFade(0f, badgeTransitionDuration).SetEase(Ease.InQuad));
                seq.OnComplete(() => badge.gameObject.SetActive(false));
                _abilityBadgeTweens[element] = seq;
            }
        }

        Image ResolveBadge(Element element, Image icon)
        {
            if (_abilityBadges.TryGetValue(element, out var cached) && cached) return cached;

            var config = ResolveElementalBarsConfig();
            var sprite = config ? config.GetPetalSprite(element) : null;
            if (!sprite)
                sprite = Resources.Load<Sprite>($"ElementPetals/{element.ToString().ToLowerInvariant()}_petal");
            if (!sprite) return null;

            string badgeName = $"UpgradeBadge_{element}";
            var existing = icon.rectTransform.Find(badgeName);
            var badge = existing ? existing.GetComponent<Image>() : null;
            if (!badge)
            {
                var go = new GameObject(badgeName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(icon.rectTransform, false);
                badge = go.GetComponent<Image>();
            }

            ApplyBadgeRect(badge, icon);

            badge.sprite = sprite;
            // Level 5 is the all-petals-WHITE state of the flower - the badge is that same white.
            badge.color = config ? config.whiteColor : Color.white;
            badge.raycastTarget = false;
            badge.preserveAspect = true;
            badge.gameObject.SetActive(false);

            _abilityBadges[element] = badge;
            return badge;
        }

        void ApplyBadgeRect(Image badge, Image icon)
        {
            var rt = badge.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = badgeAnchor;
            rt.anchoredPosition = Vector2.zero;
            rt.localRotation = Quaternion.identity;

            // Icons authored with stretch anchors have a zero rect until the layout pass runs; keep a
            // sane badge instead of an invisible zero-sized one.
            var iconSize = icon.rectTransform.rect.size;
            float side = Mathf.Max(iconSize.x, iconSize.y) * badgeSizeFraction;
            if (side <= 1f) side = DefaultBadgeSize;
            rt.sizeDelta = new Vector2(side, side);
        }

        const float DefaultBadgeSize = 32f;

        ElementalBarsConfigSO ResolveElementalBarsConfig()
        {
            if (!elementalBarsConfig)
                elementalBarsConfig = Resources.Load<ElementalBarsConfigSO>(elementalBarsConfigResourcePath);
            return elementalBarsConfig;
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        /// <summary>
        /// Editor-time structural check: four ability icons, one per element, bound in the canonical
        /// order AND laid out left to right in that same order. Called once from
        /// <see cref="VesselHUDController"/> after it seeds the upgrade state.
        /// </summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public void ValidateAbilityIconRow(VesselClassType vesselClass = VesselClassType.Any)
        {
            // The row is a fleet-wide REQUIREMENT, not an opt-in. A vessel that does not author it
            // says so once per class instead of failing silently - silence is how the Squirrel shipped
            // a reversed row and a mis-bound Charge slot unnoticed. Run
            // FrogletTools > Vessels > Audit Vessel Ability Rows for the whole fleet at once.
            if (abilityIcons == null || abilityIcons.Count == 0)
            {
                if (_missingRowReported.Add(vesselClass))
                    Debug.LogWarning($"[VesselHUDView] {vesselClass} ({name}) binds NO abilityIcons - the " +
                                     "four-icon ability row is missing on this vessel. Every vessel is " +
                                     "expected to show one icon per element in charge/mass/space/time order. " +
                                     "Audit the fleet with FrogletTools > Vessels > Audit Vessel Ability Rows.", this);
                return;
            }

            if (abilityIcons.Count != AbilityDisplayOrder.Length)
            {
                Debug.LogWarning($"[VesselHUDView] {vesselClass} ({name}) binds {abilityIcons.Count} ability " +
                                 $"icon(s); the standard is {AbilityDisplayOrder.Length} - one per element.", this);
                return;
            }

            float previousX = float.NegativeInfinity;
            for (int i = 0; i < AbilityDisplayOrder.Length; i++)
            {
                var expected = AbilityDisplayOrder[i];
                var binding = abilityIcons[i];

                if (binding.element != expected)
                {
                    Debug.LogWarning($"[VesselHUDView] {name} ability icon slot {i} is bound to " +
                                     $"'{binding.element}' but the fleet order is " +
                                     $"charge/mass/space/time (expected '{expected}').", this);
                    return;
                }

                if (!binding.icon) continue;

                float x = binding.icon.rectTransform.position.x;
                if (x < previousX)
                    Debug.LogWarning($"[VesselHUDView] {name} ability icon '{binding.element}' sits LEFT of " +
                                     "the previous slot. Icons must run charge -> mass -> space -> time, " +
                                     "left to right, matching the element flowers above them.", this);
                previousX = x;
            }
        }

        private void EnsureCanvasGroup()
        {
            if (_canvasGroup) return;
            _canvasGroup = GetComponent<CanvasGroup>();
            if (!_canvasGroup)
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        protected virtual void OnValidate()
        {
            // Rigor: the list itself is the ordering contract, so keep it canonical in the inspector.
            if (abilityIcons == null || abilityIcons.Count < 2) return;
            abilityIcons.Sort((a, b) => OrderIndex(a.element).CompareTo(OrderIndex(b.element)));
        }

        // One report per vessel class, not per spawn.
        static readonly HashSet<VesselClassType> _missingRowReported = new();

        static int OrderIndex(Element element)
        {
            for (int i = 0; i < AbilityDisplayOrder.Length; i++)
                if (AbilityDisplayOrder[i] == element) return i;
            return AbilityDisplayOrder.Length; // unmapped slots sink to the end
        }

        protected virtual void OnDestroy()
        {
            _fadeTween?.Kill();
            foreach (var tween in _abilityIconTweens.Values)
                tween?.Kill();
            _abilityIconTweens.Clear();
            foreach (var tween in _abilityBadgeTweens.Values)
                tween?.Kill();
            _abilityBadgeTweens.Clear();
        }
    }
}
