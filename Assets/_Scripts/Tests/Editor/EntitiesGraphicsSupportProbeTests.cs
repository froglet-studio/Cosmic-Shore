#if UNITY_EDITOR
using NUnit.Framework;
using Unity.Entities;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The DOTS bootstrap gate exists because of one shipped crash: an Intel iGPU laptop where
    /// the D3D12 device filter forced a DX11 fallback, Entities Graphics' compute kernels failed to
    /// load, and the half-built EntitiesGraphicsSystem took the process down before the Bootstrap
    /// scene's first frame (Docs/PRISM_ECS_MIGRATION.md §8). These hold the two things that make
    /// the gate a gate: the decision table itself, and that the bootstrap Entities discovers is
    /// ours and concrete (an abstract or generic type is silently skipped by CreateBootStrap).
    /// </summary>
    public class EntitiesGraphicsSupportProbeTests
    {
        [Test]
        public void Decide_AllGatesPass_IsSupported()
        {
            Assert.IsTrue(EntitiesGraphicsSupportProbe.Decide(true, true, true, out var reason));
            Assert.AreEqual("ok", reason);
        }

        [Test]
        public void Decide_KernelsFailToLoad_IsUnsupported_EvenWithComputeSupportReported()
        {
            // The shipped case: SRP active, SystemInfo.supportsComputeShaders true, kernels dead.
            Assert.IsFalse(EntitiesGraphicsSupportProbe.Decide(true, true, false, out var reason));
            StringAssert.Contains("kernels", reason);
        }

        [Test]
        public void Decide_NoCompute_IsUnsupported()
        {
            Assert.IsFalse(EntitiesGraphicsSupportProbe.Decide(true, false, true, out var reason));
            StringAssert.Contains("compute", reason);
        }

        [Test]
        public void Decide_NoSrp_IsUnsupported()
        {
            Assert.IsFalse(EntitiesGraphicsSupportProbe.Decide(false, true, true, out var reason));
            StringAssert.Contains("render pipeline", reason);
        }

        [Test]
        public void Bootstrap_IsAConcreteICustomBootstrap_EntitiesCanDiscover()
        {
            var type = typeof(CosmicShoreEntitiesBootstrap);
            Assert.IsTrue(typeof(ICustomBootstrap).IsAssignableFrom(type));
            Assert.IsFalse(type.IsAbstract);
            Assert.IsFalse(type.ContainsGenericParameters);
            Assert.IsEmpty(type.GetCustomAttributes(typeof(DisableBootstrapOverridesAttribute), false),
                "a [DisableBootstrapOverrides] on the gate would make Entities ignore it");
        }

        [Test]
        public void Probe_KernelNames_MatchThePackageConstructor()
        {
            // SparseUploader's constructor resolves exactly these two; a package upgrade that
            // renames one turns the probe into a false negative (legacy path everywhere).
            CollectionAssert.AreEquivalent(new[] { "CopyKernel", "ReplaceKernel" }, EntitiesGraphicsSupportProbe.SparseUploaderKernels);
            Assert.AreEqual("SparseUploader", EntitiesGraphicsSupportProbe.SparseUploaderShaderName);
        }
    }
}
#endif
