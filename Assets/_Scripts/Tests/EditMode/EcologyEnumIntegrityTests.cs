#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using CosmicShore.Data;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Serialization-drift guards for the ecology enums. Unity serializes enums by
    /// integer value, so reordering or renumbering these silently corrupts every
    /// prefab/asset field that stores them — e.g. `FaunaDiet` is serialized on the
    /// fauna prefabs (`diet: 1` = Predator), and `CellPhase`/`CellAggressionLevel`
    /// drive flora/fauna gating. These lock the integer ↔ name mapping.
    /// </summary>
    [TestFixture]
    public class EcologyEnumIntegrityTests
    {
        // ---- FaunaDiet (serialized on fauna prefabs) ----

        [Test]
        [TestCase(FaunaDiet.Herbivore, 0)]
        [TestCase(FaunaDiet.Predator, 1)]
        public void FaunaDiet_HasCorrectIntegerValue(FaunaDiet diet, int expected)
        {
            Assert.AreEqual(expected, (int)diet,
                $"FaunaDiet.{diet} changed value — breaks the serialized `diet:` field on fauna prefabs.");
        }

        [Test]
        public void FaunaDiet_HasExpectedMemberCount()
        {
            Assert.AreEqual(2, Enum.GetValues(typeof(FaunaDiet)).Length,
                "FaunaDiet member count changed — update tests + any diet switch logic.");
        }

        // ---- CellPhase (drives flora planting/growth + fauna aggression) ----

        [Test]
        [TestCase(CellPhase.None, 0)]
        [TestCase(CellPhase.Sprout, 1)]
        [TestCase(CellPhase.Quiet, 2)]
        [TestCase(CellPhase.Settled, 3)]
        [TestCase(CellPhase.Restless, 4)]
        [TestCase(CellPhase.Frozen, 5)]
        [TestCase(CellPhase.Rabid, 6)]
        public void CellPhase_HasCorrectIntegerValue(CellPhase phase, int expected)
        {
            Assert.AreEqual(expected, (int)phase,
                $"CellPhase.{phase} changed value — breaks CellNetworkSync replication + phase-gated assets.");
        }

        [Test]
        public void CellPhase_IsMonotonicAscending_SproutToRabid()
        {
            // The phase gates (FloraPlantingEnabled < Settled, FloraGrowingEnabled < Frozen,
            // AggressionLevel by phase) rely on the ordering Sprout < … < Rabid.
            Assert.Less((int)CellPhase.Sprout, (int)CellPhase.Quiet);
            Assert.Less((int)CellPhase.Quiet, (int)CellPhase.Settled);
            Assert.Less((int)CellPhase.Settled, (int)CellPhase.Restless);
            Assert.Less((int)CellPhase.Restless, (int)CellPhase.Frozen);
            Assert.Less((int)CellPhase.Frozen, (int)CellPhase.Rabid);
        }

        // ---- CellAggressionLevel ----

        [Test]
        [TestCase(CellAggressionLevel.Level0, 0)]
        [TestCase(CellAggressionLevel.Level1, 1)]
        [TestCase(CellAggressionLevel.Level2, 2)]
        public void CellAggressionLevel_HasCorrectIntegerValue(CellAggressionLevel level, int expected)
        {
            // Cast to int is used to index aggression-scaled arrays in Fauna/LightFauna.
            Assert.AreEqual(expected, (int)level,
                $"CellAggressionLevel.{level} changed value — breaks the aggression-indexed multiplier arrays.");
        }

        [Test]
        public void EcologyEnums_AllValuesUnique()
        {
            AssertUnique<FaunaDiet>();
            AssertUnique<CellPhase>();
            AssertUnique<CellAggressionLevel>();
        }

        static void AssertUnique<T>() where T : Enum
        {
            var values = Enum.GetValues(typeof(T)).Cast<int>().ToList();
            var dupes = values.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key);
            Assert.IsEmpty(dupes, $"Duplicate integer values in {typeof(T).Name} — ambiguous deserialization.");
        }
    }
}
#endif
