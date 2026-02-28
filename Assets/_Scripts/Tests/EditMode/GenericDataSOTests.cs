using NUnit.Framework;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// GenericDataSO Tests — Validates the ScriptableObject data container pattern.
    ///
    /// WHY THIS MATTERS:
    /// GenericDataSO is the base class for all runtime data containers (IntDataSO,
    /// StringDataSO, etc.). It provides value storage with change notification events.
    /// If the Value setter stops firing OnValueChanged, any UI or system that listens
    /// for value changes will silently break — scores won't update, HUD won't refresh, etc.
    /// </summary>
    [TestFixture]
    public class GenericDataSOTests
    {
        IntDataSO _intData;
        StringDataSO _stringData;

        [SetUp]
        public void SetUp()
        {
            _intData = ScriptableObject.CreateInstance<IntDataSO>();
            _stringData = ScriptableObject.CreateInstance<StringDataSO>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_intData);
            Object.DestroyImmediate(_stringData);
        }

        #region IntDataSO

        [Test]
        public void IntDataSO_DefaultValue_IsZero()
        {
            Assert.AreEqual(0, _intData.Value);
        }

        [Test]
        public void IntDataSO_SetValue_ReturnsNewValue()
        {
            _intData.Value = 42;

            Assert.AreEqual(42, _intData.Value);
        }

        [Test]
        public void IntDataSO_SetValue_FiresOnValueChanged()
        {
            bool eventFired = false;
            _intData.OnValueChanged += () => eventFired = true;

            _intData.Value = 10;

            Assert.IsTrue(eventFired, "OnValueChanged should fire when Value is set.");
        }

        [Test]
        public void IntDataSO_SetSameValue_StillFiresEvent()
        {
            // Setting to the same value should still notify — this is by design
            // (systems may need to re-process even if the value didn't change).
            _intData.Value = 5;
            bool eventFired = false;
            _intData.OnValueChanged += () => eventFired = true;

            _intData.Value = 5;

            Assert.IsTrue(eventFired,
                "OnValueChanged should fire even when setting the same value.");
        }

        [Test]
        public void IntDataSO_ImplicitConversion_ReturnsValue()
        {
            _intData.Value = 99;

            int result = _intData; // implicit conversion

            Assert.AreEqual(99, result);
        }

        [Test]
        public void IntDataSO_MultipleSubscribers_AllNotified()
        {
            int callCount = 0;
            _intData.OnValueChanged += () => callCount++;
            _intData.OnValueChanged += () => callCount++;

            _intData.Value = 1;

            Assert.AreEqual(2, callCount, "All subscribers should be notified.");
        }

        #endregion

        #region StringDataSO

        [Test]
        public void StringDataSO_DefaultValue_IsNull()
        {
            Assert.IsNull(_stringData.Value);
        }

        [Test]
        public void StringDataSO_SetValue_ReturnsNewValue()
        {
            _stringData.Value = "Hello Cosmic Shore";

            Assert.AreEqual("Hello Cosmic Shore", _stringData.Value);
        }

        [Test]
        public void StringDataSO_SetValue_FiresOnValueChanged()
        {
            bool eventFired = false;
            _stringData.OnValueChanged += () => eventFired = true;

            _stringData.Value = "test";

            Assert.IsTrue(eventFired);
        }

        [Test]
        public void StringDataSO_ImplicitConversion_ReturnsValue()
        {
            _stringData.Value = "vessel";

            string result = _stringData; // implicit conversion

            Assert.AreEqual("vessel", result);
        }

        [Test]
        public void StringDataSO_SetToNull_FiresEvent()
        {
            _stringData.Value = "something";
            bool eventFired = false;
            _stringData.OnValueChanged += () => eventFired = true;

            _stringData.Value = null;

            Assert.IsTrue(eventFired);
            Assert.IsNull(_stringData.Value);
        }

        #endregion

        #region ScriptableObject Lifecycle

        [Test]
        public void CreateInstance_IntDataSO_IsNotNull()
        {
            Assert.IsNotNull(_intData);
            Assert.IsInstanceOf<IntDataSO>(_intData);
            Assert.IsInstanceOf<GenericDataSO<int>>(_intData);
        }

        [Test]
        public void CreateInstance_StringDataSO_IsNotNull()
        {
            Assert.IsNotNull(_stringData);
            Assert.IsInstanceOf<StringDataSO>(_stringData);
            Assert.IsInstanceOf<GenericDataSO<string>>(_stringData);
        }

        #endregion
    }
}
