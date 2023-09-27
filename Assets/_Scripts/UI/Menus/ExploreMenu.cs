using StarWriter.Core.HangerBuilder;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ExploreMenu : MonoBehaviour
{
    [Header("Game Selection View")]
    [SerializeField] SO_GameList GameList;
    [SerializeField] GameObject GameSelectionView;
    [SerializeField] Transform GameSelectionGrid;

    [Header("Game Detail View")]
    [SerializeField] GameObject GameDetailView;
    [SerializeField] TMPro.TMP_Text SelectedGameName;
    [SerializeField] TMPro.TMP_Text SelectedGameDescription;
    [SerializeField] TMPro.TMP_Text AllowedPlayerCountText;
    [SerializeField] GameObject SelectedGamePreviewWindow;
    [SerializeField] Transform ShipSelectionGrid;

    [Header("Game Play Settings")]
    [SerializeField] GameObject PlayerCountButtonContainer;
    [SerializeField] GameObject IntensityButtonContainer;
    [SerializeField] List<Sprite> IntensityIcons = new(4);
    [SerializeField] List<Sprite> PlayerCountIcons = new(4);

    SO_Ship SelectedShip;
    SO_ArcadeGame SelectedGame;
    List<GameCard> GameCards;

    void Start()
    {
        PopulateGameSelectionList();
        ShowGameSelectionView();
    }

    void ShowGameSelectionView()
    {
        GameSelectionView.SetActive(true);
        GameDetailView.SetActive(false);
    }

    void ShowGameDetailView()
    {
        GameSelectionView.SetActive(false);
        GameDetailView.SetActive(true);
    }
    void PopulateGameSelectionList()
    {
        GameCards = new List<GameCard>();

        // Deactivate all game cards and add them to the list of game cards
        for (var i = 0; i < GameSelectionGrid.transform.childCount; i++)
        {
            var gameSelectionRow = GameSelectionGrid.GetChild(i);
            for (var j = 0; j < gameSelectionRow.childCount; j++)
            {
                gameSelectionRow.GetChild(j).gameObject.SetActive(false);
                GameCards.Add(gameSelectionRow.GetChild(j).GetComponent<GameCard>());
            }
        }

        for (var i = 0; i < GameList.GameList.Count; i++) {
            var selectionIndex = i;
            var game = GameList.GameList[i];

            Debug.Log($"Populating Game Select List: {game.Name}");
            
            var gameCard = GameCards[i];
            gameCard.GameMode = game.Mode;
            gameCard.Locked = i % 3 == 0;  // TODO: pull this from somewhere real
            gameCard.GetComponent<Button>().onClick.RemoveAllListeners();
            gameCard.GetComponent<Button>().onClick.AddListener(() => SelectGame(selectionIndex));
            gameCard.GetComponent<Button>().onClick.AddListener(() => GameSelectionGrid.GetComponent<MenuAudio>().PlayAudio());
            gameCard.gameObject.SetActive(true);
        }

        //StartCoroutine(SelectGameCoroutine(0));
    }

    void PopulateGameDetails()
    {
        Debug.Log($"Populating Game Details List: {SelectedGame.Name}");
        Debug.Log($"Populating Game Details List: {SelectedGame.Description}");
        Debug.Log($"Populating Game Details List: {SelectedGame.Icon}");
        Debug.Log($"Populating Game Details List: {SelectedGame.PreviewClip}");

        // Set Game Detail Meta Data
        SelectedGameName.text = SelectedGame.Name;
        SelectedGameDescription.text = SelectedGame.Description;
        AllowedPlayerCountText.text = SelectedGame.MinPlayers + "-" + SelectedGame.MaxPlayers;

        // TODO: reconsider how we load the video
        // Load Preview Video
        for (var i=2; i< SelectedGamePreviewWindow.transform.childCount; i++)
            Destroy(SelectedGamePreviewWindow.transform.GetChild(i).gameObject);

        var preview = Instantiate(SelectedGame.PreviewClip);
        preview.transform.SetParent(SelectedGamePreviewWindow.transform, false);
    }
    void PopulateShipSelectionList()
    {
        // Deactivate All
        for (var i = 0; i < ShipSelectionGrid.childCount; i++)
            ShipSelectionGrid.GetChild(i).gameObject.SetActive(false);

        // Reactivate based on the number of ships
        for (var i = 0; i < SelectedGame.Pilots.Count; i++)
        {
            var selectionIndex = i;
            var ship = SelectedGame.Pilots[i].Ship;

            Debug.Log($"MiniGamesMenu - Populating Ship Select List: {ship.Name}");
            var shipSelection = ShipSelectionGrid.GetChild(i).gameObject;
            shipSelection.SetActive(true);
            shipSelection.GetComponent<Image>().sprite = ship.Icon;
            shipSelection.GetComponent<Button>().onClick.RemoveAllListeners();
            shipSelection.GetComponent<Button>().onClick.AddListener(() => SelectShip(selectionIndex));
            shipSelection.GetComponent<Button>().onClick.AddListener(() => ShipSelectionGrid.GetComponent<MenuAudio>().PlayAudio());
        }

        StartCoroutine(SelectShipCoroutine(0));
    }

    IEnumerator SelectGameCoroutine(int index)
    {
        yield return new WaitForEndOfFrame();
        SelectGame(index);
    }

    public void SelectGame(int index)
    {
        Debug.Log($"SelectGame: {index}");

        // Deselect them all
        //for (var i = 0; i < GameList.GameList.Count; i++)
        //    GameSelectionGrid.GetChild(i).gameObject.GetComponent<Image>().sprite = GameList.GameList[i].Icon;

        // Select the one
        SelectedGame = GameList.GameList[index];
        GameSelectionGrid.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedGame.SelectedIcon;

        Debug.Log($"SelectGame, PlayerCountButtonContainer.transform.childCount: {PlayerCountButtonContainer.transform.childCount}");

        // Setup player count and difficulty buttons

        // TODO: this is kludgy
        for (var i = 0; i < PlayerCountButtonContainer.transform.childCount; i++)
        {
            Debug.Log($"SelectGame - SelectedGame.MaxPlayers:{SelectedGame.MaxPlayers}, i:{i}, i < SelectedGame.MaxPlayers:{i < SelectedGame.MaxPlayers}");
            var playerCount = i + 1;
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.SetActive(i < SelectedGame.MaxPlayers);
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.RemoveAllListeners();
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => SetPlayerCount(playerCount));
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => PlayerCountButtonContainer.GetComponent<MenuAudio>().PlayAudio());

        }
        SetPlayerCount(1);

        // TODO: this is kludgy
        for (var i = 0; i < IntensityButtonContainer.transform.childCount; i++)
        {
            var intensity = i + 1;
            IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.RemoveAllListeners();
            IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => SetIntensity(intensity));
            IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => IntensityButtonContainer.GetComponent<MenuAudio>().PlayAudio());
        }
        SetIntensity(1);

        PopulateGameDetails();
        PopulateShipSelectionList();
        ShowGameDetailView();
    }

    IEnumerator SelectShipCoroutine(int index)
    {
        yield return new WaitForEndOfFrame();
        SelectShip(index);
    }

    public void SelectShip(int index)
    {
        Debug.Log($"SelectShip: {index}");
        Debug.Log($"ShipSelectionContainer.childCount: {ShipSelectionGrid.childCount}");
        Debug.Log($"Ships.Count: {SelectedGame.Pilots.Count}");

        // Deselect them all
        for (var i = 0; i < SelectedGame.Pilots.Count; i++)
            ShipSelectionGrid.GetChild(i).gameObject.GetComponent<Image>().sprite = SelectedGame.Pilots[i].Ship.Icon;

        // Select the one
        SelectedShip = SelectedGame.Pilots[index].Ship;
        ShipSelectionGrid.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedShip.SelectedIcon;

        // notify the mini game engine that this is the ship to play
        MiniGame.PlayerShipType = SelectedShip.Class;
        MiniGame.PlayerPilot = SelectedGame.Pilots[index];
    }

    public void SetPlayerCount(int playerCount)
    {
        Debug.Log($"SetPlayerCount: {playerCount}");

        for (var i = 0; i < PlayerCountButtonContainer.transform.childCount; i++)
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite = PlayerCountIcons[i];

        PlayerCountButtonContainer.transform.GetChild(playerCount - 1).gameObject.GetComponent<Image>().sprite = PlayerCountButtonContainer.transform.GetChild(playerCount - 1).gameObject.GetComponent<Button>().spriteState.selectedSprite;

        // notify the mini game engine that this is the number of players
        MiniGame.NumberOfPlayers = playerCount;
    }

    public void SetIntensity(int intensity)
    {
        Debug.Log($"ArcadeMenu - SetIntensity: {intensity}");

        for (var i = 0; i < IntensityButtonContainer.transform.childCount; i++)
            IntensityButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite = IntensityIcons[i];

        IntensityButtonContainer.transform.GetChild(intensity - 1).gameObject.GetComponent<Image>().sprite = IntensityButtonContainer.transform.GetChild(intensity - 1).gameObject.GetComponent<Button>().spriteState.selectedSprite;

        Hangar.Instance.SetAiDifficultyLevel(intensity);

        // notify the mini game engine that this is the difficulty
        MiniGame.IntensityLevel = intensity;
    }

    public void PlaySelectedGame()
    {
        SceneManager.LoadScene(SelectedGame.SceneName);
    }
}