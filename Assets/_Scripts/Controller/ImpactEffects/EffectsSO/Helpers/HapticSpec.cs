using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    [System.Serializable]
    public struct HapticSpec
    {
        [SerializeField] HapticType _type;

        public void PlayIfManual(IVesselStatus status)
        {
            // Only the vessel the local human is actively flying may buzz this
            // device — without the identity gate, a REMOTE human's collisions
            // (autopilot off) leak haptics onto every peer's hands. The helper
            // also covers the non-networked single-player spawn path, where
            // IsLocalUser alone is structurally false.
            if (HapticController.IsLocalHumanPilot(status))
                HapticController.PlayHaptic(_type);
        }
    }
}