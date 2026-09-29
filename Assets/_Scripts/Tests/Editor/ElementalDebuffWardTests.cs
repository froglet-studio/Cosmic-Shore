#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Data;
using CosmicShore.Gameplay;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Elemental-debuff WARD SCOPING tests — validates ResourceSystem.WardStops, the whole of the
    /// rule that decides which debuffs an immunity grant actually stops.
    ///
    /// WHY THIS MATTERS:
    /// "Immune to elemental debuffs" was a bare bool while its only holders (Sparrow while
    /// boosting, Serpent while stopped) wanted total immunity. The Dolphin's Time-5 Drift Ward is
    /// earned against DANGER PRISMS — flying its own hazardous arena — and, held unscoped, it also
    /// cancelled the Dolphin crystal blast's debuff, which is the entire scoring event of The
    /// Bends, a mode in which every pilot is a Dolphin and the comeback buff hands Time 5 to
    /// whoever is LOSING. Nothing errored, nothing logged: the trailing pilot simply could not be
    /// scored on. That is the failure class these tests pin.
    ///
    /// Two invariants beyond the obvious matrix:
    ///   (1) All is ~0, NOT the OR of today's members — it is serialized on prefabs, so an
    ///       "everything" ward authored today must cover a class added tomorrow.
    ///   (2) An unclassified debuff lands in Other, which only an everything-ward stops — so a new
    ///       source class can never silently WIDEN a narrow ward, and forgetting to classify a new
    ///       debuff fails in the safe direction (it still lands).
    /// </summary>
    [TestFixture]
    public class ElementalDebuffWardTests
    {
        const ElementalDebuffSources DriftWard = ElementalDebuffSources.DangerPrism;

        [Test]
        public void DriftWard_StopsDangerPrisms()
        {
            Assert.IsTrue(ResourceSystem.WardStops(DriftWard, ElementalDebuffSources.DangerPrism),
                "The Dolphin's Drift Ward exists to deny a danger prism's all-element drain.");
        }

        [Test]
        public void DriftWard_DoesNotStopAnotherPilotsBlast()
        {
            Assert.IsFalse(ResourceSystem.WardStops(DriftWard, ElementalDebuffSources.Explosion),
                "A ward earned against the ARENA must never cancel a weapon another pilot aimed - " +
                "this is the whole scoring event of The Bends.");
        }

        [Test]
        public void DriftWard_DoesNotStopUnrelatedClasses()
        {
            Assert.IsFalse(ResourceSystem.WardStops(DriftWard, ElementalDebuffSources.VesselContact));
            Assert.IsFalse(ResourceSystem.WardStops(DriftWard, ElementalDebuffSources.Other),
                "An unclassified debuff is Other, and a narrow ward must not stop it.");
        }

        [Test]
        public void TotalWard_StopsEveryNamedClass()
        {
            foreach (var source in new[]
                     {
                         ElementalDebuffSources.DangerPrism,
                         ElementalDebuffSources.Explosion,
                         ElementalDebuffSources.VesselContact,
                         ElementalDebuffSources.Other,
                     })
                Assert.IsTrue(ResourceSystem.WardStops(ElementalDebuffSources.All, source),
                    $"The Sparrow / Serpent ward must still stop {source}.");
        }

        [Test]
        public void TotalWard_CoversAClassAddedLater()
        {
            // All is ~0 rather than the OR of the members above, so a mask serialized on a prefab
            // today keeps covering a source class introduced tomorrow. Standing in for that future
            // member with an unassigned bit is the only way to test it before it exists.
            const ElementalDebuffSources futureClass = (ElementalDebuffSources)(1 << 20);
            Assert.IsTrue(ResourceSystem.WardStops(ElementalDebuffSources.All, futureClass),
                "An 'everything' ward authored before a source class existed must still cover it - " +
                "otherwise adding a class silently un-wards every shipped prefab.");
            Assert.IsFalse(ResourceSystem.WardStops(DriftWard, futureClass),
                "...and adding a class must never WIDEN a narrow ward.");
        }

        [Test]
        public void NoWard_StopsNothing()
        {
            Assert.IsFalse(ResourceSystem.WardStops(ElementalDebuffSources.None,
                ElementalDebuffSources.DangerPrism));
            Assert.IsFalse(ResourceSystem.WardStops(ElementalDebuffSources.None,
                ElementalDebuffSources.Explosion));
        }
    }
}
#endif
