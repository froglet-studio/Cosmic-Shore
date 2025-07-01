using CosmicShore.Core;
using System.Collections.Generic;
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
            foreach (var effect in _crystalImpactEffects)
            {
                if (effect is IImpactEffect impactEffect)
                {
                    impactEffect.Execute(new ImpactContext
                    {
                        CrystalProperties = crystalProperties,
                        ShipStatus = _shipStatus
                    });
                }
                else
                {
                    Debug.LogWarning($"Impact effect {effect.name} does not implement IImpactEffect interface.");
                }
            }
        }

        public void PerformTrailBlockImpactEffects(TrailBlockProperties trailBlockProperties)
        {
            foreach (var effect in _trailBlockImpactEffects)
            {
                if (effect is IImpactEffect impactEffect)
                {
                    impactEffect.Execute(new ImpactContext
                    {
                        TrailBlockProperties = trailBlockProperties,
                        ShipStatus = _shipStatus
                    });
                }
                else
                {
                    Debug.LogWarning($"Impact effect {effect.name} does not implement IImpactEffect interface.");
                }
            }
        }
    }
}
