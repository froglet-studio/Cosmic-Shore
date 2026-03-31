using UnityEditor;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.Data;
using Unity.Netcode;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor utility to set up the NetworkCrystalManager for Menu_Main.
    /// Run via Tools > Cosmic Shore > Setup Menu Crystal Manager.
    ///
    /// Creates:
    ///   1. A CellRuntimeDataSO asset at _SO_Assets/Menu Crystal Data/
    ///   2. A "MenuCrystalManager" GameObject in the active scene with
    ///      NetworkObject + NetworkCrystalManager configured for a single Jade crystal.
    ///
    /// After running, manually wire:
    ///   - crystalPrefab → Crystal.prefab (from _Prefabs/Environment/)
    ///   - cellData SOAP events (OnCrystalSpawned, OnCellItemsUpdated) if needed
    ///   - Anchor positions for the menu flight area
    /// </summary>
    public static class MenuCrystalSetup
    {
        private const string MenuPath = "Tools/Cosmic Shore/Setup Menu Crystal Manager";
        private const string SOFolder = "Assets/_SO_Assets/Menu Crystal Data";
        private const string SOAssetName = "MenuCellRuntimeData";

        [MenuItem(MenuPath)]
        public static void SetupMenuCrystalManager()
        {
            // 1. Create SO asset folder if needed
            if (!AssetDatabase.IsValidFolder(SOFolder))
            {
                string parent = System.IO.Path.GetDirectoryName(SOFolder).Replace('\\', '/');
                string folder = System.IO.Path.GetFileName(SOFolder);
                AssetDatabase.CreateFolder(parent, folder);
            }

            // 2. Create or find CellRuntimeDataSO asset
            string assetPath = $"{SOFolder}/{SOAssetName}.asset";
            var cellData = AssetDatabase.LoadAssetAtPath<CellRuntimeDataSO>(assetPath);
            if (cellData == null)
            {
                cellData = ScriptableObject.CreateInstance<CellRuntimeDataSO>();
                AssetDatabase.CreateAsset(cellData, assetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[MenuCrystalSetup] Created CellRuntimeDataSO at {assetPath}");
            }
            else
            {
                Debug.Log($"[MenuCrystalSetup] Found existing CellRuntimeDataSO at {assetPath}");
            }

            // 3. Create scene GameObject
            var existing = GameObject.Find("MenuCrystalManager");
            if (existing != null)
            {
                Debug.LogWarning("[MenuCrystalSetup] MenuCrystalManager already exists in scene. Skipping creation.");
                Selection.activeGameObject = existing;
                return;
            }

            var go = new GameObject("MenuCrystalManager");

            // Add NetworkObject (required for NetworkCrystalManager which extends NetworkBehaviour)
            go.AddComponent<NetworkObject>();

            // Add NetworkCrystalManager
            var manager = go.AddComponent<NetworkCrystalManager>();

            // Use SerializedObject to set private/protected serialized fields
            var so = new SerializedObject(manager);

            // cellData
            var cellDataProp = so.FindProperty("cellData");
            if (cellDataProp != null)
                cellDataProp.objectReferenceValue = cellData;

            // spawnOnClientReady = true (crystal appears when menu loads)
            var spawnOnClientReadyProp = so.FindProperty("spawnOnClientReady");
            if (spawnOnClientReadyProp != null)
                spawnOnClientReadyProp.boolValue = true;

            // spawnCrystalWithPlayerDomain = false (always Jade, not per-player)
            var spawnWithDomainProp = so.FindProperty("spawnCrystalWithPlayerDomain");
            if (spawnWithDomainProp != null)
                spawnWithDomainProp.boolValue = false;

            // defaultCrystalDomain = Jade (1)
            var defaultDomainProp = so.FindProperty("defaultCrystalDomain");
            if (defaultDomainProp != null)
                defaultDomainProp.intValue = (int)Domains.Jade;

            // extraCrystalsToSpawnBeyondPlayerCount = 0
            var extraProp = so.FindProperty("extraCrystalsToSpawnBeyondPlayerCount");
            if (extraProp != null)
                extraProp.intValue = 0;

            so.ApplyModifiedProperties();

            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Selection.activeGameObject = go;
            Debug.Log("[MenuCrystalSetup] Created MenuCrystalManager. " +
                      "Wire crystalPrefab to Crystal.prefab and add anchor positions manually.");
        }
    }
}
