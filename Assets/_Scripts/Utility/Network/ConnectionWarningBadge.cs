using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// A small "Poor connection - N ms" label in the top-right corner, shown on a CLIENT while its
    /// RTT to the host stays above <see cref="ConnectionQuality.PoorAboveMs"/> for a few seconds.
    /// Runtime-built (no prefab or scene edit), so it exists in every scene and in release builds.
    /// Silent on the host, offline and in menus without a connection.
    /// </summary>
    public sealed class ConnectionWarningBadge : MonoBehaviour
    {
        const float SAMPLE_SECONDS = 0.5f;
        const float SUSTAIN_SECONDS = 3f; // ignore a single spike

        static ConnectionWarningBadge _instance;

        float _nextSample;
        float _badSince = -1f;
        int _rttMs;
        bool _show;
        GUIStyle _style;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (_instance) return;
            var go = new GameObject("[ConnectionWarningBadge]");
            go.hideFlags = HideFlags.DontSave;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ConnectionWarningBadge>();
        }

        void Update()
        {
            if (Time.unscaledTime < _nextSample) return;
            _nextSample = Time.unscaledTime + SAMPLE_SECONDS;

            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsConnectedClient || nm.IsServer || nm.NetworkConfig.NetworkTransport == null)
            {
                _show = false; _badSince = -1f;
                return;
            }

            _rttMs = (int)nm.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId);
            if (ConnectionQuality.IsBad(_rttMs))
            {
                if (_badSince < 0f) _badSince = Time.unscaledTime;
                _show = Time.unscaledTime - _badSince >= SUSTAIN_SECONDS;
            }
            else
            {
                _badSince = -1f;
                _show = false;
            }
        }

        void OnGUI()
        {
            if (!_show) return;
            _style ??= new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            var c = GUI.color;
            GUI.color = new Color(1f, 0.55f, 0.2f, 0.9f);
            GUI.Box(new Rect(Screen.width - 230, 10, 220, 28), "Poor connection - " + _rttMs + " ms", _style);
            GUI.color = c;
        }
    }
}
