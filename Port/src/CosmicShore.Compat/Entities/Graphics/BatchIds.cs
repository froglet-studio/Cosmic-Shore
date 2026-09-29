using System;

// UnityEngine.Rendering.BatchMeshID / BatchMaterialID — the live-src sync maps
// UnityEngine.Rendering onto CosmicShore.Engine.Rendering, so they live there.
namespace CosmicShore.Engine.Rendering
{
    /// <summary>Handle to a mesh registered with the BatchRendererGroup (Entities Graphics).</summary>
    public struct BatchMeshID : IEquatable<BatchMeshID>, IComparable<BatchMeshID>
    {
        public uint value;
        public static readonly BatchMeshID Null = default;
        public bool Equals(BatchMeshID other) => value == other.value;
        public override bool Equals(object obj) => obj is BatchMeshID id && Equals(id);
        public override int GetHashCode() => (int)value;
        public int CompareTo(BatchMeshID other) => value.CompareTo(other.value);
        public static bool operator ==(BatchMeshID a, BatchMeshID b) => a.value == b.value;
        public static bool operator !=(BatchMeshID a, BatchMeshID b) => a.value != b.value;
        public override string ToString() => $"BatchMeshID({value})";
    }

    /// <summary>Handle to a material registered with the BatchRendererGroup (Entities Graphics).</summary>
    public struct BatchMaterialID : IEquatable<BatchMaterialID>, IComparable<BatchMaterialID>
    {
        public uint value;
        public static readonly BatchMaterialID Null = default;
        public bool Equals(BatchMaterialID other) => value == other.value;
        public override bool Equals(object obj) => obj is BatchMaterialID id && Equals(id);
        public override int GetHashCode() => (int)value;
        public int CompareTo(BatchMaterialID other) => value.CompareTo(other.value);
        public static bool operator ==(BatchMaterialID a, BatchMaterialID b) => a.value == b.value;
        public static bool operator !=(BatchMaterialID a, BatchMaterialID b) => a.value != b.value;
        public override string ToString() => $"BatchMaterialID({value})";
    }
}
