using CosmicShore.Game.IO;
using UnityEngine;
using CosmicShore.Game.Ship;
using CosmicShore.Models.Enums;
namespace CosmicShore.Game.ImpactEffects
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