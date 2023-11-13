using CosmicShore.App.UI.Menus;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class VesselCard : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] Sprite LockIcon;
        [SerializeField] Sprite MassBackground;
        [SerializeField] Sprite ChargeBackground;
        [SerializeField] Sprite SpaceBackground;
        [SerializeField] Sprite TimeBackground;
        [SerializeField] Color ActiveBorderColor = new Color(1f, .5f, .25f);
        [SerializeField] Color InActiveBorderColor = Color.white;
        [SerializeField] public bool Locked;

        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text VesselName;
        [SerializeField] Image BorderImage;
        [SerializeField] Image BackgroundImage;
        [SerializeField] Image VesselImage;
        [SerializeField] Image LockImage;
        [SerializeField] public int Index;  // Serialized for debugging

        SO_Vessel vessel;
        public SO_Vessel Vessel
        {
            get { return vessel; }
            set
            {
                vessel = value;
                UpdateCardView();
            }
        }

        public SquadMenu SquadMenu;

        void Start()
        {
            UpdateCardView();
            GetComponent<Button>().onClick.RemoveAllListeners();
            GetComponent<Button>().onClick.AddListener(OnCardClicked);
        }

        void UpdateCardView()
        {
            if (vessel == null) return;

            //SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == gameMode).FirstOrDefault();
            VesselName.text = vessel.Name;
            VesselImage.sprite = vessel.Image;
            LockImage.sprite = LockIcon;
            LockImage.gameObject.SetActive(Locked);

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
            BorderImage.color = active ? ActiveBorderColor : InActiveBorderColor;
        }

        public void OnCardClicked()
        {
            Debug.Log($"VesselCard - Clicked: Vessel Name: {vessel.Name}");

            if (SquadMenu != null)
            {
                SquadMenu.AssignVessel(this);
            }
        }

        public void OnUpgradeButtonClicked()
        {

        }
    }
}