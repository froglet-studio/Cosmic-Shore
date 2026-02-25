using CosmicShore.Models.Enums;

﻿[System.Serializable]
public struct InputEventBlockPayload
{
    public InputEvents Input;
    public bool Ended;
    public float TotalSeconds;   
    public bool Started;         

}