using StarWriter.Core.Favoriting;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class LoadoutMenu : MonoBehaviour
{
    [SerializeField] GameObject loadout0;
    [SerializeField] GameObject loadout1;
    [SerializeField] GameObject loadout2;
    [SerializeField] GameObject loadout3;
    /*
    [SerializeField] GameObject shipSelection;
    [SerializeField] GameObject gameModeSelection;

    [SerializeField] GameObject IntensitySelect0;
    [SerializeField] GameObject IntensitySelect1;
    [SerializeField] GameObject IntensitySelect2;
    [SerializeField] GameObject IntensitySelect3;

    [SerializeField] GameObject PlayerCountSelect0;
    [SerializeField] GameObject PlayerCountSelect1;
    [SerializeField] GameObject PlayerCountSelect2;
    [SerializeField] GameObject PlayerCountSelect3;*/

    [SerializeField] Transform ShipSelectionContainer; //
    [SerializeField] Transform GameSelectionContainer; //

    [SerializeField] GameObject PlayerCountButtonContainer; //

    [FormerlySerializedAs("DifficultyButtonContainer")]
    [SerializeField] GameObject IntensityButtonContainer; //

    List<Sprite> IntensityIcons = new(); //
    List<Sprite> PlayerCountIcons = new(); //
    SO_Ship SelectedShip;//
    SO_ArcadeGame SelectedGame;//

    // Loadout Settings
    int maxLoadoutSlots = 4;
    int activeIntensity = 0;
    int activePlayerCount = 0;
    ShipTypes activeShipType = 0;
    MiniGames activeGameMode = 0;

    // Start is called before the first frame update
    void Start()
    {
       LoadoutSystem. 
    }

    #region Loadouts

    void PopulateLoadoutSelectionList()
    {


    }
    
    // Sets Intensity
    public void OnClickedChangeActiveIntensity(int newIntensity)
    {
        //SetIntensity(newIntensity);
        activeIntensity = newIntensity;
        Debug.Log("Intensity changed to " + newIntensity);
        UpdateActiveLoadOut();
    }
    // Sets Player Count
    public void OnClickChangeActivePlayerCount(int newPlayerCount)
    {
        //SetPlayerCount(newPlayerCount);
        activePlayerCount = newPlayerCount;
        UpdateActiveLoadOut();
    }

    // Sets ShipTypes
    public void OnClickGotoNextEnum(bool gotoNext)
    {
        int ShipTypesCount = Enum.GetNames(typeof(ShipTypes)).Length;
        if (!gotoNext)   //Decrease to next
        {
            activeShipType--;
            if (activeShipType < 0)
            {
                activeShipType = (ShipTypes)ShipTypesCount;
            }
        }
        else
        {
            activeShipType++;    //Increase to next
            if (activeShipType > (ShipTypes)ShipTypesCount)
            {
                activeShipType = 0;
            }
        }
        Debug.Log("Active Ship Type is " + activeShipType);
        UpdateActiveLoadOut();
    }

    // Sets MiniGames
    public void OnClickGotoNextGameMode(bool gotoNext)
    {
        int gameModesCount = Enum.GetNames(typeof(MiniGames)).Length;
        if (!gotoNext)   //Decrease to next
        {
            activeGameMode--;
            if (activeGameMode <= 0)
            {
                activeGameMode = (MiniGames)gameModesCount;
            }
        }
        else
        {
            activeGameMode++;    //Increase to next
            if (activeGameMode >= (MiniGames)gameModesCount)
            {
                activeGameMode = 0;
            }
        }
        Debug.Log("Active Game Mode is " + activeGameMode);
        UpdateActiveLoadOut();
    }
    // Changes the loadout index
    public void OnClickLoadOutButton(int loadoutIndex)
    {
        LoadoutSystem.SetActiveLoadoutIndex(loadoutIndex);
    }

    void UpdateActiveLoadOut()
    {
        Loadout loadout = new Loadout();
        loadout.Intensity = activeIntensity;
        loadout.PlayerCount = activePlayerCount;
        loadout.ShipType = activeShipType;
        loadout.GameMode = activeGameMode;

        LoadoutSystem.SetCurrentlySelectedLoadout(loadout, LoadoutSystem.GetActiveLoadoutsIndex());
    }

    #endregion
}
