using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class ShipHUDView : MonoBehaviour, IShipHUDView
    {
        [Serializable]
        public struct HighlightBinding
        {
            public InputEvents input;  
            public Image image;        
        }
        
        [Header("Button highlights")]
        public List<HighlightBinding> highlights = new();
    }
}
