#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Guards for <see cref="ModePreviewDefinitionSO.ResolveSpawnRingRadiusFloor"/>, the preview's
    /// copy of <c>ServerPlayerVesselInitializer.ResolveSpawnRingRadiusFloor</c>. The two must agree,
    /// or a card's preview opens somewhere the match never puts a pilot (Cleave's preview stood at
    /// 3,150 on all four rungs while rungs 3 and 4 spawn at 1,050).
    /// </summary>
    [TestFixture]
    public class ModePreviewSpawnFloorTests
    {
        const string CleavePreview = "Assets/_SO_Assets/Mode Previews/ModePreview_Cleave.asset";

        ModePreviewDefinitionSO _definition;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<ModePreviewDefinitionSO>();
            _definition.SpawnRingRadiusFloor = 500f;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_definition);

        [Test]
        public void EmptyList_FallsBackToScalar_AtEveryIntensity()
        {
            _definition.SpawnRingRadiusFloorByIntensity = new List<float>();
            for (int intensity = 1; intensity <= 4; intensity++)
                Assert.AreEqual(500f, _definition.ResolveSpawnRingRadiusFloor(intensity));
        }

        [Test]
        public void NullList_FallsBackToScalar()
        {
            _definition.SpawnRingRadiusFloorByIntensity = null;
            Assert.AreEqual(500f, _definition.ResolveSpawnRingRadiusFloor(3));
        }

        [Test]
        public void AuthoredEntries_AreReadPerIntensity()
        {
            _definition.SpawnRingRadiusFloorByIntensity = new List<float> { 3150f, 2000f, 1050f, 900f };
            Assert.AreEqual(3150f, _definition.ResolveSpawnRingRadiusFloor(1));
            Assert.AreEqual(2000f, _definition.ResolveSpawnRingRadiusFloor(2));
            Assert.AreEqual(1050f, _definition.ResolveSpawnRingRadiusFloor(3));
            Assert.AreEqual(900f, _definition.ResolveSpawnRingRadiusFloor(4));
        }

        [Test]
        public void IntensityPastTheEnd_ClampsToLastEntry()
        {
            _definition.SpawnRingRadiusFloorByIntensity = new List<float> { 3150f, 1050f };
            Assert.AreEqual(1050f, _definition.ResolveSpawnRingRadiusFloor(3));
            Assert.AreEqual(1050f, _definition.ResolveSpawnRingRadiusFloor(9));
        }

        [Test]
        public void IntensityBelowOne_ClampsToFirstEntry()
        {
            _definition.SpawnRingRadiusFloorByIntensity = new List<float> { 3150f, 1050f };
            Assert.AreEqual(3150f, _definition.ResolveSpawnRingRadiusFloor(0));
        }

        [Test]
        public void ZeroEntry_DefersToScalar_NotToNoFloor()
        {
            _definition.SpawnRingRadiusFloorByIntensity = new List<float> { 3150f, 0f, 1050f };
            Assert.AreEqual(500f, _definition.ResolveSpawnRingRadiusFloor(2),
                "A 0 entry means 'this rung has nothing to say', so the scalar applies.");
            Assert.AreEqual(1050f, _definition.ResolveSpawnRingRadiusFloor(3));
        }

        /// <summary>
        /// Negative control: the scalar-only behaviour this replaced must be DISTINGUISHABLE from
        /// the per-intensity resolve on a list whose rungs differ. If this ever passes with equal
        /// values, the resolver has collapsed back to the scalar and the tests above would be
        /// checking nothing for a list like Cleave's.
        /// </summary>
        [Test]
        public void NegativeControl_ScalarOnlyResolve_WouldGetSmallRungsWrong()
        {
            _definition.SpawnRingRadiusFloor = 3150f;
            _definition.SpawnRingRadiusFloorByIntensity = new List<float> { 3150f, 3150f, 1050f, 1050f };

            float scalarOnly = _definition.SpawnRingRadiusFloor;
            Assert.AreNotEqual(scalarOnly, _definition.ResolveSpawnRingRadiusFloor(3));
            Assert.AreNotEqual(scalarOnly, _definition.ResolveSpawnRingRadiusFloor(4));
            Assert.AreEqual(scalarOnly, _definition.ResolveSpawnRingRadiusFloor(1));
        }

        [Test]
        public void ResolveSpawnPose_UsesTheIntensitysFloor()
        {
            _definition.SpawnFromCellRing = true;
            _definition.SpawnDistanceOutsideNucleus = 40f;
            _definition.SpawnRingRadiusFloor = 3150f;
            _definition.SpawnRingRadiusFloorByIntensity = new List<float> { 3150f, 3150f, 1050f, 1050f };

            var centre = new Vector3(120000f, 0f, 0f);
            float big = (_definition.ResolveSpawnPose(centre, 0f, 0, 1).position - centre).magnitude;
            float small = (_definition.ResolveSpawnPose(centre, 0f, 0, 3).position - centre).magnitude;

            Assert.AreEqual(3150f, big, 0.5f);
            Assert.AreEqual(1050f, small, 0.5f);
        }

        [Test]
        public void ShippedCleavePreview_MirrorsTheScenesPerRungFloors()
        {
            var cleave = AssetDatabase.LoadAssetAtPath<ModePreviewDefinitionSO>(CleavePreview);
            Assert.IsNotNull(cleave, $"{CleavePreview} did not load");

            Assert.AreEqual(3150f, cleave.ResolveSpawnRingRadiusFloor(1));
            Assert.AreEqual(3150f, cleave.ResolveSpawnRingRadiusFloor(2));
            Assert.AreEqual(1050f, cleave.ResolveSpawnRingRadiusFloor(3));
            Assert.AreEqual(1050f, cleave.ResolveSpawnRingRadiusFloor(4));
        }
    }
}
#endif
