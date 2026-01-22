using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ElementalCrystalImpactor : CrystalImpactor
    {
        SkimmerCrystalEffectSO[] elementalCrystalShipEffects;

        bool isImpacting;

        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (isImpacting) return;
            if (Crystal.IsExploding) return;

            if (impactee is not SkimmerImpactor skimmerImpactor)
                return;

            isImpacting = true;
            WaitForImpact().Forget();

            // if (!TryCollect(skimmerImpactor))
            //     return;

            if (DoesEffectExist(elementalCrystalShipEffects))
            {
                var data = CrystalImpactData.FromCrystal(Crystal);
                foreach (var effect in elementalCrystalShipEffects)
                    effect.Execute(skimmerImpactor, this);
            }

            HandleCrystalVisualAndLifetime(skimmerImpactor);
        }

        bool TryCollect(SkimmerImpactor skimmerImpactor)
        {
            var shipStatus = skimmerImpactor.Skimmer.VesselStatus;
            return Crystal.CanBeCollected(shipStatus.Domain);
        }

        void HandleCrystalVisualAndLifetime(SkimmerImpactor skimmerImpactor)
        {
            var shipStatus = skimmerImpactor.Skimmer.VesselStatus;
            var element = Crystal.crystalProperties.Element;

            PlaySpaceCollectAndDestroy().Forget();

            // Crystal.Explode(new Crystal.ExplodeParams
            // {
            //     Course = shipStatus.Course,
            //     Speed = shipStatus.Speed,
            //     PlayerName = shipStatus.PlayerName
            // });
            //Crystal.gameObject.SetActive(false);
        }

        async UniTaskVoid PlaySpaceCollectAndDestroy()
        {
            var col = Crystal.GetComponent<SphereCollider>();
            if (col) col.enabled = false;

            float delay = 0.6f;

            var models = Crystal.CrystalModels;
            if (models != null)
            {
                for (int i = 0; i < models.Count; i++)
                {
                    var md = models[i];
                    if (md.model == null) continue;
                    if (md.spaceCrystalAnimator == null) continue;
                    md.spaceCrystalAnimator.PlayCollect();
                    delay = Mathf.Max(delay, md.spaceCrystalAnimator.TotalCollectTime);
                }
            }
        }

        async UniTask WaitForImpact()
        {
            await UniTask.WaitForSeconds(0.5f);
            isImpacting = false;
        }
    }
}