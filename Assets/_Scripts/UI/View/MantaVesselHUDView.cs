using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Manta's HUD view (the 2026-11 overhaul, MANTA_STING_KABLOOM.md §6) - drawn entirely INSIDE the ability row:
    ///  • the CHARGE card shows the ONE shared fuse every planted bomb rides - <see cref="bombChargeFill"/> is that card's
    ///    lockup gauge (<c>AbilityIconBinding.gauge</c>) and drains as the fuse burns, refilling on every new bomb; the
    ///    corner badge (<see cref="armedCountText"/>) is how many bombs ride it, and the fuse board
    ///    (<see cref="fuseText"/>) is the seconds left;
    ///  • the SPACE card shows the ENERGY the skim has gathered - <see cref="energyFill"/> (whole bombs and the fraction
    ///    toward the next), and <see cref="energyCountText"/> how many bombs it buys, highlighted while it buys one.
    /// The view only ever writes <c>fillAmount</c> on a gauge - the lockup owns gauge styling.
    ///
    /// The four cards are PARTS (the 2026-11-07 icon pass, <c>Tools/Build/icon_kit/specs/manta.py</c>): white sprites in
    /// each slot's transparent container, tinted and shown here - the Manta in the pilot's domain colour, rivals and the
    /// trap in the danger colour, everything else white. Charge draws a bomb per plant (three at most) on a rival and,
    /// at Charge 5, the second rival Contagion bombs; Mass draws the decoys that stand; Space brightens the explosive
    /// spikes as the energy gathers and shows the knocked-off shell at Space 5; Time adds Soar's chevrons at Time 5.
    /// </summary>
    public class MantaVesselHUDView : VesselHUDView
    {
        [Header("Shared fuse (Charge card)")]
        [Tooltip("The Charge card's lockup gauge: the shared fuse, full when a bomb has just reset it.")]
        [FormerlySerializedAs("fillImage")]
        [SerializeField] private Image bombChargeFill;
        [Tooltip("Corner badge on the Charge icon: bombs riding the fuse.")]
        [FormerlySerializedAs("overchargePrismCount")]
        [SerializeField] private TextMeshProUGUI armedCountText;

        [FormerlySerializedAs("overchargeCountdownContainer")]
        [SerializeField] private GameObject fuseContainer;
        [FormerlySerializedAs("overchargeCountdownText")]
        [SerializeField] private TextMeshProUGUI fuseText;

        [Tooltip("The fuse board's line. {seconds} = seconds left on the shared fuse, {planted} = bombs riding it. " +
                 "Empty shows nothing.")]
        [SerializeField] private string fuseTemplate = "{planted}x {seconds}s";

        [Header("Energy (Space card)")]
        [Tooltip("The Space card's gauge: energy over capacity. Bind it to the Space card's lockup gauge.")]
        [SerializeField] private Image energyFill;
        [Tooltip("Corner badge on the Space icon: how many bombs the energy buys.")]
        [SerializeField] private TextMeshProUGUI energyCountText;

        [Header("Charge card parts (Sting)")]
        [Tooltip("The Manta from above (Manta_StingManta), the domain colour.")]
        [SerializeField] private Image stingMantaIcon;
        [Tooltip("The rival it bombed (Manta_StingRival), the danger colour; dim with nothing planted.")]
        [SerializeField] private Image stingRivalIcon;
        [Tooltip("The first planted bomb (Manta_StingBomb0), white.")]
        [SerializeField] private Image stingBomb0Icon;
        [Tooltip("The second planted bomb (Manta_StingBomb1), white.")]
        [SerializeField] private Image stingBomb1Icon;
        [Tooltip("The third planted bomb and on (Manta_StingBomb2), white.")]
        [SerializeField] private Image stingBomb2Icon;
        [Tooltip("Contagion (Charge 5): the second rival a blast catches (Manta_StingContagionRival), the danger colour.")]
        [SerializeField] private Image stingContagionRivalIcon;
        [Tooltip("Contagion (Charge 5): the bomb the blast gave it (Manta_StingContagionBomb), white.")]
        [SerializeField] private Image stingContagionBombIcon;
        [Tooltip("The rival's alpha with nothing planted.")]
        [SerializeField, Range(0f, 1f)] private float rivalIdleAlpha = 0.35f;

        [Header("Mass card parts (Decoy)")]
        [Tooltip("The decoy crystal, one plate frozen (Manta_DecoyCrystal), white; dim with no decoy standing.")]
        [SerializeField] private Image decoyCrystalIcon;
        [Tooltip("The omni decoy (Manta_DecoyOmni), white; shown while one stands (Mass 5).")]
        [SerializeField] private Image decoyOmniIcon;
        [Tooltip("The danger prisms a taken decoy springs (Manta_DecoyTrap), the danger colour.")]
        [SerializeField] private Image decoyTrapIcon;
        [Tooltip("The decoy and trap alpha with no decoy standing.")]
        [SerializeField, Range(0f, 1f)] private float decoyIdleAlpha = 0.3f;

        [Header("Space card parts (Skim)")]
        [Tooltip("The Manta from above (Manta_SkimManta), the domain colour.")]
        [SerializeField] private Image skimMantaIcon;
        [Tooltip("The rival prisms it skims (Manta_SkimPrisms), the danger colour.")]
        [SerializeField] private Image skimPrismsIcon;
        [Tooltip("The explosive spikes round each prism (Manta_SkimSparks), white; brighten as the energy gathers.")]
        [SerializeField] private Image skimSparksIcon;
        [Tooltip("Shield Breaker (Space 5): a shell knocked off the prisms (Manta_SkimShards), white.")]
        [SerializeField] private Image skimShardsIcon;
        [Tooltip("The spikes' alpha with no energy gathered.")]
        [SerializeField, Range(0f, 1f)] private float sparksIdleAlpha = 0.45f;

        [Header("Time card parts (Yawstery)")]
        [Tooltip("The Manta from above (Manta_YawsteryManta), the domain colour.")]
        [FormerlySerializedAs("yastriMantaIcon")]
        [SerializeField] private Image yawsteryMantaIcon;
        [Tooltip("The hard corner its turn pair cuts (Manta_YawsteryTurn), white.")]
        [FormerlySerializedAs("yastriTurnIcon")]
        [SerializeField] private Image yawsteryTurnIcon;
        [Tooltip("Soar (Time 5): the boost chevrons ahead of the nose (Manta_YawsterySoar), white.")]
        [FormerlySerializedAs("yastriSoarIcon")]
        [SerializeField] private Image yawsterySoarIcon;

        [Tooltip("The danger colour the rivals and the trap wear (ElementalBarsConfigSO.FireColor).")]
        [SerializeField] private Color dangerColor = new(1f, 0.3294f, 0.1020f, 1f);

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color highlightColor = Color.yellow;

        public Color NormalColor => normalColor;
        public Color HighLightColor => ColorTrace.Retrace(ColorTrace.Ui, highlightColor);

        Color _domain = Color.white;
        int _planted;
        float _energy01;
        bool _elementalDecoy;
        bool _omniDecoy;

        public override void Initialize()
        {
            SetEnergy(0, 1, 0f);
            SetFuse(0, 0f, -1f);
            SetDecoys(false, false);
            RefreshParts();
        }

        /// <summary>The pilot's domain colour (alpha 0 = keep the last); the four Mantas wear it.</summary>
        public void SetDomainColor(Color domain)
        {
            if (domain.a <= 0f || domain == _domain) return;
            _domain = domain;
            RefreshParts();
        }

        /// <summary>Which of this Manta's decoys stand: the Mass card's decoy and omni decoy parts.</summary>
        public void SetDecoys(bool elemental, bool omni)
        {
            if (elemental == _elementalDecoy && omni == _omniDecoy) return;
            _elementalDecoy = elemental;
            _omniDecoy = omni;
            RefreshParts();
        }

        public override void SetAbilityUpgraded(Element element, bool upgraded)
        {
            base.SetAbilityUpgraded(element, upgraded);
            RefreshParts();
        }

        void RefreshParts()
        {
            Paint(stingMantaIcon, _domain, 1f);
            Paint(yawsteryMantaIcon, _domain, 1f);
            Paint(skimMantaIcon, _domain, 1f);

            bool charge5 = IsAbilityUpgraded(Element.Charge);
            // The worn colour set's danger (UIThemeLive repaints the parts when another set is worn between refreshes).
            var danger = ColorTrace.Retrace(ColorTrace.Ui, dangerColor);
            Paint(stingRivalIcon, danger, _planted > 0 ? 1f : rivalIdleAlpha);
            Paint(stingBomb0Icon, Color.white, _planted >= 1 ? 1f : 0f);
            Paint(stingBomb1Icon, Color.white, _planted >= 2 ? 1f : 0f);
            Paint(stingBomb2Icon, Color.white, _planted >= 3 ? 1f : 0f);
            Paint(stingContagionRivalIcon, danger, charge5 && _planted > 0 ? 1f : 0f);
            Paint(stingContagionBombIcon, Color.white, charge5 && _planted > 0 ? 1f : 0f);

            float decoy = _elementalDecoy || _omniDecoy ? 1f : decoyIdleAlpha;
            Paint(decoyCrystalIcon, Color.white, decoy);
            Paint(decoyTrapIcon, danger, decoy);
            Paint(decoyOmniIcon, Color.white, _omniDecoy ? 1f : 0f);

            Paint(skimPrismsIcon, danger, 1f);
            Paint(skimSparksIcon, Color.white, Mathf.Lerp(sparksIdleAlpha, 1f, _energy01));
            Paint(skimShardsIcon, Color.white, IsAbilityUpgraded(Element.Space) ? 1f : 0f);

            Paint(yawsteryTurnIcon, Color.white, 1f);
            Paint(yawsterySoarIcon, Color.white, IsAbilityUpgraded(Element.Time) ? 1f : 0f);
        }

        static void Paint(Image part, Color color, float alpha)
        {
            if (!part) return;
            var c = new Color(color.r, color.g, color.b, color.a * alpha);
            if (part.color != c) part.color = c;
        }

        /// <summary>Energy (in bombs, fractional) over capacity; <paramref name="armed"/> whole bombs of it.</summary>
        public void SetEnergy(int armed, int capacity, float energy)
        {
            if (energyCountText)
            {
                energyCountText.text = armed.ToString();
                energyCountText.color = armed >= 1 ? HighLightColor : normalColor;
            }

            float energy01 = capacity > 0 ? Mathf.Clamp01(energy / capacity) : 0f;
            if (energyFill && capacity > 0)
                energyFill.fillAmount = energy01;
            if (!Mathf.Approximately(energy01, _energy01))
            {
                _energy01 = energy01;
                RefreshParts();
            }
        }

        /// <summary>
        /// The shared fuse: <paramref name="planted"/> bombs riding it, <paramref name="fuse01"/> of its length left,
        /// <paramref name="secondsLeft"/> seconds (negative with nothing planted - the board hides).
        /// </summary>
        public void SetFuse(int planted, float fuse01, float secondsLeft)
        {
            bool show = planted > 0;
            if (planted != _planted)
            {
                _planted = planted;
                RefreshParts();
            }

            if (bombChargeFill) bombChargeFill.fillAmount = show ? Mathf.Clamp01(fuse01) : 0f;

            if (armedCountText)
            {
                armedCountText.text = planted.ToString();
                armedCountText.color = show ? HighLightColor : normalColor;
            }

            if (fuseContainer && fuseContainer.activeSelf != show)
                fuseContainer.SetActive(show);

            if (!fuseText) return;
            fuseText.text = show
                ? TemplateText.Fill(fuseTemplate,
                    "planted", planted.ToString(),
                    "seconds", Mathf.CeilToInt(Mathf.Max(0f, secondsLeft)).ToString())
                : string.Empty;
        }
    }
}
