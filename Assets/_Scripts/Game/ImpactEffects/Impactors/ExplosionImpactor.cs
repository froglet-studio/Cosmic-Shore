using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(AOEExplosion))]
    public class ExplosionImpactor : ImpactorBase
    {
        [SerializeField] private ExplosionImpactorDataContainerSO explosionImpactorDataContainer;
        
        [SerializeField] bool affectSelf;
        [SerializeField] bool destructive = true;
        [SerializeField] bool devastating;
        [SerializeField] bool shielding;

        AOEExplosion explosion;
        

        void Awake()
        {
            explosion ??= GetComponent<AOEExplosion>();
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {    
            var impactVector = explosion.CalculateImpactVector(impactee.Transform.position);
            
            switch (impactee)
            {
                case VesselImpactor vesselImpactee:
                    if (vesselImpactee.Vessel.VesselStatus.Domain == explosion.Domain && !affectSelf)
                        break;
                    
                    if (!explosionImpactorDataContainer) return;
                    var vesselExplosionEffects = explosionImpactorDataContainer.vesselExplosionEffects;
                    if(!DoesEffectExist(vesselExplosionEffects)) return;
                    foreach (var effect in vesselExplosionEffects)
                    {
                        effect.Execute(vesselImpactee, this);
                    }
                    break;
                
                case PrismImpactor prismImpactee:
                    ExecuteCommonPrismCommands(prismImpactee.Prism, impactVector);
                    if (!explosionImpactorDataContainer) return;
                    var explosionPrismEffects = explosionImpactorDataContainer.explosionPrismEffects;
                    if(!DoesEffectExist(explosionPrismEffects)) return;
                    foreach (var effect in explosionPrismEffects)
                    {
                        effect.Execute(this, prismImpactee);
                    }
                    break;
            }
        }
        
        void ExecuteCommonPrismCommands(Prism prism, Vector3 impactVector)
        {
            if ((prism.Domain != explosion.Domain || affectSelf) && prism.prismProperties.IsSuperShielded)
            {
                prism.DeactivateShields();
                Destroy(gameObject);    // TODO: This seems wrong...
            } 
            if ((prism.Domain == explosion.Domain && !affectSelf) || !destructive)
            {
                if (shielding && prism.Domain == explosion.Domain)
                    prism.ActivateShield();
                else 
                    prism.ActivateShield(2f);
                return;
            }
            
            if (explosion.AnonymousExplosion) // Vessel Status will be null here
                prism.Damage(impactVector, Domains.None, "🔥GuyFawkes🔥", devastating);
            else
            {
                var shipStatus = explosion.Vessel.VesselStatus;
                prism.Damage(impactVector, shipStatus.Domain, shipStatus.Player.Name, devastating);
            }
        }
    }
}