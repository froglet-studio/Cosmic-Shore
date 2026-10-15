#if UNITY_EDITOR
using System;
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
    /// Full play-mode integration tests for the host/invitee accept flow
    /// (plan Tests 1, 2, 3) require two NetworkManager instances and are
    /// driven through Multiplayer Play Mode (MPPM) virtual players, which
    /// cannot be launched from a single PlayMode test method. The MPPM
    /// integration is documented as a manual smoke procedure in CLAUDE.md;
    /// when a single-process two-NM harness exists the tests below can be
    /// expanded - see TODO comments.
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
        // Plan Test 1 - happy-path accept (host + invitee both reach Ready)
        // Plan Test 2 - invite click does NOT respawn host vessel
        // Plan Test 3 - injected SDK AOORE does not destabilise session
        //
        // TODO: implement once a two-NetworkManager harness or MPPM-driven
        //   test runner is available.  Until then the manual MPPM smoke
        //   procedure in CLAUDE.md is the verification surface:
        //
        //   1. Run two MPPM virtual players. Both reach menu Ready.
        //   2. VP-A opens OnlinePlayersPanel, clicks "+" next to VP-B.
        //   3. Verify on VP-A: vessel NetworkObjectId unchanged before / after click.
        //                      No [Invalid Destroy] logs.
        //   4. VP-B accepts invite.
        //   5. Verify on VP-A: new vessel (VP-B's) appears; host vessel still
        //                      same NetworkObjectId.
        //   6. Verify on VP-B: two vessels visible. VP-A's vessel rendered + flying.
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
