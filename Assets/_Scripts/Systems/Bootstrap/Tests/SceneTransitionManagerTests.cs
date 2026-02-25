using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Systems.Bootstrap.Tests
{
    [TestFixture]
    public class SceneTransitionManagerTests
    {
        GameObject _go;
        SceneTransitionManager _manager;

        [SetUp]
        public void SetUp()
        {
            ServiceLocator.ClearAll();

            _go = new GameObject("TestSceneTransition");
            // AddComponent calls Awake(), which creates the overlay and registers in ServiceLocator.
            _manager = _go.AddComponent<SceneTransitionManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);

            ServiceLocator.ClearAll();
        }

        #region Registration

        [Test]
        public void Awake_RegistersSelfInServiceLocator()
        {
            Assert.IsTrue(ServiceLocator.IsRegistered<SceneTransitionManager>());

            var retrieved = ServiceLocator.Get<SceneTransitionManager>();
            Assert.AreSame(_manager, retrieved);
        }

        [Test]
        public void OnDestroy_UnregistersSelfFromServiceLocator()
        {
            Object.DestroyImmediate(_go);
            _go = null;

            Assert.IsFalse(ServiceLocator.IsRegistered<SceneTransitionManager>());
        }

        #endregion

        #region Overlay Creation

        [Test]
        public void Awake_CreatesOverlayChild()
        {
            // The overlay should be a child of the manager's transform.
            Assert.IsTrue(_go.transform.childCount > 0);

            var overlayChild = _go.transform.GetChild(0);
            Assert.AreEqual("[SceneTransition_Overlay]", overlayChild.name);
        }

        [Test]
        public void Awake_OverlayHasCanvas()
        {
            var overlayChild = _go.transform.GetChild(0);
            var canvas = overlayChild.GetComponent<Canvas>();

            Assert.IsNotNull(canvas);
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
            Assert.AreEqual(32767, canvas.sortingOrder);
        }

        [Test]
        public void Awake_OverlayHasCanvasScaler()
        {
            var overlayChild = _go.transform.GetChild(0);
            var scaler = overlayChild.GetComponent<UnityEngine.UI.CanvasScaler>();

            Assert.IsNotNull(scaler);
            Assert.AreEqual(UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(new Vector2(1920, 1080), scaler.referenceResolution);
        }

        [Test]
        public void Awake_OverlayHasFadeImage()
        {
            var overlayChild = _go.transform.GetChild(0);
            Assert.IsTrue(overlayChild.childCount > 0);

            var fadeImage = overlayChild.GetChild(0);
            Assert.AreEqual("FadeImage", fadeImage.name);

            var image = fadeImage.GetComponent<UnityEngine.UI.Image>();
            Assert.IsNotNull(image);
            Assert.AreEqual(Color.black, image.color);
        }

        [Test]
        public void Awake_FadeCanvasGroupStartsTransparent()
        {
            var overlayChild = _go.transform.GetChild(0);
            var fadeImage = overlayChild.GetChild(0);
            var canvasGroup = fadeImage.GetComponent<CanvasGroup>();

            Assert.IsNotNull(canvasGroup);
            Assert.AreEqual(0f, canvasGroup.alpha);
            Assert.IsFalse(canvasGroup.blocksRaycasts);
            Assert.IsFalse(canvasGroup.interactable);
        }

        [Test]
        public void Awake_FadeImageStretchesFullScreen()
        {
            var overlayChild = _go.transform.GetChild(0);
            var fadeImage = overlayChild.GetChild(0);
            var rt = fadeImage.GetComponent<RectTransform>();

            Assert.AreEqual(Vector2.zero, rt.anchorMin);
            Assert.AreEqual(Vector2.one, rt.anchorMax);
            Assert.AreEqual(Vector2.zero, rt.offsetMin);
            Assert.AreEqual(Vector2.zero, rt.offsetMax);
        }

        #endregion

        #region IsTransitioning

        [Test]
        public void IsTransitioning_InitiallyFalse()
        {
            Assert.IsFalse(_manager.IsTransitioning);
        }

        #endregion

        #region SetFadeImmediate

        [Test]
        public void SetFadeImmediate_SetsAlphaToOne()
        {
            _manager.SetFadeImmediate(1f);

            var overlayChild = _go.transform.GetChild(0);
            var fadeImage = overlayChild.GetChild(0);
            var canvasGroup = fadeImage.GetComponent<CanvasGroup>();

            Assert.AreEqual(1f, canvasGroup.alpha);
            Assert.IsTrue(canvasGroup.blocksRaycasts);
            Assert.IsFalse(canvasGroup.interactable);
        }

        [Test]
        public void SetFadeImmediate_SetsAlphaToZero()
        {
            // First set to 1 to verify it changes.
            _manager.SetFadeImmediate(1f);
            _manager.SetFadeImmediate(0f);

            var overlayChild = _go.transform.GetChild(0);
            var fadeImage = overlayChild.GetChild(0);
            var canvasGroup = fadeImage.GetComponent<CanvasGroup>();

            Assert.AreEqual(0f, canvasGroup.alpha);
            Assert.IsFalse(canvasGroup.blocksRaycasts);
        }

        [Test]
        public void SetFadeImmediate_HalfAlpha_BlocksRaycasts()
        {
            _manager.SetFadeImmediate(0.5f);

            var overlayChild = _go.transform.GetChild(0);
            var fadeImage = overlayChild.GetChild(0);
            var canvasGroup = fadeImage.GetComponent<CanvasGroup>();

            Assert.AreEqual(0.5f, canvasGroup.alpha);
            Assert.IsTrue(canvasGroup.blocksRaycasts);
        }

        [Test]
        public void SetFadeImmediate_VerySmallAlpha_DoesNotBlockRaycasts()
        {
            _manager.SetFadeImmediate(0.005f);

            var overlayChild = _go.transform.GetChild(0);
            var fadeImage = overlayChild.GetChild(0);
            var canvasGroup = fadeImage.GetComponent<CanvasGroup>();

            Assert.AreEqual(0.005f, canvasGroup.alpha, 0.001f);
            Assert.IsFalse(canvasGroup.blocksRaycasts);
        }

        #endregion
    }
}
