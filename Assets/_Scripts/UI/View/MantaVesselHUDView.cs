using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Manta's HUD view — the bomb bay and the fuse board (the overcharge readouts this
    /// class used to draw died with the overcharge kit; the same authored UI objects were
    /// repurposed via <see cref="FormerlySerializedAsAttribute"/>, which is why the fields
    /// keep their old serialized names: a radial fill is a radial fill, whatever it meters).
    ///
    /// Three readouts:
    ///  • the BAY — radial fill (armed / capacity) + the armed count, highlighted when a bomb
    ///    is ready to plant;
    ///  • the BOARD — how many silent bombs are riding targets right now;
    ///  • the FUSE — the soonest-burning fuse, counting down: the number the whole
    ///    "one more tag, or cash in now" decision is read off.
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
                armedCountText.text = armed.ToString();

            if (!bombChargeFill || capacity <= 0) return;

            bombChargeFill.fillAmount = Mathf.Clamp01(charge / capacity);
            bombChargeFill.color = armed >= 1 ? highlightColor : normalColor;
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
                ? $"{planted} armed - {Mathf.CeilToInt(Mathf.Max(0f, shortestFuseSeconds))}s"
                : string.Empty;
        }
    }
}
