// PORT Deviation #12 — type-preserving SHELL of the Cinemachine-bound CameraManager
// (original: Assets/_Scripts/Controller/Managers/CameraManager.cs, 233 lines). The
// original wires CinemachineCamera priorities, scene-name routing, and the
// player/death/end CustomCameraController trio discovered by child-transform lookup —
// all presentation-phase concerns. Only the public surface consumers need is present:
// the vessel-layer closure (VesselCameraCustomizer.RetargetAndApply reads
// Instance.GetActiveController()) and — grown 2026-07-08 for the camera arc — the
// MainMenuCameraController activation quartet (SetMainMenuCameraActive /
// SetupGamePlayCameras / SetupEndCameraFollow / DeactivateAllCameras), which no-op
// into an observable ShellCameraState mirror. The real port arrives with the
// phase-5 Cinemachine replacement. Precedent: AudioSystem shell (Deviation #11).
//
// Drift-sync note (bleeding-edge merge c833c580): upstream added a
// DisplayGraphicsSettings/GraphicsSettingsApplier sync (FOV + post-process AA pushed
// onto the managed cameras) — entirely inside the stubbed presentation body, so the
// shell surface is unchanged. Restore with the full port.
using CosmicShore.Utility;
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    public class CameraManager : Singleton<CameraManager>
    {
        private ICameraController _activeController;

        public ICameraController GetActiveController() => _activeController;

        /// <summary>
        /// Shell surface for the controller-chain arc: the original re-snaps the player's
        /// Cinemachine camera to its follow target after ResetPlayers teleports the vessel
        /// (MultiplayerMiniGameControllerBase.ResetForReplay_ClientRpc). No-op headless.
        /// </summary>
        public void SnapPlayerCameraToTarget() { }

        // ── Shell surface for the camera arc (MainMenuCameraController) ──
        // The original activates/deactivates the Cinemachine vCam family and hands
        // gameplay following to CustomCameraController ("CM PlayerCam"). The shell
        // mirrors the CALL STATE so scene logic (and tests) can observe the handoff
        // sequencing; the presentation bodies arrive with the full Cinemachine
        // replacement.

        /// <summary>Which camera family the last activation call selected — shell state mirror.</summary>
        public enum ShellCameraState { None = 0, MainMenu = 1, Gameplay = 2, AllOff = 3 }

        /// <summary>Last activation observed by the shell (state mirror for scene logic/tests).</summary>
        public ShellCameraState ActiveShellState { get; private set; } = ShellCameraState.None;

        /// <summary>Follow target passed to the last <see cref="SetupGamePlayCameras"/> call.</summary>
        public Transform LastGameplayFollowTarget { get; private set; }

        /// <summary>Original: activates "CM Main Menu" and deactivates the gameplay trio.</summary>
        public void SetMainMenuCameraActive() => ActiveShellState = ShellCameraState.MainMenu;

        /// <summary>Original: activates the CustomCameraController pipeline on the follow target.</summary>
        public void SetupGamePlayCameras(Transform followTarget)
        {
            LastGameplayFollowTarget = followTarget;
            ActiveShellState = ShellCameraState.Gameplay;
        }

        /// <summary>Original: re-targets the end-game follow camera. Shell records the target only.</summary>
        public void SetupEndCameraFollow(Transform followTarget)
            => LastGameplayFollowTarget = followTarget;

        /// <summary>Original: deactivates every managed camera before a hand-rolled activation.</summary>
        public void DeactivateAllCameras() => ActiveShellState = ShellCameraState.AllOff;
    }
}
