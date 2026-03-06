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
                "PartyInviteNotificationPanel", "PartyAreaPanel"
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
            CreatePartyInviteNotificationPrefab();
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
