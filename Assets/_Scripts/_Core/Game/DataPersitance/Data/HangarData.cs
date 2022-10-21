using System.Collections.Generic;

[System.Serializable]
public struct PlayerBuild
{
    public string Pilot;
    public string Ship;   
    public string Trail;

    public PlayerBuild(string pilot, string ship, string trail)
    {
        Pilot = pilot;
        Ship = ship;
        Trail = trail;
    }
}

[System.Serializable]
public class HangarData 
{
    public Dictionary<string, PlayerBuild> PlayerBuilds;

    public HangarData()
    {
        //Default builds if HangarData.data file doesn't exist
        PlayerBuild DefaultPlayerBuild001 = new PlayerBuild("Zak", "Manta", "GreenTrail");
        PlayerBuild DefaultPlayerBuild002 = new PlayerBuild("Milliron", "Dolphin", "BlueTrail");
        PlayerBuild DefaultPlayerBuild003 = new PlayerBuild("Iggy", "Shark", "RedTrail");

        this.PlayerBuilds = new Dictionary<string, PlayerBuild>();

        PlayerBuilds.Add("DefaultPlayerBuild001", DefaultPlayerBuild001);
        PlayerBuilds.Add("DefaultPlayerBuild002", DefaultPlayerBuild002);
        PlayerBuilds.Add("DefaultPlayerBuild003", DefaultPlayerBuild003);
    }
}
