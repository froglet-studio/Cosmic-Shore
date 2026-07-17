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
            if (status == null) return;
            // Only the locally-piloted vessel may buzz this device — without the
            // IsLocalUser gate, a REMOTE human's collisions (autopilot off) leak
            // haptics onto every peer's hands.
            if (!status.AutoPilotEnabled && status.IsLocalUser)
                HapticController.PlayHaptic(_type);
        }
    }
}