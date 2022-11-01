using System.Collections.Generic;

[System.Serializable]
public struct ShipConfiguration
{
    public string Ship;   
    public string Trail;
    public string Upgrade1;

    public ShipConfiguration(string ship, string trail, string upgrade1)
    {
        Ship = ship;
        Trail = trail;
        Upgrade1 = upgrade1;
    }
}

[System.Serializable]
public class HangarData 
{
    public int MaxBayCount = 10;
    public List<ShipConfiguration> PlayerBuilds;

    public HangarData()
    {
        // Default builds if HangarData.data file doesn't exist
        PlayerBuilds = new List<ShipConfiguration>();
        PlayerBuilds.Add(new ShipConfiguration("Manta", "GreenTrail", "1.5X Boost"));
        PlayerBuilds.Add(new ShipConfiguration("Dolphin", "BlueTrail", "Skim"));
        PlayerBuilds.Add(new ShipConfiguration("Shark", "RedTrail", "2X Points"));
    }
}