using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public class VesselHUDView : MonoBehaviour
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
        public Transform silhouetteContainer;
        public RectTransform trailDisplayContainer;
        
        [Header("Silhouette Parts")]
        public List<GameObject> silhouetteParts = new();
        
        [Header("Jaws (Resource Meter)")]
        public Image topJaw;
        public Image bottomJaw;
        public int jawResourceIndex = -1;
        
        [FormerlySerializedAs("prismSpawner")] [FormerlySerializedAs("trailSpawner")] [Header("Trail / Drift Sources")]
        public VesselPrismController vesselPrismController;
        public DriftTrailActionExecutor driftTrailAction;

        
        [Header("Trail Config (UI scaling only)")]
        public float worldToUIScale = 2f;
        public float imageScale = 0.02f;
        public bool swingBlocks;
        
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);
    }
}
