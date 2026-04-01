using CosmicShore.Integrations.PlayFab.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore
{
    public class SquadMemberCard : MonoBehaviour
    {
        [SerializeField] bool ShowShipName = false;
        [SerializeField] TMP_Text CaptainName;
        [SerializeField] TMP_Text ShipName;
        [SerializeField] Image CaptainImage;
        [SerializeField] Image ShipImage;

        SO_Captain captain;
        public SO_Captain Captain
        {
            get { return captain; }
            set
            {
                captain = value;
                UpdateView();
            }
        }

        public void SetShip(SO_Vessel ship)
        {
            // Captain system removed from vessels — Port/squad screen is inactive.
        }

        /// <summary>
        /// This exists in addition to the Property for Captain so that it can be invoked as a delegate
        /// </summary>
        /// <param name="captain"></param>
        public void SetCaptain(SO_Captain captain)
        {
            CSDebug.Log($"SetCaptain:{captain.Name}");
            Captain = captain;
        }

        void UpdateView()
        {
            CSDebug.Log($"UpdateView:{captain.Name}");
            if (CaptainName != null) CaptainName.text = captain.Name;
            CaptainImage.sprite = captain.Image;
            ShipImage.sprite = captain.Vessel.SquadImage;

            if (ShowShipName) ShipName.text = captain.Vessel.Name;
        }
    }
}