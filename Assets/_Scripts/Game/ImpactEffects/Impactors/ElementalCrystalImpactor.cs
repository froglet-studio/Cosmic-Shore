using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
{
    public class ElementalCrystalImpactor : CrystalImpactor
    {
        SkimmerCrystalEffectSO[] elementalCrystalShipEffects;

        [Header("Space Collect: move-to-vessel")] [SerializeField]
        float moveToVesselDuration = 3f;

        [SerializeField] bool easeMoveToVessel = true;

        bool isImpacting;

        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (isImpacting) return;
            if (Crystal.IsExploding) return;

            if (impactee is not SkimmerImpactor skimmerImpactor)
                return;

            isImpacting = true;
            WaitForImpact().Forget();

            if (DoesEffectExist(elementalCrystalShipEffects))
            {
                var data = CrystalImpactData.FromCrystal(Crystal);
                foreach (var effect in elementalCrystalShipEffects)
                    effect.Execute(skimmerImpactor, this);
            }

            HandleCrystalVisualAndLifetime(skimmerImpactor);
        }

        void HandleCrystalVisualAndLifetime(SkimmerImpactor skimmerImpactor)
        {
            MoveToVesselThenPlaySpaceCollect(skimmerImpactor).Forget();
        }

        async UniTaskVoid MoveToVesselThenPlaySpaceCollect(SkimmerImpactor skimmerImpactor)
        {
            var vesselStatusTransform = skimmerImpactor.Skimmer.VesselStatus.VesselTransformer.transform;
            var col = Crystal.GetComponent<SphereCollider>();
            if (col) col.enabled = false;

            float dur = Mathf.Max(0.0001f, moveToVesselDuration);
            Vector3 startPos = Crystal.transform.position;
            Quaternion startRot = Crystal.transform.rotation;

            Vector3 targetPos = vesselStatusTransform.position;
            Quaternion targetRot = vesselStatusTransform.rotation;

            float t = 0f;
            while (t < 1f && Crystal != null)
            {
                t += Time.deltaTime / dur;
                float u = Mathf.Clamp01(t);

                if (easeMoveToVessel)
                    u = u * u * (3f - 2f * u); // smoothstep

                Crystal.transform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(startPos, targetPos, u),
                    Quaternion.SlerpUnclamped(startRot, targetRot, u));

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // Snap (just in case)
            if (Crystal)
                Crystal.transform.SetPositionAndRotation(targetPos, targetRot);

            // Now play the collect animation
            PlaySpaceCollectAndDestroy().Forget();
        }

        async UniTaskVoid PlaySpaceCollectAndDestroy()
        {
            float delay = 0.6f;

            var crystalModels = Crystal.CrystalModels;
            if (crystalModels != null)
            {
                foreach (var crystalModelData in crystalModels)
                {
                    crystalModelData.spaceCrystalAnimator.PlayCollect();
                    delay = Mathf.Max(delay, crystalModelData.spaceCrystalAnimator.TotalCollectTime);
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