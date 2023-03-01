using StarWriter.Core.Input;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniGame : MonoBehaviour
{
    [SerializeField] TurnMonitor TurnMonitor;
    [SerializeField] ScoreTracker ScoreTracker;
    [SerializeField] List<ShipTypes> AllowedShipTypes;
    [SerializeField] int NumberOfPlayers = 1;   // TODO: get rid of this and use player count instead
    [SerializeField] int NumberOfRounds = int.MaxValue;
    [SerializeField] GameObject CountdownDisplay;   // TODO: we will show the player a brief countdown before the round starts
    [SerializeField] GameObject PlayerOrigin;
    [SerializeField] protected List<Player> Players;

    // Game State Tracking
    int TurnsTakenThisRound = 0;
    int RoundsPlayedThisGame = 0;
    
    // playerId Tracking
    int activePlayerId;
    int RemainingPlayersActivePlayerIndex = -1;
    List<int> RemainingPlayers;
    protected Player ActivePlayer;

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

    public virtual void StartNewGame()
    {
        RemainingPlayers = new();
        for (var i = 0; i < Players.Count; i++) RemainingPlayers.Add(i);

        StartGame();
    }
    void Update()
    {
        if (TurnMonitor.CheckForEndOfTurn())
        {
            EndTurn();
            return;
        }
    }

    void StartGame()
    {
        RoundsPlayedThisGame = 0;
        StartRound();
    }

    void StartRound()
    {
        Debug.Log($"Round {RoundsPlayedThisGame + 1} Start");
        TurnsTakenThisRound = 0;
        StartTurn();
    }

    void StartTurn()
    {
        ReadyNextPlayer();
        SetupTurn();
        TurnMonitor.NewTurn();

        Debug.Log($"Player {activePlayerId + 1} Get Ready!");
    }

    public virtual void EndTurn() // TODO: this needs to be public?
    {
        TurnsTakenThisRound++;

        Debug.Log($"MiniGame.EndTurn - Turns Taken: {TurnsTakenThisRound} ");

        if (TurnsTakenThisRound >= RemainingPlayers.Count)
            EndRound();
        else
            StartTurn();
    }

    void EndRound()
    {
        RoundsPlayedThisGame++;

        ResolveEliminations();

        Debug.Log($"MiniGame.EndRound - Rounds Played: {RoundsPlayedThisGame} ");

        if (RoundsPlayedThisGame >= NumberOfRounds)// || RemainingPlayers.Count < 2
            EndGame();
        else
            StartRound();
    }

    void EndGame()
    {
        Debug.Log($"MiniGame.EndGame - Rounds Played: {RoundsPlayedThisGame} ");

        // TODO: show a scoreboard or do other cool stuff
        StartNewGame();
    }

    void LoopActivePlayerIndex()
    {
        RemainingPlayersActivePlayerIndex++;
        RemainingPlayersActivePlayerIndex %= RemainingPlayers.Count;
    }

    List<int> EliminatedPlayers = new List<int>();

    void EliminateActivePlayer()
    {
        // TODO Add to queue and resolve when round ends
        EliminatedPlayers.Add(activePlayerId);
    }

    void ResolveEliminations()
    {
        EliminatedPlayers.Reverse();
        foreach (var playerId in EliminatedPlayers)
            RemainingPlayers.Remove(playerId);

        EliminatedPlayers = new List<int>();
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
        ActivePlayer.GetComponent<InputController>().PauseInput();
        ActivePlayer.Ship.Teleport(transform);
        ActivePlayer.Ship.TrailSpawner.PauseTrailSpawner();
        TrailSpawner.NukeTheTrails();
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