#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.UI;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The browser Toybox's decisions, pinned where they are made (Docs/WEBGL_TOYBOX.md): its boot
    /// never attempts Relay and never counts its local session as an unwanted fallback, and its
    /// menu opens only the toybox's own modals.
    ///
    /// <para>These run in an ordinary Editor, where <see cref="WebToybox.IsBuild"/> is false; they
    /// test the pure decisions the flag feeds, which is all a compile without the define can see.</para>
    /// </summary>
    public class WebToyboxTests
    {
        [TestCase(false, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(false, false, true)]
        public void TheToyboxBoot_NeverAttemptsRelay_AndIsNeverAnUnwantedFallback(
            bool offlinePreferred, bool deviceOffline, bool offlineSessionLive)
        {
            var plan = AuthenticationSceneController.PlanBootNetwork(
                offlinePreferred, deviceOffline, offlineSessionLive, webToybox: true);
            Assert.IsFalse(plan.AttemptRelay, "the browser Toybox has no online services to reach");
            Assert.IsFalse(plan.UnwantedFallback, "a session that is offline by design is neither counted nor explained");
        }

        [Test]
        public void WithoutTheToybox_TheBootPlanIsUnchanged()
        {
            // The new parameter defaults to false, so every existing caller keeps its plan.
            var plan = AuthenticationSceneController.PlanBootNetwork(offlinePreferred: false, deviceOffline: false, offlineSessionLive: false);
            Assert.IsTrue(plan.AttemptRelay);
            Assert.IsTrue(plan.UnwantedFallback);
        }

        [Test]
        public void TheToyboxOpensOnlyItsOwnModals_AndEveryModalIsDecided()
        {
            // Enumerates the enum, so a modal added tomorrow is refused in the browser until
            // someone decides otherwise here and in ScreenSwitcher.IsModalInWebToybox.
            var allowed = new HashSet<ScreenSwitcher.ModalWindows>
            {
                ScreenSwitcher.ModalWindows.NONE,
                ScreenSwitcher.ModalWindows.TOYBOX,
                ScreenSwitcher.ModalWindows.TOYBOX_CONFIGURE,
                ScreenSwitcher.ModalWindows.SETTINGS,
                ScreenSwitcher.ModalWindows.CREDITS,
            };
            foreach (ScreenSwitcher.ModalWindows modal in Enum.GetValues(typeof(ScreenSwitcher.ModalWindows)))
            {
                Assert.AreEqual(allowed.Contains(modal), ScreenSwitcher.IsModalInWebToybox(modal),
                    $"{modal}: the browser Toybox should {(allowed.Contains(modal) ? "" : "not ")}open it");
            }
        }

        [Test]
        public void TheEditorWithoutTheDefine_KeepsTheLoopbackTransport()
        {
            // UsesInMemoryTransport is true only on a WebGL player or in the toybox build; a plain
            // Editor run must keep the UnityTransport loopback host every other platform uses.
            Assert.IsFalse(WebToybox.IsBuild, "these tests run without FROGLET_WEB_TOYBOX");
            Assert.IsFalse(OfflineModeService.UsesInMemoryTransport);
        }
    }
}
#endif
