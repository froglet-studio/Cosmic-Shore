using System.Collections.Generic;
using System.Reflection;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
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
    /// <para><b>The card is TWO BORDERLESS TRAPEZOIDS</b> meeting at their wide edges across a
    /// small gap - the element flower in the upper one, the ability icon in the lower one. The gap
    /// replaces the hairline divider and the silhouette replaces the rim, so the plates carry no
    /// outline at all; the upgrade is a bloom behind both plus a lift in their fill. Both plates
    /// are generated (<see cref="TrapezoidGraphic"/>) rather than sprited, because a trapezoid has
    /// no 9-slice and a sprited one would freeze the slant into the art.</para>
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
        [Tooltip("The fleet's control-glyph table. Loaded from Resources/ControlGlyphSet when empty.")]
        [SerializeField] private ControlGlyphSetSO glyphSet;
        [SerializeField] private string glyphSetResourcePath = "ControlGlyphSet";

        [Header("Bindings (resolved from siblings when empty)")]
        [Tooltip("The vessel HUD view whose abilityIcons this decorates. Defaults to this GameObject's.")]
        [SerializeField] private VesselHUDView hudView;
        [Tooltip("The element flowers to dock into the cards. Found on this HUD, or created here.")]
        [SerializeField] private ElementalBarsView elementBars;

        sealed class Slot
        {
            public RectTransform Card;
            public Image ElementBloom;
            public Image AbilityBloom;
            public TrapezoidGraphic ElementPlate;
            public TrapezoidGraphic AbilityPlate;
            public TrapezoidGraphic GaugeTrack;
            public RectTransform GaugeClip;
            public TrapezoidGraphic Flash;
            public RectTransform CooldownClip;
            public Image CooldownSweep;
            public bool OnCooldown;
            public RectTransform FlowerSocket;
            public RectTransform ChipSocket;
            public Image ChipGlyph;
            public TMP_Text ChipLabel;
            public RectTransform Keycap;
            public Image KeycapFill;
            public Image KeycapBorder;
            public TMP_Text KeycapLabel;
            public InputDeviceIconSetSwitcher.HintBinding PadBinding;
            public InputDeviceIconSetSwitcher.HintBinding KeyBinding;
            public Tween Tween;
            public Tween FlashTween;
            public bool Pressed;
            public bool Locked;
            public bool Upgraded;
            public bool HasTint;        // rev 3: the card wears its element's colour
            public Color Tint;
            public AbilityCardPress Press;   // a tap on the card presses its ability
            public RectTransform Host;       // what the row positions (the card hangs off it)
            public int Index;                // its row slot, for RestoreRowLayout
            public int Count;
            public Image TouchGlyph;         // the thumb-lift glyph a phone draws in the chip socket
        }

        /// <summary>One card, as the phone controls (<c>TouchControlsHud</c>) see it.</summary>
        public struct CardInfo
        {
            public bool IsCore;
            public Element Element;
            public CoreAbility Core;
            public RectTransform Host;
            public AbilityCardPress Press;
            public bool Locked;
        }

        /// <summary>One drifting colour blob of the rev 3 lava lamp, behind a card's icon.</summary>
        sealed class LavaBlob
        {
            public RectTransform Rt;
            public Vector2 Home;
            public float Period;
            public float Phase;
        }

        readonly List<LavaBlob> _blobs = new();

        readonly Dictionary<Element, Slot> _slots = new();
        readonly Dictionary<CoreAbility, Slot> _coreSlots = new();
        bool _built;

        public bool IsBuilt => _built;

        /// <summary>
        /// The element-bar view whose flowers are docked into these cards. Non-null once
        /// <see cref="Build"/> has run; the vessel's ElementalBarsController adopts THIS rather
        /// than creating the fleet-standard row on top of it.
        /// </summary>
        public ElementalBarsView ElementBars => elementBars;

        void Awake()
        {
            // The lava blobs move every frame. On GameCanvas (thousands of graphics) each move re-batched
            // the WHOLE canvas every frame - the in-match slowdown after the lava-lamp row (2026-10-17). A
            // nested Canvas on the row confines that re-batch to the row; it needs its own raycaster for
            // the touch ability buttons inside it.
            if (!TryGetComponent<Canvas>(out _))
            {
                gameObject.AddComponent<Canvas>();
                gameObject.AddComponent<GraphicRaycaster>();
            }
            Build();
        }

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

        /// <summary>The same kerning for a non-elemental card's icon.</summary>
        public float CoreIconContentScale(CoreAbility ability)
        {
            var s = ResolveStyle();
            if (!s) return 1f;

            if (!hudView) hudView = GetComponent<VesselHUDView>();
            if (!hudView || !hudView.TryGetCoreAbilityIcon(ability, out var icon) || !icon) return 1f;

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

        static readonly Vector3[] s_corners = new Vector3[4];

        /// <summary>
        /// The screen rectangle, in pixels, that the built cards cover together AT REST - so a view or an input can keep
        /// clear of the totems (the Wrasse's hand line sits just above it). Each card is measured without its own scale and
        /// rotation, so its press and upgrade tweens do not move the answer. False before <see cref="Build"/>, or while no
        /// card is showing. A question, not a demand: a miss is an answer.
        /// </summary>
        public bool TryGetCardsScreenRect(out Rect rect)
        {
            rect = default;
            if (!_built) return false;
            var canvas = GetComponentInParent<Canvas>();
            var root = canvas ? canvas.rootCanvas : null;
            Camera cam = root && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;
            bool any = false;
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (var slot in _slots.Values) Grow(slot.Card, cam, ref any, ref xMin, ref yMin, ref xMax, ref yMax);
            foreach (var slot in _coreSlots.Values) Grow(slot.Card, cam, ref any, ref xMin, ref yMin, ref xMax, ref yMax);
            if (any) rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return any;
        }

        static void Grow(RectTransform card, Camera cam, ref bool any, ref float xMin, ref float yMin, ref float xMax, ref float yMax)
        {
            if (!card || !card.gameObject.activeInHierarchy) return;
            // the card's rect placed at its pivot in its parent, leaving out its own (tweened) scale and rotation
            var r = card.rect;
            var at = card.localPosition;
            var parent = card.parent;
            s_corners[0] = at + new Vector3(r.xMin, r.yMin, 0f);
            s_corners[1] = at + new Vector3(r.xMin, r.yMax, 0f);
            s_corners[2] = at + new Vector3(r.xMax, r.yMax, 0f);
            s_corners[3] = at + new Vector3(r.xMax, r.yMin, 0f);
            for (int i = 0; i < 4; i++)
            {
                var world = parent ? parent.TransformPoint(s_corners[i]) : s_corners[i];
                var p = RectTransformUtility.WorldToScreenPoint(cam, world);
                xMin = Mathf.Min(xMin, p.x); xMax = Mathf.Max(xMax, p.x);
                yMin = Mathf.Min(yMin, p.y); yMax = Mathf.Max(yMax, p.y);
            }
            any = true;
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
        // Phone controls (TouchControlsHud, Docs/Reports/MOBILE_VESSELS.md)
        // ---------------------------------------------------------------

        /// <summary>Every card of the row, elemental then non-elemental.</summary>
        public void GetCards(List<CardInfo> into)
        {
            into.Clear();
            foreach (var pair in _slots)
                into.Add(new CardInfo { Element = pair.Key, Host = pair.Value.Host, Press = pair.Value.Press,
                                        Locked = pair.Value.Locked });
            foreach (var pair in _coreSlots)
                into.Add(new CardInfo { IsCore = true, Core = pair.Key, Host = pair.Value.Host,
                                        Press = pair.Value.Press, Locked = pair.Value.Locked });
        }

        /// <summary>The row the cards hang off (bottom-right of the HUD root).</summary>
        public RectTransform Row => transform.Find("AbilityLockupRow") as RectTransform;

        /// <summary>A card's width in canvas units, at host scale 1.</summary>
        public float CardWidth => ResolveStyle() ? style.PlateWidth : 104f;

        /// <summary>A full totem's height (element plate, gap, ability plate) in canvas units.</summary>
        public float CardHeight => ResolveStyle() ? style.PlateHeight : 182f;

        /// <summary>How far above its host's centre a full totem's centre sits, in canvas units at scale 1.</summary>
        public float CardCentreAboveHost => ResolveStyle() ? style.CardCenterOffsetY : 47f;

        /// <summary>A colour for a button that is not an element's (the drift colour).</summary>
        public Color NeutralColour => ResolveStyle() ? style.DriftColour : Color.white;

        public Color ColourFor(Element element) => ResolveStyle() ? style.ColourFor(element) : Color.white;

        /// <summary>Puts every card back where the fleet row puts it, at scale 1 - undoing a phone layout.</summary>
        public void RestoreRowLayout()
        {
            var row = Row;
            if (!row || !ResolveStyle()) return;
            foreach (var slot in _slots.Values)
                if (slot.Host) PlaceHost(row, slot.Host, slot.Index, slot.Count);
            foreach (var slot in _coreSlots.Values)
                if (slot.Host) PlaceHost(row, slot.Host, slot.Index, slot.Count);
        }

        /// <summary>The touch gesture a card's ability is on, drawn in its chip socket. Null clears it.</summary>
        public void SetTouchGlyph(Element element, Sprite glyph)
        {
            if (_slots.TryGetValue(element, out var slot)) SetSlotTouchGlyph(slot, glyph);
        }

        /// <summary>The same for a non-elemental card (the Squirrel's drift).</summary>
        public void SetCoreTouchGlyph(CoreAbility ability, Sprite glyph)
        {
            if (_coreSlots.TryGetValue(ability, out var slot)) SetSlotTouchGlyph(slot, glyph);
        }

        void SetSlotTouchGlyph(Slot slot, Sprite glyph)
        {
            if (!slot.ChipSocket) return;
            if (!glyph)
            {
                if (slot.TouchGlyph) slot.TouchGlyph.enabled = false;
                return;
            }
            if (!slot.TouchGlyph)
            {
                slot.TouchGlyph = ResolveChildImage(slot.ChipSocket, "TouchGlyph", null);
                var rt = slot.TouchGlyph.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                float side = style.ChipHeight * 1.8f;
                rt.sizeDelta = new Vector2(side, side);
                slot.TouchGlyph.preserveAspect = true;
                slot.TouchGlyph.raycastTarget = false;
            }
            slot.TouchGlyph.sprite = glyph;
            slot.TouchGlyph.color = slot.HasTint ? slot.Tint : Color.white;
            slot.TouchGlyph.enabled = true;
        }

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

            // A vessel whose readout is a live MEASUREMENT builds its icon here rather than
            // authoring one, so the row can find it in the pass below like any other. No-op on
            // every vessel that authors all of its art.
            hudView.EnsureGeneratedAbilityIcons();

            // ...and the one card no vessel gets to skip. Not a virtual and not opt-in: every hull
            // can fly through a crystal, so every hull has this card - with a LOCKED plate on the
            // three that do nothing with one yet.
            hudView.EnsureOmniCrystalCard();

            var row = ResolveRow();
            var order = VesselHUDView.AbilityDisplayOrder;
            var coreOrder = VesselHUDView.CoreAbilityDisplayOrder;

            // Three passes, and the SEPARATION is load-bearing. A vessel's meter is not always
            // authored on the card its ability belongs to (the Squirrel's boost fill sits under the
            // skimming button; the Scarab's ball-energy ring under the throttle button), so a gauge
            // is frequently adopted ONTO one card OUT OF another card's host. Retiring chrome in the
            // same pass would therefore deactivate a meter a later element was about to claim -
            // silently, and only on the vessels whose authoring drifted.
            var hosts = new Dictionary<Element, RectTransform>();
            var icons = new Dictionary<Element, Image>();
            var coreHosts = new Dictionary<CoreAbility, RectTransform>();
            var coreIcons = new Dictionary<CoreAbility, Image>();

            // The NON-elemental cards first, at NEGATIVE slot indices, so they land one pitch further
            // left per card and the elemental columns never move. A vessel that binds none of them
            // (every vessel but the Squirrel today) emits nothing at all here.
            // A core card is DROPPED entirely only when the fleet has nothing to say about it:
            // no art, no meter and no emblem. The omni crystal card always has an emblem, so
            // every vessel draws it - locked where that hull has no omni ability yet, which is
            // the honest state and what a locked card is for. The cards that are drawn pack
            // against the elemental row, so a hull that binds only some of them shows no hole.
            bool Present(CoreAbility a) =>
                (hudView.TryGetCoreAbilityIcon(a, out var probeIcon) && probeIcon) ||
                (hudView.TryGetCoreAbilityEmblem(a, out var probeEmblem) && probeEmblem) ||
                (style && style.EmblemFor(a));

            int coreCount = 0;
            for (int i = 0; i < coreOrder.Length; i++)
            {
                var ability = coreOrder[i];
                var emblemSprite = style ? style.EmblemFor(ability) : null;
                hudView.TryGetCoreAbilityEmblem(ability, out var emblem);

                bool hasIcon = hudView.TryGetCoreAbilityIcon(ability, out var icon) && icon;
                if (!hasIcon && !emblem && !emblemSprite) continue;

                var host = hasIcon
                    ? icon.rectTransform.parent as RectTransform
                    : ResolveLockedHost(row, ability.ToString());
                if (!host || host == row) continue;

                // The LAST drawn entry in the display order sits nearest Charge: index -1, then -2 leftward.
                int drawnAfter = 0;
                for (int j = i + 1; j < coreOrder.Length; j++)
                    if (Present(coreOrder[j])) drawnAfter++;
                PlaceHost(row, host, -(drawnAfter + 1), order.Length);

                var slot = BuildSlot($"AbilityLockup_{ability}", host, hasIcon ? icon : null,
                                     Element.None, emblem, emblemSprite);
                if (slot == null) continue;
                slot.Host = host;
                slot.Index = -(coreOrder.Length - i);
                slot.Count = order.Length;

                if (hasIcon)
                {
                    NormaliseIcon(icon.rectTransform);
                    coreIcons[ability] = icon;
                }
                coreHosts[ability] = host;
                _coreSlots[ability] = slot;
                ApplyElementColour(slot, ability == CoreAbility.OmniCrystal ? OmniEdge : style.DriftColour,
                                   omni: ability == CoreAbility.OmniCrystal);
                coreCount++;
            }

            for (int i = 0; i < order.Length; i++)
            {
                var element = order[i];
                bool hasIcon = hudView.TryGetAbilityIcon(element, out var icon) && icon;

                var host = hasIcon
                    ? icon.rectTransform.parent as RectTransform
                    : ResolveLockedHost(row, element);
                if (!host || host == row) continue;

                PlaceHost(row, host, i, order.Length);

                var slot = BuildSlot($"AbilityLockup_{element}", host, hasIcon ? icon : null, element,
                                     emblem: null, emblemSprite: null);
                if (slot == null) continue;
                slot.Host = host;
                slot.Index = i;
                slot.Count = order.Length;

                if (hasIcon) NormaliseIcon(icon.rectTransform);

                hosts[element] = host;
                if (hasIcon) icons[element] = icon;
                _slots[element] = slot;
                ApplyElementColour(slot, style.ColourFor(element), omni: false);
            }

            foreach (var pair in _coreSlots)
                if (hudView.TryGetCoreAbilityGauge(pair.Key, out var gauge))
                    AdoptGauge(pair.Value, gauge);

            foreach (var pair in _slots)
                if (hudView.TryGetAbilityGauge(pair.Key, out var gauge))
                    AdoptGauge(pair.Value, gauge);

            foreach (var pair in _coreSlots)
            {
                if (!coreHosts.TryGetValue(pair.Key, out var host)) continue;
                coreIcons.TryGetValue(pair.Key, out var icon);
                RetireLegacyChrome(host, pair.Value, icon);
            }

            foreach (var pair in _slots)
            {
                if (!hosts.TryGetValue(pair.Key, out var host)) continue;
                icons.TryGetValue(pair.Key, out var icon);
                RetireLegacyChrome(host, pair.Value, icon);
            }

            DockElementFlowers();

            RetireLegacyHudContent(row, anyIconBound: icons.Count + coreIcons.Count > 0);

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
            var self = NormaliseHudRoot();
            var row = self.Find(rowName) as RectTransform;
            if (!row)
            {
                var go = new GameObject(rowName, typeof(RectTransform));
                row = (RectTransform)go.transform;
                row.SetParent(self, false);
            }

            row.anchorMin = row.anchorMax = row.pivot = new Vector2(1f, 0f);
            row.sizeDelta = Vector2.zero;
            row.anchoredPosition = new Vector2(-style.RowMarginRight, style.RowMarginBottom);
            row.localScale = Vector3.one;
            row.localRotation = Quaternion.identity;
            return row;
        }

        /// <summary>
        /// Makes the HUD root the SCREEN, which is what the row's bottom-right anchoring has always
        /// assumed.
        ///
        /// <para>It was not true. Measured off the prefabs, <c>RhinoHUDVariant</c>,
        /// <c>ScarabHUDVariant</c> and <c>SparrowHUDVariant</c> stretch their root to the full
        /// canvas, while <c>DolphinHUDVariant</c> and <c>VesselHUDPrefab</c> (and therefore the
        /// Squirrel, Serpent and Manta) point-anchor theirs at the CENTRE at 100x100. Anchoring the
        /// row to (1,0) of that resolves to the screen's corner on one group and to a point 50px
        /// from the canvas centre on the other - so the row's margins meant two different things and
        /// the vessels visibly disagreed about where the row sits.</para>
        ///
        /// <para>Normalising the root is safe because by the time the lockup is done, everything it
        /// owns has moved into the row and everything else at root level has been retired - so there
        /// is nothing left whose own anchors could be disturbed. It also flattens any per-vessel HUD
        /// scale, which is the same divergence one level up.</para>
        /// </summary>
        RectTransform NormaliseHudRoot()
        {
            var self = (RectTransform)transform;
            self.anchorMin = Vector2.zero;
            self.anchorMax = Vector2.one;
            self.pivot = new Vector2(0.5f, 0.5f);
            self.offsetMin = Vector2.zero;
            self.offsetMax = Vector2.zero;
            self.localScale = Vector3.one;
            self.localRotation = Quaternion.identity;
            return self;
        }

        /// <summary>A host for a slot the vessel has no ability for yet.</summary>
        RectTransform ResolveLockedHost(RectTransform row, Element element)
            => ResolveLockedHost(row, element.ToString());

        /// <summary>
        /// The same, for a NON-elemental card. Keyed on a string rather than on the enum so the two
        /// callers share one body: the omni crystal card exists on every vessel and most vessels
        /// have no art for it, so a core card has to be able to be locked exactly as an elemental
        /// one is - which it could not before, because the core pass simply skipped a binding with
        /// no icon and the card vanished rather than reading as undesigned.
        /// </summary>
        RectTransform ResolveLockedHost(RectTransform row, string key)
        {
            string name = $"LockedSlot_{key}";
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
        ///
        /// <para><b>Normalising the scale is not enough on its own</b>: a component that CACHES the
        /// host's rest scale caches whatever the prefab authored, because its <c>Awake</c> runs
        /// long before this does - and then writes that stale value back on its next release or
        /// disable. Every Squirrel ability button is authored at 0.7 and carries
        /// <see cref="AbilityButtonPressJuice"/>, so four of its five cards sat permanently at 0.7
        /// beside the one card with no juice on it, which reads as that ONE card being oversized.
        /// The juice now captures lazily, and this hands it the new rest outright so an instance
        /// that already captured is corrected without waiting for a press.</para>
        /// </summary>
        void PlaceHost(RectTransform row, RectTransform host, int index, int count)
        {
            if (host.parent != row) host.SetParent(row, false);

            host.anchorMin = host.anchorMax = host.pivot = new Vector2(0.5f, 0.5f);
            host.sizeDelta = new Vector2(style.PlateWidth, style.AbilityCellHeight);
            host.localScale = Vector3.one;
            host.localRotation = Quaternion.identity;
            if (host.TryGetComponent<AbilityButtonPressJuice>(out var juice))
                juice.SetRestScale(Vector3.one);
            // The row is pinned to the screen's bottom-right, so a card's x is measured LEFTWARD
            // from that corner. index is signed: 0..count-1 are the elemental columns and NEGATIVE
            // values are the non-elemental cards beyond Charge, one pitch apart each.
            host.anchoredPosition = new Vector2(
                -(count - 1 - index) * style.CardPitch - style.PlateWidth * 0.5f,
                style.AbilityCellHeight * 0.5f);
            host.SetSiblingIndex(Mathf.Max(0, index));
        }

        /// <summary>
        /// Lands an ability icon in the middle of its card at the fleet's ONE drawn size, whatever
        /// size the prefab authored for it.
        ///
        /// <para><b>The scale write is load-bearing and used to be missing.</b> It was left to
        /// <c>VesselHUDController</c>'s upgrade seeding, which writes
        /// <c>VesselHUDView.AbilityIconRestScale</c> for every ELEMENT - so an elemental icon was
        /// kerned a moment after the row was built and a NON-elemental one, which no element ever
        /// seeds, never was. It therefore drew at its authored size (80) in a cell sized for 60,
        /// a third larger than its neighbours, on the one card in the row nothing else touches.
        /// Writing it here makes the row correct the instant it is laid out, and the seeding pass
        /// then writes the identical value - nothing is upgraded at build time, so the rest scale
        /// IS the content scale.</para>
        /// </summary>
        void NormaliseIcon(RectTransform iconRT)
        {
            iconRT.anchorMin = iconRT.anchorMax = iconRT.pivot = new Vector2(0.5f, 0.5f);
            iconRT.anchoredPosition = Vector2.zero;
            iconRT.localScale = Vector3.one * style.IconScaleFor(AuthoredIconSize(iconRT));
        }

        /// <summary>
        /// Builds one card. <paramref name="flowerElement"/> of <see cref="Element.None"/> is the
        /// NON-elemental case - the sentinel is what that member is for - and the element cell is
        /// then present only if the card has an EMBLEM to put in it, the rect otherwise collapsing
        /// to the ability cell alone.
        ///
        /// <para>So there are three shapes, and the upper cell is the thing that differs: an
        /// elemental card (flower), the omni crystal card (emblem), and the drift (neither). The
        /// emblem and the flower share the socket, the plate, the bloom and the Y arithmetic -
        /// what differs is only WHAT is docked, because a flower is a level readout and an emblem
        /// is a name.</para>
        ///
        /// <para>The Y arithmetic is what makes the two kinds line up, and it is exact rather than
        /// eyeballed: an elemental card is <c>PlateHeight</c> tall, offset up by
        /// <c>CardCenterOffsetY</c>, with its ability plate at <c>-CardCenterOffsetY</c> - so that
        /// plate's centre sits at the HOST's origin. A core card is <c>abilityCellHeight</c> tall at
        /// offset zero with its plate at zero, which is the same place. The control chip hangs off
        /// the card's own bottom edge either way, so the chips line up for the same reason.</para>
        /// </summary>
        Slot BuildSlot(string cardName, RectTransform host, Image icon, Element flowerElement,
                       Image emblem, Sprite emblemSprite)
        {
            bool withElementCell = flowerElement != Element.None || emblem || emblemSprite;

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
            float cardHeight = withElementCell ? style.PlateHeight : style.AbilityCellHeight;
            card.sizeDelta = new Vector2(style.PlateWidth, cardHeight);
            card.anchoredPosition = new Vector2(0f, withElementCell ? style.CardCenterOffsetY : 0f);
            card.SetSiblingIndex(0);   // behind the icon; UGUI draws siblings in order

            var slot = new Slot { Card = card, Locked = !icon };

            float narrow = style.NarrowEdgeFraction;
            float elementY = style.FlowerLocalY;
            float abilityY = withElementCell ? style.AbilityPlateLocalY : 0f;

            if (withElementCell)
            {
                // Sibling order IS draw order, and a bloom has to sit BEHIND the plate it haloes - so
                // each bloom is a sibling placed before its plate rather than a child of it. Building
                // them in this order is the whole layering contract.
                slot.ElementBloom = ResolveChildImage(card, "ElementBloom", style.BloomSprite);
                SetCellRect(slot.ElementBloom.rectTransform, elementY,
                            style.PlateWidth + style.BloomPadding * 2f,
                            style.PetalCellHeight + style.BloomPadding * 2f);
                slot.ElementBloom.color = WithAlpha(style.BloomColor, 0f);   // nothing glows at rest

                // The element plate narrows UPWARD and the ability plate narrows DOWNWARD, so the pair
                // meets wide-edge to wide-edge across the gap and reads as one waisted object.
                slot.ElementPlate = ResolveTrapezoid(card, "ElementPlate", narrow, 1f);
                SetCellRect(slot.ElementPlate.rectTransform, elementY, style.PlateWidth, style.PetalCellHeight);
                slot.ElementPlate.color = slot.Locked ? style.LockedPlateColor : style.PlateColor;
                ApplySlantEdge(slot.ElementPlate, slot.Locked, upgraded: false);
            }

            slot.AbilityBloom = ResolveChildImage(card, "AbilityBloom", style.BloomSprite);
            SetCellRect(slot.AbilityBloom.rectTransform, abilityY,
                        style.PlateWidth + style.BloomPadding * 2f,
                        style.AbilityCellHeight + style.BloomPadding * 2f);
            slot.AbilityBloom.color = WithAlpha(style.BloomColor, 0f);

            slot.AbilityPlate = ResolveTrapezoid(card, "AbilityPlate", 1f, narrow);
            SetCellRect(slot.AbilityPlate.rectTransform, abilityY, style.PlateWidth, style.AbilityCellHeight);
            slot.AbilityPlate.color = slot.Locked ? style.LockedPlateColor : style.PlateColor;
            ApplySlantEdge(slot.AbilityPlate, slot.Locked, upgraded: false);
            slot.AbilityPlate.raycastTarget = false;
            AdoptButtonTarget(host, slot.AbilityPlate);

            // The card is its ability's button (SetSlotControl decides whether it is pressable).
            if (!host.TryGetComponent(out slot.Press))
                slot.Press = host.gameObject.AddComponent<AbilityCardPress>();
            slot.Press.SetRegion(card);
            slot.Press.SetInput(InputEvents.FullSpeedStraightAction, pressable: false);

            // The gauge lines the ABILITY plate, so the fill reads as the icon filling up. It stays
            // INVISIBLE until a meter is actually adopted onto this card (AdoptGauge turns it on):
            // an empty track under an ability that has no meter is a false affordance - it reads as
            // a gauge stuck at zero.
            float gaugeFrac = Mathf.Clamp01(style.GaugeCellFraction);
            float gaugeH = style.AbilityCellHeight * gaugeFrac;
            float gaugeY = abilityY - (style.AbilityCellHeight - gaugeH) * 0.5f;   // sits on the base

            // A gauge shorter than its plate must take the plate's width AT ITS OWN TOP, not the
            // plate's full width - the ability plate is already tapering by then, so a track that
            // assumed 1.0 would overhang the shape it is supposed to line. Identical to (1, narrow)
            // at gaugeCellFraction 1, which is why the seam was invisible until someone lowered it.
            float gaugeTopEdge = Mathf.Lerp(narrow, 1f, gaugeFrac);

            // INSET inside the slant band. The track is drawn after the plate and, at
            // gaugeCellFraction 1, is exactly the same trapezoid - so at full width it painted over
            // the plate's band and the card lost its edge. That is invisible on three cards out of
            // four and shows up only on whichever slot a vessel happens to bind a meter to.
            // Insetting makes the band FRAME the meter, which is the better read anyway.
            float d = style.PlateEdgeReach;
            float gaugeW = style.PlateWidth - d * 2f;
            var gauge = new PlateRect(
                width: gaugeW,
                height: Mathf.Max(1f, gaugeH - d * 2f),
                centreY: gaugeY,
                topEdge: gaugeW > 0.01f ? (style.PlateWidth * gaugeTopEdge - d * 2f) / gaugeW : 1f,
                bottomEdge: gaugeW > 0.01f ? (style.PlateWidth * narrow - d * 2f) / gaugeW : 1f);

            slot.GaugeTrack = ResolveTrapezoid(card, "GaugeTrack", gauge.TopEdge, gauge.BottomEdge);
            SetCellRect(slot.GaugeTrack.rectTransform, gauge.CentreY, gauge.Width, gauge.Height);
            slot.GaugeTrack.color = Color.clear;

            slot.GaugeClip = ResolveGaugeClip(card, gauge);

            if (slot.Locked) BuildLockedMark(card, abilityY);

            // Press flash sits ABOVE the plate and below the icon, so a press lights the ability
            // trapezoid rather than blooming a circle behind it.
            slot.Flash = ResolveTrapezoid(card, "PressFlash", 1f, narrow);
            SetCellRect(slot.Flash.rectTransform, abilityY, style.PlateWidth, style.AbilityCellHeight);
            slot.Flash.color = WithAlpha(style.PressFlashColor, 0f);

            if (flowerElement != Element.None)
                slot.FlowerSocket = ResolveFlowerSocket(card, flowerElement);
            else if (withElementCell)
                ResolveEmblem(card, emblem, emblemSprite);

            slot.ChipSocket = ResolveChipSocket(card, cardHeight);
            return slot;
        }

        /// <summary>The rect + taper of something laid on a plate, already inset inside its band.</summary>
        readonly struct PlateRect
        {
            public readonly float Width, Height, CentreY, TopEdge, BottomEdge;
            public PlateRect(float width, float height, float centreY, float topEdge, float bottomEdge)
            {
                Width = width; Height = height; CentreY = centreY;
                TopEdge = Mathf.Clamp01(topEdge); BottomEdge = Mathf.Clamp01(bottomEdge);
            }
        }

        /// <summary>
        /// The stencil the vessel's own meter is drawn through, so a rectangular <c>Filled</c> Image
        /// reads as the trapezoid it sits in.
        ///
        /// <para>Masking rather than re-shaping is what keeps the gauge contract intact: the vessel
        /// keeps writing <c>fillAmount</c> on the very same <see cref="Image"/> it always did. The
        /// alternative - mirroring that value onto a <see cref="TrapezoidGraphic"/> - would need a
        /// per-frame poll of somebody else's field, which is a drive site this style has no business
        /// owning.</para>
        ///
        /// <para>Costs two extra draw calls on a card that HAS a meter (stencil in, stencil out).
        /// Three cards fleet-wide carry one today, so it is six.</para>
        /// </summary>
        RectTransform ResolveGaugeClip(RectTransform card, PlateRect rect)
        {
            const string name = "GaugeClip";
            var clip = card.Find(name) as RectTransform;
            if (!clip)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                                        typeof(TrapezoidGraphic), typeof(Mask));
                clip = (RectTransform)go.transform;
                clip.SetParent(card, false);
            }

            var shape = clip.GetComponent<TrapezoidGraphic>();
            shape.SetEdges(rect.TopEdge, rect.BottomEdge);
            shape.color = Color.white;          // the stencil reads ALPHA; colour is irrelevant
            shape.raycastTarget = false;

            var mask = clip.GetComponent<Mask>();
            mask.showMaskGraphic = false;       // the track already draws the shape

            SetCellRect(clip, rect.CentreY, rect.Width, rect.Height);
            return clip;
        }

        /// <summary>
        /// The placeholder an undesigned slot shows: a short bar where the icon would be. Deliberately
        /// not a padlock - the ability is not locked to the PLAYER, it does not exist yet.
        /// </summary>
        void BuildLockedMark(RectTransform card, float abilityY)
        {
            var mark = ResolveChildImage(card, "LockedMark", null);
            var rt = mark.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(style.IconBoxSize * 0.4f, style.LockedMarkThickness);
            rt.anchoredPosition = new Vector2(0f, abilityY);
            mark.color = style.LockedMarkColor;
        }

        /// <summary>
        /// Re-homes the vessel's meter into the card and restyles it as the fleet's one gauge: a
        /// linear fill rising through the ability trapezoid, behind the icon, clipped to that
        /// trapezoid's slant. The vessel keeps writing <c>fillAmount</c> on the very same Image, so
        /// no gameplay wiring changes - only where and how it draws. The legacy ring and its frame
        /// are retired with the rest of the host's chrome.
        /// </summary>
        void AdoptGauge(Slot slot, Image gauge)
        {
            if (slot.Locked || !gauge) return;

            var rt = gauge.rectTransform;
            if (rt.parent != slot.GaugeClip) rt.SetParent(slot.GaugeClip, false);

            StretchTo(rt, 0f);                              // the clip owns the size and the shape
            slot.GaugeTrack.color = style.GaugeTrackColor;  // there IS a meter here now

            // Filled needs a sprite - Image draws a plain quad and IGNORES fillAmount when the
            // sprite is null - and it must be a plain box, because the stencil is what shapes this,
            // not the art. Any silhouette in the sprite would punch notches inside the trapezoid.
            gauge.sprite = PlainSprite;
            gauge.type = Image.Type.Filled;
            gauge.fillMethod = Image.FillMethod.Vertical;
            gauge.fillOrigin = (int)Image.OriginVertical.Bottom;
            gauge.preserveAspect = false;
            gauge.raycastTarget = false;
            gauge.color = style.GaugeFillColor;
        }

        /// <summary>
        /// A plain white box for anything that needs a sprite only because Unity demands one - the
        /// gauge's <c>Type.Filled</c>. Built from <c>Texture2D.whiteTexture</c> so it costs no
        /// asset, no import settings and nothing to keep in sync with the style.
        /// </summary>
        static Sprite _plainSprite;
        static Sprite PlainSprite =>
            _plainSprite ? _plainSprite
                         : _plainSprite = Sprite.Create(Texture2D.whiteTexture,
                                                        new Rect(0f, 0f, Texture2D.whiteTexture.width,
                                                                 Texture2D.whiteTexture.height),
                                                        new Vector2(0.5f, 0.5f));

        /// <summary>
        /// Switches off the HUD's own root-level legacy content: <b>anything no component on this
        /// HUD still references.</b>
        ///
        /// <para><see cref="RetireLegacyChrome"/> works per ability HOST, so it only ever reached
        /// what sat around an icon. Everything else a vessel drew before the lockup kept drawing
        /// beside the new row - old boost rings on the Rhino and Sparrow, an ammo readout nothing
        /// writes to, and the authored device-glyph roots. Every one of those glyph roots is now a
        /// leftover: the card draws its own control chip, so a second set of glyphs is a competing
        /// display no matter which vessel authored it or whether a switcher can still toggle it.</para>
        ///
        /// <para>By the time this runs the lockup has moved everything it owns into the row, so a
        /// root-level child that still draws is either something the vessel actively drives or
        /// something nothing drives. Asking which is a REFERENCE question, and it is asked rather
        /// than guessed: a branch survives if any component on the HUD root - the view, its
        /// controller - or the icon-set switcher still points into it. A live readout is spared
        /// automatically on any vessel that wires one; a leftover is retired on every vessel that
        /// does not, with no per-vessel list to maintain and no name to match.</para>
        ///
        /// <para>Reflection, once, at build. It is the only way to answer "does anything still
        /// drive this?" without hand-authoring the answer per vessel, which is the exact
        /// duplication this style exists to remove.</para>
        /// </summary>
        void RetireLegacyHudContent(RectTransform row, bool anyIconBound)
        {
            var self = NormaliseHudRoot();

            // A vessel that binds NO ability icon has no designed row: all four cards render LOCKED,
            // and everything else at root level is, by definition, the HUD it had before the lockup.
            // On those vessels the reference guard answers the wrong question - it asks "is this
            // driven?" when what matters is "is this the old UI?", and on the Rhino the answer is
            // BOTH. Its status cluster is genuinely driven (six readouts the view writes every
            // frame) AND anchored in the bottom-right corner the row now occupies, so sparing it
            // leaves two visual languages stacked in one seat. The slate is cleared instead.
            //
            // Stated cost, because it is a real one: the Rhino's debuff timer, slowed count,
            // skimmer-size ring, laser and crystal indicators stop being shown until they are
            // re-homed into the lockup or re-authored. Manta and Serpent are in the same position
            // today. The view keeps writing to them - they are switched off, not unwired - so
            // restoring one is re-activating a branch, not rebuilding a readout.
            var referenced = anyIconBound ? CollectReferencedObjects() : null;

            for (int i = self.childCount - 1; i >= 0; i--)
            {
                var child = self.GetChild(i);
                if (!child || child == row) continue;
                if (!child.gameObject.activeSelf) continue;
                if (!child.GetComponentInChildren<Graphic>(true)) continue;   // logic, not UI
                if (referenced != null && BranchIsReferenced(child, referenced)) continue;

                child.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Everything the HUD's own drivers point at. Sources are deliberately limited to components
        /// on the HUD ROOT: a component sitting INSIDE a leftover branch would otherwise reference
        /// its own children and spare the branch it belongs to.
        ///
        /// <para>The icon-set switcher used to be consulted here too, to spare its three device-glyph
        /// roots. That reference was the ONLY thing keeping those roots alive on the three HUDs that
        /// authored them - a second glyph display drawn beside the card's own chip. The switcher now
        /// detects the device and draws nothing, so it holds no reference to spare and the roots
        /// retire like any other leftover. Same rule as the retired press glow:
        /// <b>a reference from something the lockup superseded is not evidence anything still uses
        /// it.</b></para>
        /// </summary>
        HashSet<Object> CollectReferencedObjects()
        {
            var referenced = new HashSet<Object>();

            foreach (var component in GetComponents<MonoBehaviour>())
                if (component && component != this) CollectSerializedReferences(component, referenced);

            return referenced;
        }

        static bool BranchIsReferenced(Transform branch, HashSet<Object> referenced)
        {
            if (referenced.Count == 0) return false;

            foreach (var component in branch.GetComponentsInChildren<Component>(true))
                if (component && referenced.Contains(component)) return true;

            foreach (var t in branch.GetComponentsInChildren<Transform>(true))
                if (t && referenced.Contains(t.gameObject)) return true;

            return false;
        }

        static void CollectSerializedReferences(Component source, HashSet<Object> into)
        {
            const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public |
                                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            for (var type = source.GetType();
                 type != null && type != typeof(MonoBehaviour) && type != typeof(Component);
                 type = type.BaseType)
            {
                foreach (var field in type.GetFields(Fields))
                {
                    if (field.Name == RetiredHighlightsField) continue;
                    AddReference(field.GetValue(source), into, 0);
                }
            }
        }

        /// <summary>
        /// Records a field's Unity references, stepping one level into lists and into the small
        /// [Serializable] payloads this codebase uses to group a binding (an ability icon and its
        /// gauge, a hint and its sprites). Depth is capped because a HUD field graph is shallow and
        /// an uncapped walk would wander into engine types.
        /// </summary>
        static void AddReference(object value, HashSet<Object> into, int depth)
        {
            switch (value)
            {
                case null: return;
                case Object unityObject:
                    if (unityObject) into.Add(unityObject);
                    return;
                case string: return;
            }

            if (depth >= MaxReferenceDepth) return;

            if (value is System.Collections.IEnumerable sequence)
            {
                foreach (var element in sequence) AddReference(element, into, depth + 1);
                return;
            }

            // Only the project's own [Serializable] payloads - never engine structs, which are
            // numbers all the way down.
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) return;
            if (type.Namespace == null || type.Namespace.StartsWith("Unity")) return;

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                 BindingFlags.NonPublic))
                AddReference(field.GetValue(value), into, depth + 1);
        }

        const int MaxReferenceDepth = 3;

        /// <summary>
        /// The one field a reference from does NOT count as "still driven". <c>highlights</c> is the
        /// legacy per-vessel press glow, which the card superseded - the Rhino's entry points inside
        /// its old boost container, so honouring it would spare exactly the chrome the row replaced.
        /// A reference from something the lockup retired is not evidence that anything still uses it.
        /// </summary>
        const string RetiredHighlightsField = "highlights";

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
            socket.sizeDelta = new Vector2(style.PetalFlowerSize, style.PetalFlowerSize);
            socket.anchoredPosition = new Vector2(0f, style.FlowerLocalY);
            socket.localScale = Vector3.one;
            socket.localRotation = Quaternion.identity;
            return socket;
        }

        /// <summary>
        /// Docks a non-elemental card's upper-plate MARK. An authored <paramref name="emblem"/> is
        /// re-homed (the same reasoning as an authored flower container: moving the transform keeps
        /// whatever the vessel put in it); otherwise one is built from the fleet-wide sprite.
        ///
        /// <para>It is deliberately NOT tinted. The emblem answers "what is this card", which is
        /// the same answer on every hull and for every domain - and the omni crystal in particular
        /// belongs to nobody until somebody takes it, so painting it a team colour would be a
        /// claim the crystal itself never makes (<c>Docs/PALETTE.md §2.2</c>: a crystal's colour
        /// says who may COLLECT it).</para>
        /// </summary>
        RectTransform ResolveEmblem(RectTransform card, Image emblem, Sprite emblemSprite)
        {
            const string emblemName = "ElementEmblem";

            if (!emblem)
            {
                emblem = ResolveChildImage(card, emblemName, emblemSprite);
            }
            else if (emblem.transform.parent != card)
            {
                emblem.transform.SetParent(card, false);
            }

            // An Image with no sprite draws a SOLID QUAD, which on a plate this size is the plate
            // painted over. Nothing to draw means draw nothing.
            emblem.enabled = emblem.sprite;
            emblem.color = Color.white;
            emblem.raycastTarget = false;

            var rt = emblem.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(style.PetalFlowerSize, style.PetalFlowerSize);
            rt.anchoredPosition = new Vector2(0f, style.FlowerLocalY);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            return rt;
        }

        RectTransform ResolveChipSocket(RectTransform card, float cardHeight)
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
            socket.sizeDelta = new Vector2(style.PlateWidth, style.ChipHeight);
            socket.anchoredPosition = new Vector2(0f, -cardHeight * 0.5f - style.ChipGap - style.ChipHeight * 0.5f);
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

            // Borderless: with the rim retired, the bloom and the plate lift ARE the upgrade signal,
            // and BOTH trapezoids take it - the element upgraded the ability, so the pair is what
            // changed state, not one half of it.
            Color bloomTarget = upgraded ? style.BloomColor : WithAlpha(style.BloomColor, 0f);
            Color plateTarget = upgraded ? style.UpgradedPlateColor : style.PlateColor;

            // The slant edge crosses with them - it is part of the plate, not a separate frame, and
            // it is the one channel with enough contrast left to read at a glance on a dark plate.
            ApplySlantEdge(slot.ElementPlate, locked: false, upgraded, slot);
            ApplySlantEdge(slot.AbilityPlate, locked: false, upgraded, slot);

            if (!animate)
            {
                if (slot.ElementBloom) slot.ElementBloom.color = bloomTarget;
                if (slot.AbilityBloom) slot.AbilityBloom.color = bloomTarget;
                if (slot.ElementPlate) slot.ElementPlate.color = plateTarget;
                if (slot.AbilityPlate) slot.AbilityPlate.color = plateTarget;
                return;
            }

            float d = style.UpgradeTransitionDuration;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(slot.Card.gameObject);
            if (slot.ElementBloom) seq.Join(slot.ElementBloom.DOColor(bloomTarget, d).SetEase(Ease.OutCubic));
            if (slot.AbilityBloom) seq.Join(slot.AbilityBloom.DOColor(bloomTarget, d).SetEase(Ease.OutCubic));
            if (slot.ElementPlate) seq.Join(slot.ElementPlate.DOColor(plateTarget, d).SetEase(Ease.OutCubic));
            if (slot.AbilityPlate) seq.Join(slot.AbilityPlate.DOColor(plateTarget, d).SetEase(Ease.OutCubic));

            if (upgraded && style.UnlockPunchScale > 1f)
            {
                slot.Card.localScale = Vector3.one;
                seq.Join(slot.Card
                    .DOPunchScale(Vector3.one * (style.UnlockPunchScale - 1f),
                                  style.UnlockPunchDuration, 1, 0.6f));
            }

            slot.Tween = seq;
        }

        /// <summary>
        /// Lights the CARD on a press. This is the totem's answer to the per-vessel circular glow,
        /// which was authored to sit behind a round button and reads as a foreign shape now.
        /// </summary>
        /// <summary>
        /// The fleet's ONE recharge readout: a radial veil swept over the ability plate while an
        /// ability is recovering, ending in a one-shot flash the moment it comes back.
        /// <paramref name="remaining01"/> is 1 the instant the ability fires and 0 when it is ready.
        ///
        /// <para><b>Radial, where the gauge is linear - and OVER the icon, where the gauge is
        /// behind it.</b> Two motions that cannot be mistaken for each other is what lets one card
        /// carry both, which several vessels need (an ability that both banks a resource and has a
        /// recharge). A cooldown drawn as another rising fill would read as the meter running
        /// backwards.</para>
        ///
        /// <para>The overlay is built LAZILY, on the first call: a card whose ability has no
        /// cooldown pays nothing - no object, no mask, no draw calls. That is also why this is a
        /// VALUE the vessel pushes rather than an <c>Image</c> it binds like the gauge: there is no
        /// existing per-vessel artwork to preserve, so the lockup owns the whole presentation and a
        /// vessel supplies one float.</para>
        ///
        /// <para><b>A LOCKED card draws it too</b>, unlike every other state on this view. Locked
        /// means "this vessel has not authored an icon for this slot", which is a fact about the
        /// ART - and a slot that is being DRIVEN is an ability that exists, whatever the row can
        /// draw of it. The Serpent is the case that proved it: its sniper's recharge was pushed
        /// correctly from <c>SerpentHUDController</c> from the day the ability shipped, and
        /// landed nowhere for three rounds of playtest because that vessel binds 0 of its 4 icons.
        /// The veil needs no icon anyway - it sizes itself on the ability PLATE, which a locked
        /// card has. The general rule: <i>an indicator that refuses to draw because its
        /// decoration is missing is indistinguishable from an indicator nobody is driving.</i>
        /// <see cref="SetUpgraded"/> is deliberately NOT changed: an upgrade is a statement about
        /// an ability the player can see, and there is nothing there to light.</para>
        /// </summary>
        public void SetAbilityCooldown(Element element, float remaining01)
        {
            if (_slots.TryGetValue(element, out var slot)) SetSlotCooldown(slot, remaining01);
        }

        /// <summary>The same recharge veil on a NON-elemental card.</summary>
        public void SetCoreAbilityCooldown(CoreAbility ability, float remaining01)
        {
            if (_coreSlots.TryGetValue(ability, out var slot)) SetSlotCooldown(slot, remaining01);
        }

        void SetSlotCooldown(Slot slot, float remaining01)
        {
            if (!style) return;

            remaining01 = Mathf.Clamp01(remaining01);
            bool active = remaining01 > 0.0001f;

            // Sitting ready is the common case and this is polled every frame, so it costs nothing:
            // writing fillAmount dirties the Image's mesh, which is fine while a sweep is moving and
            // pure waste while it is parked at zero.
            if (!active && !slot.OnCooldown) return;

            if (!slot.CooldownSweep)
            {
                if (!active) return;                                // never fired: build nothing
                BuildCooldownOverlay(slot);
                if (!slot.CooldownSweep) return;
            }

            slot.CooldownSweep.fillAmount = remaining01;
            if (slot.CooldownClip.gameObject.activeSelf != active)
                slot.CooldownClip.gameObject.SetActive(active);

            // The beat the player is actually waiting for. Only on the falling edge - driving this
            // per frame would restart the flash every frame it sat at ready.
            if (slot.OnCooldown && !active) PlayReadyFlash(slot);
            slot.OnCooldown = active;
        }

        /// <summary>True while this ability is drawn as recharging.</summary>
        public bool IsOnCooldown(Element element)
            => _slots.TryGetValue(element, out var slot) && slot.OnCooldown;

        /// <summary>True while this non-elemental ability is drawn as recharging.</summary>
        public bool IsCoreOnCooldown(CoreAbility ability)
            => _coreSlots.TryGetValue(ability, out var slot) && slot.OnCooldown;

        /// <summary>
        /// The cooldown veil is the ONE piece of the lockup that lives outside the card: it has to
        /// darken the ICON as well as the plate, and the icon is a later sibling of the card, so a
        /// child of the card could only ever draw behind it. It is parented to the host and pushed
        /// to the end of the sibling list instead.
        /// </summary>
        void BuildCooldownOverlay(Slot slot)
        {
            var host = slot.Card.parent as RectTransform;
            if (!host) return;

            const string clipName = "AbilityCooldown";
            var clip = host.Find(clipName) as RectTransform;
            if (!clip)
            {
                var go = new GameObject(clipName, typeof(RectTransform), typeof(CanvasRenderer),
                                        typeof(TrapezoidGraphic), typeof(Mask));
                clip = (RectTransform)go.transform;
                clip.SetParent(host, false);
            }

            var shape = clip.GetComponent<TrapezoidGraphic>();
            shape.SetEdges(1f, style.NarrowEdgeFraction);
            shape.color = Color.white;      // a stencil reads ALPHA; its colour never renders
            shape.raycastTarget = false;
            clip.GetComponent<Mask>().showMaskGraphic = false;

            // Sized and placed on the ability plate in HOST space, where the plate sits at zero.
            clip.anchorMin = clip.anchorMax = clip.pivot = new Vector2(0.5f, 0.5f);
            clip.sizeDelta = new Vector2(style.PlateWidth, style.AbilityCellHeight);
            clip.anchoredPosition = Vector2.zero;
            clip.localScale = Vector3.one;
            clip.localRotation = Quaternion.identity;
            clip.SetAsLastSibling();        // over the icon - the point of living out here

            var sweep = ResolveChildImage(clip, "Sweep", PlainSprite);
            // The sweep has to cover the trapezoid's corners at every angle, so it is squared off to
            // the plate's diagonal rather than its width - a disc inscribed in the rect would leave
            // the corners permanently lit.
            float reach = Mathf.Sqrt(style.PlateWidth * style.PlateWidth +
                                     style.AbilityCellHeight * style.AbilityCellHeight);
            SetCellRect(sweep.rectTransform, 0f, reach, reach);
            sweep.type = Image.Type.Filled;
            sweep.fillMethod = Image.FillMethod.Radial360;
            sweep.fillOrigin = (int)Image.Origin360.Top;

            // fillClockwise FALSE is what makes the sweep read CLOCKWISE, which looks backwards
            // until you remember the veil DEPLETES. The flag describes the direction the filled
            // wedge is drawn from the origin; we animate fillAmount DOWNWARD, so the edge the player
            // actually watches is the wedge's far end travelling the other way. Clockwise-true drew
            // the wedge clockwise from the top and therefore retreated anticlockwise. False sweeps
            // the cleared area clockwise out of the top, which is the classic cooldown read.
            sweep.fillClockwise = false;
            sweep.preserveAspect = false;
            sweep.color = style.CooldownVeilColor;

            slot.CooldownClip = clip;
            slot.CooldownSweep = sweep;
        }

        void PlayReadyFlash(Slot slot)
        {
            if (!slot.Flash) return;

            slot.FlashTween?.Kill();
            slot.Flash.color = style.CooldownReadyFlashColor;
            slot.FlashTween = slot.Flash
                .DOFade(0f, style.CooldownReadyFlashDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(slot.Flash.gameObject);
        }

        /// <summary>
        /// Tells this card which physical controls fire its ability, so the lockup can draw the
        /// control chip itself from the fleet's one glyph set. Called by
        /// <see cref="VesselHUDController"/>, which is where the vessel's ability map lives.
        ///
        /// <para>This is what replaced per-vessel authored glyphs. Three HUDs carried device-icon
        /// roots with no switcher to drive them - never lit, never matched to the player's device,
        /// and left stranded when the lockup moved the row - and one of them had authored a pad set
        /// that did not even correspond between families. Deriving the chip means a vessel authors
        /// no glyphs and none of that can recur.</para>
        /// </summary>
        public void SetAbilityControl(Element element, InputEvents input)
        {
            if (_slots.TryGetValue(element, out var slot)) SetSlotControl(slot, input);
        }

        /// <summary>
        /// A card's chip named by its CONTROL rather than by an input event - for an ability driven by
        /// a gesture no <see cref="InputEvents"/> button names (the Rhino's ramp is the dual-stick
        /// throttle pushed apart; <c>FullSpeedStraightAction</c> is also the passive sentinel and draws
        /// nothing, deliberately, for every passive card in the fleet). Overrides whatever the map's
        /// input seeded on this card.
        /// </summary>
        public void SetAbilityControlBinding(Element element, InputDeviceIconSetSwitcher.HintBinding pad,
                                             InputDeviceIconSetSwitcher.HintBinding key)
        {
            if (!_slots.TryGetValue(element, out var slot)) return;
            slot.PadBinding = pad;
            slot.KeyBinding = key;
            RefreshChip(slot);

            // A tap presses the control's own event when it has one; a gesture no button names
            // (the Rhino's sticks pushed apart) cannot be pressed from a card.
            var events = InputHintBindingMap.InputEventsFor(InputHintBindingMap.Canonical(pad));
            SetSlotPressInput(slot, events.Count > 0 ? events[0] : InputEvents.FullSpeedStraightAction);
        }

        /// <summary>The same chip on a NON-elemental card.</summary>
        public void SetCoreAbilityControl(CoreAbility ability, InputEvents input)
        {
            if (_coreSlots.TryGetValue(ability, out var slot)) SetSlotControl(slot, input);
        }

        void SetSlotControl(Slot slot, InputEvents input)
        {
            slot.PadBinding = InputHintBindingMap.BindingFor(input, keyboard: false);
            slot.KeyBinding = InputHintBindingMap.BindingFor(input, keyboard: true);
            RefreshChip(slot);
            SetSlotPressInput(slot, input);
        }

        /// <summary>
        /// Arms the card's tap to press <paramref name="input"/>. A locked card or the passive sentinel
        /// is not pressable. A pressable card's plates take hits, so a finger anywhere on the totem counts.
        /// </summary>
        void SetSlotPressInput(Slot slot, InputEvents input)
        {
            if (!slot.Press) return;
            bool pressable = !slot.Locked && input != InputEvents.FullSpeedStraightAction;
            slot.Press.SetInput(input, pressable);
            if (!pressable) return;
            if (slot.AbilityPlate) slot.AbilityPlate.raycastTarget = true;
            if (slot.ElementPlate) slot.ElementPlate.raycastTarget = true;
        }

        /// <summary>Who a tapped card presses for: the pilot's input status and the vessel's action handler.</summary>
        public void SetPressSource(IInputStatus input, R_VesselActionHandler actions)
        {
            foreach (var slot in _slots.Values)
                if (slot.Press) slot.Press.Bind(input, actions);
            foreach (var slot in _coreSlots.Values)
                if (slot.Press) slot.Press.Bind(input, actions);
        }

        /// <summary>Which device the chips should speak for. Pushed on every set change.</summary>
        public void SetControlDevice(bool keyboard)
        {
            if (_chipKeyboard == keyboard && _chipDeviceKnown) return;
            _chipKeyboard = keyboard;
            _chipDeviceKnown = true;
            foreach (var slot in _slots.Values) RefreshChip(slot);
            foreach (var slot in _coreSlots.Values) RefreshChip(slot);
        }

        bool _chipKeyboard;
        bool _chipDeviceKnown;

        void RefreshChip(Slot slot)
        {
            var set = ResolveGlyphSet();
            if (!set || !slot.ChipSocket) return;

            var binding = _chipKeyboard ? slot.KeyBinding : slot.PadBinding;
            // A locked slot has no ability, so it has no control to name (the board's "dim plate,
            // a dash, no chip").
            // A phone or tablet with no pad names no controls at all (KitInputDevice).
            var glyph = slot.Locked || !KitInputDevice.ShowsControlHints ? null : set.For(binding);

            // A chip depicts a BUTTON, so it wears the button's own held art while the ability is
            // firing - the one thing the retired hint visuals did that the card cannot say, since
            // the card lights the ability and this lights the control. Both are the same press.
            bool held = slot.Pressed;

            // Style B: the control's NAME on a keycap, on either device. A pad control with no
            // name authored falls through to its artwork below.
            string keyName = glyph == null ? null : _chipKeyboard ? glyph.keyboardLabel : glyph.padLabel;
            bool keycap = style && style.KeycapChips && !string.IsNullOrEmpty(keyName);
            DrawKeycap(slot, keycap ? keyName : null, held, set);

            // A passive ability has no button, and several pad buttons have no keyboard equivalent.
            // Both draw NOTHING rather than borrowing another device's picture.
            bool showGlyph = !keycap && glyph != null && !_chipKeyboard && glyph.padGlyph;
            bool showLabel = !keycap && glyph != null && _chipKeyboard && !string.IsNullOrEmpty(glyph.keyboardLabel);

            if (showGlyph || slot.ChipGlyph)
            {
                slot.ChipGlyph ??= ResolveChildImage(slot.ChipSocket, "Glyph", null);
                StretchTo(slot.ChipGlyph.rectTransform, 0f);
                slot.ChipGlyph.preserveAspect = true;
                slot.ChipGlyph.sprite = !showGlyph ? null
                                      : held && glyph.padGlyphHeld ? glyph.padGlyphHeld
                                                                   : glyph.padGlyph;
                slot.ChipGlyph.color = held ? set.HeldColor : set.RestColor;
                slot.ChipGlyph.enabled = showGlyph;
            }

            if (showLabel || slot.ChipLabel)
            {
                slot.ChipLabel ??= ResolveChipLabel(slot.ChipSocket, set);
                slot.ChipLabel.text = showLabel ? glyph.keyboardLabel : string.Empty;
                slot.ChipLabel.color = held ? set.HeldColor : set.RestColor;
                slot.ChipLabel.enabled = showLabel;
            }
        }

        /// <summary>
        /// The style-B keycap: a slivered socket that hugs the control's name, centred in the
        /// chip socket under its card. <paramref name="keyName"/> null hides it. Held, the key
        /// reads as pressed in - a lit fill with dark text - which is the same press the card's
        /// flash shows, said by the control rather than by the ability.
        /// </summary>
        void DrawKeycap(Slot slot, string keyName, bool held, ControlGlyphSetSO set)
        {
            if (string.IsNullOrEmpty(keyName))
            {
                if (slot.Keycap) slot.Keycap.gameObject.SetActive(false);
                return;
            }

            if (!slot.Keycap)
            {
                var existing = slot.ChipSocket.Find("Keycap") as RectTransform;
                if (!existing)
                {
                    existing = (RectTransform)new GameObject("Keycap", typeof(RectTransform)).transform;
                    existing.SetParent(slot.ChipSocket, false);
                }
                slot.Keycap = existing;
                slot.KeycapFill = ResolveChildImage(existing, "Fill", style.KeycapFillSprite);
                slot.KeycapBorder = ResolveChildImage(existing, "Border", style.KeycapBorderSprite);
                StretchTo(slot.KeycapFill.rectTransform, 0f);
                StretchTo(slot.KeycapBorder.rectTransform, 0f);
                slot.KeycapLabel = ResolveChipLabel(existing, set);
                slot.KeycapLabel.fontSize = style.KeycapTextSize;
                if (style.KeycapFont) slot.KeycapLabel.font = style.KeycapFont;
                slot.KeycapLabel.textWrappingMode = TextWrappingModes.NoWrap;
            }

            slot.Keycap.gameObject.SetActive(true);
            slot.KeycapLabel.text = keyName;
            slot.KeycapLabel.color = CosmicShore.UI.UITheme.Resolve(null, held ? style.KeycapHeldTextToken : style.KeycapTextToken);
            slot.KeycapFill.color = CosmicShore.UI.UITheme.Resolve(null, held ? style.KeycapHeldFillToken : style.KeycapFillToken);
            // Rev 3: the keycap's edge in the card's element colour, so control and ability read as one.
            slot.KeycapBorder.color = slot.HasTint ? slot.Tint : CosmicShore.UI.UITheme.Resolve(null, style.KeycapBorderToken);

            float width = Mathf.Min(style.PlateWidth,
                                    slot.KeycapLabel.GetPreferredValues(keyName).x + style.KeycapPadding * 2f);
            slot.Keycap.anchorMin = slot.Keycap.anchorMax = slot.Keycap.pivot = new Vector2(0.5f, 0.5f);
            slot.Keycap.sizeDelta = new Vector2(Mathf.Max(width, style.ChipHeight), style.ChipHeight);
            slot.Keycap.anchoredPosition = Vector2.zero;
            // Held, the key reads as pressed IN: lit and a touch smaller.
            slot.Keycap.localScale = held ? Vector3.one * 0.92f : Vector3.one;
            slot.Keycap.localRotation = Quaternion.identity;
        }

        TMP_Text ResolveChipLabel(RectTransform socket, ControlGlyphSetSO set)
        {
            const string name = "Label";
            var existing = socket.Find(name);
            var label = existing ? existing.GetComponent<TMP_Text>() : null;
            if (!label)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                                        typeof(TextMeshProUGUI));
                go.transform.SetParent(socket, false);
                label = go.GetComponent<TextMeshProUGUI>();
            }

            StretchTo(label.rectTransform, 0f);
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = set.KeyboardLabelSize;
            label.raycastTarget = false;
            return label;
        }

        ControlGlyphSetSO ResolveGlyphSet()
        {
            if (!glyphSet) glyphSet = Resources.Load<ControlGlyphSetSO>(glyphSetResourcePath);
            return glyphSet;
        }

        public void PlayPressFlash(Element element) => SetPressed(element, true, releaseImmediately: true);

        /// <summary>
        /// The card's press state - the fleet's ONE "this ability is firing" signal, replacing the
        /// per-vessel circular glow that used to be switched on behind the icon. It lights the whole
        /// card rather than drawing a second shape, so a held ability reads at a glance and a card
        /// never grows chrome the totem does not own.
        ///
        /// <para>It lights the ABILITY trapezoid, not the whole totem: an upgrade is a change to
        /// the ability-plus-element pair and lights both plates, a press is the ability firing and
        /// lights the one you pressed. Two states that lit the same area would be one signal.</para>
        ///
        /// <para>Held while the control is down, then decayed rather than switched off - the same
        /// continuity rule the rest of the game follows: nothing pops out of existence.</para>
        /// </summary>
        public void SetAbilityPressed(Element element, bool pressed) => SetPressed(element, pressed, false);

        void SetPressed(Element element, bool pressed, bool releaseImmediately)
        {
            if (_slots.TryGetValue(element, out var slot)) SetSlotPressed(slot, pressed, releaseImmediately);
        }

        /// <summary>The same press signal on a NON-elemental card.</summary>
        public void SetCoreAbilityPressed(CoreAbility ability, bool pressed)
        {
            if (_coreSlots.TryGetValue(ability, out var slot)) SetSlotPressed(slot, pressed, false);
        }

        /// <summary>A one-shot flash of a NON-elemental card.</summary>
        public void PlayCoreAbilityFlash(CoreAbility ability)
        {
            if (_coreSlots.TryGetValue(ability, out var slot)) SetSlotPressed(slot, true, true);
        }

        void SetSlotPressed(Slot slot, bool pressed, bool releaseImmediately)
        {
            if (!style || !slot.Flash) return;

            // A one-shot flash is not a HOLD: PlayPressFlash presses and releases in the same call,
            // so latching the chip on it would leave the control drawn as held for good.
            bool holding = pressed && !releaseImmediately;
            if (slot.Pressed != holding)
            {
                slot.Pressed = holding;
                RefreshChip(slot);   // the chip wears the control's held art, the card the ability's
            }

            slot.FlashTween?.Kill();
            slot.FlashTween = null;

            if (pressed) slot.Flash.color = style.PressFlashColor;

            if (!pressed || releaseImmediately)
            {
                slot.FlashTween = slot.Flash
                    .DOFade(0f, style.PressFlashDuration)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true)
                    .SetLink(slot.Flash.gameObject);
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        void AdoptButtonTarget(RectTransform host, TrapezoidGraphic plate)
        {
            var button = host.GetComponent<Button>();
            if (!button) return;

            button.targetGraphic = plate;
            plate.raycastTarget = true;   // the plate is the touch target now, so it must be hittable
        }

        /// <summary>
        /// The band on a plate's two SLOPED sides - solid the whole length of each slant, wrapping
        /// around both corners onto the horizontals and grading to nothing there, so it accents the
        /// edges that carry the shape and never closes into the border this style retired. A locked slot wears it at the locked
        /// mark's colour, so an undesigned slot is quiet in every channel at once.
        /// </summary>
        void ApplySlantEdge(TrapezoidGraphic plate, bool locked, bool upgraded, Slot tinted = null)
        {
            if (!plate) return;
            plate.EdgeThickness = style.SlantEdgeThickness;
            plate.EdgeWrap = style.SlantEdgeWrap;
            plate.EdgeAntialias = style.SlantEdgeAntialias;
            plate.EdgeColor = locked ? style.LockedMarkColor
                                     : upgraded ? style.UpgradedSlantEdgeColor
                                                : tinted is { HasTint: true } ? WithAlpha(tinted.Tint, style.ElementEdgeAlpha)
                                                                              : style.SlantEdgeColor;
        }

        // ---------------------------------------------------------------
        // Element colour - the rev 3 lava lamp
        // ---------------------------------------------------------------

        /// <summary>The omni card's edge: near-white, because its blobs carry all four elements.</summary>
        static readonly Color OmniEdge = new(0.9f, 0.92f, 1f, 1f);

        /// <summary>
        /// Gives a built card its element's colour (rev 3, approved 2026-10-16): two soft blobs per plate
        /// drifting behind the icon like a lava lamp, the slant edge and the keycap edge in the same
        /// colour. The omni card's blobs are the four elements. A LOCKED card stays dim and still - it
        /// has no ability for the colour to qualify. The flower petals are never touched: their level
        /// ladder is the HUD's vocabulary.
        ///
        /// <para>Each plate's blobs sit in a soft-edged <see cref="RectMask2D"/> the size of the plate,
        /// drawn right above it and below the gauge, flash and icon. RectMask2D rather than the stencil
        /// Mask the gauge uses: it costs no extra draw calls, and the soft edge hides the corners where a
        /// rectangle and a trapezoid disagree.</para>
        /// </summary>
        void ApplyElementColour(Slot slot, Color colour, bool omni)
        {
            if (slot == null || !style || !style.ElementColour || slot.Locked) return;
            slot.HasTint = true;
            slot.Tint = colour;
            if (slot.ElementPlate) BuildLava(slot.Card, slot.ElementPlate, colour, omni, 0);
            if (slot.AbilityPlate) BuildLava(slot.Card, slot.AbilityPlate, colour, omni, 2);
            ApplySlantEdge(slot.ElementPlate, false, slot.Upgraded, slot);
            ApplySlantEdge(slot.AbilityPlate, false, slot.Upgraded, slot);
        }

        void BuildLava(RectTransform card, TrapezoidGraphic plate, Color colour, bool omni, int omniOffset)
        {
            string clipName = plate.name + "Lava";
            var clip = card.Find(clipName) as RectTransform;
            if (!clip)
            {
                clip = (RectTransform)new GameObject(clipName, typeof(RectTransform), typeof(RectMask2D)).transform;
                clip.SetParent(card, false);
            }
            var prt = plate.rectTransform;
            SetCellRect(clip, prt.anchoredPosition.y, prt.sizeDelta.x - style.PlateEdgeReach * 2f, prt.sizeDelta.y - style.PlateEdgeReach * 2f);
            var mask = clip.GetComponent<RectMask2D>();
            mask.softness = new Vector2Int(10, 10);
            clip.SetSiblingIndex(plate.transform.GetSiblingIndex() + 1);

            Color.RGBToHSV(colour, out float h, out float sat, out float val);
            var second = Color.HSVToRGB(Mathf.Repeat(h + 0.06f, 1f), sat, val * 0.95f);
            Color[] omniColours = { style.ChargeColour, style.MassColour, style.SpaceColour, style.TimeColour };
            for (int i = 0; i < 2; i++)
            {
                var blob = ResolveChildImage(clip, $"Blob{i}", style.BloomSprite);
                var c = omni ? omniColours[(i + omniOffset) % 4] : i == 0 ? colour : second;
                blob.color = WithAlpha(c, style.LavaAlpha * (i == 0 ? 1f : 0.8f));
                float d = style.PlateWidth * style.LavaBlobScale * (i == 0 ? 1f : 0.8f);
                var rt = blob.rectTransform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(d, d);
                var home = i == 0 ? new Vector2(-10f, -6f) : new Vector2(12f, 8f);
                rt.anchoredPosition = home;
                // Every blob on its own clock (golden-ratio spread), so the row never pulses in step.
                float k = Mathf.Repeat((_blobs.Count + 1) * 0.618034f, 1f);
                _blobs.Add(new LavaBlob
                {
                    Rt = rt,
                    Home = home,
                    Period = Mathf.Lerp(style.LavaPeriod.x, style.LavaPeriod.y, k),
                    Phase = k * Mathf.PI * 2f,
                });
            }
        }

        void Update()
        {
            if (_blobs.Count == 0 || !style || !style.LavaMotion) return;
            float t = Time.unscaledTime;
            for (int i = 0; i < _blobs.Count; i++)
            {
                var b = _blobs[i];
                if (!b.Rt) continue;
                float a = t * (Mathf.PI * 2f) / Mathf.Max(0.1f, b.Period) + b.Phase;
                b.Rt.anchoredPosition = b.Home + new Vector2(Mathf.Sin(a), Mathf.Cos(a * 0.77f)) * style.LavaDrift;
                float sc = 1f + 0.18f * Mathf.Sin(a * 0.6f);
                b.Rt.localScale = new Vector3(sc, sc, 1f);
            }
        }

        /// <summary>
        /// One of the card's generated plates. <paramref name="top"/> and <paramref name="bottom"/>
        /// are edge widths as fractions of the rect - both always derived from the style's single
        /// inset, mirrored, so the two halves of a totem can never disagree about the slant.
        /// </summary>
        TrapezoidGraphic ResolveTrapezoid(RectTransform parent, string childName, float top, float bottom)
        {
            var existing = parent.Find(childName);
            var shape = existing ? existing.GetComponent<TrapezoidGraphic>() : null;
            if (!shape)
            {
                var go = new GameObject(childName, typeof(RectTransform), typeof(CanvasRenderer),
                                        typeof(TrapezoidGraphic));
                go.transform.SetParent(parent, false);
                shape = go.GetComponent<TrapezoidGraphic>();
            }

            shape.SetEdges(top, bottom);
            shape.raycastTarget = false;   // decorative by default; AdoptButtonTarget opts one in
            return shape;
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
            foreach (var slot in _coreSlots.Values)
            {
                slot.Tween?.Kill();
                slot.FlashTween?.Kill();
            }
            _slots.Clear();
            _coreSlots.Clear();
            _blobs.Clear();
        }
    }
}
