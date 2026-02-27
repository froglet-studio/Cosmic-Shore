#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Class that permits auto-loading a bootstrap scene when the editor switches to play mode.
    /// This class is initialized when Unity is opened and when scripts are recompiled.
    /// This is to be able to subscribe to EditorApplication.playModeStateChanged event, which is when the editor switches to play mode or we wish to open a new scene.
    /// </summary>
    /// <remarks>
    /// A critical edge case scenario regarding NetworkManager is accounted for here.
    /// A NetworkObject's GlobalObjectIdHash value is currently generated in OnValidate() which is invoked during a
    /// build and when the asset is loaded/viewed in the editor.
    /// If we were to manually open Bootstrap Scene via EditorSceneManager.OpenScene(...) as the editor is exiting play mode,
    /// Bootstrap scene would be entering play mode within the editor prior to having loaded any assets, meaning
    /// NetworkManager itself has no entry within the AssetDatabase cache.
    /// As a result of this, any referenced NetworkPrefabs wouldn't have any entry either.
    /// To account for this necessary AssetDatabase step, whenever we're redirecting from a new scene, or a scene
    /// existing in our EditorBuildSettings, we forcefully stop the editor, open Bootstrap scene, and re-enter play mode.
    /// This provides the editor the chance to create AssetDatabase cache entries for the Network Prefabs assigned to the NetworkManager.
    /// If we are entering play mode directly from Bootstrap scene, no additional steps need to be taken and the scene is loaded normally.
    ///
    /// Scene dirty suppression:
    /// The Bootstrap scene contains Cinemachine cameras, URP camera data, and NetworkManager
    /// components whose OnValidate() modifies serialized state on every domain reload and
    /// play-mode exit. Since we cannot modify those package scripts, we track whether the
    /// scene was clean before the transition and, if it became dirty solely from OnValidate
    /// noise, save it once to normalize the serialized state. Subsequent reloads then match
    /// the persisted values and no longer produce a diff.
    /// </remarks>
    [InitializeOnLoad]
    public class SceneBootstrapper
    {
        private const string PREVIOUS_SCENE_KEY = "Previous Scene";
        private const string SHOULD_LOAD_BOOTSTRAP_SCENE_KEY = "Load Main_Menu Scene";
        private const string WAS_DIRTY_BEFORE_RELOAD_KEY = "SceneBootstrapper_WasDirtyBeforeReload";

        private const string LOAD_BOOTSTRAP_SCENE_ON_PLAY = "FrogletTools/TestingMultiplayer/Load Bootstrap Scene on play";
        private const string DO_NOT_LOAD_BOOTSTRAP_SCENE_ON_PLAY = "FrogletTools/TestingMultiplayer/Do not load Bootstrap Scene on Play";

        // To run tests, we need to open a specific scene that has the test runner in it.
        private const string TESTRUNNER_SCENE_NAME = "InitTestScene";

        static bool s_restartingToSwitchScene;

        // Tracks whether the active scene was dirty before entering play mode.
        // Used to distinguish user changes from OnValidate noise after play-mode exit.
        static bool s_wasSceneDirtyBeforePlay;

        static string s_bootstrapScenePath => EditorBuildSettings.scenes[0].path;

        static string s_previousScene
        {
            get => EditorPrefs.GetString(PREVIOUS_SCENE_KEY);
            set => EditorPrefs.SetString(PREVIOUS_SCENE_KEY, value);
        }

        static bool s_shouldLoadBootstrapScene
        {
            get
            {
                if (!EditorPrefs.HasKey(SHOULD_LOAD_BOOTSTRAP_SCENE_KEY))
                {
                    EditorPrefs.SetBool(SHOULD_LOAD_BOOTSTRAP_SCENE_KEY, true);
                }

                return EditorPrefs.GetBool(SHOULD_LOAD_BOOTSTRAP_SCENE_KEY, true);
            }

            set => EditorPrefs.SetBool(SHOULD_LOAD_BOOTSTRAP_SCENE_KEY, value);
        }

        static SceneBootstrapper()
        {
            EditorApplication.playModeStateChanged += EditorApplicationOnPlayModeStateChanged;

            // After each domain reload, record pre-reload dirty state and suppress
            // false dirty flags from OnValidate noise.
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.delayCall += SuppressDomainReloadNoise;
        }

        [MenuItem(LOAD_BOOTSTRAP_SCENE_ON_PLAY, true)]
        static bool ShowLoadBootstrapSceneOnPlay()
        {
            return !s_shouldLoadBootstrapScene;
        }

        [MenuItem(LOAD_BOOTSTRAP_SCENE_ON_PLAY)]
        static void EnableLoadBootstrapSceneOnPlay()
        {
            s_shouldLoadBootstrapScene = true;
        }

        [MenuItem(DO_NOT_LOAD_BOOTSTRAP_SCENE_ON_PLAY, true)]
        static bool ShowDoNotLoadBootstrapSceneOnPlay()
        {
            return s_shouldLoadBootstrapScene;
        }

        [MenuItem(DO_NOT_LOAD_BOOTSTRAP_SCENE_ON_PLAY)]
        static void DisableLoadBootstrapSceneOnPlay()
        {
            s_shouldLoadBootstrapScene = false;
        }

        private static void EditorApplicationOnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (IsTestRunnerActive())
            {
                return;
            }

            if (!s_shouldLoadBootstrapScene)
            {
                return;
            }

            if (s_restartingToSwitchScene)
            {
                if (change == PlayModeStateChange.EnteredPlayMode)
                {
                    // For some reason there's multiple start and stops events happening while restarting the editor's play mode.
                    // We're making sure to set stopping and starting only when we're done and we've entered play mode.
                    // This way we won't corrupt "activeScene" with the multiple start and stop and will be able to return to the scene we were editing at first.
                    s_restartingToSwitchScene = false;
                }
                return;
            }

            if (change == PlayModeStateChange.ExitingEditMode)
            {
                // Record dirty state before play so we can distinguish user changes
                // from OnValidate noise after play-mode exit.
                s_wasSceneDirtyBeforePlay = EditorSceneManager.GetActiveScene().isDirty;

                // cache previous scene so we return to this scene after play session, if possible
                s_previousScene = EditorSceneManager.GetActiveScene().path;

                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    // user either hit "Save" or "Don't Save" in the dialog - we can continue to exit edit mode (current scene) and open bootstrap scene
                    if (!string.IsNullOrEmpty(s_bootstrapScenePath) &&
                        System.Array.Exists(EditorBuildSettings.scenes, scenes => scenes.path == s_bootstrapScenePath))
                    {
                        Scene activeScene = EditorSceneManager.GetActiveScene();

                        s_restartingToSwitchScene = activeScene.path == string.Empty || !s_bootstrapScenePath.Contains(activeScene.path);

                        // we only manually inject Bootstrap scene if we are in a blank empty scene,
                        // or if the active scene is not already Bootstrap scene.
                        if (s_restartingToSwitchScene)
                        {
                            EditorApplication.isPlaying = false;

                            // scene is included in build settings; open it
                            EditorSceneManager.OpenScene(s_bootstrapScenePath);

                            EditorApplication.isPlaying = true;
                        }
                        else
                        {
                            // Already on Bootstrap scene. After the save dialog, the scene
                            // is clean. Record that so post-play suppression works correctly.
                            s_wasSceneDirtyBeforePlay = false;
                        }
                    }
                }
                else
                {
                    // user either hit "Cancel" or exited window; don't open bootstrap scene & return to editor
                    EditorApplication.isPlaying = false;
                }
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                if (!string.IsNullOrEmpty(s_previousScene))
                {
                    // Only reopen when the previous scene differs from the active one.
                    // Reopening the same scene forces a full reload that triggers OnValidate
                    // on all components (Cinemachine cameras, URP camera data, etc.),
                    // which can modify serialized state and mark the scene dirty.
                    if (EditorSceneManager.GetActiveScene().path != s_previousScene)
                    {
                        EditorSceneManager.OpenScene(s_previousScene);
                    }
                }

                // After scene restoration, suppress false dirty state from OnValidate
                // noise (Cinemachine, URP camera data, NetworkManager, etc.).
                // Delay one frame to let all OnValidate calls and edit-mode component
                // evaluation settle before checking.
                EditorApplication.delayCall += SuppressPostPlayDirtyState;
            }
        }

        /// <summary>
        /// After play mode exits, if the scene was clean before play but is now
        /// dirty, the dirt comes from OnValidate on framework components
        /// (Cinemachine cameras, URP camera data, NetworkManager, etc.), not user
        /// changes. Save once to normalize the serialized state so subsequent
        /// reloads no longer produce a diff.
        /// </summary>
        static void SuppressPostPlayDirtyState()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isDirty || string.IsNullOrEmpty(scene.path)) return;

            if (!s_wasSceneDirtyBeforePlay)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        /// <summary>
        /// Before the domain unloads (script recompilation), record whether the
        /// active scene is dirty. Stored in SessionState so it survives the reload.
        /// </summary>
        static void OnBeforeAssemblyReload()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var scene = EditorSceneManager.GetActiveScene();
            SessionState.SetBool(WAS_DIRTY_BEFORE_RELOAD_KEY, scene.isDirty);
        }

        /// <summary>
        /// After domain reload, if the scene was clean before the reload but is
        /// now dirty, it is OnValidate noise from Cinemachine, URP, or Netcode
        /// components. Save to normalize the serialized state.
        /// </summary>
        static void SuppressDomainReloadNoise()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.isDirty || string.IsNullOrEmpty(scene.path)) return;

            bool wasDirtyBeforeReload = SessionState.GetBool(WAS_DIRTY_BEFORE_RELOAD_KEY, false);

            if (!wasDirtyBeforeReload)
            {
                EditorSceneManager.SaveScene(scene);
            }
        }

        static bool IsTestRunnerActive()
        {
            return EditorSceneManager.GetActiveScene().name.StartsWith(TESTRUNNER_SCENE_NAME);
        }
    }
}

#endif
