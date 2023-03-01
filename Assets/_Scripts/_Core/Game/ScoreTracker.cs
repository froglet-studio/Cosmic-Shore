using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ScoringModes
{
    VolumeDestroyed,
    VolumeCreated,
    TimePlayed,
    TurnsPlayed
}

public class ScoreTracker : MonoBehaviour
{
    [SerializeField] ScoringModes ScoringMode;
    [SerializeField] bool GolfRules;

    Dictionary<string, float> playerScores = new ();
    string currentPlayerId;
    int turnsPlayed = 0;
    float turnStartTime;

    public virtual void StartTurn(string playerId)
    {
        if (!playerScores.ContainsKey(playerId))
            playerScores.Add(playerId, 0);

        currentPlayerId = playerId;
        turnStartTime = Time.time;
    }

    public virtual void EndTurn()
    {
        turnsPlayed++;

        switch (ScoringMode)
        {
            case ScoringModes.VolumeDestroyed:
                playerScores[currentPlayerId] += StatsManager.Instance.playerStats[currentPlayerId].volumeDestroyed;
                break;
            case ScoringModes.VolumeCreated:
                playerScores[currentPlayerId] += StatsManager.Instance.playerStats[currentPlayerId].volumeCreated;
                break;
            case ScoringModes.TimePlayed:
                // TODO: 1000 is a magic number to give more precision to time tracking as an integer value
                playerScores[currentPlayerId] += (Time.time - turnStartTime) * 1000;  
                break;
            case ScoringModes.TurnsPlayed:
                playerScores[currentPlayerId] = turnsPlayed;
                break;
        }
    }

    public virtual string GetWinner()
    {
        // TODO this doesn't handle a tie

        if (GolfRules)
            return playerScores.Min().Key;
        else 
            return playerScores.Max().Key;
    }

    public virtual int GetScore(string playerId) 
    {
        return (int) playerScores[playerId];
    }
}