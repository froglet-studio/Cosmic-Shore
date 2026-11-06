#if UNITY_EDITOR
using CosmicShore.Core;
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.Tests
{
    /// <summary>AccountPromptWindow (Docs/MENU_REDESIGN_B.md section 135): the window takes its PC heights (680 / 330) on
    /// PC and its phone heights (the bottom sheet's 300 / 220) while its canvas is in the phone layout, and an open window
    /// is re-sized when the layout switches.</summary>
    public class AccountPromptWindowTests
    {
        private GameObject _root, _window;
        private AccountPromptWindow _prompt;
        private RectTransform _rect;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Prompt", typeof(RectTransform), typeof(CanvasGroup));
            _window = new GameObject("Window", typeof(RectTransform));
            _window.transform.SetParent(_root.transform, false);
            _rect = (RectTransform)_window.transform;
            _rect.sizeDelta = new Vector2(1180f, 680f);
            _root.SetActive(false);
            _prompt = _root.AddComponent<AccountPromptWindow>();
            var so = new UnityEditor.SerializedObject(_prompt);
            so.FindProperty("group").objectReferenceValue = _root.GetComponent<CanvasGroup>();
            so.FindProperty("window").objectReferenceValue = _rect;
            so.ApplyModifiedPropertiesWithoutUndo();
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private void Conflict() => _prompt.ShowConflict("Link Google", "Title", "Body", "Foot",
            "This device", new PilotSummary("A", 1, 1, 1), "Google", new PilotSummary("B", 2, 2, 2),
            "FLY A", () => { }, "KEEP B", () => { }, "CANCEL");

        [Test]
        public void OnPcTheWindowTakesItsPcHeights()
        {
            _prompt.OnPhoneLayout(false);
            Conflict();
            Assert.AreEqual(680f, _rect.sizeDelta.y);
            _prompt.ShowConfirm("Sign out?", "Body", "SIGN OUT", () => { }, "CANCEL");
            Assert.AreEqual(330f, _rect.sizeDelta.y);
            Assert.AreEqual(1180f, _rect.sizeDelta.x, "the height never touches the width");
        }

        [Test]
        public void OnAPhoneTheWindowTakesItsPhoneHeights()
        {
            _prompt.OnPhoneLayout(true);
            Assert.IsTrue(_prompt.UsesPhoneHeights);
            Conflict();
            Assert.AreEqual(300f, _rect.sizeDelta.y);
            _prompt.ShowConfirm("Sign out?", "Body", "SIGN OUT", () => { }, "CANCEL");
            Assert.AreEqual(220f, _rect.sizeDelta.y);
        }

        [Test]
        public void AnOpenWindowIsResizedWhenTheLayoutSwitches()
        {
            _prompt.OnPhoneLayout(false);
            Conflict();
            _prompt.OnPhoneLayout(true);
            Assert.AreEqual(300f, _rect.sizeDelta.y);
            _prompt.OnPhoneLayout(false);
            Assert.AreEqual(680f, _rect.sizeDelta.y);
            Assert.AreEqual(_prompt.HeightFor(true), _rect.sizeDelta.y);
        }
    }
}
#endif
