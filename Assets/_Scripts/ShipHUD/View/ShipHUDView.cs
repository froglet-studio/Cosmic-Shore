using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class ShipHUDView : MonoBehaviour
    {
        [Serializable]
        public struct HighlightBinding
        {
            public InputEvents input;  
            public Image image;        
        }
        
        [Header("Button highlights")]
        public List<HighlightBinding> highlights = new();
        
        [Header("Silhouette Containers")]
        public RectTransform silhouetteContainer;
        public RectTransform trailDisplayContainer;
        
        [Header("Silhouette Parts")]
        public List<GameObject> silhouetteParts = new();
        
        [Header("Jaws (Resource Meter)")]
        public Image topJaw;
        public Image bottomJaw;
        public int jawResourceIndex = -1;
        
        [Header("Trail / Drift Sources")]
        public TrailSpawner trailSpawner;
        public DriftTrailAction driftTrailAction;

        
        [Header("Trail Config (UI scaling only)")]
        public float worldToUIScale = 2f;
        public float imageScale = 0.02f;
        public bool swingBlocks;
    }
}
