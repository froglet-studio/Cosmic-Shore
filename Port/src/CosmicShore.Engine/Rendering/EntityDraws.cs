using System;
using System.Collections.Generic;

namespace CosmicShore.Engine.Rendering
{
    /// <summary>
    /// The seam between the Entities Graphics emulation and the renderer: each frame the
    /// renderer hands an <see cref="EntityDrawList"/> to <see cref="Collect"/>, and whatever
    /// draws entities (the compat EntitiesGraphicsSystem) appends one record per visible
    /// entity — mesh, material, submesh, world matrix, layer, and the per-instance values of
    /// every component bound to a shader property ([MaterialProperty]). Values are uploaded
    /// verbatim, as Entities Graphics does.
    /// </summary>
    public static class EntityDraws
    {
        public static Action<EntityDrawList> Collect;
    }

    public sealed class EntityDrawList
    {
        /// <summary>Most distinct per-instance properties one draw can carry (bit width of <see cref="Mask"/>).</summary>
        public const int MaxSlots = 64;

        static readonly Dictionary<string, int> s_slots = new(StringComparer.Ordinal);
        static readonly List<string> s_names = new();

        /// <summary>Stable slot for a shader property name (registered on first use); -1 when the table is full.</summary>
        public static int Slot(string property)
        {
            lock (s_slots)
            {
                if (s_slots.TryGetValue(property, out var s)) return s;
                if (s_names.Count >= MaxSlots) return -1;
                s = s_names.Count;
                s_slots[property] = s;
                s_names.Add(property);
                return s;
            }
        }

        public static string SlotName(int slot) => slot >= 0 && slot < s_names.Count ? s_names[slot] : null;

        public int Count;
        public Mesh[] Meshes = new Mesh[256];
        public Material[] Materials = new Material[256];
        public int[] Submeshes = new int[256];
        public int[] Layers = new int[256];
        public Matrix4x4[] Matrices = new Matrix4x4[256];
        public ulong[] Mask = new ulong[256];
        public Vector4[] Props = new Vector4[256 * MaxSlots];

        public void Clear() => Count = 0;

        public int Add(Mesh mesh, Material material, int submesh, in Matrix4x4 localToWorld, int layer)
        {
            if (Count == Meshes.Length)
            {
                int n = Count * 2;
                Array.Resize(ref Meshes, n);
                Array.Resize(ref Materials, n);
                Array.Resize(ref Submeshes, n);
                Array.Resize(ref Layers, n);
                Array.Resize(ref Matrices, n);
                Array.Resize(ref Mask, n);
                Array.Resize(ref Props, n * MaxSlots);
            }
            int i = Count++;
            Meshes[i] = mesh;
            Materials[i] = material;
            Submeshes[i] = submesh;
            Layers[i] = layer;
            Matrices[i] = localToWorld;
            Mask[i] = 0;
            return i;
        }

        public void Set(int index, int slot, Vector4 value)
        {
            if (slot < 0) return;
            Props[index * MaxSlots + slot] = value;
            Mask[index] |= 1UL << slot;
        }

        public bool TryGet(int index, int slot, out Vector4 value)
        {
            if (slot >= 0 && (Mask[index] & (1UL << slot)) != 0) { value = Props[index * MaxSlots + slot]; return true; }
            value = default;
            return false;
        }
    }
}
