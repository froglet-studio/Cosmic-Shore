using UnityEngine;
using TMPro;
using StarWriter.Core;
using static StarWriter.Core.GameSetting;
using System.Collections.Generic;
using StarWriter.Utility.Singleton;

public class ScoringManager : Singleton<ScoringManager>
{
    // TODO: remove deprecated 'extended life' stuff
    [SerializeField] int extendedLifeScore;
    [SerializeField] int extendedLifeHighScore;
    [SerializeField] GameObject WinnerDisplay;
    [SerializeField] List<GameObject> ScoreContainers;
    
    Dictionary<string, GameObject> ScoreDisplays = new Dictionary<string, GameObject>(); // TODO: not sure I like this
    Dictionary<string, int> PlayerScores = new Dictionary<string, int>();
    Dictionary<Team, int> TeamScores = new Dictionary<Team, int>();
    static int SinglePlayerScore = 0;
    static bool firstLife = true;
    static float bedazzleThresholdPercentage = 0.8f;

    private static bool newHighScore;
    private static bool firstLifeThresholdBeat;

    public TextMeshProUGUI scoreText;
    private bool RoundEnded = false;

    public bool FirstLife { get => firstLife; set => firstLife = value; }

    [SerializeField] public bool nodeGame = false;

    private void OnEnable()
    {
        GameManager.onPlayGame += ResetScoreAndDeathCount;
        GameManager.onDeath += UpdateScoresAndDeathCount;
        GameManager.onGameOver += UpdateScoresAndDeathCount;
        Crystal.AddToScore += UpdateScore;
        Trail.AddToScore += UpdateScore;
    }

    private void OnDisable()
    {
        GameManager.onPlayGame -= ResetScoreAndDeathCount;
        GameManager.onDeath -= UpdateScoresAndDeathCount;
        GameManager.onGameOver -= UpdateScoresAndDeathCount;
        Crystal.AddToScore -= UpdateScore;
        Trail.AddToScore -= UpdateScore;
    }

    private void Start()
    {
        // Initialize SinglePlayerScore panel to be blank
        foreach (var sc in ScoreContainers)
        {
            var playerName = sc.transform.GetChild(0).GetComponent<TMP_Text>();
            playerName.text = "";

            var playerScore = sc.transform.GetChild(1).GetComponent<TMP_Text>();
            playerScore.text = "";
        }

        WinnerDisplay.SetActive(false);
    }

    public void UpdateScoreBoard(int value)
    {
        Debug.Log($"UpdateScoreBoard - value:{value}");
        scoreText.text = value.ToString("D3"); // SinglePlayerScore text located on the fuel bar
    }

    public void UpdateScore(string uuid, int amount)
    {
        if (RoundEnded)
            return;

        if (!PlayerScores.ContainsKey(uuid))
        {
            PlayerScores.Add(uuid, 0);
            ScoreDisplays.Add(uuid, ScoreContainers[PlayerScores.Count - 1]);
            ScoreContainers[PlayerScores.Count - 1].transform.GetChild(0).GetComponent<TMP_Text>().text = uuid;
            ScoreContainers[PlayerScores.Count - 1].transform.GetChild(1).GetComponent<TMP_Text>().text = "000";
        }

        PlayerScores[uuid] += amount;
        ScoreDisplays[uuid].transform.GetChild(1).GetComponent<TMP_Text>().text = PlayerScores[uuid].ToString("D3");

        if (uuid == "admin")
        {
            SinglePlayerScore += amount;
        }

        foreach (var key in PlayerScores.Keys)
        {
            Debug.Log($"Scores: {key}, {PlayerScores[key]}");
        }

        UpdateScoreBoard(SinglePlayerScore);
    }

    private void ResetScoreAndDeathCount()
    {
        Debug.Log("ScoringManager.OnPlay");
        foreach (var key in PlayerScores.Keys)
            PlayerScores[key] = 0;

        WinnerDisplay.SetActive(false);

        SinglePlayerScore = 0;
        firstLife = true;
        newHighScore = false;
        firstLifeThresholdBeat = false;
        RoundEnded = false;
    }

    private void UpdateScoresAndDeathCount()
    {
        PlayerPrefs.SetInt(PlayerPrefKeys.score.ToString(), SinglePlayerScore);

        // Compares Score to High Score and saves the highest value
        if (PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString()) < SinglePlayerScore)
        {
            PlayerPrefs.SetInt(PlayerPrefKeys.highScore.ToString(), SinglePlayerScore);
            newHighScore = true;
        }

        if (firstLife)
        {
            if (PlayerPrefs.GetInt(PlayerPrefKeys.firstLifeHighScore.ToString()) * bedazzleThresholdPercentage <= SinglePlayerScore)
            {
                firstLifeThresholdBeat = true;
            }
            if (PlayerPrefs.GetInt(PlayerPrefKeys.firstLifeHighScore.ToString()) < SinglePlayerScore)
            {
                PlayerPrefs.SetInt(PlayerPrefKeys.firstLifeHighScore.ToString(), SinglePlayerScore);
            }
        }

        PlayerPrefs.Save();

        DisplayWinner();

        // TODO: duplicate bookkeeping happening here - introduce different game modes?
        firstLife = false;
        RoundEnded = true;
    }

    void DisplayWinner()
    {
        int winnersScore = 0;
        string winnersName = "";
        foreach (var key in PlayerScores.Keys)
        {
            if (PlayerScores[key] > winnersScore)
            {
                winnersScore = PlayerScores[key];
                winnersName = key;
            }
        }
        WinnerDisplay.transform.GetChild(0).GetComponent<TMP_Text>().text = winnersName;
        WinnerDisplay.transform.GetChild(1).GetComponent<TMP_Text>().text = winnersScore.ToString("D3");

        WinnerDisplay.SetActive(true);
    }

    public static bool IsScoreBedazzleWorthy
    {
        get => firstLife ?
            firstLifeThresholdBeat || (PlayerPrefs.GetInt(PlayerPrefKeys.firstLifeHighScore.ToString()) * bedazzleThresholdPercentage) <= SinglePlayerScore :
            newHighScore || (PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString())) < SinglePlayerScore;
    }

    public static bool IsAdBedazzleWorthy
    {
        get => PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString()) <= SinglePlayerScore;
    }

    public static bool IsShareBedazzleWorthy
    {
        get => PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString()) <= SinglePlayerScore;
    }
}