using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MiniGamesMenu : MonoBehaviour
{
    [SerializeField] List<SO_MiniGame> Games;
    [SerializeField] List<SO_Ship> Ships;
    [SerializeField] TMPro.TMP_Text SelectedGameName;
    [SerializeField] TMPro.TMP_Text SelectedGameDescription;
    [SerializeField] GameObject SelectedGamePreviewWindow;
    [SerializeField] TMPro.TMP_Text SelectedGameName2;
    [SerializeField] TMPro.TMP_Text SelectedGameDescription2;
    [SerializeField] GameObject SelectedGamePreviewWindow2;
    [SerializeField] GameObject ShipSelectionTemplate;
    [SerializeField] GameObject GameSelectionTemplate;
    [SerializeField] GameObject PlayerCountButtonContainer;
    [SerializeField] GameObject DifficultyButtonContainer;

    Transform ShipSelectionContainer;
    Transform GameSelectionContainer;
    SO_Ship SelectedShip;
    SO_MiniGame SelectedGame;
    int PlayerCount;
    int DifficultyLevel;


    // Start is called before the first frame update
    void Start()
    {
        ShipSelectionContainer = ShipSelectionTemplate.transform.parent;
        GameSelectionContainer = GameSelectionTemplate.transform.parent;

        ShipSelectionTemplate.SetActive(false);
        GameSelectionTemplate.SetActive(false);

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
        
        // populate the games list with the one's games
        PopulateGameSelectionList();

        // TODO: need to notify the mini game engine that this is the ship to play (use the hangar?)
    }

    public void SelectGame(int index)
    {
        Debug.Log($"SelectGame: {index}");

        // Deselect them all
        for (var i = 0; i < SelectedShip.MiniGames.Count; i++)
            GameSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = SelectedShip.MiniGames[i].Icon;

        // Select the one
        SelectedGame = SelectedShip.MiniGames[index];
        GameSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedGame.SelectedIcon;

        // Setup player count and difficulty buttons
        for (var i = 0; i >= PlayerCountButtonContainer.transform.childCount; i++)
        {
            var playerCount = i;
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.SetActive(i < SelectedGame.MaxPlayers);
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.RemoveAllListeners();
            PlayerCountButtonContainer.transform.GetChild(i).gameObject.GetComponent<Button>().onClick.AddListener(() => SetPlayerCount(playerCount));
        }

        PopulateGameDetails();
    }

    public void PlaySelectedGame()
    {
        SceneManager.LoadScene(SelectedGame.SceneName);
    }

    public void SetPlayerCount(int playerCount)
    {
        PlayerCount = playerCount;

        // TODO: need to notify the mini game engine that this is the number of players (use the hangar?)
    }

    public void SetDifficulty(int difficulty)
    {
        DifficultyLevel = difficulty;

        // TODO: need to notify the mini game engine that this is the difficulty
    }

    void PopulateShipSelectionList()
    {
        // Destroy all but the template
        for (var i = 0; i < ShipSelectionContainer.childCount; i++)
            ShipSelectionContainer.GetChild(i).gameObject.SetActive(false);

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
        //SelectShip(0);
    }

    void PopulateGameSelectionList()
    {
        for (var i = 0; i < GameSelectionContainer.transform.childCount; i++)
            GameSelectionContainer.GetChild(i).gameObject.SetActive(false);
                //Destroy(GameSelectionContainer.transform.GetChild(i).gameObject);

        for (var i = 0; i < SelectedShip.MiniGames.Count; i++) {
            var selectionIndex = i;
            var game = SelectedShip.MiniGames[i];
            Debug.Log($"Populating Game Select List: {game.Name}");
            var gameSelection = GameSelectionContainer.GetChild(i).gameObject;//Instantiate(GameSelectionTemplate);
            gameSelection.SetActive(true);
            //gameSelection.transform.parent = GameSelectionContainer;
            gameSelection.GetComponent<Image>().sprite = game.Icon;
            gameSelection.GetComponent<Button>().onClick.RemoveAllListeners();
            gameSelection.GetComponent<Button>().onClick.AddListener(() => SelectGame(selectionIndex));
        }

        StartCoroutine(SelectGameCoroutine(0));
        //SelectGame(0);
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