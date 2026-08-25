#if UNITY_EDITOR
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Structural gates for Docs/PRISM_ANIMATION.md §5 C13b — environment-lay
    /// pooling. A maxSize-bounded pool, an OnReturnToPool wire, or a leftover
    /// raw Instantiate in LayOne would either destroy conserved mass or kill
    /// the Blue → domain spawn repaint. File-read only; no play mode.
    /// </summary>
    public class EnvironmentPrismPoolTests
    {
        const string PoolPath = "Assets/_Scripts/Utility/PoolsAndBuffers/EnvironmentPrismPool.cs";
        const string BuilderPath = "Assets/_Scripts/Controller/Environment/Spawning/PrismTrailBuilder.cs";
        const string CellPath = "Assets/_Scripts/Controller/Environment/Cell.cs";
        const string PrismPath = "Assets/_Scripts/Controller/Vessel/Prism.cs";
        const string TeamPath = "Assets/_Scripts/Controller/Managers/PrismTeamManager.cs";
        const string AnimatorPath = "Assets/_Scripts/Controller/Environment/Prisms/MaterialPropertyAnimator.cs";
        const string PhyllotacticPath = "Assets/_Scripts/Controller/Environment/FloraAndFauna/PhyllotacticFlora.cs";
        const string BranchingPath = "Assets/_Scripts/Controller/Environment/FloraAndFauna/BranchingFlora.cs";
        const string AssembledPath = "Assets/_Scripts/Controller/Environment/FloraAndFauna/AssembledFlora.cs";
        const string BoidPath = "Assets/_Scripts/Controller/Environment/FloraAndFauna/Boid.cs";
        const string SpawnableBasePath = "Assets/_Scripts/Controller/Environment/Spawning/SpawnableBase.cs";
        const string SpawnableCordPath = "Assets/_Scripts/Controller/Environment/FloraAndFauna/SpawnableCord.cs";

        static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing.");
            return File.ReadAllText(path);
        }

        [Test]
        public void Pool_IsUnboundedAndDoesNotWireOnReturnToPool()
        {
            string text = Read(PoolPath);
            Assert.IsFalse(Regex.IsMatch(text, @"\bmaxSize\b"),
                "EnvironmentPrismPool must not be maxSize-bounded — overflow Destroy is an ecology-law breach.");
            Assert.IsFalse(text.Contains("OnReturnToPool"),
                "Wiring OnReturnToPool would let Cell.RetireWorldIntoSuctionRoot vacuum Wanderway stock.");
            Assert.IsTrue(text.Contains("TryRelease"),
                "Membership must be issued-dict + TryRelease, not OnReturnToPool.");
        }

        [Test]
        public void Builder_LayAndClone_DoNotRawInstantiate()
        {
            string text = Read(BuilderPath);
            Assert.IsFalse(text.Contains("UnityEngine.Object.Instantiate"),
                "PrismTrailBuilder still raw-Instantiates. Route through EnvironmentPrismPool.");
            Assert.IsFalse(text.Contains("Object.InstantiateAsync") || text.Contains("UnityEngine.Object.Instantiate("),
                "PrismTrailBuilder still raw-clones. Route through EnvironmentPrismPool.");
            Assert.IsTrue(text.Contains("EnvironmentPrismPool.Get"),
                "LayOne must pull from EnvironmentPrismPool.");
            Assert.IsTrue(text.Contains("GetBatchAsync"),
                "CloneBatchAsync must share the same spawn contract via GetBatchAsync.");
        }

        [Test]
        public void Cell_DrainUsesTryRelease_TrailDiscriminatorUnchanged()
        {
            string cell = Read(CellPath);
            Assert.IsTrue(cell.Contains("EnvironmentPrismPool.TryRelease"),
                "ReleaseRetiredWorld must TryRelease issued environment prisms.");
            Assert.IsTrue(cell.Contains("OnReturnToPool"),
                "RetireWorldIntoSuctionRoot must keep OnReturnToPool for vessel trail only.");
        }

        [Test]
        public void Prism_OnDestroy_ForgetsIssued()
        {
            string text = Read(PrismPath);
            Assert.IsTrue(text.Contains("EnvironmentPrismPool.ForgetDestroyed"),
                "Prism.OnDestroy must ForgetDestroyed so consumed mass is not returned.");
        }

        [Test]
        public void BlueSnap_ExistsAndDoesNotRaiseOnTeamChanged()
        {
            string team = Read(TeamPath);
            Assert.IsTrue(team.Contains("ResetToNeutralForReuse"),
                "PrismTeamManager must snap Domain to Blue without ChangeTeam.");
            Assert.IsTrue(team.Contains("BindMaterialsImmediate"),
                "ResetToNeutralForReuse must snap Blue materials, not clock-lerp.");

            int resetIdx = team.IndexOf("ResetToNeutralForReuse");
            int methodEnd = team.IndexOf("public void ChangeTeam", resetIdx);
            Assert.Greater(resetIdx, 0);
            Assert.Greater(methodEnd, resetIdx);
            string resetBody = team.Substring(resetIdx, methodEnd - resetIdx);
            Assert.IsFalse(resetBody.Contains("OnTeamChanged"),
                "ResetToNeutralForReuse must not invoke OnTeamChanged.");

            string animator = Read(AnimatorPath);
            Assert.IsTrue(animator.Contains("BindMaterialsImmediate"),
                "MaterialPropertyAnimator must snap materials while inactive.");
        }

        [Test]
        public void FloraLeaves_FoldThroughPool_NamedSitesStayInstantiate()
        {
            string phylo = Read(PhyllotacticPath);
            string branching = Read(BranchingPath);
            string assembled = Read(AssembledPath);
            Assert.IsFalse(Regex.IsMatch(phylo, @"Instantiate\(\s*healthPrism"),
                "PhyllotacticFlora still Instantiates health prisms.");
            Assert.IsFalse(Regex.IsMatch(branching, @"Instantiate\(\s*healthPrism"),
                "BranchingFlora still Instantiates health prisms.");
            Assert.IsFalse(Regex.IsMatch(assembled, @"Instantiate\(\s*healthPrism"),
                "AssembledFlora still Instantiates health prisms.");
            Assert.IsTrue(phylo.Contains("EnvironmentPrismPool.Get"));
            Assert.IsTrue(branching.Contains("EnvironmentPrismPool.Get"));
            Assert.IsTrue(assembled.Contains("EnvironmentPrismPool.Get"));

            Assert.IsTrue(Regex.IsMatch(Read(BoidPath), @"Instantiate\(\s*healthPrism"),
                "Boid body Instantiates were named, not folded — do not silently route them.");
            Assert.IsTrue(Read(SpawnableBasePath).Contains("Instantiate("),
                "SpawnableBase non-prism leafPrefab Instantiates were named, not folded.");
            Assert.IsTrue(Read(SpawnableCordPath).Contains("Instantiate("),
                "SpawnableCord Instantiates were named, not folded.");
        }
    }
}
#endif
