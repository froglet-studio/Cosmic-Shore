#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// PhoneRect (Docs/UI_STYLE_GUIDE.md §8, D6): a panel takes its phone pose when the phone layout turns on and gets
    /// its authored pose back, exactly, when it turns off.
    /// </summary>
    public class PhoneRectTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp() => _go = new GameObject("Panel", typeof(RectTransform));

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_go);

        private PhoneRect Make(out RectTransform r)
        {
            r = (RectTransform)_go.transform;
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, 120f);
            r.sizeDelta = new Vector2(600f, 520f);
            var p = _go.AddComponent<PhoneRect>();
            p.SetPhonePose(new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-78f, 0f), new Vector2(300f, 380f));
            return p;
        }

        [Test]
        public void PhoneTakesThePhonePose()
        {
            var p = Make(out var r);
            p.Apply(true);
            Assert.IsTrue(p.OnPhone);
            Assert.AreEqual(new Vector2(1f, 0.5f), r.anchorMin);
            Assert.AreEqual(new Vector2(1f, 0.5f), r.pivot);
            Assert.AreEqual(new Vector2(-78f, 0f), r.anchoredPosition);
            Assert.AreEqual(new Vector2(300f, 380f), r.sizeDelta);
        }

        [Test]
        public void TurningOffRestoresTheAuthoredPose()
        {
            var p = Make(out var r);
            p.Apply(true);
            p.Apply(false);
            Assert.IsFalse(p.OnPhone);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), r.anchorMin);
            Assert.AreEqual(new Vector2(0.5f, 1f), r.pivot);
            Assert.AreEqual(new Vector2(0f, 120f), r.anchoredPosition);
            Assert.AreEqual(new Vector2(600f, 520f), r.sizeDelta);
        }

        [Test]
        public void ApplyingTwiceKeepsTheAuthoredPose()
        {
            var p = Make(out var r);
            p.Apply(true);
            p.Apply(true);
            p.Apply(false);
            Assert.AreEqual(new Vector2(0f, 120f), r.anchoredPosition, "a second phone apply must not record the phone pose as authored");
        }

        [Test]
        public void TurningOffBeforeEverTurningOnChangesNothing()
        {
            var p = Make(out var r);
            p.Apply(false);
            Assert.AreEqual(new Vector2(600f, 520f), r.sizeDelta);
        }

        [Test]
        public void PhoneTextSizeAndLayoutSizeSwapAndRestore()
        {
            var text = _go.AddComponent<TextMeshProUGUI>();
            text.fontSize = 28f;
            var le = _go.AddComponent<LayoutElement>();
            le.preferredWidth = 1400f;
            le.preferredHeight = 84f;
            var p = _go.AddComponent<PhoneRect>();
            p.SetPhoneSizes(16f, new Vector2(0f, 52f), false);

            p.Apply(true);
            Assert.AreEqual(16f, text.fontSize);
            Assert.AreEqual(1400f, le.preferredWidth, "a 0 axis is left alone");
            Assert.AreEqual(52f, le.preferredHeight);

            p.Apply(false);
            Assert.AreEqual(28f, text.fontSize);
            Assert.AreEqual(84f, le.preferredHeight);
        }

        [Test]
        public void PhoneScaleSwapsAndRestores()
        {
            var r = (RectTransform)_go.transform;
            var p = _go.AddComponent<PhoneRect>();
            p.SetPhoneSizes(0f, Vector2.zero, false, 0.54f);
            p.Apply(true);
            Assert.AreEqual(new Vector3(0.54f, 0.54f, 1f), r.localScale);
            p.Apply(false);
            Assert.AreEqual(Vector3.one, r.localScale);
        }

        [Test]
        public void ARectThatDoesNotMoveKeepsItsPose()
        {
            var r = (RectTransform)_go.transform;
            r.anchoredPosition = new Vector2(12f, 34f);
            var p = _go.AddComponent<PhoneRect>();
            p.SetPhoneSizes(0f, Vector2.zero, false);
            p.Apply(true);
            Assert.AreEqual(new Vector2(12f, 34f), r.anchoredPosition);
        }
    }
}
#endif
