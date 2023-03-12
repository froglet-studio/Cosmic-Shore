using StarWriter.Core.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MiniGame : MonoBehaviour
{
    [SerializeField] Player playerPrefab;
    [SerializeField] List<TurnMonitor> TurnMonitors;
    [SerializeField] ScoreTracker ScoreTracker;
    [SerializeField] List<ShipTypes> AllowedShipTypes;
    
    [SerializeField] int NumberOfRounds = int.MaxValue;
    [SerializeField] GameObject PlayerOrigin;
    [SerializeField] GameObject EndGameScreen;
    protected List<Player> Players;

    List<Teams> PlayerTeams = new List<Teams>() { Teams.Green, Teams.Red, Teams.Blue, Teams.Yellow };
    List<string> PlayerNames = new List<string>() { "PlayerOne", "PlayerTwo", "PlayerThree", "PlayerFour" };

    // Configuration set by player
    public static int NumberOfPlayers = 2;
    public static int DifficultyLevel = 1;
    public static ShipTypes PlayerShipType = ShipTypes.Dolphin;

    // Game State Tracking
    int TurnsTakenThisRound = 0;
    int RoundsPlayedThisGame = 0;
    
    // playerId Tracking
    int activePlayerId;
    int RemainingPlayersActivePlayerIndex = -1;
    List<int> RemainingPlayers = new();
    public Player ActivePlayer;
    protected bool gameRunning;

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

        //StartNewGame();

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
        StartTurn();
    }

    void StartTurn()
    {
        ReadyNextPlayer();
        SetupTurn();
        foreach (var turnMonitor in TurnMonitors)
            turnMonitor.NewTurn(Players[activePlayerId].PlayerName);

        ScoreTracker.StartTurn(Players[activePlayerId].PlayerName);

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
            StartTurn();
    }

    void EndRound()
    {
        RoundsPlayedThisGame++;

        ResolveEliminations();

        Debug.Log($"MiniGame.EndRound - Rounds Played: {RoundsPlayedThisGame}, ... {Time.time}");

        if (RoundsPlayedThisGame >= NumberOfRounds || RemainingPlayers.Count <=0)// || RemainingPlayers.Count < 2
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

        PauseSystem.TogglePauseGame();
        gameRunning = false;
        EndGameScreen.SetActive(true);
        ScoreTracker.DisplayScores();
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
        ActivePlayer.transform.SetPositionAndRotation(PlayerOrigin.transform.position, PlayerOrigin.transform.rotation);
        //ActivePlayer.Ship.transform.SetPositionAndRotation(PlayerOrigin.transform.position, PlayerOrigin.transform.rotation);
        ActivePlayer.GetComponent<InputController>().PauseInput();
        ActivePlayer.Ship.Teleport(PlayerOrigin.transform);
        ActivePlayer.Ship.GetComponent<ShipController>().Reset();
        ActivePlayer.Ship.TrailSpawner.PauseTrailSpawner();

        CameraManager.Instance.SetupGamePlayCameras(ActivePlayer.Ship.transform);
        StartCoroutine(CountdownCoroutine());
    }

    [SerializeField] Image CountdownDisplay;
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

        CountdownDisplay.transform.localScale = Vector3.one;
        CountdownDisplay.gameObject.SetActive(false);

        ActivePlayer.GetComponent<InputController>().PauseInput(false);
        ActivePlayer.Ship.TrailSpawner.RestartTrailSpawnerAfterDelay();
        ActivePlayer.Ship.TrailSpawner.ForceStartSpawningTrail();
    }
}