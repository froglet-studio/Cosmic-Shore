using CosmicShore.Core;
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

            CreateFriendEntryViewPrefab();
            CreateFriendRequestEntryViewPrefab();
            CreateAddFriendPanelPrefab();
            CreateFriendsPanelPrefab();
            CreateOnlinePlayerEntryPrefab();
            CreateOnlinePlayersPanelPrefab();
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
