using CosmicShore.Data;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Kite's four ability cards read the navigator's loops (Garrett, 2026-10-18; design:
    /// <c>Controller/Vessel/Crew/KITE_NAVIGATOR.md</c> "Round 4d"):
    /// <list type="bullet">
    /// <item><b>Charge</b> — the rams the kite holds power for, as a number on the card.</item>
    /// <item><b>Mass</b> — a flash on the card each time a Mass cluster is laid.</item>
    /// <item><b>Space</b> — how many prisms the last Space blast destroyed, as a number.</item>
    /// <item><b>Time</b> — the boosts the navigator's Time loops have saved for the pilot, as a number, with the
    /// boost's recharge on the card's veil.</item>
    /// </list>
    /// The view is the Sparrow's HUD (the hull is a Sparrow clone), whose Initialize hides the Charge and Space
    /// icons until its guns report; the Kite has no guns, so this controller shows them. The counts are GENERATED
    /// into the cards at runtime (the Shrike's missile count is the pattern), so the HUD prefab carries no extra
    /// object. Polled from this hull's own seat, a serialized reference (vessel rule 14), behind the fleet's pilot
    /// gate (rule 5).
    /// </summary>
    public class KiteHUDController : VesselHUDController
    {
        [Header("Kite")]
        [Tooltip("This hull's navigator seat: the saved boosts, the charges, the last blast and the Mass flash.")]
        [SerializeField] KiteNavigator navigator;

        [Header("Counts (generated into the Charge, Space and Time cards)")]
        [Tooltip("The count's text. {count} is the number; empty shows nothing.")]
        [SerializeField] string countTemplate = "{count}";
        [SerializeField, Min(8f)] float countFontSize = 30f;
        [Tooltip("Where a count sits, as a fraction of its icon's height from the centre (negative = below).")]
        [SerializeField] float countOffset = -0.62f;
        [Tooltip("A count above zero — the palette's CTA lime.")]
        [SerializeField] Color countLiveColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("A count of zero.")]
        [SerializeField] Color countEmptyColor = new(0.59f, 0.65f, 0.76f, 1f);

        const string CountName = "KiteCount";
        static readonly Element[] Counted = { Element.Charge, Element.Space, Element.Time };

        readonly TMP_Text[] _counts = new TMP_Text[Counted.Length];
        readonly int[] _shown = new int[Counted.Length];
        bool _isLocalPilotHud;
        float _flashedAt;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);

            if (!navigator && vesselStatus is Component hull) hull.TryGetComponent(out navigator);

            // The fleet's pilot gate (BrittleStarHUDController / ShrikeHUDController).
            _isLocalPilotHud = vesselStatus?.Player != null
                               && !vesselStatus.IsInitializedAsAI
                               && vesselStatus.IsLocalUser;

            var view = View;
            if (!view) return;
            for (int i = 0; i < Counted.Length; i++)
            {
                if (!view.TryGetAbilityIcon(Counted[i], out var icon)) continue;
                icon.enabled = true;   // the Sparrow's view hides Charge and Space until its guns report
                _counts[i] = EnsureCount(icon);
                _shown[i] = -1;
                Paint(i, 0);
            }
            _flashedAt = navigator ? navigator.MassFlashAt : 0f;
        }

        void Update()
        {
            if (!_isLocalPilotHud || !navigator) return;
            Paint(0, navigator.Charges);
            Paint(1, navigator.LastBlastDestroyed);
            Paint(2, navigator.SavedBoosts);

            var view = View;
            if (!view) return;
            view.SetAbilityCooldown(Element.Time, navigator.BoostCooldown01);
            if (navigator.MassFlashAt != _flashedAt)
            {
                _flashedAt = navigator.MassFlashAt;
                view.PlayAbilityFlash(Element.Mass);
            }
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

        /// <summary>Repaints a count only when it changes.</summary>
        void Paint(int i, int count)
        {
            var text = _counts[i];
            if (!text || count == _shown[i]) return;
            _shown[i] = count;
            text.text = string.IsNullOrEmpty(countTemplate) ? string.Empty : countTemplate.Replace("{count}", count.ToString());
            text.color = count > 0 ? countLiveColor : countEmptyColor;
        }
    }
}
