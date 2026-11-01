#if UNITY_EDITOR
using System.IO;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// B36 (Docs/PartySystem/BUGS.md): the boot loads Menu_Main BEFORE its Relay host starts.
    ///
    /// WHY THIS MATTERS:
    /// Relay drops a host it has not heard from for about 10 s, and the transport only talks to it from
    /// the main thread. The boot used to start the Relay host in the Authentication scene and then load
    /// the menu through Netcode, and that load froze a cold first Play for 10-62 s (measured on three
    /// machines, 26 Oct - 1 Nov), so the host was dropped on its way to the menu: "Failed to establish
    /// connection with the Relay server (server didn't answer any BIND message)", a transport failure, a
    /// bounce and a re-host. The leave-party and bounce paths already loaded the menu first and started
    /// the host after it; the boot now does the same. These pin the two decisions and the order.
    /// </summary>
    public class BootMenuBeforeRelayTests
    {
        // ── The boot gate: which load runs before a host ─────────────────────────────────

        [Test]
        public void RelayBoot_WithNoHostYet_LoadsTheMenuFirst()
        {
            Assert.IsTrue(AuthenticationSceneController.LoadsMenuBeforeHost(attemptRelay: true, hostListening: false),
                "a Relay host must not be live during the boot's menu load");
        }

        [Test]
        public void RelayBoot_WithAHostAlreadyListening_KeepsTheNetcodeLoad()
        {
            Assert.IsFalse(AuthenticationSceneController.LoadsMenuBeforeHost(attemptRelay: true, hostListening: true),
                "a live host gets its menu through Netcode's scene manager, never a local load under it");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NoRelayAttempts_KeepTheLoopbackHostFirst(bool hostListening)
        {
            Assert.IsFalse(AuthenticationSceneController.LoadsMenuBeforeHost(attemptRelay: false, hostListening: hostListening),
                "offline, no network and the browser Toybox start a loopback host, which cannot time out");
        }

        // ── The party layer: the session waits for the boot's menu ───────────────────────

        [TestCase("Authentication")]
        [TestCase("Bootstrap")]
        public void BeforeTheBootsMenuLoad_ThePartySessionIsHeld(string activeScene)
        {
            Assert.IsTrue(HostConnectionService.HoldsPartySessionForBootMenu(menuLoadedSinceBoot: false, activeScene));
        }

        [TestCase("Authentication")]
        [TestCase("Menu_Main")]
        public void OnceTheMenuHasLoaded_ThePartySessionIsNotHeld(string activeScene)
        {
            Assert.IsFalse(HostConnectionService.HoldsPartySessionForBootMenu(menuLoadedSinceBoot: true, activeScene));
        }

        [TestCase("Menu_Main")]
        [TestCase("MinigameSkimRace")]
        public void APlayStartedOutsideTheBootScenes_NeverWaits(string activeScene)
        {
            Assert.IsFalse(HostConnectionService.HoldsPartySessionForBootMenu(menuLoadedSinceBoot: false, activeScene),
                "nothing would ever load the boot's menu to release it");
        }

        // ── The order, in the source ─────────────────────────────────────────────────────

        static string Source(params string[] parts)
        {
            var path = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Scripts", Path.Combine(parts));
            Assert.IsTrue(File.Exists(path), $"{path} not found");
            return File.ReadAllText(path);
        }

        [Test]
        public void TheBootLoadsTheMenuLocally_BeforeItWaitsForRelay()
        {
            var src = Source("System", "AuthenticationSceneController.cs");
            int localLoad = src.IndexOf("SceneManager.LoadSceneAsync(menuScene, LoadSceneMode.Single)", System.StringComparison.Ordinal);
            int relayWait = src.IndexOf("await WaitForRelayReadyAsync(linkedCts.Token)", System.StringComparison.Ordinal);
            Assert.Greater(localLoad, 0, "the menu-first load is gone");
            Assert.Greater(relayWait, 0, "the Relay wait is gone");
            Assert.Less(localLoad, relayWait, "the menu must load before the boot waits on the Relay host");
        }

        [Test]
        public void ThePresenceJoin_ChecksTheHold_BeforeStartingThePartySession()
        {
            var src = Source("Controller", "Party", "HostConnectionService.cs");
            int hold = src.IndexOf("if (PartySessionWaitsForBootMenu())", System.StringComparison.Ordinal);
            Assert.Greater(hold, 0, "the presence join no longer holds the party session for the boot's menu");
            int start = src.IndexOf("await EnsurePartySessionAsync();", hold, System.StringComparison.Ordinal);
            Assert.Greater(start, hold, "the hold must come before the presence join's party session start");
        }
    }
}
#endif
