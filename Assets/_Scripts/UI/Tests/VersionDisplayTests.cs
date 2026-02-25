using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace CosmicShore.App.UI.Tests
{
    [TestFixture]
    public class VersionDisplayTests
    {
        GameObject _go;
        VersionDisplay _display;
        TMP_Text _tmpText;

        static FieldInfo Field(string name) =>
            typeof(VersionDisplay).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestVersionDisplay");
            _display = _go.AddComponent<VersionDisplay>();

            // Create a TextMeshPro text component for the display to write to
            var textGo = new GameObject("VersionText");
            _tmpText = textGo.AddComponent<TextMeshPro>();
            Field("tmpText").SetValue(_display, _tmpText);
        }

        [TearDown]
        public void TearDown()
        {
            if (_tmpText != null)
                Object.DestroyImmediate(_tmpText.gameObject);
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        [Test]
        public void Start_SetsVersionText()
        {
            Field("prefix").SetValue(_display, "v");

            // Invoke Start manually since we're in edit mode
            var startMethod = typeof(VersionDisplay).GetMethod(
                "Start", BindingFlags.NonPublic | BindingFlags.Instance);
            startMethod.Invoke(_display, null);

            // Should contain the prefix and Application.version
            StringAssert.StartsWith("v ", _tmpText.text);
            StringAssert.Contains(Application.version, _tmpText.text);
        }

        [Test]
        public void Start_EmptyPrefix_JustShowsVersion()
        {
            Field("prefix").SetValue(_display, "");

            var startMethod = typeof(VersionDisplay).GetMethod(
                "Start", BindingFlags.NonPublic | BindingFlags.Instance);
            startMethod.Invoke(_display, null);

            Assert.AreEqual(" " + Application.version, _tmpText.text);
        }

        [Test]
        public void Start_CustomPrefix_IsIncluded()
        {
            Field("prefix").SetValue(_display, "Build");

            var startMethod = typeof(VersionDisplay).GetMethod(
                "Start", BindingFlags.NonPublic | BindingFlags.Instance);
            startMethod.Invoke(_display, null);

            Assert.AreEqual("Build " + Application.version, _tmpText.text);
        }
    }
}
