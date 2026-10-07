#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Gameplay;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Gates the ecology invariant of Docs/ECOSYSTEM.md §26.10: every standing spindle of a
    /// lifeform has a path of standing spindles back to its crystal, so an ordered death wither
    /// (<see cref="Spindle.OrderOutsideIn"/>) must never spend a limb before any limb that hangs
    /// off it - and its one sanctioned exception, the crystal joust
    /// (<see cref="Spindle.OrderHeartOutward"/>), must run the same tree exactly backwards, from
    /// the missing crystal to the leaves. The tree under test folds back past the heart, the way
    /// the Borromean membrane does, which is the shape that defeats a pure distance sort in
    /// EITHER direction.
    /// </summary>
    public class SpindleOutsideInOrderTests
    {
        readonly List<GameObject> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _spawned)
                if (go) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        Spindle Limb(string name, Vector3 position, Spindle parent)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.position = position;
            var limb = go.AddComponent<Spindle>();
            if (parent) limb.AttachToParent(parent);
            return limb;
        }

        // heart at the origin. A -> B -> C, where C folds back to sit NEARER the heart than A;
        // D is a short sibling branch off A that reaches farthest of all.
        (Spindle a, Spindle b, Spindle c, Spindle d) FoldedTree()
        {
            var a = Limb("A", new Vector3(10, 0, 0), null);
            var b = Limb("B", new Vector3(20, 0, 0), a);
            var c = Limb("C", new Vector3(0, 5, 0), b);
            var d = Limb("D", new Vector3(-30, 0, 0), a);
            return (a, b, c, d);
        }

        static void AssertEveryChildBeforeItsParent(IList<Spindle> order, params (Spindle child, Spindle parent)[] edges)
        {
            foreach (var (child, parent) in edges)
                Assert.Less(order.IndexOf(child), order.IndexOf(parent),
                    $"{parent.name} withers before {child.name}, which hangs off it - " +
                    $"{child.name} would stand cut off from the heart. Order: " +
                    string.Join(", ", order.Select(s => s.name)));
        }

        [Test]
        public void OrderOutsideIn_NeverSpendsAParentBeforeItsSubtree()
        {
            var (a, b, c, d) = FoldedTree();
            var order = Spindle.OrderOutsideIn(new[] { a, b, c, d }, Vector3.zero);

            Assert.AreEqual(4, order.Count);
            AssertEveryChildBeforeItsParent(order, (b, a), (c, b), (d, a));
            Assert.AreEqual(a, order[order.Count - 1], "The limb at the crystal must wither LAST.");
        }

        [Test]
        public void NegativeControl_OutsideInDistanceSortOrphansTheFold()
        {
            var (a, b, c, d) = FoldedTree();
            var all = new[] { a, b, c, d };

            // The pre-§26.10 starvation order (farthest first) is wrong on a fold: C sits nearer
            // the heart than its own grandparent, so B is spent while C still hangs off it.
            var farthestFirst = all.OrderByDescending(s => s.transform.position.sqrMagnitude).ToList();
            Assert.Less(farthestFirst.IndexOf(b), farthestFirst.IndexOf(c),
                "Control is broken: the distance-only outside-in sort should spend B before C.");
        }

        [Test]
        public void OrderHeartOutward_UnravelsFromTheCrystalToTheLeaves()
        {
            var (a, b, c, d) = FoldedTree();
            var order = Spindle.OrderHeartOutward(new[] { a, b, c, d }, Vector3.zero);

            Assert.AreEqual(4, order.Count);
            Assert.AreEqual(a, order[0], "The joust must spend the limb at the missing crystal FIRST.");
            foreach (var (child, parent) in new[] { (b, a), (c, b), (d, a) })
                Assert.Less(order.IndexOf(parent), order.IndexOf(child),
                    $"The joust skipped ahead: {child.name} withers before {parent.name}, the limb it " +
                    "grows out of. Order: " + string.Join(", ", order.Select(s => s.name)));
        }

        [Test]
        public void NegativeControl_HeartOutwardDistanceSortSkipsAroundTheFold()
        {
            var (a, b, c, d) = FoldedTree();

            // The joust's pre-§26.10 order: nearest the heart first. C folds back nearer the heart
            // than its own grandparent, so it unravels before the limbs it grows out of.
            var byDistance = new[] { a, b, c, d }.OrderBy(s => s.transform.position.sqrMagnitude).ToList();
            Assert.Less(byDistance.IndexOf(c), byDistance.IndexOf(b),
                "Control is broken: the distance-only heart-outward sort should spend C before B.");
        }

        [Test]
        public void OrderHeartOutward_IsTheExactReverseOfOrderOutsideIn()
        {
            var (a, b, c, d) = FoldedTree();
            var all = new[] { a, b, c, d };

            var inward = Spindle.OrderOutsideIn(all, Vector3.zero);
            var outward = Spindle.OrderHeartOutward(all, Vector3.zero);
            inward.Reverse();

            CollectionAssert.AreEqual(inward, outward);
        }

        [Test]
        public void OrderOutsideIn_WithNoTree_FallsBackToFarthestFirst()
        {
            var near = Limb("Near", new Vector3(1, 0, 0), null);
            var far = Limb("Far", new Vector3(9, 0, 0), null);
            var mid = Limb("Mid", new Vector3(0, 0, 4), null);

            var order = Spindle.OrderOutsideIn(new[] { near, far, mid }, Vector3.zero);

            CollectionAssert.AreEqual(new[] { far, mid, near }, order);
        }

        [Test]
        public void AttachToParent_RelinksTheTree()
        {
            var (a, b, c, d) = FoldedTree();
            // Keep B holding something after C leaves it, so the release does not wither it here.
            var e = Limb("E", new Vector3(25, 0, 0), b);

            c.AttachToParent(d);
            var order = Spindle.OrderOutsideIn(new[] { a, b, c, d, e }, Vector3.zero);

            AssertEveryChildBeforeItsParent(order, (c, d), (d, a), (e, b), (b, a));
        }

        [Test]
        public void OrderOutsideIn_SkipsDestroyedLimbs()
        {
            var (a, b, c, d) = FoldedTree();
            var dead = Limb("Dead", Vector3.one, null);
            Object.DestroyImmediate(dead.gameObject);

            var order = Spindle.OrderOutsideIn(new[] { a, b, dead, c, d }, Vector3.zero);

            Assert.AreEqual(4, order.Count);
        }
    }
}
#endif
