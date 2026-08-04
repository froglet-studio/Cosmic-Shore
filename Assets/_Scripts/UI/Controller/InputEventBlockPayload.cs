using CosmicShore.Data;
using System;

namespace CosmicShore.UI
{
    ﻿[System.Serializable]
    public struct InputEventBlockPayload
    {
        public InputEvents Input;
        public bool Ended;
        public float TotalSeconds;   
        public bool Started;         

    }
}
