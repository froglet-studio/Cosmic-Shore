#if UNITY_EDITOR
using System.IO;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// A MASS DEATH MUST SHED ITS OWN DEBRIS LENGTH.
    ///
    /// WHY THIS MATTERS. <c>PrismExplosion.PressuredDuration</c> exists so "a dense blast's
    /// effects COMPLETE as smaller, quicker puffs instead of piling up" — a statement about
    /// the SCREEN, not about the CPU. The batched entity path (<c>PrismDebris</c>,
    /// <c>PrismShieldShatter</c>) read it as a cost bound, correctly observed that an entity
    /// costs no per-frame CPU, and ran every death at the full 7.5s. What that removed was
    /// the only thing bounding how much debris a mass death stacks in front of the camera:
    /// 7.5s × every prism killed, each surviving sliver SOLID (the erosion wipe is
    /// coverage-preserving in AREA, deliberately not a dither — PRISM_EROSION_FRINGE ships
    /// at 0), which is what was reported as being blinded by destroying lots of prisms.
    ///
    /// These are SOURCE LAWS rather than behaviour tests on purpose: the duration is chosen
    /// inside a static request that needs a live ECS world and a render service, and
    /// <c>PressuredDuration</c> is <c>internal</c> to Assembly-CSharp, which this editor
    /// assembly cannot call. What can be pinned from here is that neither producer has gone
    /// back to the flat constant — which is exactly the regression that happened once.
    /// </summary>
    [TestFixture]
    public class PrismDebrisPressureTests
    {
        const string ExplosionPath = "Assets/_Scripts/Utility/Effects/PrismExplosion.cs";
        const string DebrisPath = "Assets/_Scripts/Utility/Effects/PrismDebris.cs";
        const string ShatterPath = "Assets/_Scripts/Utility/Effects/PrismShieldShatter.cs";

        static string Read(string path)
        {
            Assert.IsTrue(File.Exists(path), $"{path} is missing.");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        [Test]
        public void PressureValve_IsReachableFromTheBatchedProducers()
        {
            // Both live in CosmicShore.Utility alongside PrismExplosion, so `internal` is
            // the narrowest access that works. Narrowing it further (private) silently
            // strands the producers on the flat constant again.
            StringAssert.Contains("internal static float PressuredDuration(int activeCount)",
                Read(ExplosionPath),
                "PressuredDuration is no longer reachable from the batched debris producers.");
        }

        [Test]
        public void BatchedExplosionDebris_IsPressureShortened()
        {
            string src = Read(DebrisPath);
            StringAssert.Contains("PrismExplosion.PressuredDuration(", src,
                "PrismDebris no longer sheds explosion length under load — a mass death " +
                "will stack every piece at full duration in front of the camera.");
            StringAssert.DoesNotContain("float duration = PrismExplosion.DefaultDuration;", src,
                "PrismDebris is back on the flat full-length constant.");
        }

        [Test]
        public void ShieldShatterDebris_IsPressureShortened()
        {
            string src = Read(ShatterPath);
            StringAssert.Contains("PrismExplosion.PressuredDuration(", src,
                "PrismShieldShatter no longer sheds shatter length under load.");
            StringAssert.DoesNotContain("float duration = PrismExplosion.DefaultDuration;", src,
                "PrismShieldShatter is back on the flat full-length constant.");
        }

        [Test]
        public void BothProducers_ShedAgainstOneSharedDebrisField()
        {
            // A blast that pops shields is usually the same blast destroying prisms, and the
            // player sees ONE debris field. Each producer counting only its own share would
            // let two half-full producers both decline to shed.
            StringAssert.Contains("PrismShieldShatter.LiveShatterCount", Read(DebrisPath),
                "PrismDebris sheds against its own debris only.");
            StringAssert.Contains("PrismDebris.LiveDebrisCount", Read(ShatterPath),
                "PrismShieldShatter sheds against its own debris only.");
        }

        [Test]
        public void SuctionDebris_KeepsItsAuthoredLength()
        {
            // Deliberately NOT pressured: a suction comes from fauna grazing one prism at a
            // time and converges on a point, so it has never arrived in the volumes an AOE
            // blast does. If this ever fails, decide it on purpose rather than by symmetry.
            StringAssert.Contains("Duration = s_impDuration", Read(DebrisPath),
                "Batched suction debris no longer runs at the authored implosion duration.");
        }
    }
}
#endif
