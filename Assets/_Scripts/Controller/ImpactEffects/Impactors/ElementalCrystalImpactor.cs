using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using System.Linq;
namespace CosmicShore.Gameplay
{
    public class ElementalCrystalImpactor : CrystalImpactor
    {
        SkimmerCrystalEffectSO[] elementalCrystalShipEffects;

        [Header("Space Collect: move-to-vessel")] 
        [SerializeField] float moveToVesselDuration = 3f;
        [SerializeField] bool easeMoveToVessel = true;

        bool isImpacting;
        private bool _hasBeenCollected;
        public static event Action<string> OnCrystalCollected;
        
        void OnEnable() 
        {
            _hasBeenCollected = false;
        }
        
        protected override void AcceptImpactee(IImpactor impactee)
        {
            if (_hasBeenCollected) return;
            if (isImpacting) return;
            if (Crystal.IsExploding) return;
            if (impactee is not SkimmerImpactor skimmerImpactor) return;

            isImpacting = true;
            _hasBeenCollected = true;

            // Powerup buff: collecting an elemental crystal raises the collector's matching
            // element level, which the elemental ship UI reflects via
            // ResourceSystem.OnElementLevelChange → ElementalBarsView. This is the reliable
            // buff point: elemental crystals are collected by the SKIMMER, which disables the
            // collider just below, so the VesselImpactor crystal-buff path (which keys off the
            // collider) can't fire. The crystal's authored Element selects which resource is
            // buffed (e.g. Mass for the brittlestar's dropped crystal).
            var resourceSystem = skimmerImpactor.Skimmer?.VesselStatus?.ResourceSystem;
            resourceSystem?.IncrementLevel(Crystal.crystalProperties.Element);

            var col = Crystal.GetComponent<Collider>();
            if (col) col.enabled = false;

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
            if (Crystal == null || skimmerImpactor?.Skimmer?.VesselStatus == null) return;

            var vesselStatus = skimmerImpactor.Skimmer.VesselStatus;
            var vesselTransform = vesselStatus.VesselTransformer.transform;
            OnCrystalCollected?.Invoke(vesselStatus.PlayerName);
            float dur = Mathf.Max(0.0001f, moveToVesselDuration);
            Vector3 startPos = Crystal.transform.position;
            Quaternion startRot = Crystal.transform.rotation;

            float t = 0f;
            while (t < 1f)
            {
                if (Crystal == null || vesselTransform == null) break;

                t += Time.deltaTime / dur;
                float u = Mathf.Clamp01(t);

                if (easeMoveToVessel)
                    u = u * u * (3f - 2f * u); // smoothstep

                // Lerp towards the moving vessel
                Crystal.transform.SetPositionAndRotation(
                    Vector3.LerpUnclamped(startPos, vesselTransform.position, u),
                    Quaternion.SlerpUnclamped(startRot, vesselTransform.rotation, u));

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            // Play animation logic
            await PlaySpaceCollectAndDestroy();
        }

        async UniTask PlaySpaceCollectAndDestroy()
        {
            if (Crystal == null) return;

            float delay = 0.6f;
            var crystalModels = Crystal.CrystalModels;
            
            if (crystalModels != null)
            {
                foreach (var crystalModelData in crystalModels)
                {
                    if (crystalModelData?.spaceCrystalAnimator != null)
                    {
                        crystalModelData.spaceCrystalAnimator.PlayCollect();
                        delay = Mathf.Max(delay, crystalModelData.spaceCrystalAnimator.TotalCollectTime);
                    }
                }
            }

            // Wait for animation then destroy
            await UniTask.WaitForSeconds(delay);
            //if (Crystal) Crystal.DestroyCrystal();
        }

        async UniTask WaitForImpact()
        {
            await UniTask.WaitForSeconds(0.5f);
            isImpacting = false;
        }
    }
}