#if UNITY_EDITOR || DEVELOPMENT_BUILD
// ─────────────────────────────────────────────────────────────────────────────
// MultiplayerDebugOverlay - the "hybrid bug hunt" HUD for the friends / presence /
// arcade-lobby path. Editor and Development builds only; never ships in a release.
//
// Toggle: Ctrl+Shift+M (F2-F12 are all taken by other tools).
// Shows, refreshed twice a second:
//   - presence lobby id, host/member, converge outcome and how many lobbies are visible
//     (more than one = a split: players in different lobbies cannot see each other);
//   - every lobby player's ready flag, whether they are listed, and their absence strikes;
//   - the online list rows, pending departures, the safety-poll timer and watchers;
//   - UGS Friends presence for every friend;
//   - the replicated arcade LobbySnapshot (generation, seats, humans, placed AI).
// "Copy" puts the whole text on the clipboard so it can be pasted into a bug report.
// ─────────────────────────────────────────────────────────────────────────────
using System.Text;
using CosmicShore.Gameplay;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.Utility
{
    public sealed class MultiplayerDebugOverlay : MonoBehaviour
    {
        const float REFRESH_SECONDS = 0.5f;

        static MultiplayerDebugOverlay _instance;

        bool    _visible;
        float   _nextRefresh;
        string  _text = "";
        Vector2 _scroll;
        FriendsInitializer _friends;
        GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance) return;
            var go = new GameObject("[MultiplayerDebugOverlay]");
            go.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<MultiplayerDebugOverlay>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame &&
                (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) &&
                (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed))
            {
                _visible = !_visible;
                CSDebug.LogVerbose(CSLogChannel.Party, $"[MultiplayerDebugOverlay] {(_visible ? "shown" : "hidden")}");
            }

            if (!_visible || Time.realtimeSinceStartup < _nextRefresh) return;
            _nextRefresh = Time.realtimeSinceStartup + REFRESH_SECONDS;
            _text = Build();
        }

        string Build()
        {
            var sb = new StringBuilder(1024);
            sb.Append("t=").Append(Time.realtimeSinceStartup.ToString("F1")).AppendLine("s   (Ctrl+Shift+M hides)");

            var hcs = HostConnectionService.Instance;
            sb.AppendLine("── Presence ──");
            sb.Append(hcs ? hcs.BuildPresenceDiagnostics() : "HostConnectionService: not present\n");

            sb.AppendLine("── Friends (UGS presence) ──");
            if (!_friends) _friends = FindAnyObjectByType<FriendsInitializer>();
            var fd = _friends ? _friends.FriendsData : null;
            if (fd == null || fd.Friends == null) sb.AppendLine("  (no FriendsDataSO)");
            else if (fd.Friends.Count == 0) sb.AppendLine("  (no friends)");
            else
                foreach (var f in fd.Friends)
                    sb.Append("  ").Append(f.DisplayName).Append("  ").Append(f.IsOnline ? "ONLINE" : "offline")
                      .Append(" (").Append(f.Availability).Append(")  ").AppendLine(f.ActivityStatus);

            sb.AppendLine("── Arcade lobby snapshot ──");
            var sync = ArcadeConfigSyncManager.Instance;
            if (!sync || !sync.IsSpawned) sb.AppendLine("  (sync manager not spawned)");
            else sb.Append("  ").Append(sync.IsServer ? "[host] " : "[guest] ").AppendLine(sync.CurrentLobby.ToString());

            return sb.ToString();
        }

        void OnGUI()
        {
            if (!_visible) return;
            _style ??= new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = false, richText = false };

            var rect = new Rect(10, 10, Mathf.Min(620, Screen.width - 20), Mathf.Min(560, Screen.height - 20));
            GUI.Box(rect, GUIContent.none);
            GUILayout.BeginArea(new Rect(rect.x + 6, rect.y + 6, rect.width - 12, rect.height - 12));
            GUILayout.BeginHorizontal();
            GUILayout.Label("Multiplayer debug", GUI.skin.label);
            if (GUILayout.Button("Copy", GUILayout.Width(60))) GUIUtility.systemCopyBuffer = _text;
            if (GUILayout.Button("Poll now", GUILayout.Width(100)) && HostConnectionService.Instance) HostConnectionService.Instance.RequestDiagnosticRefresh();
            GUILayout.EndHorizontal();
            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.Label(_text, _style);
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
#endif
