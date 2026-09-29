using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.ECS
{
    /// <summary>
    /// Answers ONE question before the DOTS default world is created: can Entities Graphics
    /// actually run on the graphics device this process got?
    ///
    /// Entities Graphics' own gate (<c>EntitiesGraphicsUtils.IsEntitiesGraphicsSupportedOnSystem</c>)
    /// asks only whether an SRP is active and <see cref="SystemInfo.supportsComputeShaders"/> is
    /// true. Both were true on the machine that shipped this class into existence — an Intel Iris
    /// Xe laptop where Unity's built-in D3D12 device filter refused the integrated GPU, the player
    /// fell back to Direct3D 11, and every Entities Graphics compute KERNEL then failed to load
    /// (<c>ArgumentException: Kernel 'CopyKernel' not found</c> out of
    /// <c>EntitiesGraphicsSystem.OnCreate</c>). Entities catches that exception, logs it, and leaves
    /// the system registered half-built; the first frame to touch it then took the whole process
    /// down with a native crash, before the Bootstrap scene had finished its first frame.
    ///
    /// So the probe runs the exact call that failed — <see cref="ComputeShader.FindKernel"/> on the
    /// package's own <c>SparseUploader</c> shader, the one <c>EntitiesGraphicsSystem</c> cannot
    /// construct without — and reports the reason. <see cref="CosmicShoreEntitiesBootstrap"/> reads
    /// the verdict to stand the Entities Graphics world down, and <see cref="PrismRenderService"/>
    /// reads it so its status line names the cause instead of "EntitiesGraphicsSystem missing".
    ///
    /// The decision itself is the pure <see cref="Decide"/> so the branch table is unit-testable
    /// without a graphics device.
    /// </summary>
    public static class EntitiesGraphicsSupportProbe
    {
        /// <summary>The compute shader Entities Graphics loads by this name from its own Resources folder.</summary>
        public const string SparseUploaderShaderName = "SparseUploader";

        /// <summary>The two kernels <c>SparseUploader</c>'s constructor resolves; either missing is fatal.</summary>
        public static readonly string[] SparseUploaderKernels = { "CopyKernel", "ReplaceKernel" };

        static bool _probed;
        static bool _supported;
        static string _reason = "not probed";

        /// <summary>True when Entities Graphics can be created on this device. Probes once per process.</summary>
        public static bool IsSupported
        {
            get { Probe(); return _supported; }
        }

        /// <summary>Why <see cref="IsSupported"/> holds — "ok", or the first failed gate, for diagnostics.</summary>
        public static string Reason
        {
            get { Probe(); return _reason; }
        }

        /// <summary>
        /// The branch table, separated from the device reads so it can be tested. Order matters:
        /// it mirrors the package's own gate first (no SRP / no compute = the package would have
        /// disabled itself cleanly anyway) and only then asks the question the package does not.
        /// </summary>
        public static bool Decide(bool srpActive, bool supportsCompute, bool kernelsLoad, out string reason)
        {
            if (!srpActive) { reason = "no scriptable render pipeline active"; return false; }
            if (!supportsCompute) { reason = "device reports no compute shader support"; return false; }
            if (!kernelsLoad) { reason = "Entities Graphics compute kernels failed to load on this device"; return false; }
            reason = "ok";
            return true;
        }

        /// <summary>Force a re-probe (tests, or a graphics device change). Not needed at runtime.</summary>
        public static void Reset() { _probed = false; _supported = false; _reason = "not probed"; }

        static void Probe()
        {
            if (_probed) return;
            _probed = true;

            bool srp = GraphicsSettings.currentRenderPipeline != null;
            bool compute = SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && SystemInfo.supportsComputeShaders;
            string kernelFailure = null;
            bool kernels = compute && TryLoadSparseUploaderKernels(out kernelFailure);

            _supported = Decide(srp, compute, kernels, out _reason);
            if (!_supported && kernelFailure != null)
                _reason += $" ({kernelFailure})";
        }

        /// <summary>
        /// Loads the package's SparseUploader compute shader and resolves both kernels the way the
        /// package's constructor does. A kernel that failed to compile or load for the active
        /// graphics API makes <see cref="ComputeShader.FindKernel"/> THROW, which is exactly the
        /// exception this probe exists to catch before Entities does.
        /// </summary>
        static bool TryLoadSparseUploaderKernels(out string failure)
        {
            failure = null;
            ComputeShader shader;
            try
            {
                shader = Resources.Load<ComputeShader>(SparseUploaderShaderName);
            }
            catch (Exception e)
            {
                failure = $"Resources.Load<ComputeShader>(\"{SparseUploaderShaderName}\") threw {e.GetType().Name}: {e.Message}";
                return false;
            }

            if (shader == null)
            {
                failure = $"Resources.Load<ComputeShader>(\"{SparseUploaderShaderName}\") returned null — Entities Graphics package resources missing from the build";
                return false;
            }

            foreach (var kernel in SparseUploaderKernels)
            {
                try
                {
                    if (!shader.HasKernel(kernel))
                    {
                        failure = $"kernel '{kernel}' not present on {SystemInfo.graphicsDeviceType} / {SystemInfo.graphicsDeviceName}";
                        return false;
                    }
                    shader.FindKernel(kernel);
                }
                catch (Exception e)
                {
                    failure = $"kernel '{kernel}' failed on {SystemInfo.graphicsDeviceType} / {SystemInfo.graphicsDeviceName}: {e.Message}";
                    return false;
                }
            }
            return true;
        }
    }
}
