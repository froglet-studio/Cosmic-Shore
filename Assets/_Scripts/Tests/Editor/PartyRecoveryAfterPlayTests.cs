#if UNITY_EDITOR
using System.Reflection;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// B38 (Docs/PartySystem/BUGS.md): a recovery or leave-lobby flow that was still awaiting the party
    /// leave or the Netcode shutdown when Play stopped went on to load Menu_Main in edit mode and threw
    /// ("This can only be used during play mode", Bug Ledger E-41b7610878; "Value cannot be null. Parameter
    /// name: asyncOperation", E-0de9ec6c70). Each step after an await now asks whether Play is still on.
    /// </summary>
    [TestFixture]
    public class PartyRecoveryAfterPlayTests
    {
        GameObject _go;
        bool _playing;

        [SetUp]
        public void SetUp()
        {
            _playing = true;
            PartyInviteController.IsPlaying = () => _playing;
            _go = new GameObject("PartyInviteController (test)");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
            PartyInviteController.IsPlaying = () => Application.isPlaying;
        }

        static bool PlayEnded(PartyInviteController c)
        {
            var m = typeof(PartyInviteController).GetMethod("PlayEnded", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(m, "PartyInviteController.PlayEnded exists");
            return (bool)m.Invoke(c, new object[] { "test" });
        }

        [Test]
        public void TheFlowGoesOn_WhilePlayRuns()
        {
            var c = _go.AddComponent<PartyInviteController>();
            Assert.IsFalse(PlayEnded(c), "Play is on and the controller is alive: load the menu (negative control)");
        }

        [Test]
        public void TheFlowStops_OncePlayHasStopped()
        {
            var c = _go.AddComponent<PartyInviteController>();
            _playing = false;
            Assert.IsTrue(PlayEnded(c), "belac's 19 Oct recovery reached the scene load in edit mode");
        }

        [Test]
        public void TheFlowStops_OnceTheControllerIsGone()
        {
            var c = _go.AddComponent<PartyInviteController>();
            Object.DestroyImmediate(_go);
            Assert.IsTrue(PlayEnded(c), "the scene that held it was torn down");
        }
    }
}
#endif
