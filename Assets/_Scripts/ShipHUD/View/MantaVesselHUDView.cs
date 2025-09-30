using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class MantaVesselHUDView : VesselHUDView
    {
        [Header("Simple counter")]
        public TMP_Text countText;

        public Image FillImage => fillImage;
        public TextMeshProUGUI OverchargePrismCount => overchargePrismCount; 
        public GameObject OverchargeCountdownContainer => overchargeCountdownContainer;
        public TextMeshProUGUI OverChargeCountdownText => overchargeCountdownText;
        public Color NormalColor => normalColor;
        public Color HighLightColor => highlightColor;

        [SerializeField] private Image fillImage;
        [SerializeField] private TextMeshProUGUI overchargePrismCount;
        [SerializeField] private GameObject overchargeCountdownContainer;
        [SerializeField] private TextMeshProUGUI overchargeCountdownText;
        [SerializeField] private Color normalColor;
        [SerializeField] private Color highlightColor;
    }
}