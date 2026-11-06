#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using CosmicShore.UI;

namespace CosmicShore.Tests
{
    /// <summary>
    /// The Codex screen's entry pick from outside the orbit (Docs/MENU_REDESIGN_B.md section 135): the phone's ELEMENTS rows
    /// call <see cref="CodexScreen.SelectEntry"/>, which picks the same entry tapping it on the orbit does, and ignores an
    /// index the open chapter does not have.
    /// </summary>
    public class CodexScreenTests
    {
        GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root) Object.DestroyImmediate(_root);
        }

        CodexScreen Screen()
        {
            _root = new GameObject("codex", typeof(RectTransform));
            var screen = _root.AddComponent<CodexScreen>();
            screen.ShowStop(CodexChapter.Elements);
            return screen;
        }

        [Test]
        public void ShowStop_PicksTheFirstEntry()
        {
            Assert.AreEqual(0, Screen().SelectedIndex, "opening ELEMENTS picks its first entry (Charge)");
        }

        [Test]
        public void SelectEntry_PicksThatEntry()
        {
            var screen = Screen();
            for (int i = 0; i < 4; i++)
            {
                screen.SelectEntry(i);
                Assert.AreEqual(i, screen.SelectedIndex, $"row {i} picks element {i}");
            }
        }

        [Test]
        public void SelectEntry_OutsideTheChapter_KeepsThePick()
        {
            var screen = Screen();
            screen.SelectEntry(2);
            screen.SelectEntry(-1);
            Assert.AreEqual(2, screen.SelectedIndex);
            screen.SelectEntry(999);
            Assert.AreEqual(2, screen.SelectedIndex);
        }
    }
}
#endif
