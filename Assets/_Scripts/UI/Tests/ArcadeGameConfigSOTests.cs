using NUnit.Framework;
using UnityEngine;

namespace CosmicShore.App.UI.Tests
{
    [TestFixture]
    public class ArcadeGameConfigSOTests
    {
        ArcadeGameConfigSO _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<ArcadeGameConfigSO>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_config != null)
                Object.DestroyImmediate(_config);
        }

        #region Defaults

        [Test]
        public void CreateInstance_ReturnsNonNull()
        {
            Assert.IsNotNull(_config);
        }

        [Test]
        public void CreateInstance_IsArcadeGameConfigSO()
        {
            Assert.IsInstanceOf<ArcadeGameConfigSO>(_config);
        }

        [Test]
        public void Default_SelectedGame_IsNull()
        {
            Assert.IsNull(_config.SelectedGame);
        }

        [Test]
        public void Default_Intensity_IsZero()
        {
            Assert.AreEqual(0, _config.Intensity);
        }

        [Test]
        public void Default_PlayerCount_IsZero()
        {
            Assert.AreEqual(0, _config.PlayerCount);
        }

        [Test]
        public void Default_TeamCount_IsZero()
        {
            Assert.AreEqual(0, _config.TeamCount);
        }

        [Test]
        public void Default_SelectedShip_IsNull()
        {
            Assert.IsNull(_config.SelectedShip);
        }

        #endregion

        #region State Mutation

        [Test]
        public void Intensity_CanBeSet()
        {
            _config.Intensity = 5;
            Assert.AreEqual(5, _config.Intensity);
        }

        [Test]
        public void PlayerCount_CanBeSet()
        {
            _config.PlayerCount = 4;
            Assert.AreEqual(4, _config.PlayerCount);
        }

        [Test]
        public void TeamCount_CanBeSet()
        {
            _config.TeamCount = 2;
            Assert.AreEqual(2, _config.TeamCount);
        }

        #endregion

        #region ResetState

        [Test]
        public void ResetState_ClearsIntensity()
        {
            _config.Intensity = 10;
            _config.ResetState();

            Assert.AreEqual(0, _config.Intensity);
        }

        [Test]
        public void ResetState_ClearsPlayerCount()
        {
            _config.PlayerCount = 4;
            _config.ResetState();

            Assert.AreEqual(0, _config.PlayerCount);
        }

        [Test]
        public void ResetState_ClearsTeamCount()
        {
            _config.TeamCount = 2;
            _config.ResetState();

            Assert.AreEqual(0, _config.TeamCount);
        }

        [Test]
        public void ResetState_ClearsSelectedGame()
        {
            // SelectedGame is SO_ArcadeGame which we can't easily instantiate,
            // so we just verify it ends up null after reset.
            _config.ResetState();
            Assert.IsNull(_config.SelectedGame);
        }

        [Test]
        public void ResetState_ClearsSelectedShip()
        {
            _config.ResetState();
            Assert.IsNull(_config.SelectedShip);
        }

        [Test]
        public void ResetState_AllFieldsCleared()
        {
            _config.Intensity = 7;
            _config.PlayerCount = 3;
            _config.TeamCount = 2;

            _config.ResetState();

            Assert.AreEqual(0, _config.Intensity);
            Assert.AreEqual(0, _config.PlayerCount);
            Assert.AreEqual(0, _config.TeamCount);
            Assert.IsNull(_config.SelectedGame);
            Assert.IsNull(_config.SelectedShip);
        }

        #endregion

        #region SerializedObject

        [Test]
        public void SetFieldsViaSerializedObject_ReturnsUpdatedValues()
        {
            var so = new UnityEditor.SerializedObject(_config);

            so.FindProperty("Intensity").intValue = 42;
            so.FindProperty("PlayerCount").intValue = 8;
            so.FindProperty("TeamCount").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.AreEqual(42, _config.Intensity);
            Assert.AreEqual(8, _config.PlayerCount);
            Assert.AreEqual(4, _config.TeamCount);
        }

        #endregion
    }
}
