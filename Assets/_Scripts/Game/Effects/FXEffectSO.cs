using CosmicShore.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "FXImpactEffect", menuName = "ScriptableObjects/Impact Effects/FXImpactEffectSO")]
    public class FXEffectSO : ImpactEffectSO, ITrailBlockImpactEffect
    {
        [SerializeField]
        float particleDurationAtSpeedOne = 300f;

        IShipStatus _shipStatus;

        public void Execute(ImpactEffectData data, TrailBlockProperties trailBlockProperties)
        {
            _shipStatus = data.ThisShipStatus;
            DisplaySkimParticleEffectAsync(trailBlockProperties.trailBlock).Forget(); // Fire and forget
        }

        async UniTaskVoid DisplaySkimParticleEffectAsync(TrailBlock trailBlock)
        {
            if (trailBlock == null || _shipStatus == null || _shipStatus.ShipTransform == null)
                return;

            var particle = Object.Instantiate(trailBlock.ParticleEffect);
            particle.transform.SetParent(trailBlock.transform);

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
