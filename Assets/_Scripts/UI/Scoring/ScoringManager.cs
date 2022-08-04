using UnityEngine;
using TMPro;
using StarWriter.Core;
using static StarWriter.Core.GameSetting;

public class ScoringManager : MonoBehaviour
{
    
    [SerializeField] int extendedLifeScore;
    [SerializeField] int extendedLifeHighScore;

    static int score = 0;
    static bool firstLife = true;
    static float bedazzleThresholdPercentage = 0.8f;

    public TextMeshProUGUI scoreText;

    public bool FirstLife { get => firstLife; set => firstLife = value; }

    private void OnEnable()
    {
        GameManager.onPlayGame += OnPlay;
        GameManager.onDeath += OnDeath;
        GameManager.onExtendGamePlay += ExtendGamePlay;
        MutonPopUp.AddToScore += AddMutonBonus;
    }

    private void OnDisable()
    {
        GameManager.onPlayGame -= OnPlay;
        GameManager.onDeath -= OnDeath;
        GameManager.onExtendGamePlay -= ExtendGamePlay;
        MutonPopUp.AddToScore -= AddMutonBonus;
    }

    public void UpdateScoreBoard(int value)
    {
        scoreText.text = value.ToString("D3"); // score text located on the fuel bar
    }

    private void AddMutonBonus(string uuid, int amount)
    {
        if (uuid == "admin") { score += amount; }

        UpdateScoreBoard(score);
    }

    private void OnDeath()
    {
        UpdatePlayerPrefScores();
    }

    private void OnPlay()
    {
        Debug.Log("ScoringManager.OnPlay");
        score = 0;
        firstLife = true;
    }

    public static bool IsScoreBedazzleWorthy
    {
        get => firstLife ?
            (PlayerPrefs.GetInt(PlayerPrefKeys.firstLifeHighScore.ToString()) * bedazzleThresholdPercentage) <= score :
            (PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString())) <= score;
    }

    public void UpdatePlayerPrefScores()
    {
        PlayerPrefs.SetInt(PlayerPrefKeys.score.ToString(), score);

        // Compares Score to High Score and saves the highest value
        if (PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString()) < score)
        {
            PlayerPrefs.SetInt(PlayerPrefKeys.highScore.ToString(), score);
        }
        
        if (firstLife)
        {
            if (PlayerPrefs.GetInt(PlayerPrefKeys.firstLifeHighScore.ToString()) < score)
            {
                PlayerPrefs.SetInt(PlayerPrefKeys.firstLifeHighScore.ToString(), score);
            }
        }

        PlayerPrefs.Save();
    }

    private void ExtendGamePlay()
    {
        Debug.Log("ScoringManager.ExtendGamePlay");
        firstLife = false;
    }
}