using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class HangerData 
{
    public string Bay001Ship;
    public string Bay002Ship;
    public string Bay003Ship;

    public string Bay001Pilot;
    public string Bay002Pilot;
    public string Bay003Pilot;

    // the constructor will provide the default values before a Hanger.data file exists
    public HangerData()
    {
        // Ships
        this.Bay001Ship = "Manta";
        this.Bay002Ship = "Dolphin";
        this.Bay003Ship = "Shark";

        // Pilots
        this.Bay001Pilot = "Zak";
        this.Bay002Pilot = "Milliron";
        this.Bay003Pilot = "Iggy";
    }
}
