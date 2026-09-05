using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A weapon that RELOADS BY DESTROYING MASS: every hostile prism this vessel destroys puts a
    /// little ammunition back in the named weapon's tank.
    ///
    /// <para>It replaces the Sparrow's crystal-stocked missile economy — a rocket used to be
    /// bought by flying to an omni crystal, and is now bought by taking the arena apart. The
    /// crystal did not lose its job, it changed jobs: it now grants a temporary elemental-debuff
    /// ward (<see cref="VesselTimedElementalWard"/>), so the two pickups answer two different
    /// questions instead of one.</para>
    ///
    /// <para><b>Why this listens on the DESTROYED CHANNEL rather than hanging off an impact
    /// effect.</b> A rule enforced at one PRODUCER can only ever see that producer — the same
    /// finding the Scarab's ball ceiling records. A Sparrow destroys prisms five different ways:
    /// full-auto bullets, turret-stance prism rounds, a missile's direct hit, a missile's BLAST,
    /// and a hull ram. The blast is the big one and it is the one an effect cannot see at all:
    /// while the spatial index is up, <c>ExplosionImpactor</c> resolves prism damage through the
    /// Burst batch path (<c>PrismSpatialIndex.ProcessExplosionFrame</c>), which dispatches NO
    /// per-prism effects — an <c>ExplosionPrismEffectSO</c> wired for this would run only on the
    /// Physics fallback, i.e. almost never, and would look correct in code the whole time.
    /// <see cref="Prism"/> raises the destroyed channel from ONE place on every route, so
    /// counting what actually happened notices every producer by construction.</para>
    ///
    /// <para><b>Only HOSTILE mass pays.</b> The test is <see cref="StatsManager"/>'s own
    /// (<c>IsFriendlyEnvironmentPrism</c>) so "which mass is worth something to me" has one
    /// answer platform-wide: your own trail, a teammate's trail, and environment mass wearing
    /// your colour are all free of charge, while <see cref="Domains.Blue"/> neutral mass is
    /// hostile to everyone. Without it, a pilot could park and reload off their own ribbon.</para>
    ///
    /// <para><b>Ammo is LOCAL state.</b> Every machine simulates its own vessels' firing (see
    /// <c>SalvoController.RefuelDomainMissiles_ClientRpc</c>, which broadcasts a reload precisely
    /// because the write that matters is the one on each vessel's owner). So this is deliberately
    /// NOT gated on network ownership: it credits whoever the local simulation says destroyed the
    /// prism, and a replica's copy of the tank is inert — nothing reads it, because only the
    /// owner fires.</para>
    /// </summary>
    public class VesselRearmOnPrismDestruction : MonoBehaviour
    {
        [Header("Channel")]
        [Tooltip("Drag EventOnPrismDestroyed.asset — the channel every Prism raises from " +
                 "Prism.Explode, whatever destroyed it. Fail-loud: no null guard by policy.")]
        [SerializeField] ScriptableEventPrismStats onPrismDestroyed;

        [Header("Weapon")]
        [Tooltip("The weapon whose tank this refills. The ammo INDEX is read off this asset " +
                 "(FireGunActionSO.AmmoIndex) rather than authored again here, so the meter the " +
                 "gun spends and the meter this fills can never drift apart.")]
        [SerializeField] FireGunActionSO weaponAction;

        [Header("Payout")]
        [Tooltip("Ammunition added per hostile prism destroyed, in the resource's own units " +
                 "(the Sparrow's missile tank is 0..1 and a skyburst costs 0.5, so 0.02 means " +
                 "25 prisms per missile and 50 for a full rack).")]
        [SerializeField, Min(0f)] float ammoPerPrism = 0.02f;

        [Tooltip("On (default): only prisms that are NOT your own domain's pay. Off: any prism " +
                 "you destroy pays, including your own trail — which is a self-service reload " +
                 "and almost certainly not what you want.")]
        [SerializeField] bool hostileMassOnly = true;

        IVesselStatus _status;
        bool _warnedNoWeapon;

        void Awake()
        {
            _status = GetComponent<VesselStatus>();

            // Warn and degrade, never fail silently: with no VesselStatus there is no player name
            // to match against and no ResourceSystem to pay into, which looks exactly like a
            // weapon that simply never reloads.
            if (_status == null)
                CSDebug.LogWarning($"[{nameof(VesselRearmOnPrismDestruction)}] {name} has no " +
                    "VesselStatus, so it can never rearm anything. Move this component onto the " +
                    "vessel ROOT (the GameObject carrying VesselStatus).", this);
        }

        void OnEnable() => onPrismDestroyed.OnRaised += HandlePrismDestroyed;

        void OnDisable() => onPrismDestroyed.OnRaised -= HandlePrismDestroyed;

        void HandlePrismDestroyed(PrismStats stats)
        {
            if (_status == null || ammoPerPrism <= 0f) return;

            // Whose kill was it? The channel carries every prism death on this machine, including
            // the ones this vessel had nothing to do with.
            var me = _status.PlayerName;
            if (string.IsNullOrEmpty(me) || stats.AttackerName != me) return;

            if (hostileMassOnly &&
                StatsManager.IsFriendlyEnvironmentPrism(_status.Domain, stats.OwnDomain)) return;

            var resources = _status.ResourceSystem;
            if (!resources) return;

            int index = ResolveAmmoIndex();
            if (index < 0 || index >= resources.Resources.Count) return;

            // ChangeResourceAmount clamps to the tank's MaxAmount and raises OnResourceChanged,
            // which is what drives the HUD gauge — never write CurrentAmount directly.
            resources.ChangeResourceAmount(index, ammoPerPrism);
        }

        /// <summary>
        /// The tank to fill, single-sourced from the weapon that spends it. Reported once when
        /// unwired: an un-named weapon is a component that quietly does nothing.
        /// </summary>
        int ResolveAmmoIndex()
        {
            if (weaponAction) return weaponAction.AmmoIndex;

            if (!_warnedNoWeapon)
            {
                _warnedNoWeapon = true;
                CSDebug.LogWarning($"[{nameof(VesselRearmOnPrismDestruction)}] '{name}' has no " +
                    "weaponAction assigned, so it does not know which resource to refill and " +
                    "will never rearm. Assign the FireGunActionSO this vessel fires.", this);
            }
            return -1;
        }
    }
}
