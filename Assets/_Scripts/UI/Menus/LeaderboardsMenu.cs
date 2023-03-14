using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LeaderboardsMenu : MonoBehaviour
{
    Dictionary<MiniGames, List<LeaderboardEntry>> LeaderboardEntries = new() 
    {
        { 
            MiniGames.Darts, new List<LeaderboardEntry>()
            { 
                new LeaderboardEntry("foo", 10, ShipTypes.Manta),
                new LeaderboardEntry("bar", 75, ShipTypes.Dolphin),
                new LeaderboardEntry("bar", 50, ShipTypes.Dolphin),
                new LeaderboardEntry("ass", 100, ShipTypes.Shark) 
            }  
        },
        { 
            MiniGames.DestructionDerby, new List<LeaderboardEntry>() 
            {
                new LeaderboardEntry("qux", 10, ShipTypes.Manta),
                new LeaderboardEntry("foo", 75, ShipTypes.Shark),
                new LeaderboardEntry("foo", 50, ShipTypes.Shark),
                new LeaderboardEntry("ass", 100, ShipTypes.Manta)
            }
        },
        { 
            MiniGames.FlightSchool, new List<LeaderboardEntry>() 
            {
                new LeaderboardEntry("baz", 20, ShipTypes.Dolphin),
                new LeaderboardEntry("qux", 75, ShipTypes.GunManta),
                new LeaderboardEntry("bar", 50, ShipTypes.Manta),
                new LeaderboardEntry("ass", 100, ShipTypes.Shark)
            }  
        },
    };

    [SerializeField] List<SO_MiniGame> Games;
    [SerializeField] Transform GameSelectionContainer;
    [SerializeField] GameObject HighScoresContainer;

    SO_MiniGame SelectedGame;

    // Start is called before the first frame update
    void Start()
    {
        LeaderboardEntries = LeaderboardDataAccessor.Load();
        PopulateGameSelectionList();
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
        }

        StartCoroutine(SelectGameCoroutine(0));
    }

    void PopulateGameHighScores()
    {
        Debug.Log($"PopulateGameHighScores: {SelectedGame.Name}");
        Debug.Log($"PopulateGameHighScores: {SelectedGame.Description}");

        var highScores = LeaderboardEntries[SelectedGame.Mode];
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