using CosmicShore.Integrations.PlayFab.Economy;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

        public void SetShip(SO_Ship ship)
        {
            foreach (var captain in ship.Captains)
            {
                if (CatalogManager.Inventory.ContainsCaptain(captain.Name))
                {
                    Captain = captain;
                    break;
                }
            }
        }

        /// <summary>
        /// This exists in addition to the Property for Captain so that it can be invoked as a delegate
        /// </summary>
        /// <param name="captain"></param>
        public void SetCaptain(SO_Captain captain)
        {
            Debug.Log($"SetCaptain:{captain.Name}");
            Captain = captain;
        }

        void UpdateView()
        {
            Debug.Log($"UpdateView:{captain.Name}");
            CaptainName.text = captain.Name;
            CaptainImage.sprite = captain.Image;
            ShipImage.sprite = captain.Ship.CardSilohoutteActive;

            if (ShowShipName) ShipName.text = captain.Ship.Name;
        }
    }
}