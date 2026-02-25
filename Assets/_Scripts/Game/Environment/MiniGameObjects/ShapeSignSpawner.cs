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
        [SerializeField] float ringRadius = 40f;
        [SerializeField] float height = 0f;
        [SerializeField] bool faceCenter = true;
        [SerializeField] float signScale = 0.3f;

        [Header("Origin")]
        [Tooltip("Signs spawn around this point. Leave at (0,0,0) or set to your spawn area center.")]
        [SerializeField] Vector3 origin = Vector3.zero;

        // Keep references so we can destroy/respawn if needed later
        readonly List<GameObject> _spawnedSigns = new();

        void Start()
        {
            // Don't auto-spawn at Start — controller calls ShowSigns with player position
        }

        public void SpawnSigns(Vector3 center) => SpawnSigns(center, center);

        public void SpawnSigns(Vector3 center, Vector3 faceTarget)
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

                // Evenly distribute around a circle centered on the given position
                float angle = (i / (float)count) * Mathf.PI * 2f;
                Vector3 pos = center + new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    height,
                    Mathf.Sin(angle) * ringRadius);

                var go = Instantiate(entry.signPrefab, pos, Quaternion.identity, transform);
                go.transform.localScale *= signScale;

                // Face the specified target (typically the player position)
                if (faceCenter)
                {
                    Vector3 dirToFace = (faceTarget - pos).normalized;
                    if (dirToFace != Vector3.zero)
                        go.transform.rotation = Quaternion.LookRotation(dirToFace, Vector3.up);
                }

                // Inject the shape definition
                var sign = go.GetComponent<ShapeSign>();
                if (sign)
                    sign.Initialize(entry.shapeDefinition);
                else
                    Debug.LogWarning($"[ShapeSignSpawner] Prefab '{entry.signPrefab.name}' has no ShapeSign component.");

                _spawnedSigns.Add(go);
            }

            Debug.Log($"[ShapeSignSpawner] Spawned {_spawnedSigns.Count} shape signs around {center}.");
        }

        /// <summary>
        /// Hide all signs — call this when shape drawing mode starts.
        /// </summary>
        public void HideSigns()
        {
            foreach (var s in _spawnedSigns)
                if (s) s.SetActive(false);
        }

        /// <summary>
        /// Respawn signs around the given center (typically the player position).
        /// Destroys old signs and creates fresh ones.
        /// </summary>
        public void ShowSigns(Vector3 center) => SpawnSigns(center);

        /// <summary>
        /// Respawn signs around center, with all signs rotated to face faceTarget.
        /// </summary>
        public void ShowSigns(Vector3 center, Vector3 faceTarget) => SpawnSigns(center, faceTarget);

        /// <summary>
        /// Show existing signs without repositioning.
        /// </summary>
        public void ShowSigns()
        {
            foreach (var s in _spawnedSigns)
                if (s) s.SetActive(true);
        }
    }
}