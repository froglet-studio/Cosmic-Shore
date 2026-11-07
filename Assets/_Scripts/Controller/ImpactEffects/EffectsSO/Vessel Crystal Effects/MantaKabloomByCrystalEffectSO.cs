using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// KABLOOM - the payoff beat of the Manta's whole kit (MANTA_STING_KABLOOM.md §3). Collecting an OMNI crystal sets off
    /// EVERY bomb this Manta has planted, big, and every bloom is bigger again for each bomb on the board
    /// (<see cref="MantaStingConfigSO.OmniScaleMultiplier"/>) - press your luck: each extra bomb is more to win and more
    /// to lose to the fuse or a prism. Plus one extra DOMAINED blast at the Manta's own position, wearing the signature
    /// flower bloom. (Elemental crystals do not cash the board - they leave decoys, MantaDecoyByCrystalEffectSO.)
    ///
    /// Machine model: the vessel-side omni-crystal dispatch is a lockstep broadcast (NetworkVesselImpactor's
    /// Server→ClientRpc round trip) PLUS a local fallback, so this effect runs on every peer and can run twice within
    /// milliseconds on some - the static per-impactor cooldown dedupes that. The EXTRA blast runs everywhere; the
    /// PLANTED BOARD goes off only where the bomb ledger lives - the planter's simulation machine - and each bomb relays
    /// its own bloom to peers.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MantaKabloomByCrystalEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Crystal/MantaKabloomByCrystalEffectSO")]
    public class MantaKabloomByCrystalEffectSO : VesselCrystalEffectSO
    {
        [Header("Config (the bomb system's single tuning surface)")]
        [SerializeField] MantaStingConfigSO stingConfig;

        [Header("The Manta's own blast")]
        [Tooltip("Spawned once at the Manta on every Kabloom — author the plain blast plus " +
                 "the flower-bloom AOE (AOEFlowerCreation), the Manta's signature visual. " +
                 "Domained: it never eats the pilot's own prisms.")]
        [SerializeField] AOEExplosion[] selfBlastPrefabs;

        [Header("Anti-Spam")]
        [Tooltip("Minimum seconds between Kablooms from the same vessel — dedupes the " +
                 "broadcast+local double-dispatch and multi-collider hulls alike.")]
        [SerializeField] float kabloomCooldown = 0.15f;

        // Keyed by instance ID so destroyed impactors are never retained across scene loads.
        static readonly Dictionary<int, float> LastKabloomByImpactor = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => LastKabloomByImpactor.Clear();

        public override void Execute(VesselImpactor vesselImpactor, CrystalImpactData data)
        {
            if (vesselImpactor == null || vesselImpactor.Vessel == null || !stingConfig) return;

            int id = vesselImpactor.GetInstanceID();
            float now = Time.time;
            if (LastKabloomByImpactor.TryGetValue(id, out float last) && now - last < kabloomCooldown)
                return;
            LastKabloomByImpactor[id] = now;

            var status = vesselImpactor.Vessel.VesselStatus;

            // The planted board — only where this Manta's bomb ledger actually lives. The
            // executor credits the cashed fuses ("fuses beaten") itself, server/client aware.
            if (MantaStingActionExecutor.TryGetFor(status, out var bay))
                bay.DetonateBoard(MantaDetonation.OmniCrystal);

            // The extra domained blast + flower, at the Manta, on EVERY machine (lockstep):
            // spared allies always — this one is the celebration, not the weapon.
            float scale = stingConfig.KabloomSelfBlastScale;
            if (scale <= 0f || selfBlastPrefabs == null || selfBlastPrefabs.Length == 0) return;

            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain = status.Domain,
                Vessel = status.Vessel,
                MaxScale = scale,
                OverrideMaterial = stingConfig.BloomMaterial
                    ? stingConfig.BloomMaterial : status.AOEExplosionMaterial,
                AnnonymousExplosion = false,
                SpawnPosition = status.ShipTransform.position,
                SpawnRotation = status.ShipTransform.rotation,
                AffectSelfOverride = false,
            };

            ExplosionHelper.CreateExplosion(selfBlastPrefabs, init, vesselImpactor.DIContainer);
        }
    }
}
