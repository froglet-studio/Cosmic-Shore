using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Stoat's HUD readouts (design: <c>R_VesselActions/STOAT_DIPOLE.md</c> §1d). The row order IS the element map:
    /// <b>Ripple (charge) · Orbit Mode (mass) · Catch Orbit (space) · Timed Slingshot (time)</b>.
    ///
    /// <para>One gauge per card, each answering the one question its element poses:</para>
    /// <list type="number">
    /// <item><b>Ripple</b> (charge) — how big a ripple letting go RIGHT NOW would throw: the open pair's separation as a
    /// share of the largest ripple. Empty with no pair held.</item>
    /// <item><b>Mode</b> (mass) — 1 in Mass mode, 0.5 in Gate, 0 in Time mode, eased: a SWITCH moving between its
    /// ends, never a meter being spent (the Butterfly's Mass card). The card's ICON follows the mode too
    /// (<see cref="SetModeIcon"/>), so the mode B is in reads at a glance on every device - on a phone the card is the
    /// button that switches it.</item>
    /// <item><b>Accretion</b> (space) — how far the open pair has grown on what its sink swallowed (Space 5); empty
    /// without the upgrade.</item>
    /// <item><b>Time</b> — in Mass mode the pathfinder's boost (how much of the warp's speed-up is on); in Time mode,
    /// while orbiting, how far round toward the 90° sweet spot, turning to the ready colour inside the window.</item>
    /// </list>
    /// Time mode's Skim Sling also counts its run on the Time card: <b>SKIP ×N</b> (STOAT_DIPOLE.md §1q), a small label
    /// generated at runtime on the card's icon (no prefab edit), shown after each skip and faded out.
    /// The four cards are drawn in parts for the orbit-only kit (<c>Tools/Build/icon_kit/specs/stoat.py</c>, 2026-11-07)
    /// that <c>RefreshParts</c> paints; the Mass card's mode-icon swap below now lands on a transparent holder.
    /// Colour here is a GAUGE channel; the upgrade lives on the lockup's card, so this view only re-anchors its
    /// captured rest scales in <see cref="SetAbilityUpgraded"/> (<c>SquirrelVesselHUDView</c> is the reference).
    /// </summary>
    public class StoatHUDView : VesselHUDView
    {
        [Header("Gauges (one filled Image per card; the lockup re-homes and masks each to its plate)")]
        [Tooltip("Charge card: the ripple a let-go would throw now, as a share of the largest.")]
        [SerializeField] Image rippleGauge;
        [Tooltip("Mass card: the mode switch — full in Mass mode, empty in Time mode, eased between.")]
        [SerializeField] Image modeGauge;
        [Tooltip("Space card: Space-5 Accretion, how far the open pair has grown.")]
        [SerializeField] Image accretionGauge;
        [Tooltip("Time card: the pathfinder's boost (Mass mode) or the orbit's progress to the sweet spot (Time mode).")]
        [SerializeField] Image timeGauge;

        [Header("Colours")]
        [Tooltip("Every gauge's resting colour.")]
        [SerializeField] Color gaugeColor = new(0.59f, 0.65f, 0.76f, 1f);
        [Tooltip("The Time card inside the slingshot's window until the domain resolves; then the pilot's domain. It was " +
                 "the pathfinder's CTA lime until 2026-11-07: the lime is never a card's \"on\" colour.")]
        [SerializeField] Color readyColor = Color.white;
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        [Header("Mode icons (the Mass card's icon follows the mode; empty = the card keeps its authored icon)")]
        [Tooltip("Mass mode: the field trajectory (removed from the kit 2026-11-02; kept for when a mode returns).")]
        [SerializeField] Sprite massModeIcon;
        [Tooltip("Time mode: the catch sling - the orbit. The Stoat's only mode since 2026-11-02.")]
        [SerializeField] Sprite timeModeIcon;

        [Header("Skip counter (Time mode's Skim Sling; generated on the Time card when empty)")]
        [Tooltip("The SKIP ×N label. Empty: one is generated on the Time card's icon at runtime.")]
        [SerializeField] TMP_Text skipLabel;
        [SerializeField, Min(8f)] float skipFontSize = 20f;

        [Header("Ability parts (icon kit: Tools/Build/icon_kit/specs/stoat.py)")]
        [Tooltip("Charge card: the Stoat side-on (domain).")]
        [SerializeField] Image rippleStoatIcon;
        [Tooltip("Charge card: the pair meeting (white; faint with no pair held).")]
        [SerializeField] Image ripplePairIcon;
        [Tooltip("Charge card: the ripple (white; faint with no pair held).")]
        [SerializeField] Image rippleRingsIcon;
        [Tooltip("Charge card: a fauna it reaches (danger).")]
        [SerializeField] Image rippleFaunaIcon;
        [Tooltip("Charge card, Siphon (Charge 5): the stripped petals coming back (danger).")]
        [SerializeField] Image rippleSiphonIcon;
        [Tooltip("Mass card: the Stoat leaping (domain).")]
        [SerializeField] Image launchStoatIcon;
        [Tooltip("Mass card: the launch's kick (white; just launched).")]
        [SerializeField] Image launchKickIcon;
        [Tooltip("Mass card: its wake (domain; faint at rest).")]
        [SerializeField] Image launchWakeIcon;
        [Tooltip("Mass card, Armoured Wake (Mass 5): the wake rimmed (white).")]
        [SerializeField] Image launchShieldIcon;
        [Tooltip("Space card: the Stoat (domain).")]
        [SerializeField] Image orbitStoatIcon;
        [Tooltip("Space card: the attractor (white; faint with no pair).")]
        [SerializeField] Image orbitAttractorIcon;
        [Tooltip("Space card: the repulsor (white; faint with no pair).")]
        [SerializeField] Image orbitRepulsorIcon;
        [Tooltip("Space card: the orbit (white; caught).")]
        [SerializeField] Image orbitRingIcon;
        [Tooltip("Space card, Accretion (Space 5): a rival prism (danger).")]
        [SerializeField] Image orbitPreyIcon;
        [Tooltip("Space card, Accretion (Space 5): its fall into the attractor (white).")]
        [SerializeField] Image orbitPullIcon;
        [Tooltip("Time card: the attractor (white).")]
        [SerializeField] Image slingAttractorIcon;
        [Tooltip("Time card: the orbit (white).")]
        [SerializeField] Image slingOrbitIcon;
        [Tooltip("Time card: the 90-degree window (white; lit inside it).")]
        [SerializeField] Image slingWindowIcon;
        [Tooltip("Time card: the Stoat riding the orbit (domain).")]
        [SerializeField] Image slingStoatOrbitIcon;
        [Tooltip("Time card: the Stoat let go on the tangent (domain).")]
        [SerializeField] Image slingStoatGoneIcon;
        [Tooltip("Time card: the slingshot's speed (white; just let go).")]
        [SerializeField] Image slingSpeedIcon;
        [Tooltip("Time card, Foresight (Time 5): a rival's predicted path (danger).")]
        [SerializeField] Image slingForesightIcon;
        [Tooltip("Rival colour on the parts (the fleet danger orange).")]
        [SerializeField] Color dangerColor = new(1f, 0.3294f, 0.1020f, 1f);
        [Tooltip("Alpha of a part that is there but not in play.")]
        [SerializeField, Range(0f, 1f)] float idlePartAlpha = 0.35f;

        Color _domain = Color.white;
        bool _pairHeld;
        bool _orbiting;
        bool _inWindow;
        bool _launched;

        StoatModeExecutor.Mode? _shownMode;
        int _skipShown = -1;
        bool _skipPerfectShown;
        Sprite _authoredModeIcon;   // the card's own icon, shown for a mode whose slot is empty

        /// <summary>Seat every readout at rest. Idempotent: it re-runs on a vessel swap.</summary>
        public override void Initialize()
        {
            Seat(rippleGauge, 0f);
            Seat(modeGauge, 0f);
            Seat(accretionGauge, 0f);
            Seat(timeGauge, 0f);
            _shownMode = null;
            SetModeIcon(StoatModeExecutor.Mode.Time);
            _skipShown = -1;
            if (skipLabel) skipLabel.alpha = 0f;
            _pairHeld = _orbiting = _inWindow = _launched = false;
            RefreshParts();
        }

        /// <summary>The pilot's domain colour (alpha 0 = keep the last); every Stoat on the cards wears it.</summary>
        public void SetDomainColor(Color domain)
        {
            if (domain.a <= 0f || domain == _domain) return;
            _domain = domain;
            readyColor = domain;
            RefreshParts();
        }

        /// <summary>The orbit's live state: a pair held, caught on the orbit, inside the 90-degree window, just let go.</summary>
        public void SetOrbitState(bool pairHeld, bool orbiting, bool inWindow, bool launched)
        {
            if (pairHeld == _pairHeld && orbiting == _orbiting && inWindow == _inWindow && launched == _launched) return;
            _pairHeld = pairHeld;
            _orbiting = orbiting;
            _inWindow = inWindow;
            _launched = launched;
            RefreshParts();
        }

        // A colour set worn while the cards show repaints their parts (ColorTrace).
        void OnEnable()
        {
            ColorTrace.Changed += RefreshParts;
            RefreshParts();
        }

        void OnDisable() => ColorTrace.Changed -= RefreshParts;

        void RefreshParts()
        {
            float held = _pairHeld ? 1f : idlePartAlpha;
            var danger = ColorTrace.Retrace(ColorTrace.Ui, dangerColor);
            Paint(rippleStoatIcon, _domain, 1f);
            Paint(ripplePairIcon, Color.white, held);
            Paint(rippleRingsIcon, Color.white, held);
            Paint(rippleFaunaIcon, danger, 1f);
            Paint(rippleSiphonIcon, danger, IsAbilityUpgraded(Element.Charge) ? 1f : 0f);

            Paint(launchStoatIcon, _domain, 1f);
            Paint(launchKickIcon, Color.white, _launched ? 1f : 0f);
            Paint(launchWakeIcon, _domain, _launched ? 1f : idlePartAlpha);
            Paint(launchShieldIcon, Color.white, IsAbilityUpgraded(Element.Mass) ? 1f : 0f);

            bool accretion = IsAbilityUpgraded(Element.Space);
            Paint(orbitStoatIcon, _domain, 1f);
            Paint(orbitAttractorIcon, Color.white, held);
            Paint(orbitRepulsorIcon, Color.white, held);
            Paint(orbitRingIcon, Color.white, _orbiting ? 1f : 0f);
            Paint(orbitPreyIcon, danger, accretion ? 1f : 0f);
            Paint(orbitPullIcon, Color.white, accretion ? 1f : 0f);

            float ring = _orbiting ? 1f : idlePartAlpha;
            Paint(slingAttractorIcon, Color.white, ring);
            Paint(slingOrbitIcon, Color.white, ring);
            Paint(slingWindowIcon, Color.white, _orbiting && _inWindow ? 1f : idlePartAlpha);
            Paint(slingStoatOrbitIcon, _domain, _orbiting ? 1f : 0f);
            Paint(slingStoatGoneIcon, _domain, _orbiting ? 0f : 1f);
            Paint(slingSpeedIcon, Color.white, !_orbiting && _launched ? 1f : 0f);
            Paint(slingForesightIcon, danger, IsAbilityUpgraded(Element.Time) ? 1f : 0f);
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

        public void SetRipple(float charge01) => Push(rippleGauge, charge01, false);
        public void SetMode(float massMode01) => Push(modeGauge, massMode01, false);

        /// <summary>The Mass card's icon for <paramref name="mode"/>; a no-op while the mode is unchanged.</summary>
        public void SetModeIcon(StoatModeExecutor.Mode mode)
        {
            if (_shownMode == mode) return;
            var sprite = mode switch
            {
                StoatModeExecutor.Mode.Time => timeModeIcon,
                _ => massModeIcon,
            };
            if (!TryGetAbilityIcon(Element.Mass, out var icon) || !icon) return;
            if (!_authoredModeIcon) _authoredModeIcon = icon.sprite;
            icon.sprite = sprite ? sprite : _authoredModeIcon;
            _shownMode = mode;
        }
        public void SetAccretion(float accretion01) => Push(accretionGauge, accretion01, false);
        /// <summary>The Time card: <paramref name="fill01"/>, in the ready colour while <paramref name="ready"/>.</summary>
        public void SetTime(float fill01, bool ready) => Push(timeGauge, fill01, ready);

        /// <summary>
        /// The Skim Sling's run on the Time card: "SKIP ×<paramref name="count"/>" at <paramref name="alpha01"/> (0 hides
        /// it), in the ready colour for a perfect skip. The label is generated on first use when none is wired.
        /// </summary>
        public void SetSkip(int count, float alpha01, bool perfect)
        {
            if (alpha01 <= 0f || count <= 0)
            {
                if (skipLabel) skipLabel.alpha = 0f;
                return;
            }
            if (!skipLabel) skipLabel = MakeSkipLabel();
            if (!skipLabel) return;
            if (count != _skipShown || perfect != _skipPerfectShown)
            {
                _skipShown = count;
                _skipPerfectShown = perfect;
                skipLabel.text = perfect ? $"SKIP \u00d7{count}!" : $"SKIP \u00d7{count}";
                skipLabel.color = perfect ? readyColor : Color.white;
            }
            skipLabel.alpha = Mathf.Clamp01(alpha01);
        }

        TMP_Text MakeSkipLabel()
        {
            if (!TryGetAbilityIcon(Element.Time, out var icon) || !icon) return null;
            var text = ResolveGeneratedChild<TextMeshProUGUI>(icon.rectTransform, "SkipCount");
            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 4f);
            rt.sizeDelta = new Vector2(120f, 28f);
            text.alignment = TextAlignmentOptions.Bottom;
            text.fontSize = skipFontSize;
            text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.raycastTarget = false;
            text.alpha = 0f;
            return text;
        }

        void Push(Image gauge, float fill01, bool ready)
        {
            if (!gauge) return;
            gauge.fillAmount = Mathf.Clamp01(fill01);
            float k = 1f - Mathf.Exp(-colorBlendRate * Time.unscaledDeltaTime);
            gauge.color = Color.Lerp(gauge.color, ready ? readyColor : ColorTrace.Retrace(ColorTrace.Ui, gaugeColor), k);
        }

        /// <summary>
        /// Re-anchor every gauge's rest scale after the base's upgrade handling, or the next gauge write settles it back
        /// to the pre-upgrade scale and wipes the lockup's persistent bump (vessel contract rule 9).
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            RefreshParts();
            var gauge = element switch
            {
                Element.Charge => rippleGauge,
                Element.Mass => modeGauge,
                Element.Space => accretionGauge,
                Element.Time => timeGauge,
                _ => null,
            };
            if (gauge) gauge.transform.localScale = AbilityIconRestScale(element);
        }
    }
}
