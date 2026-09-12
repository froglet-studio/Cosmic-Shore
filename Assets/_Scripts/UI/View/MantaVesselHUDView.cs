using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Manta's HUD view — the bomb bay and the fuse board, drawn entirely INSIDE the
    /// ability row (playtest 2026-08-26: the readouts' old floating homes read as a second
    /// UI and as messages outside the toast feed, so they were re-homed onto the cards).
    ///
    /// Three readouts, three seats:
    ///  • the BAY charge — <see cref="bombChargeFill"/> is bound as the CHARGE card's lockup
    ///    gauge (<c>AbilityIconBinding.gauge</c>), so the lockup re-homes and restyles it and
    ///    this class only ever writes <c>fillAmount</c>. Never write its colour here — the
    ///    lockup owns gauge styling, and a per-event colour write would stomp it;
    ///  • the ARMED count — a corner badge on the Charge icon, and the "can I plant?" answer:
    ///    highlighted whenever at least one bomb is ready;
    ///  • the FUSE board — a compact line above the Space (Kabloom) card: how many bombs are
    ///    riding targets and the soonest fuse. Hidden while nothing is planted.
    ///
    /// The serialized names keep their overcharge-era spellings via
    /// <see cref="FormerlySerializedAsAttribute"/> — same authored objects, new seats.
    /// </summary>
    public class MantaVesselHUDView : VesselHUDView
    {
        [Header("Bomb bay")]
        [FormerlySerializedAs("fillImage")]
        [SerializeField] private Image bombChargeFill;
        [FormerlySerializedAs("overchargePrismCount")]
        [SerializeField] private TextMeshProUGUI armedCountText;

        [Header("Planted board + fuse")]
        [FormerlySerializedAs("overchargeCountdownContainer")]
        [SerializeField] private GameObject fuseContainer;
        [FormerlySerializedAs("overchargeCountdownText")]
        [SerializeField] private TextMeshProUGUI fuseText;

        [Header("Colors")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color highlightColor = Color.yellow;

        public Color NormalColor => normalColor;
        public Color HighLightColor => highlightColor;

        public override void Initialize()
        {
            SetBombBay(0, 1, 0f);
            SetPlantedBoard(0, -1f);
        }

        /// <summary>Armed bombs over capacity; the fill carries the fractional charge.</summary>
        public void SetBombBay(int armed, int capacity, float charge)
        {
            if (armedCountText)
            {
                armedCountText.text = armed.ToString();
                armedCountText.color = armed >= 1 ? highlightColor : normalColor;
            }

            if (!bombChargeFill || capacity <= 0) return;
            bombChargeFill.fillAmount = Mathf.Clamp01(charge / capacity);
        }

        /// <summary>
        /// The live board: bombs riding targets, and the soonest fuse. Hidden while nothing
        /// is planted — a fuse readout with no fuse would be noise on the accessibility HUD.
        /// </summary>
        public void SetPlantedBoard(int planted, float shortestFuseSeconds)
        {
            bool show = planted > 0;
            if (fuseContainer && fuseContainer.activeSelf != show)
                fuseContainer.SetActive(show);

            if (!fuseText) return;
            fuseText.text = show
                ? $"{planted}x {Mathf.CeilToInt(Mathf.Max(0f, shortestFuseSeconds))}s"
                : string.Empty;
        }
    }
}
