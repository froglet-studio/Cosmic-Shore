using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(AOEExplosion))]
    public class ExplosionImpactor : ImpactorBase
    {
        ShipExplosionEffectSO[] shipAOEEffects;
        
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
                case ShipImpactor shipImpactee:
                    if (shipImpactee.Ship.ShipStatus.Team == Explosion.Team && !affectSelf)
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
        
        void ExecuteCommonPrismCommands(TrailBlock prism, Vector3 impactVector)
        {
            if ((prism.Team != Explosion.Team || affectSelf) && prism.TrailBlockProperties.IsSuperShielded)
            {
                prism.DeactivateShields();
                Destroy(gameObject);    // TODO: This seems wrong...
            } 
            if ((prism.Team == Explosion.Team && !affectSelf) || !destructive)
            {
                if (shielding && prism.Team == Explosion.Team)
                    prism.ActivateShield();
                else 
                    prism.ActivateShield(2f);
                return;
            }
            
            if (Explosion.AnonymousExplosion) // Ship Status will be null here
                prism.Damage(impactVector, Teams.None, "🔥GuyFawkes🔥", devastating);
            else
            {
                var shipStatus = Explosion.Ship.ShipStatus;
                prism.Damage(impactVector, shipStatus.Team, shipStatus.Player.Name, devastating);
            }
        }
    }
}