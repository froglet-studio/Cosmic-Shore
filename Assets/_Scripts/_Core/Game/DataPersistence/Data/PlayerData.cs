using StarWriter.Core;
using System.Collections.Generic;

[System.Serializable]
public class PlayerData : DataPersistenceBase<PlayerData>
{
    public string playerName;
    public int highestScore;

    public Dictionary<string, string> playerBuild;

    public PlayerData()
    {
        playerName = "Default_Name";

        playerBuild = new Dictionary<string, string>();
        playerBuild.Add("Pilot", "Default_Pilot");
        playerBuild.Add("Ship", "Default_Ship");
        playerBuild.Add("Trail", "Default_Trail");
    }

    public override PlayerData LoadData()
    {
        var loadedData = DataPersistenceManager.Instance.LoadPlayer();
        playerBuild = loadedData.playerBuild;
        return loadedData;
    }
    public override void SaveData()
    {
        DataPersistenceManager.Instance.SavePlayer(this);
    }
}
