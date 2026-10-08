# Branch archive: `claude/fix-profile-avatar-selection-FlM5n`

_Snapshot 2026-10-08. Index: [README](../README.md#index--medium-branches-410-unmerged-commits)_

- **Last commit:** 2026-05-01 by Claude
- **Unmerged commits:** 5
- **Open pull request:** https://github.com/froglet-studio/Cosmic-Shore/pull/507
- **Forked from:** `16f954ee0` (2026-04-30, engine mix change)
- **Tip:** `86e2b54a9`
- **Files touched (7):**
  - `Assets/_Scripts/UI/Elements/ProfileImage.cs`
  - `Assets/_Scripts/UI/Modals/ProfileModal.cs`
  - `Assets/_Scripts/UI/ScreenSwitcher.cs`
  - `Assets/_Scripts/UI/Screens/HangarScreen.cs`
  - `Assets/_Scripts/UI/Views/HangarOverviewView.cs`
  - `Assets/_Scripts/UI/Views/HangarOverviewView.cs.meta`
  - `Assets/_Scripts/UI/Views/ProfileIconSelectView.cs`

### `f66d61f82` — fix(ui): inject DI container into profile icon buttons

_Claude, 2026-04-30 19:00:55 +0000_

```text
ProfileIconSelectButton.OnClick threw NullReferenceException because
audioSystem was never injected — buttons are Instantiated at runtime by
ProfileIconSelectView.BuildAvatarGrid, and Reflex does not auto-inject
runtime-spawned objects. Inject the container into the view and run
GameObjectInjector.InjectRecursive on each instantiated button, matching
the pattern used by PlayerSpawner / VesselSpawner / MenuMiniGameHUD.
```

```text
 Assets/_Scripts/UI/Views/ProfileIconSelectView.cs | 6 ++++++
 1 file changed, 6 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Views/ProfileIconSelectView.cs b/Assets/_Scripts/UI/Views/ProfileIconSelectView.cs
index 672b64ff0..2a053df5d 100644
--- a/Assets/_Scripts/UI/Views/ProfileIconSelectView.cs
+++ b/Assets/_Scripts/UI/Views/ProfileIconSelectView.cs
@@ -5,6 +5,8 @@ using CosmicShore.ScriptableObjects;
 using CosmicShore.UI;
 using CosmicShore.Utility;
 using Reflex.Attributes;
+using Reflex.Core;
+using Reflex.Injectors;
 using UnityEngine;
 using UnityEngine.UI;
 using TMPro;
@@ -38,6 +40,7 @@ namespace CosmicShore.UI
         [Header("Profile / UGS")]
         [Inject] private PlayerDataService dataService;
         [Inject] private GameDataSO gameData;
+        [Inject] private Container _container;
 
         // Internal state
         private ProfileIconSelectButton _selectedButton;
@@ -157,6 +160,9 @@ namespace CosmicShore.UI
                 var buttonInstance = Instantiate(iconButtonPrefab, iconGrid.transform);
                 buttonInstance.transform.localScale = Vector3.one;
 
+                if (_container != null)
+                    GameObjectInjector.InjectRecursive(buttonInstance.gameObject, _container);
+
                 buttonInstance.ProfileIcon = profileIcon;
                 buttonInstance.IconView    = this;
 
```

</details>

### `8167367ea` — fix(ui): refresh home-screen avatar from UGS + drop legacy HangarOverviewView

_Claude, 2026-05-01 18:19:02 +0000_

```text
- ProfileImage was subscribed to legacy PlayerDataController.OnProfileLoaded /
  OnPlayerAvatarUpdated, which the UGS path (PlayerDataService.SetAvatarId)
  never raises — so the home-screen avatar stayed stale after a profile
  change. Switch to [Inject] PlayerDataService and listen to
  OnProfileChanged, the same channel the rest of the app uses.

- Remove the HangarOverviewView legacy stub (a no-op View subclass kept
  only so the legacy fallback in HangarScreen had something to point at)
  and drop the matching field + usages from HangarScreen. The active
  flow is gridPanel + detailPanel (HangarVesselDetailView); the
  inspector "Overview View" slot was a dead reference that confused
  scene wiring.
```

```text
 Assets/_Scripts/UI/Elements/ProfileImage.cs         | 66 ++++++++++++++++++++++++++++++++++-----------------
 Assets/_Scripts/UI/Screens/HangarScreen.cs          |  8 -------
 Assets/_Scripts/UI/Views/HangarOverviewView.cs      | 13 ----------
 Assets/_Scripts/UI/Views/HangarOverviewView.cs.meta |  2 --
 4 files changed, 44 insertions(+), 45 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Elements/ProfileImage.cs b/Assets/_Scripts/UI/Elements/ProfileImage.cs
index 9f9edf592..fe08ca84a 100644
--- a/Assets/_Scripts/UI/Elements/ProfileImage.cs
+++ b/Assets/_Scripts/UI/Elements/ProfileImage.cs
@@ -1,46 +1,68 @@
-using CosmicShore.Core;
-using System.Linq;
-using UnityEngine;
-using UnityEngine.UI;
-using CosmicShore.Gameplay;
 using CosmicShore.ScriptableObjects;
 using CosmicShore.Utility;
+using Reflex.Attributes;
+using UnityEngine;
+using UnityEngine.UI;
 
 namespace CosmicShore.UI
 {
-    [RequireComponent (typeof (Image))]
+    [RequireComponent(typeof(Image))]
     public class ProfileImage : MonoBehaviour
     {
         [SerializeField] SO_ProfileIconList ProfileIcons;
 
-        void OnEnable()
+        [Inject] PlayerDataService playerDataService;
+
+        Image _image;
+
+        void Awake()
         {
-            PlayerDataController.OnProfileLoaded += SetSprite;
-            PlayerDataController.OnPlayerAvatarUpdated += SetSprite;
+            _image = GetComponent<Image>();
         }
 
-        void OnDisable()
+        void Start()
         {
-            PlayerDataController.OnProfileLoaded -= SetSprite;
-            PlayerDataController.OnPlayerAvatarUpdated -= SetSprite;
+            if (playerDataService == null) return;
+
+            playerDataService.OnProfileChanged += OnProfileChanged;
+
+            if (playerDataService.CurrentProfile != null)
+                ApplySprite(playerDataService.CurrentProfile.avatarId);
         }
 
-        void Start()
+        void OnDestroy()
         {
-            SetSprite();
+            if (playerDataService != null)
+                playerDataService.OnProfileChanged -= OnProfileChanged;
         }
 
-        void SetSprite()
+        void OnProfileChanged(PlayerProfileData profile)
         {
-            var image = GetComponent<Image>();
-            image.sprite = GetProfileImage();
+            if (profile == null) return;
+            ApplySprite(profile.avatarId);
         }
 
-        public Sprite GetProfileImage()
+        void ApplySprite(int avatarId)
         {
-            var profileIconId = PlayerDataController.PlayerProfile.ProfileIconId;
-            CSDebug.Log($"ProfileImage - GetProfileImage - {profileIconId}");
-            return ProfileIcons.profileIcons.FirstOrDefault(x => x.Id == profileIconId).IconSprite;
+            if (_image == null) _image = GetComponent<Image>();
+            if (ProfileIcons == null || ProfileIcons.profileIcons == null) return;
+
+            Sprite sprite = null;
+            foreach (var icon in ProfileIcons.profileIcons)
+            {
+                if (icon.Id == avatarId)
+                {
+                    sprite = icon.IconSprite;
+                    break;
+                }
+            }
+            if (sprite == null && ProfileIcons.profileIcons.Count > 0)
+                sprite = ProfileIcons.profileIcons[0].IconSprite;
+
+            _image.sprite = sprite;
+            _image.enabled = sprite != null;
+
+            CSDebug.Log($"ProfileImage - applied sprite for avatarId={avatarId}");
         }
     }
-}
\ No newline at end of file
+}
diff --git a/Assets/_Scripts/UI/Screens/HangarScreen.cs b/Assets/_Scripts/UI/Screens/HangarScreen.cs
index d0d93894c..29c323b2a 100644
--- a/Assets/_Scripts/UI/Screens/HangarScreen.cs
+++ b/Assets/_Scripts/UI/Screens/HangarScreen.cs
@@ -31,7 +31,6 @@ namespace CosmicShore.UI
         [SerializeField] private HangarVesselDetailView detailView;
 
         [Header("Legacy Views (kept for backward compatibility)")]
-        [SerializeField] private HangarOverviewView OverviewView;
         [SerializeField] private HangarAbilitiesView AbilitiesView;
         [SerializeField] private HangarTrainingModal HangarTrainingModal;
         [SerializeField] private NavGroup TopNav;
@@ -217,8 +216,6 @@ namespace CosmicShore.UI
 
         void LoadViewLegacy()
         {
-            if (OverviewView)
-                OverviewView.AssignModels(Ships.ConvertAll(x => (ScriptableObject)x));
             PopulateShipSelectionList();
         }
 
@@ -276,11 +273,6 @@ namespace CosmicShore.UI
         {
             if (!ship) return;
 
-            int shipIndex = Ships.IndexOf(ship);
-
-            if (OverviewView && shipIndex >= 0)
-                OverviewView.Select(shipIndex);
-
             if (AbilitiesView && ship.Abilities != null)
             {
                 foreach (var ability in ship.Abilities)
diff --git a/Assets/_Scripts/UI/Views/HangarOverviewView.cs b/Assets/_Scripts/UI/Views/HangarOverviewView.cs
deleted file mode 100644
index 60fbce759..000000000
--- a/Assets/_Scripts/UI/Views/HangarOverviewView.cs
+++ /dev/null
@@ -1,13 +0,0 @@
-using UnityEngine;
-
-namespace CosmicShore.UI
-{
-    /// <summary>
-    /// Legacy hangar overview view. Kept as a stub for backward compatibility
-    /// with serialized references in HangarScreen.
-    /// </summary>
-    public class HangarOverviewView : View
-    {
-        public override void UpdateView() { }
-    }
-}
```

</details>

### `3d0786304` — fix(ui): refresh home-screen avatar via PlayerDataService.OnProfileChanged

_Claude, 2026-05-01 18:37:54 +0000_

```text
ProfileModal owns the home-screen profileIconImage / profileNameLabel
(scene refs at Menu_Main fileID 653179561 + 231317289). It was wired
only to the legacy PlayerDataController.OnProfileLoaded event, which
the modern UGS write path never raises — so picking a new icon in
ProfileIconSelectView updated cloud save but the home-screen avatar
stayed on the previous sprite until a full app reload.

Subscribe to PlayerDataService.OnProfileChanged in Start and unsubscribe
in OnDestroy. On each event update profileNameLabel + the input field
(unless focused) and re-resolve the sprite from the SO_ProfileIconList,
matching the existing RefreshAvatarSprite logic but keyed off
PlayerProfileData.avatarId instead of the legacy ProfileIconId.

The earlier ProfileImage.cs change is dead code — that script isn't
referenced anywhere in the scene — but keep it converted to the modern
service so it's correct if anything starts using it.
```

```text
 Assets/_Scripts/UI/Modals/ProfileModal.cs | 49 +++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 49 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Modals/ProfileModal.cs b/Assets/_Scripts/UI/Modals/ProfileModal.cs
index bdaa8a1b2..1858e9e0a 100644
--- a/Assets/_Scripts/UI/Modals/ProfileModal.cs
+++ b/Assets/_Scripts/UI/Modals/ProfileModal.cs
@@ -81,9 +81,58 @@ namespace CosmicShore.UI
 
             PlayerDataController.OnProfileLoaded += InitializePlayerDisplayNameView;
 
+            if (playerDataService != null)
+            {
+                playerDataService.OnProfileChanged += OnUgsProfileChanged;
+
+                if (playerDataService.CurrentProfile != null)
+                    OnUgsProfileChanged(playerDataService.CurrentProfile);
+            }
+
             base.Start();
         }
 
+        void OnDestroy()
+        {
+            PlayerDataController.OnProfileLoaded -= InitializePlayerDisplayNameView;
+
+            if (playerDataService != null)
+                playerDataService.OnProfileChanged -= OnUgsProfileChanged;
+        }
+
+        void OnUgsProfileChanged(PlayerProfileData profile)
+        {
+            if (profile == null) return;
+
+            if (profileNameLabel)
+                profileNameLabel.text = profile.displayName;
+
+            if (displayNameInputField && !displayNameInputField.isFocused)
+                displayNameInputField.text = profile.displayName;
+
+            ApplyAvatarSpriteFromUgs(profile.avatarId);
+        }
+
+        void ApplyAvatarSpriteFromUgs(int avatarId)
+        {
+            if (!profileIconImage || profileIconList == null ||
+                profileIconList.profileIcons == null || profileIconList.profileIcons.Count == 0)
+                return;
+
+            ProfileIcon chosen = profileIconList.profileIcons[0];
+            foreach (var icon in profileIconList.profileIcons)
+            {
+                if (icon.Id == avatarId)
+                {
+                    chosen = icon;
+                    break;
+                }
+            }
+
+            profileIconImage.enabled = true;
+            profileIconImage.sprite = chosen.IconSprite;
+        }
+
         #region Email Input Field Operations (unchanged)
 
         void InitializeEmailLinking()
```

</details>

### `9d7d071f9` — fix(ui): force-enable navbar children on Start

_Claude, 2026-05-01 21:24:10 +0000_

```text
HangarLink was rendering as inactive at runtime even though the scene
asset has m_IsActive: 1 and nothing in code calls SetActive(false) on
it. Source of the toggle isn't traceable from a clean checkout —
likely a local editor edit or a prefab override on a downstream branch.

Walk NavBar.children once in ScreenSwitcher.Start and SetActive(true)
on any that come up disabled. This is a belt-and-suspenders guard, not
a fix for the root cause; if a script is genuinely disabling a link
later in the frame this won't help, but for a stale inspector toggle
it will.
```

```text
 Assets/_Scripts/UI/ScreenSwitcher.cs | 13 +++++++++++++
 1 file changed, 13 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/ScreenSwitcher.cs b/Assets/_Scripts/UI/ScreenSwitcher.cs
index 71273e6f6..2e835c704 100644
--- a/Assets/_Scripts/UI/ScreenSwitcher.cs
+++ b/Assets/_Scripts/UI/ScreenSwitcher.cs
@@ -235,6 +235,7 @@ namespace CosmicShore.UI
 
             CacheScreenComponents();
             LayoutScreensToViewport();
+            EnsureNavBarChildrenActive();
 
             panelLocation = transform.position;
 
@@ -684,6 +685,18 @@ namespace CosmicShore.UI
 
         #region NavBar & Icons
 
+        private void EnsureNavBarChildrenActive()
+        {
+            if (!NavBar) return;
+
+            for (var i = 0; i < NavBar.childCount; i++)
+            {
+                var child = NavBar.GetChild(i).gameObject;
+                if (!child.activeSelf)
+                    child.SetActive(true);
+            }
+        }
+
         private void UpdateNavBar(int index)
         {
             if (NavBar)
```

</details>

### `86e2b54a9` — fix(ui): subscribe ProfileModal in Awake; re-enable navbar on every nav

_Claude, 2026-05-01 21:37:36 +0000_

```text
Avatar refresh:
ProfileModal's MonoBehaviour starts disabled in Menu_Main.unity (the
PrefabInstance modification block sets m_Enabled: 0 on script GUID
d03b222767521fb46b35a956ee450a8b). Unity skips Start() on disabled
components, so the OnProfileChanged subscription added in the previous
commit never wired up — picking a new icon updated UGS but the
home-screen Image stayed stale. Awake DOES run on disabled components
of active GameObjects, and PlayerDataService is a Bootstrap-scene
singleton (DontDestroyOnLoad), so its Instance is set well before
Menu_Main loads. Subscribe in Awake via PlayerDataService.Instance
with an idempotent guard, also retry from Start() in case the modal
gets enabled later.

Navbar re-enable:
The earlier Start-only EnsureNavBarChildrenActive guard didn't hold —
something is deactivating HangarLink between ScreenSwitcher.Start and
the first navigation, so by the time the user sees the menu it's gone
again. Call EnsureNavBarChildrenActive at the top of every
UpdateNavBar (i.e. on every screen switch) and log a warning whenever
we have to re-enable a child, so the next play session shows in the
console which frame the toggle is happening on. Still belt-and-
suspenders — root cause is unknown — but at least the link won't stay
hidden.
```

```text
 Assets/_Scripts/UI/Modals/ProfileModal.cs | 36 ++++++++++++++++++++++++++++--------
 Assets/_Scripts/UI/ScreenSwitcher.cs      |  5 +++++
 2 files changed, 33 insertions(+), 8 deletions(-)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Modals/ProfileModal.cs b/Assets/_Scripts/UI/Modals/ProfileModal.cs
index 1858e9e0a..7478154cd 100644
--- a/Assets/_Scripts/UI/Modals/ProfileModal.cs
+++ b/Assets/_Scripts/UI/Modals/ProfileModal.cs
@@ -62,6 +62,32 @@ namespace CosmicShore.UI
 
         Action SummoningProfileMenu;
 
+        bool _ugsSubscribed;
+
+        void Awake()
+        {
+            // ProfileModal's MonoBehaviour starts disabled in the scene (m_Enabled=0),
+            // so Start() never runs and the [Inject] field is never read here. Awake DOES
+            // run on disabled components of active GameObjects, and PlayerDataService.Instance
+            // is set in Bootstrap (DontDestroyOnLoad) — so it's available now.
+            TrySubscribeToUgsProfile();
+        }
+
+        void TrySubscribeToUgsProfile()
+        {
+            if (_ugsSubscribed) return;
+
+            var ds = playerDataService ?? PlayerDataService.Instance;
+            if (ds == null) return;
+
+            playerDataService = ds;
+            ds.OnProfileChanged += OnUgsProfileChanged;
+            _ugsSubscribed = true;
+
+            if (ds.CurrentProfile != null)
+                OnUgsProfileChanged(ds.CurrentProfile);
+        }
+
         protected override void Start()
         {
             if (setDisplayNameButton)
@@ -81,13 +107,7 @@ namespace CosmicShore.UI
 
             PlayerDataController.OnProfileLoaded += InitializePlayerDisplayNameView;
 
-            if (playerDataService != null)
-            {
-                playerDataService.OnProfileChanged += OnUgsProfileChanged;
-
-                if (playerDataService.CurrentProfile != null)
-                    OnUgsProfileChanged(playerDataService.CurrentProfile);
-            }
+            TrySubscribeToUgsProfile();
 
             base.Start();
         }
@@ -96,7 +116,7 @@ namespace CosmicShore.UI
         {
             PlayerDataController.OnProfileLoaded -= InitializePlayerDisplayNameView;
 
-            if (playerDataService != null)
+            if (playerDataService != null && _ugsSubscribed)
                 playerDataService.OnProfileChanged -= OnUgsProfileChanged;
         }
 
diff --git a/Assets/_Scripts/UI/ScreenSwitcher.cs b/Assets/_Scripts/UI/ScreenSwitcher.cs
index 2e835c704..ff1daea1e 100644
--- a/Assets/_Scripts/UI/ScreenSwitcher.cs
+++ b/Assets/_Scripts/UI/ScreenSwitcher.cs
@@ -693,12 +693,17 @@ namespace CosmicShore.UI
             {
                 var child = NavBar.GetChild(i).gameObject;
                 if (!child.activeSelf)
+                {
+                    Debug.LogWarning($"[ScreenSwitcher] NavBar child '{child.name}' was deactivated — re-enabling. Caller stack will show who disabled it.");
                     child.SetActive(true);
+                }
             }
         }
 
         private void UpdateNavBar(int index)
         {
+            EnsureNavBarChildrenActive();
+
             if (NavBar)
             {
                 for (var i = 0; i < NavBar.childCount; i++)
```

</details>
