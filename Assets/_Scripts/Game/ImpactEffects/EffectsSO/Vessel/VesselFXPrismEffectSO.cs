using CosmicShore.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShipFXPrismEffect", menuName = "ScriptableObjects/Impact Effects/Vessel/VesselFXPrismEffectSO")]
    public class VesselFXPrismEffectSO : VesselPrismEffectSO
    {
        [SerializeField] private float particleDurationAtSpeedOne = 300f;

        private IShipStatus _shipStatus;
        
        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var shipStatus = impactor.Ship.ShipStatus;
            var trailBlockProperties = prismImpactee.Prism.TrailBlockProperties;
            
            _shipStatus = shipStatus;
            
            DisplaySkimParticleEffectAsync(trailBlockProperties.trailBlock).Forget(); // Fire and forget
        }
        
        async UniTaskVoid DisplaySkimParticleEffectAsync(TrailBlock trailBlock)
        {
            if (trailBlock == null || _shipStatus == null || _shipStatus.ShipTransform == null)
                return;

            var particle = Object.Instantiate(trailBlock.ParticleEffect, trailBlock.transform, true);

            int timer = 0;
            float scaledTime = 0;

            do
            {
                if (_shipStatus.Speed == 0)
                {
                    await UniTask.Yield();
                    continue;
                }

                var distance = trailBlock.transform.position - _shipStatus.ShipTransform.position;
                scaledTime = particleDurationAtSpeedOne / _shipStatus.Speed;

                particle.transform.localScale = new Vector3(1, 1, distance.magnitude);
                particle.transform.SetPositionAndRotation(_shipStatus.ShipTransform.position, Quaternion.LookRotation(distance, trailBlock.transform.up));

                timer++;
                await UniTask.Yield();
            }
            while (timer < scaledTime);

            Destroy(particle);
        }
    }
}
