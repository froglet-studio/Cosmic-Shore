using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.ShapeDrawing
{
    /// <summary>
    /// Spawns ShapeSign prefabs in a simple ring around the origin.
    /// Completely independent of SegmentSpawner — drop this anywhere in the scene.
    ///
    /// Signs are evenly spaced in a circle at a configurable radius and height.
    /// On game start they spawn once and just float there.
    ///
    /// Inspector setup:
    ///   - signEntries      : one entry per shape (prefab + definition)
    ///   - ringRadius       : how far from origin the ring sits
    ///   - height           : y position of the signs
    ///   - faceCenter       : if true, signs rotate to face the center (good for a ring)
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
        [SerializeField] float ringRadius = 300f;
        [SerializeField] float height = 0f;
        [SerializeField] bool faceCenter = true;

        [Header("Origin")]
        [Tooltip("Signs spawn around this point. Leave at (0,0,0) or set to your spawn area center.")]
        [SerializeField] Vector3 origin = Vector3.zero;

        // Keep references so we can destroy/respawn if needed later
        readonly List<GameObject> _spawnedSigns = new();

        void Start()
        {
            SpawnSigns();
        }

        public void SpawnSigns()
        {
            // Clean up any existing signs first
            foreach (var s in _spawnedSigns)
                if (s) Destroy(s);
            _spawnedSigns.Clear();

            if (signEntries == null || signEntries.Count == 0) return;

            int count = signEntries.Count;

            for (int i = 0; i < count; i++)
            {
                var entry = signEntries[i];
                if (entry.signPrefab == null) continue;

                // Evenly distribute around a circle
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 pos = origin + new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    height,
                    Mathf.Sin(angle) * ringRadius);

                var go = Instantiate(entry.signPrefab, pos, Quaternion.identity, transform);

                // Optionally face the center
                if (faceCenter)
                {
                    Vector3 dirToCenter = (origin - pos).normalized;
                    if (dirToCenter != Vector3.zero)
                        go.transform.rotation = Quaternion.LookRotation(dirToCenter, Vector3.up);
                }

                // Inject the shape definition
                var sign = go.GetComponent<ShapeSign>();
                if (sign)
                    sign.Initialize(entry.shapeDefinition);
                else
                    Debug.LogWarning($"[ShapeSignSpawner] Prefab '{entry.signPrefab.name}' has no ShapeSign component.");

                _spawnedSigns.Add(go);
            }

            Debug.Log($"[ShapeSignSpawner] Spawned {_spawnedSigns.Count} shape signs.");
        }

        /// <summary>
        /// Hide all signs — call this when shape drawing mode starts.
        /// ShapeDrawingManager calls this automatically if you wire it in.
        /// </summary>
        public void HideSigns()
        {
            foreach (var s in _spawnedSigns)
                if (s) s.SetActive(false);
        }

        /// <summary>
        /// Show all signs again — call this when returning to freestyle.
        /// </summary>
        public void ShowSigns()
        {
            foreach (var s in _spawnedSigns)
                if (s) s.SetActive(true);
        }
    }
}