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

        [Test]
        public void StackedChildrenGoTopDownAndComeBack()
        {
            var hlg = _go.AddComponent<HorizontalLayoutGroup>();
            var a = new GameObject("Hangar", typeof(RectTransform));
            var b = new GameObject("Home", typeof(RectTransform));
            try
            {
                a.transform.SetParent(_go.transform, false);
                b.transform.SetParent(_go.transform, false);
                ((RectTransform)a.transform).anchoredPosition = new Vector2(100f, 0f);
                var p = _go.AddComponent<PhoneRect>();
                p.SetPhoneSizes(0f, Vector2.zero, false);
                p.SetPhoneChildren(false, true, new Vector2(0f, -14f), new Vector2(72f, 56f), 2f, new[] { "Home" }, Vector2.zero, 0);

                p.Apply(true);
                Assert.IsFalse(hlg.enabled, "the band's layout is off while it is a rail");
                var rb = (RectTransform)b.transform;
                var ra = (RectTransform)a.transform;
                Assert.AreEqual(new Vector2(0f, -14f), rb.anchoredPosition, "Stack Order puts Home first");
                Assert.AreEqual(new Vector2(0f, -14f - 58f), ra.anchoredPosition);
                Assert.AreEqual(new Vector2(72f, 56f), ra.sizeDelta);

                p.Apply(false);
                Assert.IsTrue(hlg.enabled);
                Assert.AreEqual(new Vector2(100f, 0f), ra.anchoredPosition);
            }
            finally
            {
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void GridTakesItsPhoneColumnsAndCellsAndComesBack()
        {
            var grid = _go.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(182f, 150f);
            grid.constraint = GridLayoutGroup.Constraint.Flexible;
            var p = _go.AddComponent<PhoneRect>();
            p.SetPhoneSizes(0f, Vector2.zero, false);
            p.SetPhoneChildren(false, false, Vector2.zero, Vector2.zero, 0f, null, new Vector2(186f, 104f), 4);

            p.Apply(true);
            Assert.AreEqual(new Vector2(186f, 104f), grid.cellSize);
            Assert.AreEqual(GridLayoutGroup.Constraint.FixedColumnCount, grid.constraint);
            Assert.AreEqual(4, grid.constraintCount);

            p.Apply(false);
            Assert.AreEqual(new Vector2(182f, 150f), grid.cellSize);
            Assert.AreEqual(GridLayoutGroup.Constraint.Flexible, grid.constraint);
        }

        [Test]
        public void HiddenOnAPhoneShowsAgainOnPc()
        {
            var g = _go.AddComponent<CanvasGroup>();
            var p = _go.AddComponent<PhoneRect>();
            p.SetPhoneSizes(0f, Vector2.zero, false);
            p.SetPhoneChildren(true, false, Vector2.zero, Vector2.zero, 0f, null, Vector2.zero, 0);
            p.Apply(true);
            Assert.AreEqual(0f, g.alpha);
            Assert.IsFalse(g.blocksRaycasts);
            p.Apply(false);
            Assert.AreEqual(1f, g.alpha);
            Assert.IsTrue(g.blocksRaycasts);
        }

        [Test]
        public void AWidthScaledBlockWidensByOneOverItsScaleAndKeepsItsHeight()
        {
            var parent = new GameObject("Viewport", typeof(RectTransform));
            try
            {
                var pr = (RectTransform)parent.transform;
                pr.sizeDelta = new Vector2(738f, 400f);
                _go.transform.SetParent(pr, false);
                var r = (RectTransform)_go.transform;
                r.anchorMin = new Vector2(0f, 1f);
                r.anchorMax = new Vector2(1f, 1f);
                r.pivot = new Vector2(0.5f, 1f);
                r.sizeDelta = new Vector2(0f, 2200f);
                r.anchoredPosition = new Vector2(0f, 120f);
                var p = _go.AddComponent<PhoneRect>();
                p.SetPhoneSizes(0f, Vector2.zero, false, 0f, 0.68f);

                p.Apply(true);
                Assert.AreEqual(738f / 0.68f, r.rect.width, 0.05f, "laid out as wide as the viewport at that scale");
                Assert.AreEqual(2200f, r.sizeDelta.y, "its height is left to its fitter");
                Assert.AreEqual(new Vector2(0f, 120f), r.anchoredPosition, "its scroll position is left alone");
                Assert.AreEqual(0.68f, r.localScale.x, 1e-5f);
                Assert.IsTrue(PhoneRect.InsidePcScaleBlock(r), "its KitTexts keep their PC sizes");

                p.Apply(false);
                Assert.AreEqual(0f, r.sizeDelta.x);
                Assert.AreEqual(Vector3.one, r.localScale);
            }
            finally
            {
                Object.DestroyImmediate(parent);
            }
        }
    }
}
#endif
