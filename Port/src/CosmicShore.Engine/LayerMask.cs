using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// 32-layer mask with a name registry. Defaults mirror the original project's
    /// layer table (ProjectSettings/TagManager.asset) — built-in engine layers 0-5
    /// plus the game's project layers 6-18 — so NameToLayer resolves the same indices
    /// the original engine did. Additional layers register via <see cref="SetLayerName"/>.
    /// </summary>
    [Serializable]
    public struct LayerMask
    {
        public int value;

        static readonly Dictionary<string, int> NameToIndex = new()
        {
            ["Default"] = 0,
            ["TransparentFX"] = 1,
            ["Ignore Raycast"] = 2,
            ["Water"] = 4,
            ["UI"] = 5,
            // Project layers ported from TagManager.asset:
            ["3D UI"] = 6,
            ["Skimmers"] = 7,
            ["Ships"] = 8,
            ["Crystals"] = 9,
            ["Explosions"] = 10,
            ["TrailBlocks"] = 11,
            ["Projectiles"] = 12,
            ["Skybox"] = 13,
            ["Shards"] = 14,
            ["Cage"] = 15,
            ["Automata"] = 16,
            ["Mound"] = 17,
            ["TrailBlockOcclusion"] = 18,
        };

        static readonly string[] IndexToName = CreateIndexToName();

        static string[] CreateIndexToName()
        {
            var names = new string[32];
            foreach (var pair in NameToIndex) names[pair.Value] = pair.Key;
            return names;
        }

        public static implicit operator int(LayerMask mask) => mask.value;
        public static implicit operator LayerMask(int intVal) => new() { value = intVal };

        /// <summary>Returns the layer index for a name, or -1 if unregistered.</summary>
        public static int NameToLayer(string layerName)
            => NameToIndex.TryGetValue(layerName, out int layer) ? layer : -1;

        public static string LayerToName(int layer)
            => layer is >= 0 and < 32 ? IndexToName[layer] ?? string.Empty : string.Empty;

        public static int GetMask(params string[] layerNames)
        {
            int mask = 0;
            foreach (string layerName in layerNames)
            {
                int layer = NameToLayer(layerName);
                if (layer == -1)
                {
                    Debug.LogError($"Layer '{layerName}' not found.");
                    continue;
                }
                mask |= 1 << layer;
            }
            return mask;
        }

        /// <summary>Register or rename a project layer (ported from project settings).</summary>
        public static void SetLayerName(int layer, string layerName)
        {
            if (layer is < 0 or >= 32) throw new ArgumentOutOfRangeException(nameof(layer));
            if (IndexToName[layer] != null) NameToIndex.Remove(IndexToName[layer]);
            IndexToName[layer] = layerName;
            NameToIndex[layerName] = layer;
        }
    }
}
