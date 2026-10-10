using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Original-contract per-draw parameter block (UnityEngine.RenderParams, subset the
    /// ported call sites author). Data-only headless: a render backend interprets the
    /// values in the presentation phase.
    /// </summary>
    public struct RenderParams
    {
        public Material material;
        public Bounds worldBounds;
        public Rendering.ShadowCastingMode shadowCastingMode;
        public bool receiveShadows;
        public uint renderingLayerMask;
        public int layer;
        /// <summary>Per-draw property overrides (UnityEngine.RenderParams.matProps).</summary>
        public MaterialPropertyBlock matProps;

        public RenderParams(Material material)
        {
            this.material = material;
            matProps = null;
            worldBounds = new Bounds(Vector3.zero, Vector3.zero);
            shadowCastingMode = Rendering.ShadowCastingMode.On;
            receiveShadows = true;
            renderingLayerMask = 1;
            layer = 0;
        }
    }

    /// <summary>
    /// Original-contract immediate-mode draw API (the mesh arc), as a data-only
    /// SUBMISSION RECORDER: calls validate their inputs and record what would have been
    /// drawn instead of touching a GPU. A render backend replays the same submissions in
    /// the presentation phase; tests assert on them via <see cref="InstancedSubmissions"/>
    /// / <see cref="InstancedSubmissionCount"/>.
    ///
    /// Memory contract: only the most recent <see cref="MaxRecordedSubmissions"/>
    /// submissions are retained (ring buffer) so per-frame callers (CapsuleMembrane.Update
    /// submits every frame) never grow memory over headless soaks; the total counter keeps
    /// counting. Matrices are held by reference — callers that reuse a scratch array
    /// (the original engine copies at the GPU boundary) share it here; read
    /// <see cref="InstancedDrawSubmission.instanceCount"/> rather than snapshotting.
    /// </summary>
    public static class Graphics
    {
        /// <summary>Runs a command buffer (UnityEngine.Graphics.ExecuteCommandBuffer). The port's CommandBuffer records nothing, so this does nothing (Rendering/RenderGraph.cs).</summary>
        public static void ExecuteCommandBuffer(Rendering.CommandBuffer buffer) { }

        /// <summary>
        /// Installed by the GL renderer: performs a GPU copy (optionally through a material).
        /// Headless it bumps the destination's version so a reader sees fresh contents.
        /// </summary>
        public static Action<Texture, RenderTexture, Material, int> BlitHook;

        /// <summary>
        /// The render backend's GPU copy for <see cref="CopyTexture(Texture, int, int, Texture, int, int)"/>;
        /// null while no backend provides one, and then <see cref="SystemInfo.copyTextureSupport"/>
        /// reports None so callers take their fallback, as on a device without copy support.
        /// </summary>
        public static Action<Texture, int, int, Texture, int, int> CopyTextureHook;

        /// <summary>Original: Graphics.CopyTexture - one element/mip of a texture into another's (a 2D target into an array slice).</summary>
        public static void CopyTexture(Texture src, int srcElement, int srcMip, Texture dst, int dstElement, int dstMip)
            => CopyTextureHook?.Invoke(src, srcElement, srcMip, dst, dstElement, dstMip);

        public static void CopyTexture(Texture src, Texture dst) => CopyTexture(src, 0, 0, dst, 0, 0);

        public static void Blit(Texture source, RenderTexture dest) => Blit(source, dest, null, -1);
        public static void Blit(Texture source, RenderTexture dest, Material mat, int pass = -1)
        {
            if (BlitHook != null) BlitHook(source, dest, mat, pass);
            dest?.MarkModified();
        }
        public static void Blit(Texture source, Material mat, int pass = -1) => Blit(source, null, mat, pass);

        public const int MaxRecordedSubmissions = 16;

        /// <summary>One recorded RenderMeshInstanced call.</summary>
        public readonly struct InstancedDrawSubmission
        {
            public readonly RenderParams renderParams;
            public readonly Mesh mesh;
            public readonly int submeshIndex;
            public readonly CosmicShore.Engine.Matrix4x4[] instanceData;
            public readonly int instanceCount;

            internal InstancedDrawSubmission(in RenderParams rparams, Mesh mesh, int submeshIndex,
                CosmicShore.Engine.Matrix4x4[] instanceData, int instanceCount)
            {
                renderParams = rparams;
                this.mesh = mesh;
                this.submeshIndex = submeshIndex;
                this.instanceData = instanceData;
                this.instanceCount = instanceCount;
            }
        }

        static readonly List<InstancedDrawSubmission> _recent = new();
        static readonly object _gate = new(); // xunit runs test classes in parallel; guard the recorder
        static long _totalInstancedSubmissions;

        /// <summary>Snapshot of the most recent submissions, oldest first (bounded — see class doc).</summary>
        public static InstancedDrawSubmission[] InstancedSubmissions
        {
            get { lock (_gate) return _recent.ToArray(); }
        }

        /// <summary>Total RenderMeshInstanced calls recorded since process start / last <see cref="ClearRecordedSubmissions"/>.</summary>
        public static long InstancedSubmissionCount
        {
            get { lock (_gate) return _totalInstancedSubmissions; }
        }

        /// <summary>Test/harness hook: drop the recorded submissions and reset the counter.</summary>
        public static void ClearRecordedSubmissions()
        {
            lock (_gate)
            {
                _recent.Clear();
                _totalInstancedSubmissions = 0;
            }
        }

        /// <summary>
        /// Original signature: draw <paramref name="instanceData"/>.Length (or
        /// <paramref name="instanceCount"/>, when non-negative) instances of one submesh.
        /// Headless: records the submission.
        /// </summary>
        static bool s_primitivesWarned;

        /// <summary>
        /// Procedural instancing: the shader builds each instance from GPU buffers (SV_InstanceID).
        /// Prisma's renderer has no vertex-stage storage buffers
        /// (<see cref="SystemInfo.maxComputeBufferInputsVertex"/> is 0), so nothing is drawn and the
        /// first call says so. Code that checks the capability first never gets here.
        /// </summary>
        public static void RenderMeshPrimitives(in RenderParams rparams, Mesh mesh, int submeshIndex, int instanceCount = 1, int startInstance = 0)
        {
            if (s_primitivesWarned || mesh is null || instanceCount <= 0) return;
            s_primitivesWarned = true;
            Debug.LogWarning($"[port] Graphics.RenderMeshPrimitives ('{mesh.name}', material '{rparams.material?.name}') is not drawn: procedural instancing from GPU buffers has no Amoebius backend yet.");
        }

        public static void RenderMeshInstanced(in RenderParams rparams, Mesh mesh, int submeshIndex,
            CosmicShore.Engine.Matrix4x4[] instanceData, int instanceCount = -1, int startInstance = 0)
        {
            if (mesh is null || instanceData is null) return;
            int available = Math.Max(0, instanceData.Length - Math.Max(0, startInstance));
            int count = instanceCount < 0 ? available : Math.Min(instanceCount, available);

            lock (_gate)
            {
                _recent.Add(new InstancedDrawSubmission(rparams, mesh, submeshIndex, instanceData, count));
                if (_recent.Count > MaxRecordedSubmissions)
                    _recent.RemoveAt(0);
                _totalInstancedSubmissions++;
            }
        }
    }
}
