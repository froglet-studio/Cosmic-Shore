#if UNITY_EDITOR
using CosmicShore.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Tests
{
    /// <summary>PhoneSheet (Docs/MENU_REDESIGN_B.md section 129): on PC the panel shows and the open button hides; on a
    /// phone the panel starts closed, opens from its button and closes again.</summary>
    public class PhoneSheetTests
    {
        private GameObject _root, _panel, _open;
        private PhoneSheet _sheet;
        private CanvasGroup _group;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Root", typeof(RectTransform));
            _panel = new GameObject("Panel", typeof(RectTransform), typeof(CanvasGroup));
            _open = new GameObject("Open", typeof(RectTransform), typeof(Image), typeof(Button));
            _panel.transform.SetParent(_root.transform, false);
            _open.transform.SetParent(_root.transform, false);
            _group = _panel.GetComponent<CanvasGroup>();
            _root.SetActive(false);
            _sheet = _root.AddComponent<PhoneSheet>();
            var so = new UnityEditor.SerializedObject(_sheet);
            so.FindProperty("panel").objectReferenceValue = _group;
            so.FindProperty("openButton").objectReferenceValue = _open.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();
            _root.SetActive(true);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        [Test]
        public void OnPcThePanelShowsAndTheButtonHides()
        {
            _sheet.OnLayout(false);
            Assert.AreEqual(1f, _group.alpha);
            Assert.IsFalse(_open.activeSelf);
            _sheet.Close();
            Assert.AreEqual(1f, _group.alpha, "closing does nothing on PC");
        }

        [Test]
        public void OnAPhoneTheSheetStartsClosedAndOpens()
        {
            _sheet.OnLayout(true);
            Assert.AreEqual(0f, _group.alpha);
            Assert.IsTrue(_open.activeSelf);
            _sheet.Open();
            Assert.AreEqual(1f, _group.alpha);
            Assert.IsTrue(_group.blocksRaycasts);
            _sheet.Close();
            Assert.AreEqual(0f, _group.alpha);
        }
    }
}
#endif
