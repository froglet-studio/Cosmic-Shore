using CosmicShore.Data;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Orca's four ability cards are its four SEATS (round 6, Garrett 2026-10-21; the config's seatElements; lab EL):
    /// <list type="bullet">
    /// <item><b>Time</b> — the helm: the boost tank emptying on the card's veil, a flash when the shield goes up.</item>
    /// <item><b>Space</b> — the sniper: its long cooldown on the veil.</item>
    /// <item><b>Mass</b> — the fast gun: its heat on the veil (full while it is locked out).</item>
    /// <item><b>Charge</b> — the bomb: no veil (it has no cooldown, one bomb at a time).</item>
    /// </list>
    /// The person's own seat's card is LIT (the fleet's press signal, re-asserted every frame like the Rhino's), and each
    /// gunner card carries its own seat's missiles as a number; a card flashes when its seat's missile or bomb bursts.
    /// That is everything a seat needs (Garrett, 2026-10-21: "it has all the info it needs now"), so the Orca's own line
    /// of text across the top of the HUD is retired. The view is the Sparrow's HUD (the hull is a Sparrow clone, as the
    /// Wrasse is), whose Initialize hides the Charge and Space icons until its guns report; the Orca has no Sparrow guns,
    /// so this controller shows them. The counts are GENERATED at runtime (the Wrasse's and the Shrike's counts are the
    /// pattern). Polled from this hull's own crew, a serialized reference (vessel rule 14), behind the fleet's pilot gate
    /// (rule 5).
    ///
    /// <para><b>A gunner on a phone</b> (Garrett, 2026-10-21: "each of the orca turrets needs two buttons: fire gun, fire
    /// missiles. it does these with the right thumb and aims with the left"): while the person holds a gunner seat and no
    /// pad is connected, two buttons sit round the right thumb, Fire (held; the seat's element colour) and Missile
    /// (launch, or burst the one in flight), laid out by the fleet's <see cref="TouchButtonLayout"/> in millimetres. They
    /// press the crew directly (<see cref="OrcaCrew.SetGunnerButton"/>), because the AI flies the hull while a person
    /// aims and an InputStatus press would be dropped; a touch that starts on them never aims (FlightTouchClaims). Fire
    /// stands down while the seat flies a missile, as in the lab.</para>
    /// </summary>
    public class OrcaHUDController : VesselHUDController
    {
        [Header("Orca")]
        [Tooltip("This hull's crew: the magazine, the boost tank, the shield and the bursts.")]
        [SerializeField] OrcaCrew crew;

        [Header("Counts (generated into each gunner seat's card)")]
        [Tooltip("The count's text. {count} is the number; empty shows nothing.")]
        [SerializeField] string countTemplate = "{count}";
        [SerializeField, Min(8f)] float countFontSize = 30f;
        [Tooltip("Where the count sits, as a fraction of its icon's height from the centre (negative = below).")]
        [SerializeField] float countOffset = -0.62f;
        [Tooltip("A count above zero — the palette's CTA lime.")]
        [SerializeField] Color countLiveColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("A count of zero.")]
        [SerializeField] Color countEmptyColor = new(0.59f, 0.65f, 0.76f, 1f);

        const string CountName = "OrcaCount";
        const int Seats = OrcaCrew.Seats;
        static readonly float[] ButtonAspects = { 1f, 1f };   // Fire first: it takes the easiest reach

        readonly TMP_Text[] _counts = new TMP_Text[Seats];
        readonly int[] _shownCounts = new int[Seats];
        readonly bool[] _lit = new bool[Seats];
        readonly float[] _burstAt = new float[Seats];
        bool _isLocalPilotHud;
        float _shieldAt;

        // a gunner's phone buttons (OrcaCrew.FireButton, OrcaCrew.MissileButton)
        readonly TouchButtonView[] _buttons = new TouchButtonView[2];
        AbilityLockupView _lockup;
        int _buttonSeat = -1, _screenWidth, _screenHeight;
        bool _riderButtonsBuilt;
        Rect _safeArea;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!crew && vesselStatus is Component hull) hull.TryGetComponent(out crew);

            // The fleet's pilot gate (WrasseHUDController / BrittleStarHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;

            var view = View;
            if (!view) return;
            for (int s = 0; s < Seats; s++)
            {
                var e = SeatElement(s);
                if (view.TryGetAbilityIcon(e, out var icon))
                {
                    icon.enabled = true;   // the Sparrow's view hides Charge and Space until its guns report
                    _counts[s] = s == OrcaCrew.Helm ? null : EnsureCount(icon);
                }
                _shownCounts[s] = -1;
                _lit[s] = false;
                view.SetAbilityPressed(e, false);
                _burstAt[s] = crew ? crew.BurstAt(s) : 0f;
            }
            _shieldAt = crew ? crew.ShieldRaisedAt : 0f;
            BuildButtons(view);
        }

        // the buttons live under the view's row and die with it; a disabled HUD hides them, which lets go of a held Fire
        void OnDisable()
        {
            for (int i = 0; i < _buttons.Length; i++) if (_buttons[i]) SetShown(_buttons[i], false);
        }

        Element SeatElement(int seat) => crew ? crew.SeatElement(seat) : seat switch
        {
            1 => Element.Space, 2 => Element.Mass, 3 => Element.Charge, _ => Element.Time,
        };

        // the local person's HUD: their own hull's, or the hull whose gunner seat they man from another machine
        bool LocalHud => _isLocalPilotHud || (crew && crew.HumanRides);

        void Update()
        {
            if (!LocalHud || !crew) return;
            // a rider's HUD is a remote hull's, initialised without the phone buttons: build them the first time it is theirs
            if (!_isLocalPilotHud && !_riderButtonsBuilt && View) { _riderButtonsBuilt = true; BuildButtons(View); }
            int held = crew.HumanAboard ? crew.HumanSeat : -1;
            PaintButtons(held);

            var view = View;
            if (!view) return;
            for (int s = 0; s < Seats; s++)
            {
                var e = SeatElement(s);
                if (s > OrcaCrew.Helm) PaintCount(s, crew.MissilesAt(s));
                view.SetAbilityCooldown(e, crew.SeatVeil01(s));
                // the person's own seat is lit, re-asserted every frame: the base press path darkens a card whenever an
                // input bound to it is let go
                bool on = s == held;
                if (on || on != _lit[s]) { _lit[s] = on; view.SetAbilityPressed(e, on); }
                float burst = crew.BurstAt(s);
                if (burst != _burstAt[s]) { _burstAt[s] = burst; view.PlayAbilityFlash(e); }
            }
            if (crew.ShieldRaisedAt != _shieldAt) { _shieldAt = crew.ShieldRaisedAt; view.PlayAbilityFlash(SeatElement(OrcaCrew.Helm)); }
        }

        TMP_Text EnsureCount(Image icon)
        {
            var parent = icon.rectTransform;
            var text = FindOrMake(parent, CountName);
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(parent.sizeDelta.x, countFontSize * 1.3f);
            rt.anchoredPosition = new Vector2(0f, parent.sizeDelta.y * countOffset);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = countFontSize;
            return text;
        }

        static TMP_Text FindOrMake(RectTransform parent, string name)
        {
            var existing = parent.Find(name);
            if (!existing || !existing.TryGetComponent(out TextMeshProUGUI text))
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                text = go.GetComponent<TextMeshProUGUI>();
            }
            text.rectTransform.localScale = Vector3.one;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>Repaints a seat's missile count only when it changes.</summary>
        void PaintCount(int seat, int count)
        {
            var text = _counts[seat];
            if (!text || count == _shownCounts[seat]) return;
            _shownCounts[seat] = count;
            text.text = TemplateText.Fill(countTemplate, "count", count.ToString());
            text.color = count > 0 ? countLiveColor : countEmptyColor;
        }

        // ------------------------------------------------------------------ a gunner's phone buttons

        void BuildButtons(VesselHUDView view)
        {
            ClearButtons();
            _buttonSeat = -1;
            if (!LocalHud || !crew || !KitInputDevice.IsTouchPrimary) return;
            _lockup = view.GetComponent<AbilityLockupView>();
            var row = _lockup ? _lockup.Row : null;
            var cfg = crew.Config;
            if (!row || !cfg) return;

            var library = TouchControlProfileLibrarySO.Instance;
            var disc = library ? library.ButtonDisc : null;
            var rim = library ? library.ButtonRim : null;
            _buttons[OrcaCrew.FireButton] = MakeButton(row, cfg.FireButtonIcon, cfg.FireButtonLabel, disc, rim, OrcaCrew.FireButton);
            _buttons[OrcaCrew.MissileButton] = MakeButton(row, cfg.MissileButtonIcon, cfg.MissileButtonLabel, disc, rim, OrcaCrew.MissileButton);
            _screenWidth = -1;   // lay out on the first frame they show
        }

        TouchButtonView MakeButton(RectTransform row, Sprite icon, string label, Sprite disc, Sprite rim, int button)
        {
            var definition = new TouchExtraButton { icon = icon, label = label, primary = true };
            var view = TouchButtonView.Create(row, definition, _lockup.NeutralColour, disc, rim, _lockup.CardWidth);
            var target = crew;
            view.Bind(down => { if (target) target.SetGunnerButton(button, down); });
            view.gameObject.SetActive(false);
            return view;
        }

        void ClearButtons()
        {
            for (int i = 0; i < _buttons.Length; i++)
            {
                if (_buttons[i]) Destroy(_buttons[i].gameObject);
                _buttons[i] = null;
            }
        }

        /// <summary>Shows the two buttons while the person holds a gunner seat on a phone with no pad, tinted for the seat
        /// and re-laid when the screen or the safe area changes.</summary>
        void PaintButtons(int held)
        {
            var fire = _buttons[OrcaCrew.FireButton];
            var missile = _buttons[OrcaCrew.MissileButton];
            if (!fire || !missile) return;
            bool show = held > OrcaCrew.Helm && !KitInputDevice.ShowsControlHints;
            SetShown(missile, show);
            SetShown(fire, show && !crew.MissileInFlight(held));
            if (!show) return;

            if (held != _buttonSeat)
            {
                _buttonSeat = held;
                fire.SetTint(_lockup.ColourFor(SeatElement(held)));
            }
            if (Screen.width != _screenWidth || Screen.height != _screenHeight || Screen.safeArea != _safeArea)
                LayOutButtons();
        }

        static void SetShown(TouchButtonView button, bool shown)
        {
            if (button.gameObject.activeSelf != shown) button.gameObject.SetActive(shown);
        }

        void LayOutButtons()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            _safeArea = Screen.safeArea;
            var row = _lockup ? _lockup.Row : null;
            if (!row) return;
            var canvas = row.GetComponentInParent<Canvas>();
            var root = canvas ? canvas.rootCanvas : null;
            Camera cam = root && root.renderMode != RenderMode.ScreenSpaceOverlay ? root.worldCamera : null;

            float dpi = Screen.dpi > 1f ? Screen.dpi : 160f;
            var layout = TouchButtonLayout.Arrange(ButtonAspects, new Vector2(Screen.width, Screen.height), _safeArea, dpi,
                TouchControlsHud.LayoutSettings(TouchControlProfileLibrarySO.Instance));
            float scale = layout.ButtonWidthPixels * TouchControlsHud.UnitsPerPixel(row, cam) / Mathf.Max(1f, _lockup.CardWidth);
            for (int i = 0; i < _buttons.Length && i < layout.Centres.Count; i++)
            {
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(row, layout.Centres[i], cam, out var local)) continue;
                ((RectTransform)_buttons[i].transform).anchoredPosition = local;
                _buttons[i].SetBaseScale(scale);
            }
        }
    }
}
