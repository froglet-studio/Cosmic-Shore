using CosmicShore.Core;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(AOEExplosion))]
    public class ExplosionImpactor : ImpactorBase
    {
        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] explosionShipEffectsSO;

        [SerializeField, RequireInterface(typeof(IImpactEffect))]
        ScriptableObject[] explosionPrismEffectsSO;

        [SerializeField] bool affectSelf = false;
        [SerializeField] bool destructive = true;
        [SerializeField] bool devastating = false;
        [SerializeField] bool shielding = false;

        AOEExplosion explosion;
        public AOEExplosion Explosion => explosion ??= GetComponent<AOEExplosion>();

        IImpactEffect[] explosionShipEffects;
        IImpactEffect[] explosionPrismEffects;


        protected override void AcceptImpactee(IImpactor impactee)
        {
            var impactVector = Explosion.CalculateImpactVector(impactee.Transform.position);

            switch (impactee)
            {
                case ShipImpactor shipImpactee:
                    if (shipImpactee.Ship.ShipStatus.Team == Explosion.Team && !affectSelf)
                        break;
                    ExecuteEffect(shipImpactee, explosionShipEffects);
                    break;

                case PrismImpactor prismImpactee:
                    ExecuteCommonPrismCommands(prismImpactee.Prism, impactVector);
                    ExecuteEffect(prismImpactee, explosionPrismEffects);
                    break;
            }
        }

        void ExecuteCommonPrismCommands(TrailBlock prism, Vector3 impactVector)
        {
            if ((prism.Team != Explosion.Team || affectSelf) && prism.TrailBlockProperties.IsSuperShielded)
            {
                prism.DeactivateShields();
                Destroy(gameObject); // TODO: This seems wrong...
            }

            if ((prism.Team == Explosion.Team && !affectSelf) || !destructive)
            {
                if (shielding && prism.Team == Explosion.Team)
                    prism.ActivateShield();
                else
                    prism.ActivateShield(2f);
                return;
            }

            if (Explosion.AnonymousExplosion)
                prism.Damage(impactVector, Teams.None, "🔥GuyFawkes🔥", devastating);
            else
            {
                var shipStatus = Explosion.Ship.ShipStatus;
                prism.Damage(impactVector, shipStatus.Team, shipStatus.Player.Name, devastating);
            }
        }
    }
}