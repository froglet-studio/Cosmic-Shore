using StarWriter.Core.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MiniGame : MonoBehaviour
{
    [SerializeField] protected MiniGames gameMode;
    [SerializeField] protected int NumberOfRounds = int.MaxValue;
    [SerializeField] protected List<TurnMonitor> TurnMonitors;
    [SerializeField] protected ScoreTracker ScoreTracker;
    [SerializeField] GameCanvas GameCanvas;
    [SerializeField] Player playerPrefab;
    [SerializeField] GameObject PlayerOrigin;

    protected Button ReadyButton;
    protected GameObject EndGameScreen;
    protected MiniGameHUD HUD;
    protected List<Player> Players;

    List<Teams> PlayerTeams = new() { Teams.Green, Teams.Red, Teams.Yellow };
    List<string> PlayerNames = new() { "PlayerOne", "PlayerTwo", "PlayerThree" };

    // Configuration set by player
    public static int NumberOfPlayers = 2;
    public static int DifficultyLevel = 1;
    public static ShipTypes PlayerShipType = ShipTypes.Dolphin;

    // Game State Tracking
    protected int TurnsTakenThisRound = 0;
    int RoundsPlayedThisGame = 0;
    
    // PlayerId Tracking
    int activePlayerId;
    int RemainingPlayersActivePlayerIndex = -1;
    protected List<int> RemainingPlayers = new();
    [HideInInspector] public Player ActivePlayer;
    protected bool gameRunning;

    protected virtual void Awake()
    {
        EndGameScreen = GameCanvas.EndGameScreen;
        HUD = GameCanvas.MiniGameHUD;
        ReadyButton = HUD.ReadyButton;
        CountdownDisplay = HUD.CountdownDisplay;
        ScoreTracker.GameCanvas = GameCanvas;
        foreach (var turnMonitor in TurnMonitors)
            if (turnMonitor is TimeBasedTurnMonitor tbtMonitor)
                tbtMonitor.display = HUD.RoundTimeDisplay;
    }

    protected virtual void Start()
    {
        Players = new List<Player>();
        for (var i = 0; i < NumberOfPlayers; i++)
        {
            Players.Add(Instantiate(playerPrefab));
            Players[i].defaultShip = PlayerShipType;
            Players[i].Team = PlayerTeams[i];
            Players[i].PlayerName = PlayerNames[i];
            Players[i].PlayerUUID = PlayerNames[i];
            Players[i].name = "Player" + (i+1);
            Players[i].gameObject.SetActive(true);
        }

        ReadyButton.onClick.AddListener(OnReadyClicked);
        ReadyButton.gameObject.SetActive(false);

        // Give other objects a few moments to start
        StartCoroutine(StartNewGameCoroutine());
    }

    IEnumerator StartNewGameCoroutine()
    {
        yield return new WaitForSeconds(.2f);
        
        StartNewGame();
    }

    // TODO: use the scene navigator instead?
    public void ResetAndReplay()
    { 
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void Exit()
    {
        if (PauseSystem.Paused) PauseSystem.TogglePauseGame();
        // TODO: this is kind of hokie
        SceneManager.LoadScene(0);
    }

    public void OnReadyClicked()
    {
        ReadyButton.gameObject.SetActive(false);
        StartCoroutine(CountdownCoroutine());
    }

    public virtual void StartNewGame()
    {
        if (PauseSystem.Paused) PauseSystem.TogglePauseGame();

        RemainingPlayers = new();
        for (var i = 0; i < Players.Count; i++) RemainingPlayers.Add(i);

        StartGame();
    }
    protected virtual void Update()
    {
        if (!gameRunning)
            return;

        foreach (var turnMonitor in TurnMonitors)
        {
            if (turnMonitor.CheckForEndOfTurn())
            {
                EndTurn();
                return;
            }
        }
    }

    void StartGame()
    {
        gameRunning = true;
        Debug.Log($"MiniGame.StartGame, ... {Time.time}");
        EndGameScreen.SetActive(false);
        RoundsPlayedThisGame = 0;
        StartRound();
    }

    void StartRound()
    {
        Debug.Log($"MiniGame.StartRound - Round {RoundsPlayedThisGame + 1} Start, ... {Time.time}");
        TurnsTakenThisRound = 0;
        SetupTurn();
    }

    protected void StartTurn()
    {
        foreach (var turnMonitor in TurnMonitors)
            turnMonitor.ResumeTurn();

        ScoreTracker.StartTurn(Players[activePlayerId].PlayerName, Players[activePlayerId].Team);

        Debug.Log($"Player {activePlayerId + 1} Get Ready! {Time.time}");
    }

    public virtual void EndTurn() // TODO: this needs to be public?
    {
        TurnsTakenThisRound++;

        ScoreTracker.EndTurn();
        Debug.Log($"MiniGame.EndTurn - Turns Taken: {TurnsTakenThisRound}, ... {Time.time}");

        if (TurnsTakenThisRound >= RemainingPlayers.Count)
            EndRound();
        else
            SetupTurn();
    }

    protected void EndRound()
    {
        RoundsPlayedThisGame++;

        ResolveEliminations();

        Debug.Log($"MiniGame.EndRound - Rounds Played: {RoundsPlayedThisGame}, ... {Time.time}");

        if (RoundsPlayedThisGame >= NumberOfRounds || RemainingPlayers.Count <=0)
            EndGame();
        else
            StartRound();
    }

    void EndGame()
    {
        Debug.Log($"MiniGame.EndGame - Rounds Played: {RoundsPlayedThisGame}, ... {Time.time}");
        Debug.Log($"MiniGame.EndGame - Winner: {ScoreTracker.GetWinner()} ");

        foreach (var player in Players)
            Debug.Log($"MiniGame.EndGame - Player Score: {ScoreTracker.GetScore(player.PlayerName)} ");

        UpdateLeaderboardEntries();

        CameraManager.Instance.SetEndCameraActive();
        PauseSystem.TogglePauseGame();
        gameRunning = false;
        EndGameScreen.SetActive(true);
        ScoreTracker.DisplayScores();
    }

    void UpdateLeaderboardEntries()
    {
        // Update leaderboard stats
        var leaderboardDictionary = LeaderboardDataAccessor.Load();
        List<LeaderboardEntry> leaderboardEntries;
        if (leaderboardDictionary.ContainsKey(gameMode))
            leaderboardEntries = leaderboardDictionary[gameMode];
        else
            leaderboardEntries = LeaderboardDataAccessor.LeaderboardEntriesDefault[gameMode];

        
        foreach (var entry in leaderboardEntries)
            Debug.Log($"LeaderboardEntries: {entry.PlayerName}, {entry.Score}");

        // For each score in score tracker, check to see if it belongs on leaderboard.
        // iterate over highest score first,
        // compare against lowest score first in a loop
        foreach (var player in Players)
        {
            Debug.Log($"MiniGame.EndGame - Player Score: {ScoreTracker.GetScore(player.PlayerName)} ");
            leaderboardEntries.Add(new LeaderboardEntry(player.PlayerName, ScoreTracker.GetScore(player.PlayerName), PlayerShipType));
        }

        leaderboardEntries.Sort((score1, score2) => score2.Score.CompareTo(score1.Score));
        leaderboardEntries.RemoveRange(5, leaderboardEntries.Count - 5);

        foreach (var entry in leaderboardEntries)
            Debug.Log($"LeaderboardEntries: {entry.PlayerName}, {entry.Score}");

        LeaderboardDataAccessor.Save(gameMode, leaderboardEntries);
    }

    void LoopActivePlayerIndex()
    {
        RemainingPlayersActivePlayerIndex++;
        RemainingPlayersActivePlayerIndex %= RemainingPlayers.Count;
    }

    List<int> EliminatedPlayers = new List<int>();

    protected void EliminateActivePlayer()
    {
        // TODO Add to queue and resolve when round ends
        EliminatedPlayers.Add(activePlayerId);
    }

    protected void ResolveEliminations()
    {
        EliminatedPlayers.Reverse();
        foreach (var playerId in EliminatedPlayers)
            RemainingPlayers.Remove(playerId);

        EliminatedPlayers = new List<int>();

        if (RemainingPlayers.Count <= 0)
            EndGame();
    }

    protected virtual void ReadyNextPlayer()
    {
        LoopActivePlayerIndex();
        activePlayerId = RemainingPlayers[RemainingPlayersActivePlayerIndex];
        ActivePlayer = Players[activePlayerId];

        foreach (var player in Players)
        {
            Debug.Log($"PlayerUUID: {player.PlayerUUID}");
            player.gameObject.SetActive(player.PlayerUUID == ActivePlayer.PlayerUUID);
        }
    }

    protected virtual void SetupTurn()
    {
        ReadyNextPlayer();

        // Wait for player ready before activating turn monitor (only really relevant for time based monitor)
        foreach (var turnMonitor in TurnMonitors)
        {
            turnMonitor.NewTurn(Players[activePlayerId].PlayerName);
            turnMonitor.PauseTurn();
        }

        ActivePlayer.transform.SetPositionAndRotation(PlayerOrigin.transform.position, PlayerOrigin.transform.rotation);
        ActivePlayer.GetComponent<InputController>().PauseInput();
        ActivePlayer.Ship.Teleport(PlayerOrigin.transform);
        ActivePlayer.Ship.GetComponent<ShipTransformer>().Reset();
        ActivePlayer.Ship.TrailSpawner.PauseTrailSpawner();
        ActivePlayer.Ship.ResourceSystem.Reset();

        CameraManager.Instance.SetupGamePlayCameras(ActivePlayer.Ship.transform);

        // For single player games, don't require the extra button press
        if (Players.Count > 1)
            ReadyButton.gameObject.SetActive(true);
        else
            OnReadyClicked();
    }

    // TODO: P1 make the countdown timer its own monobehavior
    Image CountdownDisplay;
    [SerializeField] Sprite Countdown3;
    [SerializeField] Sprite Countdown2;
    [SerializeField] Sprite Countdown1;
    [SerializeField] Sprite Countdown0;
    [SerializeField] float CountdownGrowScale = 1.5f;

    IEnumerator CountdownDigitCoroutine(Sprite digit)
    {
        var elapsedTime = 0f;
        CountdownDisplay.transform.localScale = Vector3.one;
        CountdownDisplay.sprite = digit;

        while (elapsedTime < 1)
        {
            elapsedTime += Time.deltaTime;
            CountdownDisplay.transform.localScale = Vector3.one + (Vector3.one * ((CountdownGrowScale - 1) * elapsedTime));
            yield return null;
        }
    }

    IEnumerator CountdownCoroutine()
    {
        CountdownDisplay.gameObject.SetActive(true);

        yield return StartCoroutine(CountdownDigitCoroutine(Countdown3));
        yield return StartCoroutine(CountdownDigitCoroutine(Countdown2));
        yield return StartCoroutine(CountdownDigitCoroutine(Countdown1));
        yield return StartCoroutine(CountdownDigitCoroutine(Countdown0));

        StartTurn();

        CountdownDisplay.transform.localScale = Vector3.one;
        CountdownDisplay.gameObject.SetActive(false);

        ActivePlayer.GetComponent<InputController>().PauseInput(false);
        ActivePlayer.Ship.TrailSpawner.ForceStartSpawningTrail();
        ActivePlayer.Ship.TrailSpawner.RestartTrailSpawnerAfterDelay(2f);
    }
}