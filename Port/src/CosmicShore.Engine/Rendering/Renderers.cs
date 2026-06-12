using System.Collections.Generic;
using CosmicShore.Engine;

namespace CosmicShore.Engine
{
    /// <summary>
    /// E2 renderer data stubs (VESSEL_LAYER.md). Headless-first: these hold the data
    /// ported code reads/writes (materials, blend-shape weights) and no-op the GPU
    /// side. A future render backend reads the same state. Unlike Unity, `material`
    /// does NOT clone — the engine has no instancing leak to defend against, and
    /// ported gameplay treats the returned instance as the thing to mutate.
    /// </summary>
    public class Renderer : Component
    {
        public bool enabled = true;

        Material[] _materials = System.Array.Empty<Material>();

        public Material[] materials
        {
            get => _materials;
            set => _materials = value ?? System.Array.Empty<Material>();
        }

        public Material[] sharedMaterials
        {
            get => _materials;
            set => _materials = value ?? System.Array.Empty<Material>();
        }

        public Material material
        {
            get => _materials.Length > 0 ? _materials[0] : null;
            set
            {
                if (_materials.Length == 0) _materials = new Material[1];
                _materials[0] = value;
            }
        }

        public Material sharedMaterial
        {
            get => material;
            set => material = value;
        }

        MaterialPropertyBlock _propertyBlock;

        /// <summary>Snapshot <paramref name="properties"/> onto this renderer (copy-on-set; null/empty clears).</summary>
        public void SetPropertyBlock(MaterialPropertyBlock properties)
        {
            if (properties == null || properties.isEmpty)
            {
                _propertyBlock = null;
                return;
            }
            _propertyBlock ??= new MaterialPropertyBlock();
            _propertyBlock.CopyFrom(properties);
        }

        /// <summary>Overwrite <paramref name="dest"/> with this renderer's current block (cleared if none set).</summary>
        public void GetPropertyBlock(MaterialPropertyBlock dest) => dest.CopyFrom(_propertyBlock);
    }

    public class MeshRenderer : Renderer
    {
    }

    public class SkinnedMeshRenderer : Renderer
    {
        readonly Dictionary<int, float> _blendShapeWeights = new();

        public void SetBlendShapeWeight(int index, float value) => _blendShapeWeights[index] = value;

        public float GetBlendShapeWeight(int index)
            => _blendShapeWeights.TryGetValue(index, out var w) ? w : 0f;
    }

    public class TrailRenderer : Renderer
    {
        public bool emitting = true;
        public float time;
        public float startWidth;
        public float endWidth;

        public void Clear() { }
    }
}
