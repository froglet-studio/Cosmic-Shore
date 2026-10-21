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
    /// Plus one line naming the seat the person holds and its gun's state (the config's <c>seatLineTemplate</c> and gun
    /// words, so the words are the team's). The view is the Sparrow's HUD (the hull is a Sparrow clone, as the Wrasse is), whose Initialize hides the
    /// Charge and Space icons until its guns report; the Orca has no Sparrow guns, so this controller shows them. The
    /// count and the line are GENERATED at runtime (the Wrasse's and the Shrike's counts are the pattern). Polled from
    /// this hull's own crew, a serialized reference (vessel rule 14), behind the fleet's pilot gate (rule 5).
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

        [Header("Seat line (generated at the top of the HUD)")]
        [SerializeField, Min(8f)] float seatLineFontSize = 26f;
        [Tooltip("Pixels below the top of the HUD.")]
        [SerializeField] float seatLineTop = 24f;
        [SerializeField] Color seatLineColor = new(0.86f, 0.92f, 1f, 0.9f);

        const string CountName = "OrcaCount", SeatLineName = "OrcaSeatLine";
        const int Seats = OrcaCrew.Seats;

        readonly TMP_Text[] _counts = new TMP_Text[Seats];
        readonly int[] _shownCounts = new int[Seats];
        readonly bool[] _lit = new bool[Seats];
        readonly float[] _burstAt = new float[Seats];
        TMP_Text _seatLine;
        int _shownSeat = int.MinValue, _shownLineCount = -1, _shownGunKey = int.MinValue;
        bool _isLocalPilotHud;
        float _shieldAt;

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
            _seatLine = EnsureSeatLine(view.transform as RectTransform);
            _shownSeat = _shownGunKey = int.MinValue;
            _shieldAt = crew ? crew.ShieldRaisedAt : 0f;
        }

        Element SeatElement(int seat) => crew ? crew.SeatElement(seat) : seat switch
        {
            1 => Element.Space, 2 => Element.Mass, 3 => Element.Charge, _ => Element.Time,
        };

        void Update()
        {
            if (!_isLocalPilotHud || !crew) return;
            int held = crew.HumanAboard ? crew.HumanSeat : -1;
            PaintSeatLine(held);

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

        TMP_Text EnsureSeatLine(RectTransform parent)
        {
            if (!parent) return null;
            var text = FindOrMake(parent, SeatLineName);
            var rt = text.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = new Vector2(900f, seatLineFontSize * 1.4f);
            rt.anchoredPosition = new Vector2(0f, -seatLineTop);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = seatLineFontSize;
            text.color = seatLineColor;
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

        /// <summary>The seat line: the config's authored template, or nothing when no person is aboard. Rebuilt only when the
        /// seat, the missiles or the gun's state change (no per-frame strings).</summary>
        void PaintSeatLine(int seat)
        {
            if (!_seatLine) return;
            int count = crew.Missiles, gunKey = seat > 0 ? crew.GunStateKey(seat) : 0;
            if (seat == _shownSeat && count == _shownLineCount && gunKey == _shownGunKey) return;
            _shownSeat = seat; _shownLineCount = count; _shownGunKey = gunKey;
            var cfg = crew.Config;
            string template = cfg ? cfg.SeatLineTemplate : string.Empty;
            _seatLine.text = seat < 0 || string.IsNullOrEmpty(template)
                ? string.Empty
                : template.Replace("{seat}", cfg.SeatName(seat)).Replace("{gun}", crew.GunLine(seat)).Replace("{count}", count.ToString());
        }
    }
}
