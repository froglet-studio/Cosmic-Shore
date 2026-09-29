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
        /// A <b>non-elemental</b> ability's bindings. Same two visuals as an elemental one - an icon
        /// and an optional meter - and no <see cref="Element"/>, because nothing upgrades it.
        ///
        /// <para>Above the plate the card carries an <see cref="emblem"/> if this binding names
        /// one and NOTHING if it does not, which is the difference between the two cards the fleet
        /// has: the Squirrel's drift is a bare plate, the omni crystal's is a plate under the
        /// crystal's own mark. It is never a flower - a flower is a LEVEL readout and there is no
        /// element here to level.</para>
        /// </summary>
        [Serializable]
        public struct CoreAbilityBinding
        {
            [Tooltip("Which core ability this is. Fixes the card's place in the row - see " +
                     "CoreAbilityDisplayOrder - and is the key every SetCoreAbility* call uses.")]
            public CoreAbility ability;

            [Tooltip("The ability's icon Image. Its PARENT is re-homed into the row as the card's " +
                     "host, exactly as an elemental icon's is, so the button, its touch target and " +
                     "any gauge children travel together.")]
            public Image icon;

            [Tooltip("Optional meter for this ability. Re-homed into the card and restyled as the " +
                     "fleet's one gauge; the vessel keeps writing fillAmount on the same Image.")]
            public Image gauge;

            [Tooltip("Optional mark for the card's UPPER plate, where an elemental card carries " +
                     "its element flower. Leave empty and the card has no upper cell at all - " +
                     "which is the drift's shape. The omni crystal card names the crystal's own " +
                     "emblem here, and it is deliberately NOT domain-tinted: an omni crystal " +
                     "belongs to nobody until somebody takes it.")]
            public Image emblem;

            [Tooltip("The control this ability is bound to, for the card's control chip. An " +
                     "ELEMENTAL card takes this from the vessel's ElementalAbilityMapSO entry; a " +
                     "core ability has no map entry, so the binding names it here. It is still ONE " +
                     "authored fact - which control - and the glyph is derived from it, so a wrong " +
                     "label stays structurally impossible. FullSpeedStraightAction (0) draws no chip.")]
            public InputEvents input;
        }

        /// <summary>
        /// The fleet-wide ability slot order, left to right. It is the element display order used by
        /// <see cref="ElementalBarsView"/> (charge, mass, space, time), so an ability icon always sits
        /// in the same column as the element flower that upgrades it. Do not reorder.
        /// </summary>
        public static readonly Element[] AbilityDisplayOrder =
            { Element.Charge, Element.Mass, Element.Space, Element.Time };

        /// <summary>
        /// The fleet-wide NON-elemental ability order, and they sit to the LEFT of the four elemental
        /// cards - the last entry nearest Charge, so adding one pushes the set further left and never
        /// disturbs the elemental columns. That separation is the whole point: the elemental row must
        /// go on reading left-to-right as charge / mass / space / time with nothing interleaved, or
        /// "which flower upgrades this?" stops being answered by position.
        /// </summary>
        /// <para>The omni crystal sits LAST, i.e. nearest Charge, and that is a layout argument
        /// rather than a preference: it is the one non-elemental card with an upper cell, so
        /// putting it against the elemental row makes the upper cells one continuous band and
        /// leaves the drift's bare plate at the far end. Between them the band would have a hole
        /// in it.</para>
        public static readonly CoreAbility[] CoreAbilityDisplayOrder =
            { CoreAbility.Drift, CoreAbility.OmniCrystal };

        [Header("Button highlights")] public List<HighlightBinding> highlights = new();

        [Header("Ability icons (elemental upgrade highlight)")]
        [Tooltip("Exactly four entries - one per ability - keyed by the element that upgrades it (per " +
                 "the vessel's ElementalAbilityMapSO). The list is kept in charge/mass/space/time order " +
                 "and the icons must sit in that same order, left to right, in the lower-right row. " +
                 "Shared system - every vessel HUD wires its own four icons.")]
        public List<AbilityIconBinding> abilityIcons = new();

        [Header("Non-elemental abilities (cards to the LEFT of the elemental row)")]
        [Tooltip("Abilities no element upgrades - the hull's own engine. Each gets a lockup card " +
                 "with an ability plate and NO element flower above it, placed left of the four " +
                 "elemental cards. Empty on most vessels; the Squirrel binds its drift here.")]
        public List<CoreAbilityBinding> coreAbilities = new();

        [Header("Omni crystal card (structural - EVERY vessel has one)")]
        [Tooltip("What THIS hull does when it flies through an omni crystal, as a picture. The " +
                 "card itself is not optional and is not authored here - the lockup draws it on " +
                 "every vessel, with the crystal's own emblem in its upper plate, because every " +
                 "vessel can fly through a crystal. This is only the LOWER plate's art.\n\n" +
                 "Leave it EMPTY on a hull whose omni crystal has no picture yet and the card " +
                 "renders LOCKED, which is the honest state - an ability that does not exist is " +
                 "not the same as one the player has not unlocked, and the locked card says the " +
                 "first. Two hulls will always be empty here: the Urchin's crystal branch is " +
                 "haptics only, and the Scarab's is empty by design because its SKIMMER forges " +
                 "the crystal into a ball before the hull reaches it.")]
        [SerializeField] private Sprite omniAbilitySprite;

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

        /// <summary>The icon of a non-elemental ability, if the vessel wired one.</summary>
        public bool TryGetCoreAbilityIcon(CoreAbility ability, out Image icon)
        {
            foreach (var binding in coreAbilities)
            {
                if (binding.ability != ability || !binding.icon) continue;
                icon = binding.icon;
                return true;
            }
            icon = null;
            return false;
        }

        /// <summary>The upper-plate mark of a non-elemental ability, if the vessel authored one.</summary>
        public bool TryGetCoreAbilityEmblem(CoreAbility ability, out Image emblem)
        {
            foreach (var binding in coreAbilities)
            {
                if (binding.ability != ability || !binding.emblem) continue;
                emblem = binding.emblem;
                return true;
            }
            emblem = null;
            return false;
        }

        /// <summary>The meter of a non-elemental ability, if the vessel wired one.</summary>
        public bool TryGetCoreAbilityGauge(CoreAbility ability, out Image gauge)
        {
            foreach (var binding in coreAbilities)
            {
                if (binding.ability != ability || !binding.gauge) continue;
                gauge = binding.gauge;
                return true;
            }
            gauge = null;
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
        /// A ONE-SHOT flash of an ability's card — lit on this frame, decaying on its own.
        /// The pass-through twin of <see cref="SetAbilityPressed"/>, for an ability that has no
        /// held control to release: anything that HAPPENS rather than anything that is being
        /// held. (The Manta's whole kit is that shape — nothing it does is a button.)
        /// </summary>
        public void PlayAbilityFlash(Element element)
        {
            var lockups = ResolveAbilityLockups();
            if (lockups) lockups.PlayPressFlash(element);
        }

        /// <summary>
        /// Which physical control fires this ability, so the lockup can draw the control chip from
        /// the fleet's one glyph set instead of from per-vessel authored artwork.
        /// </summary>
        public void SetAbilityControl(Element element, InputEvents input)
        {
            var lockups = ResolveAbilityLockups();
            if (lockups) lockups.SetAbilityControl(element, input);
        }

        /// <summary>Which device the control chips should speak for.</summary>
        public void SetControlDevice(bool keyboard)
        {
            var lockups = ResolveAbilityLockups();
            if (lockups) lockups.SetControlDevice(keyboard);
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
        /// The same three pushes for a NON-elemental card. There is deliberately no
        /// <c>SetCoreAbilityUpgraded</c> - an upgrade is an element reaching level 5, and a core
        /// ability has no element, so there is nothing that could ever raise it.
        /// </summary>
        public void SetCoreAbilityCooldown(CoreAbility ability, float remaining01)
        {
            var lockups = ResolveAbilityLockups();
            if (lockups) lockups.SetCoreAbilityCooldown(ability, remaining01);
        }

        public void SetCoreAbilityPressed(CoreAbility ability, bool pressed)
        {
            var lockups = ResolveAbilityLockups();
            if (lockups) lockups.SetCoreAbilityPressed(ability, pressed);
        }

        public void SetCoreAbilityControl(CoreAbility ability, InputEvents input)
        {
            var lockups = ResolveAbilityLockups();
            if (lockups) lockups.SetCoreAbilityControl(ability, input);
        }

        /// <summary>
        /// Hands every NON-elemental card the control its binding names. An elemental card takes
        /// that from the vessel's <c>ElementalAbilityMapSO</c> entry, which is where an elemental
        /// ability's input lives; a core ability has no map entry at all, so the binding carries
        /// it. Still ONE authored fact per card, with the glyph derived from it.
        ///
        /// <para><c>FullSpeedStraightAction</c> (the enum's zero) means "no button", exactly as it
        /// does on the elemental side, and draws a blank chip.</para>
        /// </summary>
        public void SeedCoreAbilityControls()
        {
            for (int i = 0; i < coreAbilities.Count; i++)
                SetCoreAbilityControl(coreAbilities[i].ability, coreAbilities[i].input);
        }

        /// <summary>
        /// A vessel's chance to BUILD an ability icon rather than author one, called by
        /// <see cref="AbilityLockupView.Build"/> before the row is laid out.
        ///
        /// <para>It exists because some readouts are a live MEASUREMENT rather than a picture - the
        /// Squirrel's skimmer reach, the Dolphin's blast profile - and a measurement drawn as a
        /// sprite ladder quantizes it and silently stops matching the thing it depicts. A generated
        /// icon is authored by nobody, so there is no prefab object for the row to find; this is
        /// where the vessel makes one and writes it into <see cref="abilityIcons"/>.</para>
        ///
        /// <para>An override must be IDEMPOTENT (the lockup may rebuild) and must create its host
        /// somewhere other than the row - the lockup re-homes it into the row itself. Default:
        /// nothing at all, so every other vessel is byte-for-byte unchanged.</para>
        /// </summary>
        public virtual void EnsureGeneratedAbilityIcons() { }

        /// <summary>
        /// Builds the OMNI CRYSTAL card's lower-plate icon, on every vessel, from the one sprite
        /// that vessel authors for it. Called by <see cref="AbilityLockupView.Build"/> beside
        /// <see cref="EnsureGeneratedAbilityIcons"/>.
        ///
        /// <para>It is NOT virtual and NOT opt-in, for the same reason the lockup itself is not: a
        /// card a vessel can forget is a card most vessels will be missing, and every vessel can
        /// fly through a crystal. The card's UPPER plate is drawn by the lockup from the fleet-wide
        /// style, so a hull that authors nothing here still gets the crystal emblem and a LOCKED
        /// plate under it.</para>
        ///
        /// <para>Idempotent, and the host is created OUTSIDE the row - the lockup re-homes it -
        /// so a rebuild finds what it made last time.</para>
        /// </summary>
        public void EnsureOmniCrystalCard()
        {
            if (!omniAbilitySprite || _omniAbilityIcon) return;

            var host = transform.Find(OmniHostName) as RectTransform;
            if (!host)
            {
                host = new GameObject(OmniHostName, typeof(RectTransform))
                    .GetComponent<RectTransform>();
                host.SetParent(transform, false);
            }

            _omniAbilityIcon = ResolveGeneratedChild<Image>(host, "OmniCrystalIcon");
            var rt = _omniAbilityIcon.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(AuthoredOmniIconSize, AuthoredOmniIconSize);
            _omniAbilityIcon.sprite = omniAbilitySprite;
            _omniAbilityIcon.raycastTarget = false;
            _omniAbilityIcon.color = _omniTint.a > 0f ? _omniTint : Color.white;

            BindCoreAbilityIcon(CoreAbility.OmniCrystal, _omniAbilityIcon);
        }

        /// <summary>
        /// Tints the omni crystal card's LOWER icon - what this hull's crystal pickup leaves
        /// behind. Pushed by the vessel's controller rather than read here, because a view holds no
        /// <c>GameDataSO</c>; the same shape as every other palette push on this HUD.
        ///
        /// <para>Alpha 0 means "the palette authors no such colour", and the icon then KEEPS its
        /// white rather than being painted black - a black icon reads as "not implemented" where a
        /// white one reads as untinted (<c>Docs/PALETTE.md §2.4</c>).</para>
        ///
        /// <para>The EMBLEM above it is deliberately untouched by this. The emblem is the crystal,
        /// which belongs to nobody; the icon is what YOUR hull makes of it.</para>
        /// </summary>
        public void SetOmniAbilityTint(Color tint)
        {
            _omniTint = tint;
            if (!_omniAbilityIcon || tint.a <= 0f) return;

            // A collect flash captured the OLD tint as its end value; a repaint mid-flash must
            // not be undone by it settling, so the flash yields and the new tint lands now.
            _omniColorTween?.Kill();
            _omniAbilityIcon.color = tint;
        }

        [Header("Omni crystal card - collect juice")]
        [Tooltip("Scale the omni card's icon punches to when this hull collects an omni crystal.")]
        [SerializeField] private float omniCollectPunchScale = 1.35f;
        [Tooltip("How long the punch and the white flash take to settle back to rest.")]
        [SerializeField] private float omniCollectDuration = 0.4f;
        [Tooltip("How far toward white the icon flashes on a collect (0 = no colour flash).")]
        [SerializeField, Range(0f, 1f)] private float omniCollectWhiteMix = 0.75f;

        /// <summary>
        /// This hull just COLLECTED an omni crystal - say so on the card that pictures what the
        /// pickup does. Two beats, on two objects, so neither overwrites the other: the lockup's own
        /// one-shot press flash lights the card's ability plate (the fleet's "this fired" signal, the
        /// same one a button press draws), and the icon itself punches and flashes toward white
        /// before settling back to its tint. Both decay rather than switch off.
        ///
        /// <para>A pickup is CONTACT rather than a button, which is why this is pushed by the
        /// vessel's controller off the crystal event rather than resolved from an input like a
        /// press - the card is bound to no input at all.</para>
        ///
        /// <para>Safe on a hull whose omni card has no art yet: the plate still flashes and there is
        /// no icon to punch.</para>
        /// </summary>
        public void PlayOmniCrystalCollected()
        {
            var lockups = ResolveAbilityLockups();
            if (lockups) lockups.PlayCoreAbilityFlash(CoreAbility.OmniCrystal);

            if (!_omniAbilityIcon) return;

            var rt = _omniAbilityIcon.rectTransform;
            var rest = CoreAbilityIconRestScale(CoreAbility.OmniCrystal);
            _omniScaleTween?.Kill();
            rt.localScale = rest * omniCollectPunchScale;
            _omniScaleTween = rt.DOScale(rest, omniCollectDuration)
                                .SetEase(Ease.OutBack)
                                .SetLink(rt.gameObject);

            Color settle = _omniTint.a > 0f ? _omniTint : Color.white;
            _omniColorTween?.Kill();
            _omniAbilityIcon.color = Color.Lerp(settle, Color.white, omniCollectWhiteMix);
            _omniColorTween = _omniAbilityIcon.DOColor(settle, omniCollectDuration)
                                              .SetEase(Ease.OutQuad)
                                              .SetLink(_omniAbilityIcon.gameObject);
        }

        private Tween _omniScaleTween;
        private Tween _omniColorTween;

        const string OmniHostName = "OmniCrystalButton";

        // The fleet's authored icon size. The lockup kerns whatever it finds to iconBoxSize, so
        // this only has to be the size the row's other icons are authored at - not a magic number
        // the card depends on.
        const float AuthoredOmniIconSize = 80f;

        private Image _omniAbilityIcon;
        private Color _omniTint = new Color(0f, 0f, 0f, 0f);

        /// <summary>
        /// Points a NON-elemental card at an icon built at runtime. The core-ability twin of
        /// <see cref="BindGeneratedAbilityIcon"/>, with the same rule: authored art always wins.
        /// </summary>
        protected void BindCoreAbilityIcon(CoreAbility ability, Image icon)
        {
            if (!icon) return;

            for (int i = 0; i < coreAbilities.Count; i++)
            {
                if (coreAbilities[i].ability != ability) continue;
                if (coreAbilities[i].icon) return;         // authored art wins
                var binding = coreAbilities[i];
                binding.icon = icon;
                coreAbilities[i] = binding;
                return;
            }

            // FullSpeedStraightAction is the input enum's zero and the project's passive sentinel:
            // collecting a crystal is contact, so this card draws no control chip.
            coreAbilities.Add(new CoreAbilityBinding
            {
                ability = ability,
                icon = icon,
                input = InputEvents.FullSpeedStraightAction,
            });
        }

        /// <summary>
        /// Find-or-create a named child carrying <typeparamref name="T"/>. Shared by every
        /// generated HUD readout so a rebuild is idempotent by NAME rather than by a cached
        /// reference that a domain reload drops.
        /// </summary>
        protected static T ResolveGeneratedChild<T>(RectTransform parent, string name)
            where T : Component
        {
            var existing = parent.Find(name);
            var found = existing ? existing.GetComponent<T>() : null;
            if (found) return found;

            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            go.transform.SetParent(parent, false);
            return go.GetComponent<T>();
        }

        /// <summary>
        /// Points an ELEMENT's card at an icon built at runtime, adding the entry when the vessel
        /// authored none. The counterpart of <see cref="EnsureGeneratedAbilityIcons"/>; an authored
        /// icon always wins, so this can never overwrite a prefab's own art.
        /// </summary>
        protected void BindGeneratedAbilityIcon(Element element, Image icon)
        {
            if (!icon) return;

            for (int i = 0; i < abilityIcons.Count; i++)
            {
                if (abilityIcons[i].element != element) continue;
                if (abilityIcons[i].icon) return;          // authored art wins
                var binding = abilityIcons[i];
                binding.icon = icon;
                abilityIcons[i] = binding;
                return;
            }

            abilityIcons.Add(new AbilityIconBinding { element = element, icon = icon });
        }

        /// <summary>
        /// A non-elemental card's icon KERNING, and the rest scale that goes with it. No upgrade
        /// term - see <see cref="SetCoreAbilityCooldown"/>.
        /// </summary>
        protected Vector3 CoreAbilityIconRestScale(CoreAbility ability)
        {
            var lockups = ResolveAbilityLockups();
            return Vector3.one * (lockups ? lockups.CoreIconContentScale(ability) : 1f);
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

            // The NON-elemental cards come first in the same left-to-right walk, because "left of
            // the elemental row" is the whole layout contract: interleave one and the elemental
            // columns stop answering "which flower upgrades this?" by position alone.
            for (int i = 0; i < CoreAbilityDisplayOrder.Length; i++)
            {
                if (!TryGetCoreAbilityIcon(CoreAbilityDisplayOrder[i], out var coreIcon)) continue;

                float coreX = coreIcon.rectTransform.position.x;
                if (coreX < previousX)
                    Debug.LogWarning($"[VesselHUDView] {name} core ability '{CoreAbilityDisplayOrder[i]}' " +
                                     "sits LEFT of an earlier core card. Core cards run in " +
                                     "CoreAbilityDisplayOrder, left to right, and all of them sit left " +
                                     "of the four elemental cards.", this);
                previousX = coreX;
            }

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
            _omniScaleTween?.Kill();
            _omniColorTween?.Kill();
            foreach (var tween in _abilityIconTweens.Values)
                tween?.Kill();
            _abilityIconTweens.Clear();
        }
    }
}
