using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Verifies the post-merge fix surface (Fix 1, Fix 2, Fix 3) added to
    /// <see cref="HostConnectionService"/> in response to the "invite click
    /// respawns vessel" symptom.
    ///
    /// These tests are pure-static and reflect into the two private helpers
    /// added by the fix:
    ///   • <c>ContainsLobbyPatcherIndexError(string)</c> — log filter (Fix 1)
    ///   • <c>IsBenignLobbyPatcherError(Exception)</c>  — refresh-loop transient
    ///                                                  detector (Fix 2, Fix 3)
    ///
    /// Full play-mode integration tests for the host/invitee accept flow
    /// (plan Tests 1, 2, 3) require two NetworkManager instances and are
    /// driven through Multiplayer Play Mode (MPPM) virtual players, which
    /// cannot be launched from a single PlayMode test method. The MPPM
    /// integration is documented as a manual smoke procedure in CLAUDE.md;
    /// when a single-process two-NM harness exists the tests below can be
    /// expanded — see TODO comments.
    /// </summary>
    [TestFixture]
    public class PartyAcceptFlowPlayModeTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // Fix 1 — log filter widening
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void LobbyPatcherFilter_MatchesLegacySdkMessage()
        {
            bool match = InvokeContainsLobbyPatcherIndexError(
                "LobbyPatcher: Index was out of range. Must be non-negative.");
            Assert.IsTrue(match, "Legacy SDK message should be filtered.");
        }

        [Test]
        public void LobbyPatcherFilter_MatchesCurrentSdkMessage()
        {
            // Symptom from the bug report:
            //   Unity.Services.Lobbies.Internal.LobbyPatcher.ApplyPatchesToLobby
            //   ArgumentOutOfRangeException: Index must be within the bounds of the List
            bool match = InvokeContainsLobbyPatcherIndexError(
                "LobbyPatcher.ApplyPatchesToLobby: Index must be within the bounds of the List.");
            Assert.IsTrue(match, "Current SDK message should be filtered.");
        }

        [Test]
        public void LobbyPatcherFilter_IgnoresUnrelatedMessage()
        {
            Assert.IsFalse(InvokeContainsLobbyPatcherIndexError(
                "Some other library: Index was out of range."));
            Assert.IsFalse(InvokeContainsLobbyPatcherIndexError(
                "LobbyPatcher: success"));
            Assert.IsFalse(InvokeContainsLobbyPatcherIndexError(string.Empty));
            Assert.IsFalse(InvokeContainsLobbyPatcherIndexError(null));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Fix 2 / Fix 3 — benign-error detector
        // ─────────────────────────────────────────────────────────────────────

        [Test]
        public void BenignLobbyPatcherError_DetectsDirectAOORE()
        {
            var ex = MakeAooreWithLobbyPatcherFrame();
            Assert.IsTrue(InvokeIsBenignLobbyPatcherError(ex),
                "Direct AOORE with LobbyPatcher frame must be classified benign.");
        }

        [Test]
        public void BenignLobbyPatcherError_DetectsWrappedAOORE()
        {
            // UniTask / Task.WhenAll wraps exceptions. Verify the helper walks
            // the InnerException chain.
            var inner = MakeAooreWithLobbyPatcherFrame();
            var wrapped = new InvalidOperationException("await wrapper", inner);
            Assert.IsTrue(InvokeIsBenignLobbyPatcherError(wrapped),
                "Wrapped AOORE with LobbyPatcher frame must be classified benign.");
        }

        [Test]
        public void BenignLobbyPatcherError_IgnoresUnrelatedAOORE()
        {
            var ex = new ArgumentOutOfRangeException("idx",
                "Index out of range from non-SDK code");
            // No LobbyPatcher frame on the stack — must NOT be classified benign,
            // otherwise the refresh loop would silently drop real session
            // corruption.
            Assert.IsFalse(InvokeIsBenignLobbyPatcherError(ex),
                "AOORE from non-SDK code must not be classified benign.");
        }

        [Test]
        public void BenignLobbyPatcherError_IgnoresOtherExceptionTypes()
        {
            var ex = new TimeoutException("Lobby request timed out");
            Assert.IsFalse(InvokeIsBenignLobbyPatcherError(ex),
                "Non-AOORE exceptions must not be classified benign.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Plan Test 1 — happy-path accept (host + invitee both reach Ready)
        // Plan Test 2 — invite click does NOT respawn host vessel
        // Plan Test 3 — injected SDK AOORE does not destabilise session
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
        // ─────────────────────────────────────────────────────────────────────

        // ─────────────────────────────────────────────────────────────────────
        // Reflection plumbing — the helpers are private static so we can keep
        // them tightly scoped to HostConnectionService.
        // ─────────────────────────────────────────────────────────────────────

        private static MethodInfo _containsMethod;
        private static MethodInfo _benignMethod;

        private static bool InvokeContainsLobbyPatcherIndexError(string s)
        {
            if (_containsMethod == null)
            {
                var filterType = typeof(HostConnectionService)
                    .GetNestedType("LobbyPatcherLogFilter", BindingFlags.NonPublic);
                Assert.NotNull(filterType, "LobbyPatcherLogFilter nested type missing.");
                _containsMethod = filterType.GetMethod("ContainsLobbyPatcherIndexError",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(_containsMethod,
                    "ContainsLobbyPatcherIndexError static method missing.");
            }
            return (bool)_containsMethod.Invoke(null, new object[] { s });
        }

        private static bool InvokeIsBenignLobbyPatcherError(Exception e)
        {
            if (_benignMethod == null)
            {
                _benignMethod = typeof(HostConnectionService).GetMethod(
                    "IsBenignLobbyPatcherError",
                    BindingFlags.NonPublic | BindingFlags.Static);
                Assert.NotNull(_benignMethod,
                    "IsBenignLobbyPatcherError static method missing.");
            }
            return (bool)_benignMethod.Invoke(null, new object[] { e });
        }

        /// <summary>
        /// Constructs an <see cref="ArgumentOutOfRangeException"/> whose stack
        /// trace contains the substring <c>"LobbyPatcher"</c> — matching the
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
