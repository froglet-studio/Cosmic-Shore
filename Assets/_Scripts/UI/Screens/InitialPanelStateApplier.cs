using System;
using System.Collections.Generic;
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
                entry.panel.SetActive(entry.startActive);
            }
        }
    }
}
