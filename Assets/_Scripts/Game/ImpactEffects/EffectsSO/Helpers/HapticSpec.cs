using CosmicShore.Game.IO;
using UnityEngine;

namespace CosmicShore.Game
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