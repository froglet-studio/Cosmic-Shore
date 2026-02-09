namespace CosmicShore.Game.Cinematics
{
    /// <summary>
    /// Data structure for vessel display information
    /// </summary>
    [System.Serializable]
    public struct VesselDisplayData
    {
        public string playerName;
        public VesselClassType vesselType;
        public int ranking;
        public Domains domain;
        public int score;

        public VesselDisplayData(string name, VesselClassType type, int rank, Domains playerDomain, int playerScore)
        {
            playerName = name;
            vesselType = type;
            ranking = rank;
            domain = playerDomain;
            score = playerScore;
        }
    }
}