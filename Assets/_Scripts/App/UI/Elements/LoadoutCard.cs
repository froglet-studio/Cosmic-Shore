using CosmicShore.App.Systems.Loadout;
using CosmicShore.App.UI.Views;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements
{
    public class LoadoutCard : MonoBehaviour
    {
        [Header("Resources")]
        [SerializeField] SO_GameList AllGames;
        [SerializeField] SO_ShipList AllShips;
        [SerializeField] Sprite PlusIconBackground;
        [SerializeField] Sprite[] PlayerCountImages = new Sprite[4];
        [SerializeField] Sprite[] IntensityImages = new Sprite[4];
        [SerializeField] Color DeselectedColor = Color.white;
        [SerializeField] Color SelectedColor;

        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameTitle;
        [SerializeField] Image BackgroundImage;
        [SerializeField] Image BorderImage;
        [SerializeField] Image ShipImage;
        [SerializeField] Image PlayerCountImage;
        [SerializeField] Image IntensityImage;
        [SerializeField] int Index;
        Loadout loadout;
        ArcadeLoadoutView loadoutMenu;

        void Start()
        {
            BorderImage.color = DeselectedColor;
            GameTitle.color = DeselectedColor;
            UpdateCardView();
        }

        public void SetLoadoutMenu(ArcadeLoadoutView menu)
        {
            loadoutMenu = menu;
        }
        public void SetLoadoutCard(Loadout loadout)
        {
            this.loadout = loadout;
            UpdateCardView();
        }
        public Loadout GetLoadout()
        {
            return loadout;
        }

        void UpdateCardView()
        {
            Debug.Log($"LoadoutCard.UpdateCardView - loadout: {loadout}");

            if (!loadout.Initialized)
            {
                // Show the + icon background
                Debug.Log($"No loadout for card: {Index}");
                BackgroundImage.sprite = PlusIconBackground;
                GameTitle.gameObject.SetActive(false);
                ShipImage.gameObject.SetActive(false);
                PlayerCountImage.gameObject.SetActive(false);
                IntensityImage.gameObject.SetActive(false);
                BackgroundImage.preserveAspect = true;
            }
            else
            {  
                SO_ArcadeGame game = AllGames.Games.Where(x => x.Mode == loadout.GameMode).FirstOrDefault();
                GameTitle.text = game.DisplayName;
                BackgroundImage.sprite = game.CardBackground;

                SO_Ship ship = AllShips.ShipList.Where(x => x.Class == loadout.ShipType).FirstOrDefault();
                ShipImage.sprite = ship.CardSilohoutte;

                PlayerCountImage.sprite = PlayerCountImages[loadout.PlayerCount - 1];
                IntensityImage.sprite = IntensityImages[loadout.Intensity - 1];

                GameTitle.gameObject.SetActive(true);
                ShipImage.gameObject.SetActive(true);
                PlayerCountImage.gameObject.SetActive(true);
                IntensityImage.gameObject.SetActive(true);

                BackgroundImage.preserveAspect = false;
            }
        }

        public void Select()
        {
            // Add highlight boarder
            BorderImage.color = SelectedColor;
            GameTitle.color = SelectedColor;

            // Set active and show details
            loadoutMenu.SelectLoadout(Index);
        }

        public void Deselect()
        {
            BorderImage.color = DeselectedColor;
            GameTitle.color = DeselectedColor;
        }
    }
}