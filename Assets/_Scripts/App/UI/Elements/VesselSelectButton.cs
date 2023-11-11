using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class VesselSelectButton : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] Sprite MassBackground;
        [SerializeField] Sprite ChargeBackground;
        [SerializeField] Sprite SpaceBackground;
        [SerializeField] Sprite TimeBackground;
        [SerializeField] Color ActiveBorderColor = new Color(1f, .5f, .25f);
        [SerializeField] Color InActiveBorderColor = Color.white;

        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text VesselName;
        [SerializeField] Image BorderImage;
        [SerializeField] Image BackgroundImage;
        [SerializeField] Image VesselImage;
        //[SerializeField] int Index;

        SO_Vessel vessel;
        public SO_Vessel Vessel
        {
            get { return vessel; }
            set
            {
                vessel = value;
                UpdateButtonView();
            }
        }

        //public SquadMenu SquadMenu;

        void Start()
        {
            UpdateButtonView();
        }


        void UpdateButtonView()
        {
            if (vessel == null) return;

            //SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == gameMode).FirstOrDefault();
            //VesselName.text = vessel.Name;
            VesselImage.sprite = vessel.Image;

            switch (vessel.PrimaryElement)
            {
                case Element.Mass:
                    BackgroundImage.sprite = MassBackground;
                    break;
                case Element.Charge:
                    BackgroundImage.sprite = ChargeBackground;
                    break;
                case Element.Space:
                    BackgroundImage.sprite = SpaceBackground;
                    break;
                case Element.Time:
                    BackgroundImage.sprite = TimeBackground;
                    break;
            }
        }

        public void Active(bool active = true)
        {
            BackgroundImage.color = active ? ActiveBorderColor : InActiveBorderColor;
            VesselName.color = active ? ActiveBorderColor : InActiveBorderColor;
            VesselImage.sprite = active ? vessel.Ship.CardSilohoutteActive : vessel.Ship.CardSilohoutte;
        }

        /*
        public void OnCardClicked()
        {
            Debug.Log($"VesselCard - Clicked: Vessel Name: {vessel.Name}");

            if (SquadMenu != null)
            {
                SquadMenu.AssignVessel(vessel);
            }

            // Add highlight border
            BorderImage.color = Color.yellow;
        }

        public void OnUpgradeButtonClicked()
        {

        }
        */
    }
}