using CosmicShore.Data;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Orca's four ability cards read its crew (the 4-seat vessel; <c>Controller/Vessel/Crew/ORCA_CREW.md</c>):
    /// <list type="bullet">
    /// <item><b>Charge</b> — missiles, as a number on the card: the person's own seat's, or all three seats' at the helm
    /// (round 5: each gunner seat has its own magazine).</item>
    /// <item><b>Space</b> — a flash each time one of the crew's missiles or bombs bursts.</item>
    /// <item><b>Mass</b> — the helm's shield: a flash when it goes up, its recharge on the card's veil.</item>
    /// <item><b>Time</b> — the boost tank, emptying on the card's veil as the helm boosts.</item>
    /// </list>
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

        [Header("Count (generated into the Charge card)")]
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

        TMP_Text _count, _seatLine;
        int _shownCount = -1, _shownSeat = int.MinValue, _shownLineCount = -1, _shownGunKey = int.MinValue;
        bool _isLocalPilotHud;
        float _burstAt, _shieldAt;

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
            foreach (var e in new[] { Element.Charge, Element.Space })
                if (view.TryGetAbilityIcon(e, out var icon)) icon.enabled = true;   // the Sparrow's view hides them until its guns report
            if (view.TryGetAbilityIcon(Element.Charge, out var charge)) _count = EnsureCount(charge);
            _seatLine = EnsureSeatLine(view.transform as RectTransform);
            _shownCount = -1;
            _shownSeat = _shownGunKey = int.MinValue;
            _burstAt = crew ? crew.LastBurstAt : 0f;
            _shieldAt = crew ? crew.ShieldRaisedAt : 0f;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !crew) return;
            PaintCount(crew.Missiles);
            PaintSeatLine(crew.HumanAboard ? crew.HumanSeat : -1);

            var view = View;
            if (!view) return;
            view.SetAbilityCooldown(Element.Time, 1f - crew.BoostTank01);
            view.SetAbilityCooldown(Element.Mass, crew.ShieldCooldown01);
            if (crew.LastBurstAt != _burstAt) { _burstAt = crew.LastBurstAt; view.PlayAbilityFlash(Element.Space); }
            if (crew.ShieldRaisedAt != _shieldAt) { _shieldAt = crew.ShieldRaisedAt; view.PlayAbilityFlash(Element.Mass); }
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

        /// <summary>Repaints the magazine count only when it changes.</summary>
        void PaintCount(int count)
        {
            if (!_count || count == _shownCount) return;
            _shownCount = count;
            _count.text = string.IsNullOrEmpty(countTemplate) ? string.Empty : countTemplate.Replace("{count}", count.ToString());
            _count.color = count > 0 ? countLiveColor : countEmptyColor;
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
