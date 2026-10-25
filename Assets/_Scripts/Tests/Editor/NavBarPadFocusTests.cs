#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The one test for "the pad is on the top tab bar" (Docs/MENU_REDESIGN_B.md §64; Yash, 2026-10-25: "you have
    /// to navigate the tab menu to access it with LT and RT"). LT / RT page the screens only when this answers true.
    /// </summary>
    public class NavBarPadFocusTests
    {
        GameObject _navBar, _tab, _keycap, _screenControl;

        [SetUp]
        public void SetUp()
        {
            _navBar = new GameObject("NavBar");
            var buttons = new GameObject("NavBarButtons");
            buttons.transform.SetParent(_navBar.transform);
            _tab = new GameObject("HangarLink");
            _tab.transform.SetParent(buttons.transform);
            _keycap = new GameObject("RightArrow");
            _keycap.transform.SetParent(buttons.transform);
            _screenControl = new GameObject("SparrowTile");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_navBar);
            Object.DestroyImmediate(_screenControl);
        }

        [Test]
        public void ATabOrAKeycapOnTheBarIsOnTheBar()
        {
            Assert.IsTrue(ScreenSwitcher.IsOnNavBar(_tab, _navBar.transform));
            Assert.IsTrue(ScreenSwitcher.IsOnNavBar(_keycap, _navBar.transform));
        }

        [Test]
        public void AControlOnAScreenIsNot()
        {
            Assert.IsFalse(ScreenSwitcher.IsOnNavBar(_screenControl, _navBar.transform));
        }

        [Test]
        public void NoSelectionOrNoBarIsNot()
        {
            Assert.IsFalse(ScreenSwitcher.IsOnNavBar(null, _navBar.transform));
            Assert.IsFalse(ScreenSwitcher.IsOnNavBar(_tab, null));
        }

        [Test]
        public void AHiddenTabIsNot()
        {
            _tab.SetActive(false);
            Assert.IsFalse(ScreenSwitcher.IsOnNavBar(_tab, _navBar.transform));
        }
    }
}
#endif
