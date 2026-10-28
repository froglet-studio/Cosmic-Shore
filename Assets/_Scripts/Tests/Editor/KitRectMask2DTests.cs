#if UNITY_EDITOR
using NUnit.Framework;
using TMPro;
using UnityEngine;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Guards for <see cref="KitRectMask2D"/> parking the text under it while it sleeps (#1485): a text
    /// under a mask nobody can see stops TMP's per-frame InternalUpdate, and exactly the texts the mask
    /// parked are given back when it wakes.
    /// </summary>
    [TestFixture]
    public class KitRectMask2DTests
    {
        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        static T Child<T>(Transform parent, string name) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(100f, 100f);
            return go.AddComponent<T>();
        }

        CanvasGroup Window(out KitRectMask2D mask)
        {
            _root = new GameObject("canvas", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)_root.transform).sizeDelta = new Vector2(1000f, 1000f);
            var group = Child<CanvasGroup>(_root.transform, "window");
            mask = group.gameObject.AddComponent<KitRectMask2D>();
            return group;
        }

        [Test]
        public void HiddenMask_ParksItsText_AndGivesItBackOnWake()
        {
            var group = Window(out var mask);
            var text = Child<TextMeshProUGUI>(mask.transform, "label");

            group.alpha = 0f;
            mask.PerformClipping();
            Assert.IsTrue(mask.Asleep);
            Assert.IsTrue(text.isTextObjectScaleStatic, "a text nobody can see is parked");
            Assert.AreEqual(1, mask.ParkedTextCount);

            group.alpha = 1f;
            mask.PerformClipping();
            Assert.IsFalse(mask.Asleep);
            Assert.IsFalse(text.isTextObjectScaleStatic, "the text is given back the frame the window opens");
            Assert.AreEqual(0, mask.ParkedTextCount);
        }

        [Test]
        public void AuthoredStaticText_IsLeftStatic()
        {
            var group = Window(out var mask);
            var text = Child<TextMeshProUGUI>(mask.transform, "label");
            text.isTextObjectScaleStatic = true;

            group.alpha = 0f;
            mask.PerformClipping();
            Assert.AreEqual(0, mask.ParkedTextCount, "a text already static is not the mask's to give back");

            group.alpha = 1f;
            mask.PerformClipping();
            Assert.IsTrue(text.isTextObjectScaleStatic);
        }

        [Test]
        public void DisablingASleepingMask_GivesTheTextBack()
        {
            var group = Window(out var mask);
            var text = Child<TextMeshProUGUI>(mask.transform, "label");

            group.alpha = 0f;
            mask.PerformClipping();
            Assert.IsTrue(text.isTextObjectScaleStatic);

            mask.enabled = false;
            Assert.IsFalse(text.isTextObjectScaleStatic);
            Assert.AreEqual(0, mask.ParkedTextCount);
        }

        [Test]
        public void OuterMaskWaking_LeavesAStillHiddenInnerMasksTextParked()
        {
            var outerGroup = Window(out var outer);
            var outerText = Child<TextMeshProUGUI>(outer.transform, "outer label");
            var innerGroup = Child<CanvasGroup>(outer.transform, "inner");
            var inner = innerGroup.gameObject.AddComponent<KitRectMask2D>();
            var innerText = Child<TextMeshProUGUI>(inner.transform, "inner label");

            // Freestyle: the whole strip goes dark, then the strip comes back with one screen still hidden.
            outerGroup.alpha = 0f;
            innerGroup.alpha = 0f;
            outer.PerformClipping();
            inner.PerformClipping();
            Assert.IsTrue(outerText.isTextObjectScaleStatic);
            Assert.IsTrue(innerText.isTextObjectScaleStatic);

            outerGroup.alpha = 1f;
            outer.PerformClipping();
            Assert.IsFalse(outerText.isTextObjectScaleStatic);
            Assert.IsTrue(innerText.isTextObjectScaleStatic, "the inner mask is still asleep and keeps its text parked");
            Assert.AreEqual(1, inner.ParkedTextCount);

            innerGroup.alpha = 1f;
            inner.PerformClipping();
            Assert.IsFalse(innerText.isTextObjectScaleStatic);
        }
    }
}
#endif
