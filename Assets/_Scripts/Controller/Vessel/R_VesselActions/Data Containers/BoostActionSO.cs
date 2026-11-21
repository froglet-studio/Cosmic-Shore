using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "BoostAction", menuName = "ScriptableObjects/Vessel Actions/Boost")]
    public class BoostActionSO : ShipActionSO
    {
        [Tooltip("The element whose level-5 upgrade unlocks this boost. None (every shared asset) = always boosts. " +
                 "The Manta's own MantaSoarBoostAction asks for Time: Soar is its Time 5 upgrade.")]
        [SerializeField] Element requiresUpgrade = Element.None;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (vesselStatus == null) return;
            if (requiresUpgrade != Element.None
                && vesselStatus.ElementalAbilityHandler?.IsUpgradeActive(requiresUpgrade) != true) return;
            // Held, not a one-shot: Boost Activate loops (see HeldVesselSfx). This asset is shared
            // by every vessel, so the vessel itself owns the sound.
            HeldVesselSfx.Begin(execs ? execs.AudioSystem : null, GameplaySFXCategory.BoostActivate, SoundOwner(vesselStatus), vesselStatus);
            vesselStatus.IsBoosting = true;
            vesselStatus.IsStationary = false;
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (vesselStatus == null) return;
            HeldVesselSfx.End(execs ? execs.AudioSystem : null, GameplaySFXCategory.BoostActivate, SoundOwner(vesselStatus));
            vesselStatus.IsBoosting = false;
        }

        static Object SoundOwner(IVesselStatus vesselStatus)
            => vesselStatus.Vessel is Object vessel ? vessel : null;
    }
}
