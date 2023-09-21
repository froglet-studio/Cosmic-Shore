using PlayFab.ClientModels;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static LeaderboardManager;

[RequireComponent(typeof(MenuAudio))]
public class LeaderboardsMenu : MonoBehaviour
{
    Dictionary<MiniGames, List<LeaderboardEntry>> LeaderboardEntries;
    List<LeaderboardEntryV2> LeaderboardEntriesV2;

    [SerializeField] bool UsePlayfab;
    [SerializeField] SO_GameList GameList;
    [SerializeField] Transform GameSelectionContainer;
    [SerializeField] GameObject HighScoresContainer;

    List<SO_ArcadeGame> Games = new();
    SO_ArcadeGame SelectedGame;
    ShipTypes SelectedShipType = ShipTypes.Any;

    // Start is called before the first frame update
    void Start()
    {
        // TODO: Reconsider this implementation for avoiding displaying Freestyle on the scoreboard
        // Copy the game list, but skip Freestyle -- IMPORTANT to copy the list so we don't modify the SO
        foreach (var game in GameList.GameList)
            if (game.Mode != MiniGames.Freestyle)
                Games.Add(game);

        if (!UsePlayfab)
            LeaderboardEntries = LeaderboardDataAccessor.Load();

        if (UsePlayfab)
        {
            //AccountManager.onLoginSuccess += TestFetch;
        }

        PopulateGameSelectionList();
    }

    void TestFetch(LoginResult loginResult)
    {
        LeaderboardManager.Instance.FetchLeaderboard(LeaderboardManager.Instance.GetGameplayStatKey(MiniGames.BlockBandit, ShipTypes.Manta), OnFetchLeaderboard);

        LeaderboardManager.Instance.FetchLeaderboard(
            LeaderboardManager.Instance.GetGameplayStatKey(MiniGames.BlockBandit, ShipTypes.Manta),
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
    }

    IEnumerator SelectGameCoroutine(int index)
    {
        yield return new WaitForEndOfFrame();
        SelectGame(index);
    }

    public void SelectShipType(int shipType)
    {

    }

    public void SelectGame(int index)
    {
        Debug.Log($"SelectGame: {index}");

        // Deselect them all
        for (var i = 0; i < Games.Count; i++)
            GameSelectionContainer.GetChild(i).gameObject.GetComponent<Image>().sprite = Games[i].Icon;

        // Select the one
        SelectedGame = Games[index];
        GameSelectionContainer.GetChild(index).gameObject.GetComponent<Image>().sprite = SelectedGame.SelectedIcon;
        PopulateGameHighScores();
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

        List<LeaderboardEntry> highScores;
        if (UsePlayfab)
        {
            highScores = LeaderboardEntries[SelectedGame.Mode];
            
            //highScores = LeaderboardManager.
        }
        else
        {
            if (!LeaderboardEntries.ContainsKey(SelectedGame.Mode))
            {
                highScores = LeaderboardDataAccessor.LeaderboardEntriesDefault[SelectedGame.Mode];
                LeaderboardDataAccessor.Save(SelectedGame.Mode, highScores);
            }
            else
                highScores = LeaderboardEntries[SelectedGame.Mode];
        }
        
        // TODO: need reverse sort for golf mode
        highScores.Sort( (score1,score2) => score2.Score.CompareTo(score1.Score));

        for (var i = 0; i < HighScoresContainer.transform.childCount; i++)
            HighScoresContainer.transform.GetChild(i).gameObject.SetActive(false);

        for (var i = 0; i < highScores.Count; i++)
        {
            var score = highScores[i];
            HighScoresContainer.transform.GetChild(i).GetChild(0).GetComponent<TMP_Text>().text = score.PlayerName;
            HighScoresContainer.transform.GetChild(i).GetChild(1).GetComponent<TMP_Text>().text = score.Score.ToString();
            HighScoresContainer.transform.GetChild(i).GetChild(2).GetComponent<TMP_Text>().text = score.ShipType.ToString();
            HighScoresContainer.transform.GetChild(i).gameObject.SetActive(true);
        }
    }
}