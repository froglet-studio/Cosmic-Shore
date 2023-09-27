using StarWriter.Core.Favoriting;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LoadoutContainer : MonoBehaviour
{
    [SerializeField] GameObject gameModeGO;
    [SerializeField] GameObject shipTypeGO;
    [SerializeField] GameObject intensityGO;
    [SerializeField] GameObject playerCountGO;

    [SerializeField] LoadoutSystem loadoutSystem;

    Loadout loadout;

    [SerializeField] int idx; //set in inspector to same as parent name index

    void Start()
    {
        //Get Loadout for this index
        loadout = loadoutSystem.GetLoadout(idx);
    }
    
    public void UpdateLoadoutSelectionImages()
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

    }

}
