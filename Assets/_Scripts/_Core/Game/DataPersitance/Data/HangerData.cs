using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
public class HangerData 
{
    public SerializableDictionary<string, PlayerBuild> PlayerBuilds;

    public HangerData()
    {
        //Default builds if HangerData.data file doesn't exist
        PlayerBuild DefaultPlayerBuild001 = new PlayerBuild("Zak", "Manta", "GreenTrail");
        PlayerBuild DefaultPlayerBuild002 = new PlayerBuild("Milliron", "Dolphin", "BlueTrail");
        PlayerBuild DefaultPlayerBuild003 = new PlayerBuild("Iggy", "Shark", "RedTrail");

        PlayerBuilds = new SerializableDictionary<string, PlayerBuild>();

        PlayerBuilds.Add("DefaultPlayerBuild001", DefaultPlayerBuild001);
        PlayerBuilds.Add("DefaultPlayerBuild002", DefaultPlayerBuild002);
        PlayerBuilds.Add("DefaultPlayerBuild003", DefaultPlayerBuild003);

    }

    //public void AddCustomPlayerBuild(string buildName, string pilot, string ship, string trail)
    //{
    //    PlayerBuild newPlayerBuild = new PlayerBuild(pilot, ship, trail);
    //    PlayerBuilds.Add(buildName, newPlayerBuild);
    //}

    //public void RemoveCustomPlayerBuild(string buildName)
    //{
    //    if (buildName == "DefaultPlayerBuild001" || buildName == "DefaultPlayerBuild002" || buildName == "DefaultPlayerBuild003") 
    //        return;

    //    PlayerBuilds.Remove(buildName);
    //}
}
