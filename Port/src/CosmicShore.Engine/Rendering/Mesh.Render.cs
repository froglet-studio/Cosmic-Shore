using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    public partial class Mesh
    {
        // Zero-copy views for the render backend. The public accessors copy on read (the
        // original contract), which a per-frame upload check cannot afford. Every write path
        // replaces the backing array, so array identity is also the change signal.

        /// <summary>Backing vertex buffer (do not mutate).</summary>
        public Vector3[] RenderVertices => _vertices;
        public Vector3[] RenderNormals => _normals;
        public Vector2[] RenderUv => _uv;
        public Vector4[] RenderTangents => _tangents;
        public Color[] RenderColors => _colors;
        public BoneWeight[] RenderBoneWeights => _boneWeights;
        public Matrix4x4[] RenderBindposes => _bindposes;
        public int RenderSubmeshCount => _submeshes.Count;
        public int[] RenderSubmesh(int i) => _submeshes[i];
    }

    public partial class Renderer
    {
        static readonly List<Renderer> s_live = new();
        static readonly List<TrailRenderer> s_trails = new();
        internal static void RegisterLive(Renderer r)
        {
            s_live.Add(r);
            if (r is TrailRenderer t) s_trails.Add(t);
            r.MarkRenderDirty();
        }

        /// <summary>Every trail that still exists (destroyed entries pruned).</summary>
        public static void CollectLiveTrails(List<TrailRenderer> into)
        {
            into.Clear();
            int w = 0;
            for (int i = 0; i < s_trails.Count; i++)
            {
                var r = s_trails[i];
                if (r.destroyedFlag || r.gameObject == null || r.gameObject.destroyedFlag) continue;
                s_trails[w++] = r;
                into.Add(r);
            }
            s_trails.RemoveRange(w, s_trails.Count - w);
        }

        /// <summary>
        /// Every renderer that still exists, for the render backend (destroyed entries are
        /// pruned as they are found). Activity and enablement are the caller's to test.
        /// </summary>
        public static void CollectLive(List<Renderer> into)
        {
            into.Clear();
            int w = 0;
            for (int i = 0; i < s_live.Count; i++)
            {
                var r = s_live[i];
                if (r.destroyedFlag || r.gameObject == null || r.gameObject.destroyedFlag) continue;
                s_live[w++] = r;
                into.Add(r);
            }
            s_live.RemoveRange(w, s_live.Count - w);
        }
    }
}
