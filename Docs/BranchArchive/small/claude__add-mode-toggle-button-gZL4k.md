# Branch archive: `claude/add-mode-toggle-button-gZL4k`

_Snapshot 2026-10-08. Index: [README](../README.md)_

- **Last commit:** 2026-02-28 by Claude
- **Unmerged commits:** 1
- **Forked from:** `68f734177` (2026-02-28, Merge pull request #272 from froglet-studio/claude/replace-raycast-with-button)
- **Tip:** `c917cbfae`
- **Files touched (1):**
  - `Assets/_Scripts/UI/Elements/ModeToggleButton.cs`

### `c917cbfae` — feat(ui): add ModeToggleButton for Menu/Freestyle toggle

_Claude, 2026-02-28 05:24:31 +0000_

```text
Adds a reusable UI button component that wires to
MenuCrystalClickHandler.ToggleTransition(). Subscribes to
MenuFreestyleEventsContainerSO SOAP events to keep label in sync.
Spacebar shortcut included for testing.
```

```text
 Assets/_Scripts/UI/Elements/ModeToggleButton.cs | 87 +++++++++++++++++++++++++++++++++++++++++++++++++++++++
 1 file changed, 87 insertions(+)
```

<details><summary>Patch (code/doc/text files)</summary>

```diff
diff --git a/Assets/_Scripts/UI/Elements/ModeToggleButton.cs b/Assets/_Scripts/UI/Elements/ModeToggleButton.cs
new file mode 100644
index 000000000..53ffdca40
--- /dev/null
+++ b/Assets/_Scripts/UI/Elements/ModeToggleButton.cs
@@ -0,0 +1,87 @@
+using CosmicShore.Gameplay;
+using CosmicShore.ScriptableObjects;
+using TMPro;
+using UnityEngine;
+using UnityEngine.InputSystem;
+using UnityEngine.UI;
+
+namespace CosmicShore.UI
+{
+    /// <summary>
+    /// UI button that toggles between Menu and Freestyle mode on Menu_Main.
+    /// Wire to a Button's OnClick or use Spacebar for testing.
+    /// Listens to <see cref="MenuFreestyleEventsContainerSO"/> SOAP events
+    /// to keep the label in sync with the current state.
+    /// </summary>
+    [RequireComponent(typeof(Button))]
+    public class ModeToggleButton : MonoBehaviour
+    {
+        [Header("References")]
+        [SerializeField] MenuCrystalClickHandler crystalClickHandler;
+
+        [Header("SOAP Events")]
+        [SerializeField] MenuFreestyleEventsContainerSO freestyleEvents;
+
+        [Header("Label")]
+        [SerializeField] TMP_Text buttonLabel;
+        [SerializeField] string menuModeLabel = "Freestyle";
+        [SerializeField] string freestyleModeLabel = "Menu";
+
+        Button _button;
+
+        void Awake()
+        {
+            _button = GetComponent<Button>();
+            _button.onClick.AddListener(OnClick);
+        }
+
+        void OnEnable()
+        {
+            freestyleEvents.OnEnterFreestyle.OnRaised += HandleEnterFreestyle;
+            freestyleEvents.OnExitFreestyle.OnRaised += HandleExitFreestyle;
+        }
+
+        void OnDisable()
+        {
+            freestyleEvents.OnEnterFreestyle.OnRaised -= HandleEnterFreestyle;
+            freestyleEvents.OnExitFreestyle.OnRaised -= HandleExitFreestyle;
+        }
+
+        void Start()
+        {
+            UpdateLabel(crystalClickHandler.IsInFreestyle);
+        }
+
+        void Update()
+        {
+            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
+                OnClick();
+        }
+
+        void OnClick()
+        {
+            crystalClickHandler.ToggleTransition();
+        }
+
+        void HandleEnterFreestyle()
+        {
+            UpdateLabel(true);
+        }
+
+        void HandleExitFreestyle()
+        {
+            UpdateLabel(false);
+        }
+
+        void UpdateLabel(bool isFreestyle)
+        {
+            if (!buttonLabel) return;
+            buttonLabel.text = isFreestyle ? freestyleModeLabel : menuModeLabel;
+        }
+
+        void OnDestroy()
+        {
+            _button.onClick.RemoveListener(OnClick);
+        }
+    }
+}
```

</details>
