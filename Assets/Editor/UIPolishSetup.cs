using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.App.UI.FX;

/// <summary>
/// One-click editor tool to attach UI polish components across the project.
/// Run via menu: Tools > Cosmic Shore > Setup UI Polish Components
/// </summary>
public static class UIPolishSetup
{
    [MenuItem("Tools/Cosmic Shore/Setup UI Polish Components (All)")]
    public static void SetupAll()
    {
        int totalAdded = 0;
        totalAdded += AddButtonJuiceToAllButtons();
        totalAdded += AddSelectionHighlightToAllSelectables();
        totalAdded += AddAutoNavSetupToScreenRoots();

        Debug.Log($"[UIPolishSetup] Complete! Added {totalAdded} components total.");
    }

    [MenuItem("Tools/Cosmic Shore/1 - Add UIButtonJuice to All Buttons")]
    public static int AddButtonJuiceToAllButtons()
    {
        int count = 0;

        // Find all Button components in loaded scenes and prefabs
        var buttons = Resources.FindObjectsOfTypeAll<Button>();
        foreach (var button in buttons)
        {
            if (button == null) continue;
            if (PrefabUtility.IsPartOfImmutablePrefab(button.gameObject)) continue;

            if (button.GetComponent<UIButtonJuice>() == null)
            {
                Undo.AddComponent<UIButtonJuice>(button.gameObject);
                EditorUtility.SetDirty(button.gameObject);
                count++;
            }
        }

        Debug.Log($"[UIPolishSetup] Added UIButtonJuice to {count} buttons.");
        return count;
    }

    [MenuItem("Tools/Cosmic Shore/2 - Add SelectionHighlight to All Selectables")]
    public static int AddSelectionHighlightToAllSelectables()
    {
        int count = 0;

        var selectables = Resources.FindObjectsOfTypeAll<Selectable>();
        foreach (var selectable in selectables)
        {
            if (selectable == null) continue;
            if (PrefabUtility.IsPartOfImmutablePrefab(selectable.gameObject)) continue;

            if (selectable.GetComponent<SelectionHighlight>() == null)
            {
                Undo.AddComponent<SelectionHighlight>(selectable.gameObject);
                EditorUtility.SetDirty(selectable.gameObject);
                count++;
            }
        }

        Debug.Log($"[UIPolishSetup] Added SelectionHighlight to {count} selectables.");
        return count;
    }

    [MenuItem("Tools/Cosmic Shore/3 - Add AutoNavSetup to Screen Roots")]
    public static int AddAutoNavSetupToScreenRoots()
    {
        int count = 0;

        // Find ScreenSwitcher to locate menu screen roots
        var screenSwitchers = Resources.FindObjectsOfTypeAll<CosmicShore.App.UI.ScreenSwitcher>();
        foreach (var switcher in screenSwitchers)
        {
            if (switcher == null) continue;
            // Add AutoNavSetup to each direct child (each is a screen)
            for (int i = 0; i < switcher.transform.childCount; i++)
            {
                var screen = switcher.transform.GetChild(i).gameObject;
                if (screen.GetComponent<AutoNavSetup>() == null)
                {
                    Undo.AddComponent<AutoNavSetup>(screen);
                    EditorUtility.SetDirty(screen);
                    count++;
                }
            }
        }

        // Also add to modal roots
        var modals = Resources.FindObjectsOfTypeAll<CosmicShore.App.UI.Modals.ModalWindowManager>();
        foreach (var modal in modals)
        {
            if (modal == null) continue;
            if (PrefabUtility.IsPartOfImmutablePrefab(modal.gameObject)) continue;

            if (modal.GetComponent<AutoNavSetup>() == null)
            {
                Undo.AddComponent<AutoNavSetup>(modal.gameObject);
                EditorUtility.SetDirty(modal.gameObject);
                count++;
            }
        }

        Debug.Log($"[UIPolishSetup] Added AutoNavSetup to {count} screen/modal roots.");
        return count;
    }

    [MenuItem("Tools/Cosmic Shore/4 - Add MenuDPadNavigator to EventSystem")]
    public static void AddMenuDPadNavigator()
    {
        var eventSystem = Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
        if (eventSystem == null)
        {
            Debug.LogWarning("[UIPolishSetup] No EventSystem found in the active scene.");
            return;
        }

        if (eventSystem.GetComponent<MenuDPadNavigator>() == null)
        {
            var navigator = Undo.AddComponent<MenuDPadNavigator>(eventSystem.gameObject);
            EditorUtility.SetDirty(eventSystem.gameObject);

            // Try to auto-wire ScreenSwitcher reference
            var switcher = Object.FindObjectOfType<CosmicShore.App.UI.ScreenSwitcher>();
            if (switcher != null)
            {
                var so = new SerializedObject(navigator);
                var prop = so.FindProperty("screenSwitcher");
                if (prop != null)
                {
                    prop.objectReferenceValue = switcher;
                    so.ApplyModifiedProperties();
                }
            }

            Debug.Log("[UIPolishSetup] Added MenuDPadNavigator to EventSystem and wired ScreenSwitcher.");
        }
        else
        {
            Debug.Log("[UIPolishSetup] MenuDPadNavigator already on EventSystem.");
        }
    }

    [MenuItem("Tools/Cosmic Shore/5 - Enable DOTween Entrance on Modals")]
    public static void EnableDOTweenOnModals()
    {
        int count = 0;
        var modals = Resources.FindObjectsOfTypeAll<CosmicShore.App.UI.Modals.ModalWindowManager>();
        foreach (var modal in modals)
        {
            if (modal == null) continue;

            var so = new SerializedObject(modal);
            var prop = so.FindProperty("useDOTweenEntrance");
            if (prop != null && !prop.boolValue)
            {
                prop.boolValue = true;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(modal);
                count++;
            }
        }

        Debug.Log($"[UIPolishSetup] Enabled DOTween entrance on {count} modals.");
    }
}
