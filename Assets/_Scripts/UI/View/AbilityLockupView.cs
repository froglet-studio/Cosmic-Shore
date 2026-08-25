using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Builds the fleet-wide ABILITY LOCKUP - the "totem" card that fuses each ability with the
    /// element indicator that upgrades it - and OWNS the row it sits in.
    ///
    /// <para><b>The row is always four cards.</b> A slot whose ability does not exist yet renders
    /// LOCKED rather than being absent, so a vessel part-way through design (the Rhino: one named
    /// ability, three open slots) still wears the fleet's UI and reads honestly, instead of keeping
    /// the old one until its abilities are finished.</para>
    ///
    /// <para><b>It owns geometry the prefabs used to disagree on</b> - row position, pitch, cell
    /// size, host scale, icon size - all read from one <see cref="AbilityLockupStyleSO"/> and
    /// written onto every vessel. It also retires the chrome the card replaces: the legacy decagon
    /// plate, the circular press glow, and the chevron gauge frame.</para>
    ///
    /// <para><b>What it never does is re-author a vessel's content.</b> The ability icon, the element
    /// flower and the gauge are the vessel's own objects, re-homed and re-scaled. Nothing is
    /// duplicated, so every gameplay script keeps driving the same references it always did.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class AbilityLockupView : MonoBehaviour
    {
        [Header("Style (shared spec - single source of truth)")]
        [Tooltip("Loaded from Resources/AbilityLockupStyle when empty.")]
        [SerializeField] private AbilityLockupStyleSO style;
        [SerializeField] private string styleResourcePath = "AbilityLockupStyle";

        [Header("Bindings (resolved from siblings when empty)")]
        [Tooltip("The vessel HUD view whose abilityIcons this decorates. Defaults to this GameObject's.")]
        [SerializeField] private VesselHUDView hudView;
        [Tooltip("The element flowers to dock into the cards. Found on this HUD, or created here.")]
        [SerializeField] private ElementalBarsView elementBars;

        sealed class Slot
        {
            public RectTransform Card;
            public Image Bloom;
            public Image Plate;
            public Image Rim;
            public Image Divider;
            public Image GaugeTrack;
            public Image Flash;
            public RectTransform FlowerSocket;
            public RectTransform ChipSocket;
            public Tween Tween;
            public Tween FlashTween;
            public bool Locked;
            public bool Upgraded;
        }

        readonly Dictionary<Element, Slot> _slots = new();
        bool _built;

        public bool IsBuilt => _built;

        /// <summary>
        /// The element-bar view whose flowers are docked into these cards. Non-null once
        /// <see cref="Build"/> has run; the vessel's ElementalBarsController adopts THIS rather
        /// than creating the fleet-standard row on top of it.
        /// </summary>
        public ElementalBarsView ElementBars => elementBars;

        void Awake() => Build();

        /// <summary>
        /// The scale that draws THIS element's icon at the fleet's one drawn size, whatever size the
        /// vessel's prefab authored for it. Per-element because icon sizes differ WITHIN a vessel as
        /// well as between them (the Dolphin authors 80 on three slots and 96 on its fourth).
        ///
        /// <para>Readable before <see cref="Build"/> has laid the row out - the authored size comes
        /// straight off the icon's rect - because per-vessel views capture their rest scales during
        /// Initialize.</para>
        /// </summary>
        public float IconContentScale(Element element)
        {
            var s = ResolveStyle();
            if (!s) return 1f;

            if (!hudView) hudView = GetComponent<VesselHUDView>();
            if (!hudView || !hudView.TryGetAbilityIcon(element, out var icon) || !icon) return 1f;

            return s.IconScaleFor(AuthoredIconSize(icon.rectTransform));
        }

        /// <summary>
        /// The size a vessel authored its icon at. A prefab rect is not laid out, so a point-anchored
        /// icon's size is its sizeDelta; a stretch-anchored one falls back to whatever rect it has.
        /// </summary>
        static float AuthoredIconSize(RectTransform rt)
        {
            var size = rt.anchorMin == rt.anchorMax ? rt.sizeDelta : rt.rect.size;
            if (size.sqrMagnitude < 1f) size = rt.sizeDelta;
            return Mathf.Max(size.x, size.y);
        }

        /// <summary>The flower socket for an element, if a card was built for it.</summary>
        public RectTransform GetFlowerSocket(Element element)
            => _slots.TryGetValue(element, out var slot) ? slot.FlowerSocket : null;

        /// <summary>
        /// Where this card's control chip belongs. The hint binder places its (LT)/(RT) glyph HERE
        /// rather than at a per-vessel offset from the icon, which is what finally locks the label
        /// to the totem instead of leaving it floating near it.
        /// </summary>
        public bool TryGetChipSocket(Element element, out RectTransform socket)
        {
            socket = _slots.TryGetValue(element, out var slot) ? slot.ChipSocket : null;
            return socket;
        }

        /// <summary>True when this slot's ability does not exist yet (no icon bound).</summary>
        public bool IsSlotLocked(Element element)
            => _slots.TryGetValue(element, out var slot) && slot.Locked;

        // ---------------------------------------------------------------
        // Build
        // ---------------------------------------------------------------

        public void Build()
        {
            if (_built) return;

            ResolveStyle();
            if (!style)
            {
                Debug.LogError($"[AbilityLockupView] No AbilityLockupStyleSO assigned and none found at " +
                               $"Resources/{styleResourcePath}. The ability lockups cannot be built.", this);
                return;
            }

            if (!hudView) hudView = GetComponent<VesselHUDView>();
            if (!hudView)
            {
                Debug.LogWarning("[AbilityLockupView] No VesselHUDView on this GameObject - there are no " +
                                 "ability icons to build lockups around.", this);
                return;
            }

            var row = ResolveRow();
            var order = VesselHUDView.AbilityDisplayOrder;

            // Three passes, and the SEPARATION is load-bearing. A vessel's meter is not always
            // authored on the card its ability belongs to (the Squirrel's boost fill sits under the
            // skimming button; the Scarab's ball-energy ring under the throttle button), so a gauge
            // is frequently adopted ONTO one card OUT OF another card's host. Retiring chrome in the
            // same pass would therefore deactivate a meter a later element was about to claim -
            // silently, and only on the vessels whose authoring drifted.
            var hosts = new Dictionary<Element, RectTransform>();
            var icons = new Dictionary<Element, Image>();

            for (int i = 0; i < order.Length; i++)
            {
                var element = order[i];
                bool hasIcon = hudView.TryGetAbilityIcon(element, out var icon) && icon;

                var host = hasIcon
                    ? icon.rectTransform.parent as RectTransform
                    : ResolveLockedHost(row, element);
                if (!host || host == row) continue;

                PlaceHost(row, host, i, order.Length);

                var slot = BuildSlot(element, host, hasIcon ? icon : null);
                if (slot == null) continue;

                if (hasIcon) NormaliseIcon(icon.rectTransform);

                hosts[element] = host;
                if (hasIcon) icons[element] = icon;
                _slots[element] = slot;
            }

            foreach (var pair in _slots) AdoptGauge(pair.Key, pair.Value);

            foreach (var pair in _slots)
            {
                if (!hosts.TryGetValue(pair.Key, out var host)) continue;
                icons.TryGetValue(pair.Key, out var icon);
                RetireLegacyChrome(host, pair.Value, icon);
            }

            DockElementFlowers();
            _built = true;
        }

        /// <summary>
        /// The one container every lockup card hangs off, pinned to the screen's bottom-right corner
        /// by the shared style. Anchoring the ROW rather than each card is what makes the row's
        /// position and pitch identical on every vessel: a prefab's own anchors stop mattering.
        /// </summary>
        RectTransform ResolveRow()
        {
            const string rowName = "AbilityLockupRow";
            var self = (RectTransform)transform;
            var row = self.Find(rowName) as RectTransform;
            if (!row)
            {
                var go = new GameObject(rowName, typeof(RectTransform));
                row = (RectTransform)go.transform;
                row.SetParent(self, false);
            }

            row.anchorMin = row.anchorMax = row.pivot = new Vector2(1f, 0f);
            row.sizeDelta = Vector2.zero;
            row.anchoredPosition = new Vector2(-style.rowMarginRight, style.rowMarginBottom);
            row.localScale = Vector3.one;
            row.localRotation = Quaternion.identity;
            return row;
        }

        /// <summary>A host for a slot the vessel has no ability for yet.</summary>
        RectTransform ResolveLockedHost(RectTransform row, Element element)
        {
            string name = $"LockedSlot_{element}";
            var host = row.Find(name) as RectTransform;
            if (host) return host;

            var go = new GameObject(name, typeof(RectTransform));
            host = (RectTransform)go.transform;
            host.SetParent(row, false);
            return host;
        }

        /// <summary>
        /// Re-homes the vessel's own ability button into the row at the fleet slot position, and
        /// normalises the things a prefab is otherwise free to disagree on: cell size and scale.
        ///
        /// <para>The HOST is moved rather than the icon so that the button, its touch target, its
        /// press juice and any gauge children all travel together and keep working.</para>
        /// </summary>
        void PlaceHost(RectTransform row, RectTransform host, int index, int count)
        {
            if (host.parent != row) host.SetParent(row, false);

            host.anchorMin = host.anchorMax = host.pivot = new Vector2(0.5f, 0.5f);
            host.sizeDelta = new Vector2(style.plateWidth, style.abilityCellHeight);
            host.localScale = Vector3.one;
            host.localRotation = Quaternion.identity;
            host.anchoredPosition = new Vector2(
                -(count - 1 - index) * style.cardPitch - style.plateWidth * 0.5f,
                style.abilityCellHeight * 0.5f);
            host.SetSiblingIndex(index);
        }

        void NormaliseIcon(RectTransform iconRT)
        {
            iconRT.anchorMin = iconRT.anchorMax = iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;
        }

        Slot BuildSlot(Element element, RectTransform host, Image icon)
        {
            string cardName = $"AbilityLockup_{element}";
            var card = host.Find(cardName) as RectTransform;
            if (!card)
            {
                var go = new GameObject(cardName, typeof(RectTransform));
                card = (RectTransform)go.transform;
                card.SetParent(host, false);
            }

            card.anchorMin = card.anchorMax = card.pivot = new Vector2(0.5f, 0.5f);
            card.localRotation = Quaternion.identity;
            card.localScale = Vector3.one;
            card.sizeDelta = new Vector2(style.plateWidth, style.PlateHeight);
            card.anchoredPosition = new Vector2(0f, style.CardCenterOffsetY);
            card.SetSiblingIndex(0);   // behind the icon; UGUI draws siblings in order

            var slot = new Slot { Card = card, Locked = !icon };

            slot.Bloom = ResolveChildImage(card, "Bloom", style.bloomSprite);
            StretchTo(slot.Bloom.rectTransform, style.bloomPadding);
            slot.Bloom.color = WithAlpha(style.bloomColor, 0f);   // nothing glows at rest

            slot.Plate = ResolveChildImage(card, "Plate", style.plateSprite);
            StretchTo(slot.Plate.rectTransform, 0f);
            slot.Plate.color = slot.Locked ? style.lockedPlateColor : style.plateColor;
            AdoptButtonTarget(host, slot.Plate);

            // The gauge track lines the ABILITY cell only, so the fill reads as the icon filling up.
            // It stays INVISIBLE until a meter is actually adopted onto this card (AdoptGauge turns
            // it on): an empty track under an ability that has no meter is a false affordance -
            // it reads as a gauge stuck at zero.
            slot.GaugeTrack = ResolveChildImage(card, "GaugeTrack", null);
            SetCellRect(slot.GaugeTrack.rectTransform, -style.petalCellHeight * 0.5f,
                        style.plateWidth, style.abilityCellHeight * style.gaugeCellFraction);
            slot.GaugeTrack.color = Color.clear;

            slot.Divider = ResolveChildImage(card, "Divider", null);
            var dRT = slot.Divider.rectTransform;
            dRT.anchorMin = dRT.anchorMax = dRT.pivot = new Vector2(0.5f, 0.5f);
            dRT.sizeDelta = new Vector2(style.plateWidth - style.dividerInset * 2f, style.dividerThickness);
            dRT.anchoredPosition = new Vector2(0f, style.DividerLocalY);
            slot.Divider.color = slot.Locked ? style.lockedMarkColor : style.dividerColor;

            if (slot.Locked) BuildLockedMark(card);

            slot.Rim = ResolveChildImage(card, "Rim", style.rimSprite);
            StretchTo(slot.Rim.rectTransform, 0f);
            slot.Rim.color = slot.Locked ? style.lockedMarkColor : style.hairlineColor;

            // Press flash sits ABOVE the plate and below the icon, so a press lights the card rather
            // than blooming a circle behind it.
            slot.Flash = ResolveChildImage(card, "PressFlash", style.plateSprite);
            StretchTo(slot.Flash.rectTransform, 0f);
            slot.Flash.color = WithAlpha(style.pressFlashColor, 0f);

            slot.FlowerSocket = ResolveFlowerSocket(card, element);
            slot.ChipSocket = ResolveChipSocket(card);
            return slot;
        }

        /// <summary>
        /// The placeholder an undesigned slot shows: a short bar where the icon would be. Deliberately
        /// not a padlock - the ability is not locked to the PLAYER, it does not exist yet.
        /// </summary>
        void BuildLockedMark(RectTransform card)
        {
            var mark = ResolveChildImage(card, "LockedMark", null);
            var rt = mark.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(style.iconBoxSize * 0.4f, style.dividerThickness * 2f);
            rt.anchoredPosition = new Vector2(0f, -style.petalCellHeight * 0.5f);
            mark.color = style.lockedMarkColor;
        }

        /// <summary>
        /// Re-homes the vessel's meter into the card and restyles it as the fleet's one gauge: a
        /// linear fill rising through the icon's cell, behind the icon. The vessel keeps writing
        /// <c>fillAmount</c> on the very same Image, so no gameplay wiring changes - only where and
        /// how it draws. The legacy chevron bar and its frame are retired with the rest of the
        /// host's chrome.
        /// </summary>
        void AdoptGauge(Element element, Slot slot)
        {
            if (slot.Locked || !hudView.TryGetAbilityGauge(element, out var gauge) || !gauge) return;

            var rt = gauge.rectTransform;
            if (rt.parent != slot.Card) rt.SetParent(slot.Card, false);

            SetCellRect(rt, -style.petalCellHeight * 0.5f,
                        style.plateWidth, style.abilityCellHeight * style.gaugeCellFraction);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.SetSiblingIndex(slot.GaugeTrack.rectTransform.GetSiblingIndex() + 1);

            slot.GaugeTrack.color = style.gaugeTrackColor;   // there IS a meter here now

            // Filled needs a sprite - Image draws a plain quad and ignores fillAmount when the
            // sprite is null - and the plate's own shape is the one the card already speaks.
            gauge.sprite = style.plateSprite;
            gauge.type = Image.Type.Filled;
            gauge.fillMethod = Image.FillMethod.Vertical;
            gauge.fillOrigin = (int)Image.OriginVertical.Bottom;
            gauge.preserveAspect = false;
            gauge.raycastTarget = false;
            gauge.color = style.gaugeFillColor;
        }

        /// <summary>
        /// Switches off everything the host drew that the card now replaces - the decagon
        /// <c>Ability Background Small</c>, the circular press glow, the chevron gauge frame, an
        /// undriven heat halo. The rule is positional rather than by name: a direct child of the
        /// host that is not the icon and not the card is chrome the card supersedes.
        ///
        /// <para>Runs AFTER every gauge is re-homed, so retiring the frame a meter used to live in
        /// cannot take that meter with it - and a meter adopted onto a DIFFERENT card is already
        /// out of this host by the time we get here.</para>
        ///
        /// <para><b>A touch target is never retired, only made invisible.</b> These hosts are
        /// <see cref="Button"/>s and a UGUI button raycasts through its <c>targetGraphic</c>, so
        /// disabling that Image would silently delete the on-screen ability control on every touch
        /// device - a graphic with a clear colour still raycasts, an absent one does not.</para>
        /// </summary>
        void RetireLegacyChrome(RectTransform host, Slot slot, Image icon)
        {
            var selectable = host.GetComponent<Selectable>();
            var touchTarget = selectable ? selectable.targetGraphic : null;

            var hostImage = host.GetComponent<Image>();
            if (hostImage && hostImage.enabled)
            {
                if (hostImage == touchTarget) hostImage.color = Color.clear;
                else hostImage.enabled = false;
            }

            for (int i = host.childCount - 1; i >= 0; i--)
            {
                var child = host.GetChild(i) as RectTransform;
                if (!child || child == slot.Card) continue;
                if (icon && child == icon.rectTransform) continue;
                if (!child.gameObject.activeSelf) continue;

                if (touchTarget && (child == touchTarget.rectTransform ||
                                    touchTarget.transform.IsChildOf(child)))
                {
                    touchTarget.color = Color.clear;
                    continue;
                }

                child.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The flower container for one card. A vessel that AUTHORED its flowers (the Squirrel) has
        /// its container RE-HOMED into the card rather than replaced: moving the authored transform
        /// keeps its authored petals, so nothing is built at runtime, nothing warns, and no orphan
        /// flower is left rendering at the old row position.
        /// </summary>
        RectTransform ResolveFlowerSocket(RectTransform card, Element element)
        {
            EnsureElementBars();

            RectTransform socket = null;
            if (elementBars && elementBars.TryGetPetalRoot(element, out var authored) && authored)
                socket = authored;

            if (!socket)
            {
                const string socketName = "ElementFlower";
                socket = card.Find(socketName) as RectTransform;
                if (!socket)
                {
                    var go = new GameObject(socketName, typeof(RectTransform));
                    socket = (RectTransform)go.transform;
                }
            }

            if (socket.parent != card) socket.SetParent(card, false);

            socket.anchorMin = socket.anchorMax = socket.pivot = new Vector2(0.5f, 0.5f);
            socket.sizeDelta = new Vector2(style.petalFlowerSize, style.petalFlowerSize);
            socket.anchoredPosition = new Vector2(0f, style.FlowerLocalY);
            socket.localScale = Vector3.one;
            socket.localRotation = Quaternion.identity;
            return socket;
        }

        RectTransform ResolveChipSocket(RectTransform card)
        {
            const string name = "ControlChip";
            var socket = card.Find(name) as RectTransform;
            if (!socket)
            {
                var go = new GameObject(name, typeof(RectTransform));
                socket = (RectTransform)go.transform;
                socket.SetParent(card, false);
            }

            socket.anchorMin = socket.anchorMax = socket.pivot = new Vector2(0.5f, 0.5f);
            socket.sizeDelta = new Vector2(style.plateWidth, style.chipHeight);
            socket.anchoredPosition = new Vector2(0f, -style.PlateHeight * 0.5f - style.chipGap - style.chipHeight * 0.5f);
            socket.localScale = Vector3.one;
            socket.localRotation = Quaternion.identity;
            return socket;
        }

        void DockElementFlowers()
        {
            EnsureElementBars();
            if (!elementBars) return;

            foreach (var pair in _slots)
            {
                if (!pair.Value.FlowerSocket) continue;
                elementBars.TrySetPetalRoot(pair.Key, pair.Value.FlowerSocket);
            }
        }

        void EnsureElementBars()
        {
            if (elementBars) return;
            elementBars = GetComponentInChildren<ElementalBarsView>(true);
            if (!elementBars) elementBars = gameObject.AddComponent<ElementalBarsView>();
        }

        // ---------------------------------------------------------------
        // State
        // ---------------------------------------------------------------

        /// <summary>
        /// Crosses a card between its resting and upgraded states - the rim brightens to the
        /// level-5 white and the bloom comes up behind the plate. Called from
        /// <see cref="VesselHUDView.SetAbilityUpgraded"/>, so every vessel gets it for free.
        /// A locked slot has no ability to upgrade, so it never lights.
        /// </summary>
        public void SetUpgraded(Element element, bool upgraded, bool animate = true)
        {
            if (!_slots.TryGetValue(element, out var slot) || !style || slot.Locked) return;
            slot.Upgraded = upgraded;
            slot.Tween?.Kill();

            Color rimTarget   = upgraded ? style.upgradedRimColor : style.hairlineColor;
            Color bloomTarget = upgraded ? style.bloomColor : WithAlpha(style.bloomColor, 0f);
            Color plateTarget = upgraded ? style.upgradedPlateColor : style.plateColor;

            if (!animate)
            {
                if (slot.Rim)   slot.Rim.color   = rimTarget;
                if (slot.Bloom) slot.Bloom.color = bloomTarget;
                if (slot.Plate) slot.Plate.color = plateTarget;
                return;
            }

            float d = style.upgradeTransitionDuration;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(slot.Card.gameObject);
            if (slot.Rim)   seq.Join(slot.Rim.DOColor(rimTarget, d).SetEase(Ease.OutCubic));
            if (slot.Bloom) seq.Join(slot.Bloom.DOColor(bloomTarget, d).SetEase(Ease.OutCubic));
            if (slot.Plate) seq.Join(slot.Plate.DOColor(plateTarget, d).SetEase(Ease.OutCubic));

            if (upgraded && style.unlockPunchScale > 1f)
            {
                slot.Card.localScale = Vector3.one;
                seq.Join(slot.Card
                    .DOPunchScale(Vector3.one * (style.unlockPunchScale - 1f),
                                  style.unlockPunchDuration, 1, 0.6f));
            }

            slot.Tween = seq;
        }

        /// <summary>
        /// Lights the CARD on a press. This is the totem's answer to the per-vessel circular glow,
        /// which was authored to sit behind a round button and reads as a foreign shape now.
        /// </summary>
        public void PlayPressFlash(Element element) => SetPressed(element, true, releaseImmediately: true);

        /// <summary>
        /// The card's press state - the fleet's ONE "this ability is firing" signal, replacing the
        /// per-vessel circular glow that used to be switched on behind the icon. It lights the whole
        /// card rather than drawing a second shape, so a held ability reads at a glance and a card
        /// never grows chrome the totem does not own.
        ///
        /// <para>Held while the control is down, then decayed rather than switched off - the same
        /// continuity rule the rest of the game follows: nothing pops out of existence.</para>
        /// </summary>
        public void SetAbilityPressed(Element element, bool pressed) => SetPressed(element, pressed, false);

        void SetPressed(Element element, bool pressed, bool releaseImmediately)
        {
            if (!_slots.TryGetValue(element, out var slot) || !style || !slot.Flash) return;

            slot.FlashTween?.Kill();
            slot.FlashTween = null;

            if (pressed) slot.Flash.color = style.pressFlashColor;

            if (!pressed || releaseImmediately)
            {
                slot.FlashTween = slot.Flash
                    .DOFade(0f, style.pressFlashDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(slot.Flash.gameObject);
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        void AdoptButtonTarget(RectTransform host, Image plate)
        {
            var button = host.GetComponent<Button>();
            if (!button) return;

            button.targetGraphic = plate;
            plate.raycastTarget = true;   // the card is the touch target now, so it must be hittable
        }

        Image ResolveChildImage(RectTransform parent, string childName, Sprite sprite)
        {
            var existing = parent.Find(childName);
            var img = existing ? existing.GetComponent<Image>() : null;
            if (!img)
            {
                var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                img = go.GetComponent<Image>();
            }

            img.sprite = sprite;
            img.raycastTarget = false;                 // decorative - never eats a touch
            img.type = sprite && sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            return img;
        }

        /// <summary>Centres a rect of the given size at an offset from the CARD's centre.</summary>
        static void SetCellRect(RectTransform rt, float offsetY, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = new Vector2(0f, offsetY);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        static void StretchTo(RectTransform rt, float padding)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(-padding, -padding);
            rt.offsetMax = new Vector2(padding, padding);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
        }

        static Color WithAlpha(Color c, float a) => new(c.r, c.g, c.b, a);

        AbilityLockupStyleSO ResolveStyle()
        {
            if (!style) style = Resources.Load<AbilityLockupStyleSO>(styleResourcePath);
            return style;
        }

        void OnDestroy()
        {
            foreach (var slot in _slots.Values)
            {
                slot.Tween?.Kill();
                slot.FlashTween?.Kill();
            }
            _slots.Clear();
        }
    }
}
