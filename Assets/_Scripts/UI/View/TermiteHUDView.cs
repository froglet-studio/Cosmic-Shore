using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Termite queen's HUD: her deck (design: <c>R_VesselActions/TERMITE.md</c> §6). The row
    /// order IS the element map — <b>Autothysis / Team Crystal (charge) · Queen / Mound Drones
    /// (mass) · Teleport / New Mound (space) · Pheromone (time)</b>.
    ///
    /// <para><b>The HAND is the deck's readable face.</b> The card art carries its own name and
    /// icon at 128 x 136 px, and the fleet's ability row draws an icon in a ~60-unit cell, so a
    /// card shown only there is a smudge — the first playtest called the Termite unplayable for
    /// exactly that reason. The hand draws each face-up card at 1.4x its native size along the
    /// bottom centre, with its pheromone COST, the card that comes up NEXT, the control that plays
    /// it, and a veil that drains as the tank fills toward the cost. It is built in code at
    /// <see cref="Initialize"/> (no prefab wiring to forget) and needs nothing but the card art
    /// already on this view. The ability row stays — it is the fleet's structural contract — and
    /// its slot icons keep showing the same card.</para>
    ///
    /// <para>Nothing in the hand pops: a played card TURNS OVER (squeezes to an edge, swaps face,
    /// opens again) and lands with a small punch, which is continuity of existence applied to a
    /// card.</para>
    /// </summary>
    public class TermiteHUDView : VesselHUDView
    {
        [Serializable]
        public struct CardArt
        {
            public TermiteCard card;
            [Tooltip("The card's FRONT art (_Graphics/VesselButtons/TermiteCard_Front_*).")]
            public Sprite front;
        }

        [Header("Deck")]
        [Tooltip("Front art per card. The slot icon and the hand card show the face-up card's front.")]
        [SerializeField] List<CardArt> cardArt = new();

        [Header("Pheromone (Time row)")]
        [Tooltip("Filled Image on the Time plate: the pheromone tank. Bind it as the Time icon's " +
                 "gauge too, so the lockup re-homes and masks it — and keep writing fillAmount here.")]
        [SerializeField] Image pheromoneGauge;

        [Tooltip("Gauge colour while filling.")]
        [SerializeField] Color fillingColor = new(0.85f, 0.7f, 0.35f, 1f);

        [Tooltip("Gauge colour when the tank is full (pheromone is being wasted — play a card).")]
        [SerializeField] Color fullColor = new(1f, 0.95f, 0.6f, 1f);

        [Header("Hand")]
        [Tooltip("Draw the large readable hand along the bottom centre.")]
        [SerializeField] bool showHand = true;
        [Tooltip("Card size in canvas units (the art is 128 x 136; 1.4x keeps its text legible).")]
        [SerializeField] Vector2 handCardSize = new(180f, 191f);
        [SerializeField] float handCardSpacing = 18f;
        [Tooltip("Distance of the hand's bottom edge from the bottom of the screen.")]
        [SerializeField] float handBottomMargin = 64f;
        [Tooltip("Height of the pheromone bar under the hand.")]
        [SerializeField] float handBarHeight = 26f;
        [SerializeField] Color veilColor = new(0f, 0f, 0f, 0.62f);
        [SerializeField] Color costColor = new(1f, 0.82f, 0.35f, 1f);

        static readonly Element[] s_handSlots = { Element.Charge, Element.Mass, Element.Space };

        sealed class HandCard
        {
            public RectTransform Root;
            public RectTransform Face;
            public Image Art;
            public Image Veil;
            public TMP_Text Cost;
            public TMP_Text Next;
            public TMP_Text Blocked;
            public Image ChipGlyph;
            public TMP_Text ChipLabel;
            public InputEvents Input;
            public bool HasInput;
            public TermiteCard Shown;
            public bool HasShown;
            public TermiteCard Pending;
            public float FlipT = -1f;    // < 0 idle; 0..1 turning over
            public float PunchT = -1f;
        }

        readonly Dictionary<Element, TermiteCard> _shown = new();
        readonly Dictionary<Element, HandCard> _hand = new();
        RectTransform _handRoot;
        Image _barFill;
        TMP_Text _barLabel;
        readonly List<Image> _barTicks = new();
        bool _keyboard;
        ControlGlyphSetSO _glyphs;

        const float FlipSeconds = 0.22f;
        const float PunchSeconds = 0.28f;

        public override void Initialize()
        {
            _shown.Clear();
            if (showHand) EnsureHand();
            SetPheromone(0.5f, 10f);
            SetAbilityCooldown(Element.Charge, 0f);
            SetAbilityCooldown(Element.Mass, 0f);
            SetAbilityCooldown(Element.Space, 0f);
        }

        /// <summary>The tank: <paramref name="fill01"/> of <paramref name="capacity"/> pheromone.</summary>
        public void SetPheromone(float fill01, float capacity)
        {
            float v = Mathf.Clamp01(fill01);
            var c = v >= 0.999f ? fullColor : fillingColor;
            if (pheromoneGauge)
            {
                pheromoneGauge.fillAmount = v;
                pheromoneGauge.color = c;
            }

            if (_barFill)
            {
                _barFill.fillAmount = v;
                _barFill.color = c;
                EnsureTicks(Mathf.Clamp(Mathf.RoundToInt(capacity), 1, 20));
            }
            if (_barLabel) _barLabel.text = Mathf.FloorToInt(v * capacity + 1e-4f).ToString();
        }

        /// <summary>Turn the slot to <paramref name="card"/>. Returns true when the card actually
        /// changed (i.e. the slot was played), which the controller flashes.</summary>
        public bool SetFaceUp(Element element, TermiteCard card)
        {
            if (_shown.TryGetValue(element, out var current) && current == card) return false;
            bool hadCard = _shown.ContainsKey(element);
            _shown[element] = card;

            if (TryGetAbilityIcon(element, out var icon) && icon && TryGetArt(card, out var art))
            {
                icon.sprite = art;
                icon.preserveAspect = true;
            }

            if (_hand.TryGetValue(element, out var h))
            {
                if (!h.HasShown) { ShowFace(h, card); }
                else { h.Pending = card; h.FlipT = 0f; }
            }
            return hadCard;
        }

        /// <summary>
        /// The hand card's play state: <paramref name="remaining01"/> is the share of the card
        /// still veiled (the pheromone wait), <paramref name="blocked"/> means something other than
        /// pheromone refuses it (a drone or mound cap), and <paramref name="next"/> is the card
        /// that comes up after it.
        /// </summary>
        public void SetHandCard(Element element, float cost, float remaining01, bool blocked, TermiteCard next)
        {
            if (!_hand.TryGetValue(element, out var h)) return;
            h.Cost.text = Mathf.RoundToInt(cost).ToString();
            h.Veil.fillAmount = blocked ? 1f : Mathf.Clamp01(remaining01);
            h.Blocked.enabled = blocked;
            h.Next.text = "next: " + DisplayName(next);
            h.Art.color = remaining01 <= 0f && !blocked ? Color.white : new Color(0.8f, 0.8f, 0.85f, 1f);
        }

        public override void SetAbilityControl(Element element, InputEvents input)
        {
            base.SetAbilityControl(element, input);
            if (!_hand.TryGetValue(element, out var h)) return;
            h.Input = input;
            h.HasInput = true;
            RefreshChip(h);
        }

        public override void SetControlDevice(bool keyboard)
        {
            base.SetControlDevice(keyboard);
            _keyboard = keyboard;
            foreach (var h in _hand.Values) RefreshChip(h);
        }

        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            if (element == Element.Time && pheromoneGauge)
                pheromoneGauge.transform.localScale = AbilityIconRestScale(element);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var h in _hand.Values)
            {
                if (h.FlipT >= 0f)
                {
                    float before = h.FlipT;
                    h.FlipT += dt / FlipSeconds;
                    if (before < 0.5f && h.FlipT >= 0.5f) ShowFace(h, h.Pending);
                    if (h.FlipT >= 1f) { h.FlipT = -1f; h.PunchT = 0f; }
                    float x = h.FlipT < 0f ? 1f : Mathf.Abs(Mathf.Cos(h.FlipT * Mathf.PI));
                    h.Face.localScale = new Vector3(Mathf.Max(0.02f, x), 1f, 1f);
                }
                else if (h.PunchT >= 0f)
                {
                    h.PunchT += dt / PunchSeconds;
                    float s = h.PunchT >= 1f ? 1f : 1f + 0.12f * Mathf.Sin(h.PunchT * Mathf.PI);
                    h.Face.localScale = new Vector3(s, s, 1f);
                    if (h.PunchT >= 1f) h.PunchT = -1f;
                }
            }
        }

        // ------------------------------------------------------------------ hand construction

        void EnsureHand()
        {
            if (_handRoot) return;
            if (transform is not RectTransform parent) return;

            _handRoot = NewRect("TermiteHand", parent);
            float width = s_handSlots.Length * handCardSize.x + (s_handSlots.Length - 1) * handCardSpacing;
            _handRoot.anchorMin = _handRoot.anchorMax = new Vector2(0.5f, 0f);
            _handRoot.pivot = new Vector2(0.5f, 0f);
            _handRoot.sizeDelta = new Vector2(width, handCardSize.y + handBarHeight + 60f);
            _handRoot.anchoredPosition = new Vector2(0f, handBottomMargin);

            // Pheromone bar along the bottom of the hand: the one number every card is priced in.
            var bar = NewRect("PheromoneBar", _handRoot);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, handBarHeight);
            bar.anchoredPosition = Vector2.zero;
            var bed = bar.gameObject.AddComponent<Image>();
            bed.color = new Color(0f, 0f, 0f, 0.55f);
            bed.raycastTarget = false;

            var fill = NewRect("Fill", bar);
            Stretch(fill, 3f);
            _barFill = fill.gameObject.AddComponent<Image>();
            _barFill.sprite = WhiteSprite();
            _barFill.type = Image.Type.Filled;
            _barFill.fillMethod = Image.FillMethod.Horizontal;
            _barFill.fillOrigin = 0;
            _barFill.raycastTarget = false;

            var label = NewRect("Count", bar);
            label.anchorMin = new Vector2(0f, 0f);
            label.anchorMax = new Vector2(0f, 1f);
            label.pivot = new Vector2(1f, 0.5f);
            label.sizeDelta = new Vector2(44f, 0f);
            label.anchoredPosition = new Vector2(-8f, 0f);
            _barLabel = NewText(label, 26f, TextAlignmentOptions.Right, costColor);

            for (int i = 0; i < s_handSlots.Length; i++)
            {
                var h = BuildCard(s_handSlots[i]);
                h.Root.anchoredPosition = new Vector2(i * (handCardSize.x + handCardSpacing),
                                                      handBarHeight + 34f);
                _hand[s_handSlots[i]] = h;
            }
        }

        HandCard BuildCard(Element element)
        {
            var h = new HandCard();
            h.Root = NewRect(element + "Card", _handRoot);
            h.Root.anchorMin = h.Root.anchorMax = new Vector2(0f, 0f);
            h.Root.pivot = new Vector2(0f, 0f);
            h.Root.sizeDelta = handCardSize;

            h.Face = NewRect("Face", h.Root);
            Stretch(h.Face, 0f);
            h.Face.pivot = new Vector2(0.5f, 0.5f);

            h.Art = h.Face.gameObject.AddComponent<Image>();
            h.Art.preserveAspect = true;
            h.Art.raycastTarget = false;

            var veil = NewRect("Veil", h.Face);
            Stretch(veil, 0f);
            h.Veil = veil.gameObject.AddComponent<Image>();
            h.Veil.sprite = WhiteSprite();
            h.Veil.color = veilColor;
            h.Veil.type = Image.Type.Filled;
            h.Veil.fillMethod = Image.FillMethod.Vertical;
            h.Veil.fillOrigin = (int)Image.OriginVertical.Top;
            h.Veil.raycastTarget = false;

            // Cost: a gold number in a dark disc over the card's folded top-left corner.
            var costBed = NewRect("Cost", h.Face);
            costBed.anchorMin = costBed.anchorMax = new Vector2(0f, 1f);
            costBed.pivot = new Vector2(0.5f, 0.5f);
            costBed.sizeDelta = new Vector2(46f, 46f);
            costBed.anchoredPosition = new Vector2(6f, -6f);
            var disc = costBed.gameObject.AddComponent<Image>();
            disc.color = new Color(0.05f, 0.04f, 0.08f, 0.92f);
            disc.raycastTarget = false;
            var costText = NewRect("Value", costBed);
            Stretch(costText, 0f);
            h.Cost = NewText(costText, 30f, TextAlignmentOptions.Center, costColor);

            var blocked = NewRect("Blocked", h.Face);
            Stretch(blocked, 0f);
            h.Blocked = NewText(blocked, 28f, TextAlignmentOptions.Center, Color.white);
            h.Blocked.text = "MAX";
            h.Blocked.enabled = false;

            // What comes up next, above the card (not on it, so the art stays readable).
            var next = NewRect("Next", h.Root);
            next.anchorMin = new Vector2(0f, 1f);
            next.anchorMax = new Vector2(1f, 1f);
            next.pivot = new Vector2(0.5f, 0f);
            next.sizeDelta = new Vector2(0f, 26f);
            next.anchoredPosition = new Vector2(0f, 4f);
            h.Next = NewText(next, 18f, TextAlignmentOptions.Center, new Color(0.8f, 0.8f, 0.85f, 1f));

            // The control that plays it, under the card.
            var chip = NewRect("Control", h.Root);
            chip.anchorMin = new Vector2(0.5f, 0f);
            chip.anchorMax = new Vector2(0.5f, 0f);
            chip.pivot = new Vector2(0.5f, 1f);
            chip.sizeDelta = new Vector2(120f, 30f);
            chip.anchoredPosition = new Vector2(0f, -2f);
            var glyph = NewRect("Glyph", chip);
            Stretch(glyph, 0f);
            h.ChipGlyph = glyph.gameObject.AddComponent<Image>();
            h.ChipGlyph.preserveAspect = true;
            h.ChipGlyph.raycastTarget = false;
            h.ChipGlyph.enabled = false;
            var chipLabel = NewRect("Label", chip);
            Stretch(chipLabel, 0f);
            h.ChipLabel = NewText(chipLabel, 20f, TextAlignmentOptions.Center, Color.white);
            return h;
        }

        void EnsureTicks(int segments)
        {
            if (!_barFill || _barTicks.Count == segments - 1) return;
            foreach (var t in _barTicks) if (t) Destroy(t.gameObject);
            _barTicks.Clear();
            var bar = (RectTransform)_barFill.transform.parent;
            for (int i = 1; i < segments; i++)
            {
                var tick = NewRect("Tick" + i, bar);
                float x = i / (float)segments;
                tick.anchorMin = new Vector2(x, 0f);
                tick.anchorMax = new Vector2(x, 1f);
                tick.pivot = new Vector2(0.5f, 0.5f);
                tick.sizeDelta = new Vector2(2f, 0f);
                var img = tick.gameObject.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.6f);
                img.raycastTarget = false;
                _barTicks.Add(img);
            }
        }

        void ShowFace(HandCard h, TermiteCard card)
        {
            h.Shown = card;
            h.HasShown = true;
            if (TryGetArt(card, out var art)) h.Art.sprite = art;
        }

        void RefreshChip(HandCard h)
        {
            if (!h.HasInput) return;
            if (!_glyphs) _glyphs = Resources.Load<ControlGlyphSetSO>("ControlGlyphSet");
            var glyph = _glyphs ? _glyphs.For(InputHintBindingMap.BindingFor(h.Input, _keyboard)) : null;
            bool showGlyph = glyph != null && !_keyboard && glyph.padGlyph;
            bool showLabel = glyph != null && _keyboard && !string.IsNullOrEmpty(glyph.keyboardLabel);
            h.ChipGlyph.sprite = showGlyph ? glyph.padGlyph : null;
            h.ChipGlyph.enabled = showGlyph;
            h.ChipLabel.text = showLabel ? glyph.keyboardLabel : string.Empty;
            h.ChipLabel.enabled = showLabel;
        }

        bool TryGetArt(TermiteCard card, out Sprite sprite)
        {
            for (int i = 0; i < cardArt.Count; i++)
                if (cardArt[i].card == card && cardArt[i].front) { sprite = cardArt[i].front; return true; }
            sprite = null;
            return false;
        }

        /// <summary>ASCII only: the project's UI font carries no glyph outside it.</summary>
        static string DisplayName(TermiteCard card) => card switch
        {
            TermiteCard.Autothysis => "Autothysis",
            TermiteCard.TeamCrystal => "Team Crystal",
            TermiteCard.QueenDrones => "Queen Drones",
            TermiteCard.MoundDrones => "Mound Drones",
            TermiteCard.Teleport => "Teleport",
            TermiteCard.NewMound => "New Mound",
            _ => card.ToString(),
        };

        // ------------------------------------------------------------------ helpers

        static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        static void Stretch(RectTransform rt, float inset)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }

        static TMP_Text NewText(RectTransform rt, float size, TextAlignmentOptions align, Color color)
        {
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.fontStyle = FontStyles.Bold;
            return t;
        }

        static Sprite s_white;

        /// <summary>A Filled Image needs a sprite to fill; one shared 4x4 white sprite serves every
        /// hand in the session.</summary>
        static Sprite WhiteSprite()
        {
            if (s_white) return s_white;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "TermiteHandWhite" };
            var px = new Color32[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply(false, true);
            s_white = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
            s_white.name = "TermiteHandWhite";
            return s_white;
        }
    }
}
