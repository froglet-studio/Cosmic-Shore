#if UNITY_EDITOR
using CosmicShore.Data;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The rule in one sentence: TEN POINTS IS ONE PETAL, on every element the hit touches.
    ///
    /// These assert the arithmetic and the two things that make it safe — that a rocket's three
    /// ranked tiers NET rather than stack, and that the classes whose drain is authored per
    /// weapon contribute nothing here (a class drawing from both would bite twice for one hit).
    /// </summary>
    public class CombatHitDrainTests
    {
        const float Eps = 1e-6f;

        static float Petals(CombatHitClass c) =>
            CombatHitDrain.PerElementFor(c) / CombatHitDrain.NormalizedPerLevel;

        [Test]
        public void TenPointsIsOnePetal()
        {
            // The two figures the spec was given in.
            Assert.AreEqual(-3f, Petals(CombatHitClass.MissileDirect), Eps,
                "a direct missile hit (30 points) must drain three petals from each element");
            Assert.AreEqual(-0.1f, Petals(CombatHitClass.Bullet), Eps,
                "one bullet (1 point) must drain a tenth of a petal, so ten bullets cost one");
        }

        [Test]
        public void EveryPricedClassScalesWithItsPrice()
        {
            foreach (var c in new[]
                     {
                         CombatHitClass.Bullet, CombatHitClass.MissileShockwave,
                         CombatHitClass.MissileBlast, CombatHitClass.MissileDirect,
                     })
            {
                Assert.AreEqual(
                    -CombatHitDrain.PointsFor(c) / CombatHitDrain.PointsPerLevel,
                    Petals(c), Eps, $"{c} must drain points/10 petals");
            }
        }

        [Test]
        public void PerWeaponClassesDrainNothingFromTheTable()
        {
            // Their bite is authored on the weapon's own asset, because it carries design a
            // price cannot express (which elements; whether it mirrors as an ally buff).
            foreach (var c in new[] { CombatHitClass.Debuff, CombatHitClass.Strike })
            {
                Assert.IsTrue(CombatHitDrain.DrainAuthoredPerWeapon(c), $"{c} is authored per weapon");
                Assert.AreEqual(0f, CombatHitDrain.PerElementFor(c), Eps,
                    $"{c} must contribute nothing here or one hit bites twice");
            }
        }

        [Test]
        public void MissileTiersNetAgainstWhatTheySupersede()
        {
            // One rocket landing shockwave -> blast -> direct must total the DIRECT tier, not
            // the sum of all three. The latch reports the superseded rank; the drain subtracts
            // it, exactly as CombatHitScoring credits only the difference in points.
            float shockwave = CombatHitDrain.PerElementFor(CombatHitClass.MissileShockwave);
            float blast = CombatHitDrain.PerElementFor(CombatHitClass.MissileBlast);
            float direct = CombatHitDrain.PerElementFor(CombatHitClass.MissileDirect);

            float upgradeToBlast = blast - shockwave;
            float upgradeToDirect = direct - blast;

            Assert.AreEqual(direct, shockwave + upgradeToBlast + upgradeToDirect, Eps,
                "the netted sum of the three tiers must equal the best tier alone");
            Assert.Less(upgradeToBlast, 0f, "an upgrade must still take something");
            Assert.Less(upgradeToDirect, 0f, "an upgrade must still take something");
        }

        [Test]
        public void SupersededRankMapsBackToItsClass()
        {
            Assert.AreEqual(CombatHitClass.MissileShockwave, CombatHitDrain.ClassForMissileRank(1));
            Assert.AreEqual(CombatHitClass.MissileBlast, CombatHitDrain.ClassForMissileRank(2));
            Assert.AreEqual(CombatHitClass.MissileDirect, CombatHitDrain.ClassForMissileRank(3));

            // And the ranks are the latch's own, not a second opinion.
            Assert.AreEqual(1, CombatHitClasses.MissileProximityRank(CombatHitClass.MissileShockwave));
            Assert.AreEqual(2, CombatHitClasses.MissileProximityRank(CombatHitClass.MissileBlast));
            Assert.AreEqual(3, CombatHitClasses.MissileProximityRank(CombatHitClass.MissileDirect));
        }

        [Test]
        public void OnePetalIsOneElementLevel()
        {
            // ResourceSystem.IncrementLevel is a bare 0.1f; the constant here is that same step
            // named, and every figure above is stated in it.
            Assert.AreEqual(0.1f, CombatHitDrain.NormalizedPerLevel, Eps);
        }
    }
}
#endif
