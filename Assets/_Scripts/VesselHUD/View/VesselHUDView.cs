using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game
{
    public abstract class VesselHUDView : MonoBehaviour
    {
        [Serializable]
        public struct HighlightBinding
        {
            public InputEvents input;
            public Image image;
        }

        [Header("Button highlights")] public List<HighlightBinding> highlights = new();
        public abstract void Initialize();
        public void Show() => gameObject.SetActive(true);
        public void Hide() => gameObject.SetActive(false);

        internal GameObject TrailBlockPrefab;
    }
}