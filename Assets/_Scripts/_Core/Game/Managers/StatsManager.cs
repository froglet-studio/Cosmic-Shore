using System.Collections.Generic;
using UnityEngine;
using TMPro;
using StarWriter.Core;
using static StarWriter.Core.GameSetting;
using StarWriter.Utility.Singleton;

// TODO: pull out into separate file
public struct RoundStats
{
    public int blocksCreated;
    public int blocksDestroyed;
    public int allyBlocksDestroyed;
    public int enemyBlocksDestroyed;
    public float volumeCreated;
    public float volumeDestroyed;
    public float allyVolumeDestroyed;
    public float enemyVolumeDestroyed;
    public int crystalsCollected;

    public RoundStats(bool dummy = false)
    {
        blocksCreated = 0;
        blocksDestroyed = 0;
        allyBlocksDestroyed = 0;
        enemyBlocksDestroyed = 0;
        volumeCreated = 0;
        volumeDestroyed = 0;
        allyVolumeDestroyed = 0;
        enemyVolumeDestroyed = 0;
        crystalsCollected = 0;
    }
}

public class StatsManager : Singleton<StatsManager>
{
    [SerializeField] int extendedLifeScore;
    [SerializeField] int extendedLifeHighScore;
    [SerializeField] GameObject WinnerDisplay;
    [SerializeField] List<GameObject> EndOfRoundStatContainers;
    [SerializeField] List<GameObject> PlayerVolumeContainers;       // TODO: remove this - has been replaced by End Of Round Stats Containers
    [SerializeField] bool TeamScoresEnabled = false;

    // Stats Tracking
    Dictionary<Teams, RoundStats> teamStats = new Dictionary<Teams, RoundStats>();
    Dictionary<string, RoundStats> playerStats = new Dictionary<string, RoundStats>();

    Dictionary<string, GameObject> ScoreDisplays = new Dictionary<string, GameObject>(); // TODO: not sure I like this
    Dictionary<string, float> PlayerScores = new Dictionary<string, float>();
    Dictionary<Teams, float> TeamScores = new Dictionary<Teams, float>();
    static float SinglePlayerScore = 0;
    static bool firstLife = true;

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
        roundStats.allyBlocksDestroyed += team == trailBlockProperties.trail.Team ? 1 : 0;
        roundStats.enemyBlocksDestroyed += team == trailBlockProperties.trail.Team ? 0 : 1;
        roundStats.volumeDestroyed += trailBlockProperties.volume;
        roundStats.allyVolumeDestroyed += team == trailBlockProperties.trail.Team ? trailBlockProperties.volume : 0;
        roundStats.enemyVolumeDestroyed += team == trailBlockProperties.trail.Team ? 0 : trailBlockProperties.volume;
        teamStats[team] = roundStats;

        roundStats = playerStats[playerName];
        roundStats.blocksDestroyed++;
        roundStats.allyBlocksDestroyed += team == trailBlockProperties.trail.Team ? 1 : 0;
        roundStats.enemyBlocksDestroyed += team == trailBlockProperties.trail.Team ? 0 : 1;
        roundStats.volumeDestroyed += trailBlockProperties.volume;
        roundStats.allyVolumeDestroyed += team == trailBlockProperties.trail.Team ? trailBlockProperties.volume : 0;
        roundStats.enemyVolumeDestroyed += team == trailBlockProperties.trail.Team ? 0 : trailBlockProperties.volume;
        playerStats[playerName] = roundStats;
    }

    void OnEnable()
    {
        GameManager.onPlayGame += ResetScoreAndDeathCount;
        GameManager.onDeath += UpdateScoresAndDeathCount;
        GameManager.onGameOver += UpdateScoresAndDeathCount;
        if (!TeamScoresEnabled)
            Trail.AddToScore += UpdateScore;
    }

    void OnDisable()
    {
        GameManager.onPlayGame -= ResetScoreAndDeathCount;
        GameManager.onDeath -= UpdateScoresAndDeathCount;
        GameManager.onGameOver -= UpdateScoresAndDeathCount;
        if (!TeamScoresEnabled)
            Trail.AddToScore -= UpdateScore;
    }

    void Start()
    {
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
            TeamScores.Add(team, 0);

        TeamScores[team] += amount;
    }

    public void UpdateScore(string playerName, float amount)
    {
        if (RoundEnded)
            return;

        if (!PlayerScores.ContainsKey(playerName))
            PlayerScores.Add(playerName, 0);

        PlayerScores[playerName] += amount;

        foreach (var key in PlayerScores.Keys)
        {
            Debug.Log($"Scores: {key}, {PlayerScores[key]}");
        }

        UpdateScoreBoard(SinglePlayerScore);
    }

    void ResetScoreAndDeathCount()
    {
        Debug.Log("ScoringManager.OnPlay");
        foreach (var key in PlayerScores.Keys)
            PlayerScores[key] = 0;
        foreach (var key in TeamScores.Keys)
            TeamScores[key] = 0;

        WinnerDisplay.SetActive(false);

        SinglePlayerScore = 0;
        firstLife = true;
        RoundEnded = false;
    }

    void UpdateScoresAndDeathCount()
    {
        PlayerPrefs.SetInt(PlayerPrefKeys.score.ToString(), (int)SinglePlayerScore);

        // Compares Score to High Score and saves the highest value
        if (PlayerPrefs.GetInt(PlayerPrefKeys.highScore.ToString()) < SinglePlayerScore)
            PlayerPrefs.SetInt(PlayerPrefKeys.highScore.ToString(), (int)SinglePlayerScore);

        if (firstLife)
            if (PlayerPrefs.GetInt(PlayerPrefKeys.firstLifeHighScore.ToString()) < SinglePlayerScore)
                PlayerPrefs.SetInt(PlayerPrefKeys.firstLifeHighScore.ToString(), (int)SinglePlayerScore);

        PlayerPrefs.Save();

        DisplayPlayerScores();

        // TODO: duplicate bookkeeping happening here - introduce different game modes?
        firstLife = false;
        RoundEnded = true;
    }

    void OutputRoundStats()
    {
        /*foreach (var team in teamStats.Keys)
        {
            Debug.LogWarning($"Team Stats - Team:{team}, Crystals Collected: {teamStats[team].crystalsCollected} ");
            //Debug.LogWarning($"Team Stats - Team:{team}, Blocks Created: {teamStats[team].blocksCreated} ");
            //Debug.LogWarning($"Team Stats - Team:{team}, Blocks Destroyed: {teamStats[team].blocksDestroyed} ");
            Debug.LogWarning($"Team Stats - Team:{team}, Volume Created: {teamStats[team].volumeCreated} ");
            Debug.LogWarning($"Team Stats - Team:{team}, Volume Destroyed: {teamStats[team].volumeDestroyed} ");
        }*/

        int i = 0;
        foreach (var player in playerStats.Keys)
        {
            Debug.LogWarning($"PlayerStats - Player:{player}, Crystals Collected: {playerStats[player].crystalsCollected} ");
            //Debug.LogWarning($"PlayerStats - Player:{Player}, Blocks Created: {playerStats[Player].blocksCreated} ");
            //Debug.LogWarning($"PlayerStats - Player:{Player}, Blocks Destroyed: {playerStats[Player].blocksDestroyed} ");
            Debug.LogWarning($"PlayerStats - Player:{player}, Volume Created: {playerStats[player].volumeCreated} ");
            Debug.LogWarning($"PlayerStats - Player:{player}, Volume Destroyed: {playerStats[player].volumeDestroyed} ");

            var container = EndOfRoundStatContainers[i];
            container.transform.GetChild(0).GetComponent<TMP_Text>().text = player;
            container.transform.GetChild(1).GetComponent<TMP_Text>().text = playerStats[player].volumeCreated.ToString("F1");
            container.transform.GetChild(2).GetComponent<TMP_Text>().text = playerStats[player].volumeDestroyed.ToString("F1");
            container.transform.GetChild(3).GetComponent<TMP_Text>().text = playerStats[player].crystalsCollected.ToString("D");

            i++;
        }
    }

    void DisplayPlayerScores()
    {
        OutputRoundStats();
    }
}