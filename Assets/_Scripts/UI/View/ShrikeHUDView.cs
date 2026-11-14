using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
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
    /// a gauge full while any is ready; the domain colour while Charge is the selected mode.</item>
    /// <item><b>Mass</b> — the nest's cooldown filling; the ready colour while docked (A detaches and leaves a nest).</item>
    /// <item><b>Space</b> — the lance's cooldown filling; the ready colour while Space is the selected mode.</item>
    /// <item><b>Time</b> — the sling's cooldown filling; the ready colour while Time is the selected mode.</item>
    /// </list>
    /// The four cards are drawn in parts (<c>Tools/Build/icon_kit/specs/shrike.py</c>) that <c>RefreshParts</c> paints
    /// from the kit's live state; the selected mode's colour is the pilot's domain (it was the CTA lime until 2026-11-07).
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
        [Tooltip("The selected mode's card (and Mass while docked) until the domain resolves; then the pilot's domain. It " +
                 "was the palette's CTA lime until 2026-11-07: the lime is never a card's \"on\" colour.")]
        [SerializeField] Color readyColor = Color.white;
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        [Header("Missile count (generated into the Charge card)")]
        [SerializeField, Min(8f)] float missileCountFontSize = 30f;
        [Tooltip("Where the count sits, as a fraction of the icon's height from its centre (negative = below).")]
        [SerializeField] float missileCountOffset = -0.62f;

        [Header("Ability parts (icon kit: Tools/Build/icon_kit/specs/shrike.py)")]
        [Tooltip("Charge card: the Shrike painting (domain).")]
        [SerializeField] Image arrayShrikeIcon;
        [Tooltip("Charge card: the reticle on the target (white; faint until painted and locked).")]
        [SerializeField] Image arrayReticleIcon;
        [Tooltip("Charge card: the rival prism painted (danger).")]
        [SerializeField] Image arrayTargetIcon;
        [Tooltip("Charge card: the flora's missiles coming in (white; painted with a missile ready).")]
        [SerializeField] Image arrayMissilesIcon;
        [Tooltip("Charge card, Salvo (Charge 5): a second missile volley beside the first (white; painted with Salvo).")]
        [SerializeField] Image arraySalvoIcon;
        [Tooltip("Mass card: the Shrike docked (domain).")]
        [SerializeField] Image nestShrikeIcon;
        [Tooltip("Mass card: the nest's super-shielded cup (white; faint undocked).")]
        [SerializeField] Image nestCupIcon;
        [Tooltip("Mass card: a nest flora with its missile (white; once a nest stands).")]
        [SerializeField] Image nestFloraIcon;
        [Tooltip("Mass card, Armed Nests (Mass 5): the nest flora's second missile (white; once a nest stands with Armed Nests).")]
        [SerializeField] Image nestArmedIcon;
        [Tooltip("Space card: the Shrike (domain).")]
        [SerializeField] Image lanceShrikeIcon;
        [Tooltip("Space card: the Void Lance (white; firing).")]
        [SerializeField] Image lanceBeamIcon;
        [Tooltip("Space card: the prism in its line (danger; whole at rest).")]
        [SerializeField] Image lancePrismsIcon;
        [Tooltip("Space card: the prism broken (danger; firing).")]
        [SerializeField] Image lanceSplitIcon;
        [Tooltip("Space card, Void Pierce (Space 5): a super-shielded block on the line (white; at rest).")]
        [SerializeField] Image lancePierceIcon;
        [Tooltip("Space card, Void Pierce (Space 5): that block cut by the beam (white; firing).")]
        [SerializeField] Image lancePierceCutIcon;
        [Tooltip("Time card: the Shrike (domain).")]
        [SerializeField] Image slingShrikeIcon;
        [Tooltip("Time card: the anchor block (white).")]
        [SerializeField] Image slingAnchorIcon;
        [Tooltip("Time card: the tether (white; faint until shot).")]
        [SerializeField] Image slingTetherIcon;
        [Tooltip("Time card: the blocks it lays slinging (domain).")]
        [SerializeField] Image slingBlocksIcon;
        [Tooltip("Time card, Whipcrack (Time 5): the whip-crack chevrons on the tether (white; with Whipcrack).")]
        [SerializeField] Image slingWhipIcon;
        [Tooltip("Rival colour on the parts (the fleet danger orange).")]
        [SerializeField] Color dangerColor = new(1f, 0.3294f, 0.1020f, 1f);
        [Tooltip("Alpha of a part that is there but not in play (the reticle before a lock, the cup undocked, the tether).")]
        [SerializeField, Range(0f, 1f)] float idlePartAlpha = 0.35f;

        Color _domain = Color.white;
        bool _domainKnown;
        bool _painted;
        bool _docked;
        bool _nestBuilt;
        bool _lancing;
        bool _tethered;
        bool _slinging;

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
            _painted = _docked = _nestBuilt = _lancing = _tethered = _slinging = false;
            RefreshParts();
        }

        /// <summary>The pilot's domain colour (alpha 0 = keep the last): every Shrike on the cards, and the selected
        /// mode's gauge, wear it.</summary>
        public void SetDomainColor(Color domain)
        {
            if (domain.a <= 0f || (_domainKnown && domain == _domain)) return;
            _domain = domain;
            _domainKnown = true;
            readyColor = domain;
            _shownCount = -1;   // repaint the count in the new colour
            RefreshParts();
        }

        /// <summary>The kit's live state, read off the executor each frame.</summary>
        public void SetKit(bool paintedWithMissiles, bool docked, bool nestBuilt, bool lancing, bool tethered, bool slinging)
        {
            if (paintedWithMissiles == _painted && docked == _docked && nestBuilt == _nestBuilt && lancing == _lancing &&
                tethered == _tethered && slinging == _slinging) return;
            _painted = paintedWithMissiles;
            _docked = docked;
            _nestBuilt = nestBuilt;
            _lancing = lancing;
            _tethered = tethered;
            _slinging = slinging;
            RefreshParts();
        }

        void OnEnable() => ColorTrace.Changed += Repaint;
        void OnDisable() => ColorTrace.Changed -= Repaint;

        // A colour set worn while the HUD is up: the parts take it now, the count at its next push (the gauges ease to
        // it every frame).
        void Repaint()
        {
            _shownCount = -1;
            RefreshParts();
        }

        void RefreshParts()
        {
            var danger = ColorTrace.Retrace(ColorTrace.Ui, dangerColor);
            Paint(arrayShrikeIcon, _domain, 1f);
            Paint(arrayReticleIcon, Color.white, _painted ? 1f : idlePartAlpha);
            Paint(arrayTargetIcon, danger, 1f);
            Paint(arrayMissilesIcon, Color.white, _painted ? 1f : 0f);
            Paint(arraySalvoIcon, Color.white, _painted && IsAbilityUpgraded(Element.Charge) ? 1f : 0f);

            Paint(nestShrikeIcon, _domain, 1f);
            Paint(nestCupIcon, Color.white, _docked ? 1f : idlePartAlpha);
            Paint(nestFloraIcon, Color.white, _nestBuilt ? 1f : 0f);
            Paint(nestArmedIcon, Color.white, _nestBuilt && IsAbilityUpgraded(Element.Mass) ? 1f : 0f);

            bool pierce = IsAbilityUpgraded(Element.Space);
            Paint(lanceShrikeIcon, _domain, 1f);
            Paint(lanceBeamIcon, Color.white, _lancing ? 1f : 0f);
            Paint(lancePrismsIcon, danger, _lancing ? 0f : 1f);
            Paint(lanceSplitIcon, danger, _lancing ? 1f : 0f);
            Paint(lancePierceIcon, Color.white, pierce && !_lancing ? 1f : 0f);
            Paint(lancePierceCutIcon, Color.white, pierce && _lancing ? 1f : 0f);

            Paint(slingShrikeIcon, _domain, 1f);
            Paint(slingAnchorIcon, Color.white, 1f);
            Paint(slingTetherIcon, Color.white, _tethered || _slinging ? 1f : idlePartAlpha);
            Paint(slingBlocksIcon, _domain, _slinging ? 1f : 0f);
            Paint(slingWhipIcon, Color.white, IsAbilityUpgraded(Element.Time) ? 1f : 0f);
        }

        static void Paint(Image part, Color color, float alpha)
        {
            if (!part) return;
            var c = new Color(color.r, color.g, color.b, color.a * alpha);
            if (part.color != c) part.color = c;
        }

        void Seat(Image gauge, float fill)
        {
            if (!gauge) return;
            gauge.fillAmount = fill;
            gauge.color = ColorTrace.Retrace(ColorTrace.Ui, gaugeColor);
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
            _missileCount.color = count > 0 ? readyColor : ColorTrace.Retrace(ColorTrace.Ui, gaugeColor);
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
            gauge.color = Color.Lerp(gauge.color, live ? readyColor : ColorTrace.Retrace(ColorTrace.Ui, gaugeColor), k);
        }

        /// <summary>Re-anchor every gauge's rest scale after the base's upgrade handling (vessel contract rule 9).</summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            RefreshParts();
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
