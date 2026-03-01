using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Applies initial GameObject active state and CanvasGroup alpha to UI panels
    /// in <c>Awake()</c>, before any <c>Start()</c> or <c>OnEnable()</c> subscriptions fire.
    ///
    /// Drop this on the ScreenSwitcher GameObject (or any root) in Menu_Main
    /// and configure panel states via the inspector or the Canvas Group Editor window.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class InitialPanelStateApplier : MonoBehaviour
    {
        [Serializable]
        public struct PanelEntry
        {
            [Tooltip("The panel GameObject whose initial state should be set.")]
            public GameObject panel;

            [Tooltip("Whether the GameObject should be active at game start.")]
            public bool startActive;

            [Tooltip("Initial CanvasGroup alpha (0 = fully hidden, 1 = fully visible).")]
            [Range(0f, 1f)]
            public float startAlpha;
        }

        [Header("Panel Initial States")]
        [Tooltip("Configure which panels start active/inactive and at what alpha when the scene loads.")]
        [SerializeField] private List<PanelEntry> panelStates = new();

        void Awake()
        {
            foreach (var entry in panelStates)
            {
                if (entry.panel == null) continue;

                entry.panel.SetActive(entry.startActive);

                var cg = entry.panel.GetOrAdd<CanvasGroup>();
                cg.alpha = entry.startAlpha;
                cg.interactable = entry.startAlpha > 0f;
                cg.blocksRaycasts = entry.startAlpha > 0f;
            }
        }
    }
}
