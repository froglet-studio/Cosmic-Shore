using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CosmicShore.Utility;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
#endif

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Keyboard shortcuts for black holes, in any scene (Docs/BLACK_HOLE.md §6.2) — the test scene,
    /// lava-lamp freestyle in Menu_Main, a game mode:
    ///   <b>B</b>        opens / closes the Black Hole tool (and, in the Editor, selects the
    ///                   <c>BlackHoleConfig</c> asset in the Inspector, so the next spawn's values are
    ///                   one click away in either place);
    ///   <b>Shift+B</b>  spawns a hole from the config right now — ahead of the camera you are
    ///                   looking through (your vessel's, while flying) — without opening anything.
    /// Ignored while a text field has focus, so typing a console command never fires them. Editor
    /// and development builds only, like the tool itself: the class compiles empty in a release
    /// player. Auto-spawned once per play session; B is bound nowhere else in the project.
    /// </summary>
    public sealed class BlackHoleHotkeys : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const Key ToolKey = Key.B;

        static BlackHoleHotkeys s_instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (s_instance != null) return;
            var go = new GameObject("[BlackHoleHotkeys]");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<BlackHoleHotkeys>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb[ToolKey].wasPressedThisFrame || TextFieldHasFocus()) return;

            if (kb.shiftKey.isPressed)
            {
                var hole = BlackHoleRegistry.SpawnFromConfig();
                if (hole == null)
                    CSDebug.LogWarning($"[BlackHole] Shift+B: spawn refused — {BlackHoleRegistry.Count}/" +
                                       $"{BlackHoleRegistry.Config.MaxBlackHoles} holes live, or BlackHoleConfig is not sane.");
                return;
            }

            bool open = !BlackHoleTool.IsOpen;
            BlackHoleTool.SetOpen(open);
#if UNITY_EDITOR
            if (open)
            {
                var config = BlackHoleRegistry.Config;
                if (UnityEditor.AssetDatabase.Contains(config))
                {
                    UnityEditor.Selection.activeObject = config;
                    UnityEditor.EditorGUIUtility.PingObject(config);
                }
            }
#endif
        }

        static bool TextFieldHasFocus()
        {
            var es = EventSystem.current;
            var selected = es != null ? es.currentSelectedGameObject : null;
            if (selected == null) return false;
            return selected.TryGetComponent<UnityEngine.UI.InputField>(out _) ||
                   selected.TryGetComponent<TMPro.TMP_InputField>(out _);
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        // Play-mode re-entry with domain reload off: the last session's instance is destroyed.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_instance = null;
#endif
    }
}
