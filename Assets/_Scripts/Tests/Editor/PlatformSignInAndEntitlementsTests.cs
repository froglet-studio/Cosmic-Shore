#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Threat Board T1 (Game Center and Play Games sign-in) and T3 (server-owned episode tokens) ship switched off: a build made today
    /// signs in and keeps its token wallet exactly as before.
    /// </summary>
    public class PlatformSignInAndEntitlementsTests
    {
        [Test]
        public void MobileSignInConfig_Ships_WithGameCenterOff()
        {
            var config = Resources.Load<MobileSignInConfigSO>(MobileSignInConfigSO.ResourcePath);
            Assert.IsNotNull(config, "Resources/MobileSignInConfig.asset is missing");
            Assert.IsFalse(config.SignInWithGameCenter);
            Assert.GreaterOrEqual(config.GameCenterTimeoutSeconds, 1f);
        }

        [Test]
        public void GameCenter_IsNeverTried_InTheEditor()
        {
            Assert.IsFalse(GameCenterPlatform.IsSupported);
            Assert.IsFalse(GameCenterSignIn.IsEnabled);
        }

        [Test]
        public void PlayGames_ShipsOff_AndIsNeverTried_InTheEditor()
        {
            var config = Resources.Load<MobileSignInConfigSO>(MobileSignInConfigSO.ResourcePath);
            Assert.IsFalse(config.SignInWithPlayGames);
            Assert.IsFalse(PlayGamesPlatform.IsSupported);
            Assert.IsFalse(PlayGamesSignIn.IsEnabled);
            Assert.IsFalse(PlatformSignIn.IsEnabled);
        }

        [Test]
        public void PlayGamesProjectId_IsDigitsOnly()
        {
            Assert.IsTrue(MobileSignInConfigSO.IsProjectId("123456789012"));
            Assert.IsTrue(MobileSignInConfigSO.IsProjectId(" 42 "));
            Assert.IsFalse(MobileSignInConfigSO.IsProjectId(""));
            Assert.IsFalse(MobileSignInConfigSO.IsProjectId(null));
            Assert.IsFalse(MobileSignInConfigSO.IsProjectId("12ab"));
        }

        [Test]
        public void ServerEntitlements_ShipOff_SoTheLocalWalletStillAnswers()
        {
            Assert.IsFalse(ServerEntitlementGate.Enabled);
            Assert.AreEqual("entitlements", ServerEntitlements.Endpoint);
        }
    }
}
#endif
