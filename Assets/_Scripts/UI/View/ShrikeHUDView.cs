using CosmicShore.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Shrike's HUD readouts (design: <c>R_VesselActions/SHRIKE.md</c> §4). The row order IS the element map:
    /// <b>Targeting Array (charge) · Nest (mass) · Void Lance (space) · Void Sling (time)</b>.
    /// <list type="number">
    /// <item><b>Charge</b> — the missiles left across every nest flora, as a NUMBER on the card (Garrett, 2026-10-15) and
    /// a gauge full while any is ready; the ready colour while Charge is the selected mode.</item>
    /// <item><b>Mass</b> — the nest's cooldown filling; the ready colour while docked (A detaches and leaves a nest).</item>
    /// <item><b>Space</b> — the lance's cooldown filling; the ready colour while Space is the selected mode.</item>
    /// <item><b>Time</b> — the sling's cooldown filling; the ready colour while Time is the selected mode.</item>
    /// </list>
    /// The count is GENERATED into the Charge card at runtime (the Squirrel's steal count is the pattern), so the HUD
    /// prefab carries no extra object. Colour here is a GAUGE channel; the upgrade lives on the lockup's card, so this
    /// view only re-anchors its captured rest scales in <see cref="SetAbilityUpgraded"/> (vessel rule 9).
    /// </summary>
    public class ShrikeHUDView : VesselHUDView
    {
        [Header("Gauges (one filled Image per card; the lockup re-homes and masks each to its plate)")]
        [Tooltip("Charge card: the Targeting Array — full while a missile is ready.")]
        [SerializeField] Image chargeGauge;
        [Tooltip("Mass card: the nest's cooldown.")]
        [SerializeField] Image massGauge;
        [Tooltip("Space card: the Void Lance's cooldown.")]
        [SerializeField] Image spaceGauge;
        [Tooltip("Time card: the Void Sling's cooldown.")]
        [SerializeField] Image timeGauge;

        [Header("Colours")]
        [Tooltip("Every gauge's resting colour.")]
        [SerializeField] Color gaugeColor = new(0.59f, 0.65f, 0.76f, 1f);
        [Tooltip("The selected mode's card (and Mass while docked) — the palette's CTA lime.")]
        [SerializeField] Color readyColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        [Header("Missile count (generated into the Charge card)")]
        [SerializeField, Min(8f)] float missileCountFontSize = 30f;
        [Tooltip("Where the count sits, as a fraction of the icon's height from its centre (negative = below).")]
        [SerializeField] float missileCountOffset = -0.62f;

        TMP_Text _missileCount;
        int _shownCount = -1;

        /// <summary>Seat every readout at rest (everything ready, no missiles). Idempotent: it re-runs on a swap.</summary>
        public override void Initialize()
        {
            Seat(chargeGauge, 0f);
            Seat(massGauge, 1f);
            Seat(spaceGauge, 1f);
            Seat(timeGauge, 1f);
            EnsureMissileCount();
            SetMissileCount(0);
        }

        void Seat(Image gauge, float fill)
        {
            if (!gauge) return;
            gauge.fillAmount = fill;
            gauge.color = gaugeColor;
        }

        void EnsureMissileCount()
        {
            if (_missileCount || !TryGetAbilityIcon(Element.Charge, out var icon)) return;
            _missileCount = ResolveGeneratedChild<TextMeshProUGUI>(icon.rectTransform, "MissileCount");
            var rt = _missileCount.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(icon.rectTransform.sizeDelta.x, missileCountFontSize * 1.3f);
            rt.anchoredPosition = new Vector2(0f, icon.rectTransform.sizeDelta.y * missileCountOffset);
            rt.localScale = Vector3.one;
            _missileCount.alignment = TextAlignmentOptions.Center;
            _missileCount.fontSize = missileCountFontSize;
            _missileCount.enableWordWrapping = false;
            _missileCount.overflowMode = TextOverflowModes.Overflow;
            _missileCount.raycastTarget = false;
        }

        /// <summary>The missiles left across every nest flora. Repaints only on a change.</summary>
        public void SetMissileCount(int count)
        {
            EnsureMissileCount();
            if (!_missileCount || count == _shownCount) return;
            _shownCount = count;
            _missileCount.text = count.ToString();
            _missileCount.color = count > 0 ? readyColor : gaugeColor;
        }

        public void SetCharge(float fill01, bool live) => Push(chargeGauge, fill01, live);
        public void SetMass(float ready01, bool live) => Push(massGauge, ready01, live);
        public void SetSpace(float ready01, bool live) => Push(spaceGauge, ready01, live);
        public void SetTime(float ready01, bool live) => Push(timeGauge, ready01, live);

        void Push(Image gauge, float fill01, bool live)
        {
            if (!gauge) return;
            gauge.fillAmount = Mathf.Clamp01(fill01);
            float k = 1f - Mathf.Exp(-colorBlendRate * Time.unscaledDeltaTime);
            gauge.color = Color.Lerp(gauge.color, live ? readyColor : gaugeColor, k);
        }

        /// <summary>Re-anchor every gauge's rest scale after the base's upgrade handling (vessel contract rule 9).</summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            var gauge = element switch
            {
                Element.Charge => chargeGauge,
                Element.Mass => massGauge,
                Element.Space => spaceGauge,
                Element.Time => timeGauge,
                _ => null,
            };
            if (gauge) gauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
