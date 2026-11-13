#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Edit-Mode coverage for <see cref="SceneTransitionManager"/>.
    ///
    /// Unity does not call <c>Awake</c> outside Play Mode, so the overlay it builds has to be
    /// driven explicitly — see <see cref="EditModeLifecycle"/>. This <c>Awake</c> is safe to
    /// invoke: with no <c>_splashOverlay</c> wired it takes the <c>CreateFadeOverlay</c> branch,
    /// which only builds child GameObjects and UI components (no DontDestroyOnLoad, no Destroy,
    /// nothing that touches the open scene).
    /// </summary>
    [TestFixture]
    public class SceneTransitionManagerTests
    {
        GameObject _go;
        SceneTransitionManager _manager;

        [SetUp]
        public void SetUp()
        {
            // SetFadeImmediate refuses to run off the main thread, and MainThreadDispatcher's own
            // Init is [RuntimeInitializeOnLoadMethod], which never runs in Edit Mode - leaving it
            // reporting FALSE for every test here and logging an error instead of fading.
            EditModeLifecycle.EnsureMainThreadDispatcherInitialized();

            _go = new GameObject("TestSceneTransition");
            _manager = _go.AddComponent<SceneTransitionManager>();

            // Builds the overlay every test below inspects.
            EditModeLifecycle.InvokeAwake(_manager);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

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

            // interactable MIRRORS blocksRaycasts at every site that drives this group
            // (SetFadeImmediate, FadeAsync, AdoptSplashOverlay). An opaque overlay must stay
            // interactable because the adopted splash hosts BootStatusPanel's retry button, which
            // has to be tappable while the overlay is visible - see the comment on
            // SceneTransitionManager.AdoptSplashOverlay. This assertion read IsFalse until the
            // retry button landed.
            Assert.IsTrue(canvasGroup.interactable,
                "interactable must mirror blocksRaycasts - an opaque overlay that blocks raycasts " +
                "but is not interactable makes the boot retry button untappable.");
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

            // The other half of the mirror, and the half that actually hurts if it breaks: a
            // cleared overlay left interactable is an invisible sheet swallowing every click.
            Assert.IsFalse(canvasGroup.interactable,
                "A fully faded overlay must not stay interactable - it would silently eat input.");
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

        #region The reveal: a hitch never eats the fade (section 185)

        [Test]
        public void FadeStep_CapsAHitchFrame()
        {
            // A half-second frame (a collect, a first render) must advance a fade by one capped step, not by half a
            // second: that frame is what made the menu bang open after the splash.
            Assert.AreEqual(1f / 30f, SceneTransitionManager.FadeStep(0.5f, 1f / 30f), 1e-6f);
        }

        [Test]
        public void FadeStep_PassesAnOrdinaryFrameAndRefusesANegativeOne()
        {
            Assert.AreEqual(1f / 60f, SceneTransitionManager.FadeStep(1f / 60f, 1f / 30f), 1e-6f);
            Assert.AreEqual(0f, SceneTransitionManager.FadeStep(-0.1f, 1f / 30f), 1e-6f);
        }

        [Test]
        public void EaseInOut_StartsAndEndsAtRestAndNeverLeavesTheRange()
        {
            Assert.AreEqual(0f, SceneTransitionManager.EaseInOut(0f), 1e-6f);
            Assert.AreEqual(1f, SceneTransitionManager.EaseInOut(1f), 1e-6f);
            Assert.AreEqual(0.5f, SceneTransitionManager.EaseInOut(0.5f), 1e-6f);
            float last = 0f;
            for (int i = 1; i <= 100; i++)
            {
                float v = SceneTransitionManager.EaseInOut(i / 100f);
                Assert.GreaterOrEqual(v, last, "the reveal must never step back");
                last = v;
            }
            // At rest at the ends: the first and last hundredths move far less than the middle one.
            Assert.Less(SceneTransitionManager.EaseInOut(0.01f), 0.01f);
        }

        #endregion
    }
}
#endif
