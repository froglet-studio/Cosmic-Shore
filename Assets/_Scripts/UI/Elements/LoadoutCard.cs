using System;
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

        [Header("Placeholder Locations")]
        [SerializeField] TMP_Text GameTitle;
        [SerializeField] Image BackgroundImage;
        [SerializeField] Image ShipImage;
        [SerializeField] Image PlayerCountImage;
        [SerializeField] Image IntensityImage;
        [SerializeField] int Index;
        Loadout loadout;
        void Start()
        {
            //loadout = LoadoutSystem.GetLoadout(Index);
            loadout = new Loadout(2, 4, ShipTypes.Manta, MiniGames.BlockBandit);
            UpdateCardView();
        }

        public void SetLoadoutCard(Loadout loadout)
        {
            this.loadout = loadout;
            UpdateCardView();
        }

        void UpdateCardView()
        {
            SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == loadout.GameMode).FirstOrDefault();
            GameTitle.text = game.Name;
            BackgroundImage.sprite = game.CardBackground;

            SO_Ship ship = AllShips.ShipList.Where(x => x.Class == loadout.ShipType).FirstOrDefault();
            ShipImage.sprite = ship.CardSilohoutte;

            PlayerCountImage.sprite = PlayerCountImages[loadout.PlayerCount - 1];
            IntensityImage.sprite = IntensityImages[loadout.Intensity - 1];
        }

        public void OnCardClicked()
        {
            // Add highlight boarder

            // Set active and show details
            //LoadoutView.ExpandLoadout(Index);
        }
    }

}

