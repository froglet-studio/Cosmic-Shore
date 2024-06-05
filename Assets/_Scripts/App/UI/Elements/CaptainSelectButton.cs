using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class CaptainSelectButton : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] Sprite MassBackground;
        [SerializeField] Sprite ChargeBackground;
        [SerializeField] Sprite SpaceBackground;
        [SerializeField] Sprite TimeBackground;
        [SerializeField] Color ActiveBorderColor = new Color(1f, .5f, .25f);
        [SerializeField] Color InActiveBorderColor = Color.white;

        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text CaptainName;
        [SerializeField] Image BorderImage;
        [SerializeField] Image BackgroundImage;
        [SerializeField] Image CaptainImage;
        //[SerializeField] int Index;

        SO_Captain captain;
        public SO_Captain Captain
        {
            get { return captain; }
            set
            {
                captain = value;
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
            if (captain == null) return;

            CaptainImage.sprite = captain.Image;

            switch (captain.PrimaryElement)
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
            CaptainName.color = active ? ActiveBorderColor : InActiveBorderColor;
            CaptainImage.sprite = active ? captain.Ship.CardSilohoutteActive : captain.Ship.CardSilohoutte;
        }

        /*
        public void OnCardClicked()
        {
            Debug.Log($"CaptainCard - Clicked: Captain Name: {captain.Name}");

            if (SquadMenu != null)
            {
                SquadMenu.AssignCaptain(captain);
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