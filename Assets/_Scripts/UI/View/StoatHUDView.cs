using CosmicShore.Data;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Stoat's HUD readouts (design: <c>R_VesselActions/STOAT_DIPOLE.md</c> §1d). The row order IS the element map:
    /// <b>Ripple (charge) · Mass / Time Mode (mass) · Field Dipole (space) · Pathfinder (time)</b>.
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
        [Tooltip("The Time card inside the slingshot's window, and the boost while the path is warped (the pathfinder's lime).")]
        [SerializeField] Color readyColor = new(157f / 255f, 1f, 46f / 255f, 1f);
        [Tooltip("How fast a gauge eases to its colour (1/s, exponential, unscaled time).")]
        [SerializeField, Min(0f)] float colorBlendRate = 15f;

        [Header("Mode icons (the Mass card's icon follows the mode B is in; empty = the card keeps its authored icon)")]
        [Tooltip("Mass mode: the field trajectory - the dipole and the thick trail.")]
        [SerializeField] Sprite massModeIcon;
        [Tooltip("Time mode: the catch sling - the orbit.")]
        [SerializeField] Sprite timeModeIcon;
        [Tooltip("Gate mode: the warp gate.")]
        [SerializeField] Sprite gateModeIcon;

        [Header("Skip counter (Time mode's Skim Sling; generated on the Time card when empty)")]
        [Tooltip("The SKIP ×N label. Empty: one is generated on the Time card's icon at runtime.")]
        [SerializeField] TMP_Text skipLabel;
        [SerializeField, Min(8f)] float skipFontSize = 20f;

        StoatModeExecutor.Mode? _shownMode;
        int _skipShown = -1;
        bool _skipPerfectShown;
        Sprite _authoredModeIcon;   // the card's own icon, shown for a mode whose slot is empty

        /// <summary>Seat every readout at rest. Idempotent: it re-runs on a vessel swap.</summary>
        public override void Initialize()
        {
            Seat(rippleGauge, 0f);
            Seat(modeGauge, 1f);
            Seat(accretionGauge, 0f);
            Seat(timeGauge, 0f);
            _shownMode = null;
            SetModeIcon(StoatModeExecutor.Mode.Mass);
            _skipShown = -1;
            if (skipLabel) skipLabel.alpha = 0f;
        }

        void Seat(Image gauge, float fill)
        {
            if (!gauge) return;
            gauge.fillAmount = fill;
            gauge.color = gaugeColor;
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
                StoatModeExecutor.Mode.Gate => gateModeIcon,
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
            gauge.color = Color.Lerp(gauge.color, ready ? readyColor : gaugeColor, k);
        }

        /// <summary>
        /// Re-anchor every gauge's rest scale after the base's upgrade handling, or the next gauge write settles it back
        /// to the pre-upgrade scale and wipes the lockup's persistent bump (vessel contract rule 9).
        /// </summary>
        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
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
