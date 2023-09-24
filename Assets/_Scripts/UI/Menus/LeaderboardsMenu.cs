using _Scripts._Core.Playfab_Models;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static LeaderboardManager;

[RequireComponent(typeof(MenuAudio))]
public class LeaderboardsMenu : MonoBehaviour
{
    List<LeaderboardEntryV2> LeaderboardEntriesV2;

    [SerializeField] SO_GameList GameList;
    [SerializeField] Transform GameSelectionContainer;
    [SerializeField] GameObject HighScoresContainer;
    [SerializeField] TMP_Dropdown ShipClassSelection;

    List<SO_ArcadeGame> Games = new();
    SO_ArcadeGame SelectedGame;
    MiniGames SelectedGameMode = MiniGames.BlockBandit;
    ShipTypes SelectedShipType = ShipTypes.Any;

    // Start is called before the first frame update
    void Start()
    {
        // TODO: Reconsider this implementation for avoiding displaying Freestyle on the scoreboard
        // Copy the game list, but skip Freestyle -- IMPORTANT to copy the list so we don't modify the SO
        foreach (var game in GameList.GameList)
            if (game.Mode != MiniGames.Freestyle)
                Games.Add(game);

        AuthenticationManager.OnProfileLoaded += FetchLeaderboard;

        ShipClassSelection.onValueChanged.AddListener(SelectShipType);

        PopulateGameSelectionList();
    }

    void FetchLeaderboard()
    {
        LeaderboardManager.Instance.FetchLeaderboard(
            LeaderboardManager.Instance.GetGameplayStatKey(SelectedGameMode, SelectedShipType),
            new() { { "Intensity", "1" } },
            OnFetchLeaderboard);
    }

    void OnFetchLeaderboard(List<LeaderboardEntryV2> results)
    {
        Debug.Log("OnFetchLeaderboard");
        foreach (var result in results)
        {
            Debug.Log($"Leaderboard Result - {result.Position} | {result.DisplayName} | {result.Score}");
        }

        LeaderboardEntriesV2 = results;
        PopulateGameHighScores();
    }

    IEnumerator SelectGameCoroutine(int index)
    {
        yield return new WaitForEndOfFrame();
        SelectGame(index);
    }

    void PopulateShipClassSelectionDropdown()
    {
        var options = new List<TMP_Dropdown.OptionData>();

        // Only add "Any" selection if there is more than one vessel class available
        if (SelectedGame.Pilots.Count > 1)
            options.Add(new TMP_Dropdown.OptionData("Any"));
        
        foreach (var pilot in SelectedGame.Pilots)
            options.Add(new TMP_Dropdown.OptionData(pilot.Ship.Class.ToString()));

        ShipClassSelection.options = options;
        ShipClassSelection.value = 0;
        SelectShipType(0);
    }

    public void SelectShipType(int optionValue)
    {
        var shiptypeName = ShipClassSelection.options[optionValue].text;
        SelectedShipType = Enum.Parse<ShipTypes>(shiptypeName);

        FetchLeaderboard();
    }

    public void SelectGame(int index)
    {
        Debug.Log($"SelectGame: {index}");

        // Deselect them all
        for (var i = 0; i < Games.Count; i++)
            GameSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = Games[i].Icon;

        // Select the one
        SelectedGame = Games[index];
        SelectedGameMode = SelectedGame.Mode;
        GameSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedGame.SelectedIcon;
        PopulateShipClassSelectionDropdown();
    }

    void PopulateGameSelectionList()
    {
        // Deactivate All
        for (var i = 0; i < GameSelectionContainer.transform.childCount; i++)
            GameSelectionContainer.GetChild(i).gameObject.SetActive(false);

        // Reactivate based on the number of games for the given ship
        for (var i = 0; i < Games.Count; i++) {
            var selectionIndex = i;
            var game = Games[i];
            Debug.Log($"Populating Game Select List: {game.Name}");
            var gameSelection = GameSelectionContainer.GetChild(i).gameObject;
            gameSelection.SetActive(true);
            gameSelection.GetComponent<Image>().sprite = game.Icon;
            gameSelection.GetComponent<Button>().onClick.RemoveAllListeners();
            gameSelection.GetComponent<Button>().onClick.AddListener(() => SelectGame(selectionIndex));
            gameSelection.GetComponent<Button>().onClick.AddListener(() => GetComponent<MenuAudio>().PlayAudio());
        }

        StartCoroutine(SelectGameCoroutine(0));
    }

    void PopulateGameHighScores()
    {
        Debug.Log($"PopulateGameHighScores: {SelectedGame.Name}");
        
        for (var i = 0; i < HighScoresContainer.transform.childCount; i++)
            HighScoresContainer.transform.GetChild(i).gameObject.SetActive(false);

        for (var i = 0; i < LeaderboardEntriesV2.Count; i++)
        {
            var score = LeaderboardEntriesV2[i];
            HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().text = (score.Position+1).ToString();
            HighScoresContainer.transform.GetChild(i).GetChild(1).GetComponent<TMP_Text>().text = score.DisplayName;
            HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().text = score.Score.ToString();
            HighScoresContainer.transform.GetChild(i).gameObject.SetActive(true);
        }
    }
}