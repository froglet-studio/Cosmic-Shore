using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CosmicShore.Tools.MiniGameMaker
{
    public static class MiniGameSceneAssembler
    {
        public sealed class Result
        {
            public Scene scene;
            public GameObject gameRoot;
            public string controllerClassName;
        }

        public static Result CreateAndSave(string sceneName,
            MiniGamePrefabLibrarySO lib,
            bool draft,
            out string assetPath,
            int spawnPointCount = 2)
        {
            assetPath = $"Assets/_Game/MiniGames/{sceneName}/{sceneName}.unity";
            string folder = System.IO.Path.GetDirectoryName(assetPath);
            System.IO.Directory.CreateDirectory(folder);

            // 1) New scene (single)
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();

            // 2) Locked: DependencySpawner
            if (lib && lib.dependencySpawnerPrefab)
            {
                var dep = (GameObject)PrefabUtility.InstantiatePrefab(lib.dependencySpawnerPrefab, scene);
                dep.name = "DependencySpawner";
                Undo.RegisterCreatedObjectUndo(dep, "Create DependencySpawner");
            }

            // 3) Game root
            var game = new GameObject("Game");
            Undo.RegisterCreatedObjectUndo(game, "Create Game Root");
            game.transform.position = Vector3.zero;
            game.transform.rotation = Quaternion.identity;
            game.transform.localScale = Vector3.one;

            var net = Undo.AddComponent<NetworkObject>(game);

            // Generate controller
// Generate controller file
            string folderPath = $"Assets/_Game/MiniGames/{sceneName}";
            string controllerPath = MiniGameControllerCodegen.Generate(sceneName, folderPath);
            AssetDatabase.ImportAsset(controllerPath);

            string controllerFullName = $"CosmicShore.Game.MiniGames.{Sanitize(sceneName)}Controller";

// Components we want on Game
            string[] wantedTypes =
            {
                controllerFullName,
                "CosmicShore.Game.Arcade.ScoreTracker",
                "CosmicShore.Game.Arcade.TurnMonitorController",
                "CosmicShore.Game.Arcade.TimeBasedTurnMonitor",
                "CosmicShore.Game.UI.LocalVolumeUIController"
            };

            foreach (var full in wantedTypes)
            {
                var t = TypeResolver.FindType(full);
                if (t != null)
                {
                    if (!game.GetComponent(t))
                        Undo.AddComponent(game, t);
                }
                else
                {
                    // Queue for post-compile
                    PostCompileAttach.QueueComponent(SceneManager.GetActiveScene().path, game.name, full);
                }
            }


            // 3b) Children: SpawnPoints/1/2
            var spRoot = new GameObject("SpawnPoints");
            Undo.RegisterCreatedObjectUndo(spRoot, "Create SpawnPoints");
            spRoot.transform.SetParent(game.transform, false);

            var sp1 = new GameObject("1");
            sp1.transform.SetParent(spRoot.transform, false);
            if (spawnPointCount == 2)
            {
                var sp2 = new GameObject("2");
                sp2.transform.SetParent(spRoot.transform, false);
                sp2.transform.position = new Vector3(5, 0, 0);
            }

            // 4) Locked camera/env/canvas
            TryInstantiateLocked(lib?.miniGameCameraPrefab, "MiniGameMainCamera", scene);
            TryInstantiateLocked(lib?.environmentPrefab, "Environment", scene);
            TryInstantiateLocked(lib?.gameCanvasPrefab, "GameCanvas", scene);

            // 5) Visible spawners
            TryInstantiateConfig(lib?.playerSpawnerPrefab, "PlayerAndShipSpawner", scene);
            // TryInstantiateConfig(lib?.shipSpawnerPrefab, "ShipSpawner", scene);
            
            AutoWire(game);
            
            var profile = lib is not null ? lib.defaultProfile : null;
            ApplyProfile(game, profile);
            
            // 6) Save scene (unless Draft)
            if (!draft)
            {
                EditorSceneManager.SaveScene(scene, assetPath);
                AssetDatabase.Refresh();
            }

            Undo.CollapseUndoOperations(group);

            return new Result { scene = scene, gameRoot = game, controllerClassName = controllerFullName };
        }

        private static void TryInstantiateLocked(GameObject prefab, string name, Scene scene)
        {
            if (!prefab) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            // Locked = user hidden in Overview; not actually hideFlags, so it stays visible in Hierarchy
        }

        private static void TryInstantiateConfig(GameObject prefab, string name, Scene scene)
        {
            if (!prefab) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        }

        private static string Sanitize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "MiniGame";
            var safe = System.Text.RegularExpressions.Regex.Replace(raw, @"[^A-Za-z0-9_]", "");
            if (char.IsDigit(safe[0])) safe = "_" + safe;
            return safe;
        }

        private static void AutoWire(GameObject game)
        {
            // MiniGameData from any MiniGameDataProvider on roots
            var miniGameDataObj = FindProviderObject("MiniGameDataProvider", "miniGameData");
            // VolumeUI from VolumeUIProvider (usually on GameCanvas)
            var volumeUIObj = FindProviderObject("VolumeUIProvider", "volumeUI");

            // ScoreTracker.miniGameData
            SetSerializedRefOn(game, "CosmicShore.Game.Arcade.ScoreTracker", "miniGameData", miniGameDataObj);

            // TurnMonitorController.miniGameData + ensure monitors list contains TimeBasedTurnMonitor on Game
            SetSerializedRefOn(game, "CosmicShore.Game.Arcade.TurnMonitorController", "miniGameData", miniGameDataObj);
            EnsureListContainsComponent(game, "CosmicShore.Game.Arcade.TurnMonitorController", "monitors",
                GetComponentOn(game, "CosmicShore.Game.Arcade.TimeBasedTurnMonitor"));

            // TimeBasedTurnMonitor.duration default (only if 0)
            SetFloatIfZero(game, "CosmicShore.Game.Arcade.TimeBasedTurnMonitor", "duration", 60f);

            // LocalVolumeUIController.miniGameData & volumeUI
            SetSerializedRefOn(game, "CosmicShore.Game.UI.LocalVolumeUIController", "miniGameData", miniGameDataObj);
            SetSerializedRefOn(game, "CosmicShore.Game.UI.LocalVolumeUIController", "volumeUI", volumeUIObj);
        }

        private static Component GetComponentOn(GameObject go, string fullType)
        {
            var t = TypeResolver.FindType(fullType);
            return t != null ? go.GetComponent(t) : null;
        }

        private static Object FindProviderObject(string providerTypeName, string fieldName)
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots)
            {
                var provider = r.GetComponent(providerTypeName);
                if (!provider) continue;
                var so = new SerializedObject(provider);
                var sp = so.FindProperty(fieldName);
                if (sp != null && sp.objectReferenceValue) return sp.objectReferenceValue;
            }

            return null;
        }

        private static void SetSerializedRefOn(GameObject go, string fullType, string prop, UnityEngine.Object value)
        {
            if (!value) return;
            var t = TypeResolver.FindType(fullType);
            if (t == null) return;
            var c = go.GetComponent(t);
            if (!c) return;

            var so = new SerializedObject(c);
            var sp = so.FindProperty(prop);
            if (sp != null && sp.objectReferenceValue == null)
            {
                sp.objectReferenceValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }

        private static void EnsureListContainsComponent(GameObject go, string listOwnerType, string listProp,
            Component item)
        {
            if (!item) return;
            var t = TypeResolver.FindType(listOwnerType);
            if (t == null) return;
            var c = go.GetComponent(t);
            if (!c) return;

            var so = new SerializedObject(c);
            var list = so.FindProperty(listProp);
            if (list != null && list.isArray)
            {
                bool present = false;
                for (int i = 0; i < list.arraySize; i++)
                    present |= list.GetArrayElementAtIndex(i).objectReferenceValue == item;

                if (!present)
                {
                    list.arraySize++;
                    list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = item;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
            }
        }

        private static void SetFloatIfZero(GameObject go, string fullType, string prop, float value)
        {
            var t = TypeResolver.FindType(fullType);
            if (t == null) return;
            var c = go.GetComponent(t);
            if (!c) return;
            var so = new SerializedObject(c);
            var sp = so.FindProperty(prop);
            if (sp != null && Mathf.Approximately(sp.floatValue, 0f))
            {
                sp.floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
        }

        // in MiniGameSceneAssembler.cs

        private static void ApplyProfile(GameObject game, MiniGameProfileSO profile)
        {
            if (!profile) return;

            // 1) Components on Game
            SetSerializedRefOn(game, "CosmicShore.Game.Arcade.ScoreTracker", "miniGameData", profile.miniGameData);
            SetBoolOn(game, "CosmicShore.Game.Arcade.ScoreTracker", "golfRules", profile.golfRules);
            SetArrayOn(game, "CosmicShore.Game.Arcade.ScoreTracker", "scoringConfigs", profile.scoringConfigs);

            SetSerializedRefOn(game, "CosmicShore.Game.Arcade.TurnMonitorController", "miniGameData",
                profile.miniGameData);
            SetFloatOn(game, "CosmicShore.Game.Arcade.TimeBasedTurnMonitor", "duration", profile.turnDurationSeconds);

            SetSerializedRefOn(game, "CosmicShore.Game.UI.LocalVolumeUIController", "miniGameData",
                profile.miniGameData);

            // 2) Optionally push events into MiniGameDataSO (by field name match)
            TryAssignEventsIntoData(profile.miniGameData, profile.eventsToAssign);
        }

        private static void SetBoolOn(GameObject go, string fullType, string prop, bool value)
        {
            var t = TypeResolver.FindType(fullType);
            if (t == null) return;
            var c = go.GetComponent(t);
            if (!c) return;
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.Boolean)
            {
                p.boolValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetFloatOn(GameObject go, string fullType, string prop, float value)
        {
            var t = TypeResolver.FindType(fullType);
            if (t == null) return;
            var c = go.GetComponent(t);
            if (!c) return;
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            if (p != null && p.propertyType == SerializedPropertyType.Float)
            {
                p.floatValue = value;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetArrayOn(GameObject go, string fullType, string prop, Object[] values)
        {
            if (values == null) return;
            var t = TypeResolver.FindType(fullType);
            if (t == null) return;
            var c = go.GetComponent(t);
            if (!c) return;
            var so = new SerializedObject(c);
            var p = so.FindProperty(prop);
            if (p != null && p.isArray)
            {
                p.arraySize = values.Length;
                for (int i = 0; i < values.Length; i++)
                    p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

// Tries to assign known ScriptableEvent-like assets into the MiniGameDataSO by matching field names/types
        private static void TryAssignEventsIntoData(Object miniGameData, Object[] events)
        {
            if (!miniGameData || events == null || events.Length == 0) return;

            var so = new SerializedObject(miniGameData);
            var it = so.GetIterator();
            foreach (var ev in events)
            {
                if (!ev) continue;
                // naive pass: find first null field of the same type and assign
                it.Reset();
                bool enterChildren = true;
                while (it.Next(enterChildren))
                {
                    enterChildren = false;
                    if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                    if (it.objectReferenceValue != null) continue;

                    var targetFieldType = it.serializedObject.targetObject.GetType()
                        .GetField(it.name,
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic)
                        ?.FieldType;

                    if (targetFieldType == null) continue;
                    if (targetFieldType.IsAssignableFrom(ev.GetType()))
                    {
                        it.objectReferenceValue = ev;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        break;
                    }
                }
            }
        }
    }
}