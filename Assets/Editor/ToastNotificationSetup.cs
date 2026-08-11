using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CosmicShore.UI;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    public static class ToastNotificationSetup
    {
        // Everything lives in Resources so the runtime-auto-created manager
        // (ToastNotificationAPI) can resolve settings, channel, and prefab.
        private const string ResourcesFolder = "Assets/Resources";
        private const string ChannelFolder = "Assets/Resources/Channels";
        private const string PrefabPath = ResourcesFolder + "/ToastNotificationItem.prefab";
        private const string SettingsPath = ResourcesFolder + "/ToastNotificationSettings.asset";
        private const string ChannelPath = ChannelFolder + "/ToastNotificationChannel.asset";

        [MenuItem("FrogletTools/Interface/Toast Notification/Create All Assets", priority = 0)]
        [FrogletTool(FrogletToolCategory.Interface, Importance = 3,
            Description = "Scaffold the whole toast notification asset set.")]
        public static void CreateAllAssets()
        {
            CreateSettingsAsset();
            CreateChannelAsset();
            CreatePrefab();
            CreateManagerInScene();

            Debug.Log("[ToastNotification] All assets created. Customize the prefab at " + PrefabPath);
        }

        [MenuItem("FrogletTools/Interface/Toast Notification/Create Settings Asset")]
        public static void CreateSettingsAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<ToastNotificationSettingsSO>(SettingsPath) != null)
            {
                Debug.Log("[ToastNotification] Settings asset already exists at " + SettingsPath);
                return;
            }

            EnsureFolder(ResourcesFolder);
            var settings = ScriptableObject.CreateInstance<ToastNotificationSettingsSO>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ToastNotification] Created settings at " + SettingsPath);
        }

        [MenuItem("FrogletTools/Interface/Toast Notification/Create Channel Asset")]
        public static void CreateChannelAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<ToastNotificationChannel>(ChannelPath) != null)
            {
                Debug.Log("[ToastNotification] Channel asset already exists at " + ChannelPath);
                return;
            }

            EnsureFolder(ChannelFolder);
            var channel = ScriptableObject.CreateInstance<ToastNotificationChannel>();
            AssetDatabase.CreateAsset(channel, ChannelPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[ToastNotification] Created channel at " + ChannelPath);
        }

        [MenuItem("FrogletTools/Interface/Toast Notification/Create Prefab")]
        public static void CreatePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            {
                Debug.Log("[ToastNotification] Prefab already exists at " + PrefabPath);
                return;
            }

            EnsureFolder(ResourcesFolder);

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
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;

            // Add the toast item component and wire the messageText field
            var item = root.AddComponent<ToastNotificationItem>();
            SetObjectReference(item, "messageText", tmp);

            // Save as prefab
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[ToastNotification] Created prefab at " + PrefabPath +
                      " - customize visuals here (background, font, size, etc.)");
        }

        [MenuItem("FrogletTools/Interface/Toast Notification/Add Manager To Scene")]
        public static void CreateManagerInScene()
        {
            if (Object.FindFirstObjectByType<ToastNotificationManager>() != null)
            {
                Debug.Log("[ToastNotification] Manager already exists in scene.");
                return;
            }

            var go = new GameObject("ToastNotificationManager");
            var mgr = go.AddComponent<ToastNotificationManager>();

            SetObjectReference(mgr, "settings",
                AssetDatabase.LoadAssetAtPath<ToastNotificationSettingsSO>(SettingsPath));
            SetObjectReference(mgr, "channel",
                AssetDatabase.LoadAssetAtPath<ToastNotificationChannel>(ChannelPath));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
                SetObjectReference(mgr, "toastPrefab", prefab.GetComponent<ToastNotificationItem>());

            Undo.RegisterCreatedObjectUndo(go, "Create ToastNotificationManager");
            Selection.activeGameObject = go;
            Debug.Log("[ToastNotification] Manager added to scene. Move it to your bootstrap/persistent scene.");
        }

        #region Helpers

        private static void SetObjectReference(Object target, string propertyName, Object value)
        {
            if (value == null) return;

            var so = new SerializedObject(target);
            var prop = so.FindProperty(propertyName);
            if (prop == null)
            {
                Debug.LogWarning($"[ToastNotification] Property '{propertyName}' not found on {target.GetType().Name}.");
                return;
            }

            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

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
