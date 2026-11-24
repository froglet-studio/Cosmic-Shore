#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using CosmicShore.Utility;
using NUnit.Framework;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Verifies the refresh loop's benign-error detection: a UGS SDK
    /// <see cref="ArgumentOutOfRangeException"/> thrown from
    /// <c>LobbyPatcher.ApplyPatchesToLobby</c> is benign and must be swallowed so that
    /// <c>HostConnectionService.RefreshPartyMembersAsync</c> does not null
    /// <c>ActiveSession</c> (which would cascade into host-vessel despawn).
    ///
    /// Since 2026-10-07 the detector is the one classifier every UGS catch reads,
    /// <see cref="UgsRequestPolicy.Classify"/> (<see cref="UgsFailureClass.Benign"/>), so these
    /// tests call it directly instead of reflecting into a private helper on
    /// <see cref="HostConnectionService"/>; the exhaustive classification table lives in
    /// <c>UgsRequestPolicyTests</c>.
    /// Plan Test 1 (host and joiner both reach the party) runs two real development players as separate
    /// processes (<see cref="TwoStandalonePlayers_HostAndJoin_BothReachTheParty"/>, skipped unless CS_MP_PLAYER
    /// names a build). Plan Tests 2 and 3 are still manual (Docs/PartySystem/TESTS.md).
    /// </summary>
    [TestFixture]
    public class PartyAcceptFlowPlayModeTests
    {
        // ─────────────────────────────────────────────────
        // Benign-error detector
        // ─────────────────────────────────────────────────

        [Test]
        public void BenignLobbyPatcherError_DetectsDirectAOORE()
        {
            var ex = MakeAooreWithLobbyPatcherFrame();
            Assert.AreEqual(UgsFailureClass.Benign, UgsRequestPolicy.Classify(ex),
                "Direct AOORE with LobbyPatcher frame must be classified benign.");
            Assert.IsTrue(UgsRequestPolicy.IsLobbyPatcherStaleIndex(ex));
        }

        [Test]
        public void BenignLobbyPatcherError_DetectsWrappedAOORE()
        {
            // UniTask / Task.WhenAll wraps exceptions. Verify the classifier walks
            // the InnerException chain.
            var inner = MakeAooreWithLobbyPatcherFrame();
            var wrapped = new InvalidOperationException("await wrapper", inner);
            Assert.AreEqual(UgsFailureClass.Benign, UgsRequestPolicy.Classify(wrapped),
                "Wrapped AOORE with LobbyPatcher frame must be classified benign.");
            Assert.IsTrue(UgsRequestPolicy.IsLobbyPatcherStaleIndex(wrapped));
        }

        [Test]
        public void BenignLobbyPatcherError_IgnoresUnrelatedAOORE()
        {
            var ex = new ArgumentOutOfRangeException("idx",
                "Index out of range from non-SDK code");
            // No LobbyPatcher frame on the stack - must NOT be classified benign,
            // otherwise the refresh loop would silently drop real session
            // corruption.
            Assert.AreNotEqual(UgsFailureClass.Benign, UgsRequestPolicy.Classify(ex),
                "AOORE from non-SDK code must not be classified benign.");
            Assert.IsFalse(UgsRequestPolicy.IsLobbyPatcherStaleIndex(ex));
        }

        [Test]
        public void BenignLobbyPatcherError_IgnoresOtherExceptionTypes()
        {
            var ex = new TimeoutException("Lobby request timed out");
            Assert.AreNotEqual(UgsFailureClass.Benign, UgsRequestPolicy.Classify(ex),
                "Non-AOORE exceptions must not be classified benign.");
            Assert.IsFalse(UgsRequestPolicy.IsLobbyPatcherStaleIndex(ex));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Plan Test 1 - happy-path join: a host and a joiner both reach the party
        //
        // Two REAL development players, launched as separate processes with the automatic multiplayer arguments
        // (MultiplayerAutoArgs: -csMpAutoHost / -csMpAutoJoin). Each signs in on its own UGS profile, the joiner
        // presses JOIN on the host's online row through PartyInviteController.JoinPartyAsync (the button's own
        // path), and each writes a result file. This replaces the MPPM procedure this placeholder pointed at:
        // two MPPM Editors used ~24 GB and ran the clone at 1-10 fps on the test machine (2026-11-02).
        //
        // It needs a built DEVELOPMENT player: set CS_MP_PLAYER to its executable (the .exe on Windows, the
        // binary inside the .app on macOS). Without it the test is skipped, not failed. CS_MP_PLAYER_ARGS adds
        // arguments to both players. Each run signs two anonymous players in to the UGS project the build
        // points at - use a development environment, never the live one.
        //
        // Plan Test 2 (the host's vessel is not respawned by an invite click) and Plan Test 3 (an injected SDK
        // AOORE does not destabilise the session) are still manual: Docs/PartySystem/TESTS.md.
        // ─────────────────────────────────────────────────────────────────────

        const string PlayerEnv = "CS_MP_PLAYER";
        const string PlayerArgsEnv = "CS_MP_PLAYER_ARGS";
        const int RunTimeoutSeconds = 240;

        [Test, Category("MultiplayerIntegration"), Timeout((RunTimeoutSeconds + 90) * 1000)]
        public void TwoStandalonePlayers_HostAndJoin_BothReachTheParty()
        {
            string exe = Environment.GetEnvironmentVariable(PlayerEnv);
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                Assert.Ignore($"{PlayerEnv} is not set to a built development player; skipped. " +
                              "Build one, set it, and run this test (Tools/Build/mp_auto.py runs the same thing outside the editor).");

            string dir = Path.Combine(Path.GetTempPath(), "cs-mp-auto-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(dir);
            string rendezvous = Path.Combine(dir, "rendezvous.json");
            string hostResult = Path.Combine(dir, "host.json");
            string joinResult = Path.Combine(dir, "join.json");
            string extra = Environment.GetEnvironmentVariable(PlayerArgsEnv) ?? string.Empty;
            string common = $"-screen-fullscreen 0 -screen-width 960 -screen-height 540 -csMpTimeout {RunTimeoutSeconds} -csMpQuit {extra}";

            Process host = null, join = null;
            try
            {
                host = Launch(exe, $"-csMpProfile mpauto-host -csMpAutoHost \"{rendezvous}\" -csMpExpect 2 " +
                                   $"-csMpResult \"{hostResult}\" -logFile \"{Path.Combine(dir, "host.log")}\" {common}");
                join = Launch(exe, $"-csMpProfile mpauto-join1 -csMpAutoJoin \"{rendezvous}\" " +
                                   $"-csMpResult \"{joinResult}\" -logFile \"{Path.Combine(dir, "join1.log")}\" {common}");

                int waitMs = (RunTimeoutSeconds + 60) * 1000;
                bool hostExited = host.WaitForExit(waitMs);
                bool joinExited = join.WaitForExit(waitMs);

                var h = ReadResult(hostResult);
                var j = ReadResult(joinResult);
                string where = $" Logs and results: {dir}";
                Assert.IsNotNull(h, $"the host wrote no result (exited={hostExited}).{where}");
                Assert.IsNotNull(j, $"the joiner wrote no result (exited={joinExited}).{where}");
                Assert.IsTrue(h.ok, $"host: {h.reason}. Steps: {string.Join(" | ", h.steps)}.{where}");
                Assert.IsTrue(j.ok, $"joiner: {j.reason}. Steps: {string.Join(" | ", j.steps)}.{where}");
            }
            finally
            {
                KillIfRunning(host);
                KillIfRunning(join);
            }
        }

        static Process Launch(string exe, string arguments) =>
            Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = false, CreateNoWindow = false });

        static MultiplayerAutoPilot.Result ReadResult(string path) =>
            File.Exists(path) ? UnityEngine.JsonUtility.FromJson<MultiplayerAutoPilot.Result>(File.ReadAllText(path)) : null;

        static void KillIfRunning(Process p)
        {
            try { if (p != null && !p.HasExited) p.Kill(); }
            catch (InvalidOperationException) { }
        }

        /// <summary>
        /// Constructs an <see cref="ArgumentOutOfRangeException"/> whose stack
        /// trace contains the substring <c>"LobbyPatcher"</c> - matching the
        /// SDK's exception surface without taking a dependency on the SDK
        /// assembly. The detector only inspects <c>StackTrace</c> as a string,
        /// so a thrown-then-caught exception from a method named to contain
        /// "LobbyPatcher" is indistinguishable from the real SDK frame.
        /// </summary>
        private static ArgumentOutOfRangeException MakeAooreWithLobbyPatcherFrame()
        {
            try
            {
                ThrowFromLobbyPatcher();
                return null;
            }
            catch (ArgumentOutOfRangeException e)
            {
                return e;
            }
        }

        // Method name must contain "LobbyPatcher" so the stack-trace string match works.
        private static void ThrowFromLobbyPatcher()
        {
            throw new ArgumentOutOfRangeException(
                "idx", "Index must be within the bounds of the List.");
        }
    }
}
#endif
