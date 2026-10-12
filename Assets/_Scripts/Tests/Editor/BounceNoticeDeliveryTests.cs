#if UNITY_EDITOR
using System.IO;
using System.Reflection;
using CosmicShore.Gameplay;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The bounce notice ("Couldn't join - returned to your menu.", "Connection to the party was
    /// lost - returned to your menu.") is what tells a player why the game just put them back in
    /// their own menu. It shipped broken in a way no test caught: it was raised on a ToastChannel
    /// field nothing assigned, whose only listener lived inside two vessel prefabs, so it reached
    /// nobody. PartyInviteController now shows it through ToastNotificationAPI after recovery.
    ///
    /// WHY THIS MATTERS:
    /// That route has three things the notice silently depends on, and each fails as "the notice
    /// does nothing", not as an error: the controller must not need an inspector assignment to
    /// deliver it, the API's Resources assets must exist for the manager it creates on demand, and
    /// the menu the bounce lands on must carry the container the API spawns toasts under.
    /// </summary>
    [TestFixture]
    public class BounceNoticeDeliveryTests
    {
        const string MenuScenePath = "Assets/_Scenes/Menu_Main.unity";

        [Test]
        public void PartyInviteController_NeedsNoToastWiringToDeliverTheNotice()
        {
            var fields = typeof(PartyInviteController).GetFields(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            foreach (var field in fields)
                Assert.IsFalse(typeof(ToastChannel).IsAssignableFrom(field.FieldType),
                    $"PartyInviteController.{field.Name} is a ToastChannel again. The bounce notice was lost for as " +
                    "long as it went through an inspector-assigned channel nobody wired; it goes through " +
                    "ToastNotificationAPI, which needs no assignment.");
        }

        [Test]
        public void ToastNotificationApi_ResourcesTheAutoCreatedManagerLoadsExist()
        {
            Assert.IsNotNull(Resources.Load<ToastNotificationSettingsSO>(ToastNotificationAPI.SettingsPath),
                $"Resources/{ToastNotificationAPI.SettingsPath} is missing: the auto-created toast manager has no " +
                "settings and drops every toast, the bounce notice included.");
            Assert.IsNotNull(Resources.Load<ToastNotificationChannel>(ToastNotificationAPI.ChannelPath),
                $"Resources/{ToastNotificationAPI.ChannelPath} is missing.");
        }

        [Test]
        public void MenuMain_CarriesTheContainerToastsSpawnUnder()
        {
            var path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, MenuScenePath);
            Assert.IsTrue(File.Exists(path), $"{MenuScenePath} is missing");

            string scene = File.ReadAllText(path);
            Assert.IsTrue(scene.Contains("m_Name: " + ToastNotificationAPI.ContainerName),
                $"{MenuScenePath} has no GameObject named '{ToastNotificationAPI.ContainerName}'. Every bounce lands " +
                "on Menu_Main, and without that container ToastNotificationAPI drops the notice.");
        }
    }
}
#endif
