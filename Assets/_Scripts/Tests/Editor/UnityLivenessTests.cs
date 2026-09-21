#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The interface-null trap, held as a source law.
    ///
    /// <para>Unity reports a destroyed object as null through an OVERLOADED <c>==</c> on
    /// <c>UnityEngine.Object</c>. A reference whose static type is an INTERFACE never reaches
    /// that operator, so `!= null` and C#'s `?.` both see a destroyed component as alive and
    /// the next member access throws <c>MissingReferenceException</c>. Because the holders are
    /// cached handles and the readers are <c>Update</c>/<c>LateUpdate</c> loops, that is not one
    /// exception — it is an unbounded per-frame storm that eats the frame rate.</para>
    ///
    /// <para>Every self-healing handle in the project (<c>GameDataSO.LocalPlayer</c>,
    /// <c>GameDataSO.LocalRoundStats</c>, <c>Player.Vessel</c>) and every caller-side check
    /// (<c>VesselLiveness</c>) now rests on ONE predicate. These tests pin that predicate,
    /// including the negative control that reproduces the trap: a plain `!= null` must
    /// disagree with it on a destroyed object, or the predicate is not doing anything.</para>
    /// </summary>
    [TestFixture]
    public class UnityLivenessTests
    {
        interface IThing { int Value { get; } }

        class ThingBehaviour : MonoBehaviour, IThing
        {
            public int Value => 7;
        }

        /// <summary>A pure C# implementation — an edit-mode test fake, which is not a
        /// UnityEngine.Object and must pass through untouched.</summary>
        class PlainThing : IThing
        {
            public int Value => 7;
        }

        GameObject _go;
        IThing _thing;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("liveness-subject");
            _thing = _go.AddComponent<ThingBehaviour>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go) Object.DestroyImmediate(_go);
        }

        [Test]
        public void Alive_IsTrue_ForALiveComponentBehindAnInterface()
        {
            Assert.IsTrue(UnityLiveness.Alive(_thing));
        }

        [Test]
        public void Alive_IsFalse_ForNull()
        {
            Assert.IsFalse(UnityLiveness.Alive(null));
        }

        [Test]
        public void Alive_IsFalse_OnceDestroyed()
        {
            Object.DestroyImmediate(_go);
            Assert.IsFalse(UnityLiveness.Alive(_thing));
        }

        /// <summary>
        /// THE NEGATIVE CONTROL. If this ever fails, the interface trap has stopped existing
        /// and the whole self-healing apparatus is dead weight — which would be worth knowing.
        /// While it passes, it is the proof that a plain null check is NOT equivalent.
        /// </summary>
        [Test]
        public void PlainNullCheck_StillReportsADestroyedObjectAsAlive()
        {
            Object.DestroyImmediate(_go);

            Assert.IsTrue(_thing != null,
                "An interface reference to a destroyed UnityEngine.Object is non-null in plain C#. " +
                "This is the trap UnityLiveness exists for.");
            Assert.IsFalse(UnityLiveness.Alive(_thing),
                "UnityLiveness must disagree with the plain check, or it is not doing anything.");
        }

        [Test]
        public void Live_HandsBackARealNull_SoNullPropagationWorks()
        {
            Object.DestroyImmediate(_go);

            var live = UnityLiveness.Live(_thing);

            Assert.IsNull(live);
            // The point of handing back a REAL null: `?.` now short-circuits instead of
            // reaching a member access on a destroyed object.
            Assert.AreEqual(0, live?.Value ?? 0);
        }

        [Test]
        public void Live_HandsBackTheReference_WhileAlive()
        {
            Assert.AreSame(_thing, UnityLiveness.Live(_thing));
        }

        [Test]
        public void Alive_IsTrue_ForANonUnityImplementation()
        {
            // Edit-mode test fakes are plain C# objects, so they are alive iff non-null.
            // PruneDestroyedRosterEntries has always relied on this.
            Assert.IsTrue(UnityLiveness.Alive(new PlainThing()));
        }
    }
}
#endif
