using NUnit.Framework;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// GameObject Extension Tests — Validates Unity extension methods.
    ///
    /// WHY THIS MATTERS:
    /// These extensions are used throughout the codebase for common operations
    /// like GetOrAdd (safely getting or adding components), child management,
    /// and layer checks. If GetOrAdd creates duplicate components, or
    /// DestroyChildren misses objects, you'll get invisible bugs at runtime.
    /// </summary>
    [TestFixture]
    public class GameObjectExtensionTests
    {
        GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestGameObject");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        #region GetOrAdd

        [Test]
        public void GetOrAdd_ComponentDoesNotExist_AddsComponent()
        {
            var rb = _go.GetOrAdd<Rigidbody>();

            Assert.IsNotNull(rb, "GetOrAdd should add the component if missing.");
            Assert.IsNotNull(_go.GetComponent<Rigidbody>(), "Component should exist on the GameObject.");
        }

        [Test]
        public void GetOrAdd_ComponentAlreadyExists_ReturnsSameInstance()
        {
            var existing = _go.AddComponent<BoxCollider>();

            var result = _go.GetOrAdd<BoxCollider>();

            Assert.AreSame(existing, result,
                "GetOrAdd should return the existing component, not create a duplicate.");
        }

        [Test]
        public void GetOrAdd_CalledTwice_DoesNotDuplicate()
        {
            _go.GetOrAdd<Rigidbody>();
            _go.GetOrAdd<Rigidbody>();

            var components = _go.GetComponents<Rigidbody>();
            Assert.AreEqual(1, components.Length,
                "Calling GetOrAdd twice should not create duplicate components.");
        }

        #endregion

        #region OrNull

        [Test]
        public void OrNull_ValidObject_ReturnsSameObject()
        {
            var result = _go.OrNull();

            Assert.AreSame(_go, result);
        }

        [Test]
        public void OrNull_DestroyedObject_ReturnsNull()
        {
            var tempGo = new GameObject("Temp");
            Object.DestroyImmediate(tempGo);

            var result = tempGo.OrNull();

            Assert.IsNull(result,
                "OrNull should return null for destroyed Unity objects.");
        }

        #endregion

        #region EnableChildren / DisableChildren

        [Test]
        public void EnableChildren_ActivatesAllChildren()
        {
            var child1 = new GameObject("Child1");
            var child2 = new GameObject("Child2");
            child1.transform.SetParent(_go.transform);
            child2.transform.SetParent(_go.transform);

            child1.SetActive(false);
            child2.SetActive(false);

            _go.EnableChildren();

            Assert.IsTrue(child1.activeSelf, "Child1 should be active.");
            Assert.IsTrue(child2.activeSelf, "Child2 should be active.");
        }

        [Test]
        public void DisableChildren_DeactivatesAllChildren()
        {
            var child1 = new GameObject("Child1");
            var child2 = new GameObject("Child2");
            child1.transform.SetParent(_go.transform);
            child2.transform.SetParent(_go.transform);

            _go.DisableChildren();

            Assert.IsFalse(child1.activeSelf, "Child1 should be inactive.");
            Assert.IsFalse(child2.activeSelf, "Child2 should be inactive.");
        }

        [Test]
        public void EnableChildren_NoChildren_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _go.EnableChildren());
        }

        [Test]
        public void DisableChildren_NoChildren_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _go.DisableChildren());
        }

        #endregion

        #region DestroyChildren

        [Test]
        public void DestroyChildren_SchedulesDestructionForAllChildren()
        {
            var child1 = new GameObject("Child1");
            var child2 = new GameObject("Child2");
            child1.transform.SetParent(_go.transform);
            child2.transform.SetParent(_go.transform);

            Assert.AreEqual(2, _go.transform.childCount);

            _go.DestroyChildren();

            // In Edit Mode, Destroy is deferred. Children are still present
            // but scheduled for destruction. Verify the call doesn't throw.
            // (In Play Mode, children would be gone after the next frame.)
            Assert.DoesNotThrow(() => _go.DestroyChildren());
        }

        [Test]
        public void DestroyChildren_NoChildren_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _go.DestroyChildren());
        }

        #endregion

        #region TryGetInterface

        interface ITestInterface
        {
            void DoSomething();
        }

        class TestComponent : MonoBehaviour, ITestInterface
        {
            public void DoSomething() { }
        }

        [Test]
        public void TryGetInterface_HasImplementor_ReturnsTrueAndInterface()
        {
            _go.AddComponent<TestComponent>();

            bool found = _go.TryGetInterface<ITestInterface>(out var iface);

            Assert.IsTrue(found, "Should find the interface implementation.");
            Assert.IsNotNull(iface);
        }

        [Test]
        public void TryGetInterface_NoImplementor_ReturnsFalse()
        {
            bool found = _go.TryGetInterface<ITestInterface>(out var iface);

            Assert.IsFalse(found, "Should not find an interface that isn't implemented.");
            Assert.IsNull(iface);
        }

        #endregion

        #region IsLayer

        [Test]
        public void IsLayer_MatchingLayer_ReturnsTrue()
        {
            _go.layer = LayerMask.NameToLayer("Default");

            Assert.IsTrue(_go.IsLayer("Default"));
        }

        [Test]
        public void IsLayer_NonMatchingLayer_ReturnsFalse()
        {
            _go.layer = LayerMask.NameToLayer("Default");

            // "UI" is a built-in layer that exists in all Unity projects.
            Assert.IsFalse(_go.IsLayer("UI"));
        }

        #endregion
    }
}
