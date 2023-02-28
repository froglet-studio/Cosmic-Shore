using System.Collections.Generic;
using UnityEngine;

public class MiniGame : MonoBehaviour
{
    [SerializeField] TurnMonitor TurnMonitor;
    //[SerializeField] ScoreTracker ScoreTracker;
    [SerializeField] List<ShipTypes> AllowedShipTypes;
    [SerializeField] int NumberOfPlayers = 1;
    [SerializeField] int NumberOfRounds = int.MaxValue;

    List<int> RemainingPlayers;

    void Start()
    {
        
    }


    void Update()
    {
        if (TurnMonitor.CheckForEndOfTurn())
        {
            TurnOver();
            return;
        }
    }

    int TurnsTakenThisRound = 0;
    int RoundsPlayedThisGame = 0;

    public virtual void TurnOver()
    {
        ++TurnsTakenThisRound;

        if (TurnsTakenThisRound >= NumberOfPlayers)
            RoundOver();
    }

    void RoundOver()
    {
        TurnsTakenThisRound = 0;

        ++RoundsPlayedThisGame;

        if (RoundsPlayedThisGame >= NumberOfRounds)
            GameOver();
    }

    void GameOver()
    {
        Debug.Log("MiniGame.GameOver");
        RoundsPlayedThisGame = 0;
    }

    void StartTurn()
    {

    }

    void StartRound()
    {

    }

    void StartGame()
    {

    }
}

public class RoundTracker
{
    public int PlayerCount;


}