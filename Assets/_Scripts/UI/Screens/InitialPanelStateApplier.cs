using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Applies initial active/inactive states to UI panels in <c>Awake()</c>,
    /// before any <c>Start()</c> or <c>OnEnable()</c> subscriptions fire.
    ///
    /// Drop this on the ScreenSwitcher GameObject (or any root) in Menu_Main
    /// and configure which panels should start enabled or disabled via the inspector.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class InitialPanelStateApplier : MonoBehaviour
    {
        [Serializable]
        public struct PanelEntry
        {
            [Tooltip("The panel GameObject whose initial active state should be set.")]
            public GameObject panel;

            [Tooltip("Whether this panel should start active (true) or inactive (false).")]
            public bool startActive;
        }

        [Header("Panel Initial States")]
        [Tooltip("Configure which panels start active or inactive when the scene loads.")]
        [SerializeField] private List<PanelEntry> panelStates = new();

        void Awake()
        {
            foreach (var entry in panelStates)
            {
                if (entry.panel == null) continue;

                // Activate deactivated panels so CanvasGroup-based visibility works.
                // Panels hidden via CanvasGroup (alpha=0) are cheaper than SetActive(false)
                // and avoid canvas-rebuild spikes when toggled back on.
                if (!entry.panel.activeSelf)
                    entry.panel.SetActive(true);

                entry.panel.SetVisible(entry.startActive);
            }
        }
    }
}
