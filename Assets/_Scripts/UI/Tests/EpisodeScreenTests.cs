using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.App.UI.Tests
{
    [TestFixture]
    public class EpisodeScreenTests
    {
        GameObject _go;
        Screens.EpisodeScreen _screen;
        GameObject _episodePanel;

        static FieldInfo Field(string name) =>
            typeof(Screens.EpisodeScreen).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestEpisodeScreen");
            _screen = _go.AddComponent<Screens.EpisodeScreen>();

            // Create and wire the episode panel
            _episodePanel = new GameObject("EpisodePanel");
            _episodePanel.SetActive(false);
            Field("episodePanel").SetValue(_screen, _episodePanel);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
            if (_episodePanel != null)
                Object.DestroyImmediate(_episodePanel);
        }

        #region Panel Toggle

        [Test]
        public void TogglePanel_WhenHidden_ShowsPanel()
        {
            _episodePanel.SetActive(false);
            _screen.TogglePanel();

            Assert.IsTrue(_episodePanel.activeSelf);
        }

        [Test]
        public void TogglePanel_WhenVisible_HidesPanel()
        {
            _episodePanel.SetActive(true);
            _screen.TogglePanel();

            Assert.IsFalse(_episodePanel.activeSelf);
        }

        [Test]
        public void TogglePanel_DoubleTap_ReturnsToOriginalState()
        {
            _episodePanel.SetActive(false);
            _screen.TogglePanel();
            _screen.TogglePanel();

            Assert.IsFalse(_episodePanel.activeSelf);
        }

        [Test]
        public void ShowPanel_ActivatesPanel()
        {
            _episodePanel.SetActive(false);
            _screen.ShowPanel();

            Assert.IsTrue(_episodePanel.activeSelf);
        }

        [Test]
        public void ShowPanel_AlreadyVisible_StaysVisible()
        {
            _episodePanel.SetActive(true);
            _screen.ShowPanel();

            Assert.IsTrue(_episodePanel.activeSelf);
        }

        [Test]
        public void HidePanel_DeactivatesPanel()
        {
            _episodePanel.SetActive(true);
            _screen.HidePanel();

            Assert.IsFalse(_episodePanel.activeSelf);
        }

        [Test]
        public void HidePanel_AlreadyHidden_StaysHidden()
        {
            _episodePanel.SetActive(false);
            _screen.HidePanel();

            Assert.IsFalse(_episodePanel.activeSelf);
        }

        #endregion

        #region Null Panel Safety

        [Test]
        public void TogglePanel_NullPanel_DoesNotThrow()
        {
            Field("episodePanel").SetValue(_screen, null);
            Assert.DoesNotThrow(() => _screen.TogglePanel());
        }

        [Test]
        public void ShowPanel_NullPanel_DoesNotThrow()
        {
            Field("episodePanel").SetValue(_screen, null);
            Assert.DoesNotThrow(() => _screen.ShowPanel());
        }

        [Test]
        public void HidePanel_NullPanel_DoesNotThrow()
        {
            Field("episodePanel").SetValue(_screen, null);
            Assert.DoesNotThrow(() => _screen.HidePanel());
        }

        #endregion

        #region Load State

        [Test]
        public void LoadView_SetsLoadedFlag()
        {
            var loadedField = typeof(Screens.EpisodeScreen).GetField(
                "_loaded", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsFalse((bool)loadedField.GetValue(_screen));

            _screen.LoadView();

            Assert.IsTrue((bool)loadedField.GetValue(_screen));
        }

        [Test]
        public void TogglePanel_FirstOpen_TriggersLoad()
        {
            var loadedField = typeof(Screens.EpisodeScreen).GetField(
                "_loaded", BindingFlags.NonPublic | BindingFlags.Instance);

            _screen.TogglePanel(); // opens panel

            Assert.IsTrue((bool)loadedField.GetValue(_screen));
        }

        [Test]
        public void ShowPanel_FirstCall_TriggersLoad()
        {
            var loadedField = typeof(Screens.EpisodeScreen).GetField(
                "_loaded", BindingFlags.NonPublic | BindingFlags.Instance);

            _screen.ShowPanel();

            Assert.IsTrue((bool)loadedField.GetValue(_screen));
        }

        [Test]
        public void LoadView_NullEpisodeList_DoesNotThrow()
        {
            // episodeList is null by default — PopulateEpisodeCards has a null guard
            Assert.DoesNotThrow(() => _screen.LoadView());
        }

        #endregion

        #region Card Spawning Safety

        [Test]
        public void LoadView_NullCardContainer_DoesNotThrow()
        {
            // Both cardContainer and episodeCardPrefab are null by default,
            // PopulateEpisodeCards has null guards for these
            Assert.DoesNotThrow(() => _screen.LoadView());
        }

        #endregion
    }
}
