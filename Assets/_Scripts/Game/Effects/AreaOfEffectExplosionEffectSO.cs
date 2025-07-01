using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "AreaOfEffectExplosionImpactEffect", menuName = "ScriptableObjects/Impact Effects/AreaOfEffectExplosionImpactEffectSO")]
    public class AreaOfEffectExplosionEffectSO : BaseImpactEffectSO
    {
        [SerializeField]
        AOEExplosion _prefabGO;

        [SerializeField]
        int _ammoResourceIndex;

        [SerializeField]
        float _minExplosionScale;

        [SerializeField]
        float _maxExplosionScale;

        public override void Execute(ImpactContext context)
        {
            var aoeExplosion = Instantiate(_prefabGO).GetComponent<AOEExplosion>();
            Transform shipTransform = context.ShipStatus.ShipTransform;
            aoeExplosion.SetPositionAndRotation(shipTransform.position, shipTransform.rotation);
            aoeExplosion.MaxScale = context.ShipStatus.ResourceSystem.Resources.Count > _ammoResourceIndex
                ? Mathf.Lerp(_minExplosionScale, _maxExplosionScale, context.ShipStatus.ResourceSystem.Resources[_ammoResourceIndex].CurrentAmount)
                : _maxExplosionScale;
            aoeExplosion.Detonate(context.ShipStatus.Ship);
        }
    }
}
