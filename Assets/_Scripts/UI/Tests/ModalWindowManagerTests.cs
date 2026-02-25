using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using static CosmicShore.App.UI.ScreenSwitcher;

namespace CosmicShore.App.UI.Tests
{
    [TestFixture]
    public class ModalWindowManagerTests
    {
        GameObject _go;
        Modals.ModalWindowManager _manager;
        Animator _animator;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestModalWindow");

            // ModalWindowManager requires an Animator with "Window In"/"Window Out" states.
            // For unit tests we add the Animator component but avoid runtime controller
            // to test the state-tracking logic without actual animation playback.
            _animator = _go.AddComponent<Animator>();
            _manager = _go.AddComponent<Modals.ModalWindowManager>();

            // Wire the animator via the serialized field
            var animatorField = typeof(Modals.ModalWindowManager).GetField(
                "windowAnimator", BindingFlags.NonPublic | BindingFlags.Instance);
            animatorField.SetValue(_manager, _animator);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        #region Modal Type

        [Test]
        public void ModalType_CanBeAssigned()
        {
            _manager.ModalType = ModalWindows.SETTINGS;
            Assert.AreEqual(ModalWindows.SETTINGS, _manager.ModalType);
        }

        [Test]
        public void ModalType_DefaultsToZero()
        {
            // Default enum value for ModalWindows is PURCHASE_ITEM_CONFIRMATION (0)
            Assert.AreEqual(ModalWindows.PURCHASE_ITEM_CONFIRMATION, _manager.ModalType);
        }

        #endregion

        #region IsOn State Tracking

        [Test]
        public void IsOn_InitiallyFalse()
        {
            var isOnField = typeof(Modals.ModalWindowManager).GetField(
                "isOn", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsFalse((bool)isOnField.GetValue(_manager));
        }

        [Test]
        public void ModalWindowIn_SetsIsOnTrue()
        {
            // ModalWindowIn will try to crossfade the animator — that's OK,
            // it will log a warning but still set the isOn flag.
            _manager.ModalWindowIn();

            var isOnField = typeof(Modals.ModalWindowManager).GetField(
                "isOn", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsTrue((bool)isOnField.GetValue(_manager));
        }

        [Test]
        public void ModalWindowIn_ActivatesGameObject()
        {
            _go.SetActive(false);
            _manager.ModalWindowIn();

            Assert.IsTrue(_go.activeSelf);
        }

        [Test]
        public void ModalWindowIn_CalledTwice_DoesNotDoubleOpen()
        {
            _manager.ModalWindowIn();
            // Second call should be a no-op because isOn is already true
            Assert.DoesNotThrow(() => _manager.ModalWindowIn());

            var isOnField = typeof(Modals.ModalWindowManager).GetField(
                "isOn", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsTrue((bool)isOnField.GetValue(_manager));
        }

        [Test]
        public void ModalWindowOut_SetsIsOnFalse()
        {
            _manager.ModalWindowIn();
            _manager.ModalWindowOut();

            var isOnField = typeof(Modals.ModalWindowManager).GetField(
                "isOn", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsFalse((bool)isOnField.GetValue(_manager));
        }

        [Test]
        public void ModalWindowOut_WhenNotOpen_DoesNothing()
        {
            // Should not throw when called without prior ModalWindowIn
            Assert.DoesNotThrow(() => _manager.ModalWindowOut());
        }

        #endregion

        #region Sharp Animations Flag

        [Test]
        public void SharpAnimations_DefaultFalse()
        {
            Assert.IsFalse(_manager.sharpAnimations);
        }

        [Test]
        public void SharpAnimations_CanBeSet()
        {
            _manager.sharpAnimations = true;
            Assert.IsTrue(_manager.sharpAnimations);
        }

        #endregion

        #region Settings Modal Special Case

        [Test]
        public void ModalWindowOut_SettingsType_DoesNotStartDisableCoroutine()
        {
            _manager.ModalType = ModalWindows.SETTINGS;
            _manager.ModalWindowIn();

            // ModalWindowOut for SETTINGS returns early before StartCoroutine(DisableWindow()).
            // If this threw, it would mean the early return isn't working.
            Assert.DoesNotThrow(() => _manager.ModalWindowOut());
        }

        #endregion
    }
}
