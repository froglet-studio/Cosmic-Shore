using System;
using System.Collections.Generic;
using UnityEngine;
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

        [Header("Button highlights")] public List<HighlightBinding> highlights = new();

        // [Header("Delegates")] [SerializeField] private VesselSilhouetteController silhouetteController;
        // [SerializeField] private UITrailController uiTrailController;

        // public VesselSilhouetteController Silhouette => silhouetteController;
        // public UITrailController TrailUI => uiTrailController;

        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        [Header("Silhouette + Trail Roots")] public Transform SilhouetteContainer;
        public RectTransform TrailDisplayContainer;

        [Header("Jaws (optional)")] public Image TopJaw;
        public Image BottomJaw;
        public int JawResourceIndex = -1;

        internal GameObject TrailBlockPrefab;
    }
}