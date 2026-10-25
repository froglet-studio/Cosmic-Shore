using CosmicShore.Data;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Jacks' four ability cards read the herd (design: <c>Controller/Vessel/R_VesselActions/JACKS.md</c>):
    /// <list type="bullet">
    /// <item><b>Charge</b> - the share of the herd that is DANGER, on the card's gauge.</item>
    /// <item><b>Mass</b> - the herd's size, as a number on the card, and how full it is on the gauge.</item>
    /// <item><b>Space</b> - lit while the second decoy (Time level 5) is down; the first is always down.</item>
    /// <item><b>Time</b> - the DASH (2026-10-25): lit while both jacks dash to a decoy.</item>
    /// </list>
    /// The view is the Rhino's HUD (the hull is a Rhino clone). The herd count is GENERATED into the Mass card at runtime
    /// (the Wrasse's counts are the pattern), so the HUD prefab carries no extra object. Polled from this hull's own
    /// executor, a serialized reference (vessel rule 14), behind the fleet's pilot gate (rule 5); every push is
    /// change-detected.
    /// </summary>
    public sealed class JacksHUDController : VesselHUDController
    {
        [Header("Jacks")]
        [Tooltip("This hull's herd and jacks (JacksExecutor on the root). Found on the hull when empty.")]
        [SerializeField] JacksExecutor crew;

        [Header("Herd count (generated into the Mass card)")]
        [Tooltip("The count's text. {count} is the herd's size; empty shows nothing.")]
        [SerializeField] string countTemplate = "{count}";
        [SerializeField, Min(8f)] float countFontSize = 30f;
        [Tooltip("Where the count sits, as a fraction of its icon's height from the centre (negative = below).")]
        [SerializeField] float countOffset = -0.62f;
        [Tooltip("A herd above its floor - the palette's CTA lime.")]
        [SerializeField] Color countLiveColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("A herd at or below its floor.")]
        [SerializeField] Color countFloorColor = new(0.59f, 0.65f, 0.76f, 1f);

        const string CountName = "HerdCount";

        TMP_Text _count;
        int _shownCount = -1;
        float _danger = -1f, _fill = -1f, _veil = -1f;
        bool _decoyLit, _callLit, _isLocalPilotHud;

        public override void Initialize(IVesselStatus vesselStatus)
        {
            base.Initialize(vesselStatus);
            if (!crew && vesselStatus is Component hull) crew = hull.GetComponentInChildren<JacksExecutor>(true);

            _isLocalPilotHud = vesselStatus?.Player != null && !vesselStatus.IsInitializedAsAI && vesselStatus.IsLocalUser;
            _shownCount = -1;
            _danger = _fill = _veil = -1f;
            _decoyLit = _callLit = false;

            var view = View;
            if (!view) return;
            if (view.TryGetAbilityIcon(Element.Mass, out var icon)) _count = EnsureCount(icon);
            view.SetAbilityPressed(Element.Space, false);
            view.SetAbilityPressed(Element.Time, false);
        }

        void Update()
        {
            var view = View;
            if (!_isLocalPilotHud || !crew || !view) return;

            int count = crew.HerdCount;
            if (count != _shownCount && _count)
            {
                _shownCount = count;
                _count.text = TemplateText.Fill(countTemplate, "count", count.ToString());
                _count.color = count > crew.MinHerd ? countLiveColor : countFloorColor;
            }

            float danger = crew.DangerShare;
            if (!Mathf.Approximately(danger, _danger) && view.TryGetAbilityGauge(Element.Charge, out var dangerGauge))
            {
                _danger = danger;
                dangerGauge.fillAmount = danger;
            }

            var cfg = crew.Config;
            float fill = cfg ? Mathf.Clamp01((float)count / Mathf.Max(1, cfg.HerdCap)) : 0f;
            if (!Mathf.Approximately(fill, _fill) && view.TryGetAbilityGauge(Element.Mass, out var herdGauge))
            {
                _fill = fill;
                herdGauge.fillAmount = fill;
            }

            bool decoy = crew.HasSecondDecoy;   // there is always one decoy; the card lights for the second (Time 5)
            if (decoy != _decoyLit)
            {
                _decoyLit = decoy;
                view.SetAbilityPressed(Element.Space, decoy);
            }

            bool calling = crew.Dashing;
            if (calling != _callLit)
            {
                _callLit = calling;
                view.SetAbilityPressed(Element.Time, calling);
            }

            float veil = 0f;
            if (!Mathf.Approximately(veil, _veil))
            {
                _veil = veil;
                view.SetAbilityCooldown(Element.Time, veil);
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
            text.text = string.Empty;
            return text;
        }
    }
}
