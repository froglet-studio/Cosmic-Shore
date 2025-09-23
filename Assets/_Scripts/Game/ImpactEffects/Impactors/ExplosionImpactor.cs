using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(AOEExplosion))]
    public class ExplosionImpactor : ImpactorBase
    {
        VesselExplosionEffectSO[] shipAOEEffects;
        
        ExplosionPrismEffectSO[] explosionPrismEffects;
        
        [SerializeField] bool affectSelf = false;
        [SerializeField] bool destructive = true;
        [SerializeField] bool devastating = false;
        [SerializeField] bool shielding = false;

        AOEExplosion explosion;
        public AOEExplosion Explosion => explosion ??= GetComponent<AOEExplosion>();
        
        protected override void AcceptImpactee(IImpactor impactee)
        {    
            var impactVector = Explosion.CalculateImpactVector(impactee.Transform.position);
            
            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                    if (shipImpactee.Vessel.VesselStatus.Domain == Explosion.Domain && !affectSelf)
                        break;
                    // ExecuteEffect(shipImpactee, explosionShipEffects);
                    if(!DoesEffectExist(shipAOEEffects)) return;
                    foreach (var effect in shipAOEEffects)
                    {
                        effect.Execute(shipImpactee, this);
                    }
                    break;
                
                case PrismImpactor prismImpactee:
                    ExecuteCommonPrismCommands(prismImpactee.Prism, impactVector);
                    // ExecuteEffect(prismImpactee, explosionPrismEffects);
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
            if ((prism.Domain != Explosion.Domain || affectSelf) && prism.prismProperties.IsSuperShielded)
            {
                prism.DeactivateShields();
                Destroy(gameObject);    // TODO: This seems wrong...
            } 
            if ((prism.Domain == Explosion.Domain && !affectSelf) || !destructive)
            {
                if (shielding && prism.Domain == Explosion.Domain)
                    prism.ActivateShield();
                else 
                    prism.ActivateShield(2f);
                return;
            }
            
            if (Explosion.AnonymousExplosion) // Vessel Status will be null here
                prism.Damage(impactVector, Domains.None, "🔥GuyFawkes🔥", devastating);
            else
            {
                var shipStatus = Explosion.Vessel.VesselStatus;
                prism.Damage(impactVector, shipStatus.Domain, shipStatus.Player.Name, devastating);
            }
        }
    }
}