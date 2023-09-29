using StarWriter.Core.HangerBuilder;
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
        [SerializeField] Image[] PlayerCountOptions = new Image[4];
        [SerializeField] Image[] PlayerCountBorders = new Image[4];
        [SerializeField] Image[] IntensityOptions = new Image[4];
        [SerializeField] Image[] IntensityBorders = new Image[4];

        [SerializeField] Color SelectedColor;

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
            CardList[0].Select();
        }

        // Populates the Loadout Card Containers with info from LoadoutSystem
        void PopulateLoadoutCards()
        {
            for (int i = 0; i < CardList.Count; i++)
            {
                CardList[i].SetLoadoutCard(LoadoutSystem.GetLoadout(i));
                CardList[i].SetLoadoutMenu(this);
            }
        }

        public void SelectLoadout(int index)
        {
            for (int i = 0; i < CardList.Count; i++)
                if (i != index) 
                    CardList[i].Deselect();

            var loadout = CardList[index].GetLoadout();

            Debug.Log($"LoadoutMenu - SelectLoadout - loadout:{loadout}");

            // Default load out for building a new one
            if (loadout.Uninitialized())
                loadout = new Loadout() { Intensity = 1, PlayerCount = 1, GameMode = MiniGames.BlockBandit, ShipType = ShipTypes.Manta };

            // Load values from loadout
            activeIntensity = loadout.Intensity;
            activePlayerCount = loadout.PlayerCount;
            activeShipType = loadout.ShipType;
            activeGameMode = loadout.GameMode;

            // Clear out player select and intensity selections
            for (int i = 0; i < 4; i++)
            {
                PlayerCountOptions[i].color = Color.white;
                PlayerCountBorders[i].color = Color.white;
                IntensityOptions[i].color = Color.white;
                IntensityBorders[i].color = Color.white;
            }

            // Highlight selected values for player count and intensity
            PlayerCountOptions[activePlayerCount-1].color = SelectedColor;
            PlayerCountBorders[activePlayerCount-1].color = SelectedColor;
            IntensityOptions[activeIntensity-1].color = SelectedColor;
            IntensityBorders[activeIntensity-1].color = SelectedColor;

            // 
            selectedGameIndex = AllGames.GameList.IndexOf(AllGames.GameList.Where(x => x.Mode == activeGameMode).FirstOrDefault());
            UpdateGameMode();
            selectedShipIndex = availableShips.IndexOf(AllShips.ShipList.Where(x => x.Class == activeShipType).FirstOrDefault());

            Debug.Log($"LoadoutMenu - SelectLoadout - selectedGameIndex:{selectedGameIndex}, selectedShipIndex:{selectedShipIndex}");

            

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
            Debug.Log("SelectedShipIndex is " + selectedShipIndex);

            activeShipType = availableShips[selectedShipIndex].Class;
            ShipTitle.text = availableShips[selectedShipIndex].Name;
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
            GameTitle.text = AllGames.GameList[selectedGameIndex].Name;
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
            UpdateShipClass();

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