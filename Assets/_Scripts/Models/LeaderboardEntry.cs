using UnityEngine.Serialization;

[System.Serializable]
public struct LeaderboardEntry
{
    public string PlayerName;
    public int Score;
    [FormerlySerializedAs("ShipType")] public VesselClassType vesselType;

    public LeaderboardEntry(string playerName, int score, VesselClassType vesselType)
    {
        PlayerName = playerName;
        Score = score;
        this.vesselType = vesselType;
    }
}