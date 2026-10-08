#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The warp field (Docs/WARP_FIELD.md): the radial field's law, the runtime's no-field and
    /// ownership rules, and the Black Hole cell's wiring. The runtime half is the safety net for
    /// every consumer — a field-free session must read EXACTLY 1 everywhere, or every vessel in
    /// the game would change size.
    /// </summary>
    public class WarpFieldTests
    {
        const string CellConfigPath = "Assets/_SO_Assets/Cell Configs/Black Hole Cell/Black Hole Cell Config.asset";
        const string HolePrefabPath = "Assets/_Prefabs/Spawnables/SpawnableBlackHole.prefab";

        RadialWarp _field;
        readonly object _owner = new();

        [SetUp]
        public void SetUp()
        {
            _field = ScriptableObject.CreateInstance<RadialWarp>();
            var so = new SerializedObject(_field);
            so.FindProperty("referenceRadius").floatValue = 1000f;
            so.FindProperty("exponent").floatValue = 1f;
            so.FindProperty("minScale").floatValue = 0.01f;
            so.FindProperty("maxScale").floatValue = 1f;
            so.FindProperty("easeSeconds").floatValue = 0f;   // instant, so edit-mode time does not matter
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            WarpFieldRuntime.Release(_owner);
            _ = WarpFieldRuntime.IsActive;   // retire the instant ease-out
            Object.DestroyImmediate(_field);
        }

        [Test]
        public void Radial_IsProportionalToDistance_FlooredAndCapped()
        {
            Assert.AreEqual(1f, _field.ScaleAt(new Vector3(1000f, 0f, 0f)), 1e-5f, "not 1 at the reference radius");
            Assert.AreEqual(0.25f, _field.ScaleAt(new Vector3(0f, 250f, 0f)), 1e-5f, "not proportional to distance");
            Assert.AreEqual(0.01f, _field.ScaleAt(Vector3.zero), 1e-6f, "the centre is not floored at minScale");
            Assert.AreEqual(1f, _field.ScaleAt(new Vector3(0f, 0f, 5000f)), 1e-5f, "not capped at maxScale outside");
        }

        [Test]
        public void Runtime_WithNoField_IsExactlyOne()
        {
            Assert.IsFalse(WarpFieldRuntime.IsActive);
            Assert.AreEqual(1f, WarpFieldRuntime.ScaleAt(new Vector3(3f, 0f, 0f)));
        }

        [Test]
        public void Runtime_ReadsTheFieldAroundItsCentre_AndOnlyItsOwnerReleasesIt()
        {
            var centre = new GameObject("WarpCentre").transform;
            try
            {
                centre.position = new Vector3(500f, 0f, 0f);
                WarpFieldRuntime.Activate(_owner, _field, centre);
                Assert.IsTrue(WarpFieldRuntime.IsActive);
                Assert.AreEqual(0.1f, WarpFieldRuntime.ScaleAt(new Vector3(600f, 0f, 0f)), 1e-4f,
                    "the field is not measured from its centre.");

                WarpFieldRuntime.Release(new object());
                Assert.IsTrue(WarpFieldRuntime.IsActive, "a stranger released the field.");

                WarpFieldRuntime.Release(_owner);
                Assert.IsFalse(WarpFieldRuntime.IsActive, "the owner's instant release left the field on.");
                Assert.AreEqual(1f, WarpFieldRuntime.ScaleAt(new Vector3(600f, 0f, 0f)));
            }
            finally
            {
                Object.DestroyImmediate(centre.gameObject);
            }
        }

        [Test]
        public void BlackHoleCell_CarriesARadialWarp_AndATinyHole()
        {
            var config = AssetDatabase.LoadAssetAtPath<CellConfigDataSO>(CellConfigPath);
            Assert.IsNotNull(config, $"{CellConfigPath} is missing.");
            Assert.IsInstanceOf<RadialWarp>(config.WarpField, "the Black Hole cell has no RadialWarp.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(HolePrefabPath);
            Assert.IsNotNull(prefab, $"{HolePrefabPath} is missing.");
            Assert.IsTrue(prefab.TryGetComponent<SpawnableBlackHole>(out var hole), $"{HolePrefabPath} has no SpawnableBlackHole.");
            float strength = new SerializedObject(hole).FindProperty("strength").floatValue;
            float rs = BlackHoleRegistry.Config.HorizonRadius(strength, 0f);

            // "Tiny" against the world a player normally flies in: at the field's reference radius
            // the shadow is a speck (under 2% of that radius), and toward the floor it is tens of
            // times larger in the player's lengths.
            var warp = (RadialWarp)config.WarpField;
            Assert.Less(rs * 2.6f, warp.ReferenceRadius * 0.02f,
                "the hole is not tiny next to the reference radius — it would not need the warp to look big.");
            Assert.LessOrEqual(warp.MinScale, 0.02f, "the floor is too high for the hole to grow much.");
        }
    }
}
#endif
