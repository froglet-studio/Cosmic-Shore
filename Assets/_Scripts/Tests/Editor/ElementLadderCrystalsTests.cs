#if UNITY_EDITOR
using System.Collections.Generic;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>The I3 rung's default placement: inside the shell, and the same on every peer for the same stream.</summary>
    public class ElementLadderCrystalsTests
    {
        [Test]
        public void Scatter_StaysInTheShell_AndRepeatsForTheSameSeed()
        {
            var a = new List<Vector3>();
            var b = new List<Vector3>();
            var centre = new Vector3(10f, -5f, 3f);
            ElementLadderCrystals.ScatterInShell(centre, 100f, 400f, 50, new System.Random(42), a);
            ElementLadderCrystals.ScatterInShell(centre, 100f, 400f, 50, new System.Random(42), b);

            Assert.AreEqual(50, a.Count);
            for (int i = 0; i < a.Count; i++)
            {
                float d = Vector3.Distance(a[i], centre);
                Assert.That(d, Is.InRange(99.9f, 400.1f));
                Assert.AreEqual(a[i], b[i], "every peer draws the same points from the same stream");
            }
        }

        [Test]
        public void Scatter_DifferentTeamsDrawDifferentPoints()
        {
            var a = new List<Vector3>();
            var b = new List<Vector3>();
            ElementLadderCrystals.ScatterInShell(Vector3.zero, 0f, 300f, 4, new System.Random(1), a);
            ElementLadderCrystals.ScatterInShell(Vector3.zero, 0f, 300f, 4, new System.Random(2), b);
            Assert.AreNotEqual(a[0], b[0]);
        }
    }
}
#endif
