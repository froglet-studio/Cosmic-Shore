using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Unity.Netcode;
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
    /// <para><b>THE TANK MUST AGREE ACROSS PEERS, and this is why the component is networked.</b>
    /// An ability press is replicated as a PRESS, not as a decision: the owner sends it to the
    /// server and <c>R_VesselActionHandler.SendButtonPressed_ClientRpc</c> replays it on EVERY
    /// peer, where <c>FireGunActionExecutor.Fire</c> reads THAT peer's own tank and returns early
    /// if it is short. So a replica whose tank has drifted low silently spawns no missile — no
    /// model, no tail, no proximity fuze and no warhead blast — and a victim on that machine
    /// takes none of the debuff the shooter's machine says landed.
    ///
    /// <para>Spending is convergent (every peer spends on the same replayed press). EARNING is
    /// not: fauna and flora are spawned per-peer from local <c>Random</c> rolls
    /// (<c>CellNetworkSync</c> — the very reason <c>Player.ReportFaunaKill_ServerRpc</c> exists),
    /// so two machines genuinely destroy different mass, and destruction ordering races diverge
    /// even over identical mass. The crystal refill this replaced was self-healing by accident —
    /// it was an unfiltered ClientRpc doing a set-to-FULL, so every peer resynchronised on every
    /// pickup. Removing it turned a self-correcting drift into a ratcheting one.</para>
    ///
    /// <para>So the owner publishes its tank as an idempotent SET, rate-limited to
    /// <see cref="syncIntervalSeconds"/> and only when the value actually moved — the same shape
    /// as <c>SalvoController.RefuelDomainMissiles_ClientRpc</c>, which broadcasts a set-to-full
    /// for exactly this reason. Local crediting stays, so the common case (trail mass, which IS
    /// convergent) needs no round trip and has no latency; the broadcast is the correction. It
    /// errs deliberately toward replicas being slightly OVER: a replica never initiates a shot,
    /// so an over-full replica is harmless while a short one eats the missile.</para></para>
    /// </summary>
    public class VesselRearmOnPrismDestruction : NetworkBehaviour
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

        [Header("Networking")]
        [Tooltip("How often (seconds) the OWNER may publish its tank to the other peers. This is " +
                 "a correction, not the mechanism — local crediting already keeps convergent mass " +
                 "in step — so it is rate-limited to keep one Sparrow to at most one RPC per " +
                 "interval. 0 disables the broadcast and accepts per-peer drift.")]
        [SerializeField, Min(0f)] float syncIntervalSeconds = 1f;

        // Below this the tanks are close enough that publishing would only spend a packet. Well
        // under a skyburst's 0.5 cost, so a divergence that could change whether a peer draws the
        // shot is always published.
        const float AmmoSyncEpsilon = 0.01f;

        IVesselStatus _status;
        bool _warnedNoWeapon;
        bool _subscribed;
        float _nextSyncTime;
        float _lastPublished = -1f;

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

        void OnEnable()
        {
            if (_subscribed) return;
            onPrismDestroyed.OnRaised += HandlePrismDestroyed;
            _subscribed = true;
        }

        void OnDisable()
        {
            if (!_subscribed) return;
            onPrismDestroyed.OnRaised -= HandlePrismDestroyed;
            _subscribed = false;
        }

        void HandlePrismDestroyed(PrismStats stats)
        {
            if (_status == null || ammoPerPrism <= 0f) return;

            // Whose kill was it? The channel carries every prism death on this machine, including
            // the ones this vessel had nothing to do with — so the cheap rejections come first.
            if (string.IsNullOrEmpty(stats.AttackerName)) return;

            // Read Player, NOT PlayerName. The interface's PlayerName getter LOGS A WARNING when
            // Player is null, and Player is only assigned in VesselController.Initialize while
            // this handler is live from the moment the GameObject activates — on a client the
            // vessel replicates first and the pair is resolved reactively, so during that window
            // every prism death anywhere on the machine would emit a warning, hundreds per blast.
            // CLAUDE.md: nothing per-contact gets a log at all.
            var player = _status.Player;
            if (player == null) return;

            var me = player.Name;
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

        void Update()
        {
            if (syncIntervalSeconds <= 0f) return;
            if (!IsSpawned || !IsOwner) return;              // one publisher: the owner
            if (Time.time < _nextSyncTime) return;

            if (!TryReadAmmo(out float current)) return;
            if (_lastPublished >= 0f && Mathf.Abs(current - _lastPublished) < AmmoSyncEpsilon) return;

            _nextSyncTime = Time.time + syncIntervalSeconds;
            _lastPublished = current;
            PublishAmmo_ServerRpc(current);
        }

        bool TryReadAmmo(out float amount)
        {
            amount = 0f;
            if (_status == null) return false;

            var resources = _status.ResourceSystem;
            if (!resources) return false;

            int index = ResolveAmmoIndex();
            if (index < 0 || index >= resources.Resources.Count) return false;

            amount = resources.Resources[index].CurrentAmount;
            return true;
        }

        [ServerRpc]
        void PublishAmmo_ServerRpc(float amount) => SetAmmo_ClientRpc(amount);

        /// <summary>
        /// Idempotent SET of this vessel's tank on every peer that is not its owner. A set rather
        /// than a delta on purpose: a delta has to arrive exactly once to be right, while a set
        /// converges however many arrive, in any order, and however many were dropped.
        /// </summary>
        [ClientRpc]
        void SetAmmo_ClientRpc(float amount)
        {
            if (IsOwner) return;                              // the owner IS the source
            if (_status == null) return;

            var resources = _status.ResourceSystem;
            if (!resources) return;

            int index = ResolveAmmoIndex();
            if (index < 0 || index >= resources.Resources.Count) return;

            resources.SetResourceAmount(index, amount);
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
