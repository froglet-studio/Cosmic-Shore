#if UNITY_EDITOR
using NUnit.Framework;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Animated-part resolution tests — validates VesselAnimation.ResolvePart, the by-name binding
    /// that lets a vessel's art be swapped for its rigged, element-labeled model without re-wiring
    /// a dozen inspector fields.
    ///
    /// WHY THIS MATTERS:
    /// Dolphin, Urchin and Rhino ship rigged models whose BONES are their animated parts
    /// ('wing.l', 'jetT.r', 'jaw.u'), authored to match the scripts that drive them. Swapping the
    /// model nulls every authored Transform reference; resolution is what re-binds them. Two
    /// promises live here: (1) an authored reference ALWAYS wins, so every already-wired vessel
    /// keeps its exact behaviour, and (2) candidate order is honoured, so the current rig's bone
    /// name is preferred over the legacy part name when a scene contains both.
    /// </summary>
    [TestFixture]
    public class VesselRigPartResolutionTests
    {
        sealed class TestAnimation : VesselAnimation
        {
            protected override void AssignTransforms() { }
            protected override void PerformShipPuppetry(float p, float y, float r, float t) { }
            public Transform Resolve(Transform authored, params string[] names) => ResolvePart(authored, names);
            public void Capture(params Transform[] parts) => CaptureRestRotations(parts);
            public Quaternion Rest(Transform part) => RestRotationOf(part);
        }

        GameObject _root;
        TestAnimation _animation;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Vessel");
            _animation = _root.AddComponent<TestAnimation>();

            // A vessel carrying BOTH a rig bone and the legacy part it replaces, so candidate
            // order is actually exercised rather than assumed.
            var handle = Child("OrientationHandle", _root.transform);
            Child("wing.l", handle);
            Child("LeftWing", handle);
            Child("jaw.u", handle);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        static Transform Child(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            return go.transform;
        }

        [Test]
        public void AuthoredReference_AlwaysWins()
        {
            var authored = Child("SomethingElse", _root.transform);
            Assert.AreSame(authored, _animation.Resolve(authored, "wing.l"),
                "An authored inspector reference must win, so already-wired vessels are untouched.");
        }

        [Test]
        public void ResolvesRigBoneByName()
        {
            var resolved = _animation.Resolve(null, "wing.l", "LeftWing");
            Assert.IsNotNull(resolved);
            Assert.AreEqual("wing.l", resolved.name,
                "The rig's bone name is listed first and must be preferred over the legacy part name.");
        }

        [Test]
        public void FallsBackToLegacyPartName()
        {
            var resolved = _animation.Resolve(null, "no_such_bone", "LeftWing");
            Assert.IsNotNull(resolved);
            Assert.AreEqual("LeftWing", resolved.name,
                "A vessel still on its legacy art must keep resolving through the fallback name.");
        }

        [Test]
        public void MatchingIsCaseInsensitive()
        {
            var resolved = _animation.Resolve(null, "JAW.U");
            Assert.IsNotNull(resolved);
            Assert.AreEqual("jaw.u", resolved.name,
                "Bone-name casing varies between exports; matching must not depend on it.");
        }

        [Test]
        public void UnknownPartResolvesToNull()
        {
            Assert.IsNull(_animation.Resolve(null, "not_a_bone", "not_a_part"),
                "An unbindable part must resolve to null (and be reported), never to a wrong transform.");
        }

        // A rig's bones rest at large angles — those angles ARE the model's shape (they fan the
        // engines out, sweep the wings back). Puppetry that drives toward a bare Euler assumes an
        // identity rest and tears that shape flat, which is why parts are driven rest-relative.
        [Test]
        public void CapturedRestPoseIsRemembered()
        {
            var bone = Child("wing1.l", _root.transform);
            var rest = Quaternion.Euler(18.8f, 10.3f, -42.1f); // the rhino rig's actual wing rest
            bone.localRotation = rest;

            _animation.Capture(bone);

            Assert.That(Quaternion.Angle(rest, _animation.Rest(bone)), Is.LessThan(0.01f),
                "A rigged bone must be driven around the pose it was authored in, not identity.");
        }

        [Test]
        public void UncapturedPartRestsAtIdentity()
        {
            var part = Child("LeftWing", _root.transform);
            part.localRotation = Quaternion.Euler(30f, 0f, 0f);

            Assert.That(Quaternion.Angle(Quaternion.identity, _animation.Rest(part)), Is.LessThan(0.01f),
                "Parts that were never captured must fall back to identity, so legacy art keeps " +
                "its exact behaviour.");
        }
    }
}
#endif
