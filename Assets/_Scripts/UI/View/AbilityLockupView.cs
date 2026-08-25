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
        readonly HashSet<Image> _retiredChrome = new();
        bool _built;

        public bool IsBuilt => _built;

        /// <summary>
        /// The element-bar view whose flowers are docked into these cards. Non-null once
        /// <see cref="Build"/> has run; the vessel's ElementalBarsController adopts THIS rather
        /// than creating the fleet-standard row on top of it.
        /// </summary>
        public ElementalBarsView ElementBars => elementBars;

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

        AbilityLockupStyleSO ResolveStyle()
        {
            if (!style) style = Resources.Load<AbilityLockupStyleSO>(styleResourcePath);
            return style;
        }

        void Awake() => Build();

        /// <summary>
        /// Composes a lockup card around every ability icon the HUD binds. Idempotent: existing
        /// cards are adopted by name, so a vessel swap re-running Initialize can never stack two.
        /// </summary>
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

            if (!hudView.HasAbilityIconRow)
            {
                Debug.LogWarning($"[AbilityLockupView] {hudView.name} binds no abilityIcons, so no lockup " +
                                 "cards were built. Wire the four-icon row first: FrogletTools > Vessels > " +
                                 "Wire Vessel Ability Row.", this);
                return;
            }

            var row = ResolveRow();

            // Right-aligned: the LAST slot sits at the row's corner and the others step left, so a
            // vessel that ever binds fewer than four still ends flush with the fleet's row edge.
            int count = VesselHUDView.AbilityDisplayOrder.Length;
            for (int i = 0; i < count; i++)
            {
                var element = VesselHUDView.AbilityDisplayOrder[i];
                if (!hudView.TryGetAbilityIcon(element, out var icon) || !icon) continue;

                PlaceHost(row, icon.rectTransform, i, count);
                var slot = BuildSlot(element, icon);
                if (slot != null) _slots[element] = slot;
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

        /// <summary>
        /// Re-homes the vessel's own ability button into the row at the fleet slot position, and
        /// normalises the things a prefab is otherwise free to disagree on: cell size, scale, and
        /// the legacy button chrome the card replaces.
        ///
        /// <para>The HOST is moved rather than the icon so that the button, its touch target, its
        /// press juice and any gauge children all travel together and keep working.</para>
        ///
        /// <para>This is deliberately destructive of per-prefab layout: the Squirrel scaled its
        /// buttons 0.7, the Sparrow and Scarab anchor theirs in a different container entirely, and
        /// the Dolphin authored one icon at 96 against its others' 80. Reading any of that would
        /// make the row a per-vessel negotiation, which is the thing this system exists to end.</para>
        /// </summary>
        void PlaceHost(RectTransform row, RectTransform iconRT, int index, int count)
        {
            var host = iconRT.parent as RectTransform;
            if (!host || host == row) return;

            if (host.parent != row) host.SetParent(row, false);

            host.anchorMin = host.anchorMax = host.pivot = new Vector2(0.5f, 0.5f);
            host.sizeDelta = new Vector2(style.plateWidth, style.abilityCellHeight);
            host.localScale = Vector3.one;
            host.localRotation = Quaternion.identity;
            host.anchoredPosition = new Vector2(
                -(count - 1 - index) * style.cardPitch - style.plateWidth * 0.5f,
                style.abilityCellHeight * 0.5f);
            host.SetSiblingIndex(index);

            // The icon sits dead centre of the ability cell at the fleet's one drawn size. Its scale
            // is applied by VesselHUDView through AbilityIconRestScale, so it composes with the
            // upgrade bump and survives every per-vessel tween.
            iconRT.anchorMin = iconRT.anchorMax = iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;

            RetireLegacyChrome(host);
        }

        /// <summary>
        /// Switches off the button plate the card replaces - the decagon
        /// <c>Ability Background Small</c> that the Sparrow, Squirrel and Scarab draw behind their
        /// icons. Left on, it sits behind the totem as a second, differently-shaped plate.
        ///
        /// <para>A disabled Graphic no longer raycasts, so a button whose target it was would stop
        /// taking touches. The card's plate takes that job instead, which also means the touch area
        /// now matches the shape the player can see.</para>
        /// </summary>
        void RetireLegacyChrome(RectTransform host)
        {
            var legacy = host.GetComponent<Image>();
            if (!legacy || !legacy.enabled) return;

            _retiredChrome.Add(legacy);
            legacy.enabled = false;
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
            AdoptButtonTarget(card, slot.Plate);

            slot.Divider = ResolveChildImage(card, "Divider", null);
            var dRT = slot.Divider.rectTransform;
            dRT.anchorMin = dRT.anchorMax = dRT.pivot = new Vector2(0.5f, 0.5f);
            dRT.sizeDelta = new Vector2(style.plateWidth - style.dividerInset * 2f, style.dividerThickness);
            dRT.anchoredPosition = new Vector2(0f, style.DividerLocalY);
            slot.Divider.color = style.dividerColor;

            slot.Rim = ResolveChildImage(card, "Rim", style.rimSprite);
            StretchTo(slot.Rim.rectTransform, 0f);
            slot.Rim.color = style.hairlineColor;

            slot.FlowerSocket = ResolveFlowerSocket(card, element);
            return slot;
        }

        /// <summary>
        /// The flower container for one card. A vessel that AUTHORED its flowers (the Squirrel) has
        /// its container RE-HOMED into the card rather than replaced: moving the authored transform
        /// keeps its authored petals, so nothing is built at runtime, nothing warns, and no orphan
        /// flower is left rendering at the old row position. A vessel with no authored flower gets a
        /// fresh socket instead.
        ///
        /// <para>Re-homing also makes docking order-independent: reparenting works whether or not
        /// <see cref="ElementalBarsView"/> has already built its petals, whereas injecting a socket
        /// only works before the build.</para>
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

        /// <summary>
        /// Hands each card's flower socket to the shared element-bar view, so the SAME petals the
        /// rest of the fleet draws are rendered inside the lockup instead of in a separate row.
        /// A re-homed authored container is already bound, so the injection is a no-op there.
        /// </summary>
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

        /// <summary>
        /// Hands the host button its new visual. Only touches a button whose target graphic was the
        /// legacy plate we just switched off - a vessel that points its button somewhere deliberate
        /// keeps it.
        /// </summary>
        void AdoptButtonTarget(RectTransform card, Image plate)
        {
            var host = card.parent as RectTransform;
            if (!host) return;

            var button = host.GetComponent<Button>();
            if (!button) return;

            if (button.targetGraphic && !_retiredChrome.Contains(button.targetGraphic as Image)) return;

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
