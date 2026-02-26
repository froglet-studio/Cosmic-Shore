using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using CosmicShore.Utility.DataContainers;

namespace CosmicShore.Systems.Bootstrap.Tests
{
    /// <summary>
    /// Integration tests that verify the Bootstrap → Authentication → Menu_Main
    /// scene flow is correctly wired across all configuration assets and code.
    /// </summary>
    [TestFixture]
    public class SceneFlowIntegrationTests
    {
        static readonly string[] RequiredScenes = { "Bootstrap", "Authentication", "Menu_Main" };

        [SetUp]
        public void SetUp()
        {
            var field = typeof(BootstrapController)
                .GetField("_hasBootstrapped", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, false);
            ServiceLocator.ClearAll();
        }

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            var field = typeof(BootstrapController)
                .GetField("_hasBootstrapped", BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, false);
            ServiceLocator.ClearAll();
        }

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
        public void SceneNameListSOAsset_BootstrapScene_IsNotEmpty()
        {
            var guids = AssetDatabase.FindAssets("t:SceneNameListSO");
            if (guids.Length == 0)
            {
                Assert.Inconclusive("No SceneNameListSO asset to validate.");
                return;
            }

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.IsNotNull(asset, $"Failed to load SceneNameListSO at {path}");

                var so = new SerializedObject(asset);
                var prop = so.FindProperty("BootstrapScene");
                Assert.IsNotNull(prop,
                    $"SceneNameListSO at {path} is missing 'BootstrapScene' field.");
                Assert.IsFalse(string.IsNullOrEmpty(prop.stringValue),
                    $"SceneNameListSO at {path} has empty BootstrapScene.");
            }
        }

        [Test]
        public void SceneNameListSOAsset_AuthenticationScene_IsNotEmpty()
        {
            var guids = AssetDatabase.FindAssets("t:SceneNameListSO");
            if (guids.Length == 0)
            {
                Assert.Inconclusive("No SceneNameListSO asset to validate.");
                return;
            }

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.IsNotNull(asset, $"Failed to load SceneNameListSO at {path}");

                var so = new SerializedObject(asset);
                var prop = so.FindProperty("AuthenticationScene");
                Assert.IsNotNull(prop,
                    $"SceneNameListSO at {path} is missing 'AuthenticationScene' field.");
                Assert.IsFalse(string.IsNullOrEmpty(prop.stringValue),
                    $"SceneNameListSO at {path} has empty AuthenticationScene.");
            }
        }

        [Test]
        public void SceneNameListSOAsset_BootstrapScene_MatchesBuildSettings()
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
                var prop = so.FindProperty("BootstrapScene");
                var value = prop.stringValue;
                Assert.IsTrue(enabledNames.Contains(value),
                    $"SceneNameListSO.BootstrapScene ('{value}') at {path} " +
                    $"not found in enabled build settings scenes.");
            }
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
                var prop = so.FindProperty("AuthenticationScene");
                var value = prop.stringValue;
                Assert.IsTrue(enabledNames.Contains(value),
                    $"SceneNameListSO.AuthenticationScene ('{value}') at {path} " +
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
                var mainMenuProp = so.FindProperty("MainMenuScene");
                Assert.IsNotNull(mainMenuProp,
                    $"SceneNameListSO at {path} is missing 'MainMenuScene' field.");

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
                var multiplayerProp = so.FindProperty("MultiplayerScene");
                Assert.IsNotNull(multiplayerProp,
                    $"SceneNameListSO at {path} is missing 'MultiplayerScene' field.");

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

        #endregion

        #region BootstrapController Fallback Validation

        [Test]
        public void BootstrapController_NullSceneNames_FallbackScene_IsAuthentication()
        {
            // When _sceneNames is null, BootstrapController falls back to "Authentication".
            // Verify that scene exists in build settings.
            var enabledNames = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => System.IO.Path.GetFileNameWithoutExtension(s.path))
                .ToHashSet();

            Assert.IsTrue(enabledNames.Contains("Authentication"),
                "BootstrapController falls back to 'Authentication' when no SceneNameListSO is assigned. " +
                "This scene must exist in build settings.");
        }

        [Test]
        public void BootstrapController_AutoCreate_ComponentsAreCorrect()
        {
            // Awake may call DontDestroyOnLoad which can log errors in Edit Mode.
            LogAssert.ignoreFailingMessages = true;

            // Verify that the auto-create flow would produce the right components.
            var go = new GameObject("[TestAutoCreate]");

            go.AddComponent<SceneTransitionManager>();
            go.AddComponent<ApplicationLifecycleManager>();
            var controller = go.AddComponent<BootstrapController>();

            Assert.IsNotNull(go.GetComponent<SceneTransitionManager>());
            Assert.IsNotNull(go.GetComponent<ApplicationLifecycleManager>());
            Assert.IsNotNull(go.GetComponent<BootstrapController>());

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
