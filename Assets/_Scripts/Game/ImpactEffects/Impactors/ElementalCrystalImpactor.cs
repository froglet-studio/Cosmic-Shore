using CosmicShore.Core;
using CosmicShore.Utilities;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ElementalCrystalImpactor : CrystalImpactor
    {
        [SerializeField] ScriptableEventCrystalStats OnCrystalCollected;
        [SerializeField] VesselCrystalEffectSO[] elementalCrystalShipEffects;

        bool isImpacting;

        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (isImpacting) return;
            if (Crystal.IsExploding) return;

            switch (impactee)
            {
                case VesselImpactor shipImpactee:
                {
                    isImpacting = true;
                    WaitForImpact().Forget();

                    if (!TryCollect(shipImpactee))
                        return;

                    if (DoesEffectExist(elementalCrystalShipEffects))
                    {
                        var data = CrystalImpactData.FromCrystal(Crystal);
                        foreach (var effect in elementalCrystalShipEffects)
                            effect.Execute(shipImpactee, data);
                    }

                    PlaySpaceCollectAndDestroy().Forget();
                    break;
                }
            }
        }

        bool TryCollect(VesselImpactor vesselImpactee)
        {
            var shipStatus = vesselImpactee.Vessel.VesselStatus;
            if (!Crystal.CanBeCollected(shipStatus.Domain))
                return false;

            OnCrystalCollected?.Raise(new CrystalStats
            {
                PlayerName = shipStatus.PlayerName,
                Element = Crystal.crystalProperties.Element,
                Value = Crystal.crystalProperties.crystalValue,
            });

            return true;
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
                    if (md != null && md.spaceCrystalAnimator != null)
                    {
                        md.spaceCrystalAnimator.PlayCollect();
                        delay = Mathf.Max(delay, md.spaceCrystalAnimator.TotalCollectTime);
                    }
                }
            }

            await UniTask.WaitForSeconds(delay);
            Destroy(Crystal.gameObject);
        }

        async UniTask WaitForImpact()
        {
            await UniTask.WaitForSeconds(0.5f);
            isImpacting = false;
        }
    }
}
