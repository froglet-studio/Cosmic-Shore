using System;
using System.Text;

namespace CosmicShore.Content.Models
{
    /// <summary>
    /// The local fileIDs Unity's ModelImporter assigns to the objects it generates from a
    /// model file, for <c>fileIdsGeneration: 2</c> (every FBX meta in this project, with
    /// <c>internalIDToNameTable</c> holding only animation clips).
    ///
    /// Reverse-engineered against 291 references the project's prefabs make into 46 FBX
    /// files (every mesh, GameObject, Transform and renderer reference resolves):
    /// <code>
    ///   fileID = (long) xxHash64(UTF8("Type:" + ClassName + "->" + Key + Occurrence), seed 0)
    /// </code>
    /// where
    ///   • a Mesh's Key is its NODE (Model) name — not the Geometry name — and Occurrence
    ///     counts earlier meshes of the same name (0, 1, …);
    ///   • a GameObject's Key is its hierarchy path, rooted at the literal
    ///     <c>//RootNode/root</c> for the model prefab root and continuing
    ///     <c>/child/grandchild</c> by node name;
    ///   • a component's Key is its GameObject's path + "/" + ClassName
    ///     (<c>Type:Transform->//RootNode/root/Transform0</c> = -8679921383154817045, the
    ///     root Transform id every FBX shares; the root GameObject is 919132149155446097).
    /// </summary>
    public static class ModelFileIds
    {
        public const string RootPath = "//RootNode/root";
        /// <summary>The model prefab asset itself (<c>m_SourcePrefab: {fileID: 100100000}</c>).</summary>
        public const long PrefabAsset = 100100000;

        public static long Hash(string className, string key, int occurrence = 0)
            => (long)XxHash64(Encoding.UTF8.GetBytes("Type:" + className + "->" + key + occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        public static long Mesh(string nodeName, int occurrence = 0) => Hash("Mesh", nodeName, occurrence);
        public static long GameObject(string path, int occurrence = 0) => Hash("GameObject", path, occurrence);
        public static long Component(string goPath, string className, int occurrence = 0) => Hash(className, goPath + "/" + className, occurrence);
        public static long Material(string name, int occurrence = 0) => Hash("Material", name, occurrence);

        // ── xxHash64 (Yann Collet), seed 0 ─────────────────────────────────

        const ulong P1 = 11400714785074694791UL, P2 = 14029467366897019727UL, P3 = 1609587929392839161UL,
                    P4 = 9650029242287828579UL, P5 = 2870177450012600261UL;

        static ulong Rotl(ulong x, int r) => (x << r) | (x >> (64 - r));
        static ulong Round(ulong acc, ulong input) { acc += input * P2; acc = Rotl(acc, 31); return acc * P1; }
        static ulong Merge(ulong acc, ulong val) { val = Round(0, val); acc ^= val; return acc * P1 + P4; }

        public static ulong XxHash64(ReadOnlySpan<byte> data, ulong seed = 0)
        {
            int len = data.Length, i = 0;
            ulong h;
            if (len >= 32)
            {
                ulong v1 = seed + P1 + P2, v2 = seed + P2, v3 = seed, v4 = seed - P1;
                while (i <= len - 32)
                {
                    v1 = Round(v1, BitConverter.ToUInt64(data.Slice(i))); i += 8;
                    v2 = Round(v2, BitConverter.ToUInt64(data.Slice(i))); i += 8;
                    v3 = Round(v3, BitConverter.ToUInt64(data.Slice(i))); i += 8;
                    v4 = Round(v4, BitConverter.ToUInt64(data.Slice(i))); i += 8;
                }
                h = Rotl(v1, 1) + Rotl(v2, 7) + Rotl(v3, 12) + Rotl(v4, 18);
                h = Merge(h, v1); h = Merge(h, v2); h = Merge(h, v3); h = Merge(h, v4);
            }
            else h = seed + P5;
            h += (ulong)len;
            while (i + 8 <= len)
            {
                h ^= Round(0, BitConverter.ToUInt64(data.Slice(i)));
                h = Rotl(h, 27) * P1 + P4;
                i += 8;
            }
            if (i + 4 <= len)
            {
                h ^= BitConverter.ToUInt32(data.Slice(i)) * P1;
                h = Rotl(h, 23) * P2 + P3;
                i += 4;
            }
            while (i < len)
            {
                h ^= data[i] * P5;
                h = Rotl(h, 11) * P1;
                i++;
            }
            h ^= h >> 33; h *= P2;
            h ^= h >> 29; h *= P3;
            h ^= h >> 32;
            return h;
        }
    }
}
