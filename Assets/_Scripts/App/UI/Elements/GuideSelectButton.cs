using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class GuideSelectButton : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] Sprite MassBackground;
        [SerializeField] Sprite ChargeBackground;
        [SerializeField] Sprite SpaceBackground;
        [SerializeField] Sprite TimeBackground;
        [SerializeField] Color ActiveBorderColor = new Color(1f, .5f, .25f);
        [SerializeField] Color InActiveBorderColor = Color.white;

        [Header("Placeholder Locations")]
        [FormerlySerializedAs("VesselName")]
        [SerializeField] TMP_Text GuideName;
        [SerializeField] Image BorderImage;
        [SerializeField] Image BackgroundImage;
        [FormerlySerializedAs("VesselImage")]
        [SerializeField] Image GuideImage;
        //[SerializeField] int Index;

        SO_Guide guide;
        public SO_Guide Guide
        {
            get { return guide; }
            set
            {
                guide = value;
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
            if (guide == null) return;

            GuideImage.sprite = guide.Image;

            switch (guide.PrimaryElement)
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
            GuideName.color = active ? ActiveBorderColor : InActiveBorderColor;
            GuideImage.sprite = active ? guide.Ship.CardSilohoutteActive : guide.Ship.CardSilohoutte;
        }

        /*
        public void OnCardClicked()
        {
            Debug.Log($"GuideCard - Clicked: Guide Name: {guide.Name}");

            if (SquadMenu != null)
            {
                SquadMenu.AssignGuide(guide);
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