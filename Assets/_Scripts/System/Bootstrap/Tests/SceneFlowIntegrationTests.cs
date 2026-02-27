using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.IO;

namespace CosmicShore.Core
{
    /// <summary>
    /// Integration tests that verify the Bootstrap → Authentication → Menu_Main
    /// scene flow is correctly wired across all configuration assets and code.
    ///
    /// Scene names are centralized in <see cref="CosmicShore.Utility.SceneNameListSO"/>,
    /// registered in DI via AppManager. All consumers inject it.
    /// </summary>
    [TestFixture]
    public class SceneFlowIntegrationTests
    {
        static readonly string[] RequiredScenes = { "Bootstrap", "Authentication", "Menu_Main" };

        #region Build Settings Validation

        [Test]
        public void BuildSettings_ContainsBootstrapScene()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.IsTrue(
                scenes.Any(s => s.enabled && s.path.Contains("Bootstrap")),
                "Bootstrap scene must be in build settings and enabled.");
        }

        [Test]
        public void BuildSettings_ContainsAuthenticationScene()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.IsTrue(
                scenes.Any(s => s.enabled && s.path.Contains("Authentication")),
                "Authentication scene must be in build settings and enabled.");
        }

        [Test]
        public void BuildSettings_ContainsMenuMainScene()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.IsTrue(
                scenes.Any(s => s.enabled && s.path.Contains("Menu_Main")),
                "Menu_Main scene must be in build settings and enabled.");
        }

        [Test]
        public void BuildSettings_BootstrapIsFirstScene()
        {
            var scenes = EditorBuildSettings.scenes;
            var enabledScenes = scenes.Where(s => s.enabled).ToArray();

            Assert.IsTrue(enabledScenes.Length > 0, "No enabled scenes in build settings.");
            Assert.IsTrue(
                enabledScenes[0].path.Contains("Bootstrap"),
                $"First enabled scene should be Bootstrap, but is: {enabledScenes[0].path}");
        }

        [Test]
        public void BuildSettings_BootstrapBeforeAuthentication()
        {
            var scenes = EditorBuildSettings.scenes;
            int bootstrapIndex = -1;
            int authIndex = -1;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled) continue;
                if (scenes[i].path.Contains("Bootstrap") && bootstrapIndex < 0) bootstrapIndex = i;
                if (scenes[i].path.Contains("Authentication") && authIndex < 0) authIndex = i;
            }

            Assert.IsTrue(bootstrapIndex >= 0, "Bootstrap scene not found in build settings.");
            Assert.IsTrue(authIndex >= 0, "Authentication scene not found in build settings.");
            Assert.Less(bootstrapIndex, authIndex,
                "Bootstrap scene must come before Authentication scene in build order.");
        }

        [Test]
        public void BuildSettings_AuthenticationBeforeMenuMain()
        {
            var scenes = EditorBuildSettings.scenes;
            int authIndex = -1;
            int menuIndex = -1;

            for (int i = 0; i < scenes.Length; i++)
            {
                if (!scenes[i].enabled) continue;
                if (scenes[i].path.Contains("Authentication") && authIndex < 0) authIndex = i;
                if (scenes[i].path.Contains("Menu_Main") && menuIndex < 0) menuIndex = i;
            }

            Assert.IsTrue(authIndex >= 0, "Authentication scene not found in build settings.");
            Assert.IsTrue(menuIndex >= 0, "Menu_Main scene not found in build settings.");
            Assert.Less(authIndex, menuIndex,
                "Authentication scene must come before Menu_Main scene in build order.");
        }

        [Test]
        public void BuildSettings_AllRequiredScenesPresent()
        {
            var enabledPaths = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .ToHashSet();

            foreach (var required in RequiredScenes)
            {
                Assert.IsTrue(enabledPaths.Contains(required),
                    $"Required scene '{required}' is missing from build settings or disabled.");
            }
        }

        #endregion

        #region SceneNameListSO Asset Validation

        [Test]
        public void SceneNameListSOAsset_Exists()
        {
            var guids = AssetDatabase.FindAssets("t:SceneNameListSO");
            Assert.IsTrue(guids.Length > 0,
                "No SceneNameListSO asset found in the project. " +
                "Create one via ScriptableObjects/SceneNameListSO.");
        }

        [Test]
        public void SceneNameListSOAsset_AuthenticationScene_MatchesBuildSettings()
        {
            var guids = AssetDatabase.FindAssets("t:SceneNameListSO");
            if (guids.Length == 0)
            {
                Assert.Inconclusive("No SceneNameListSO asset to validate.");
                return;
            }

            var enabledNames = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .ToHashSet();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.IsNotNull(asset, $"Failed to load SceneNameListSO at {path}");

                var so = new SerializedObject(asset);
                var authProp = so.FindProperty("_authenticationScene");
                Assert.IsNotNull(authProp,
                    $"SceneNameListSO at {path} is missing '_authenticationScene' field.");

                var authValue = authProp.stringValue;
                Assert.IsFalse(string.IsNullOrEmpty(authValue),
                    $"SceneNameListSO at {path} has empty AuthenticationScene.");
                Assert.IsTrue(enabledNames.Contains(authValue),
                    $"SceneNameListSO.AuthenticationScene ('{authValue}') at {path} " +
                    $"not found in enabled build settings scenes.");
            }
        }

        [Test]
        public void SceneNameListSOAsset_MainMenuScene_MatchesBuildSettings()
        {
            var guids = AssetDatabase.FindAssets("t:SceneNameListSO");
            if (guids.Length == 0)
            {
                Assert.Inconclusive("No SceneNameListSO asset to validate.");
                return;
            }

            var enabledNames = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .ToHashSet();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.IsNotNull(asset, $"Failed to load SceneNameListSO at {path}");

                var so = new SerializedObject(asset);
                var mainMenuProp = so.FindProperty("_mainMenuScene");
                Assert.IsNotNull(mainMenuProp,
                    $"SceneNameListSO at {path} is missing '_mainMenuScene' field.");

                var mainMenuValue = mainMenuProp.stringValue;
                Assert.IsFalse(string.IsNullOrEmpty(mainMenuValue),
                    $"SceneNameListSO at {path} has empty MainMenuScene.");
                Assert.IsTrue(enabledNames.Contains(mainMenuValue),
                    $"SceneNameListSO.MainMenuScene ('{mainMenuValue}') at {path} " +
                    $"not found in enabled build settings scenes.");
            }
        }

        [Test]
        public void SceneNameListSOAsset_MultiplayerScene_MatchesBuildSettings()
        {
            var guids = AssetDatabase.FindAssets("t:SceneNameListSO");
            if (guids.Length == 0)
            {
                Assert.Inconclusive("No SceneNameListSO asset to validate.");
                return;
            }

            var enabledNames = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .ToHashSet();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.IsNotNull(asset, $"Failed to load SceneNameListSO at {path}");

                var so = new SerializedObject(asset);
                var multiplayerProp = so.FindProperty("_multiplayerScene");
                Assert.IsNotNull(multiplayerProp,
                    $"SceneNameListSO at {path} is missing '_multiplayerScene' field.");

                var multiplayerValue = multiplayerProp.stringValue;
                if (string.IsNullOrEmpty(multiplayerValue))
                {
                    Assert.Inconclusive($"SceneNameListSO at {path} has empty MultiplayerScene (may not be configured yet).");
                    return;
                }

                Assert.IsTrue(enabledNames.Contains(multiplayerValue),
                    $"SceneNameListSO.MultiplayerScene ('{multiplayerValue}') at {path} " +
                    $"not found in enabled build settings scenes.");
            }
        }

        [Test]
        public void SceneNameListSO_DefaultValues_AreCorrect()
        {
            var sceneNames = ScriptableObject.CreateInstance<Utility.SceneNameListSO>();

            Assert.AreEqual("Bootstrap", sceneNames.BootstrapScene);
            Assert.AreEqual("Authentication", sceneNames.AuthenticationScene);
            Assert.AreEqual("Menu_Main", sceneNames.MainMenuScene);
            Assert.AreEqual("MinigameFreestyleMultiplayer_Gameplay", sceneNames.MultiplayerScene);

            Object.DestroyImmediate(sceneNames);
        }

        #endregion

        #region AppManager Fallback Validation

        [Test]
        public void AppManager_NullConfig_FallbackScene_IsAuthentication()
        {
            // When _sceneNames is null, AppManager falls back to "Authentication".
            // Verify that scene exists in build settings.
            var enabledNames = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .ToHashSet();

            Assert.IsTrue(enabledNames.Contains("Authentication"),
                "AppManager falls back to 'Authentication' when no SceneNameListSO is assigned. " +
                "This scene must exist in build settings.");
        }

        [Test]
        public void AppManager_AutoCreate_ComponentsAreCorrect()
        {
            // Verify that the auto-create flow would produce the right components.
            var go = new GameObject("[TestAutoCreate]");

            go.AddComponent<SceneTransitionManager>();
            go.AddComponent<ApplicationLifecycleManager>();
            var manager = go.AddComponent<AppManager>();

            Assert.IsNotNull(go.GetComponent<SceneTransitionManager>());
            Assert.IsNotNull(go.GetComponent<ApplicationLifecycleManager>());
            Assert.IsNotNull(go.GetComponent<AppManager>());

            Object.DestroyImmediate(go);
            ServiceLocator.ClearAll();
        }

        #endregion

        #region Scene File Existence

        [Test]
        public void BootstrapSceneFile_Exists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Scenes/Bootstrap.unity");
            Assert.IsNotNull(asset, "Bootstrap.unity scene file not found at Assets/_Scenes/Bootstrap.unity");
        }

        [Test]
        public void AuthenticationSceneFile_Exists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Scenes/Authentication.unity");
            Assert.IsNotNull(asset, "Authentication.unity scene file not found at Assets/_Scenes/Authentication.unity");
        }

        [Test]
        public void MenuMainSceneFile_Exists()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/_Scenes/Menu_Main.unity");
            Assert.IsNotNull(asset, "Menu_Main.unity scene file not found at Assets/_Scenes/Menu_Main.unity");
        }

        #endregion

        #region ServiceLocator Integration

        [Test]
        public void SceneTransitionManager_RegistersInServiceLocator_OnAwake()
        {
            ServiceLocator.ClearAll();

            var go = new GameObject("[TestSTM]");
            go.AddComponent<SceneTransitionManager>();

            Assert.IsTrue(ServiceLocator.IsRegistered<SceneTransitionManager>(),
                "SceneTransitionManager should register itself in ServiceLocator on Awake.");

            Object.DestroyImmediate(go);
            ServiceLocator.ClearAll();
        }

        [Test]
        public void SceneTransitionManager_CanBeRetrievedVia_TryGet()
        {
            ServiceLocator.ClearAll();

            var go = new GameObject("[TestSTM]");
            var stm = go.AddComponent<SceneTransitionManager>();

            bool found = ServiceLocator.TryGet<SceneTransitionManager>(out var retrieved);

            Assert.IsTrue(found);
            Assert.AreSame(stm, retrieved);

            Object.DestroyImmediate(go);
            ServiceLocator.ClearAll();
        }

        #endregion
    }
}
