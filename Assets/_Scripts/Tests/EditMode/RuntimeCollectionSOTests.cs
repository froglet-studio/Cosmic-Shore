using NUnit.Framework;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// RuntimeCollectionSO Tests — Validates the ScriptableObject-based runtime list.
    ///
    /// WHY THIS MATTERS:
    /// RuntimeCollectionSO is the SOAP-compatible pattern for tracking runtime
    /// collections of objects (e.g., active vessels, spawned prisms, UI panels).
    /// Multiple systems subscribe to ItemAdded/ItemRemoved events to react to
    /// collection changes. If Add doesn't fire the event, listeners won't know
    /// about new items. If Remove allows double-removal, you'll get ghost entries.
    /// </summary>
    [TestFixture]
    public class RuntimeCollectionSOTests
    {
        // Concrete test type since RuntimeCollectionSO is abstract.
        class TestCollection : RuntimeCollectionSO<string> { }

        TestCollection _collection;

        [SetUp]
        public void SetUp()
        {
            _collection = ScriptableObject.CreateInstance<TestCollection>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_collection);
        }

        #region Add

        [Test]
        public void Add_NewItem_AddsToList()
        {
            _collection.Add("item1");

            Assert.AreEqual(1, _collection.Items.Count);
            Assert.Contains("item1", _collection.Items);
        }

        [Test]
        public void Add_DuplicateItem_DoesNotAddAgain()
        {
            _collection.Add("item1");
            _collection.Add("item1");

            Assert.AreEqual(1, _collection.Items.Count,
                "Duplicate items should not be added.");
        }

        [Test]
        public void Add_FiresItemAddedEvent()
        {
            string addedItem = null;
            _collection.ItemAdded += item => addedItem = item;

            _collection.Add("item1");

            Assert.AreEqual("item1", addedItem);
        }

        [Test]
        public void Add_DuplicateItem_DoesNotFireEvent()
        {
            _collection.Add("item1");

            bool eventFired = false;
            _collection.ItemAdded += _ => eventFired = true;

            _collection.Add("item1");

            Assert.IsFalse(eventFired,
                "ItemAdded should not fire when adding a duplicate.");
        }

        [Test]
        public void Add_MultipleItems_AllPresent()
        {
            _collection.Add("a");
            _collection.Add("b");
            _collection.Add("c");

            Assert.AreEqual(3, _collection.Items.Count);
            Assert.Contains("a", _collection.Items);
            Assert.Contains("b", _collection.Items);
            Assert.Contains("c", _collection.Items);
        }

        #endregion

        #region Remove

        [Test]
        public void Remove_ExistingItem_RemovesFromList()
        {
            _collection.Add("item1");
            _collection.Remove("item1");

            Assert.AreEqual(0, _collection.Items.Count);
        }

        [Test]
        public void Remove_FiresItemRemovedEvent()
        {
            _collection.Add("item1");

            string removedItem = null;
            _collection.ItemRemoved += item => removedItem = item;

            _collection.Remove("item1");

            Assert.AreEqual("item1", removedItem);
        }

        [Test]
        public void Remove_NonExistentItem_DoesNotFireEvent()
        {
            bool eventFired = false;
            _collection.ItemRemoved += _ => eventFired = true;

            _collection.Remove("nonexistent");

            Assert.IsFalse(eventFired,
                "ItemRemoved should not fire when removing a non-existent item.");
        }

        [Test]
        public void Remove_NonExistentItem_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _collection.Remove("nonexistent"));
        }

        [Test]
        public void Remove_OnlyRemovesTargetItem()
        {
            _collection.Add("a");
            _collection.Add("b");
            _collection.Add("c");

            _collection.Remove("b");

            Assert.AreEqual(2, _collection.Items.Count);
            Assert.Contains("a", _collection.Items);
            Assert.Contains("c", _collection.Items);
        }

        #endregion

        #region Add/Remove Roundtrip

        [Test]
        public void AddThenRemove_ListIsEmpty()
        {
            _collection.Add("item1");
            _collection.Remove("item1");

            Assert.AreEqual(0, _collection.Items.Count);
        }

        [Test]
        public void AddRemoveAdd_ItemIsPresent()
        {
            _collection.Add("item1");
            _collection.Remove("item1");
            _collection.Add("item1");

            Assert.AreEqual(1, _collection.Items.Count);
            Assert.Contains("item1", _collection.Items);
        }

        #endregion
    }
}
