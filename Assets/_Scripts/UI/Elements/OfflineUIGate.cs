using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Hides or disables ONLINE-ONLY UI while the session is offline, and reveals
    /// offline-only UI (an explanatory notice, the reconnect button) in its place.
    ///
    /// <para>
    /// One reusable, inspector-wired component instead of an offline branch inside every
    /// screen: drop it on a panel, list what is online-only, and the panel gates itself.
    /// Config separation as usual - what to gate is authored data, not code.
    /// </para>
    ///
    /// <para>
    /// This is the PRESENTATION half. It is deliberately not the only defence: the services
    /// themselves refuse online work while offline (invites, leaderboard writes, purchases),
    /// so an un-wired screen still cannot fire a doomed request - it just looks live. Gate
    /// the UI so players are never offered something that cannot work; never rely on the UI
    /// alone to enforce it.
    /// </para>
    ///
    /// <para>
    /// State comes from <see cref="GameDataSO.IsOfflineSession"/> (injected). Re-applied on
    /// enable - which covers every way these panels appear, since screens and modals are
    /// activated on navigation - and on the reconnect service's state change, so a retry in
    /// flight disables the controls it is about to replace.
    /// </para>
    /// </summary>
    public class OfflineUIGate : MonoBehaviour
    {
        /// <summary>How an online-only object is suppressed while offline.</summary>
        public enum GateStyle
        {
            /// <summary>Deactivate the GameObject. Use when a dead control would confuse.</summary>
            Hide = 0,
            /// <summary>Keep it visible but non-interactive (and dimmed). Use when the layout
            /// would collapse, or when the player should see the feature exists.</summary>
            DisableAndDim = 1,
        }

        [Header("Gating")]
        [SerializeField, Tooltip("How online-only objects are suppressed while offline.")]
        GateStyle style = GateStyle.Hide;

        [SerializeField, Tooltip("Objects that require a live online session. Hidden or disabled while offline.")]
        List<GameObject> onlineOnlyObjects = new();

        [SerializeField, Tooltip("Controls that require a live online session. Made non-interactive while offline (in addition to the objects above).")]
        List<Selectable> onlineOnlyControls = new();

        [SerializeField, Tooltip("Objects shown ONLY while offline - the 'you are offline' notice, the reconnect button. Hidden while online.")]
        List<GameObject> offlineOnlyObjects = new();

        [Header("Dim")]
        [SerializeField, Range(0.1f, 1f), Tooltip("Alpha applied to online-only objects in DisableAndDim style. Ignored in Hide style.")]
        float disabledAlpha = 0.4f;

        [Inject] GameDataSO _gameData;
        [Inject] ReconnectService _reconnect;

        bool _subscribed;

        bool IsOffline => _gameData != null && _gameData.IsOfflineSession;

        void OnEnable()
        {
            Apply();
            TrySubscribe();
        }

        void Start()
        {
            // [Inject] fields land after Awake but before Start, so OnEnable's first pass on a
            // scene-loaded object can run with a null _gameData and read as "online". Re-apply
            // once injection has certainly happened.
            Apply();
            TrySubscribe();
        }

        void OnDisable()
        {
            if (_subscribed && _reconnect != null)
            {
                _reconnect.OnReconnectingChanged -= HandleReconnectingChanged;
                _subscribed = false;
            }
        }

        void TrySubscribe()
        {
            if (_subscribed || _reconnect == null) return;
            _reconnect.OnReconnectingChanged += HandleReconnectingChanged;
            _subscribed = true;
        }

        void HandleReconnectingChanged(bool _) => Apply();

        /// <summary>
        /// Applies the current online/offline state to every wired object. Public so a screen
        /// that rebuilds its content at runtime can re-gate the freshly spawned rows.
        /// </summary>
        public void Apply()
        {
            bool offline = IsOffline;

            foreach (var go in onlineOnlyObjects)
            {
                if (go == null) continue;

                if (style == GateStyle.Hide)
                {
                    go.SetActive(!offline);
                    continue;
                }

                go.SetActive(true);
                ApplyDim(go, offline);
            }

            foreach (var control in onlineOnlyControls)
            {
                if (control == null) continue;
                control.interactable = !offline;
            }

            foreach (var go in offlineOnlyObjects)
            {
                if (go == null) continue;
                go.SetActive(offline);
            }
        }

        void ApplyDim(GameObject go, bool offline)
        {
            if (!go.TryGetComponent<CanvasGroup>(out var group))
                group = go.AddComponent<CanvasGroup>();

            group.alpha = offline ? disabledAlpha : 1f;
            group.interactable = !offline;
            group.blocksRaycasts = !offline;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // A gate wired with nothing to gate is almost always a wiring mistake - it looks
            // installed and does nothing.
            if (onlineOnlyObjects.Count == 0 && onlineOnlyControls.Count == 0 && offlineOnlyObjects.Count == 0)
                CSDebug.LogWarning($"[OfflineUIGate] '{name}' has no objects or controls wired - it will do nothing.");
        }
#endif
    }
}
