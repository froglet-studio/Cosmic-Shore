using System.Reflection;
using System.Threading.Tasks;
using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using NUnit.Framework;
using Reflex.Core;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// One UGS sign-in at a time needs one facade. AppManager's bindings are installed into two containers (the
    /// root scope and the Bootstrap scene's scope), and each used to build its own facade: AppManager's started
    /// the boot sign-in, the Authentication scene's could not see it and started a second, which the SDK refused
    /// with "The player is already signing in" (Docs/Multiplayer/BUGS.md).
    ///
    /// The SDK's refusal itself cannot be planted here: it needs initialized Unity Services and a live sign-in.
    /// These tests hold the two conditions that produced it.
    /// </summary>
    public class AuthenticationFacadeSingleInstanceTests
    {
        AuthenticationDataVariable _variable;

        [SetUp]
        public void SetUp()
        {
            AuthenticationServiceFacade.ResetShared();
            _variable = ScriptableObject.CreateInstance<AuthenticationDataVariable>();
        }

        [TearDown]
        public void TearDown()
        {
            AuthenticationServiceFacade.ResetShared();
            Object.DestroyImmediate(_variable);
        }

        [Test]
        public void TwoContainersInstalledWithTheSameBindings_ResolveOneFacade()
        {
            // The boot's shape: the same installer runs for the root container and for the Bootstrap scene's.
            var root = new ContainerBuilder();
            AppManager.RegisterAuthenticationFacade(root, _variable, false);
            using var rootContainer = root.Build();

            var scene = new ContainerBuilder();
            AppManager.RegisterAuthenticationFacade(scene, _variable, false);
            using var sceneContainer = scene.Build();

            var fromAppManager = sceneContainer.Resolve<AuthenticationServiceFacade>();
            var fromLaterScenes = rootContainer.Resolve<AuthenticationServiceFacade>();

            Assert.AreSame(fromAppManager, fromLaterScenes,
                "AppManager and the Authentication scene must hold the same facade, or the scene cannot join " +
                "the boot sign-in and starts a competing one");
        }

        [Test]
        public void ANewPlay_BuildsANewFacade()
        {
            var first = AuthenticationServiceFacade.Shared(_variable, false);
            AuthenticationServiceFacade.ResetShared();   // what entering Play runs (no domain reload)
            var second = AuthenticationServiceFacade.Shared(_variable, false);

            Assert.AreNotSame(first, second, "last Play's facade (its startup guard, its success latch) must not be reused");
        }

        [Test]
        public void ResetStartupState_KeepsASignInThatIsStillInFlight()
        {
            var facade = AuthenticationServiceFacade.Shared(_variable, false);
            var field = typeof(AuthenticationServiceFacade).GetField("_signInTask", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, "_signInTask was renamed: update this test");

            var inFlight = new TaskCompletionSource<bool>();
            field.SetValue(facade, inFlight.Task);

            facade.ResetStartupState();
            Assert.IsTrue(facade.SignInInFlight,
                "a reset must not forget a running sign-in: the next caller would start a second one beside it");

            inFlight.SetResult(true);
            facade.ResetStartupState();
            Assert.IsFalse(facade.SignInInFlight);
            Assert.IsNull(field.GetValue(facade), "a finished attempt is forgotten, so the next sign-in starts fresh");
        }
    }
}
