using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Utility.Recording
{
    [Serializable]
    public class VideoMacro
    {
        public string Name = "New Macro";
        public Key TriggerKey = Key.None;

        [SerializeReference]
        public List<VideoMacroAction> Actions = new();
    }
}
