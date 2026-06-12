// PORT Deviation #12 — type-preserving SHELL of the Cinemachine-bound CameraManager
// (original: Assets/_Scripts/Controller/Managers/CameraManager.cs, 233 lines). The
// original wires CinemachineCamera priorities, scene-name routing, and the
// player/death/end CustomCameraController trio discovered by child-transform lookup —
// all presentation-phase concerns. Only the public surface used by the vessel-layer
// closure is present (VesselCameraCustomizer.RetargetAndApply reads
// Instance.GetActiveController()); bodies no-op. The real port arrives with the
// phase-5 Cinemachine replacement. Precedent: AudioSystem shell (Deviation #11).
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class CameraManager : Singleton<CameraManager>
    {
        private ICameraController _activeController;

        public ICameraController GetActiveController() => _activeController;
    }
}
