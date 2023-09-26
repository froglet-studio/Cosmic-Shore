using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadoutCard : MonoBehaviour
{
    [Serializable]
    public struct LoadoutStruct
    {
        public int Intensity;
        public int PlayerCount;
        public ShipTypes ShipClass;
        public MiniGames GameMode;
    }

    [SerializeField] SO_GameList AllGames;
    [SerializeField] SO_ShipList AllShips;

    [SerializeField] TMP_Text GameTitle;
    [SerializeField] Image BackgroundImage;
    [SerializeField] Image ShipImage;
    [SerializeField] Image PlayerCountImage;
    [SerializeField] Image IntensityImage;

    /*[HideInInspector]*/ public LoadoutStruct loadout; // Show in inspector while underdevelopment for debuging


    [SerializeField] Sprite[] PlayerCountImages = new Sprite[4];
    [SerializeField] Sprite[] IntensityImages = new Sprite[4];

    void Start()
    {
        SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == loadout.GameMode).FirstOrDefault();
        GameTitle.text = game.Name;
        BackgroundImage.sprite = game.CardBackground;

        SO_Ship ship = AllShips.ShipList.Where(x => x.Class == loadout.ShipClass).FirstOrDefault();
        ShipImage.sprite = ship.TrailPreviewImage;

        PlayerCountImage.sprite = PlayerCountImages[loadout.PlayerCount - 1];

        IntensityImage.sprite = IntensityImages[loadout.Intensity - 1];
    }

}
