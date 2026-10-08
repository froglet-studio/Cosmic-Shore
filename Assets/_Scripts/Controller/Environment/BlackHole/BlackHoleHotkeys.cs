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
    ///                   looking through (your vessel's, while flying) — without opening anything;
    ///   <b>N</b>        spawns a black–white PAIR across that camera, black on the LEFT;
    ///   <b>M</b>        the same pair, black on the RIGHT — N sits left of M on the keyboard.
    ///                   The Stoat's sling, from any vessel (Docs/BLACK_HOLE.md §11).
    /// Ignored while a text field has focus, so typing a console command never fires them. Editor
    /// and development builds only, like the tool itself: the class compiles empty in a release
    /// player. Auto-spawned once per play session. B, N and M are bound nowhere else in the project
    /// (keyboard flight uses WASD, Q/E, R, P/;, L/', Space and Shift — so Shift+B also boosts while
    /// flying; N and M do not).
    /// </summary>
    public sealed class BlackHoleHotkeys : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const Key ToolKey = Key.B;
        const Key PairBlackLeftKey = Key.N;
        const Key PairBlackRightKey = Key.M;

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
            if (kb == null || TextFieldHasFocus()) return;

            bool pairLeft = kb[PairBlackLeftKey].wasPressedThisFrame, pairRight = kb[PairBlackRightKey].wasPressedThisFrame;
            if (pairLeft || pairRight)
            {
                if (BlackHoleRegistry.SpawnStyledPairFromConfig(attractorOnLeft: pairLeft) == null)
                    CSDebug.LogWarning($"[BlackHole] {(pairLeft ? "N" : "M")}: pair refused — needs two free of {BlackHoleRegistry.Config.MaxBlackHoles} " +
                                       $"({BlackHoleRegistry.Count} live), or BlackHoleConfig is not sane.");
                return;
            }

            if (!kb[ToolKey].wasPressedThisFrame) return;

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
