using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Rhino energy-sword vs crystal effect (RHINO_ENERGY_SWORD.md). When the sword collects an
    /// elemental crystal it spends ALL stored energy: it spawns an <see cref="AOEExplosion"/> at
    /// the crystal and bursts the blade in all three dimensions — BOTH scaled by the energy at the
    /// moment of the hit (full energy = full-size explosion + full-size burst; less energy scales
    /// both down). The 3D burst, the blade flash, the local camera shake, and the energy drain are
    /// owned by the sword state (<see cref="IRhinoSwordState.TriggerCrystalBurst"/>); this effect
    /// only sizes and spawns the explosion and kicks off the burst.
    /// </summary>
    [CreateAssetMenu(
        fileName = "RhinoSwordCrystalBurstEffect",
        menuName = "ScriptableObjects/Impact Effects/Skimmer - Crystal/RhinoSwordCrystalBurstEffectSO")]
    public sealed class RhinoSwordCrystalBurstEffectSO : SkimmerCrystalEffectSO
    {
        [Header("Explosion (MaxScale lerps min→max by energy consumed, 0..1)")]
        [SerializeField] private AOEExplosion[] aoePrefabs;
        [SerializeField] private float minExplosionScale = 60f;
        [SerializeField] private float maxExplosionScale = 400f;
        [Tooltip("Optional explosion material override; falls back to the vessel's AOEExplosionMaterial.")]
        [SerializeField] private Material overrideMaterial;

        public override void Execute(SkimmerImpactor impactor, CrystalImpactor impactee)
        {
            if (impactor == null || impactor.Skimmer == null || impactee == null) return;

            var status = impactor.Skimmer.VesselStatus;
            if (status == null) return;

            var sword = impactor.Skimmer.SwordState;
            float energy = sword != null ? Mathf.Clamp01(sword.Energy01) : 0f;

            SpawnExplosion(impactor, status, impactee, energy);

            // 3D size burst + flash + shake scaled by that same energy, then consume ALL of it.
            sword?.TriggerCrystalBurst();
        }

        void SpawnExplosion(SkimmerImpactor impactor, IVesselStatus status, CrystalImpactor impactee, float energy01)
        {
            if (aoePrefabs == null || aoePrefabs.Length == 0) return;

            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain           = status.Domain,
                Vessel              = status.Vessel,
                MaxScale            = Mathf.Lerp(minExplosionScale, maxExplosionScale, energy01),
                OverrideMaterial    = overrideMaterial ? overrideMaterial : status.AOEExplosionMaterial,
                AnnonymousExplosion = false,
                SpawnPosition       = impactee.transform.position,
                SpawnRotation       = impactee.transform.rotation,
            };

            ExplosionHelper.CreateExplosion(aoePrefabs, init, impactor.DIContainer);
        }
    }
}
