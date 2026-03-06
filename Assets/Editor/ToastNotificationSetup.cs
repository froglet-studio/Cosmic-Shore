using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CosmicShore.UI;

namespace CosmicShore.Editor
{
    public static class ToastNotificationSetup
    {
        private const string PrefabFolder = "Assets/_Prefabs/UI Elements";
        private const string SOFolder = "Assets/_SO_Assets";
        private const string ChannelFolder = "Assets/Resources/Channels";

        [MenuItem("Cosmic Shore/Toast Notification/Create All Assets", priority = 0)]
        public static void CreateAllAssets()
        {
            CreateSettingsAsset();
            CreateChannelAsset();
            CreatePrefab();
            CreateManagerInScene();

            Debug.Log("[ToastNotification] All assets created. Customize the prefab at " +
                      PrefabFolder + "/ToastNotificationItem.prefab");
        }

        [MenuItem("Cosmic Shore/Toast Notification/Create Settings Asset")]
        public static void CreateSettingsAsset()
        {
            var path = SOFolder + "/ToastNotificationSettings.asset";
            if (AssetDatabase.LoadAssetAtPath<ToastNotificationSettingsSO>(path) != null)
            {
                Debug.Log("[ToastNotification] Settings asset already exists at " + path);
                return;
            }

            EnsureFolder(SOFolder);
            var settings = ScriptableObject.CreateInstance<ToastNotificationSettingsSO>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[ToastNotification] Created settings at " + path);
        }

        [MenuItem("Cosmic Shore/Toast Notification/Create Channel Asset")]
        public static void CreateChannelAsset()
        {
            var path = ChannelFolder + "/ToastNotificationChannel.asset";
            if (AssetDatabase.LoadAssetAtPath<ToastNotificationChannel>(path) != null)
            {
                Debug.Log("[ToastNotification] Channel asset already exists at " + path);
                return;
            }

            EnsureFolder(ChannelFolder);
            var channel = ScriptableObject.CreateInstance<ToastNotificationChannel>();
            AssetDatabase.CreateAsset(channel, path);
            AssetDatabase.SaveAssets();
            Debug.Log("[ToastNotification] Created channel at " + path);
        }

        [MenuItem("Cosmic Shore/Toast Notification/Create Prefab")]
        public static void CreatePrefab()
        {
            var path = PrefabFolder + "/ToastNotificationItem.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log("[ToastNotification] Prefab already exists at " + path);
                return;
            }

            EnsureFolder(PrefabFolder);

            // Root object
            var root = new GameObject("ToastNotificationItem");
            var rootRT = root.AddComponent<RectTransform>();
            rootRT.anchorMin = new Vector2(0f, 1f);
            rootRT.anchorMax = new Vector2(0f, 1f);
            rootRT.pivot = new Vector2(0f, 1f);
            rootRT.sizeDelta = new Vector2(520f, 80f);

            var cg = root.AddComponent<CanvasGroup>();
            cg.alpha = 1f;

            // Background
            var bg = CreateChild("Background", root.transform);
            Stretch(bg);
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.08f, 0.08f, 0.12f, 0.92f);
            bgImage.raycastTarget = true;

            // Message text
            var textGO = CreateChild("MessageText", root.transform);
            var textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(20f, 8f);
            textRT.offsetMax = new Vector2(-20f, -8f);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = "Notification message here";
            tmp.fontSize = 22;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.enableWordWrapping = true;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;

            // Add the toast item component
            var item = root.AddComponent<ToastNotificationItem>();

            // Wire the messageText field
            var field = typeof(ToastNotificationItem).GetField("messageText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(item, tmp);

            // Save as prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);

            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[ToastNotification] Created prefab at " + path +
                      " — customize visuals here (background, font, size, etc.)");
        }

        [MenuItem("Cosmic Shore/Toast Notification/Add Manager To Scene")]
        public static void CreateManagerInScene()
        {
            if (Object.FindFirstObjectByType<ToastNotificationManager>() != null)
            {
                Debug.Log("[ToastNotification] Manager already exists in scene.");
                return;
            }

            var go = new GameObject("ToastNotificationManager");
            var mgr = go.AddComponent<ToastNotificationManager>();

            // Wire settings
            var settings = AssetDatabase.LoadAssetAtPath<ToastNotificationSettingsSO>(
                "Assets/Resources/ToastNotificationSettings.asset");
            if (settings != null)
            {
                var settingsField = typeof(ToastNotificationManager).GetField("settings",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                settingsField?.SetValue(mgr, settings);
            }

            // Wire channel
            var channel = AssetDatabase.LoadAssetAtPath<ToastNotificationChannel>(
                ChannelFolder + "/ToastNotificationChannel.asset");
            if (channel != null)
            {
                var channelField = typeof(ToastNotificationManager).GetField("channel",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                channelField?.SetValue(mgr, channel);
            }

            // Wire prefab
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                PrefabFolder + "/ToastNotificationItem.prefab");
            if (prefab != null)
            {
                var item = prefab.GetComponent<ToastNotificationItem>();
                if (item != null)
                {
                    var prefabField = typeof(ToastNotificationManager).GetField("toastPrefab",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    prefabField?.SetValue(mgr, item);
                }
            }

            Undo.RegisterCreatedObjectUndo(go, "Create ToastNotificationManager");
            Selection.activeGameObject = go;
            Debug.Log("[ToastNotification] Manager added to scene. Move it to your bootstrap/persistent scene.");
        }

        #region Helpers

        private static GameObject CreateChild(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Stretch(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            var parts = path.Split('/');
            var current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        #endregion
    }
}
