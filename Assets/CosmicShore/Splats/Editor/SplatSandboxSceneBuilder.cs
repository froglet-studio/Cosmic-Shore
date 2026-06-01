using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using CosmicShore.Splats;

namespace CosmicShore.Tools.SplatImport
{
    // Editor command that opens a minimal "SplatSandbox" scene with a fly camera and the
    // splat cloud. Designed to bypass the gameplay/DI stack entirely so the splat pipeline
    // is testable even when Bootstrap/Reflex injection isn't behaving — useful as a
    // fallback when the arcade flow trips DI nulls on the cloned scene's components.
    //
    // NOTE: SceneBootstrapper (FrogletTools menu) will normally hijack Play to load
    // Bootstrap first. Toggle "FrogletTools/Legacy/TestingMultiplayer/Do not load
    // Bootstrap Scene on Play" before pressing Play so the sandbox actually starts here.
    public static class SplatSandboxSceneBuilder
    {
        const string SandboxScenePath = "Assets/CosmicShore/Splats/SplatSandbox.unity";
        const string SyntheticSetPath = "Assets/CosmicShore/Splats/Resources/SplatPrismSet_Synthetic.asset";
        const string FallbackSyntheticSetPath = "Assets/CosmicShore/Splats/Baked/SplatPrismSet_Synthetic.asset";

        [MenuItem("Tools/Splats/Open Splat Sandbox (No Bootstrap)")]
        public static void OpenSandbox()
        {
            var splatSet = AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath)
                        ?? AssetDatabase.LoadAssetAtPath<SplatPrismSet>(FallbackSyntheticSetPath);
            if (splatSet == null)
            {
                if (EditorUtility.DisplayDialog(
                        "SplatPrismSet missing",
                        $"No SplatPrismSet at {SyntheticSetPath}. Run Tools/Splats/Setup GSplat Arcade Game first to bake one.\n\nRun setup now?",
                        "Run Setup", "Cancel"))
                {
                    SplatArcadeSetup.Run(silent: true);
                    splatSet = AssetDatabase.LoadAssetAtPath<SplatPrismSet>(SyntheticSetPath);
                }
                if (splatSet == null)
                {
                    Debug.LogError("[SplatSandbox] Cannot proceed without a SplatPrismSet.");
                    return;
                }
            }

            Scene scene;
            if (File.Exists(SandboxScenePath))
            {
                scene = EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);
                EnsureCloud(scene, splatSet);
                EnsureCamera(scene);
                EnsureLight(scene);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EnsureCamera(scene);
                EnsureLight(scene);
                EnsureCloud(scene, splatSet);
                EditorSceneManager.SaveScene(scene, SandboxScenePath);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            EditorUtility.DisplayDialog(
                "Splat Sandbox Ready",
                "The SplatSandbox scene is open.\n\n" +
                "Before hitting Play:\n" +
                "  1. Click FrogletTools > Legacy > TestingMultiplayer > 'Do not load Bootstrap Scene on Play' (so Play starts here, not in Bootstrap).\n" +
                "  2. Press Play.\n\n" +
                "Controls: Hold right mouse to look. WASD to move. Space/Q vertical. Shift fast, Ctrl slow.",
                "OK");
        }

        private static void EnsureCloud(Scene scene, SplatPrismSet set)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "SplatCloud") { WireSpawner(root, set); return; }

            var go = new GameObject("SplatCloud");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(0f, 0f, 30f);
            var spawner = go.AddComponent<SplatPrismSpawner>();
            WireSpawnerSerialized(spawner, set);
        }

        private static void WireSpawner(GameObject go, SplatPrismSet set)
        {
            var spawner = go.GetComponent<SplatPrismSpawner>() ?? go.AddComponent<SplatPrismSpawner>();
            WireSpawnerSerialized(spawner, set);
        }

        private static void WireSpawnerSerialized(SplatPrismSpawner spawner, SplatPrismSet set)
        {
            var so = new SerializedObject(spawner);
            var prop = so.FindProperty("set");
            if (prop != null)
            {
                prop.objectReferenceValue = set;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                // Last-resort safety: still set via the runtime setter so play still works.
                spawner.SetSourceSet(set);
            }
        }

        private static void EnsureCamera(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Sandbox Camera") return;

            var go = new GameObject("Sandbox Camera");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.position = new Vector3(0f, 5f, -25f);
            go.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 65f;
            cam.farClipPlane = 5000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.05f, 1f);
            go.AddComponent<AudioListener>();
            go.AddComponent<SplatSandboxFlyCam>();
            go.tag = "MainCamera";
        }

        private static void EnsureLight(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Sun") return;

            var go = new GameObject("Sun");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.color = new Color(1f, 0.95f, 0.9f);
        }
    }
}
