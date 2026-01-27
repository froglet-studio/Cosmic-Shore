using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.Cinematics
{
    [CreateAssetMenu(
        fileName = "SceneCinematicLibrary",
        menuName = "ScriptableObjects/Cinematics/Scene Cinematic Library")]
    public class SceneCinematicLibrarySO : ScriptableObject
    {
        [Serializable]
        public struct SceneEntry
        {
            public string sceneName;
            public CinematicDefinitionSO cinematic;
        }

        [SerializeField] List<SceneEntry> entries = new();

        public bool TryGet(string sceneName, out CinematicDefinitionSO cinematic)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (!string.Equals(entries[i].sceneName, sceneName, StringComparison.Ordinal)) continue;
                cinematic = entries[i].cinematic;
                return cinematic;
            }

            cinematic = null;
            return false;
        }
    }
}