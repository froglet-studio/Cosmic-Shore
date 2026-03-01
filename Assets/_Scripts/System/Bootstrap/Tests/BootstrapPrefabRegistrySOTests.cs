using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Core
{
    [TestFixture]
    public class BootstrapPrefabRegistrySOTests
    {
        BootstrapPrefabRegistrySO _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = ScriptableObject.CreateInstance<BootstrapPrefabRegistrySO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_registry);
        }

        [Test]
        public void CreateInstance_ReturnsNonNull()
        {
            Assert.IsNotNull(_registry);
        }

        [Test]
        public void DefaultEntries_IsEmpty()
        {
            Assert.IsNotNull(_registry.Entries);
            Assert.AreEqual(0, _registry.Entries.Count);
        }

        [Test]
        public void SetEntriesViaSerializedObject_ReturnsUpdatedValues()
        {
            var prefab = new GameObject("TestPrefab");

            var so = new UnityEditor.SerializedObject(_registry);
            var entries = so.FindProperty("_entries");
            entries.arraySize = 1;

            var element = entries.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("Prefab").objectReferenceValue = prefab;
            element.FindPropertyRelative("Persistent").boolValue = true;
            element.FindPropertyRelative("Position").vector3Value = new Vector3(1, 2, 3);
            element.FindPropertyRelative("Rotation").vector3Value = new Vector3(0, 90, 0);
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(1, _registry.Entries.Count);
            Assert.AreEqual(prefab, _registry.Entries[0].Prefab);
            Assert.IsTrue(_registry.Entries[0].Persistent);
            Assert.AreEqual(new Vector3(1, 2, 3), _registry.Entries[0].Position);
            Assert.AreEqual(new Vector3(0, 90, 0), _registry.Entries[0].Rotation);

            Object.DestroyImmediate(prefab);
        }

        [Test]
        public void MultipleEntries_PreservesOrder()
        {
            var prefabA = new GameObject("PrefabA");
            var prefabB = new GameObject("PrefabB");
            var prefabC = new GameObject("PrefabC");

            var so = new UnityEditor.SerializedObject(_registry);
            var entries = so.FindProperty("_entries");
            entries.arraySize = 3;

            entries.GetArrayElementAtIndex(0).FindPropertyRelative("Prefab").objectReferenceValue = prefabA;
            entries.GetArrayElementAtIndex(1).FindPropertyRelative("Prefab").objectReferenceValue = prefabB;
            entries.GetArrayElementAtIndex(2).FindPropertyRelative("Prefab").objectReferenceValue = prefabC;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(3, _registry.Entries.Count);
            Assert.AreEqual(prefabA, _registry.Entries[0].Prefab);
            Assert.AreEqual(prefabB, _registry.Entries[1].Prefab);
            Assert.AreEqual(prefabC, _registry.Entries[2].Prefab);

            Object.DestroyImmediate(prefabA);
            Object.DestroyImmediate(prefabB);
            Object.DestroyImmediate(prefabC);
        }

        [Test]
        public void DefaultEntry_PersistentIsFalse()
        {
            var so = new UnityEditor.SerializedObject(_registry);
            var entries = so.FindProperty("_entries");
            entries.arraySize = 1;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.IsFalse(_registry.Entries[0].Persistent);
        }

        [Test]
        public void DefaultEntry_PositionIsZero()
        {
            var so = new UnityEditor.SerializedObject(_registry);
            var entries = so.FindProperty("_entries");
            entries.arraySize = 1;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(Vector3.zero, _registry.Entries[0].Position);
        }

        [Test]
        public void DefaultEntry_RotationIsZero()
        {
            var so = new UnityEditor.SerializedObject(_registry);
            var entries = so.FindProperty("_entries");
            entries.arraySize = 1;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(Vector3.zero, _registry.Entries[0].Rotation);
        }
    }
}
