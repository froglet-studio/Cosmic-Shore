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
            if (!status.AutoPilotEnabled)
                HapticController.PlayHaptic(_type);
        }
    }
}