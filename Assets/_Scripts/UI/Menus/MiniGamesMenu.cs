using StarWriter.Core.HangerBuilder;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MiniGamesMenu : MonoBehaviour
{
    [SerializeField] List<SO_MiniGame> Games;
    [SerializeField] SO_ShipList ShipList;
    
    [SerializeField] TMPro.TMP_Text SelectedGameName;
    [SerializeField] TMPro.TMP_Text SelectedGameDescription;
    [SerializeField] GameObject SelectedGamePreviewWindow;
    [SerializeField] TMPro.TMP_Text SelectedGameName2;
    [SerializeField] TMPro.TMP_Text SelectedGameDescription2;
    [SerializeField] GameObject SelectedGamePreviewWindow2;
    [SerializeField] Transform ShipSelectionContainer;
    [SerializeField] Transform GameSelectionContainer;
    [SerializeField] GameObject PlayerCountButtonContainer;
    [SerializeField] GameObject DifficultyButtonContainer;


    List<SO_Ship> Ships;
    SO_Ship SelectedShip;
    SO_MiniGame SelectedGame;
    int PlayerCount;
    int DifficultyLevel;


    // Start is called before the first frame update
    void Start()
    {
        Ships = ShipList.ShipList;

        for (var i = 0; i < PlayerCountButtonContainer.transform.childCount; i++)
            PlayerCountIcons.Add(PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite);

        for (var i = 0; i < DifficultyButtonContainer.transform.childCount; i++)
            DifficultyIcons.Add(DifficultyButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite);

        PopulateShipSelectionList();
    }

    IEnumerator SelectShipCoroutine(int index)
    {
        yield return new WaitForEndOfFrame();
        SelectShip(index);
    }

    IEnumerator SelectGameCoroutine(int index)
    {
        yield return new WaitForEndOfFrame();
        SelectGame(index);
    }

    public void SelectShip(int index)
    {
        Debug.Log($"SelectShip: {index}");
        Debug.Log($"ShipSelectionContainer.childCount: {ShipSelectionContainer.childCount}");
        Debug.Log($"Ships.Count: {Ships.Count}");

        // Deselect them all
        for (var i = 0; i < Ships.Count; i++)
            ShipSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = Ships[i].Icon;

        // Select the one
        SelectedShip = Ships[index];
        ShipSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedShip.SelectedIcon;

        // notify the mini game engine that this is the ship to play
        MiniGame.PlayerShipType = SelectedShip.Class;

        // populate the games list with the one's games
        PopulateGameSelectionList();
    }

    List<Sprite> DifficultyIcons = new List<Sprite>();
    List<Sprite> PlayerCountIcons = new List<Sprite>();

    public void SelectGame(int index)
    {
        Debug.Log($"SelectGame: {index}");

        // Deselect them all
        for (var i = 0; i < SelectedShip.MiniGames.Count; i++)
            GameSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = SelectedShip.MiniGames[i].Icon;

        // Select the one
        SelectedGame = SelectedShip.MiniGames[index];
        GameSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedGame.SelectedIcon;

        Debug.Log($"SelectGame, PlayerCountButtonContainer.transform.childCount: {PlayerCountButtonContainer.transform.childCount}");

        // Setup player count and difficulty buttons

        // TODO: this is kludgy
        //PlayerCountButtonContainer.transform.GetChild(0).gameObject.GetComponent<Image>().sprite = PlayerCountButtonContainer.transform.GetChild(0).gameObject.GetComponent<Button>().spriteState.selectedSprite;
        for (var i = 0; i < PlayerCountButtonContainer.transform.childCount; i++)
        {
            Debug.Log($"SelectGame - SelectedGame.MaxPlayers:{SelectedGame.MaxPlayers}, i:{i}, i < SelectedGame.MaxPlayers:{i < SelectedGame.MaxPlayers}");
            var playerCount = i+1;
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.SetActive(i < SelectedGame.MaxPlayers);
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.RemoveAllListeners();
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => SetPlayerCount(playerCount));
        }
        SetPlayerCount(1);

        // TODO: this is kludgy
        //DifficultyButtonContainer.transform.GetChild(0).gameObject.GetComponent<Image>().sprite = DifficultyButtonContainer.transform.GetChild(0).gameObject.GetComponent<Button>().spriteState.selectedSprite;
        for (var i = 0; i < DifficultyButtonContainer.transform.childCount; i++)
        {
            var difficulty = i + 1;
            DifficultyButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.RemoveAllListeners();
            DifficultyButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => SetDifficulty(difficulty));
        }
        SetDifficulty(1);

        PopulateGameDetails();
    }

    public void PlaySelectedGame()
    {
        SceneManager.LoadScene(SelectedGame.SceneName);
    }

    public void SetPlayerCount(int playerCount)
    {
        Debug.Log($"SetPlayerCount: {playerCount}");
        PlayerCount = playerCount;

        for (var i = 0; i < PlayerCountButtonContainer.transform.childCount; i++)
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite = PlayerCountIcons[i];

        PlayerCountButtonContainer.transform.GetChild(playerCount - 1).gameObject.GetComponent<Image>().sprite = PlayerCountButtonContainer.transform.GetChild(playerCount - 1).gameObject.GetComponent<Button>().spriteState.selectedSprite;

        // notify the mini game engine that this is the number of players
        MiniGame.NumberOfPlayers = playerCount;
    }

    public void SetDifficulty(int difficulty)
    {
        Debug.Log($"SetDifficulty: {difficulty}");
        DifficultyLevel = difficulty;

        for (var i = 0; i < DifficultyButtonContainer.transform.childCount; i++)
            DifficultyButtonContainer.transform.GetChild(i).gameObject.GetComponent<Image>().sprite = DifficultyIcons[i];

        DifficultyButtonContainer.transform.GetChild(difficulty - 1).gameObject.GetComponent<Image>().sprite = DifficultyButtonContainer.transform.GetChild(difficulty - 1).gameObject.GetComponent<Button>().spriteState.selectedSprite;

        Hangar.Instance.SetAiDifficultyLevel(difficulty);

        // notify the mini game engine that this is the difficulty
        MiniGame.DifficultyLevel = difficulty;
    }

    void PopulateShipSelectionList()
    {
        // Deactivate All
        for (var i = 0; i < ShipSelectionContainer.childCount; i++)
            ShipSelectionContainer.GetChild(i).gameObject.SetActive(false);

        // Reactivate based on the number of ships
        for (var i = 0; i < Ships.Count; i++)
        {
            var selectionIndex = i;
            var ship = Ships[i];
            Debug.Log($"Populating Ship Select List: {ship.Name}");
            var shipSelection = ShipSelectionContainer.GetChild(i).gameObject;//Instantiate(ShipSelectionTemplate);
            shipSelection.SetActive(true);
            shipSelection.GetComponent<Image>().sprite = ship.Icon;
            shipSelection.GetComponent<Button>().onClick.RemoveAllListeners();
            shipSelection.GetComponent<Button>().onClick.AddListener(() => SelectShip(selectionIndex));
        }

        StartCoroutine(SelectShipCoroutine(0));
    }

    void PopulateGameSelectionList()
    {
        // Deactivate All
        for (var i = 0; i < GameSelectionContainer.transform.childCount; i++)
            GameSelectionContainer.GetChild(i).gameObject.SetActive(false);

        // Reactivate based on the number of games for the given ship
        for (var i = 0; i < SelectedShip.MiniGames.Count; i++) {
            var selectionIndex = i;
            var game = SelectedShip.MiniGames[i];
            Debug.Log($"Populating Game Select List: {game.Name}");
            var gameSelection = GameSelectionContainer.GetChild(i).gameObject;
            gameSelection.SetActive(true);
            gameSelection.GetComponent<Image>().sprite = game.Icon;
            gameSelection.GetComponent<Button>().onClick.RemoveAllListeners();
            gameSelection.GetComponent<Button>().onClick.AddListener(() => SelectGame(selectionIndex));
        }

        StartCoroutine(SelectGameCoroutine(0));
    }

    void PopulateGameDetails()
    {
        Debug.Log($"Populating Game Details List: {SelectedGame.Name}");
        Debug.Log($"Populating Game Details List: {SelectedGame.Description}");
        Debug.Log($"Populating Game Details List: {SelectedGame.Icon}");
        Debug.Log($"Populating Game Details List: {SelectedGame.PreviewClip}");

        SelectedGameName.text = SelectedGame.Name;
        SelectedGameDescription.text= SelectedGame.Description;
        
        for (var i=2; i< SelectedGamePreviewWindow.transform.childCount; i++)
            Destroy(SelectedGamePreviewWindow.transform.GetChild(i).gameObject);

        var preview = Instantiate(SelectedGame.PreviewClip);
        preview.transform.SetParent(SelectedGamePreviewWindow.transform, false);

        SelectedGameName2.text = SelectedGame.Name;
        SelectedGameDescription2.text = SelectedGame.Description;

        for (var i = 2; i < SelectedGamePreviewWindow2.transform.childCount; i++)
            Destroy(SelectedGamePreviewWindow2.transform.GetChild(i).gameObject);

        preview = Instantiate(SelectedGame.PreviewClip);
        preview.transform.SetParent(SelectedGamePreviewWindow2.transform, false);
    }
}