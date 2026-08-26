using System.Collections.Generic;
using System.Reflection;
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

            RetireLegacyHudContent(row);

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

            float narrow = style.NarrowEdgeFraction;
            float elementY = style.FlowerLocalY;
            float abilityY = style.AbilityPlateLocalY;

            // Sibling order IS draw order, and a bloom has to sit BEHIND the plate it haloes - so
            // each bloom is a sibling placed before its plate rather than a child of it. Building
            // them in this order is the whole layering contract.
            slot.ElementBloom = ResolveChildImage(card, "ElementBloom", style.bloomSprite);
            SetCellRect(slot.ElementBloom.rectTransform, elementY,
                        style.plateWidth + style.bloomPadding * 2f,
                        style.petalCellHeight + style.bloomPadding * 2f);
            slot.ElementBloom.color = WithAlpha(style.bloomColor, 0f);   // nothing glows at rest

            // The element plate narrows UPWARD and the ability plate narrows DOWNWARD, so the pair
            // meets wide-edge to wide-edge across the gap and reads as one waisted object.
            slot.ElementPlate = ResolveTrapezoid(card, "ElementPlate", narrow, 1f);
            SetCellRect(slot.ElementPlate.rectTransform, elementY, style.plateWidth, style.petalCellHeight);
            slot.ElementPlate.color = slot.Locked ? style.lockedPlateColor : style.plateColor;
            ApplySlantEdge(slot.ElementPlate, slot.Locked, upgraded: false);

            slot.AbilityBloom = ResolveChildImage(card, "AbilityBloom", style.bloomSprite);
            SetCellRect(slot.AbilityBloom.rectTransform, abilityY,
                        style.plateWidth + style.bloomPadding * 2f,
                        style.abilityCellHeight + style.bloomPadding * 2f);
            slot.AbilityBloom.color = WithAlpha(style.bloomColor, 0f);

            slot.AbilityPlate = ResolveTrapezoid(card, "AbilityPlate", 1f, narrow);
            SetCellRect(slot.AbilityPlate.rectTransform, abilityY, style.plateWidth, style.abilityCellHeight);
            slot.AbilityPlate.color = slot.Locked ? style.lockedPlateColor : style.plateColor;
            ApplySlantEdge(slot.AbilityPlate, slot.Locked, upgraded: false);
            slot.AbilityPlate.raycastTarget = false;
            AdoptButtonTarget(host, slot.AbilityPlate);

            // The gauge lines the ABILITY plate, so the fill reads as the icon filling up. It stays
            // INVISIBLE until a meter is actually adopted onto this card (AdoptGauge turns it on):
            // an empty track under an ability that has no meter is a false affordance - it reads as
            // a gauge stuck at zero.
            float gaugeFrac = Mathf.Clamp01(style.gaugeCellFraction);
            float gaugeH = style.abilityCellHeight * gaugeFrac;
            float gaugeY = abilityY - (style.abilityCellHeight - gaugeH) * 0.5f;   // sits on the base

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
            float gaugeW = style.plateWidth - d * 2f;
            var gauge = new PlateRect(
                width: gaugeW,
                height: Mathf.Max(1f, gaugeH - d * 2f),
                centreY: gaugeY,
                topEdge: gaugeW > 0.01f ? (style.plateWidth * gaugeTopEdge - d * 2f) / gaugeW : 1f,
                bottomEdge: gaugeW > 0.01f ? (style.plateWidth * narrow - d * 2f) / gaugeW : 1f);

            slot.GaugeTrack = ResolveTrapezoid(card, "GaugeTrack", gauge.TopEdge, gauge.BottomEdge);
            SetCellRect(slot.GaugeTrack.rectTransform, gauge.CentreY, gauge.Width, gauge.Height);
            slot.GaugeTrack.color = Color.clear;

            slot.GaugeClip = ResolveGaugeClip(card, gauge);

            if (slot.Locked) BuildLockedMark(card, abilityY);

            // Press flash sits ABOVE the plate and below the icon, so a press lights the ability
            // trapezoid rather than blooming a circle behind it.
            slot.Flash = ResolveTrapezoid(card, "PressFlash", 1f, narrow);
            SetCellRect(slot.Flash.rectTransform, abilityY, style.plateWidth, style.abilityCellHeight);
            slot.Flash.color = WithAlpha(style.pressFlashColor, 0f);

            slot.FlowerSocket = ResolveFlowerSocket(card, element);
            slot.ChipSocket = ResolveChipSocket(card);
            return slot;
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
            rt.sizeDelta = new Vector2(style.iconBoxSize * 0.4f, style.lockedMarkThickness);
            rt.anchoredPosition = new Vector2(0f, abilityY);
            mark.color = style.lockedMarkColor;
        }

        /// <summary>
        /// Re-homes the vessel's meter into the card and restyles it as the fleet's one gauge: a
        /// linear fill rising through the ability trapezoid, behind the icon, clipped to that
        /// trapezoid's slant. The vessel keeps writing <c>fillAmount</c> on the very same Image, so
        /// no gameplay wiring changes - only where and how it draws. The legacy ring and its frame
        /// are retired with the rest of the host's chrome.
        /// </summary>
        void AdoptGauge(Element element, Slot slot)
        {
            if (slot.Locked || !hudView.TryGetAbilityGauge(element, out var gauge) || !gauge) return;

            var rt = gauge.rectTransform;
            if (rt.parent != slot.GaugeClip) rt.SetParent(slot.GaugeClip, false);

            StretchTo(rt, 0f);                              // the clip owns the size and the shape
            slot.GaugeTrack.color = style.gaugeTrackColor;  // there IS a meter here now

            // Filled needs a sprite - Image draws a plain quad and IGNORES fillAmount when the
            // sprite is null - and it must be a plain box, because the stencil is what shapes this,
            // not the art. Any silhouette in the sprite would punch notches inside the trapezoid.
            gauge.sprite = PlainSprite;
            gauge.type = Image.Type.Filled;
            gauge.fillMethod = Image.FillMethod.Vertical;
            gauge.fillOrigin = (int)Image.OriginVertical.Bottom;
            gauge.preserveAspect = false;
            gauge.raycastTarget = false;
            gauge.color = style.gaugeFillColor;
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
        /// writes to, and worst of all the device-glyph roots on the three HUDs that carry them
        /// with no <c>InputDeviceIconSetSwitcher</c> to drive them. Those glyphs are never lit,
        /// never switched for the player's actual device, and never placed - so when the lockup
        /// moved the row they were left behind wherever the old row used to be.</para>
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
        void RetireLegacyHudContent(RectTransform row)
        {
            var self = (RectTransform)transform;
            var referenced = CollectReferencedObjects();

            for (int i = self.childCount - 1; i >= 0; i--)
            {
                var child = self.GetChild(i);
                if (!child || child == row) continue;
                if (!child.gameObject.activeSelf) continue;
                if (!child.GetComponentInChildren<Graphic>(true)) continue;   // logic, not UI
                if (BranchIsReferenced(child, referenced)) continue;          // something drives it

                child.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Everything the HUD's own drivers point at. Sources are deliberately limited to components
        /// on the HUD ROOT plus the icon-set switcher: a component sitting INSIDE a leftover branch
        /// would otherwise reference its own children and spare the branch it belongs to.
        /// </summary>
        HashSet<Object> CollectReferencedObjects()
        {
            var referenced = new HashSet<Object>();

            foreach (var component in GetComponents<MonoBehaviour>())
                if (component && component != this) CollectSerializedReferences(component, referenced);

            // The switcher is not always on the HUD - the Squirrel keeps it on the vessel root, with
            // its glyph roots alongside it - so it is found rather than assumed.
            var switcher = GetComponentInChildren<InputDeviceIconSetSwitcher>(true)
                        ?? GetComponentInParent<InputDeviceIconSetSwitcher>();
            if (switcher) CollectSerializedReferences(switcher, referenced);

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

            // Borderless: with the rim retired, the bloom and the plate lift ARE the upgrade signal,
            // and BOTH trapezoids take it - the element upgraded the ability, so the pair is what
            // changed state, not one half of it.
            Color bloomTarget = upgraded ? style.bloomColor : WithAlpha(style.bloomColor, 0f);
            Color plateTarget = upgraded ? style.upgradedPlateColor : style.plateColor;

            // The slant edge crosses with them - it is part of the plate, not a separate frame, and
            // it is the one channel with enough contrast left to read at a glance on a dark plate.
            ApplySlantEdge(slot.ElementPlate, locked: false, upgraded);
            ApplySlantEdge(slot.AbilityPlate, locked: false, upgraded);

            if (!animate)
            {
                if (slot.ElementBloom) slot.ElementBloom.color = bloomTarget;
                if (slot.AbilityBloom) slot.AbilityBloom.color = bloomTarget;
                if (slot.ElementPlate) slot.ElementPlate.color = plateTarget;
                if (slot.AbilityPlate) slot.AbilityPlate.color = plateTarget;
                return;
            }

            float d = style.upgradeTransitionDuration;
            var seq = DOTween.Sequence().SetUpdate(true).SetLink(slot.Card.gameObject);
            if (slot.ElementBloom) seq.Join(slot.ElementBloom.DOColor(bloomTarget, d).SetEase(Ease.OutCubic));
            if (slot.AbilityBloom) seq.Join(slot.AbilityBloom.DOColor(bloomTarget, d).SetEase(Ease.OutCubic));
            if (slot.ElementPlate) seq.Join(slot.ElementPlate.DOColor(plateTarget, d).SetEase(Ease.OutCubic));
            if (slot.AbilityPlate) seq.Join(slot.AbilityPlate.DOColor(plateTarget, d).SetEase(Ease.OutCubic));

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
        /// </summary>
        public void SetAbilityCooldown(Element element, float remaining01)
        {
            if (!_slots.TryGetValue(element, out var slot) || !style || slot.Locked) return;

            remaining01 = Mathf.Clamp01(remaining01);
            bool active = remaining01 > 0.0001f;

            // Sitting ready is the common case and this is polled every frame, so it costs nothing:
            // writing fillAmount dirties the Image's mesh, which is fine while a sweep is moving and
            // pure waste while it is parked at zero.
            if (!active && !slot.OnCooldown) return;

            if (!slot.CooldownSweep)
            {
                if (!active) return;                                // never fired: build nothing
                BuildCooldownOverlay(element, slot);
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

        /// <summary>
        /// The cooldown veil is the ONE piece of the lockup that lives outside the card: it has to
        /// darken the ICON as well as the plate, and the icon is a later sibling of the card, so a
        /// child of the card could only ever draw behind it. It is parented to the host and pushed
        /// to the end of the sibling list instead.
        /// </summary>
        void BuildCooldownOverlay(Element element, Slot slot)
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
            clip.sizeDelta = new Vector2(style.plateWidth, style.abilityCellHeight);
            clip.anchoredPosition = Vector2.zero;
            clip.localScale = Vector3.one;
            clip.localRotation = Quaternion.identity;
            clip.SetAsLastSibling();        // over the icon - the point of living out here

            var sweep = ResolveChildImage(clip, "Sweep", PlainSprite);
            // The sweep has to cover the trapezoid's corners at every angle, so it is squared off to
            // the plate's diagonal rather than its width - a disc inscribed in the rect would leave
            // the corners permanently lit.
            float reach = Mathf.Sqrt(style.plateWidth * style.plateWidth +
                                     style.abilityCellHeight * style.abilityCellHeight);
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
            sweep.color = style.cooldownVeilColor;

            slot.CooldownClip = clip;
            slot.CooldownSweep = sweep;
        }

        void PlayReadyFlash(Slot slot)
        {
            if (!slot.Flash) return;

            slot.FlashTween?.Kill();
            slot.Flash.color = style.cooldownReadyFlashColor;
            slot.FlashTween = slot.Flash
                .DOFade(0f, style.cooldownReadyFlashDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .SetLink(slot.Flash.gameObject);
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

        void AdoptButtonTarget(RectTransform host, TrapezoidGraphic plate)
        {
            var button = host.GetComponent<Button>();
            if (!button) return;

            button.targetGraphic = plate;
            plate.raycastTarget = true;   // the plate is the touch target now, so it must be hittable
        }

        /// <summary>
        /// One of the card's generated plates. <paramref name="top"/> and <paramref name="bottom"/>
        /// are edge widths as fractions of the rect - both always derived from the style's single
        /// inset, mirrored, so the two halves of a totem can never disagree about the slant.
        /// </summary>
        /// <summary>
        /// The band on a plate's two SLOPED sides - solid the whole length of each slant, wrapping
        /// around both corners onto the horizontals and grading to nothing there, so it accents the
        /// edges that carry the shape and never closes into the border this style retired. A locked slot wears it at the locked
        /// mark's colour, so an undesigned slot is quiet in every channel at once.
        /// </summary>
        void ApplySlantEdge(TrapezoidGraphic plate, bool locked, bool upgraded)
        {
            plate.EdgeThickness = style.slantEdgeThickness;
            plate.EdgeWrap = style.slantEdgeWrap;
            plate.EdgeAntialias = style.slantEdgeAntialias;
            plate.EdgeColor = locked ? style.lockedMarkColor
                                     : upgraded ? style.upgradedSlantEdgeColor
                                                : style.slantEdgeColor;
        }

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
            _slots.Clear();
        }
    }
}
