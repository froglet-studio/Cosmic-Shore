using System.Collections.Generic;

[System.Serializable]
public class PlayerData 
{
    public string playerName;
    public int highestScore;

    public Dictionary<string, string> playerBuild;

    public PlayerData()
    {
        playerName = "Default Name";
        highestScore = 0;

        playerBuild.Add("Pilot", "Zak");
        playerBuild.Add("Ship", "Manta");
        playerBuild.Add("Trail", "Green");
    }
}
