using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Registry of prefabs that AppManager instantiates during bootstrap.
    ///
    /// By moving scene objects into prefabs and listing them here, the Bootstrap
    /// scene file stays nearly empty — all future additions/removals are tiny
    /// SO-asset diffs instead of monolithic scene YAML conflicts.
    ///
    /// Each entry can optionally be marked DontDestroyOnLoad and given a spawn
    /// position/rotation for objects that need specific transforms (e.g., cameras).
    /// </summary>
    [CreateAssetMenu(
        fileName = "BootstrapPrefabRegistry",
        menuName = "ScriptableObjects/Core/BootstrapPrefabRegistry")]
    public class BootstrapPrefabRegistrySO : ScriptableObject
    {
        [SerializeField, Tooltip("Prefabs to instantiate at bootstrap time, in order.")]
        BootstrapPrefabEntry[] _entries = Array.Empty<BootstrapPrefabEntry>();

        public IReadOnlyList<BootstrapPrefabEntry> Entries => _entries;
    }

    [Serializable]
    public struct BootstrapPrefabEntry
    {
        [Tooltip("The prefab to instantiate.")]
        public GameObject Prefab;

        [Tooltip("Mark the instantiated object as DontDestroyOnLoad.")]
        public bool Persistent;

        [Tooltip("Optional: override the prefab's default position.")]
        public Vector3 Position;

        [Tooltip("Optional: override the prefab's default rotation.")]
        public Vector3 Rotation;
    }
}
