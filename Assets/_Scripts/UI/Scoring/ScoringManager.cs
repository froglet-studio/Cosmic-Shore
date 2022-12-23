using UnityEngine;
using TMPro;
using StarWriter.Core;
using static StarWriter.Core.GameSetting;
using System.Collections.Generic;
using StarWriter.Utility.Singleton;


public struct RoundStats
{
    public int blocksCreated;
    public int blocksDestroyed;
    public float volumeCreated;
    public float volumeDestroyed;
    public int crystalsCollected;

    public RoundStats(bool dummy = false)
    {
        blocksCreated = 0;
        blocksDestroyed = 0;
        volumeCreated = 0;
        volumeDestroyed = 0;
        crystalsCollected = 0;
    }
}

public class ScoringManager : Singleton<ScoringManager>
{
    [SerializeField] int extendedLifeScore;
    [SerializeField] int extendedLifeHighScore;
    [SerializeField] GameObject WinnerDisplay;
    [SerializeField] List<GameObject> ScoreContainers;
    [SerializeField] List<GameObject> PlayerVolumeContainers;
    [SerializeField] bool TeamScoresEnabled = false;

    // Stats Tracking
    Dictionary<Teams, RoundStats> teamStats = new Dictionary<Teams, RoundStats>();
    Dictionary<string, RoundStats> playerStats = new Dictionary<string, RoundStats>();

    Dictionary<string, GameObject> ScoreDisplays = new Dictionary<string, GameObject>(); // TODO: not sure I like this
    Dictionary<string, float> PlayerScores = new Dictionary<string, float>();
    Dictionary<Teams, float> TeamScores = new Dictionary<Teams, float>();
    static float SinglePlayerScore = 0;
    static bool firstLife = true;
    static float bedazzleThresholdPercentage = 0.8f;

    private static bool newHighScore;
    private static bool firstLifeThresholdBeat;

    public TextMeshProUGUI scoreText;
    private bool RoundEnded = false;

    public bool FirstLife { get => firstLife; set => firstLife = value; }

    [SerializeField] public bool nodeGame = false;

    void maybeCreateDictionaryEntries(Teams team, string playerName)
    {
        if (!teamStats.ContainsKey(team))
            teamStats.Add(team, new RoundStats());

        if (!playerStats.ContainsKey(playerName))
            playerStats.Add(playerName, new RoundStats());
    }
    public void CrystalCollected(Ship ship, CrystalProperties crystalProperties)
    {
        maybeCreateDictionaryEntries(ship.Team, ship.Player.PlayerName);

        var roundStats = teamStats[ship.Team];
        roundStats.crystalsCollected++;
        teamStats[ship.Team] = roundStats;

        roundStats = playerStats[ship.Player.PlayerName];
        roundStats.crystalsCollected++;
        playerStats[ship.Player.PlayerName] = roundStats;
    }

    public void BlockCreated(Teams team, string playerName, TrailBlockProperties trailBlockProperties)
    {
        maybeCreateDictionaryEntries(team, playerName);

        var roundStats = teamStats[team];
        roundStats.blocksCreated++;
        roundStats.volumeCreated += trailBlockProperties.volume;
        teamStats[team] = roundStats;

        roundStats = playerStats[playerName];
        roundStats.blocksCreated++;
        roundStats.volumeCreated += trailBlockProperties.volume;
        playerStats[playerName] = roundStats;
    }

    public void BlockDestroyed(Teams team, string playerName, TrailBlockProperties trailBlockProperties)
    {
        maybeCreateDictionaryEntries(team, playerName);

        var roundStats = teamStats[team];
        roundStats.blocksDestroyed++;
        roundStats.volumeDestroyed += trailBlockProperties.volume;
        teamStats[team] = roundStats;

        roundStats = playerStats[playerName];
        roundStats.blocksDestroyed++;
        roundStats.volumeDestroyed += trailBlockProperties.volume;
        playerStats[playerName] = roundStats;
    }


    private void OnEnable()
    {
        GameManager.onPlayGame += ResetScoreAndDeathCount;
        GameManager.onDeath += UpdateScoresAndDeathCount;
        GameManager.onGameOver += UpdateScoresAndDeathCount;
        if (!TeamScoresEnabled)
        {
            Trail.AddToScore += UpdateScore;
        }
    }

    private void OnDisable()
    {
        GameManager.onPlayGame -= ResetScoreAndDeathCount;
        GameManager.onDeath -= UpdateScoresAndDeathCount;
        GameManager.onGameOver -= UpdateScoresAndDeathCount;
        if (!TeamScoresEnabled)
        {
            Trail.AddToScore -= UpdateScore;
        }
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

    public void UpdateScoreBoard(float value)
    {
        Debug.Log($"UpdateScoreBoard - value:{value}");
        scoreText.text = ((int)value).ToString("D3"); // SinglePlayerScore text located on the fuel bar
    }

    public void UpdateTeamScore(Teams team, float amount)
    {
        if (RoundEnded)
            return;

        if (!TeamScores.ContainsKey(team))
        {
            TeamScores.Add(team, 0);
            ScoreDisplays.Add(team.ToString(), ScoreContainers[TeamScores.Count - 1]);
            ScoreContainers[TeamScores.Count - 1].transform.GetChild(0).GetComponent<TMP_Text>().text = team.ToString();
            ScoreContainers[TeamScores.Count - 1].transform.GetChild(1).GetComponent<TMP_Text>().text = "000";
        }

        TeamScores[team] += amount;
        ScoreDisplays[team.ToString()].transform.GetChild(1).GetComponent<TMP_Text>().text = ((int)TeamScores[team]).ToString("D3");
    }

    public void UpdateScore(string playerName, float amount)
    {
        if (RoundEnded)
            return;

        if (!PlayerScores.ContainsKey(playerName))
        {
            Debug.LogWarning($"Player UUID: {playerName}");
            Debug.LogWarning($"PlayerScores Count: {PlayerScores.Count}");
            Debug.LogWarning($"ScoreContainers Count: {ScoreContainers.Count}");
            PlayerScores.Add(playerName, 0);
            ScoreDisplays.Add(playerName, ScoreContainers[PlayerScores.Count - 1]);
            ScoreContainers[PlayerScores.Count - 1].transform.GetChild(0).GetComponent<TMP_Text>().text = playerName;
            ScoreContainers[PlayerScores.Count - 1].transform.GetChild(1).GetComponent<TMP_Text>().text = "000";
        }

        PlayerScores[playerName] += amount;
        ScoreDisplays[playerName].transform.GetChild(1).GetComponent<TMP_Text>().text = ((int)PlayerScores[playerName]).ToString("D3");

        if (playerName == "admin")
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
        foreach (var key in TeamScores.Keys)
            TeamScores[key] = 0;

        WinnerDisplay.SetActive(false);

        SinglePlayerScore = 0;
        firstLife = true;
        newHighScore = false;
        firstLifeThresholdBeat = false;
        RoundEnded = false;
    }

    private void UpdateScoresAndDeathCount()
    {
        PlayerPrefs.SetInt(PlayerPrefKeys.score.ToString(), (int)SinglePlayerScore);

        // Compares Score to High Score and saves the highest value
        if (PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString()) < SinglePlayerScore)
        {
            PlayerPrefs.SetInt(PlayerPrefKeys.highScore.ToString(), (int)SinglePlayerScore);
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
                PlayerPrefs.SetInt(PlayerPrefKeys.firstLifeHighScore.ToString(), (int)SinglePlayerScore);
            }
        }

        PlayerPrefs.Save();

        // TODO: Cleanup
        if (TeamScoresEnabled)
            //DisplayWinningTeam();
            DisplayPlayerScores();
        else
            //DisplayWinner();
            DisplayPlayerScores();

        // TODO: duplicate bookkeeping happening here - introduce different game modes?
        firstLife = false;
        RoundEnded = true;
    }

    void OutputRoundStats()
    {
        foreach (var team in teamStats.Keys)
        {
            Debug.LogWarning($"Team Stats - Team:{team}, Crystals Collected: {teamStats[team].crystalsCollected} ");
            Debug.LogWarning($"Team Stats - Team:{team}, Blocks Created: {teamStats[team].blocksCreated} ");
            Debug.LogWarning($"Team Stats - Team:{team}, Blocks Destroyed: {teamStats[team].blocksDestroyed} ");
            Debug.LogWarning($"Team Stats - Team:{team}, Volume Created: {teamStats[team].volumeCreated} ");
            Debug.LogWarning($"Team Stats - Team:{team}, Volume Destroyed: {teamStats[team].volumeDestroyed} ");
        }

        foreach (var player in playerStats.Keys)
        {
            Debug.LogWarning($"PlayerStats - Player:{player}, Crystals Collected: {playerStats[player].crystalsCollected} ");
            Debug.LogWarning($"PlayerStats - Player:{player}, Blocks Created: {playerStats[player].blocksCreated} ");
            Debug.LogWarning($"PlayerStats - Player:{player}, Blocks Destroyed: {playerStats[player].blocksDestroyed} ");
            Debug.LogWarning($"PlayerStats - Player:{player}, Volume Created: {playerStats[player].volumeCreated} ");
            Debug.LogWarning($"PlayerStats - Player:{player}, Volume Destroyed: {playerStats[player].volumeDestroyed} ");
        }
    }

    void DisplayPlayerScores()
    {
        OutputRoundStats();

        float MVPScore = 0;
        string MVPName = "";
        int i = 0;
        foreach (var key in PlayerScores.Keys)
        {
            if (PlayerScores[key] > MVPScore)
            {
                MVPScore = PlayerScores[key];
                MVPName = key;
            }

            var volumeContainer = PlayerVolumeContainers[i];
            
            volumeContainer.transform.GetChild(0).GetComponent<TMP_Text>().text = key;
            volumeContainer.transform.GetChild(1).GetComponent<TMP_Text>().text = ((int)PlayerScores[key]).ToString("D3");
            volumeContainer.SetActive(true);
        }
    }

    void DisplayWinningTeam()
    {
        int winnersScore = 0;
        string winnersName = "";
        foreach (var key in TeamScores.Keys)
        {
            if (TeamScores[key] > winnersScore)
            {
                winnersScore = (int)TeamScores[key];
                winnersName = key.ToString();
            }
        }
        WinnerDisplay.transform.GetChild(0).GetComponent<TMP_Text>().text = winnersName;
        WinnerDisplay.transform.GetChild(1).GetComponent<TMP_Text>().text = ((int)winnersScore).ToString("D3");

        WinnerDisplay.SetActive(true);
    }

    void DisplayWinner()
    {
        float winnersScore = 0;
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
        WinnerDisplay.transform.GetChild(1).GetComponent<TMP_Text>().text = ((int)winnersScore).ToString("D3");

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