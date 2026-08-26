using System;
using System.Collections.Generic;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Manta's bomb bay — STING's state. Skimming charges it (the skimmer effects call
    /// <see cref="AddSkimCharge"/>), grazing plants from it (<see cref="TryPlantOnVessel"/> /
    /// <see cref="TryPlantOnFauna"/>), and a crystal cashes the whole board
    /// (<see cref="DetonateAllPlanted"/> — KABLOOM). There is no button anywhere in this file:
    /// that is the vessel's accessibility thesis.
    ///
    /// PASSIVE ability ⇒ the config SO is wired DIRECTLY here (no input binding exists for
    /// <c>CollectBoundActions</c> to resolve — the Dolphin crystal-seeding rule).
    ///
    /// SIMULATION AUTHORITY: bombs are local objects, like projectiles. Exactly one machine
    /// simulates a given Manta's bay — the machine that owns the vessel (each client for its
    /// own pilot, the host for AIs), or the only machine there is on the non-networked
    /// single-player path. Every mutating entry point gates on <see cref="IsSimAuthority"/>;
    /// peers learn about blooms through <see cref="MantaBombNetworkRelay"/> so the chain
    /// reads on every screen.
    /// </summary>
    public sealed class MantaStingActionExecutor : ShipActionExecutorBase
    {
        [Header("Config (wired directly — Sting has no input event)")]
        [SerializeField] MantaStingConfigSO config;

        [Header("Refs")]
        [Tooltip("For blast DI + the peer relay. Empty resolves from the vessel root.")]
        [SerializeField] VesselImpactor vesselImpactor;

        [Tooltip("ResourceSystem slot mirroring the bay for generic gauges (armed/capacity, " +
                 "normalized). -1 disables the mirror.")]
        [SerializeField] int bayResourceIndex = 0;

        [Inject] GameDataSO gameData;
        [Inject] StatsManager statsManager;

        /// <summary>Bay state changed (charge, armed count, capacity).</summary>
        public event Action OnBayChanged;
        /// <summary>The planted-bomb set changed (plant, bloom, shed, carrier death).</summary>
        public event Action OnPlantedChanged;

        static readonly Dictionary<IVesselStatus, MantaStingActionExecutor> Registry = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registry.Clear();

        IVesselStatus _status;
        MantaBombNetworkRelay _relay;
        float _charge;
        readonly List<MantaBomb> _planted = new();
        readonly Dictionary<int, float> _lastChargeTimeByPrism = new();
        readonly Dictionary<int, float> _lastPlantAttemptByTarget = new();

        public MantaStingConfigSO Config => config;
        public int ArmedBombs => Mathf.FloorToInt(_charge + 1e-4f);
        public float Charge => _charge;
        public int Capacity => config
            ? config.CapacityForChargeLevel(CurrentChargeLevel())
            : 1;
        public IReadOnlyList<MantaBomb> PlantedBombs => _planted;

        /// <summary>The soonest-burning fuse among planted bombs, or -1 with none planted.</summary>
        public float ShortestFuseRemaining
        {
            get
            {
                float best = -1f;
                for (int i = 0; i < _planted.Count; i++)
                {
                    var bomb = _planted[i];
                    if (!bomb) continue;
                    if (best < 0f || bomb.FuseRemaining < best) best = bomb.FuseRemaining;
                }
                return best;
            }
        }

        /// <summary>Resolves the bay simulating <paramref name="status"/>, when one exists here.</summary>
        public static bool TryGetFor(IVesselStatus status, out MantaStingActionExecutor executor)
        {
            executor = null;
            return status != null && Registry.TryGetValue(status, out executor) && executor;
        }

        /// <summary>
        /// Is THIS machine the one that simulates this vessel's bombs? Owner machine on the
        /// networked paths (host for AIs — <c>IsNetworkOwner</c>, never <c>IsLocalUser</c>);
        /// unconditionally yes when no network session is running (the single-player spawn
        /// path never network-spawns its Player, so IsNetworkOwner is structurally false there).
        /// </summary>
        public static bool IsSimAuthority(IVesselStatus status)
        {
            var player = status?.Player;
            if (player == null) return false;
            if (player.IsNetworkOwner) return true;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm == null || !nm.IsListening;
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            // Detach-first: a vessel swap re-runs Initialize on live components.
            if (_status != null) Registry.Remove(_status);

            _status = shipStatus;
            if (_status != null) Registry[_status] = this;

            if (!vesselImpactor) vesselImpactor = GetComponentInParent<VesselImpactor>();
            _relay = GetComponentInParent<MantaBombNetworkRelay>();

            if (!config)
                CSDebug.LogError("[MantaSting] No MantaStingConfigSO wired on the executor — " +
                                 "the Manta's bomb bay is dead. Wire it on Manta.prefab's " +
                                 "MantaStingActionExecutor.");

            _charge = 0f;
            _planted.Clear();
            _lastChargeTimeByPrism.Clear();
            _lastPlantAttemptByTarget.Clear();
            PushBayState();
        }

        void OnDisable()
        {
            if (_status != null) Registry.Remove(_status);
        }

        int CurrentChargeLevel() =>
            _status?.ResourceSystem ? _status.ResourceSystem.GetLevel(CosmicShore.Data.Element.Charge) : 0;

        // ── Charging ─────────────────────────────────────────────────────────

        /// <summary>
        /// One skim tick from <paramref name="source"/>. Charge-scaled, per-prism throttled
        /// (a skimmer slides along a prism and re-enters it), capped at the live capacity.
        /// </summary>
        public void AddSkimCharge(Prism source)
        {
            if (!config || _status == null || !IsSimAuthority(_status)) return;

            if (source)
            {
                int id = source.GetInstanceID();
                if (_lastChargeTimeByPrism.TryGetValue(id, out float last)
                    && Time.time - last < config.PerPrismChargeCooldown)
                    return;
                _lastChargeTimeByPrism[id] = Time.time;
            }

            float before = _charge;
            _charge = Mathf.Min(Capacity, _charge + config.ChargePerSkimFor(_status));
            if (!Mathf.Approximately(before, _charge)) PushBayState();
        }

        /// <summary>Vessel-graze charge tick (no prism identity to throttle on — uses the target).</summary>
        public void AddVesselSkimCharge(IVesselStatus target)
        {
            if (!config || _status == null || !IsSimAuthority(_status)) return;
            if (target?.Vessel?.Transform == null) return;

            int id = target.Vessel.Transform.GetInstanceID();
            if (_lastChargeTimeByPrism.TryGetValue(id, out float last)
                && Time.time - last < config.PerPrismChargeCooldown)
                return;
            _lastChargeTimeByPrism[id] = Time.time;

            float before = _charge;
            _charge = Mathf.Min(Capacity, _charge + config.ChargePerSkimFor(_status));
            if (!Mathf.Approximately(before, _charge)) PushBayState();
        }

        // ── Planting ─────────────────────────────────────────────────────────

        /// <summary>
        /// Grazed an enemy vessel — plant, if the bay holds a bomb and the target is clean.
        /// One bomb per target: an already-bombed vessel is DENIED to everyone, which is the
        /// competitive hook, so the failed attempt spends nothing.
        /// </summary>
        public bool TryPlantOnVessel(IVesselStatus target)
        {
            if (!CanPlant() || target?.Vessel?.Transform == null) return false;
            if (ReferenceEquals(target, _status)) return false;
            if (target.Domain == _status.Domain) return false;
            if (_status.Speed < target.Speed + config.PlantSpeedMargin) return false;

            var root = target.Vessel.Transform.gameObject;
            if (!AdmitPlantAttempt(root.GetInstanceID())) return false;
            if (MantaBomb.IsBombed(root)) return false;

            var bomb = MantaBomb.Plant(root, BuildSnapshot(),
                carrierFauna: null, carrierPlayerName: target.PlayerName);
            if (bomb == null) return false;

            SpendBomb();
            return true;
        }

        /// <summary>Grazed a living creature's body — plant on the creature.</summary>
        public bool TryPlantOnFauna(Fauna fauna)
        {
            if (!CanPlant() || fauna == null || !fauna.HasLiveBodyPrisms) return false;
            if (!AdmitPlantAttempt(fauna.gameObject.GetInstanceID())) return false;
            if (MantaBomb.IsBombed(fauna.gameObject)) return false;

            var bomb = MantaBomb.Plant(fauna.gameObject, BuildSnapshot(), carrierFauna: fauna);
            if (bomb == null) return false;

            SpendBomb();
            return true;
        }

        bool CanPlant() =>
            config && _status != null && IsSimAuthority(_status) && ArmedBombs >= 1;

        bool AdmitPlantAttempt(int targetId)
        {
            if (_lastPlantAttemptByTarget.TryGetValue(targetId, out float last)
                && Time.time - last < 0.5f)
                return false;
            _lastPlantAttemptByTarget[targetId] = Time.time;
            return true;
        }

        void SpendBomb()
        {
            _charge = Mathf.Max(0f, _charge - 1f);
            PushBayState();
        }

        /// <summary>
        /// Per-use snapshot at PLANT time: Contagion (Charge 5), friendly fire (Space 5 turns
        /// it off), the Space bloom radius, and the fuse (mode override wins — Bloomrush's
        /// intensity ladder rides <see cref="MantaBombRules.FuseSecondsOverride"/>).
        /// </summary>
        MantaBombSnapshot BuildSnapshot()
        {
            var handler = _status.ElementalAbilityHandler;
            bool contagion = handler && handler.IsUpgradeActive(CosmicShore.Data.Element.Charge);
            bool sparesAllies = handler && handler.IsUpgradeActive(CosmicShore.Data.Element.Space);

            return new MantaBombSnapshot
            {
                Config = config,
                GameData = gameData,
                PlanterName = _status.PlayerName,
                PlanterDomain = _status.Domain,
                PlanterVessel = _status.Vessel,
                Owner = this,
                DiContainer = vesselImpactor ? vesselImpactor.DIContainer : null,
                Contagion = contagion,
                AffectSelf = !sparesAllies,
                SpaceScaleMultiplier = config.BlastScaleMultiplierFor(_status),
                FuseSeconds = MantaBombRules.FuseSecondsOverride ?? config.FuseSeconds,
            };
        }

        // ── Kabloom ──────────────────────────────────────────────────────────

        /// <summary>
        /// Detonates EVERY planted bomb simultaneously at the crystal (medium) size. Returns
        /// how many bombs were cashed — the "fuses beaten" count the stats layer credits.
        /// </summary>
        public int DetonateAllPlanted()
        {
            if (_status == null || !IsSimAuthority(_status)) return 0;

            int cashed = 0;
            // Detonation mutates _planted through NotifyBombResolved — walk a copy.
            var copy = _planted.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                var bomb = copy[i];
                if (!bomb) continue;
                bomb.Detonate(byCrystal: true);
                cashed++;
            }

            // "Fuses beaten" — bombs the crystal cashed before their timers ran down. The
            // owner machine is the only one that KNOWS (bombs are local), so it originates
            // the credit; StatsManager arbitrates server-vs-client exactly like fauna kills.
            if (cashed > 0 && statsManager)
                statsManager.FusesBeaten(_status.PlayerName, cashed);

            return cashed;
        }

        // ── Bomb bookkeeping (called by MantaBomb) ───────────────────────────

        public void NotifyBombPlanted(MantaBomb bomb)
        {
            if (bomb && !_planted.Contains(bomb)) _planted.Add(bomb);
            OnPlantedChanged?.Invoke();
        }

        public void NotifyBombResolved(MantaBomb bomb, bool detonated, bool byCrystal)
        {
            _planted.Remove(bomb);
            OnPlantedChanged?.Invoke();
        }

        /// <summary>Broadcasts one bloom to every peer so the chain reads on all screens.</summary>
        public void RelayBloomToPeers(Vector3 position, float maxScale, bool affectSelf)
        {
            if (_relay && _relay.IsSpawned)
                _relay.BroadcastBloom(position, maxScale, affectSelf);
        }

        void PushBayState()
        {
            if (bayResourceIndex >= 0 && _status?.ResourceSystem != null
                && bayResourceIndex < _status.ResourceSystem.Resources.Count)
            {
                var meter = _status.ResourceSystem.Resources[bayResourceIndex];
                _status.ResourceSystem.SetResourceAmount(bayResourceIndex,
                    meter.MaxAmount * (Capacity > 0 ? (float)ArmedBombs / Capacity : 0f));
            }
            OnBayChanged?.Invoke();
        }
    }
}
