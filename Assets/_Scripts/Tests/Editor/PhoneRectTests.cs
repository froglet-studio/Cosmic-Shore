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
        public void AStretchedPcScaleBlockIsLaidOutAt1080AndScaledDown()
        {
            var parent = new GameObject("Canvas", typeof(RectTransform));
            try
            {
                var pr = (RectTransform)parent.transform;
                pr.sizeDelta = new Vector2(974f, 450f);
                _go.transform.SetParent(pr, false);
                var r = (RectTransform)_go.transform;
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.sizeDelta = Vector2.zero;
                var p = _go.AddComponent<PhoneRect>();
                p.SetDrawAtPcScale(true);

                p.Apply(true);
                Assert.AreEqual(1080f, r.rect.height, 0.01f, "the screen is laid out 1080 units tall");
                Assert.AreEqual(974f / (450f / 1080f), r.rect.width, 0.05f, "and as wide as the phone at that scale");
                Assert.AreEqual(450f / 1080f, r.localScale.x, 1e-5f);

                p.Apply(false);
                Assert.AreEqual(Vector2.zero, r.sizeDelta);
                Assert.AreEqual(Vector3.one, r.localScale);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void AnEdgeAnchoredPcScaleBlockKeepsItsAnchorsAndScalesItsOffset()
        {
            var parent = new GameObject("Canvas", typeof(RectTransform));
            try
            {
                var pr = (RectTransform)parent.transform;
                pr.sizeDelta = new Vector2(974f, 450f);
                _go.transform.SetParent(pr, false);
                var r = (RectTransform)_go.transform;
                r.anchorMin = r.anchorMax = Vector2.one;
                r.pivot = Vector2.one;
                r.anchoredPosition = new Vector2(-40f, -232f);
                r.sizeDelta = new Vector2(720f, 200f);
                var p = _go.AddComponent<PhoneRect>();
                p.SetDrawAtPcScale(true);

                p.Apply(true);
                float s = 450f / 1080f;
                Assert.AreEqual(Vector2.one, r.anchorMin);
                Assert.AreEqual(new Vector2(-40f, -232f) * s, r.anchoredPosition);
                Assert.AreEqual(new Vector2(720f, 200f), r.sizeDelta, "an unstretched block keeps its size and is scaled");
                Assert.AreEqual(s, r.localScale.x, 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void TextInsideAPcScaleBlockIsFound()
        {
            var child = new GameObject("Label", typeof(RectTransform));
            try
            {
                child.transform.SetParent(_go.transform, false);
                Assert.IsFalse(PhoneRect.InsidePcScaleBlock(child.transform));
                _go.AddComponent<PhoneRect>().SetDrawAtPcScale(true);
                Assert.IsTrue(PhoneRect.InsidePcScaleBlock(child.transform));
            }
            finally
            {
                Object.DestroyImmediate(child);
            }
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
