// PORT Deviation #13 — type-preserving SHELL of the UI-bound SilhouetteView
// (original: Assets/_Scripts/Controller/Vessel/SilhouetteView.cs, 305 lines). The
// original drives a RectTransform/Image pool (UnityEngine.UI) rendering the vessel
// silhouette, jaw energy gauge, conveyor trail, and Manta flower overlay — all
// presentation-phase concerns. The public surface SilhouetteController forwards to is
// preserved with no-op bodies so the controller ports verbatim against it. The real
// port arrives with the phase-5 UI/render backend. Precedent: AudioSystem shell
// (Deviation #11), CameraManager shell (Deviation #12).
using CosmicShore.Gameplay;
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    public class SilhouetteView : MonoBehaviour
    {
        // --- Energy UI ---
        public void UpdateEnergyUI(float current, float max) { }

        public void SetBlockPrefab(GameObject prefab) { }

        public void Clear() { }

        public void SyncSilhouetteRotation2D(IVesselStatus status, float dot) { }

        public void BuildPoolIfNeeded(float scaleY, float wavelength) { }

        public void ApplyHeadAndConveyor(float xShift, float wavelength, float sx, float sy, float sz, float dot) { }

        public void ApplyTintToTrail(Color tint) { }

        public void SetDangerVisual(bool dangerEnabled) { }

        public void ShowMantaFlowerOverlay() { }
    }
}
