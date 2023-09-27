using StarWriter.Core.Favoriting;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
//using static LoadoutCard;

public class LoadoutContainer : MonoBehaviour
{
    /*[SerializeField] GameObject gameModeGO;
    [SerializeField] GameObject shipTypeGO;
    [SerializeField] GameObject intensityGO;
    [SerializeField] GameObject playerCountGO;

    Loadout loadout;

    [SerializeField] int idx; //set in inspector to same as parent name index

    void Start()
    {
        //Get Loadout for this index
        loadout = LoadoutSystem.GetLoadout(idx);
    }*/

    [SerializeField] SO_GameList AllGames;
    [SerializeField] SO_ShipList AllShips;

    [SerializeField] TMP_Text GameTitle;
    [SerializeField] Image BackgroundImage;
    [SerializeField] Image ShipImage;
    [SerializeField] Image PlayerCountImage;
    [SerializeField] Image IntensityImage;

    /*[HideInInspector]*/
    public Loadout loadout; // Show in inspector while underdevelopment for debugging

    [SerializeField] Sprite[] PlayerCountImages = new Sprite[4];
    [SerializeField] Sprite[] IntensityImages = new Sprite[4];

    [SerializeField] List<LoadoutCard> LoadoutCards;

    void Start()
    {
       
        /*SO_ArcadeGame game = AllGames.GameList.Where(x => x.Mode == loadout.GameMode).FirstOrDefault();
        GameTitle.text = game.Name;
        BackgroundImage.sprite = game.CardBackground;

        SO_Ship ship = AllShips.ShipList.Where(x => x.Class == loadout.ShipType).FirstOrDefault();
        ShipImage.sprite = ship.TrailPreviewImage;

        PlayerCountImage.sprite = PlayerCountImages[loadout.PlayerCount - 1];
        IntensityImage.sprite = IntensityImages[loadout.Intensity - 1];*/
    }


    
   /* public void UpdateLoadoutSelectionImages()
    {
        int GM = (int)loadout.GameMode;
        int ST = (int)loadout.ShipType;
        int I = loadout.Intensity;
        int PC =loadout.PlayerCount;

        switch (GM)
        {
            case 0:
                {
                    //gameModeGO.GetComponent<Image>().sprite =
                    break;
                }
            case 1:
                {
                    break;
                }
            case 2:
                {
                    break;
                }
            case 3:
                {
                    break;
                }

        }

        switch (ST)
        {
            case 0:
                {
                    //shipTypeGO.GetComponent<Image>().sprite =
                    break;
                }
            case 1:
                {
                    break;
                }
            case 2:
                {
                    break;
                }
            case 3:
                {
                    break;
                }

        }
        switch (I)
        {
            case 0:
                {
                    //intensityGO.GetComponent<Image>().sprite = 
                    break;
                }
            case 1:
                {
                    break;
                }
            case 2:
                {
                    break;
                }
            case 3:
                {
                    break;
                }

        }
        switch (PC)
        {
            case 0:
                {
                    //playerCountGO.GetComponent<Image>().sprite =
                    break;
                }
            case 1:
                {
                    //playerCountGO.GetComponent<Image>().sprite =
                    break;
                }
            case 2:
                {
                    //playerCountGO.GetComponent<Image>().sprite =
                    break;
                }
            case 3:
                {
                    //playerCountGO.GetComponent<Image>().sprite =
                    break;
                }

        }

    }*/

}
