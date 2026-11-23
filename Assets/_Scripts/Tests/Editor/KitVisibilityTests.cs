#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Guards for the style-B show / hide contract (Docs/MENU_REDESIGN_B.md): a hidden part is
    /// invisible, takes no input and leaves its layout - and nothing is SetActive along the way, so
    /// a hide never fires OnDisable. <see cref="KitListCounter"/> rides the same contract to keep a
    /// list's header and empty line in step with its rows.
    /// </summary>
    [TestFixture]
    public class KitVisibilityTests
    {
        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        static CanvasGroup Part(Transform parent, string name, bool withLayout = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            if (withLayout) go.AddComponent<LayoutElement>();
            return go.GetComponent<CanvasGroup>();
        }

        [Test]
        public void SetShown_HidesThroughTheGroup_AndLeavesTheLayout()
        {
            _root = new GameObject("root", typeof(RectTransform));
            var part = Part(_root.transform, "part");

            KitVisibility.SetShown(part, false);

            Assert.AreEqual(0f, part.alpha);
            Assert.IsFalse(part.interactable);
            Assert.IsFalse(part.blocksRaycasts);
            Assert.IsTrue(part.GetComponent<LayoutElement>().ignoreLayout, "a hidden part must close up its row");
            Assert.IsTrue(part.gameObject.activeSelf, "hiding must never deactivate the object");
            Assert.IsFalse(KitVisibility.IsShown(part));

            KitVisibility.SetShown(part, true);

            Assert.AreEqual(1f, part.alpha);
            Assert.IsTrue(part.interactable && part.blocksRaycasts);
            Assert.IsFalse(part.GetComponent<LayoutElement>().ignoreLayout);
            Assert.IsTrue(KitVisibility.IsShown(part));
        }

        [Test]
        public void SetShown_WithoutALayoutElement_StillHides()
        {
            _root = new GameObject("root", typeof(RectTransform));
            var part = Part(_root.transform, "part", withLayout: false);

            KitVisibility.SetShown(part, false);

            Assert.AreEqual(0f, part.alpha);
            Assert.IsNull(part.GetComponent<LayoutElement>(), "SetShown must not add a LayoutElement");
        }

        [Test]
        public void SetShown_HideDuringAnEntrancesDelay_StaysHidden()
        {
            // The configure window's AI pilots row: shown on the last card, the window opens again
            // (its entrances replay, holding alpha at 0 through the delay), and the new card hides
            // the row. Without settling the entrance first it faded back up with raycasts off.
            _root = new GameObject("root", typeof(RectTransform));
            var part = Part(_root.transform, "part");
            var entrance = part.gameObject.AddComponent<KitEntrance>();
            typeof(KitEntrance).GetMethod("Awake", System.Reflection.BindingFlags.Instance |
                                                   System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(entrance, null);   // edit mode runs no Awake
            KitVisibility.SetShown(part, true);

            entrance.Play();
            Assert.IsTrue(entrance.IsPlaying, "the entrance must be mid-play for this test to mean anything");
            Assert.AreEqual(0f, part.alpha, "an entrance starts at alpha 0");

            KitVisibility.SetShown(part, false);
            entrance.Finish();   // what the entrance's own clock would do once it ran out

            Assert.IsFalse(entrance.IsPlaying);
            Assert.AreEqual(0f, part.alpha, "the hide must win over the entrance's remembered alpha");
            Assert.IsFalse(part.blocksRaycasts);
        }

        [Test]
        public void ListCounter_ShowsItsHeaderOnlyWithRows_AndItsEmptyLineOnlyWithout()
        {
            _root = new GameObject("root", typeof(RectTransform));
            var header = Part(_root.transform, "header");
            var empty = Part(_root.transform, "empty");
            var list = new GameObject("list", typeof(RectTransform));
            list.transform.SetParent(_root.transform, false);
            var counter = list.AddComponent<KitListCounter>();
            SetField(counter, "shownWhileFilled", new[] { header });
            SetField(counter, "shownWhileEmpty", empty);

            counter.Recount();
            Assert.AreEqual(0, counter.Count);
            Assert.IsFalse(KitVisibility.IsShown(header));
            Assert.IsTrue(KitVisibility.IsShown(empty));

            new GameObject("row", typeof(RectTransform)).transform.SetParent(list.transform, false);
            counter.Recount();
            Assert.AreEqual(1, counter.Count);
            Assert.IsTrue(KitVisibility.IsShown(header));
            Assert.IsFalse(KitVisibility.IsShown(empty));
        }

        static void SetField(object target, string name, object value)
        {
            var f = target.GetType().GetField(name, System.Reflection.BindingFlags.Instance |
                                                    System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"{target.GetType().Name}.{name} was renamed - update this test");
            f.SetValue(target, value);
        }
    }
}
#endif
