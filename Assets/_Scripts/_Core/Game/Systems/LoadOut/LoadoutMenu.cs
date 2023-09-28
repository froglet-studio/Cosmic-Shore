using PlayFab.ClientModels;
using StarWriter.Core.HangerBuilder;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

namespace StarWriter.Core.LoadoutFavoriting
{
    public class LoadoutMenu : MonoBehaviour
    {
        [SerializeField] SO_GameList GameList;

        //Loadout Cards
        [SerializeField] GameObject loadout0;
        [SerializeField] GameObject loadout1;
        [SerializeField] GameObject loadout2;
        [SerializeField] GameObject loadout3;

        List<Sprite> IntensityIcons = new(); //
        List<Sprite> PlayerCountIcons = new(); //
        SO_Ship SelectedShip;//
        SO_ArcadeGame SelectedGame;//

        [SerializeField] SO_GameList AllGames;
        [SerializeField] SO_ShipList AllShips;


        /*[SerializeField] Transform ShipSelectionContainer; // TODO operate button event in code
        [SerializeField] Transform GameSelectionContainer; //

        [SerializeField] GameObject PlayerCountButtonContainer; //

        [FormerlySerializedAs("DifficultyButtonContainer")]
        [SerializeField] GameObject IntensityButtonContainer; //*/

        // Loadout Settings
        int activeIntensity = 0;
        int activePlayerCount = 0;
        ShipTypes activeShipType = 0;
        MiniGames activeGameMode = 0;

        // Start is called before the first frame update
        void Start()
        {
            
            LoadoutSystem.Init();
            PopulateLoadoutCards();
        }


        // Populates the Loadout Card Containers with info from LoadoutSystem
        void PopulateLoadoutCards()
        {
            loadout0.GetComponent<LoadoutCard>().SetLoadoutCard(LoadoutSystem.GetLoadout(0));
            loadout1.GetComponent<LoadoutCard>().SetLoadoutCard(LoadoutSystem.GetLoadout(1));
            loadout2.GetComponent<LoadoutCard>().SetLoadoutCard(LoadoutSystem.GetLoadout(2));
            loadout3.GetComponent<LoadoutCard>().SetLoadoutCard(LoadoutSystem.GetLoadout(3));
        }


        // Changes the loadout index
        public void OnClickLoadOutButton(int loadoutIndex)
        {
            LoadoutSystem.SetActiveLoadoutIndex(loadoutIndex);
        }

        //  Play Button press gets loadout and sends to game
        public void OnClickPlayButton() 
        {
            Loadout loadoutToPlay = LoadoutSystem.GetActiveLoadout();

            SO_ArcadeGame game_SO = AllGames.GameList.Where(x => x.Mode == loadoutToPlay.GameMode).FirstOrDefault();

            MiniGame.PlayerShipType = loadoutToPlay.ShipType;
            MiniGame.PlayerPilot = Hangar.Instance.SoarPilot; //TODO change to Element?
            MiniGame.IntensityLevel = loadoutToPlay.Intensity;
            MiniGame.NumberOfPlayers = loadoutToPlay.PlayerCount;
            SceneManager.LoadScene(game_SO.SceneName);

        }

        // Sets ShipTypes
        public void OnClickChangeClass(bool goingUp)
        {
            int ShipTypesCount = Enum.GetNames(typeof(ShipTypes)).Length;
            if (!goingUp)   //Decrease to next enum
            {
                activeShipType--;
                if (activeShipType < 0)
                {
                    activeShipType = (ShipTypes)ShipTypesCount;
                    //loadout0.GetComponentInChildren<Image>().sprite
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
        public void OnClickChangeGameMode(bool goingUp)
        {
            int gameModesCount = Enum.GetNames(typeof(MiniGames)).Length;
            if (!goingUp)   //Decrease to next
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

        // Sets Intensity
        public void OnClickedChangeActiveIntensity(int newIntensity)
        {

            //arcadeMenu.SetIntensity(newIntensity);
            activeIntensity = newIntensity;
            Debug.Log("Intensity changed to " + newIntensity);
            UpdateActiveLoadOut();
        }
        // Sets Player Count
        public void OnClickChangeActivePlayerCount(int newPlayerCount)
        {
            //arcadeMenu.SetPlayerCount(newPlayerCount);
            activePlayerCount = newPlayerCount;
            UpdateActiveLoadOut();
        }
        void UpdateActiveLoadOut()
        {
            Loadout loadout = new Loadout();
            loadout.Intensity = activeIntensity;
            loadout.PlayerCount = activePlayerCount;
            loadout.ShipType = activeShipType;
            loadout.GameMode = activeGameMode;

            int idx = LoadoutSystem.GetActiveLoadoutsIndex();

            LoadoutSystem.SetCurrentlySelectedLoadout(loadout, idx);
            UpdateLoadoutCard(loadout, idx);

        }

        void UpdateLoadoutCard(Loadout loadout, int idx)
        {
            switch (idx)
            {
                case 0: { loadout0.GetComponent<LoadoutCard>().SetLoadoutCard(loadout); break; }
                case 1: { loadout0.GetComponent<LoadoutCard>().SetLoadoutCard(loadout); break; }
                case 2: { loadout0.GetComponent<LoadoutCard>().SetLoadoutCard(loadout); break; }
                case 3: { loadout0.GetComponent<LoadoutCard>().SetLoadoutCard(loadout); break; }
                default:
                    Debug.Log("Error updating card");
                    break;
            }

        }


    }
}

