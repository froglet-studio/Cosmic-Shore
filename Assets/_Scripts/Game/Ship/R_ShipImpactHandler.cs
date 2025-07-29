using CosmicShore.Core;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Handles crystal and trail block effects for a ship.
    /// </summary>
    public class R_ShipImpactHandler : MonoBehaviour
    {
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        List<ScriptableObject> _crystalImpactEffects;

        [SerializeField, RequireInterface(typeof(IImpactEffect))] 
        List<ScriptableObject> _trailBlockImpactEffects;

        IShipStatus _shipStatus;

        public void Initialize(IShipStatus shipStatus) => _shipStatus = shipStatus;

        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
        {
            var castedEffects = _crystalImpactEffects.Cast<IImpactEffect>();
            var impactEffectData = new ImpactEffectData(_shipStatus, null, Vector3.zero);
            ShipHelper.ExecuteImpactEffect(castedEffects, impactEffectData, crystalProperties);
        }

        public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            var castedEffects = _crystalImpactEffects.Cast<IImpactEffect>();
            var impactEffectData = new ImpactEffectData(_shipStatus, null, Vector3.zero);
            ShipHelper.ExecuteImpactEffect(castedEffects, impactEffectData, default, trailBlockProperties);
        }
    }
}
