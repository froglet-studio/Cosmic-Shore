using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Butterfly's Scale Dust, delivered by a BLAST instead of the capsule: every prism the
    /// omni-crystal bloom engulfs gets the same one-of-three outcome the dust gives it — own
    /// domain grows / turns dangerous / shields, opposing is destroyed / shrunk / stolen
    /// (<c>R_VesselActions/BUTTERFLY.md</c> §3.3).
    ///
    /// <para><b>It owns no outcome table.</b> It holds the dust's own
    /// <see cref="SkimmerScaleDustPrismEffectSO"/> asset and calls its <c>Apply</c>, so the bloom
    /// and the capsule roll from one set of weights, one deterministic per-prism hash and one
    /// Diamond Dust gate — retune the dust and the bloom moves with it.</para>
    ///
    /// <para>The only thing the blast supplies is the DESTROY outcome's striker velocity: the
    /// blast's own impact vector at the prism (<see cref="ExplosionImpactor.BlastImpactVector"/>),
    /// so debris leaves along the wavefront rather than along the pilot's course.</para>
    ///
    /// <para>Reaches prisms only through <see cref="ExplosionImpactor"/>'s prism-effect sweep,
    /// which runs for a blast whose prefab has <c>affectsPrisms</c> OFF — the bloom's case, so the
    /// blast's generic damage/shield pass never touches the mass this effect is deciding.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ExplosionScaleDustPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Explosion - Prism/ExplosionScaleDustPrismEffectSO")]
    public class ExplosionScaleDustPrismEffectSO : ExplosionPrismEffectSO
    {
        [Tooltip("The dust's own effect asset (ButterflyScaleDustPrismEffect). Its weights, " +
                 "grow/shrink fractions, debris tuning and Diamond Dust gate ARE this effect's — " +
                 "nothing is restated here.")]
        [SerializeField] SkimmerScaleDustPrismEffectSO dust;

        public override void Execute(ExplosionImpactor impactor, PrismImpactor prismImpactee)
        {
            if (!dust)
            {
                CSDebug.LogError($"[{name}] has no Scale Dust asset assigned — the blast cannot " +
                                 "dust the mass it engulfs. Wire ButterflyScaleDustPrismEffect.");
                return;
            }

            var status = impactor != null ? impactor.SourceVessel?.VesselStatus : null;
            var prism = prismImpactee != null ? prismImpactee.Prism : null;
            if (status == null || !prism) return;

            // Captured BEFORE the outcome: a destroy or steal can retire/reparent the prism.
            Vector3 at = prism.transform.position;
            var outcome = dust.Apply(prismImpactee, status, impactor.BlastImpactVector(at));

            // The bloom draws what it changed as the capsule's own dust (ButterflyBloomDust on
            // AOEButterflyBloom.prefab), the way the capsule voices it through ButterflyDustField.
            if (impactor.TryGetComponent(out ButterflyBloomDust bloomDust))
                bloomDust.OnDusted(at, outcome);
        }
    }
}
