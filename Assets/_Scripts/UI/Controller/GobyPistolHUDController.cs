using CosmicShore.Data;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using HintBinding = CosmicShore.UI.InputDeviceIconSetSwitcher.HintBinding;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Goby &amp; Pistol's four cards, each seat's own (Garrett, 2026-10-24: "give both of its seats separate UI
    /// ability icons, glyphs, etc."). When the person swaps seats the cards change with them: the icons
    /// (<see cref="GobyPistolHUDView.SetSeat"/>), the control chips, and what each card reads.
    /// <list type="table">
    /// <listheader><term>card</term><description>flying the Goby | flying the Pistol</description></listheader>
    /// <item><term>Charge</term><description>Lock (Y / F; lit while it holds a lock) | Snap (X / Q; lit while held,
    /// the veil is its cooldown)</description></item>
    /// <item><term>Mass</term><description>perches made, as a count (passive; it flashes on each perch) | prisms in the
    /// dens, as a count, and the Pistol's energy as the gauge (what its next den is built from); Back (Y / F) lit while
    /// heading back</description></item>
    /// <item><term>Space</term><description>the undertow's reach, as the gauge (passive) | Ghost (B / T; lit while
    /// ghosting, the veil is its cooldown), the same reach gauge</description></item>
    /// <item><term>Time</term><description>Dart (X / Q; the gauge is the held charge, the veil its cooldown) | the dive
    /// (A / Space; lit while held, the gauge is the Pistol's speed)</description></item>
    /// </list>
    /// The Goby's chips derive from the hull's own bindings (Dart Button3Action, Lock Button4Action); the Pistol's name
    /// the face buttons the seat reads (<see cref="GobyPistolDuo"/>'s HumanPistol), which are the same four the fleet
    /// labels, so every chip comes from the one glyph set. The counts are generated into the Mass card at runtime (the
    /// Wrasse's pattern), so the HUD prefab carries no extra object. Polled from this hull's own seat, a serialized
    /// reference, behind the fleet's pilot gate (or on the machine of a person riding this hull's Pistol).
    /// </summary>
    public sealed class GobyPistolHUDController : VesselHUDController
    {
        [Header("Goby & Pistol")]
        [Tooltip("This hull's crew seat: what each seat's cards read.")]
        [SerializeField] GobyPistolDuo duo;

        [Header("Mass card count (generated)")]
        [Tooltip("Flying the Goby: the perches it has made. {count} is the number; empty shows nothing.")]
        [SerializeField] string perchCountTemplate = "{count}";
        [Tooltip("Flying the Pistol: the prisms in its dens. {count} is the number; empty shows nothing.")]
        [SerializeField] string denCountTemplate = "{count}";
        [SerializeField, Min(8f)] float countFontSize = 30f;
        [Tooltip("Where the count sits, as a fraction of its icon's height from the centre (negative = below).")]
        [SerializeField] float countOffset = -0.62f;
        [Tooltip("A count above zero: the palette's CTA lime.")]
        [SerializeField] Color countLiveColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("A count of zero.")]
        [SerializeField] Color countEmptyColor = new(0.59f, 0.65f, 0.76f, 1f);

        const string CountName = "GobyPistolCount";
        static readonly Element[] Cards = VesselHUDView.AbilityDisplayOrder;

        GobyPistolHUDView _view;
        TMP_Text _count;
        bool _isLocalPilotHud;
        int _seat = -1, _shownCount = -1, _perches;
        readonly bool[] _lit = new bool[4];

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            if (!duo && vesselStatus is Component hull) hull.TryGetComponent(out duo);

            // The fleet's pilot gate (BrittleStarHUDController / WrasseHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null && !vesselStatus.IsInitializedAsAI && vesselStatus.IsLocalUser;

            _view = View as GobyPistolHUDView;
            _seat = -1;
            _shownCount = -1;
            _perches = duo ? duo.PerchCount : 0;
            for (int i = 0; i < _lit.Length; i++) _lit[i] = false;
            if (View && View.TryGetAbilityIcon(Element.Mass, out var massIcon)) _count = EnsureCount(massIcon);
        }

        void Update()
        {
            if (!duo || (!_isLocalPilotHud && !duo.RidesHere)) return;
            var view = View;
            if (!view) return;

            int seat = duo.HumanInSeat ? GobyPistolHUDView.PistolSeat : GobyPistolHUDView.GobySeat;
            if (seat != _seat)
            {
                _seat = seat;
                if (_view) _view.SetSeat(seat);
                SeedControls(view, seat);
                _shownCount = -1;
                for (int i = 0; i < _lit.Length; i++) Light(view, i, false);
            }

            bool pistol = seat == GobyPistolHUDView.PistolSeat;
            if (!pistol)
            {
                Light(view, 0, duo.HasLock || duo.LockHeld);
                Light(view, 3, duo.DartCharge01 > 0f);
                view.SetAbilityCooldown(Element.Charge, 0f);
                view.SetAbilityCooldown(Element.Space, 0f);
                view.SetAbilityCooldown(Element.Time, duo.DartCooldown01);
                if (_view) { _view.SetTimeGauge(duo.DartCharge01); _view.SetMassGauge(0f); }
                PaintCount(duo.PerchCount, perchCountTemplate);
            }
            else
            {
                Light(view, 0, duo.SnapHeld);
                Light(view, 1, duo.HeadingBack);
                Light(view, 2, duo.Ghosting);
                Light(view, 3, duo.Diving);
                view.SetAbilityCooldown(Element.Charge, duo.SnapCooldown01);
                view.SetAbilityCooldown(Element.Space, duo.GhostCooldown01);
                view.SetAbilityCooldown(Element.Time, 0f);
                if (_view) { _view.SetTimeGauge(duo.Speed01); _view.SetMassGauge(duo.Energy01); }
                PaintCount(duo.DenPrisms, denCountTemplate);
            }
            if (_view) _view.SetSpaceGauge(duo.PullReach01);

            // a perch is made in either seat (the Goby's AI perches too): the Mass card flashes
            if (duo.PerchCount != _perches)
            {
                if (duo.PerchCount > _perches) view.PlayAbilityFlash(Element.Mass);
                _perches = duo.PerchCount;
            }
        }

        /// <summary>Each seat's chips. A passive card names no control (the passive sentinel draws nothing).</summary>
        static void SeedControls(VesselHUDView view, int seat)
        {
            if (seat == GobyPistolHUDView.PistolSeat)
            {
                view.SetAbilityControlGlyph(Element.Charge, HintBinding.PadButtonWest, HintBinding.KeyQ);    // Snap
                view.SetAbilityControlGlyph(Element.Mass, HintBinding.PadButtonNorth, HintBinding.KeyF);     // Back
                view.SetAbilityControlGlyph(Element.Space, HintBinding.PadButtonEast, HintBinding.KeyR);     // Ghost (its key is T)
                view.SetAbilityControlGlyph(Element.Time, HintBinding.PadButtonSouth, HintBinding.KeySpace); // the dive
                return;
            }
            view.SetAbilityControl(Element.Charge, InputEvents.Button4Action);              // Lock
            view.SetAbilityControl(Element.Mass, InputEvents.FullSpeedStraightAction);      // perches: passive
            view.SetAbilityControl(Element.Space, InputEvents.FullSpeedStraightAction);     // the undertow: passive
            view.SetAbilityControl(Element.Time, InputEvents.Button3Action);                // Dart
        }

        void Light(VesselHUDView view, int card, bool on)
        {
            if (on == _lit[card]) return;
            _lit[card] = on;
            view.SetAbilityPressed(Cards[card], on);
        }

        TMP_Text EnsureCount(Image icon)
        {
            var parent = icon.rectTransform;
            var existing = parent.Find(CountName);
            if (!existing || !existing.TryGetComponent(out TextMeshProUGUI text))
            {
                var go = new GameObject(CountName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                text = go.GetComponent<TextMeshProUGUI>();
            }
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(parent.sizeDelta.x, countFontSize * 1.3f);
            rt.anchoredPosition = new Vector2(0f, parent.sizeDelta.y * countOffset);
            rt.localScale = Vector3.one;
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = countFontSize;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Repaints the count only when it changes.</summary>
        void PaintCount(int count, string template)
        {
            if (!_count || count == _shownCount) return;
            _shownCount = count;
            _count.text = TemplateText.Fill(template, "count", count.ToString());
            _count.color = count > 0 ? countLiveColor : countEmptyColor;
        }
    }
}
