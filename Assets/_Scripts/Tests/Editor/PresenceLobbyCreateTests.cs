#if UNITY_EDITOR
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using NUnit.Framework;
using Unity.Services.Core;

namespace CosmicShore.Tests
{
    /// <summary>
    /// B37 (Docs/PartySystem/BUGS.md): "[PresenceLobbyService] Could not create presence lobby: Object reference
    /// not set to an instance of an object" (Bug Ledger E-6b79cd07f1, Gradi and Shombith, September), then
    /// "[INVITE-SEND] ABORT - presence lobby is null" (E-ca3bc5cbe1). The join no longer calls the SDK before
    /// Unity Services are up and a player is signed in, and a create failure the rejoin recovers from is a
    /// warning, while any other is logged whole.
    /// </summary>
    [TestFixture]
    public class PresenceLobbyCreateTests
    {
        [Test]
        public void Join_WaitsForServicesAndASignIn()
        {
            Assert.IsNull(PresenceLobbyService.UgsNotReadyReason(ServicesInitializationState.Initialized, signedIn: true),
                "ready: join the presence lobby");
            StringAssert.Contains("Uninitialized",
                PresenceLobbyService.UgsNotReadyReason(ServicesInitializationState.Uninitialized, signedIn: false));
            StringAssert.Contains("Initializing",
                PresenceLobbyService.UgsNotReadyReason(ServicesInitializationState.Initializing, signedIn: true),
                "the SDK has no session service until initialization finishes");
            StringAssert.Contains("signed in",
                PresenceLobbyService.UgsNotReadyReason(ServicesInitializationState.Initialized, signedIn: false));
        }

        [Test]
        public void ARetryableCreateFailure_IsAWarning_AnythingElseIsAnError()
        {
            Assert.IsTrue(PresenceLobbyService.CreateFailureIsRetryable(UgsFailureClass.Transient), "gradi's 13 Oct 'Request timeout'");
            Assert.IsTrue(PresenceLobbyService.CreateFailureIsRetryable(UgsFailureClass.RateLimited));
            Assert.IsTrue(PresenceLobbyService.CreateFailureIsRetryable(UgsFailureClass.Benign), "the SDK's 'An unknown error occurred'");
            Assert.IsFalse(PresenceLobbyService.CreateFailureIsRetryable(UgsFailureClass.Fatal),
                "a bare NullReferenceException is our bug: logged whole, with its stack (negative control)");
            Assert.AreEqual(UgsFailureClass.Fatal, UgsRequestPolicy.Classify(new System.NullReferenceException()),
                "the September message, thrown bare, still reads as a fault");
        }
    }
}
#endif
