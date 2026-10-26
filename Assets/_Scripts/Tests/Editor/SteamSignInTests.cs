#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Steam sign-in (fair play, Threat Board T1) ships switched off: the shipped SteamConfig must not start the
    /// Steam API, so every build signs in anonymously exactly as before until the app id and the UGS Steam provider
    /// are set up (Tools/UGS/README.md).
    /// </summary>
    public class SteamSignInTests
    {
        [Test]
        public void ShippedConfig_IsOff()
        {
            var config = Resources.Load<SteamConfigSO>(SteamConfigSO.ResourcePath);
            Assert.IsNotNull(config, "Resources/SteamConfig is missing");
            Assert.AreEqual(0u, config.AppId, "the shipped app id must stay 0 until the Steam setup is done");
            Assert.IsFalse(config.SignInWithSteam);
        }

        [Test]
        public void IdentityMatchesWhatUgsAccepts()
        {
            var config = ScriptableObject.CreateInstance<SteamConfigSO>();
            StringAssert.IsMatch("^[a-zA-Z0-9]{5,30}$", config.WebApiIdentity);
            Object.DestroyImmediate(config);
        }

        [Test]
        public void NoAppId_NeverStartsSteam()
        {
            var config = ScriptableObject.CreateInstance<SteamConfigSO>();
            Assert.IsFalse(SteamPlatform.TryStart(config));
            Assert.IsFalse(SteamPlatform.TryStart(null));
            Assert.IsFalse(SteamPlatform.IsRunning);
            Object.DestroyImmediate(config);
        }
    }
}
#endif
