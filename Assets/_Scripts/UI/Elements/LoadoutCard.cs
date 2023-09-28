using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StarWriter.Core.LoadoutFavoriting
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
        [SerializeField] Color SelectedColor = Color.white;

        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameTitle;
        [SerializeField] Image BackgroundImage;
        [SerializeField] Image BorderImage;
        [SerializeField] Image ShipImage;
        [SerializeField] Image PlayerCountImage;
        [SerializeField] Image IntensityImage;
        [SerializeField] int Index;
        Loadout loadout;
        LoadoutMenu loadoutMenu;

        void Start()
        {
            BorderImage.color = DeselectedColor;
            GameTitle.color = DeselectedColor;
            loadout = new Loadout(2, 4, ShipTypes.Manta, MiniGames.BlockBandit);
            UpdateCardView();
        }

        public void SetLoadoutMenu(LoadoutMenu menu)
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

            if (loadout.Uninitialized())
            {
                // Show the + icon background
                Debug.Log($"No loadout for card: {Index}");
                BackgroundImage.sprite = PlusIconBackground;
            }
            else
            {  
                SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == loadout.GameMode).FirstOrDefault();
                GameTitle.text = game.Name;
                BackgroundImage.sprite = game.CardBackground;

                SO_Ship ship = AllShips.ShipList.Where(x => x.Class == loadout.ShipType).FirstOrDefault();
                ShipImage.sprite = ship.CardSilohoutte;

                PlayerCountImage.sprite = PlayerCountImages[loadout.PlayerCount - 1];
                IntensityImage.sprite = IntensityImages[loadout.Intensity - 1];
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