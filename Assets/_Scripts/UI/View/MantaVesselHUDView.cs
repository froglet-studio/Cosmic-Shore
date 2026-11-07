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
    /// The view only ever writes <c>fillAmount</c> on a gauge - the lockup owns gauge styling. The two energy fields are
    /// new and ship UNWIRED until the Manta's icon pass binds them to the Space card; an unwired field draws nothing.
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

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color highlightColor = Color.yellow;

        public Color NormalColor => normalColor;
        public Color HighLightColor => highlightColor;

        public override void Initialize()
        {
            SetEnergy(0, 1, 0f);
            SetFuse(0, 0f, -1f);
        }

        /// <summary>Energy (in bombs, fractional) over capacity; <paramref name="armed"/> whole bombs of it.</summary>
        public void SetEnergy(int armed, int capacity, float energy)
        {
            if (energyCountText)
            {
                energyCountText.text = armed.ToString();
                energyCountText.color = armed >= 1 ? highlightColor : normalColor;
            }

            if (energyFill && capacity > 0)
                energyFill.fillAmount = Mathf.Clamp01(energy / capacity);
        }

        /// <summary>
        /// The shared fuse: <paramref name="planted"/> bombs riding it, <paramref name="fuse01"/> of its length left,
        /// <paramref name="secondsLeft"/> seconds (negative with nothing planted - the board hides).
        /// </summary>
        public void SetFuse(int planted, float fuse01, float secondsLeft)
        {
            bool show = planted > 0;

            if (bombChargeFill) bombChargeFill.fillAmount = show ? Mathf.Clamp01(fuse01) : 0f;

            if (armedCountText)
            {
                armedCountText.text = planted.ToString();
                armedCountText.color = show ? highlightColor : normalColor;
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
