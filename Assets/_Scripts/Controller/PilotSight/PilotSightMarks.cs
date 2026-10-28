using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The marks of a TARGET-SET sight (Docs/Pilots/CALLSIGNS.md §3): a soft disc with a hard ring,
    /// drawn through mass, on every thing the pilot sees. Echo Sight's halo, instanced
    /// (<c>PilotSightMark.shader</c>): a whole frame's marks are one <see cref="Graphics.RenderMeshInstanced"/>
    /// per 1023, whether the sight finds three vessels or four hundred creature hearts, and nothing is
    /// added to the scene (no GameObject, no collider, no material clone).
    ///
    /// Photons only. Nothing reads a mark.
    /// </summary>
    public sealed class PilotSightMarks
    {
        const string MaterialResourcePath = "Pilots/PilotSightMark";
        const int MaxPerDraw = 1023;

        static readonly int MarkColorId = Shader.PropertyToID("_MarkColor");
        static readonly int MarkRadiusId = Shader.PropertyToID("_MarkRadius");
        static readonly int MinScreenRadiusId = Shader.PropertyToID("_MinScreenRadius");

        readonly List<Matrix4x4> _matrices = new(64);
        readonly List<Vector4> _colors = new(64);
        readonly List<float> _radii = new(64);

        // Fixed at the instanced-draw maximum: no per-frame allocation, and a property block's
        // array size is fixed by its first write, so it is always written at this length.
        readonly Matrix4x4[] _matrixChunk = new Matrix4x4[MaxPerDraw];
        readonly Vector4[] _colorChunk = new Vector4[MaxPerDraw];
        readonly float[] _radiusChunk = new float[MaxPerDraw];

        Material _material;
        bool _materialResolved;
        Mesh _quad;
        MaterialPropertyBlock _block;

        /// <summary>Marks gathered this frame.</summary>
        public int Count => _matrices.Count;

        public void Clear()
        {
            _matrices.Clear();
            _colors.Clear();
            _radii.Clear();
        }

        /// <summary>Mark a thing at <paramref name="position"/>.</summary>
        public void Add(Vector3 position, float radius, Color color, float intensity)
        {
            _matrices.Add(Matrix4x4.Translate(position));
            _colors.Add(new Vector4(color.r, color.g, color.b, intensity));
            _radii.Add(radius);
        }

        /// <summary>Draw the gathered marks this frame.</summary>
        public void Draw(float minScreenRadius)
        {
            if (_matrices.Count == 0) return;
            if (!EnsureResources()) return;

            _material.SetFloat(MinScreenRadiusId, minScreenRadius);
            var rp = new RenderParams(_material)
            {
                matProps = _block,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 1e7f),
            };

            for (int start = 0; start < _matrices.Count; start += MaxPerDraw)
            {
                int n = Mathf.Min(MaxPerDraw, _matrices.Count - start);
                _matrices.CopyTo(start, _matrixChunk, 0, n);
                _colors.CopyTo(start, _colorChunk, 0, n);
                _radii.CopyTo(start, _radiusChunk, 0, n);

                _block.Clear();
                _block.SetVectorArray(MarkColorId, _colorChunk);
                _block.SetFloatArray(MarkRadiusId, _radiusChunk);
                Graphics.RenderMeshInstanced(rp, _quad, 0, _matrixChunk, n);
            }
        }

        bool EnsureResources()
        {
            if (!_materialResolved)
            {
                _materialResolved = true;
                _material = Resources.Load<Material>(MaterialResourcePath);
                if (!_material)
                    CSDebug.LogWarning($"[PilotSight] No Resources/{MaterialResourcePath} material - the sight " +
                                       "draws no marks. Run Tools/Build/author_callsign_assets.py.");
                _quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                _block = new MaterialPropertyBlock();
            }
            return _material && _quad;
        }
    }
}
