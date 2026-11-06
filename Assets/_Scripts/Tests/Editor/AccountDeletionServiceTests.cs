#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace CosmicShore.Tests
{
    /// <summary>
    /// In-app account deletion (Docs/LAUNCH/READINESS.md §10 #15): the order of the steps, and the rule that a failed
    /// server step leaves the account and the device untouched so the player can try again. The UGS calls themselves
    /// sit behind <see cref="IAccountDeletionSteps"/>; only a device or editor run reaches them.
    /// </summary>
    public class AccountDeletionServiceTests
    {
        sealed class FakeSteps : IAccountDeletionSteps
        {
            public readonly List<string> Calls = new();
            public bool Can = true;
            public string FailAt;

            public bool CanDelete => Can;

            public UniTask DeleteCloudSaveAsync(CancellationToken ct) => Step("cloudsave");
            public void RequestAnalyticsDeletion() => Step("analytics");
            public UniTask DeleteAuthenticationAccountAsync(CancellationToken ct) => Step("account");
            public void WipeLocalData() => Step("wipe");
            public void Restart() => Step("restart");

            UniTask Step(string name)
            {
                Calls.Add(name);
                if (name == FailAt) throw new InvalidOperationException($"{name} failed");
                return UniTask.CompletedTask;
            }
        }

        static AccountDeletionStatus Run(AccountDeletionService service) =>
            service.DeleteAsync(CancellationToken.None).GetAwaiter().GetResult();

        [Test]
        public void ADeletion_RunsTheServerStepsBeforeTheDeviceWipe_InOrder()
        {
            var steps = new FakeSteps();
            var status = Run(new AccountDeletionService(steps));

            Assert.AreEqual(AccountDeletionStatus.Deleted, status);
            CollectionAssert.AreEqual(new[] { "cloudsave", "analytics", "account", "wipe", "restart" }, steps.Calls);
        }

        [TestCase("cloudsave")]
        [TestCase("analytics")]
        [TestCase("account")]
        public void AFailedServerStep_KeepsTheAccountAndTheDevice(string failAt)
        {
            var steps = new FakeSteps { FailAt = failAt };
            var service = new AccountDeletionService(steps);
            var status = Run(service);

            Assert.AreEqual(AccountDeletionStatus.Failed, status);
            CollectionAssert.DoesNotContain(steps.Calls, "wipe", "a half-deleted player must not lose the device copy");
            CollectionAssert.DoesNotContain(steps.Calls, "restart");
            Assert.IsTrue(service.CanDelete, "the player can try again");
        }

        [Test]
        public void ACloudSaveFailure_NeverReachesTheAccount()
        {
            var steps = new FakeSteps { FailAt = "cloudsave" };
            Run(new AccountDeletionService(steps));
            CollectionAssert.AreEqual(new[] { "cloudsave" }, steps.Calls);
        }

        [Test]
        public void WhenDeletionIsUnavailable_NothingIsAttempted()
        {
            var steps = new FakeSteps { Can = false };
            var service = new AccountDeletionService(steps);

            Assert.IsFalse(service.CanDelete);
            Assert.AreEqual(AccountDeletionStatus.Unavailable, Run(service));
            Assert.IsEmpty(steps.Calls);
        }

        [Test]
        public void ThePlaceholderSignIn_OffersDeletionToAGuest()
        {
            var signIn = new NoPlatformSignIn(new AccountDeletionService(new FakeSteps()));
            Assert.IsTrue(signIn.CanDeleteAccount, "deletion does not need a linked provider");
            Assert.AreEqual(AccountDeletionStatus.Deleted, signIn.DeleteAccountAsync(CancellationToken.None).GetAwaiter().GetResult());
        }

        [Test]
        public void ThePlaceholderSignIn_WithoutTheService_HidesDeletion()
        {
            var signIn = new NoPlatformSignIn();
            Assert.IsFalse(signIn.CanDeleteAccount);
            Assert.AreEqual(AccountDeletionStatus.Unavailable, signIn.DeleteAccountAsync(CancellationToken.None).GetAwaiter().GetResult());
        }
    }
}
#endif
