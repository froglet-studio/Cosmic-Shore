#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Codex orbit (Docs/MENU_REDESIGN_B.md section 91): entries fill the rings from the inside out, the last ring
    /// takes whatever is left, and arranging turns the picked entry to the front of its ring - the bottom of the
    /// ellipse, drawn largest and on top.
    /// </summary>
    public class CodexOrbitTests
    {
        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        CodexOrbit Orbit()
        {
            _root = new GameObject("orbit", typeof(RectTransform));
            return _root.AddComponent<CodexOrbit>();
        }

        [Test]
        public void Distribute_FillsFromTheInsideOut()
        {
            var orbit = Orbit();
            CollectionAssert.AreEqual(new[] { 4, 3, 0 }, orbit.Distribute(7, 3));
            CollectionAssert.AreEqual(new[] { 4, 8, 0 }, orbit.Distribute(12, 3));
            CollectionAssert.AreEqual(new[] { 4, 8, 18 }, orbit.Distribute(30, 3), "the last ring takes the rest");
            CollectionAssert.AreEqual(new[] { 0, 0, 0 }, orbit.Distribute(0, 3));
        }

        [Test]
        public void Arrange_TurnsThePickedEntryToTheFront()
        {
            var orbit = Orbit();
            var nodes = new List<RectTransform>();
            for (int i = 0; i < 7; i++)
            {
                var go = new GameObject($"node{i}", typeof(RectTransform));
                go.transform.SetParent(_root.transform, false);
                nodes.Add((RectTransform)go.transform);
            }
            orbit.Arrange(nodes, nodes.Count, 5);
            var picked = nodes[5];
            for (int i = 0; i < nodes.Count; i++)
                if (i != 5 && nodes[i].anchoredPosition.y < picked.anchoredPosition.y - 0.01f && nodes[i].localScale.x > picked.localScale.x)
                    Assert.Fail($"node {i} sits further front than the picked node");
            Assert.AreEqual(0f, picked.anchoredPosition.x, 0.01f, "the picked node is at the front, centred");
            Assert.Less(picked.anchoredPosition.y, 0f, "the front is the bottom of the ellipse");
            Assert.AreEqual(nodes.Count - 1, picked.GetSiblingIndex(), "the front draws on top");
        }
    }
}
#endif
