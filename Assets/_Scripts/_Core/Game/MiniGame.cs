using StarWriter.Core.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MiniGame : MonoBehaviour
{
    [SerializeField] TurnMonitor TurnMonitor;
    [SerializeField] ScoreTracker ScoreTracker;
    [SerializeField] List<ShipTypes> AllowedShipTypes;
    [SerializeField] int NumberOfPlayers = 1;   // TODO: get rid of this and use player count instead
    [SerializeField] int NumberOfRounds = int.MaxValue;
    [SerializeField] GameObject CountdownDisplay;   // TODO: we will show the player a brief countdown before the round starts
    [SerializeField] GameObject PlayerOrigin;
    [SerializeField] GameObject EndGameScreen;
    [SerializeField] protected List<Player> Players;

    // Game State Tracking
    int TurnsTakenThisRound = 0;
    int RoundsPlayedThisGame = 0;
    
    // playerId Tracking
    int activePlayerId;
    int RemainingPlayersActivePlayerIndex = -1;
    List<int> RemainingPlayers;
    public Player ActivePlayer;
    protected bool gameRunning;

    protected virtual void Start()
    {
        StartNewGame();

        // Give other objects a few moments to start
        // StartCoroutine(StartNewGameCoroutine());
    }

    IEnumerator StartNewGameCoroutine()
    {
        yield return new WaitForSeconds(.2f);
        StartNewGame();
    }

    // TODO: use the scene navigator instead?
    public void ResetAndReplay() { SceneManager.LoadScene(SceneManager.GetActiveScene().name); }

    public virtual void StartNewGame()
    {
        RemainingPlayers = new();
        for (var i = 0; i < Players.Count; i++) RemainingPlayers.Add(i);

        StartGame();
    }
    protected virtual void Update()
    {
        if (!gameRunning)
            return;

        if (TurnMonitor.CheckForEndOfTurn())
        {
            EndTurn();
            return;
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
        TurnMonitor.NewTurn(Players[activePlayerId].PlayerName);

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
        Debug.Log($"MiniGame.EndGame - Player One Score: {ScoreTracker.GetScore(Players[0].PlayerName)} ");
        Debug.Log($"MiniGame.EndGame - Player Two Score: {ScoreTracker.GetScore(Players[1].PlayerName)} ");

        gameRunning = false;
        EndGameScreen.SetActive(true);
        ScoreTracker.DisplayScores();
        // TODO: show a scoreboard or do other cool stuff
        //StartNewGame();
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
        ActivePlayer.Ship.TrailSpawner.PauseTrailSpawner();
        
        CameraManager.Instance.SetupGamePlayCameras(ActivePlayer.Ship.transform);
        StartCoroutine(CountdownCoroutine());
    }

    IEnumerator CountdownCoroutine()
    {
        Debug.Log("Countdown: 3");
        yield return new WaitForSeconds(1);
        Debug.Log("Countdown: 2");
        yield return new WaitForSeconds(1);
        Debug.Log("Countdown: 1");
        yield return new WaitForSeconds(1);
        Debug.Log("Go!");
        ActivePlayer.GetComponent<InputController>().PauseInput(false);
        ActivePlayer.Ship.TrailSpawner.RestartTrailSpawnerAfterDelay();
    }
}