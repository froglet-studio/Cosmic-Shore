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
                     "sprite - the lockup card still marks the upgrade either way.")]
            public Sprite upgradedSprite;

            [Tooltip("Optional meter for this ability - energy, heat, charge, cooldown. The lockup " +
                     "re-homes it into the card and restyles it as the fleet's one gauge: a linear " +
                     "fill rising through the icon's cell. The vessel keeps writing fillAmount, so " +
                     "no gameplay wiring changes; only where and how it draws does.")]
            public Image gauge;
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

        [Tooltip("Persistent scale an upgraded ability icon rests at while the upgrade is active.")]
        [SerializeField] private float upgradeHighlightScale = 1.15f;
        [Tooltip("Scale punch played when an ability upgrade unlocks.")]
        [SerializeField] private float upgradePunchScale = 1.35f;
        [SerializeField] private float upgradePunchDuration = 0.35f;

        readonly Dictionary<Element, Sprite>  _abilityIconRestSprites = new();
        readonly Dictionary<Element, Tween>   _abilityIconTweens      = new();
        readonly HashSet<Element>             _upgraded               = new();

        [Header("Ability lockup")]
        [Tooltip("Composes the totem card - plate, element flower, gauge, rim and bloom - around " +
                 "each ability icon, and owns the whole row's position, pitch and icon size. " +
                 "STRUCTURAL, not optional: VesselHUDController.Initialize adds one if a prefab " +
                 "lacks it, so leaving this empty is fine. See Docs/ABILITY_LOCKUP.md.")]
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
        /// <summary>The meter this ability drives, if the vessel wired one.</summary>
        public bool TryGetAbilityGauge(Element element, out Image gauge)
        {
            foreach (var binding in abilityIcons)
            {
                if (binding.element != element || !binding.gauge) continue;
                gauge = binding.gauge;
                return true;
            }
            gauge = null;
            return false;
        }

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
        /// The fleet's one "this ability is firing" signal: the lockup card lights while the
        /// control is held and decays on release. Replaces the per-vessel circular glow that used
        /// to be switched on behind the icon - that chrome is retired by the lockup, and drawing a
        /// second shape for a state the card can carry itself is exactly the divergence the totem
        /// exists to remove.
        /// </summary>
        public void SetAbilityPressed(Element element, bool pressed)
        {
            var lockups = ResolveAbilityLockups();
            if (lockups) lockups.SetAbilityPressed(element, pressed);
        }

        /// <summary>
        /// The fleet's ONE recharge readout: a radial veil swept over the ability plate while the
        /// ability recovers, ending in a flash when it comes back. <paramref name="remaining01"/>
        /// is 1 the instant it fires and 0 when it is ready.
        ///
        /// <para>A VALUE, not an <c>Image</c> binding like the gauge - a cooldown has no per-vessel
        /// artwork worth preserving, so the lockup owns the whole presentation and a vessel supplies
        /// one float. Deliberately radial where the gauge is linear, and OVER the icon where the
        /// gauge is behind it: a card can then show both without the two reading as one meter.</para>
        /// </summary>
        public void SetAbilityCooldown(Element element, float remaining01)
        {
            var lockups = ResolveAbilityLockups();
            if (lockups) lockups.SetAbilityCooldown(element, remaining01);
        }

        /// <summary>
        /// Where this element's control chip belongs on the lockup card. The control-hint binder
        /// places its (LT)/(RT) glyph HERE at zero offset rather than at a per-vessel offset from
        /// the icon - which is what locks the label TO the totem instead of leaving it floating
        /// near one. Returns false only on a HUD that has no lockup at all.
        /// </summary>
        public bool TryGetAbilityChipSocket(Element element, out RectTransform socket)
        {
            var lockups = ResolveAbilityLockups();
            if (lockups) return lockups.TryGetChipSocket(element, out socket);
            socket = null;
            return false;
        }

        /// <summary>
        /// The lockup's icon KERNING - how much of the ability cell the icon fills. 1 when this HUD
        /// has no lockup, so an unstyled vessel is unaffected.
        /// </summary>
        protected float AbilityIconContentScale(Element element)
        {
            var lockups = ResolveAbilityLockups();
            return lockups ? lockups.IconContentScale(element) : 1f;
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
            => Vector3.one * (AbilityIconContentScale(element) * (IsAbilityUpgraded(element) ? upgradeHighlightScale : 1f));

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
        /// The signal lives on the CARD - its rim crosses to the level-5 white and a bloom comes up
        /// behind the plate (<see cref="AbilityLockupView"/>). Here we only add the two things that
        /// belong to the icon itself: the authored upgraded art, when a vessel supplies any, and a
        /// persistent scale bump with a one-shot punch.
        ///
        /// <para>Deliberately NOT an icon tint or a corner badge. Both existed before the lockup and
        /// are now a second and third way to say the same thing - and the tint could never be used
        /// by a vessel whose icons are live gauges, which is most of them.</para>
        /// </summary>
        public virtual void SetAbilityUpgraded(Element element, bool upgraded)
        {
            if (upgraded) _upgraded.Add(element);
            else          _upgraded.Remove(element);

            foreach (var binding in abilityIcons)
            {
                if (binding.element != element || !binding.icon) continue;

                if (!_abilityIconRestSprites.ContainsKey(element))
                    _abilityIconRestSprites[element] = binding.icon.sprite;

                if (_abilityIconTweens.TryGetValue(element, out var tween))
                    tween?.Kill();

                if (upgraded)
                {
                    // The icon itself changes when the vessel authored upgraded art for the slot.
                    if (binding.upgradedSprite)
                        binding.icon.sprite = binding.upgradedSprite;

                    // Rest at the highlight scale (survives views that repaint colors per-frame),
                    // with a one-shot punch around it to telegraph the unlock. Both go through
                    // AbilityIconRestScale so the lockup's kerning is never re-derived here.
                    binding.icon.rectTransform.localScale = AbilityIconRestScale(element);
                    _abilityIconTweens[element] = binding.icon.rectTransform
                        .DOPunchScale(Vector3.one * ((upgradePunchScale - upgradeHighlightScale) * AbilityIconContentScale(element)),
                            upgradePunchDuration, 1, 0.5f)
                        .SetUpdate(true)
                        .SetLink(binding.icon.gameObject);
                }
                else
                {
                    if (binding.upgradedSprite && _abilityIconRestSprites[element])
                        binding.icon.sprite = _abilityIconRestSprites[element];

                    binding.icon.rectTransform.localScale = AbilityIconRestScale(element);
                }
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

        /// <summary>
        /// Guarantees this HUD wears the ability lockup. Called from
        /// <see cref="VesselHUDController.Initialize"/> - the one method every vessel HUD routes
        /// through on every spawn path - so the style is STRUCTURAL rather than something a prefab
        /// can be authored without.
        ///
        /// <para>The row is built even when the vessel binds NO icons: it is always four cards, and a
        /// slot whose ability does not exist yet renders LOCKED rather than being absent. That is
        /// what stops a vessel like the Rhino - one named ability, three open design slots - from
        /// simply keeping the old UI while it waits for design.</para>
        ///
        /// <para>Idempotent. The component is added rather than warned about because the lockup is
        /// pure composition over icons that are already authored - there is no per-vessel art or
        /// wiring for a human to supply, so a warning would only ever be noise telling someone to
        /// click Add Component.</para>
        /// </summary>
        public void EnsureAbilityLockup()
        {
            var lockups = ResolveAbilityLockups();
            if (!lockups)
            {
                lockups = gameObject.AddComponent<AbilityLockupView>();
                abilityLockups = lockups;
            }

            // Build now rather than waiting on Awake: a component added this frame has not Awoken,
            // and a HUD that starts inactive would not Awake until it is first shown - which is
            // after the controller seeds the upgrade state through SetAbilityUpgraded.
            lockups.Build();
        }


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
        }
    }
}
