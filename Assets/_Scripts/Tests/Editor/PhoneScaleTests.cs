#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The phone scale (Docs/UI_STYLE_GUIDE.md §8, D6): a screen whose short side is under 600 points is a phone and a
    /// phone-layout canvas sets its Kit Texts in the §4 Mobile @800 column. Tools/Build/author_ui_kit_prefabs.py --check
    /// guards that UITypeRoles.asset carries the same column.
    /// </summary>
    public class PhoneScaleTests
    {
        [Test]
        public void APhoneSidewaysIsAPhone() =>
            // iPhone 14 sideways: 2532 x 1170 px at 460 dpi is about 407 points tall.
            Assert.IsTrue(PhoneScale.IsPhone(2532, 1170, 460f, true));

        [Test]
        public void APhoneUprightIsStillAPhone() =>
            Assert.IsTrue(PhoneScale.IsPhone(1170, 2532, 460f, true));

        [Test]
        public void AnIPadIsNotAPhone() =>
            // iPad Air sideways: 2360 x 1640 px at 264 dpi is about 994 points tall.
            Assert.IsFalse(PhoneScale.IsPhone(2360, 1640, 264f, true));

        [Test]
        public void ADesktopMonitorIsNotAPhone()
        {
            Assert.IsFalse(PhoneScale.IsPhone(1920, 1080, 96f, false));
            Assert.IsFalse(PhoneScale.IsPhone(1920, 1080, 0f, false), "a desktop with no dpi is never a phone");
        }

        [Test]
        public void AMobileWithNoDpiIsJudgedAtTwoX()
        {
            Assert.IsTrue(PhoneScale.IsPhone(1600, 720, 0f, true), "720 px at 2x is 360 points");
            Assert.IsFalse(PhoneScale.IsPhone(2560, 1600, 0f, true), "1600 px at 2x is 800 points");
        }

        [Test]
        public void TheReferencesAreTheGuides() =>
            Assert.AreEqual(new Vector2(960f, 540f), PhoneScale.PhoneReference);

        [Test]
        public void ThePhoneColumnIsStyleFoundationsMobileColumn()
        {
            Assert.AreEqual(24f, Phone(UITypeRole.H1));
            Assert.AreEqual(20f, Phone(UITypeRole.H2));
            Assert.AreEqual(16f, Phone(UITypeRole.H3));
            Assert.AreEqual(16f, Phone(UITypeRole.Body));
            Assert.AreEqual(16f, Phone(UITypeRole.Button));
            Assert.AreEqual(12f, Phone(UITypeRole.ButtonSmall));
        }

        [Test]
        public void EveryRoleHasAPhoneSize()
        {
            foreach (var spec in UITypeRolesSO.Defaults())
                Assert.Greater(spec.phoneSize, 0f, $"{spec.role} has no phone size");
        }

        [Test]
        public void PcKeepsThePcSize()
        {
            var body = UITypeRolesSO.DefaultSpec(UITypeRole.Body);
            Assert.AreEqual(18f, UITypeRolesSO.SizeFor(body, false));
        }

        [Test]
        public void AnOldRowWithNoPhoneSizeTakesTheDefault()
        {
            var body = UITypeRolesSO.DefaultSpec(UITypeRole.Body);
            body.phoneSize = 0f;
            Assert.AreEqual(16f, UITypeRolesSO.SizeFor(body, true));
        }

        private static float Phone(UITypeRole role) => UITypeRolesSO.SizeFor(UITypeRolesSO.DefaultSpec(role), true);
    }
}
#endif
