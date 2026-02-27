using System;
using NUnit.Framework;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// DisposableGroup Tests — Validates the composite disposal pattern.
    ///
    /// WHY THIS MATTERS:
    /// DisposableGroup aggregates multiple IDisposable objects and disposes them
    /// all at once. This is used for cleaning up event subscriptions, network
    /// connections, and pooled resources. If Dispose() doesn't clear the list,
    /// calling it twice would double-dispose (crash). If Add() silently fails,
    /// resources leak.
    /// </summary>
    [TestFixture]
    public class DisposableGroupTests
    {
        /// <summary>
        /// Simple test disposable that tracks whether Dispose was called.
        /// </summary>
        class TrackingDisposable : IDisposable
        {
            public int DisposeCallCount { get; private set; }
            public bool IsDisposed => DisposeCallCount > 0;

            public void Dispose()
            {
                DisposeCallCount++;
            }
        }

        DisposableGroup _group;

        [SetUp]
        public void SetUp()
        {
            _group = new DisposableGroup();
        }

        #region Dispose

        [Test]
        public void Dispose_DisposesAllAddedItems()
        {
            var d1 = new TrackingDisposable();
            var d2 = new TrackingDisposable();
            var d3 = new TrackingDisposable();

            _group.Add(d1);
            _group.Add(d2);
            _group.Add(d3);

            _group.Dispose();

            Assert.IsTrue(d1.IsDisposed, "First disposable should be disposed.");
            Assert.IsTrue(d2.IsDisposed, "Second disposable should be disposed.");
            Assert.IsTrue(d3.IsDisposed, "Third disposable should be disposed.");
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotDoubleDispose()
        {
            var d1 = new TrackingDisposable();
            _group.Add(d1);

            _group.Dispose();
            _group.Dispose(); // Second call should be a no-op (list was cleared).

            Assert.AreEqual(1, d1.DisposeCallCount,
                "Dispose should only be called once per item. The list is cleared after first Dispose.");
        }

        [Test]
        public void Dispose_EmptyGroup_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _group.Dispose(),
                "Disposing an empty group should not throw.");
        }

        [Test]
        public void Dispose_ClearsList_CanAddNewItemsAfter()
        {
            var d1 = new TrackingDisposable();
            _group.Add(d1);
            _group.Dispose();

            // After Dispose clears the list, we should be able to add new items.
            var d2 = new TrackingDisposable();
            _group.Add(d2);
            _group.Dispose();

            Assert.IsTrue(d2.IsDisposed, "Newly added item after first Dispose should also be disposed.");
            Assert.AreEqual(1, d1.DisposeCallCount,
                "Original item should still only have been disposed once.");
        }

        #endregion

        #region Add

        [Test]
        public void Add_SingleItem_IsTracked()
        {
            var d1 = new TrackingDisposable();

            _group.Add(d1);
            _group.Dispose();

            Assert.IsTrue(d1.IsDisposed);
        }

        [Test]
        public void Add_SameItemTwice_DisposedTwice()
        {
            // Unlike RuntimeCollectionSO, DisposableGroup allows duplicates
            // (it's a simple List<IDisposable>, not a Set).
            var d1 = new TrackingDisposable();
            _group.Add(d1);
            _group.Add(d1);

            _group.Dispose();

            Assert.AreEqual(2, d1.DisposeCallCount,
                "Adding the same item twice means it gets disposed twice.");
        }

        [Test]
        public void Add_ManyItems_AllDisposed()
        {
            var items = new TrackingDisposable[100];
            for (int i = 0; i < 100; i++)
            {
                items[i] = new TrackingDisposable();
                _group.Add(items[i]);
            }

            _group.Dispose();

            for (int i = 0; i < 100; i++)
            {
                Assert.IsTrue(items[i].IsDisposed, $"Item {i} should be disposed.");
            }
        }

        #endregion

        #region IDisposable Contract

        [Test]
        public void Group_ImplementsIDisposable()
        {
            Assert.IsInstanceOf<IDisposable>(_group,
                "DisposableGroup must implement IDisposable for 'using' statement support.");
        }

        [Test]
        public void Group_WorksWithUsingStatement()
        {
            var d1 = new TrackingDisposable();

            using (var group = new DisposableGroup())
            {
                group.Add(d1);
            } // Dispose called here

            Assert.IsTrue(d1.IsDisposed,
                "Items should be disposed when the group leaves a 'using' block.");
        }

        #endregion
    }
}
