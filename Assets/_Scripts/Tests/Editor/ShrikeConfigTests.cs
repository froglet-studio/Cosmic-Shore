using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Shrike's config (<c>R_VesselActions/SHRIKE.md</c>): its four elemental ranges are the Shrike Studio's SHIPPED
    /// <c>...At0</c> / <c>...At10</c> keys, and at level 3 they give what the page's own <c>mults</c> gives.
    /// </summary>
    [TestFixture]
    public class ShrikeConfigTests
    {
        const float Eps = 1e-3f;

        [Test]
        public void Config_ElementalRanges_AreThePagesShipped()
        {
            var config = ScriptableObject.CreateInstance<ShrikeConfigSO>();
            try
            {
                void Range(ElementalFloat f, Element element, float at0, float at10, string what)
                {
                    Assert.AreEqual(element, f.ScaledElement, what + " element");
                    Assert.AreEqual(at0, f.EvaluateAtNormalizedLevel(0f), Eps, what + " at 0");
                    Assert.AreEqual(at10, f.EvaluateAtNormalizedLevel(1f), Eps, what + " at 10");
                }
                Range(config.LanceRadius, Element.Charge, 1.5f, 5f, "lanceRadius");
                Range(config.NestBlocks, Element.Mass, 12f, 32f, "nestBlocks");
                Range(config.TrailWavelength, Element.Mass, 8f, 4f, "trailWavelength");
                Range(config.LockCone, Element.Space, 5f, 14f, "lockCone");
                Range(config.SlingCooldown, Element.Time, 6f, 2.5f, "slingCooldown");
                // the page's mults at Mass/Charge/Space/Time 3: lanceR 2.55, nestBlocks 18, wave 6.8, cone 7.7, slingCd 4.95
                Assert.AreEqual(2.55f, config.LanceRadius.EvaluateAtNormalizedLevel(0.3f), Eps);
                Assert.AreEqual(18, ShrikeRules.NestBlockCount(config.NestBlocks.EvaluateAtNormalizedLevel(0.3f)));
                Assert.AreEqual(6.8f, config.TrailWavelength.EvaluateAtNormalizedLevel(0.3f), Eps);
                Assert.AreEqual(7.7f, config.LockCone.EvaluateAtNormalizedLevel(0.3f), Eps);
                Assert.AreEqual(4.95f, config.SlingCooldown.EvaluateAtNormalizedLevel(0.3f), Eps);
            }
            finally
            {
                Object.DestroyImmediate(config);
            }
        }
    }
}
