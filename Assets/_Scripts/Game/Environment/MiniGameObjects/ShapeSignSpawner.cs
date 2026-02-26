using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Manages visibility of editor-placed shape signs.
    /// Signs are positioned in the editor — this script only enables/disables them.
    /// </summary>
    public class ShapeSignSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class ShapeSignEntry
        {
            public GameObject signPrefab;
            public ShapeDefinition shapeDefinition;
        }

        [Header("Signs")]
        [SerializeField] List<ShapeSignEntry> signEntries;

        [Header("Placement")]
        [SerializeField] float ringRadius = 40f;
        [SerializeField] float height;
        [SerializeField] bool faceCenter = true;
        [SerializeField] float signScale = 0.3f;
        [SerializeField] Vector3 origin;

        /// <summary>Show all signs.</summary>
        public void ShowSigns()
        {
            if (signEntries == null) return;
            foreach (var entry in signEntries)
                if (entry?.signPrefab) entry.signPrefab.SetActive(true);
        }

        /// <summary>Hide all signs.</summary>
        public void HideSigns()
        {
            if (signEntries == null) return;
            foreach (var entry in signEntries)
                if (entry?.signPrefab) entry.signPrefab.SetActive(false);
        }
    }
}
