using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>The Pistol's world drawn AS THE GAME DRAWS IT (Garrett, 2026-10-18: "as true to the game as possible"): every
    /// prism, trail block and the Goby's own hull with their OWN meshes and materials, lit by the scene's lights under the real
    /// skybox, through a real perspective camera - only positions and orientations are taken through the unroll (the flat
    /// frame at the stage), and a prism the Goby laps has its own mesh deformed into the bar. No tint, no fog, no glass, no
    /// shader of its own. Rigid prisms are instanced per (mesh, submesh, material) - the one draw API Unity and Amoebius
    /// share - and a warped prism is a pooled mesh drawn once; a material without instancing goes through RenderMesh.</summary>
    public sealed class PistolTrueDraw
    {
        public const int MaxPooled = 420;   // warped prisms a frame (GobyPistolDuo.MaxWarped + the bars)

        sealed class Batch { public Mesh Mesh; public int Sub; public Material Material; public Matrix4x4[] M = new Matrix4x4[64]; public int N; }
        readonly List<Batch> _batches = new();
        int _used;
        readonly List<Mesh> _pool = new();
        int _pooled;
        readonly List<Vector3> _verts = new(512);
        Matrix4x4 _stage = Matrix4x4.identity;
        Bounds _bounds;
        public int Draws { get; private set; }
        public int Instances { get; private set; }

        /// <summary>Start a frame: the flat frame's place in the world, and the bounds every draw is given (the camera's reach).</summary>
        public void Begin(in Matrix4x4 stage, in Bounds worldBounds)
        {
            _stage = stage; _bounds = worldBounds; _pooled = 0; Draws = 0; Instances = 0;
            for (int i = 0; i < _used; i++) _batches[i].N = 0;
            _used = 0;
        }

        /// <summary>A mesh as it is, at <paramref name="flat"/> (local to the flat world; <see cref="PistolWarp.FlatTransform"/>), with
        /// its own materials (one per submesh; the last one covers the rest, as a Renderer does).</summary>
        public void Rigid(Mesh mesh, Material[] materials, in Matrix4x4 flat)
        {
            if (!mesh || materials == null || materials.Length == 0) return;
            var world = _stage * flat;
            int subs = Mathf.Max(1, mesh.subMeshCount);
            for (int s = 0; s < subs; s++)
            {
                var mat = materials[Mathf.Min(s, materials.Length - 1)];
                if (!mat) continue;
                Batch b = null;
                for (int i = 0; i < _used; i++) if (_batches[i].Mesh == mesh && _batches[i].Sub == s && _batches[i].Material == mat) { b = _batches[i]; break; }
                if (b == null)
                {
                    if (_used == _batches.Count) _batches.Add(new Batch());
                    b = _batches[_used++]; b.Mesh = mesh; b.Sub = s; b.Material = mat; b.N = 0;
                }
                if (b.N == b.M.Length) { if (b.N >= 1023) continue; System.Array.Resize(ref b.M, Mathf.Min(1023, b.N * 2)); }
                b.M[b.N++] = world;
            }
            Instances++;
        }

        /// <summary>The prism's own mesh deformed inside the warped box <paramref name="xhd8"/> (<see cref="PistolWarp.BoxCorners"/>'s
        /// order), in its own materials: a pooled mesh, its vertices trilinear in the box, its normals recomputed, its uv kept.</summary>
        public void Warped(PistolWorldMesh.MeshData data, Vector3[] xhd8, Material[] materials)
        {
            if (data == null || materials == null || materials.Length == 0 || _pooled >= MaxPooled) return;
            if (_pooled == _pool.Count) _pool.Add(new Mesh { name = "PistolWarped", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt16 });
            var mesh = _pool[_pooled++];
            int nv = data.Local01.Length;
            _verts.Clear();
            for (int i = 0; i < nv; i++) { var l = data.Local01[i]; _verts.Add(PistolWarp.Trilinear(xhd8, l.x, l.y, l.z)); }
            mesh.Clear();
            mesh.SetVertices(_verts);
            mesh.SetUVs(0, data.UV);
            mesh.subMeshCount = data.SubTris.Length;
            for (int s = 0; s < data.SubTris.Length; s++) mesh.SetTriangles(data.SubTris[s], s, false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            for (int s = 0; s < data.SubTris.Length; s++)
            {
                var mat = materials[Mathf.Min(s, materials.Length - 1)];
                if (!mat) continue;
                var rp = new RenderParams(mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = _bounds };
                Graphics.RenderMesh(rp, mesh, s, _stage);
                Draws++;
            }
            Instances++;
        }

        /// <summary>Draw the frame's rigid batches.</summary>
        public void End()
        {
            for (int i = 0; i < _used; i++)
            {
                var b = _batches[i]; if (b.N == 0) continue;
                var rp = new RenderParams(b.Material) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false, worldBounds = _bounds };
                if (b.Material.enableInstancing) { Graphics.RenderMeshInstanced(rp, b.Mesh, b.Sub, b.M, b.N); Draws++; }
                else for (int k = 0; k < b.N; k++) { Graphics.RenderMesh(rp, b.Mesh, b.Sub, b.M[k]); Draws++; }
                b.N = 0;
            }
            _used = 0;
        }

        public void Dispose()
        {
            foreach (var m in _pool) if (m) Object.Destroy(m);
            _pool.Clear(); _pooled = 0; _batches.Clear(); _used = 0;
        }
    }
}
