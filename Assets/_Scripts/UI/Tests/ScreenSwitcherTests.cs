using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using static CosmicShore.App.UI.ScreenSwitcher;

namespace CosmicShore.App.UI.Tests
{
    [TestFixture]
    public class ScreenSwitcherTests
    {
        GameObject _go;
        ScreenSwitcher _switcher;

        // Reflection helpers
        static FieldInfo Field(string name) =>
            typeof(ScreenSwitcher).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        static MethodInfo Method(string name) =>
            typeof(ScreenSwitcher).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("ReturnToScreen");
            PlayerPrefs.DeleteKey("ReturnToModal");

            _go = new GameObject("TestScreenSwitcher");
            _switcher = _go.AddComponent<ScreenSwitcher>();

            // Wire up an empty modal stack (field is serialized, so it's already initialized)
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);

            PlayerPrefs.DeleteKey("ReturnToScreen");
            PlayerPrefs.DeleteKey("ReturnToModal");
        }

        #region Modal Stack

        [Test]
        public void PushModal_AddsToStack()
        {
            _switcher.PushModal(ModalWindows.PROFILE);

            Assert.IsTrue(_switcher.ModalIsActive(ModalWindows.PROFILE));
        }

        [Test]
        public void PushModal_MultiplePushes_TopOfStackIsActive()
        {
            _switcher.PushModal(ModalWindows.PROFILE);
            _switcher.PushModal(ModalWindows.SETTINGS);

            Assert.IsTrue(_switcher.ModalIsActive(ModalWindows.SETTINGS));
            Assert.IsFalse(_switcher.ModalIsActive(ModalWindows.PROFILE));
        }

        [Test]
        public void PopModal_RemovesTopFromStack()
        {
            _switcher.PushModal(ModalWindows.PROFILE);
            _switcher.PushModal(ModalWindows.SETTINGS);
            _switcher.PopModal();

            Assert.IsTrue(_switcher.ModalIsActive(ModalWindows.PROFILE));
            Assert.IsFalse(_switcher.ModalIsActive(ModalWindows.SETTINGS));
        }

        [Test]
        public void PopModal_EmptyStack_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _switcher.PopModal());
        }

        [Test]
        public void PopModal_SingleItem_StackBecomesEmpty()
        {
            _switcher.PushModal(ModalWindows.DAILY_CHALLENGE);
            _switcher.PopModal();

            Assert.IsFalse(_switcher.ModalIsActive(ModalWindows.DAILY_CHALLENGE));
        }

        [Test]
        public void ModalIsActive_EmptyStack_ReturnsFalse()
        {
            Assert.IsFalse(_switcher.ModalIsActive(ModalWindows.PROFILE));
            Assert.IsFalse(_switcher.ModalIsActive(ModalWindows.NONE));
        }

        [Test]
        public void PushModal_SameModalTwice_BothOnStack()
        {
            _switcher.PushModal(ModalWindows.SETTINGS);
            _switcher.PushModal(ModalWindows.SETTINGS);

            // Pop once — still active because a second instance remains
            _switcher.PopModal();
            Assert.IsTrue(_switcher.ModalIsActive(ModalWindows.SETTINGS));
        }

        #endregion

        #region Return State (PlayerPrefs)

        [Test]
        public void SetReturnToScreen_PersistsToPlayerPrefs()
        {
            _switcher.SetReturnToScreen(MenuScreens.HANGAR);

            Assert.IsTrue(PlayerPrefs.HasKey("ReturnToScreen"));
            Assert.AreEqual((int)MenuScreens.HANGAR, PlayerPrefs.GetInt("ReturnToScreen"));
        }

        [Test]
        public void SetReturnToModal_PersistsToPlayerPrefs()
        {
            _switcher.SetReturnToModal(ModalWindows.ARCADE_GAME_CONFIGURE);

            Assert.IsTrue(PlayerPrefs.HasKey("ReturnToModal"));
            Assert.AreEqual((int)ModalWindows.ARCADE_GAME_CONFIGURE, PlayerPrefs.GetInt("ReturnToModal"));
        }

        [Test]
        public void SetReturnToModal_None_DeletesKey()
        {
            // First set a value
            _switcher.SetReturnToModal(ModalWindows.PROFILE);
            Assert.IsTrue(PlayerPrefs.HasKey("ReturnToModal"));

            // Then set NONE
            _switcher.SetReturnToModal(ModalWindows.NONE);
            Assert.IsFalse(PlayerPrefs.HasKey("ReturnToModal"));
        }

        [Test]
        public void PushModal_UpdatesReturnToModalPref()
        {
            _switcher.PushModal(ModalWindows.FACTION_MISSION);

            Assert.AreEqual(
                (int)ModalWindows.FACTION_MISSION,
                PlayerPrefs.GetInt("ReturnToModal"));
        }

        [Test]
        public void PopModal_ToEmpty_ClearsReturnToModalPref()
        {
            _switcher.PushModal(ModalWindows.HANGAR_TRAINING);
            _switcher.PopModal();

            Assert.IsFalse(PlayerPrefs.HasKey("ReturnToModal"));
        }

        #endregion

        #region Screen Queries

        [Test]
        public void ScreenIsActive_DefaultScreen_IsZero()
        {
            // currentScreen defaults to 0, which maps to STORE when no screens list is configured
            Assert.IsTrue(_switcher.ScreenIsActive(MenuScreens.STORE));
        }

        [Test]
        public void ScreenIsActive_NonCurrentScreen_ReturnsFalse()
        {
            Assert.IsFalse(_switcher.ScreenIsActive(MenuScreens.HANGAR));
        }

        #endregion

        #region Screen Mapping Helpers (via reflection)

        [Test]
        public void GetScreenCount_NoScreensList_ReturnsChildCount()
        {
            // Add some child transforms to simulate screen panels
            new GameObject("Screen0").transform.SetParent(_go.transform);
            new GameObject("Screen1").transform.SetParent(_go.transform);
            new GameObject("Screen2").transform.SetParent(_go.transform);

            var method = Method("GetScreenCount");
            var count = (int)method.Invoke(_switcher, null);

            Assert.AreEqual(3, count);
        }

        [Test]
        public void GetScreenIdForIndex_NoScreensList_ReturnsEnumCast()
        {
            var method = Method("GetScreenIdForIndex");
            var result = (MenuScreens)method.Invoke(_switcher, new object[] { 2 });

            Assert.AreEqual(MenuScreens.HOME, result);
        }

        [Test]
        public void GetIndexForScreen_NoScreensList_ReturnsEnumInt()
        {
            var method = Method("GetIndexForScreen");
            var result = (int)method.Invoke(_switcher, new object[] { MenuScreens.PORT });

            Assert.AreEqual((int)MenuScreens.PORT, result);
        }

        #endregion

        #region Navigation Bounds

        [Test]
        public void NavigateLeft_AtZero_DoesNotChangeScreen()
        {
            // currentScreen starts at 0
            var field = Field("currentScreen");
            field.SetValue(_switcher, 0);

            // Add children so GetScreenCount > 0
            new GameObject("S0").transform.SetParent(_go.transform);
            new GameObject("S1").transform.SetParent(_go.transform);

            var navigateLeft = typeof(ScreenSwitcher).GetMethod(
                "NavigateLeft", BindingFlags.NonPublic | BindingFlags.Instance);
            navigateLeft.Invoke(_switcher, null);

            Assert.AreEqual(0, (int)field.GetValue(_switcher));
        }

        [Test]
        public void NavigateRight_AtMaxScreen_DoesNotChangeScreen()
        {
            new GameObject("S0").transform.SetParent(_go.transform);
            new GameObject("S1").transform.SetParent(_go.transform);

            var field = Field("currentScreen");
            field.SetValue(_switcher, 1); // max index

            var navigateRight = typeof(ScreenSwitcher).GetMethod(
                "NavigateRight", BindingFlags.NonPublic | BindingFlags.Instance);
            navigateRight.Invoke(_switcher, null);

            Assert.AreEqual(1, (int)field.GetValue(_switcher));
        }

        #endregion

        #region Enum Value Coverage

        [Test]
        public void MenuScreens_HasExpectedValues()
        {
            Assert.AreEqual(0, (int)MenuScreens.STORE);
            Assert.AreEqual(1, (int)MenuScreens.ARK);
            Assert.AreEqual(2, (int)MenuScreens.HOME);
            Assert.AreEqual(3, (int)MenuScreens.PORT);
            Assert.AreEqual(4, (int)MenuScreens.HANGAR);
        }

        [Test]
        public void ModalWindows_NoneIsNegativeOne()
        {
            Assert.AreEqual(-1, (int)ModalWindows.NONE);
        }

        [Test]
        public void ModalWindows_HasAllExpectedValues()
        {
            Assert.AreEqual(0, (int)ModalWindows.PURCHASE_ITEM_CONFIRMATION);
            Assert.AreEqual(1, (int)ModalWindows.ARCADE_GAME_CONFIGURE);
            Assert.AreEqual(2, (int)ModalWindows.DAILY_CHALLENGE);
            Assert.AreEqual(3, (int)ModalWindows.PROFILE);
            Assert.AreEqual(4, (int)ModalWindows.PROFILE_ICON_SELECT);
            Assert.AreEqual(5, (int)ModalWindows.SETTINGS);
            Assert.AreEqual(7, (int)ModalWindows.FACTION_MISSION);
            Assert.AreEqual(8, (int)ModalWindows.SQUAD_MEMBER_CONFIGURE);
            Assert.AreEqual(9, (int)ModalWindows.HANGAR_TRAINING);
        }

        #endregion
    }
}
