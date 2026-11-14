#if UNITY_EDITOR
using System.Reflection;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>B or Escape on the Ark (Docs/MENU_REDESIGN_B.md section 205): the shown page steps one level back when it
    /// has levels, the press is refused at its top level, and nothing happens while the Ark is not the active screen.</summary>
    public class ArkTabBackTests
    {
        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        class LevelledPage : MonoBehaviour, IArkTabPage, IArkTabBack
        {
            public int Depth;
            public void SetPageShown(bool shown) { }
            public bool TryBack()
            {
                if (Depth == 0) return false;
                Depth--;
                return true;
            }
        }

        class FlatPage : MonoBehaviour, IArkTabPage
        {
            public void SetPageShown(bool shown) { }
        }

        ArkTabsView Make(out LevelledPage levelled)
        {
            _root = new GameObject("Ark tabs", typeof(RectTransform));
            var view = _root.AddComponent<ArkTabsView>();
            levelled = _root.AddComponent<LevelledPage>();
            var flat = _root.AddComponent<FlatPage>();
            var tabs = new[] { new ArkTabsView.Tab { pageLogic = levelled }, new ArkTabsView.Tab { pageLogic = flat } };
            typeof(ArkTabsView).GetField("tabs", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(view, tabs);
            view.Show(0);
            return view;
        }

        [Test]
        public void Back_StepsTheShownPageUpUntilItsTop()
        {
            var view = Make(out var page);
            view.OnScreenEnter();
            page.Depth = 2;
            Assert.IsTrue(view.TryBack());
            Assert.IsTrue(view.TryBack());
            Assert.AreEqual(0, page.Depth);
            Assert.IsFalse(view.TryBack(), "at its top level the press is not the page's");
        }

        [Test]
        public void Back_IsRefusedOffTheArkAndOnAFlatPage()
        {
            var view = Make(out var page);
            page.Depth = 1;
            Assert.IsFalse(view.TryBack(), "the Ark is not the active screen");
            Assert.AreEqual(1, page.Depth);
            view.OnScreenEnter();
            view.Show(1);
            Assert.IsFalse(view.TryBack(), "a page without levels never takes B");
        }
    }
}
#endif
