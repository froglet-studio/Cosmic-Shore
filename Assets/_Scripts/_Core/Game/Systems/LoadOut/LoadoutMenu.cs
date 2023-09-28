using StarWriter.Core.HangerBuilder;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StarWriter.Core.LoadoutFavoriting
{
    public class LoadoutMenu : MonoBehaviour
    {
        [SerializeField] SO_GameList AllGames;
        [SerializeField] SO_ShipList AllShips;
        [SerializeField] List<LoadoutCard> CardList = new(4);
        [SerializeField] Image[] GameModeImages = new Image[4];
        [SerializeField] Image ShipClassImage;
        [SerializeField] TMP_Text ShipTitle;
        [SerializeField] TMP_Text GameTitle;

        List<SO_Ship> availableShips = new List<SO_Ship>();

        int selectedShipIndex;
        int selectedGameIndex;

        // Current Loadout Settings
        int activeIntensity = 0;
        int activePlayerCount = 0;
        ShipTypes activeShipType = 0;
        MiniGames activeGameMode = 0;

        void Start()
        {
            LoadoutSystem.Init();
            PopulateLoadoutCards();
        }

        // Populates the Loadout Card Containers with info from LoadoutSystem
        void PopulateLoadoutCards()
        {
            for (int i = 0; i < CardList.Count; i++)
            {
                CardList[i].SetLoadoutCard(LoadoutSystem.GetLoadout(i));
                CardList[i].SetLoadoutMenu(this);
            }

            CardList[0].Select();
        }

        public void SelectLoadout(int index)
        {
            CardList[LoadoutSystem.GetActiveLoadoutIndex()].Deselect();

            var loadout = CardList[index].GetLoadout();

            if (loadout.Uninitialized())
                loadout = new Loadout() { Intensity = 1, PlayerCount = 1, GameMode = MiniGames.BlockBandit, ShipType = ShipTypes.Manta };

            activeIntensity = loadout.Intensity;
            activePlayerCount = loadout.PlayerCount;
            activeShipType = loadout.ShipType;
            activeGameMode = loadout.GameMode;

            selectedGameIndex = AllGames.GameList.IndexOf(AllGames.GameList.Where(x => x.Mode == activeGameMode).FirstOrDefault());
            selectedShipIndex = AllShips.ShipList.IndexOf(AllShips.ShipList.Where(x => x.Class == activeShipType).FirstOrDefault());

            Debug.Log($"LoadoutMenu - SelectLoadout - selectedGameIndex:{selectedGameIndex}, selectedShipIndex:{selectedShipIndex}");

            UpdateGameMode();
            UpdateShipClass();

            /*
            GameTitle.text = activeGameMode.ToString();
            ShipTitle.text = activeShipType.ToString();

            ShipClassImage.sprite = AllShips.ShipList[selectedShipIndex].CardSilohoutte;
            foreach (var image in GameModeImages)
                image.sprite = AllGames.GameList[selectedGameIndex].CardBackground;

            availableShips = new List<SO_Ship>();
            foreach (var pilot in AllGames.GameList[selectedGameIndex].Pilots)
                availableShips.Add(pilot.Ship);
            */

            LoadoutSystem.SetActiveLoadoutIndex(index);
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
            int iter = goingUp ? -1 : 1;

            selectedShipIndex = selectedShipIndex + iter;
            if (selectedShipIndex < 0) selectedShipIndex = availableShips.Count() - 1;
            if (selectedShipIndex >= availableShips.Count()) selectedShipIndex = 0;

            UpdateShipClass();
            UpdateActiveLoadOut();
        }

        void UpdateShipClass()
        {
            activeShipType = availableShips[selectedShipIndex].Class;
            ShipTitle.text = activeShipType.ToString();
            ShipClassImage.sprite = availableShips[selectedShipIndex].CardSilohoutte;

            Debug.Log("Active Ship Type is " + activeShipType);
        }

        // Sets MiniGames
        public void OnClickChangeGameMode(bool goingUp)
        {
            int iter = goingUp ? -1 : 1;

            selectedGameIndex = selectedGameIndex + iter;
            if (selectedGameIndex < 0) selectedGameIndex = AllGames.GameList.Count() - 1;
            if (selectedGameIndex >= AllGames.GameList.Count()) selectedGameIndex = 0;

            UpdateGameMode();
            UpdateActiveLoadOut();
        }

        void UpdateGameMode()
        {
            activeGameMode = AllGames.GameList[selectedGameIndex].Mode;
            GameTitle.text = activeGameMode.ToString();
            foreach (var image in GameModeImages)
                image.sprite = AllGames.GameList[selectedGameIndex].CardBackground;

            availableShips = new List<SO_Ship>();
            foreach (var pilot in AllGames.GameList[selectedGameIndex].Pilots)
                availableShips.Add(pilot.Ship);

            // If selected ship is not available, fall back to zero
            if (!availableShips.Contains(AllShips.ShipList.Where(x => x.Class == activeShipType).FirstOrDefault()))
            {
                selectedShipIndex = 0;
                UpdateShipClass();
            }


            // TODO: Disable unavailable player count options, if selected player count is unavailable fall back to 1

            Debug.Log("LoadoutMenu - OnClickChangeGameMode - Active Game Mode is " + activeGameMode);
        }

        // Sets Intensity
        public void OnClickedChangeActiveIntensity(int newIntensity)
        {
            Debug.Log("LoadoutMenu - OnClickedChangeActiveIntensity - Intensity changed to " + newIntensity);

            activeIntensity = newIntensity;

            UpdateActiveLoadOut();
        }

        // Sets Player Count
        public void OnClickChangeActivePlayerCount(int newPlayerCount)
        {
            Debug.Log("LoadoutMenu - OnClickChangeActivePlayerCount - PlayerCount changed to " + newPlayerCount);

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

            int idx = LoadoutSystem.GetActiveLoadoutIndex();

            LoadoutSystem.SetLoadout(loadout, idx);
            CardList[idx].SetLoadoutCard(loadout);
        }
    }
}