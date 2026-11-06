#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Tests
{
    /// <summary>PhoneScrollStepper (Docs/MENU_REDESIGN_B.md section 135): the arrows show only on a phone, a step moves
    /// the row by one card clamped to its ends, and the dash for the part in view is lit.</summary>
    public class PhoneScrollStepperTests
    {
        private GameObject _root, _controls;
        private PhoneScrollStepper _stepper;
        private CanvasGroup _group;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Root", typeof(RectTransform));
            _controls = new GameObject("Controls", typeof(RectTransform), typeof(CanvasGroup));
            _controls.transform.SetParent(_root.transform, false);
            _group = _controls.GetComponent<CanvasGroup>();
            _root.SetActive(false);
            _stepper = _root.AddComponent<PhoneScrollStepper>();
            var so = new UnityEditor.SerializedObject(_stepper);
            so.FindProperty("controls").objectReferenceValue = _group;
            so.ApplyModifiedPropertiesWithoutUndo();
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void TheControlsShowOnlyOnAPhone()
        {
            _stepper.OnLayout(false);
            Assert.AreEqual(0f, _group.alpha);
            Assert.IsFalse(_group.interactable);
            Assert.IsFalse(_group.blocksRaycasts);
            _stepper.OnLayout(true);
            Assert.AreEqual(1f, _group.alpha);
            Assert.IsTrue(_group.interactable);
            Assert.IsTrue(_group.blocksRaycasts);
        }

        [Test]
        public void AStepMovesOneCardAndStopsAtTheEnds()
        {
            // Five 200-wide cards 16 apart (1064) in a 672-wide view: 392 to scroll, one step is 216.
            Assert.AreEqual(216f / 392f, PhoneScrollStepper.Advance(0f, 392f, 216f, 1), 1e-5f);
            Assert.AreEqual(1f, PhoneScrollStepper.Advance(216f / 392f, 392f, 216f, 1), 1e-5f);
            Assert.AreEqual(0f, PhoneScrollStepper.Advance(0.2f, 392f, 216f, -1), 1e-5f);
            Assert.AreEqual(0f, PhoneScrollStepper.Advance(0.5f, 0f, 216f, 1), "a row that fits does not scroll");
        }

        [Test]
        public void TheDashForThePartInViewIsLit()
        {
            Assert.AreEqual(0, PhoneScrollStepper.DashFor(0f, 2));
            Assert.AreEqual(0, PhoneScrollStepper.DashFor(0.49f, 2));
            Assert.AreEqual(1, PhoneScrollStepper.DashFor(0.5f, 2));
            Assert.AreEqual(1, PhoneScrollStepper.DashFor(1f, 2));
            Assert.AreEqual(-1, PhoneScrollStepper.DashFor(0.5f, 0));
        }
    }
}
#endif
