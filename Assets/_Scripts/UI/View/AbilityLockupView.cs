using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Builds the fleet-wide ABILITY LOCKUP around a vessel's four authored ability icons - the
    /// "totem" card that fuses each ability with the element indicator that upgrades it.
    ///
    /// <para><b>Why it composes instead of being authored per vessel.</b> The lockup's chrome is
    /// pure style read from one asset (<see cref="AbilityLockupStyleSO"/>) and is identical on every
    /// vessel; only the ICON inside it is per-vessel, and that is already authored. Composing it
    /// here is the same shape the upgrade badge already uses in <see cref="VesselHUDView"/>, and it
    /// means the style rolls out to all eleven vessels with no per-prefab wiring to forget - the
    /// enforcement ladder this codebase uses for fleet-wide requirements (single config asset ->
    /// runtime warn-and-degrade -> auditor).</para>
    ///
    /// <para><b>It never moves authored content.</b> The card is inserted as a SIBLING behind the
    /// icon and the lower cell is centred on wherever that icon already sits, so a vessel adopts
    /// the style without one authored rect changing. The upper cell is added ABOVE - that added
    /// space is what makes the card a totem.</para>
    ///
    /// <para><b>The element flower is the shipped one, unchanged.</b> This view only supplies the
    /// SOCKET; <see cref="ElementalBarsView"/> still builds and drives the five petals exactly as
    /// it does everywhere else, on the same ladder colours and the same juice. Docking it here is
    /// what makes icon and indicator one system rather than two rows that must be read together.
    /// Because every flower root is then supplied, the shared config's standard-placement stamp
    /// correctly stands down (see <c>ElementalBarsView.Build</c>).</para>
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
            public RectTransform FlowerSocket;
            public Tween Tween;
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
        /// Composes a lockup card around every ability icon the HUD binds. Idempotent: existing
        /// cards are adopted by name, so a vessel swap re-running Initialize can never stack two.
        /// </summary>
        public void Build()
        {
            if (_built) return;

            if (!style)
                style = Resources.Load<AbilityLockupStyleSO>(styleResourcePath);
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

            if (!hudView.HasAbilityIconRow)
            {
                Debug.LogWarning($"[AbilityLockupView] {hudView.name} binds no abilityIcons, so no lockup " +
                                 "cards were built. Wire the four-icon row first: FrogletTools > Vessels > " +
                                 "Wire Vessel Ability Row.", this);
                return;
            }

            foreach (var binding in hudView.abilityIcons)
            {
                if (!binding.icon) continue;
                var slot = BuildSlot(binding.element, binding.icon);
                if (slot != null) _slots[binding.element] = slot;
            }

            DockElementFlowers();
            _built = true;
        }

        Slot BuildSlot(Element element, Image icon)
        {
            var iconRT = icon.rectTransform;
            var parent = iconRT.parent as RectTransform;
            if (!parent)
            {
                Debug.LogWarning($"[AbilityLockupView] The '{element}' ability icon has no RectTransform " +
                                 "parent to host its lockup card.", this);
                return null;
            }

            string cardName = $"AbilityLockup_{element}";
            var existing = parent.Find(cardName) as RectTransform;

            var card = existing;
            if (!card)
            {
                var go = new GameObject(cardName, typeof(RectTransform));
                card = (RectTransform)go.transform;
                card.SetParent(parent, false);
            }

            // Point-anchor the card on the icon's own anchor so the two move together. A stretch-
            // anchored icon (no vessel ships one today) falls back to the anchor rect's centre.
            var anchor = iconRT.anchorMin == iconRT.anchorMax
                ? iconRT.anchorMin
                : (iconRT.anchorMin + iconRT.anchorMax) * 0.5f;
            card.anchorMin = card.anchorMax = anchor;
            card.pivot = new Vector2(0.5f, 0.5f);
            card.localRotation = Quaternion.identity;
            card.localScale = Vector3.one;
            card.sizeDelta = new Vector2(style.plateWidth, style.PlateHeight);
            // The LOWER cell is centred on the icon exactly where it already is; the card's own
            // centre therefore rides half the upper cell above it. No authored rect is touched.
            card.localPosition = iconRT.localPosition + new Vector3(0f, style.CardCenterOffsetY, 0f);
            // Behind the icon: inserting at the icon's index pushes the icon one later in the
            // sibling order, and UGUI draws siblings in order.
            card.SetSiblingIndex(iconRT.GetSiblingIndex());

            var slot = new Slot { Card = card };

            slot.Bloom = ResolveChildImage(card, "Bloom", style.bloomSprite);
            StretchTo(slot.Bloom.rectTransform, style.bloomPadding);
            slot.Bloom.color = WithAlpha(style.bloomColor, 0f);   // nothing glows at rest

            slot.Plate = ResolveChildImage(card, "Plate", style.plateSprite);
            StretchTo(slot.Plate.rectTransform, 0f);
            slot.Plate.color = style.plateColor;

            slot.Divider = ResolveChildImage(card, "Divider", null);
            var dRT = slot.Divider.rectTransform;
            dRT.anchorMin = dRT.anchorMax = dRT.pivot = new Vector2(0.5f, 0.5f);
            dRT.sizeDelta = new Vector2(style.plateWidth - style.dividerInset * 2f, style.dividerThickness);
            dRT.anchoredPosition = new Vector2(0f, style.DividerLocalY);
            slot.Divider.color = style.dividerColor;

            slot.Rim = ResolveChildImage(card, "Rim", style.rimSprite);
            StretchTo(slot.Rim.rectTransform, 0f);
            slot.Rim.color = style.hairlineColor;

            string socketName = "ElementFlower";
            var socket = card.Find(socketName) as RectTransform;
            if (!socket)
            {
                var go = new GameObject(socketName, typeof(RectTransform));
                socket = (RectTransform)go.transform;
                socket.SetParent(card, false);
            }
            socket.anchorMin = socket.anchorMax = socket.pivot = new Vector2(0.5f, 0.5f);
            socket.sizeDelta = new Vector2(style.petalFlowerSize, style.petalFlowerSize);
            socket.anchoredPosition = new Vector2(0f, style.FlowerLocalY);
            socket.localScale = Vector3.one;
            slot.FlowerSocket = socket;

            return slot;
        }

        /// <summary>
        /// Hands each card's flower socket to the shared element-bar view, so the SAME petals the
        /// rest of the fleet draws are rendered inside the lockup instead of in a separate row.
        /// </summary>
        void DockElementFlowers()
        {
            if (!elementBars) elementBars = GetComponentInChildren<ElementalBarsView>(true);
            if (!elementBars) elementBars = gameObject.AddComponent<ElementalBarsView>();

            foreach (var pair in _slots)
            {
                if (!pair.Value.FlowerSocket) continue;
                elementBars.TrySetPetalRoot(pair.Key, pair.Value.FlowerSocket);
            }
        }

        /// <summary>The flower socket for an element, if a card was built for it.</summary>
        public RectTransform GetFlowerSocket(Element element)
            => _slots.TryGetValue(element, out var slot) ? slot.FlowerSocket : null;

        /// <summary>
        /// Crosses a card between its resting and upgraded states - the rim brightens to the
        /// level-5 white and the bloom comes up behind the plate. Called from
        /// <see cref="VesselHUDView.SetAbilityUpgraded"/>, so every vessel gets it for free.
        /// States travel rather than snapping (continuity of existence applies to the HUD too).
        /// </summary>
        public void SetUpgraded(Element element, bool upgraded, bool animate = true)
        {
            if (!_slots.TryGetValue(element, out var slot) || !style) return;
            if (slot.Upgraded == upgraded && slot.Tween == null) { /* still apply on first seed */ }
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

            // One-shot ceremony on the unlock itself - never on the re-lock, which should read as
            // something being taken away rather than celebrated.
            if (upgraded && style.unlockPunchScale > 1f)
            {
                slot.Card.localScale = Vector3.one;
                seq.Join(slot.Card
                    .DOPunchScale(Vector3.one * (style.unlockPunchScale - 1f),
                                  style.unlockPunchDuration, 1, 0.6f));
            }

            slot.Tween = seq;
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

        void OnDestroy()
        {
            foreach (var slot in _slots.Values)
                slot.Tween?.Kill();
            _slots.Clear();
        }
    }
}
