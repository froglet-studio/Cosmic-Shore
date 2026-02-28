using CosmicShore.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
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

            CreateFriendEntryViewPrefab();
            CreateFriendRequestEntryViewPrefab();
            CreateAddFriendPanelPrefab();
            CreateFriendsPanelPrefab();
            CreateOnlinePlayerEntryPrefab();
            CreateOnlinePlayersPanelPrefab();
            CreateFriendsPanelPrefab();
            CreatePartyInviteNotificationPrefab();
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
                "OnlinePlayerEntry", "OnlinePlayersPanel",
                "PartyInviteNotificationPanel", "PartyAreaPanel",
                "FriendEntryView", "FriendRequestEntryView",
                "AddFriendPanel", "FriendsPanel"
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

        [MenuItem("Tools/Cosmic Shore/Rebuild Friends Panel Prefab")]
        public static void RebuildFriendsPanelPrefab()
        {
            string path = $"{PrefabFolder}/FriendsPanel.prefab";
            var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existingPrefab != null)
            {
                AssetDatabase.DeleteAsset(path);
                Debug.Log($"[PartyPrefabSetup] Deleted existing {path} for rebuild.");
            }

            // Also rebuild entry prefabs if missing
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/FriendEntryView.prefab") == null)
                CreateFriendEntryViewPrefab();
            if (AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/FriendRequestEntryView.prefab") == null)
                CreateFriendRequestEntryViewPrefab();

            CreateFriendsPanelPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PartyPrefabSetup] FriendsPanel prefab rebuilt with all references wired.");
        }

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

            CreateFriendEntryViewPrefab();
            CreateFriendRequestEntryViewPrefab();
            CreateAddFriendPanelPrefab();
            CreateFriendsPanelPrefab();
            CreateOnlinePlayerEntryPrefab();
            CreateOnlinePlayersPanelPrefab();
            CreatePartyInviteNotificationPrefab();
            CreatePartyAreaPanelPrefab();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PartyPrefabSetup] Party prefabs created.");
        }

        // ── Friends Prefabs ──────────────────────────────────────────────

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs/Friend Entry View")]
        static void CreateFriendEntryViewPrefab()
        {
            string path = $"{PrefabFolder}/FriendEntryView.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyPrefabSetup] Skipped — {path} already exists.");
                return;
            }

            var root = CreateUIRoot("FriendEntryView", 400, 60);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Online indicator (small circle)
            var indicatorGo = CreateChildImage(root.transform, "OnlineIndicator", 12, 12, Vector2.zero);
            var indicatorImage = indicatorGo.GetComponent<Image>();
            indicatorImage.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            // Display name
            var nameGo = CreateChildText(root.transform, "DisplayName", "Friend Name", 16,
                Vector2.zero, new Vector2(140, 40));
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            // Status text
            var statusGo = CreateChildText(root.transform, "StatusText", "Offline", 12,
                Vector2.zero, new Vector2(60, 30));
            var statusText = statusGo.GetComponent<TMP_Text>();
            statusText.color = new Color(0.7f, 0.7f, 0.7f);

            // Invite button
            var inviteBtnGo = CreateChildButton(root.transform, "InviteButton", "Invite", 60, 30, Vector2.zero);

            // Invite sent indicator
            var sentGo = CreateChildText(root.transform, "InviteSentIndicator", "Sent", 12,
                Vector2.zero, new Vector2(40, 20));
            sentGo.SetActive(false);

            // Remove button
            var removeBtnGo = CreateChildButton(root.transform, "RemoveButton", "X", 30, 30, Vector2.zero);
            removeBtnGo.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 1f);

            // Add component and wire
            var entry = root.AddComponent<FriendEntryView>();
            var so = new SerializedObject(entry);
            so.FindProperty("displayNameText").objectReferenceValue = nameGo.GetComponent<TMP_Text>();
            so.FindProperty("statusText").objectReferenceValue = statusText;
            so.FindProperty("onlineIndicator").objectReferenceValue = indicatorImage;
            so.FindProperty("inviteButton").objectReferenceValue = inviteBtnGo.GetComponent<Button>();
            so.FindProperty("removeButton").objectReferenceValue = removeBtnGo.GetComponent<Button>();
            so.FindProperty("inviteSentIndicator").objectReferenceValue = sentGo;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[PartyPrefabSetup] Created {path}");
        }

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs/Friend Request Entry View")]
        static void CreateFriendRequestEntryViewPrefab()
        {
            string path = $"{PrefabFolder}/FriendRequestEntryView.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyPrefabSetup] Skipped — {path} already exists.");
                return;
            }

            var root = CreateUIRoot("FriendRequestEntryView", 400, 60);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);
            var hlg = root.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Display name
            var nameGo = CreateChildText(root.transform, "DisplayName", "Player Name", 16,
                Vector2.zero, new Vector2(140, 40));
            var nameLE = nameGo.AddComponent<LayoutElement>();
            nameLE.flexibleWidth = 1;

            // Direction label (Incoming / Sent)
            var dirGo = CreateChildText(root.transform, "DirectionLabel", "Incoming", 12,
                Vector2.zero, new Vector2(60, 30));
            var dirText = dirGo.GetComponent<TMP_Text>();
            dirText.color = new Color(0.6f, 0.8f, 1f);

            // Accept button
            var acceptBtnGo = CreateChildButton(root.transform, "AcceptButton", "Accept", 60, 30, Vector2.zero);
            acceptBtnGo.GetComponent<Image>().color = new Color(0.2f, 0.7f, 0.3f, 1f);

            // Decline button
            var declineBtnGo = CreateChildButton(root.transform, "DeclineButton", "Decline", 60, 30, Vector2.zero);
            declineBtnGo.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 1f);

            // Cancel button (for outgoing requests)
            var cancelBtnGo = CreateChildButton(root.transform, "CancelButton", "Cancel", 60, 30, Vector2.zero);
            cancelBtnGo.GetComponent<Image>().color = new Color(0.6f, 0.3f, 0.1f, 1f);
            cancelBtnGo.SetActive(false);

            // Add component and wire
            var entry = root.AddComponent<FriendRequestEntryView>();
            var so = new SerializedObject(entry);
            so.FindProperty("displayNameText").objectReferenceValue = nameGo.GetComponent<TMP_Text>();
            so.FindProperty("directionLabel").objectReferenceValue = dirText;
            so.FindProperty("acceptButton").objectReferenceValue = acceptBtnGo.GetComponent<Button>();
            so.FindProperty("declineButton").objectReferenceValue = declineBtnGo.GetComponent<Button>();
            so.FindProperty("cancelButton").objectReferenceValue = cancelBtnGo.GetComponent<Button>();
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[PartyPrefabSetup] Created {path}");
        }

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs/Add Friend Panel")]
        static void CreateAddFriendPanelPrefab()
        {
            string path = $"{PrefabFolder}/AddFriendPanel.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyPrefabSetup] Skipped — {path} already exists.");
                return;
            }

            var root = CreateUIRoot("AddFriendPanel", 380, 180);

            // Input row
            var inputRow = new GameObject("InputRow", typeof(RectTransform));
            inputRow.transform.SetParent(root.transform, false);
            var inputRowRT = inputRow.GetComponent<RectTransform>();
            inputRowRT.anchoredPosition = new Vector2(0, 30);
            inputRowRT.sizeDelta = new Vector2(360, 40);
            var inputHlg = inputRow.AddComponent<HorizontalLayoutGroup>();
            inputHlg.spacing = 8;
            inputHlg.childForceExpandWidth = false;
            inputHlg.childForceExpandHeight = true;

            // TMP_InputField
            var inputFieldGo = new GameObject("NameInputField", typeof(RectTransform), typeof(Image));
            inputFieldGo.transform.SetParent(inputRow.transform, false);
            inputFieldGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 1f);
            var inputFieldLE = inputFieldGo.AddComponent<LayoutElement>();
            inputFieldLE.flexibleWidth = 1;
            inputFieldLE.preferredHeight = 40;

            // Text Area child for TMP_InputField
            var textAreaGo = new GameObject("Text Area", typeof(RectTransform));
            textAreaGo.transform.SetParent(inputFieldGo.transform, false);
            var textAreaRT = textAreaGo.GetComponent<RectTransform>();
            textAreaRT.anchorMin = Vector2.zero;
            textAreaRT.anchorMax = Vector2.one;
            textAreaRT.offsetMin = new Vector2(10, 0);
            textAreaRT.offsetMax = new Vector2(-10, 0);

            // Placeholder
            var placeholderGo = new GameObject("Placeholder", typeof(RectTransform));
            placeholderGo.transform.SetParent(textAreaGo.transform, false);
            var placeholderRT = placeholderGo.GetComponent<RectTransform>();
            placeholderRT.anchorMin = Vector2.zero;
            placeholderRT.anchorMax = Vector2.one;
            placeholderRT.offsetMin = Vector2.zero;
            placeholderRT.offsetMax = Vector2.zero;
            var placeholderTmp = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderTmp.text = "Enter player name...";
            placeholderTmp.fontSize = 14;
            placeholderTmp.fontStyle = FontStyles.Italic;
            placeholderTmp.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            placeholderTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Input Text
            var inputTextGo = new GameObject("Text", typeof(RectTransform));
            inputTextGo.transform.SetParent(textAreaGo.transform, false);
            var inputTextRT = inputTextGo.GetComponent<RectTransform>();
            inputTextRT.anchorMin = Vector2.zero;
            inputTextRT.anchorMax = Vector2.one;
            inputTextRT.offsetMin = Vector2.zero;
            inputTextRT.offsetMax = Vector2.zero;
            var inputTextTmp = inputTextGo.AddComponent<TextMeshProUGUI>();
            inputTextTmp.fontSize = 14;
            inputTextTmp.color = Color.white;
            inputTextTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var inputField = inputFieldGo.AddComponent<TMP_InputField>();
            inputField.textViewport = textAreaRT;
            inputField.textComponent = inputTextTmp;
            inputField.placeholder = placeholderTmp;
            inputField.fontAsset = inputTextTmp.font;

            // Send button
            var sendBtnGo = CreateChildButton(inputRow.transform, "SendButton", "Send", 70, 40, Vector2.zero);
            sendBtnGo.GetComponent<Image>().color = new Color(0.2f, 0.6f, 1f, 1f);

            // Feedback text
            var feedbackGo = CreateChildText(root.transform, "FeedbackText", "", 13,
                new Vector2(0, -20), new Vector2(360, 30));
            var feedbackText = feedbackGo.GetComponent<TMP_Text>();
            feedbackText.color = new Color(0.2f, 0.9f, 0.3f);

            // Close button (top-right)
            var closeBtnGo = CreateChildButton(root.transform, "CloseButton", "X", 30, 30,
                new Vector2(165, 70));
            closeBtnGo.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 1f);

            // Add component and wire
            var panel = root.AddComponent<AddFriendPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("nameInputField").objectReferenceValue = inputField;
            so.FindProperty("sendButton").objectReferenceValue = sendBtnGo.GetComponent<Button>();
            so.FindProperty("closeButton").objectReferenceValue = closeBtnGo.GetComponent<Button>();
            so.FindProperty("feedbackText").objectReferenceValue = feedbackText;

            // Wire FriendsDataSO if found
            var friendsData = FindAsset<FriendsDataSO>();
            WireIfExists(so, "friendsData", friendsData);

            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[PartyPrefabSetup] Created {path}");
        }

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs/Friends Panel")]
        static void CreateFriendsPanelPrefab()
        {
            string path = $"{PrefabFolder}/FriendsPanel.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                Debug.Log($"[PartyPrefabSetup] Skipped — {path} already exists.");
                return;
            }

            var root = CreateUIRoot("FriendsPanel", 420, 550);
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            var rootVlg = root.AddComponent<VerticalLayoutGroup>();
            rootVlg.spacing = 4;
            rootVlg.padding = new RectOffset(8, 8, 8, 8);
            rootVlg.childForceExpandWidth = true;
            rootVlg.childForceExpandHeight = false;

            // ── Header Bar ──
            var headerBar = new GameObject("HeaderBar", typeof(RectTransform));
            headerBar.transform.SetParent(root.transform, false);
            var headerBarLE = headerBar.AddComponent<LayoutElement>();
            headerBarLE.preferredHeight = 40;
            var headerBarHlg = headerBar.AddComponent<HorizontalLayoutGroup>();
            headerBarHlg.spacing = 8;
            headerBarHlg.padding = new RectOffset(4, 4, 4, 4);
            headerBarHlg.childAlignment = TextAnchor.MiddleLeft;
            headerBarHlg.childForceExpandWidth = false;
            headerBarHlg.childForceExpandHeight = false;

            // Header text
            var headerTextGo = CreateChildText(headerBar.transform, "HeaderText", "Friends", 20,
                Vector2.zero, new Vector2(200, 36));
            var headerTextLE = headerTextGo.AddComponent<LayoutElement>();
            headerTextLE.flexibleWidth = 1;
            var headerTmp = headerTextGo.GetComponent<TMP_Text>();
            headerTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Requests badge
            var badgeGo = CreateChildText(headerBar.transform, "RequestsBadge", "0", 12,
                Vector2.zero, new Vector2(24, 24));
            var badgeBg = badgeGo.AddComponent<Image>();
            badgeBg.color = new Color(0.9f, 0.2f, 0.2f, 1f);
            var badgeTmp = badgeGo.GetComponent<TMP_Text>();
            badgeTmp.alignment = TextAlignmentOptions.Center;
            var badgeLE = badgeGo.AddComponent<LayoutElement>();
            badgeLE.preferredWidth = 24;
            badgeLE.preferredHeight = 24;
            badgeGo.SetActive(false);

            // Refresh button
            var refreshBtnGo = CreateChildButton(headerBar.transform, "RefreshButton", "Refresh", 70, 30, Vector2.zero);
            refreshBtnGo.GetComponent<Image>().color = new Color(0.3f, 0.5f, 0.7f, 1f);

            // Close button
            var closeBtnGo = CreateChildButton(headerBar.transform, "CloseButton", "X", 30, 30, Vector2.zero);
            closeBtnGo.GetComponent<Image>().color = new Color(0.4f, 0.4f, 0.4f, 1f);

            // ── Tab Bar ──
            var tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(root.transform, false);
            var tabBarLE = tabBar.AddComponent<LayoutElement>();
            tabBarLE.preferredHeight = 36;
            var tabBarHlg = tabBar.AddComponent<HorizontalLayoutGroup>();
            tabBarHlg.spacing = 4;
            tabBarHlg.childForceExpandWidth = true;
            tabBarHlg.childForceExpandHeight = true;

            var friendsTabGo = CreateChildButton(tabBar.transform, "FriendsTabButton", "Friends", 100, 32, Vector2.zero);
            friendsTabGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.3f);

            var requestsTabGo = CreateChildButton(tabBar.transform, "RequestsTabButton", "Requests", 100, 32, Vector2.zero);
            requestsTabGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);

            var addFriendTabGo = CreateChildButton(tabBar.transform, "AddFriendTabButton", "Add Friend", 100, 32, Vector2.zero);
            addFriendTabGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);

            // ── Friends List Content ──
            var friendsListGo = new GameObject("FriendsListContent", typeof(RectTransform));
            friendsListGo.transform.SetParent(root.transform, false);
            var friendsListLE = friendsListGo.AddComponent<LayoutElement>();
            friendsListLE.flexibleHeight = 1;

            // Scroll view for friends
            var friendsScrollGo = new GameObject("FriendsContainer", typeof(RectTransform));
            friendsScrollGo.transform.SetParent(friendsListGo.transform, false);
            var friendsScrollRT = friendsScrollGo.GetComponent<RectTransform>();
            friendsScrollRT.anchorMin = Vector2.zero;
            friendsScrollRT.anchorMax = Vector2.one;
            friendsScrollRT.offsetMin = Vector2.zero;
            friendsScrollRT.offsetMax = Vector2.zero;
            var friendsVlg = friendsScrollGo.AddComponent<VerticalLayoutGroup>();
            friendsVlg.spacing = 4;
            friendsVlg.childForceExpandWidth = true;
            friendsVlg.childForceExpandHeight = false;
            var friendsCSF = friendsScrollGo.AddComponent<ContentSizeFitter>();
            friendsCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Friends empty state
            var friendsEmptyGo = CreateChildText(friendsListGo.transform, "FriendsEmptyState",
                "No friends yet.\nAdd friends to see them here!", 14,
                Vector2.zero, new Vector2(300, 60));
            var friendsEmptyRT = friendsEmptyGo.GetComponent<RectTransform>();
            friendsEmptyRT.anchorMin = new Vector2(0.5f, 0.5f);
            friendsEmptyRT.anchorMax = new Vector2(0.5f, 0.5f);
            var friendsEmptyText = friendsEmptyGo.GetComponent<TMP_Text>();
            friendsEmptyText.color = new Color(0.6f, 0.6f, 0.6f);
            friendsEmptyGo.SetActive(false);

            // ── Requests List Content ──
            var requestsListGo = new GameObject("RequestsListContent", typeof(RectTransform));
            requestsListGo.transform.SetParent(root.transform, false);
            var requestsListLE = requestsListGo.AddComponent<LayoutElement>();
            requestsListLE.flexibleHeight = 1;
            requestsListGo.SetActive(false);

            // Scroll view for requests
            var requestsScrollGo = new GameObject("RequestsContainer", typeof(RectTransform));
            requestsScrollGo.transform.SetParent(requestsListGo.transform, false);
            var requestsScrollRT = requestsScrollGo.GetComponent<RectTransform>();
            requestsScrollRT.anchorMin = Vector2.zero;
            requestsScrollRT.anchorMax = Vector2.one;
            requestsScrollRT.offsetMin = Vector2.zero;
            requestsScrollRT.offsetMax = Vector2.zero;
            var requestsVlg = requestsScrollGo.AddComponent<VerticalLayoutGroup>();
            requestsVlg.spacing = 4;
            requestsVlg.childForceExpandWidth = true;
            requestsVlg.childForceExpandHeight = false;
            var requestsCSF = requestsScrollGo.AddComponent<ContentSizeFitter>();
            requestsCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Requests empty state
            var requestsEmptyGo = CreateChildText(requestsListGo.transform, "RequestsEmptyState",
                "No pending requests.", 14,
                Vector2.zero, new Vector2(300, 40));
            var requestsEmptyRT = requestsEmptyGo.GetComponent<RectTransform>();
            requestsEmptyRT.anchorMin = new Vector2(0.5f, 0.5f);
            requestsEmptyRT.anchorMax = new Vector2(0.5f, 0.5f);
            var requestsEmptyText = requestsEmptyGo.GetComponent<TMP_Text>();
            requestsEmptyText.color = new Color(0.6f, 0.6f, 0.6f);
            requestsEmptyGo.SetActive(false);

            // ── Add Friend Content (embedded AddFriendPanel) ──
            var addFriendGo = new GameObject("AddFriendContent", typeof(RectTransform));
            addFriendGo.transform.SetParent(root.transform, false);
            var addFriendLE = addFriendGo.AddComponent<LayoutElement>();
            addFriendLE.flexibleHeight = 1;
            addFriendGo.SetActive(false);

            // Input row inside add friend content
            var addInputRow = new GameObject("InputRow", typeof(RectTransform));
            addInputRow.transform.SetParent(addFriendGo.transform, false);
            var addInputRowRT = addInputRow.GetComponent<RectTransform>();
            addInputRowRT.anchoredPosition = new Vector2(0, 30);
            addInputRowRT.sizeDelta = new Vector2(380, 40);
            var addInputHlg = addInputRow.AddComponent<HorizontalLayoutGroup>();
            addInputHlg.spacing = 8;
            addInputHlg.childForceExpandWidth = false;
            addInputHlg.childForceExpandHeight = true;

            // TMP_InputField for add friend
            var addInputFieldGo = new GameObject("NameInputField", typeof(RectTransform), typeof(Image));
            addInputFieldGo.transform.SetParent(addInputRow.transform, false);
            addInputFieldGo.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.15f, 1f);
            var addInputFieldLE = addInputFieldGo.AddComponent<LayoutElement>();
            addInputFieldLE.flexibleWidth = 1;
            addInputFieldLE.preferredHeight = 40;

            var addTextArea = new GameObject("Text Area", typeof(RectTransform));
            addTextArea.transform.SetParent(addInputFieldGo.transform, false);
            var addTextAreaRT = addTextArea.GetComponent<RectTransform>();
            addTextAreaRT.anchorMin = Vector2.zero;
            addTextAreaRT.anchorMax = Vector2.one;
            addTextAreaRT.offsetMin = new Vector2(10, 0);
            addTextAreaRT.offsetMax = new Vector2(-10, 0);

            var addPlaceholderGo = new GameObject("Placeholder", typeof(RectTransform));
            addPlaceholderGo.transform.SetParent(addTextArea.transform, false);
            var addPlaceholderRT = addPlaceholderGo.GetComponent<RectTransform>();
            addPlaceholderRT.anchorMin = Vector2.zero;
            addPlaceholderRT.anchorMax = Vector2.one;
            addPlaceholderRT.offsetMin = Vector2.zero;
            addPlaceholderRT.offsetMax = Vector2.zero;
            var addPlaceholderTmp = addPlaceholderGo.AddComponent<TextMeshProUGUI>();
            addPlaceholderTmp.text = "Enter player name...";
            addPlaceholderTmp.fontSize = 14;
            addPlaceholderTmp.fontStyle = FontStyles.Italic;
            addPlaceholderTmp.color = new Color(0.5f, 0.5f, 0.5f, 0.7f);
            addPlaceholderTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var addInputTextGo = new GameObject("Text", typeof(RectTransform));
            addInputTextGo.transform.SetParent(addTextArea.transform, false);
            var addInputTextRT = addInputTextGo.GetComponent<RectTransform>();
            addInputTextRT.anchorMin = Vector2.zero;
            addInputTextRT.anchorMax = Vector2.one;
            addInputTextRT.offsetMin = Vector2.zero;
            addInputTextRT.offsetMax = Vector2.zero;
            var addInputTextTmp = addInputTextGo.AddComponent<TextMeshProUGUI>();
            addInputTextTmp.fontSize = 14;
            addInputTextTmp.color = Color.white;
            addInputTextTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var addInputField = addInputFieldGo.AddComponent<TMP_InputField>();
            addInputField.textViewport = addTextAreaRT;
            addInputField.textComponent = addInputTextTmp;
            addInputField.placeholder = addPlaceholderTmp;
            addInputField.fontAsset = addInputTextTmp.font;

            var addSendBtnGo = CreateChildButton(addInputRow.transform, "SendButton", "Send", 70, 40, Vector2.zero);
            addSendBtnGo.GetComponent<Image>().color = new Color(0.2f, 0.6f, 1f, 1f);

            var addFeedbackGo = CreateChildText(addFriendGo.transform, "FeedbackText", "", 13,
                new Vector2(0, -20), new Vector2(360, 30));
            var addFeedbackText = addFeedbackGo.GetComponent<TMP_Text>();
            addFeedbackText.color = new Color(0.2f, 0.9f, 0.3f);

            // Attach AddFriendPanel component to the add friend content
            var addFriendPanel = addFriendGo.AddComponent<AddFriendPanel>();
            var addFriendSO = new SerializedObject(addFriendPanel);
            addFriendSO.FindProperty("nameInputField").objectReferenceValue = addInputField;
            addFriendSO.FindProperty("sendButton").objectReferenceValue = addSendBtnGo.GetComponent<Button>();
            addFriendSO.FindProperty("feedbackText").objectReferenceValue = addFeedbackText;
            var friendsData = FindAsset<FriendsDataSO>();
            WireIfExists(addFriendSO, "friendsData", friendsData);
            addFriendSO.ApplyModifiedPropertiesWithoutUndo();

            // ── Wire FriendsPanel component ──
            var friendsPanel = root.AddComponent<FriendsPanel>();
            var so = new SerializedObject(friendsPanel);

            // SOAP Data
            WireIfExists(so, "friendsData", friendsData);
            var connectionData = FindAsset<HostConnectionDataSO>();
            WireIfExists(so, "connectionData", connectionData);

            // Tab Buttons
            so.FindProperty("friendsTabButton").objectReferenceValue = friendsTabGo.GetComponent<Button>();
            so.FindProperty("requestsTabButton").objectReferenceValue = requestsTabGo.GetComponent<Button>();
            so.FindProperty("addFriendTabButton").objectReferenceValue = addFriendTabGo.GetComponent<Button>();

            // Tab Content Panels
            so.FindProperty("friendsListContent").objectReferenceValue = friendsListGo;
            so.FindProperty("requestsListContent").objectReferenceValue = requestsListGo;
            so.FindProperty("addFriendContent").objectReferenceValue = addFriendPanel;

            // Friends List
            var friendEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/FriendEntryView.prefab");
            WireIfExists(so, "friendEntryPrefab", friendEntryPrefab);
            so.FindProperty("friendsContainer").objectReferenceValue = friendsScrollGo.transform;
            so.FindProperty("friendsEmptyState").objectReferenceValue = friendsEmptyGo;

            // Requests List
            var requestEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/FriendRequestEntryView.prefab");
            WireIfExists(so, "friendRequestEntryPrefab", requestEntryPrefab);
            so.FindProperty("requestsContainer").objectReferenceValue = requestsScrollGo.transform;
            so.FindProperty("requestsEmptyState").objectReferenceValue = requestsEmptyGo;

            // Header
            so.FindProperty("headerText").objectReferenceValue = headerTmp;
            so.FindProperty("requestsBadge").objectReferenceValue = badgeTmp;
            so.FindProperty("closeButton").objectReferenceValue = closeBtnGo.GetComponent<Button>();
            so.FindProperty("refreshButton").objectReferenceValue = refreshBtnGo.GetComponent<Button>();

            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[PartyPrefabSetup] Created {path}");
        }

        // ── Online / Party Prefabs ──────────────────────────────────────────

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

        [MenuItem("Tools/Cosmic Shore/Create Party Prefabs/Friends Panel")]
        static void CreateFriendsPanelPrefab()
        {
            string path = $"{PrefabFolder}/FriendsPanel.prefab";

            var root = CreateUIRoot("FriendsPanel", 420, 500);
            root.layer = 5; // UI layer
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);

            // Header bar
            var headerGo = CreateChildText(root.transform, "HeaderText", "Friends", 20,
                new Vector2(0, 220), new Vector2(300, 40));

            // Requests badge
            var badgeGo = CreateChildText(root.transform, "RequestsBadge", "", 12,
                new Vector2(120, 230), new Vector2(30, 24));
            badgeGo.SetActive(false);

            // Close button
            var closeBtnGo = CreateChildButton(root.transform, "CloseButton", "X", 30, 30,
                new Vector2(185, 220));

            // Refresh button
            var refreshBtnGo = CreateChildButton(root.transform, "RefreshButton", "\u21BB", 30, 30,
                new Vector2(145, 220));

            // Tab buttons bar
            var friendsTabGo = CreateChildButton(root.transform, "FriendsTabButton", "Friends", 120, 32,
                new Vector2(-130, 180));
            friendsTabGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.3f);

            var requestsTabGo = CreateChildButton(root.transform, "RequestsTabButton", "Requests", 120, 32,
                new Vector2(0, 180));
            requestsTabGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);

            var addFriendTabGo = CreateChildButton(root.transform, "AddFriendTabButton", "Add", 120, 32,
                new Vector2(130, 180));
            addFriendTabGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);

            // Friends List content panel
            var friendsListGo = new GameObject("FriendsListContent");
            friendsListGo.layer = 5;
            friendsListGo.transform.SetParent(root.transform, false);
            var friendsListRT = friendsListGo.AddComponent<RectTransform>();
            friendsListRT.anchoredPosition = new Vector2(0, -20);
            friendsListRT.sizeDelta = new Vector2(400, 350);

            // Friends container (scrollable content parent)
            var friendsContainerGo = new GameObject("FriendsContainer");
            friendsContainerGo.layer = 5;
            friendsContainerGo.transform.SetParent(friendsListGo.transform, false);
            var friendsContainerRT = friendsContainerGo.AddComponent<RectTransform>();
            friendsContainerRT.anchoredPosition = Vector2.zero;
            friendsContainerRT.sizeDelta = new Vector2(400, 350);
            var friendsVLG = friendsContainerGo.AddComponent<VerticalLayoutGroup>();
            friendsVLG.spacing = 4;
            friendsVLG.childForceExpandWidth = true;
            friendsVLG.childForceExpandHeight = false;

            // Friends empty state
            var friendsEmptyGo = CreateChildText(friendsListGo.transform, "FriendsEmptyState",
                "No friends yet", 14, Vector2.zero, new Vector2(300, 40));
            friendsEmptyGo.SetActive(false);

            // Requests List content panel
            var requestsListGo = new GameObject("RequestsListContent");
            requestsListGo.layer = 5;
            requestsListGo.transform.SetParent(root.transform, false);
            var requestsListRT = requestsListGo.AddComponent<RectTransform>();
            requestsListRT.anchoredPosition = new Vector2(0, -20);
            requestsListRT.sizeDelta = new Vector2(400, 350);
            requestsListGo.SetActive(false);

            // Requests container
            var requestsContainerGo = new GameObject("RequestsContainer");
            requestsContainerGo.layer = 5;
            requestsContainerGo.transform.SetParent(requestsListGo.transform, false);
            var requestsContainerRT = requestsContainerGo.AddComponent<RectTransform>();
            requestsContainerRT.anchoredPosition = Vector2.zero;
            requestsContainerRT.sizeDelta = new Vector2(400, 350);
            var requestsVLG = requestsContainerGo.AddComponent<VerticalLayoutGroup>();
            requestsVLG.spacing = 4;
            requestsVLG.childForceExpandWidth = true;
            requestsVLG.childForceExpandHeight = false;

            // Requests empty state
            var requestsEmptyGo = CreateChildText(requestsListGo.transform, "RequestsEmptyState",
                "No pending requests", 14, Vector2.zero, new Vector2(300, 40));
            requestsEmptyGo.SetActive(false);

            // Add Friend content panel (AddFriendPanel component)
            var addFriendGo = CreateUIRoot("AddFriendContent", 400, 350);
            addFriendGo.layer = 5;
            addFriendGo.transform.SetParent(root.transform, false);
            addFriendGo.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -20);
            addFriendGo.SetActive(false);
            var addFriendPanel = addFriendGo.AddComponent<AddFriendPanel>();

            // Add FriendsPanel component and wire
            var panel = root.AddComponent<FriendsPanel>();
            var so = new SerializedObject(panel);

            // Wire SO data
            var friendsData = FindAsset<FriendsDataSO>();
            var connectionData = FindAsset<HostConnectionDataSO>();
            WireIfExists(so, "friendsData", friendsData);
            WireIfExists(so, "connectionData", connectionData);

            // Wire tab buttons
            so.FindProperty("friendsTabButton").objectReferenceValue = friendsTabGo.GetComponent<Button>();
            so.FindProperty("requestsTabButton").objectReferenceValue = requestsTabGo.GetComponent<Button>();
            so.FindProperty("addFriendTabButton").objectReferenceValue = addFriendTabGo.GetComponent<Button>();

            // Wire content panels
            so.FindProperty("friendsListContent").objectReferenceValue = friendsListGo;
            so.FindProperty("requestsListContent").objectReferenceValue = requestsListGo;
            so.FindProperty("addFriendContent").objectReferenceValue = addFriendPanel;

            // Wire entry prefabs
            var friendEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/FriendEntryView.prefab");
            WireIfExists(so, "friendEntryPrefab", friendEntryPrefab);
            var friendRequestEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/FriendRequestEntryView.prefab");
            WireIfExists(so, "friendRequestEntryPrefab", friendRequestEntryPrefab);

            // Wire containers
            so.FindProperty("friendsContainer").objectReferenceValue = friendsContainerGo.transform;
            so.FindProperty("friendsEmptyState").objectReferenceValue = friendsEmptyGo;
            so.FindProperty("requestsContainer").objectReferenceValue = requestsContainerGo.transform;
            so.FindProperty("requestsEmptyState").objectReferenceValue = requestsEmptyGo;

            // Wire header
            so.FindProperty("headerText").objectReferenceValue = headerGo.GetComponent<TMP_Text>();
            so.FindProperty("requestsBadge").objectReferenceValue = badgeGo.GetComponent<TMP_Text>();
            so.FindProperty("closeButton").objectReferenceValue = closeBtnGo.GetComponent<Button>();
            so.FindProperty("refreshButton").objectReferenceValue = refreshBtnGo.GetComponent<Button>();

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

        // ── Scene Wiring (Menu_Main) ──────────────────────────────────

        [MenuItem("Tools/Cosmic Shore/Wire Party Scene References")]
        public static void WirePartySceneReferences()
        {
            var connectionData = FindAsset<HostConnectionDataSO>();
            var friendsData = FindAsset<FriendsDataSO>();
            var profileIcons = FindAsset<SO_ProfileIconList>();

            int wired = 0;

            // ── OnlinePlayersPanel ───────────────────────────────────────
            var onlinePanelGo = FindInScene<OnlinePlayersPanel>();
            if (onlinePanelGo != null)
            {
                wired += WireOnlinePlayersPanelScene(onlinePanelGo, connectionData, profileIcons);
            }
            else
                Debug.LogWarning("[PartySceneWiring] OnlinePlayersPanel not found in scene.");

            // ── FriendsPanel ─────────────────────────────────────────────
            var friendsPanelGo = FindInScene<FriendsPanel>();
            if (friendsPanelGo != null)
            {
                wired += WireFriendsPanelScene(friendsPanelGo, friendsData, connectionData);
            }
            else
                Debug.LogWarning("[PartySceneWiring] FriendsPanel not found in scene.");

            // ── PartyInviteNotificationPanel ─────────────────────────────
            var invitePanelGo = FindInScene<PartyInviteNotificationPanel>();
            if (invitePanelGo != null)
            {
                wired += WireInviteNotificationPanelScene(invitePanelGo, connectionData, profileIcons);
            }
            else
                Debug.LogWarning("[PartySceneWiring] PartyInviteNotificationPanel not found in scene.");

            // ── PartyArcadeView ──────────────────────────────────────────
            var partyArcadeView = FindInScene<PartyArcadeView>();
            if (partyArcadeView != null)
            {
                wired += WirePartyArcadeViewScene(partyArcadeView, onlinePanelGo, friendsPanelGo,
                    invitePanelGo, connectionData, friendsData, profileIcons);
            }
            else
                Debug.LogWarning("[PartySceneWiring] PartyArcadeView not found in scene.");

            if (wired > 0)
            {
                EditorSceneManager.MarkSceneDirty(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }

            Debug.Log($"[PartySceneWiring] Wired {wired} reference(s). Save the scene to persist.");
        }

        static int WireOnlinePlayersPanelScene(OnlinePlayersPanel panel,
            HostConnectionDataSO connectionData, SO_ProfileIconList profileIcons)
        {
            int count = 0;
            var so = new SerializedObject(panel);
            var root = panel.transform;

            // Wire SO data
            count += WireIfNullCount(so, "connectionData", connectionData);
            count += WireIfNullCount(so, "profileIcons", profileIcons);

            // Wire playerEntryPrefab
            var entryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/OnlinePlayerEntry.prefab");
            count += WireIfNullCount(so, "playerEntryPrefab", entryPrefab);

            // Wire friendsPanel cross-reference
            var friendsPanel = FindInScene<FriendsPanel>();
            count += WireIfNullCount(so, "friendsPanel", friendsPanel);

            // Create or find child elements
            count += EnsureChildAndWire(so, root, "entryContainer",
                () => CreateContentContainer(root, "Content", new Vector2(0, -20), new Vector2(400, 400)),
                go => go.transform);

            count += EnsureChildAndWire(so, root, "closeButton",
                () => CreateChildButton(root, "CloseButton", "X", 30, 30, new Vector2(180, 220)),
                go => go.GetComponent<Button>());

            count += EnsureChildAndWire(so, root, "openFriendsButton",
                () => CreateChildButton(root, "OpenFriendsButton", "Friends", 80, 30, new Vector2(90, 220)),
                go => go.GetComponent<Button>());

            count += EnsureChildAndWire(so, root, "emptyStateLabel",
                () => { var g = CreateChildText(root, "EmptyLabel", "No players online", 14,
                    Vector2.zero, new Vector2(300, 40)); g.SetActive(false); return g; },
                go => go);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);
            Debug.Log($"[PartySceneWiring] OnlinePlayersPanel: wired {count} reference(s).");
            return count;
        }

        static int WireFriendsPanelScene(FriendsPanel panel,
            FriendsDataSO friendsData, HostConnectionDataSO connectionData)
        {
            int count = 0;
            var so = new SerializedObject(panel);
            var root = panel.transform;

            // Wire SO data
            count += WireIfNullCount(so, "friendsData", friendsData);
            count += WireIfNullCount(so, "connectionData", connectionData);

            // Wire entry prefabs
            var friendEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/FriendEntryView.prefab");
            count += WireIfNullCount(so, "friendEntryPrefab", friendEntryPrefab);
            var friendRequestEntryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{PrefabFolder}/FriendRequestEntryView.prefab");
            count += WireIfNullCount(so, "friendRequestEntryPrefab", friendRequestEntryPrefab);

            // Tab buttons
            count += EnsureChildAndWire(so, root, "friendsTabButton",
                () => { var g = CreateChildButton(root, "FriendsTabButton", "Friends", 120, 32,
                    new Vector2(-130, 180)); g.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.3f); return g; },
                go => go.GetComponent<Button>());

            count += EnsureChildAndWire(so, root, "requestsTabButton",
                () => { var g = CreateChildButton(root, "RequestsTabButton", "Requests", 120, 32,
                    new Vector2(0, 180)); g.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f); return g; },
                go => go.GetComponent<Button>());

            count += EnsureChildAndWire(so, root, "addFriendTabButton",
                () => { var g = CreateChildButton(root, "AddFriendTabButton", "Add", 120, 32,
                    new Vector2(130, 180)); g.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f); return g; },
                go => go.GetComponent<Button>());

            // Friends list content panel + container
            count += EnsureChildAndWire(so, root, "friendsListContent",
                () => CreateContentPanel(root, "FriendsListContent"),
                go => go);

            var friendsListContent = root.Find("FriendsListContent");
            if (friendsListContent != null)
            {
                count += EnsureChildAndWire(so, friendsListContent, "friendsContainer",
                    () => CreateContentContainer(friendsListContent, "FriendsContainer",
                        Vector2.zero, new Vector2(400, 350)),
                    go => go.transform);

                count += EnsureChildAndWire(so, friendsListContent, "friendsEmptyState",
                    () => { var g = CreateChildText(friendsListContent, "FriendsEmptyState",
                        "No friends yet", 14, Vector2.zero, new Vector2(300, 40));
                        g.SetActive(false); return g; },
                    go => go);
            }

            // Requests list content panel + container
            count += EnsureChildAndWire(so, root, "requestsListContent",
                () => { var g = CreateContentPanel(root, "RequestsListContent");
                    g.SetActive(false); return g; },
                go => go);

            var requestsListContent = root.Find("RequestsListContent");
            if (requestsListContent != null)
            {
                count += EnsureChildAndWire(so, requestsListContent, "requestsContainer",
                    () => CreateContentContainer(requestsListContent, "RequestsContainer",
                        Vector2.zero, new Vector2(400, 350)),
                    go => go.transform);

                count += EnsureChildAndWire(so, requestsListContent, "requestsEmptyState",
                    () => { var g = CreateChildText(requestsListContent, "RequestsEmptyState",
                        "No pending requests", 14, Vector2.zero, new Vector2(300, 40));
                        g.SetActive(false); return g; },
                    go => go);
            }

            // Add Friend content panel
            count += EnsureChildAndWire(so, root, "addFriendContent",
                () => { var g = CreateUIRoot("AddFriendContent", 400, 350);
                    g.layer = 5; g.transform.SetParent(root, false);
                    g.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -20);
                    g.SetActive(false); g.AddComponent<AddFriendPanel>(); return g; },
                go => go.GetComponent<AddFriendPanel>());

            // Header text
            count += EnsureChildAndWire(so, root, "headerText",
                () => CreateChildText(root, "HeaderText", "Friends", 20,
                    new Vector2(0, 220), new Vector2(300, 40)),
                go => go.GetComponent<TMP_Text>());

            // Requests badge
            count += EnsureChildAndWire(so, root, "requestsBadge",
                () => { var g = CreateChildText(root, "RequestsBadge", "", 12,
                    new Vector2(120, 230), new Vector2(30, 24));
                    g.SetActive(false); return g; },
                go => go.GetComponent<TMP_Text>());

            // Close button
            count += EnsureChildAndWire(so, root, "closeButton",
                () => CreateChildButton(root, "CloseButton", "X", 30, 30,
                    new Vector2(185, 220)),
                go => go.GetComponent<Button>());

            // Refresh button
            count += EnsureChildAndWire(so, root, "refreshButton",
                () => CreateChildButton(root, "RefreshButton", "\u21BB", 30, 30,
                    new Vector2(145, 220)),
                go => go.GetComponent<Button>());

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);
            Debug.Log($"[PartySceneWiring] FriendsPanel: wired {count} reference(s).");
            return count;
        }

        static int WireInviteNotificationPanelScene(PartyInviteNotificationPanel panel,
            HostConnectionDataSO connectionData, SO_ProfileIconList profileIcons)
        {
            int count = 0;
            var so = new SerializedObject(panel);
            var root = panel.transform;

            count += WireIfNullCount(so, "connectionData", connectionData);
            count += WireIfNullCount(so, "profileIcons", profileIcons);
            count += WireIfNullCount(so, "panelRoot", panel.gameObject);

            count += EnsureChildAndWire(so, root, "inviterNameText",
                () => CreateChildText(root, "InviterName", "Player invited you!", 16,
                    new Vector2(20, 20), new Vector2(220, 30)),
                go => go.GetComponent<TMP_Text>());

            count += EnsureChildAndWire(so, root, "inviterAvatarImage",
                () => CreateChildImage(root, "InviterAvatar", 50, 50, new Vector2(-130, 20)),
                go => go.GetComponent<Image>());

            count += EnsureChildAndWire(so, root, "acceptButton",
                () => CreateChildButton(root, "AcceptButton", "Accept", 100, 36,
                    new Vector2(-50, -40)),
                go => go.GetComponent<Button>());

            count += EnsureChildAndWire(so, root, "declineButton",
                () => CreateChildButton(root, "DeclineButton", "Decline", 100, 36,
                    new Vector2(60, -40)),
                go => go.GetComponent<Button>());

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(panel);
            Debug.Log($"[PartySceneWiring] PartyInviteNotificationPanel: wired {count} reference(s).");
            return count;
        }

        static int WirePartyArcadeViewScene(PartyArcadeView view,
            OnlinePlayersPanel onlinePanel, FriendsPanel friendsPanel,
            PartyInviteNotificationPanel invitePanel,
            HostConnectionDataSO connectionData, FriendsDataSO friendsData,
            SO_ProfileIconList profileIcons)
        {
            int count = 0;
            var so = new SerializedObject(view);
            var root = view.transform;

            // Wire SO data
            count += WireIfNullCount(so, "connectionData", connectionData);
            count += WireIfNullCount(so, "friendsData", friendsData);
            count += WireIfNullCount(so, "profileIcons", profileIcons);

            // Wire sub-panel cross-references
            count += WireIfNullCount(so, "onlinePlayersPanel", onlinePanel);
            count += WireIfNullCount(so, "friendsPanel", friendsPanel);
            count += WireIfNullCount(so, "inviteNotificationPanel", invitePanel);

            // Create or find buttons and status text
            count += EnsureChildAndWire(so, root, "friendsButton",
                () => CreateChildButton(root, "FriendsButton", "Friends", 90, 32,
                    new Vector2(0, -40)),
                go => go.GetComponent<Button>());

            count += EnsureChildAndWire(so, root, "refreshButton",
                () => CreateChildButton(root, "RefreshButton", "\u21BB", 32, 32,
                    new Vector2(55, -40)),
                go => go.GetComponent<Button>());

            count += EnsureChildAndWire(so, root, "friendsRequestBadge",
                () => { var g = CreateChildText(root, "FriendsRequestBadge", "", 11,
                    new Vector2(50, -28), new Vector2(24, 24));
                    g.SetActive(false); return g; },
                go => go.GetComponent<TMP_Text>());

            count += EnsureChildAndWire(so, root, "partyStatusText",
                () => CreateChildText(root, "PartyStatusText", "Invite players to your party", 12,
                    new Vector2(0, -60), new Vector2(200, 24)),
                go => go.GetComponent<TMP_Text>());

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(view);
            Debug.Log($"[PartySceneWiring] PartyArcadeView: wired {count} reference(s).");
            return count;
        }

        // ── Helpers ────────────────────────────────────────────────────

        static T FindInScene<T>() where T : Component
        {
            // Search all root objects including inactive ones
            var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var root in roots)
            {
                var comp = root.GetComponentInChildren<T>(true);
                if (comp != null) return comp;
            }
            return null;
        }

        static int WireIfNullCount(SerializedObject so, string propertyName, Object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop != null && prop.objectReferenceValue == null && value != null)
            {
                prop.objectReferenceValue = value;
                return 1;
            }
            return 0;
        }

        /// <summary>
        /// Finds an existing child by expected name or creates one, then wires the serialized property.
        /// Only acts if the property is currently null.
        /// </summary>
        static int EnsureChildAndWire<T>(SerializedObject so, Transform parent,
            string propertyName, System.Func<GameObject> createFn,
            System.Func<GameObject, T> extractFn) where T : Object
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null || prop.objectReferenceValue != null)
                return 0;

            // Try to find existing child by common naming conventions
            string[] candidateNames = GetCandidateNames(propertyName);
            GameObject existing = null;
            foreach (var name in candidateNames)
            {
                var found = parent.Find(name);
                if (found != null) { existing = found.gameObject; break; }
            }

            var target = existing ?? createFn();
            if (target == null) return 0;

            // Ensure proper layer for UI
            target.layer = 5;

            var value = extractFn(target);
            if (value == null) return 0;

            prop.objectReferenceValue = value;
            return 1;
        }

        static string[] GetCandidateNames(string propertyName)
        {
            // Map serialized field names to common child GameObject names
            return propertyName switch
            {
                "entryContainer" => new[] { "Content", "EntryContainer", "ScrollContent" },
                "closeButton" => new[] { "CloseButton", "Close", "BtnClose" },
                "openFriendsButton" => new[] { "OpenFriendsButton", "FriendsButton" },
                "emptyStateLabel" => new[] { "EmptyLabel", "EmptyState", "EmptyStateLabel" },
                "friendsTabButton" => new[] { "FriendsTabButton", "FriendsTab" },
                "requestsTabButton" => new[] { "RequestsTabButton", "RequestsTab" },
                "addFriendTabButton" => new[] { "AddFriendTabButton", "AddFriendTab" },
                "friendsListContent" => new[] { "FriendsListContent", "FriendsList" },
                "requestsListContent" => new[] { "RequestsListContent", "RequestsList" },
                "addFriendContent" => new[] { "AddFriendContent", "AddFriendPanel" },
                "friendsContainer" => new[] { "FriendsContainer", "Content" },
                "friendsEmptyState" => new[] { "FriendsEmptyState", "EmptyState" },
                "requestsContainer" => new[] { "RequestsContainer", "Content" },
                "requestsEmptyState" => new[] { "RequestsEmptyState", "EmptyState" },
                "headerText" => new[] { "HeaderText", "Header" },
                "requestsBadge" => new[] { "RequestsBadge", "Badge" },
                "refreshButton" => new[] { "RefreshButton", "Refresh", "BtnRefresh" },
                "friendsButton" => new[] { "FriendsButton", "Friends", "BtnFriends" },
                "friendsRequestBadge" => new[] { "FriendsRequestBadge", "RequestBadge" },
                "partyStatusText" => new[] { "PartyStatusText", "StatusText" },
                "inviterNameText" => new[] { "InviterName", "InviterNameText" },
                "inviterAvatarImage" => new[] { "InviterAvatar", "InviterAvatarImage" },
                "acceptButton" => new[] { "AcceptButton", "Accept" },
                "declineButton" => new[] { "DeclineButton", "Decline" },
                _ => new[] { propertyName }
            };
        }

        static GameObject CreateContentPanel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(0, -20);
            rt.sizeDelta = new Vector2(400, 350);
            return go;
        }

        static GameObject CreateContentContainer(Transform parent, string name,
            Vector2 position, Vector2 size)
        {
            var go = new GameObject(name);
            go.layer = 5;
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            return go;
        }

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
