using CosmicShore.Data;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The upright layout (Docs/MENU_REDESIGN_B.md section 138): a PortraitRect takes its pose when upright and puts the
    /// authored one back on a turn to landscape; a screen with no upright pose is its PC layout fitted to the width; and
    /// only a Ready menu lets the phone turn upright.
    /// </summary>
    public class PortraitRectTests
    {
        GameObject _parent;
        GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _parent = new GameObject("Canvas", typeof(RectTransform));
            ((RectTransform)_parent.transform).sizeDelta = new Vector2(1080f, 2337f);
            _go = new GameObject("Piece", typeof(RectTransform));
            _go.transform.SetParent(_parent.transform, false);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_parent);

        [Test]
        public void TheUprightPoseAppliesAndTheAuthoredOneComesBack()
        {
            var r = (RectTransform)_go.transform;
            r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
            r.anchoredPosition = new Vector2(-60f, -44f);
            r.sizeDelta = new Vector2(400f, 120f);
            var p = _go.AddComponent<PortraitRect>();
            p.SetPose(Vector2.up, Vector2.up, Vector2.up, new Vector2(40f, -230f), new Vector2(760f, 120f), 0.9f);

            p.Apply(true);
            Assert.AreEqual(Vector2.up, r.anchorMin);
            Assert.AreEqual(new Vector2(40f, -230f), r.anchoredPosition);
            Assert.AreEqual(new Vector2(760f, 120f), r.sizeDelta);
            Assert.AreEqual(0.9f, r.localScale.x, 1e-6f);

            p.Apply(false);
            Assert.AreEqual(new Vector2(1f, 1f), r.anchorMin);
            Assert.AreEqual(new Vector2(-60f, -44f), r.anchoredPosition);
            Assert.AreEqual(new Vector2(400f, 120f), r.sizeDelta);
            Assert.AreEqual(Vector3.one, r.localScale);
        }

        [Test]
        public void AScreenWithNoUprightPoseIsItsPcLayoutFittedToTheWidth()
        {
            var r = (RectTransform)_go.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
            var p = _go.AddComponent<PortraitRect>();
            p.SetDrawAtPcScale(true);

            p.Apply(true);
            float s = 1080f / 1920f;
            Assert.AreEqual(s, r.localScale.x, 1e-5f, "the 1920-wide layout fits the 1080-wide canvas");
            Assert.AreEqual(1920f, r.rect.width, 0.05f, "it is laid out 1920 wide");
            Assert.AreEqual(2337f / s, r.rect.height, 0.05f, "and as tall as the screen allows at that scale");

            p.Apply(false);
            Assert.AreEqual(Vector2.zero, r.sizeDelta);
            Assert.AreEqual(Vector3.one, r.localScale);
        }

        [Test]
        public void TheGridTakesItsUprightColumnsAndGivesThemBack()
        {
            var grid = _go.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.cellSize = new Vector2(341f, 296f);
            var p = _go.AddComponent<PortraitRect>();
            p.SetGrid(3, new Vector2(320f, 296f));

            p.Apply(true);
            Assert.AreEqual(3, grid.constraintCount);
            Assert.AreEqual(new Vector2(320f, 296f), grid.cellSize);

            p.Apply(false);
            Assert.AreEqual(5, grid.constraintCount);
            Assert.AreEqual(new Vector2(341f, 296f), grid.cellSize);
        }

        [TestCase(MainMenuState.Ready, true)]
        [TestCase(MainMenuState.Freestyle, false)]
        [TestCase(MainMenuState.LaunchingGame, false)]
        [TestCase(MainMenuState.Initializing, false)]
        [TestCase(MainMenuState.None, false)]
        public void OnlyAReadyMenuTurnsUpright(MainMenuState state, bool upright)
        {
            Assert.AreEqual(upright, MenuOrientation.AllowsPortrait(state));
        }
    }
}
