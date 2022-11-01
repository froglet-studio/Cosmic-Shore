using System.Collections.Generic;

[System.Serializable]
public class PlayerData 
{
    public string playerName;
    public int highestScore;

    public Dictionary<string, string> playerBuild;

    public PlayerData()
    {
        this.playerName = "Default_Name";
        this.highestScore = 0;

        this.playerBuild = new Dictionary<string, string>();

        playerBuild.Add("Pilot", "Default_Pilot");
        playerBuild.Add("Ship", "Default_Ship");
        playerBuild.Add("Trail", "Default_Trail");
    }
}
