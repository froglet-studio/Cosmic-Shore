using System;
using System.Threading;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Maelstrom-only connecting panel, shown at the start of each tournament game BEFORE the pre-game
    /// cinematic. It enables its own <see cref="connectingCamera"/> (authored in the prefab to match the
    /// Maelstrom lobby camera's pose) and shows the round's mode / intensity / leading-domain reveal via
    /// <see cref="TournamentConnectingInfo"/>, holds for <see cref="dwellSeconds"/> (2s), then hides and
    /// lets the cinematic take over.
    ///
    /// No-ops outside a tournament (<c>gameData.IsTournamentMode</c> false), so every other game mode
    /// skips it entirely. Lives on a prefab under the MiniGameHUD (same for all scenes); the MiniGameHUD
    /// awaits <see cref="ShowAsync"/> in its client-ready flow.
    ///
    /// Camera note: give <see cref="connectingCamera"/> a higher Depth than the gameplay camera and
    /// Clear Flags = Skybox/Solid Color so it renders over the (still-initializing) game world for the
    /// 2s; it's disabled again before the cinematic so the gameplay camera drives the flythrough.
    /// </summary>
    public class MaelstromConnectingPanel : MonoBehaviour
    {
        [SerializeField] GameDataSO gameData;
        [Tooltip("The panel UI root (toggled on for the duration).")]
        [SerializeField] GameObject panelRoot;
        [Tooltip("Separate camera posed to match the Maelstrom lobby camera. Enabled only during the panel.")]
        [SerializeField] Camera connectingCamera;
        [Tooltip("The Maelstrom-only data reveal (mode / intensity / leading domain).")]
        [SerializeField] TournamentConnectingInfo info;
        [SerializeField, Min(0f)] float dwellSeconds = 2f;

        public bool ShouldShow => gameData != null && gameData.IsTournamentMode;

        void Awake() => Hide();

        /// <summary>
        /// Shows the panel + camera, refreshes the reveal, holds for <see cref="dwellSeconds"/>, then
        /// hides. Returns immediately (and stays hidden) outside a tournament. Cancellation (HUD disabled)
        /// still hides via the finally.
        /// </summary>
        public async UniTask ShowAsync(CancellationToken ct)
        {
            if (!ShouldShow) { Hide(); return; }

            if (panelRoot) panelRoot.SetActive(true);
            if (connectingCamera) connectingCamera.enabled = true;
            if (info) info.Refresh();

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(dwellSeconds), ignoreTimeScale: true, cancellationToken: ct);
            }
            finally
            {
                Hide();
            }
        }

        void Hide()
        {
            if (panelRoot) panelRoot.SetActive(false);
            if (connectingCamera) connectingCamera.enabled = false;
        }
    }
}
