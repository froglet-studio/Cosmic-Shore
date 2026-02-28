using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CosmicShore.UI;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Editor utility to create and wire missing Party system prefabs and SO references.
    /// Run via menu: Tools / Cosmic Shore / Party System Setup.
    /// </summary>
    public static class PartyPrefabSetup
    {
        private const string PrefabFolder = "Assets/_Prefabs/UI Elements/Panels/Party";
        private const string SOFolder = "Assets/_SO_Assets";

        // ── Full Setup ────────────────────────────────────────────────────

        [MenuItem("Tools/Cosmic Shore/Party System Setup (Full)")]
        public static void FullPartySetup()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Prefabs/UI Elements/Panels", "Party");

            CreateOnlinePlayerEntryPrefab();
            CreateOnlinePlayersPanelPrefab();
            CreatePartySlotViewPrefab();
            CreatePartyInviteNotificationPrefab();
            CreateInviteNotificationUIPrefab();
            CreatePartyAreaPanelPrefab();

            WireOnlinePlayersPanelReferences();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            ValidatePartySetup();

            Debug.Log("[PartyPrefabSetup] Full party setup completed.");
        }

        // ── Validation ────────────────────────────────────────────────────

        [MenuItem("Tools/Cosmic Shore/Validate Party Setup")]
        public static void ValidatePartySetup()
        {
            int issues = 0;

            // 1. Check HostConnectionData SO exists
            var connectionData = FindAsset<HostConnectionDataSO>();
            if (connectionData == null)
            {
                Debug.LogError("[PartySetup] HostConnectionDataSO asset not found!");
                issues++;
            }

            // 2. Check AppManager prefab has hostConnectionData wired
            var appManagerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Prefabs/CORE/AppManager.prefab");
            if (appManagerPrefab != null)
            {
                var appManager = appManagerPrefab.GetComponent<AppManager>();
                if (appManager != null)
                {
                    var so = new SerializedObject(appManager);
                    var prop = so.FindProperty("hostConnectionData");
                    if (prop != null && prop.objectReferenceValue == null)
                    {
                        Debug.LogWarning("[PartySetup] AppManager.hostConnectionData is not wired. Attempting auto-wire...");
                        if (connectionData != null)
                        {
                            prop.objectReferenceValue = connectionData;
                            so.ApplyModifiedPropertiesWithoutUndo();
                            EditorUtility.SetDirty(appManagerPrefab);
                            Debug.Log("[PartySetup] Auto-wired AppManager.hostConnectionData.");
                        }
                        else
                            issues++;
                    }
                }
            }

            // 3. Check PartyServices prefab exists and has all components
            var partyServicesPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Prefabs/CORE/PartyServices.prefab");
            if (partyServicesPrefab == null)
            {
                Debug.LogError("[PartySetup] PartyServices.prefab not found in _Prefabs/CORE/!");
                issues++;
            }
            else
            {
                if (partyServicesPrefab.GetComponent<CosmicShore.Gameplay.HostConnectionService>() == null)
                {
                    Debug.LogError("[PartySetup] PartyServices prefab missing HostConnectionService!");
                    issues++;
                }
                if (partyServicesPrefab.GetComponent<CosmicShore.Gameplay.PartyInviteController>() == null)
                {
                    Debug.LogError("[PartySetup] PartyServices prefab missing PartyInviteController!");
                    issues++;
                }
                if (partyServicesPrefab.GetComponent<CosmicShore.Gameplay.FriendsInitializer>() == null)
                {
                    Debug.LogError("[PartySetup] PartyServices prefab missing FriendsInitializer!");
                    issues++;
                }
            }

            // 4. Check required prefabs exist
            string[] requiredPrefabs = {
                "OnlinePlayerEntry", "OnlinePlayersPanel", "PartySlotView",
                "PartyInviteNotificationPanel", "InviteNotificationUI", "PartyAreaPanel"
            };
            foreach (var name in requiredPrefabs)
            {
                var path = $"{PrefabFolder}/{name}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    Debug.LogWarning($"[PartySetup] Missing prefab: {path}. Run 'Party System Setup (Full)' to create it.");
                    issues++;
                }
            }

            // 5. Check OnlinePlayersPanel has playerEntryPrefab wired
            var onlinePanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/OnlinePlayersPanel.prefab");
            if (onlinePanelPrefab != null)
            {
                var panel = onlinePanelPrefab.GetComponent<OnlinePlayersPanel>();
                if (panel != null)
                {
                    var so = new SerializedObject(panel);
                    var entryPrefabProp = so.FindProperty("playerEntryPrefab");
                    if (entryPrefabProp != null && entryPrefabProp.objectReferenceValue == null)
                    {
                        Debug.LogWarning("[PartySetup] OnlinePlayersPanel.playerEntryPrefab is not wired.");
                        issues++;
                    }
                }
            }

            if (issues == 0)
                Debug.Log("[PartySetup] All validation checks passed!");
            else
                Debug.LogWarning($"[PartySetup] {issues} issue(s) found. See warnings above.");
        }

        // ── Wire SO References ────────────────────────────────────────────

        [MenuItem("Tools/Cosmic Shore/Wire Party SO References")]
        public static void WireOnlinePlayersPanelReferences()
        {
            var connectionData = FindAsset<HostConnectionDataSO>();
            var profileIcons = FindAsset<SO_ProfileIconList>();

            // Wire OnlinePlayersPanel
            var onlinePanelPath = $"{PrefabFolder}/OnlinePlayersPanel.prefab";
            var onlinePanelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(onlinePanelPath);
            if (onlinePanelPrefab != null)
            {
                var panel = onlinePanelPrefab.GetComponent<OnlinePlayersPanel>();
                if (panel != null)
                {
                    var so = new SerializedObject(panel);
                    WireIfNull(so, "connectionData", connectionData);
                    WireIfNull(so, "profileIcons", profileIcons);

                    // Wire playerEntryPrefab
                    var entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        $"{PrefabFolder}/OnlinePlayerEntry.prefab");
                    WireIfNull(so, "playerEntryPrefab", entryPrefab);

                    // Wire entryContainer to Content child
                    var contentTransform = onlinePanelPrefab.transform.Find("Content");
                    if (contentTransform != null)
                        WireIfNull(so, "entryContainer", contentTransform);

                    // Wire closeButton
                    var closeBtn = onlinePanelPrefab.transform.Find("CloseButton");
                    if (closeBtn != null)
                        WireIfNull(so, "closeButton", closeBtn.GetComponent<Button>());

                    // Wire emptyStateLabel
                    var emptyLabel = onlinePanelPrefab.transform.Find("EmptyLabel");
                    if (emptyLabel != null)
                        WireIfNull(so, "emptyStateLabel", emptyLabel.gameObject);

                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(onlinePanelPrefab);
                    Debug.Log("[PartyPrefabSetup] Wired OnlinePlayersPanel SO references.");
                }
            }

            AssetDatabase.SaveAssets();
        }

        // ── Create Party Prefabs ──────────────────────────────────────────

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs")]
        public static void CreateAllPartyPrefabs()
        {
            if (!AssetDatabase.IsValidFolder(PrefabFolder))
                AssetDatabase.CreateFolder("Assets/_Prefabs/UI Elements/Panels", "Party");

            CreateOnlinePlayerEntryPrefab();
            CreateOnlinePlayersPanelPrefab();
            CreatePartySlotViewPrefab();
            CreatePartyInviteNotificationPrefab();
            CreateInviteNotificationUIPrefab();
            CreatePartyAreaPanelPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PartyPrefabSetup] Party prefabs created.");
        }

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs/Online Player Entry")]
        static void CreateOnlinePlayerEntryPrefab()
        {
            string path = $"{PrefabFolder}/OnlinePlayerEntry.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyPrefabSetup] Skipped — {path} already exists.");
                return;
            }

            var root = CreateUIRoot("OnlinePlayerEntry", 400, 60);

            // Background
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);

            // Avatar
            var avatarGo = CreateChildImage(root.transform, "Avatar", 50, 50, new Vector2(30, 0));
            var avatarImage = avatarGo.GetComponent<Image>();

            // Display Name
            var nameGo = CreateChildText(root.transform, "DisplayName", "Player Name", 16,
                new Vector2(70, 0), new Vector2(180, 40));

            // Invite Button (+)
            var inviteBtnGo = CreateChildButton(root.transform, "InviteButton", "+", 40, 40,
                new Vector2(150, 0));

            // Invite Sent Indicator
            var sentGo = CreateChildText(root.transform, "InviteSentIndicator", "Sent", 12,
                new Vector2(150, 0), new Vector2(40, 20));
            sentGo.SetActive(false);

            // Add Friend Button
            var friendBtnGo = CreateChildButton(root.transform, "AddFriendButton", "Add", 50, 30,
                new Vector2(185, 0));

            // Friend Request Sent Indicator
            var friendSentGo = CreateChildText(root.transform, "FriendRequestSentIndicator", "Sent", 12,
                new Vector2(185, 0), new Vector2(50, 20));
            friendSentGo.SetActive(false);

            // Add component and wire
            var entry = root.AddComponent<OnlinePlayerEntry>();
            var so = new SerializedObject(entry);
            so.FindProperty("avatarImage").objectReferenceValue = avatarImage;
            so.FindProperty("displayNameText").objectReferenceValue = nameGo.GetComponent<TMP_Text>();
            so.FindProperty("inviteButton").objectReferenceValue = inviteBtnGo.GetComponent<Button>();
            so.FindProperty("inviteSentIndicator").objectReferenceValue = sentGo;
            so.FindProperty("addFriendButton").objectReferenceValue = friendBtnGo.GetComponent<Button>();
            so.FindProperty("friendRequestSentIndicator").objectReferenceValue = friendSentGo;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[PartyPrefabSetup] Created {path}");
        }

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs/Online Players Panel")]
        static void CreateOnlinePlayersPanelPrefab()
        {
            string path = $"{PrefabFolder}/OnlinePlayersPanel.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyPrefabSetup] Skipped — {path} already exists.");
                return;
            }

            var root = CreateUIRoot("OnlinePlayersPanel", 420, 500);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.15f, 0.95f);

            // Header
            CreateChildText(root.transform, "Header", "Online Players", 20,
                new Vector2(0, 220), new Vector2(380, 40));

            // Close button
            var closeBtnGo = CreateChildButton(root.transform, "CloseButton", "X", 30, 30,
                new Vector2(180, 220));

            // Scroll content container
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(root.transform, false);
            var contentRT = contentGo.AddComponent<RectTransform>();
            contentRT.anchoredPosition = new Vector2(0, -20);
            contentRT.sizeDelta = new Vector2(400, 400);
            var vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Empty state label
            var emptyGo = CreateChildText(root.transform, "EmptyLabel", "No players online", 14,
                new Vector2(0, 0), new Vector2(300, 40));
            emptyGo.SetActive(false);

            // Add OnlinePlayersPanel component and wire internal refs
            var panel = root.AddComponent<OnlinePlayersPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("entryContainer").objectReferenceValue = contentGo.transform;
            so.FindProperty("closeButton").objectReferenceValue = closeBtnGo.GetComponent<Button>();
            so.FindProperty("emptyStateLabel").objectReferenceValue = emptyGo;

            // Wire SO references
            var connectionData = FindAsset<HostConnectionDataSO>();
            var profileIcons = FindAsset<SO_ProfileIconList>();
            if (connectionData != null)
                so.FindProperty("connectionData").objectReferenceValue = connectionData;
            if (profileIcons != null)
                so.FindProperty("profileIcons").objectReferenceValue = profileIcons;

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[PartyPrefabSetup] Created {path}");
        }

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs/Party Slot View")]
        static void CreatePartySlotViewPrefab()
        {
            string path = $"{PrefabFolder}/PartySlotView.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyPrefabSetup] Skipped — {path} already exists.");
                return;
            }

            var root = CreateUIRoot("PartySlotView", 80, 80);

            // Occupied state root
            var occupiedGo = new GameObject("OccupiedRoot");
            occupiedGo.transform.SetParent(root.transform, false);
            var occupiedRT = occupiedGo.AddComponent<RectTransform>();
            occupiedRT.sizeDelta = new Vector2(80, 80);

            var avatarGo = CreateChildImage(occupiedGo.transform, "Avatar", 60, 60, Vector2.zero);
            var nameGo = CreateChildText(occupiedGo.transform, "DisplayName", "Name", 10,
                new Vector2(0, -35), new Vector2(80, 16));

            // Empty state root
            var emptyGo = new GameObject("EmptyRoot");
            emptyGo.transform.SetParent(root.transform, false);
            var emptyRT = emptyGo.AddComponent<RectTransform>();
            emptyRT.sizeDelta = new Vector2(80, 80);

            var addBtnGo = CreateChildButton(emptyGo.transform, "AddButton", "+", 60, 60, Vector2.zero);

            // Add component and wire
            var slot = root.AddComponent<PartySlotView>();
            var so = new SerializedObject(slot);
            so.FindProperty("occupiedRoot").objectReferenceValue = occupiedGo;
            so.FindProperty("avatarImage").objectReferenceValue = avatarGo.GetComponent<Image>();
            so.FindProperty("displayNameText").objectReferenceValue = nameGo.GetComponent<TMP_Text>();
            so.FindProperty("emptyRoot").objectReferenceValue = emptyGo;
            so.FindProperty("addButton").objectReferenceValue = addBtnGo.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[PartyPrefabSetup] Created {path}");
        }

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs/Party Invite Notification")]
        static void CreatePartyInviteNotificationPrefab()
        {
            string path = $"{PrefabFolder}/PartyInviteNotificationPanel.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyPrefabSetup] Skipped — {path} already exists.");
                return;
            }

            var root = CreateUIRoot("PartyInviteNotificationPanel", 350, 150);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

            var avatarGo = CreateChildImage(root.transform, "InviterAvatar", 50, 50,
                new Vector2(-130, 20));
            var nameGo = CreateChildText(root.transform, "InviterName", "Player invited you!",
                16, new Vector2(20, 20), new Vector2(220, 30));

            var acceptBtnGo = CreateChildButton(root.transform, "AcceptButton", "Accept", 100, 36,
                new Vector2(-50, -40));
            var declineBtnGo = CreateChildButton(root.transform, "DeclineButton", "Decline", 100, 36,
                new Vector2(60, -40));

            var panel = root.AddComponent<PartyInviteNotificationPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("inviterNameText").objectReferenceValue = nameGo.GetComponent<TMP_Text>();
            so.FindProperty("inviterAvatarImage").objectReferenceValue = avatarGo.GetComponent<Image>();
            so.FindProperty("acceptButton").objectReferenceValue = acceptBtnGo.GetComponent<Button>();
            so.FindProperty("declineButton").objectReferenceValue = declineBtnGo.GetComponent<Button>();
            so.FindProperty("panelRoot").objectReferenceValue = root;

            // Wire SO references
            var connectionData = FindAsset<HostConnectionDataSO>();
            if (connectionData != null)
                WireIfExists(so, "connectionData", connectionData);

            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[PartyPrefabSetup] Created {path}");
        }

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs/Invite Notification UI")]
        static void CreateInviteNotificationUIPrefab()
        {
            string path = $"{PrefabFolder}/InviteNotificationUI.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyPrefabSetup] Skipped — {path} already exists.");
                return;
            }

            var root = CreateUIRoot("InviteNotificationUI", 350, 120);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.2f, 0.95f);

            var avatarGo = CreateChildImage(root.transform, "HostAvatar", 50, 50,
                new Vector2(-130, 10));
            var nameGo = CreateChildText(root.transform, "HostName", "Player invited you!",
                16, new Vector2(20, 10), new Vector2(220, 30));

            var acceptBtnGo = CreateChildButton(root.transform, "AcceptButton", "Accept", 100, 36,
                new Vector2(-50, -30));
            var declineBtnGo = CreateChildButton(root.transform, "DeclineButton", "Decline", 100, 36,
                new Vector2(60, -30));

            var transitionGo = CreateChildText(root.transform, "TransitionIndicator", "Connecting...",
                12, new Vector2(0, -30), new Vector2(200, 20));
            transitionGo.SetActive(false);

            var ui = root.AddComponent<InviteNotificationUI>();
            var so = new SerializedObject(ui);
            so.FindProperty("hostAvatarImage").objectReferenceValue = avatarGo.GetComponent<Image>();
            so.FindProperty("hostNameText").objectReferenceValue = nameGo.GetComponent<TMP_Text>();
            so.FindProperty("acceptButton").objectReferenceValue = acceptBtnGo.GetComponent<Button>();
            so.FindProperty("declineButton").objectReferenceValue = declineBtnGo.GetComponent<Button>();
            so.FindProperty("transitionIndicator").objectReferenceValue = transitionGo;

            // Wire SO references
            var connectionData = FindAsset<HostConnectionDataSO>();
            if (connectionData != null)
                WireIfExists(so, "connectionData", connectionData);

            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[PartyPrefabSetup] Created {path}");
        }

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs/Party Area Panel")]
        static void CreatePartyAreaPanelPrefab()
        {
            string path = $"{PrefabFolder}/PartyAreaPanel.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyPrefabSetup] Skipped — {path} already exists.");
                return;
            }

            var root = CreateUIRoot("PartyAreaPanel", 300, 100);

            // Horizontal layout for slots
            var slotsGo = new GameObject("Slots");
            slotsGo.transform.SetParent(root.transform, false);
            var slotsRT = slotsGo.AddComponent<RectTransform>();
            slotsRT.sizeDelta = new Vector2(300, 80);
            var hlg = slotsGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[PartyPrefabSetup] Created {path} — add PartySlotView children and PartyAreaPanel component in scene.");
        }

        // ── Helpers ────────────────────────────────────────────────────

        static T FindAsset<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}");
            if (guids.Length == 0) return null;
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(path);
        }

        static void WireIfNull(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null && prop.objectReferenceValue == null && value != null)
            {
                prop.objectReferenceValue = value;
            }
        }

        static void WireIfExists(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null && value != null)
            {
                prop.objectReferenceValue = value;
            }
        }

        static GameObject CreateUIRoot(string name, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, height);
            return go;
        }

        static GameObject CreateChildImage(Transform parent, string name, float w, float h, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = new Color(0.3f, 0.3f, 0.4f, 1f);
            return go;
        }

        static GameObject CreateChildText(Transform parent, string name, string text, int fontSize,
            Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return go;
        }

        static GameObject CreateChildButton(Transform parent, string name, string label,
            float w, float h, Vector2 pos)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = pos;
            go.GetComponent<Image>().color = new Color(0.2f, 0.6f, 1f, 1f);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRT = labelGo.GetComponent<RectTransform>();
            labelRT.sizeDelta = new Vector2(w, h);
            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 14;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return go;
        }
    }
}
