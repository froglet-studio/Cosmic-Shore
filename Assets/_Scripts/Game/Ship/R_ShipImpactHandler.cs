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
        [SerializeField, RequireInterface(typeof(ICrystalImpactEffect))]
        List<ScriptableObject> _crystalImpactEffects;

        [SerializeField, RequireInterface(typeof(ITrailBlockImpactEffect))] 
        List<ScriptableObject> _trailBlockImpactEffects;

        IShipStatus _shipStatus;

        public void Initialize(IShipStatus shipStatus) => _shipStatus = shipStatus;


        public void PerformCrystalImpactEffects(CrystalProperties crystalProperties)
        {
            foreach (ICrystalImpactEffect effect in _crystalImpactEffects.Cast<ICrystalImpactEffect>())
            {
                if (effect is null)
                    continue;

                effect.Execute(new ImpactEffectData(_shipStatus, null, Vector3.zero), crystalProperties);
            }
        }

        public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (ITrailBlockImpactEffect effect in _trailBlockImpactEffects.Cast<ITrailBlockImpactEffect>())
            {
                if (effect is null)
                    continue;

                effect.Execute(new ImpactEffectData(_shipStatus, null, Vector3.zero), trailBlockProperties);
            }
        }
    }
}
