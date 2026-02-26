using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Manages ShapeSign spawning and visibility in the freestyle lobby.
    /// Uses signEntries to instantiate signs once, then shows/hides them.
    /// Positions, rotations, and scales are set by the entry configuration —
    /// once spawned, this script never modifies their transforms.
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

        readonly List<GameObject> _spawnedSigns = new();
        bool _hasSpawned;

        /// <summary>Show all signs, spawning them on first call.</summary>
        public void ShowSigns()
        {
            if (!_hasSpawned)
                SpawnSigns();

            foreach (var s in _spawnedSigns)
                if (s) s.SetActive(true);
        }

        /// <summary>Hide all signs.</summary>
        public void HideSigns()
        {
            foreach (var s in _spawnedSigns)
                if (s) s.SetActive(false);
        }

        void SpawnSigns()
        {
            _hasSpawned = true;

            if (signEntries is not { Count: > 0 }) return;

            int count = signEntries.Count;

            for (int i = 0; i < count; i++)
            {
                var entry = signEntries[i];
                if (entry.signPrefab == null) continue;

                float angle = (i / (float)count) * Mathf.PI * 2f;
                var pos = origin + new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    height,
                    Mathf.Sin(angle) * ringRadius);

                var go = Instantiate(entry.signPrefab, pos, Quaternion.identity, transform);
                go.transform.localScale = Vector3.one * signScale;

                if (faceCenter)
                {
                    var dirToCenter = (origin - pos).normalized;
                    if (dirToCenter != Vector3.zero)
                        go.transform.rotation = Quaternion.LookRotation(dirToCenter, Vector3.up);
                }

                var sign = go.GetComponent<ShapeSign>();
                if (sign)
                    sign.Initialize(entry.shapeDefinition);

                _spawnedSigns.Add(go);
            }
        }
    }
}
