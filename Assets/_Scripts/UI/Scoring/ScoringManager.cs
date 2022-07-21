using UnityEngine;
using TMPro;
using StarWriter.Core;

public class ScoringManager : MonoBehaviour
{
    [SerializeField]
    int score = 0;
    [SerializeField]
    private int extendedLifeScore;
    [SerializeField]
    private int extendedLifeHighScore;
    [SerializeField]
    private float extralifeModifier = 0.8f;
    [SerializeField]
    bool bedazzled; //wether or not the watchAdButton is amped up
    [SerializeField]
    private bool firstLife = true;

    public TextMeshProUGUI scoreText;

    public bool FirstLife { get => firstLife; set => firstLife = value; }

    public delegate void OnGameOverPreEvent();
    public static event OnGameOverPreEvent onGameOverPre;

    public delegate void OnGameOverEvent(bool bedazzled, bool advertisement);
    public static event OnGameOverEvent onGameOver;

    private void OnEnable()
    {
        GameManager.onDeath += OnDeath;
        GameManager.onExtendGamePlay += ExtendGamePlay;
        MutonPopUp.AddToScore += AddMutonBous;
    }

    private void OnDisable()
    {
        GameManager.onDeath -= OnDeath;
        MutonPopUp.AddToScore -= AddMutonBous;
        GameManager.onExtendGamePlay -= ExtendGamePlay;
    }

    public void UpdateScoreBoard(int value)
    {
        scoreText.text = value.ToString("D3"); // score text located on the fuel bar
    }

    private void AddMutonBous(string uuid, int amount)
    {
        if (uuid == "admin") { score += amount; }

        UpdateScoreBoard(score);
    }

    private void OnDeath()
    {
        bool advertisements = (PlayerPrefs.GetInt(GameSetting.PlayerPrefKeys.adsEnabled.ToString()) == 1);
        if (advertisements)
        {
            bedazzled = ((PlayerPrefs.GetInt("Single Life High Score") * extralifeModifier) <= score);  //Sets beddazed value
            onGameOverPre?.Invoke();
            onGameOver?.Invoke(bedazzled, advertisements); //send (true, true || false)
        }
        else
        {
            bedazzled = ((PlayerPrefs.GetInt("High Score")) <= score);
            onGameOverPre?.Invoke();
            onGameOver?.Invoke(bedazzled, advertisements); //send (true || false, false)
        }
        UpdatePlayerPrefScores();
    }

    public void UpdatePlayerPrefScores()
    {
        PlayerPrefs.SetInt("Score", score);

        //Compares Score to High Score and saves the highest value
        if (PlayerPrefs.GetInt("High Score") < score)
        {
            PlayerPrefs.SetInt("High Score", score);
        }
        if (firstLife)
        {
            if (PlayerPrefs.GetInt("Single Life High Score") < score)
            {
                PlayerPrefs.SetInt("Single LifeHigh Score", score);
            }
        }
        PlayerPrefs.Save();
    }

    private void ExtendGamePlay()
    {
        firstLife = false;
        PlayerPrefs.SetInt(GameSetting.PlayerPrefKeys.adsEnabled.ToString(), 0);  // set false 
        PlayerPrefs.Save();
    }
}