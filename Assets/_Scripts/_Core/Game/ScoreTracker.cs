using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public enum ScoringModes
{
    HostileVolumeDestroyed = 0,
    VolumeCreated = 1,
    TimePlayed = 2,
    TurnsPlayed = 3,
    VolumeStolen = 4,
    BlocksStolen = 5,
    TeamVolumeRemaining = 6,
}

public class ScoreTracker : MonoBehaviour
{
    [HideInInspector] public TMP_Text ActivePlayerScoreDisplay;

    // Magic number to give more precision to time tracking as an integer value
    
    [SerializeField] public ScoringModes ScoringMode;
    [SerializeField] bool GolfRules;
    [HideInInspector] public GameCanvas GameCanvas;

    [Header("Optional Configuration")]
    [SerializeField] float TimePlayedScoreMultiplier = 1000f;
    [SerializeField] float VolumeNormalizationQuotient = 145.65f;

    VerticalLayoutGroup Scoreboard;
    TMP_Text WinnerNameContainer;
    Image WinnerBannerImage;
    Color GreenTeamWinColor;
    Color RedTeamWinColor;
    Color YellowTeamWinColor;

    List<TMP_Text> PlayerNameContainers = new();
    List<TMP_Text> PlayerScoreContainers = new();
    Dictionary<string, float> playerScores = new ();
    Dictionary<string, Teams> playerTeams = new();
    string currentPlayerName;
    Teams currentPlayerTeam;
    int turnsPlayed = 0;
    float turnStartTime;

    void Start()
    {
        Scoreboard = GameCanvas.Scoreboard;
        ActivePlayerScoreDisplay = GameCanvas.MiniGameHUD.ScoreDisplay;
        WinnerNameContainer = GameCanvas.WinnerNameContainer;
        WinnerBannerImage = GameCanvas.WinnerBannerImage;
        GreenTeamWinColor = GameCanvas.GreenTeamWinColor;
        RedTeamWinColor = GameCanvas.RedTeamWinColor;
        YellowTeamWinColor = GameCanvas.YellowTeamWinColor;

        for (var i = 0; i < Scoreboard.transform.childCount; i++)
        {
            var child = Scoreboard.transform.GetChild(i);
            Debug.Log($"init scoreboard name: {child.name}");
            Debug.Log($"init scoreboard child count: {child.transform.childCount}");
            PlayerNameContainers.Add(child.transform.GetChild(0).GetComponent<TMP_Text>());
            PlayerScoreContainers.Add(child.transform.GetChild(1).GetComponent<TMP_Text>());
        }
    }

    public virtual void StartTurn(string playerName, Teams playerTeam)
    {
        if (!playerScores.ContainsKey(playerName))
        {
            playerScores.Add(playerName, 0);
            playerTeams.Add(playerName, playerTeam);
        }

        currentPlayerName = playerName;
        currentPlayerTeam = playerTeam;
        turnStartTime = Time.time;
    }

    void Update()
    {
        if (turnStartTime == 0)
            return;

        if (ActivePlayerScoreDisplay != null)
        {
            var score = 0f;
            switch (ScoringMode)
            {
                case ScoringModes.HostileVolumeDestroyed:
                    if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                        score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].hostileVolumeDestroyed / VolumeNormalizationQuotient;
                    break;
                case ScoringModes.VolumeCreated:
                    if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                        score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].volumeCreated;
                    break;
                case ScoringModes.VolumeStolen:
                    if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                        score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].volumeStolen;
                    break;
                case ScoringModes.TimePlayed:
                    score = playerScores[currentPlayerName] + (Time.time - turnStartTime) * TimePlayedScoreMultiplier;
                    break;
                case ScoringModes.TurnsPlayed:
                    score = turnsPlayed;
                    break;
                case ScoringModes.BlocksStolen:
                    if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                        score = playerScores[currentPlayerName] + StatsManager.Instance.playerStats[currentPlayerName].blocksStolen;
                    break;
                case ScoringModes.TeamVolumeRemaining:
                    var teamStats = StatsManager.Instance.teamStats;  // TODO: Hardcoded player team to Green... reconsider
                    var greenVolume = teamStats.ContainsKey(Teams.Green) ? teamStats[Teams.Green].volumeRemaining : 0f;
                    var redVolume = teamStats.ContainsKey(Teams.Red) ? teamStats[Teams.Red].volumeRemaining : 0f;

                    score = (greenVolume - redVolume);
                    break;
            }

            ActivePlayerScoreDisplay.text = ((int) score).ToString();
        }
    }

    public virtual void EndTurn()
    {
        turnsPlayed++;

        switch (ScoringMode)
        {
            case ScoringModes.HostileVolumeDestroyed:
                if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                    playerScores[currentPlayerName] += StatsManager.Instance.playerStats[currentPlayerName].hostileVolumeDestroyed / VolumeNormalizationQuotient;
                StatsManager.Instance.ResetStats();
                break;
            case ScoringModes.VolumeCreated:
                if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                    playerScores[currentPlayerName] += StatsManager.Instance.playerStats[currentPlayerName].volumeCreated;
                StatsManager.Instance.ResetStats();
                break;
            case ScoringModes.VolumeStolen:
                if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                    playerScores[currentPlayerName] += StatsManager.Instance.playerStats[currentPlayerName].volumeStolen;
                StatsManager.Instance.ResetStats();
                break;
            case ScoringModes.TimePlayed:
                playerScores[currentPlayerName] += (Time.time - turnStartTime) * TimePlayedScoreMultiplier;  
                break;
            case ScoringModes.TurnsPlayed:
                playerScores[currentPlayerName] = turnsPlayed;
                break;
            case ScoringModes.BlocksStolen:
                if (StatsManager.Instance.playerStats.ContainsKey(currentPlayerName))
                    playerScores[currentPlayerName] += StatsManager.Instance.playerStats[currentPlayerName].blocksStolen;
                StatsManager.Instance.ResetStats();
                break;
            case ScoringModes.TeamVolumeRemaining:
                var teamStats = StatsManager.Instance.teamStats;
                var greenVolume = teamStats.ContainsKey(Teams.Green) ? teamStats[Teams.Green].volumeRemaining : 0f;
                var redVolume = teamStats.ContainsKey(Teams.Red) ? teamStats[Teams.Red].volumeRemaining : 0f;
                playerScores[currentPlayerName] = (greenVolume - redVolume);
                StatsManager.Instance.ResetStats();
                break;
        }

        foreach (var playerTeam in playerTeams) // Add all the players back into the reset stats dictionary so the score will update at the start of the player's turn
            StatsManager.Instance.AddPlayer(playerTeam.Value, playerTeam.Key);
    }

    public List<int> GetScores()
    {
        var scores = new List<int>();
        foreach (var score in playerScores.Values)
            scores.Add((int) score);
        
        return scores;
    }

    public virtual string GetWinner()
    {
        bool minTie;
        bool maxTie;
        float minScore = float.MaxValue; 
        float maxScore = float.MinValue;
        string minKey ="";
        string maxKey = "";
        foreach (var key in playerScores.Keys)
        {
            if (playerScores[key] <= minScore)
            {
                minTie = playerScores[key] == minScore;
                minScore = playerScores[key];
                minKey = key;
            }
            if (playerScores[key] >= maxScore)
            {
                maxTie = playerScores[key] == maxScore;
                maxScore = playerScores[key];
                maxKey = key;
            }
        }

        if (GolfRules)
            return minKey;
        else 
            return maxKey;
    }

    public virtual int GetHighScore()
    {
        bool minTie;
        bool maxTie;
        float minScore = float.MaxValue;
        float maxScore = float.MinValue;
        string minKey = "";
        string maxKey = "";
        foreach (var key in playerScores.Keys)
        {
            if (playerScores[key] <= minScore)
            {
                minTie = playerScores[key] == minScore;
                minScore = playerScores[key];
                minKey = key;
            }
            if (playerScores[key] >= maxScore)
            {
                maxTie = playerScores[key] == maxScore;
                maxScore = playerScores[key];
                maxKey = key;
            }
        }

        if (GolfRules)
            return (int)minScore;
        else
            return (int)maxScore;
    }

    public virtual int GetScore(string playerName) 
    {
        return (int) playerScores[playerName];
    }

    public virtual void DisplayScores()
    {
        List<LeaderboardEntry> scores = new List<LeaderboardEntry>();
        foreach (var score in playerScores) {
            scores.Add(new LeaderboardEntry(score.Key, (int) score.Value, ShipTypes.Manta));
        }
        scores.Sort((score1, score2) => score2.Score.CompareTo(score1.Score));

        for (var i = 0; i < scores.Count; i++)
        {
            PlayerNameContainers[i].text = scores[i].PlayerName;
            PlayerScoreContainers[i].text = scores[i].Score.ToString();
        }

        for (var i = playerScores.Keys.Count; i<PlayerNameContainers.Count; i++)
        {
            PlayerNameContainers[i].text = "";
            PlayerScoreContainers[i].text = "";
            PlayerScoreContainers[i].gameObject.SetActive(false);
        }

        var winner = GetWinner();
        switch (playerTeams[winner])
        {
            case Teams.Green:
                WinnerBannerImage.color = GreenTeamWinColor;
                WinnerNameContainer.text = "Green Victory";
                break;
            case Teams.Red:
                WinnerBannerImage.color = RedTeamWinColor;
                WinnerNameContainer.text = "Red Victory";
                break;
            case Teams.Yellow:
                WinnerBannerImage.color = YellowTeamWinColor;
                WinnerNameContainer.text = "Gold Victory";
                break;
        }
    }
}