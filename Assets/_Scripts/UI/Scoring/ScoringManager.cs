using UnityEngine;
using TMPro;
using StarWriter.Core;
using static StarWriter.Core.GameSetting;
using System.Collections.Generic;

public class ScoringManager : MonoBehaviour
{
    [SerializeField] int extendedLifeScore;
    [SerializeField] int extendedLifeHighScore;
    [SerializeField] List<GameObject> ScoreContainers;
    //[SerializeField] float ScoreVerticalSpacing = 57.4f; //TODO: for dynamic scoring layout

    Dictionary<string, GameObject> ScoreDisplays = new Dictionary<string, GameObject>(); // TODO: not sure I like this
    Dictionary<string, int> Scores = new Dictionary<string, int>();
    static int score = 0;
    static bool firstLife = true;
    static float bedazzleThresholdPercentage = 0.8f;

    private static bool newHighScore;
    private static bool firstLifeThresholdBeat;

    public TextMeshProUGUI scoreText;

    public bool FirstLife { get => firstLife; set => firstLife = value; }

    private void OnEnable()
    {
        GameManager.onPlayGame += ResetScoreAndDeathCount;
        GameManager.onDeath += UpdateScoresAndDeathCount;
        GameManager.onGameOver += UpdateScoresAndDeathCount;
        MutonPopUp.AddToScore += UpdateScore;
        FlowCrystal.AddToScore += UpdateScore;
        Trail.AddToScore += UpdateScore;
    }

    private void OnDisable()
    {
        GameManager.onPlayGame -= ResetScoreAndDeathCount;
        GameManager.onDeath -= UpdateScoresAndDeathCount;
        GameManager.onGameOver -= UpdateScoresAndDeathCount;
        MutonPopUp.AddToScore -= UpdateScore;
        FlowCrystal.AddToScore -= UpdateScore;
        Trail.AddToScore -= UpdateScore;
    }

    private void Start()
    {
        // Initialize score panel to be blank
        foreach (var sc in ScoreContainers)
        {
            var playerName = sc.transform.GetChild(0).GetComponent<TMP_Text>();
            playerName.text = "";

            var playerScore = sc.transform.GetChild(1).GetComponent<TMP_Text>();
            playerScore.text = "";
        }
    }

    public void UpdateScoreBoard(int value)
    {
        Debug.Log($"UpdateScoreBoard - value:{value}");
        scoreText.text = value.ToString("D3"); // score text located on the fuel bar
    }

    private void UpdateScore(string uuid, int amount)
    {
        if (!Scores.ContainsKey(uuid))
        {
            Scores.Add(uuid, 0);
            ScoreDisplays.Add(uuid, ScoreContainers[Scores.Count - 1]);
            ScoreContainers[Scores.Count - 1].transform.GetChild(0).GetComponent<TMP_Text>().text = uuid;
            ScoreContainers[Scores.Count - 1].transform.GetChild(1).GetComponent<TMP_Text>().text = "000";
        }

        Scores[uuid] += amount;
        ScoreDisplays[uuid].transform.GetChild(1).GetComponent<TMP_Text>().text = Scores[uuid].ToString("D3");

        if (uuid == "admin")
        {
            score += amount;
        }

        foreach (var key in Scores.Keys)
        {
            Debug.Log($"Scores: {key}, {Scores[key]}");
        }

        UpdateScoreBoard(score);
    }

    private void ResetScoreAndDeathCount()
    {
        Debug.Log("ScoringManager.OnPlay");
        foreach (var key in Scores.Keys)
            Scores[key] = 0;

        score = 0;
        firstLife = true;
        newHighScore = false;
        firstLifeThresholdBeat = false;
    }

    private void UpdateScoresAndDeathCount()
    {
        PlayerPrefs.SetInt(PlayerPrefKeys.score.ToString(), score);

        // Compares Score to High Score and saves the highest value
        if (PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString()) < score)
        {
            PlayerPrefs.SetInt(PlayerPrefKeys.highScore.ToString(), score);
            newHighScore = true;
        }

        if (firstLife)
        {
            if (PlayerPrefs.GetInt(PlayerPrefKeys.firstLifeHighScore.ToString()) * bedazzleThresholdPercentage <= score)
            {
                firstLifeThresholdBeat = true;
            }
            if (PlayerPrefs.GetInt(PlayerPrefKeys.firstLifeHighScore.ToString()) < score)
            {
                PlayerPrefs.SetInt(PlayerPrefKeys.firstLifeHighScore.ToString(), score);
            }
        }

        PlayerPrefs.Save();

        firstLife = false;
    }

    public static bool IsScoreBedazzleWorthy
    {
        get => firstLife ?
            firstLifeThresholdBeat || (PlayerPrefs.GetInt(PlayerPrefKeys.firstLifeHighScore.ToString()) * bedazzleThresholdPercentage) <= score :
            newHighScore || (PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString())) < score;
    }

    public static bool IsAdBedazzleWorthy
    {
        get => PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString()) <= score;
    }

    public static bool IsShareBedazzleWorthy
    {
        get => PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString()) <= score;
    }
}