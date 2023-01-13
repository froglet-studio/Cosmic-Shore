using StarWriter.Core;
using System.Collections.Generic;

[System.Serializable]
public struct ShipConfiguration
{
    public string Ship;   
    public string Trail;
    public string TagLine;

    public ShipConfiguration(string ship, string trail, string tagLine)
    {
        Ship = ship;
        Trail = trail;
        TagLine = tagLine;
    }
}

[System.Serializable]
public class HangarData : DataPersistenceBase<HangarData>
{
    public int MaxBayCount = 10;
    public List<ShipConfiguration> PlayerBuilds;

    public HangarData()
    {
        // Default builds if HangarData.data file doesn't exist
        PlayerBuilds = new List<ShipConfiguration>();
        PlayerBuilds.Add(new ShipConfiguration("Manta", "GreenTrail", "1.5X Boosting"));
        PlayerBuilds.Add(new ShipConfiguration("Dolphin", "BlueTrail", "Skim"));
        PlayerBuilds.Add(new ShipConfiguration("Shark", "RedTrail", "2X Points"));
    }

    public override HangarData LoadData()
    {
        var loadedData = DataPersistenceManager.Instance.LoadHangerData();
        PlayerBuilds = loadedData.PlayerBuilds;
        return loadedData;
    }

    public override void SaveData()
    {
        DataPersistenceManager.Instance.SaveHangar(this);
    }
}