using System.Collections.Generic;
using CosmicShore.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Binds the LOCAL player's profile avatar to a UI Image so it stays consistent with the
    /// avatar everywhere else (profile modal, multiplayer lobby, in-game HUD). It adds a
    /// <see cref="ProfileImage"/> component to each selected GameObject - ProfileImage
    /// auto-subscribes to <c>PlayerDataService.OnProfileChanged</c> and resolves the sprite via
    /// <c>GetAvatarSprite</c> (the single source of truth), so no further inspector wiring is needed.
    ///
    /// Why this is needed: the menu's header avatar Image ("AvatarIcon") has no live-binding
    /// component - the purpose-built avatar widgets are orphaned after the UI migration, so the
    /// header never refreshes when the avatar changes. Select the header AvatarIcon (the Image
    /// next to your name, top-left) and run this once.
    ///
    /// Menu: FrogletTools > Services > Bind Selected Image To Local Avatar
    /// </summary>
    public static class ProfileAvatarBinder
    {
        const string Menu = "FrogletTools/Services/Bind Selected Image To Local Avatar";

        [MenuItem(Menu)]
        public static void BindSelected()
        {
            var selected = Selection.gameObjects;
            if (selected == null || selected.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Bind Profile Avatar",
                    "Select the GameObject(s) whose Image should show the local player's avatar " +
                    "(e.g. the header 'AvatarIcon'), then run this again.",
                    "OK");
                return;
            }

            var bound = new List<string>();
            var already = new List<string>();

            foreach (var go in selected)
            {
                if (go == null) continue;

                if (go.GetComponent<ProfileImage>() != null)
                {
                    already.Add(go.name);
                    continue;
                }

                // ProfileImage requires an Image; add one if the object doesn't have it yet.
                if (go.GetComponent<Image>() == null)
                    Undo.AddComponent<Image>(go);

                Undo.AddComponent<ProfileImage>(go);
                EditorUtility.SetDirty(go);
                bound.Add(go.name);
            }

            if (bound.Count > 0)
                Debug.Log($"[ProfileAvatarBinder] Bound ProfileImage to: {string.Join(", ", bound)}. " +
                          "Save the scene/prefab to persist.");
            if (already.Count > 0)
                Debug.Log($"[ProfileAvatarBinder] Already bound (skipped): {string.Join(", ", already)}.");
        }

        [MenuItem(Menu, true)]
        public static bool BindSelectedValidate() => Selection.gameObjects.Length > 0;
    }
}
