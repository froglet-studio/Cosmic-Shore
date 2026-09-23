using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
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

        // ── Juice beats. Distinct from the two state events above because a HUD reacts to a
        //    BEAT differently from a VALUE: a gauge follows OnBayChanged every tick, while a
        //    card flash has to fire exactly once, on the frame the thing happened.
        /// <summary>A skim just paid charge into the bay.</summary>
        public event Action OnSkimCharged;
        /// <summary>The bay finished arming a whole bomb — "you may plant".</summary>
        public event Action OnBombArmed;
        /// <summary>A bomb went onto a target.</summary>
        public event Action OnBombPlanted;
        /// <summary>A crystal cashed the board; the argument is how many bombs are cascading.</summary>
        public event Action<int> OnKabloom;

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

            PayCharge();
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

            PayCharge();
        }

        /// <summary>
        /// The one place charge is added, so every skim source produces the same feedback.
        /// Two beats come out of it: the TICK (every paid skim — the card answers, so the
        /// pilot can tell that grazing the reef is doing something) and the ARM (the charge
        /// crossed a whole bomb — the moment the bay becomes usable, which deserves more).
        /// </summary>
        void PayCharge()
        {
            float before = _charge;
            int armedBefore = ArmedBombs;

            _charge = Mathf.Min(Capacity, _charge + config.ChargePerSkimFor(_status));
            if (Mathf.Approximately(before, _charge)) return;   // already at capacity

            PushBayState();
            OnSkimCharged?.Invoke();
            PlayCue(config.SkimChargeEvent);

            if (ArmedBombs > armedBefore)
            {
                OnBombArmed?.Invoke();
                PlayCue(config.BombArmedEvent);
            }
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
                carrierLifeform: null, carrierPlayerName: target.PlayerName);
            if (bomb == null) return false;

            SpendBomb();
            return true;
        }

        /// <summary>Grazed a living creature's body — plant on the creature.</summary>
        public bool TryPlantOnFauna(Fauna fauna)
        {
            if (fauna != null && !fauna.HasLiveBodyPrisms) return false;
            return TryPlantOnLifeform(fauna);
        }

        /// <summary>
        /// Plants on ANY living lifeform — a creature grazed by the skimmer, or a plant the
        /// hull jousted through its heart. Flora and fauna are separate class hierarchies that
        /// meet at <see cref="ILifeFormEntity"/>, which is the level this belongs at: a bomb
        /// does not care what kind of life it is riding.
        /// </summary>
        public bool TryPlantOnLifeform(ILifeFormEntity lifeform)
        {
            if (!CanPlant() || lifeform == null) return false;

            var root = lifeform.GetGameObject();
            if (!root || !root.activeInHierarchy) return false;
            if (!AdmitPlantAttempt(root.GetInstanceID())) return false;
            if (MantaBomb.IsBombed(root)) return false;

            var bomb = MantaBomb.Plant(root, BuildSnapshot(), carrierLifeform: lifeform);
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
            OnBombPlanted?.Invoke();
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
                // Markers and cues are for the pilot flying this ship, not for a host watching
                // its AIs simulate theirs — the predicate the two haptic feels already use.
                LocalHumanPlanter = _status.IsLocalUser && !_status.AutoPilotEnabled,
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

            // Walk a copy: resolving a bomb mutates _planted through NotifyBombResolved.
            var live = new List<MantaBomb>();
            var copy = _planted.ToArray();
            for (int i = 0; i < copy.Length; i++)
            {
                // Skip bombs ALREADY cascading. They stay in _planted until they actually
                // bloom (up to cascadeMaxSeconds), and the Kabloom cooldown is far shorter
                // than that — so a pilot touching a second crystal mid-cascade would
                // otherwise re-count them and be credited their fuses twice.
                if (copy[i] && !copy[i].IsCascading) live.Add(copy[i]);
            }

            int cashed = live.Count;
            if (cashed == 0) return 0;

            // NEAREST FIRST, so the chain reads as a wave rolling outward from the pilot who
            // set it off rather than as an arbitrary order. Sorting by distance from the ship
            // (not the crystal) is what ties the payoff to where the player is looking.
            Vector3 origin = _status.Vessel?.Transform ? _status.Vessel.Transform.position
                                                       : transform.position;
            live.Sort((a, b) =>
                (a.transform.position - origin).sqrMagnitude
                    .CompareTo((b.transform.position - origin).sqrMagnitude));

            // Commit every bomb up front — the whole board is spoken for on this frame, so a
            // fuse cannot expire mid-cascade and pay the small blast by accident — then let
            // each bomb bloom on its own beat. Every one of them holds its marker at full
            // critical while it waits, which is what "watch the fuses turn into explosions"
            // actually looks like.
            for (int i = 0; i < live.Count; i++)
                live[i].CommitToCascade(config ? config.CascadeDelayFor(i, live.Count) : 0f);

            OnKabloom?.Invoke(cashed);
            PlayCue(config ? config.KabloomEvent : default);
            PostKabloomToast(cashed);

            // "Fuses beaten" — bombs the crystal cashed before their timers ran down. Credited
            // on COMMIT, not on bloom: the pilot beat those fuses the moment they touched the
            // crystal, and a round that ends mid-cascade must still pay for them. The owner
            // machine is the only one that KNOWS (bombs are local), so it originates the
            // credit; StatsManager arbitrates server-vs-client exactly like fauna kills.
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

        /// <summary>
        /// One-shot FMOD cue at the ship, local human pilot only. Empty references ship silent
        /// by design — each of these is an authoring slot for the audio owner, never a
        /// borrowed event (the audio law). Resolved through the live singleton so the executor
        /// needs no extra injected field.
        /// </summary>
        void PlayCue(FMODUnity.EventReference reference)
        {
            if (reference.IsNull || _status == null) return;
            if (!_status.IsLocalUser || _status.AutoPilotEnabled) return;

            var audio = AudioSystem.Instance;
            if (!audio) return;

            var t = _status.Vessel?.Transform;
            if (t) audio.PlaySFXEvent(reference, t.position);
            else audio.PlaySFXEvent(reference);
        }

        /// <summary>
        /// Announces the cash-out in the ONE place messages belong — the dedicated toast feed.
        /// A situation a mode has not authored shows nothing, so this is silent everywhere
        /// except Bloomrush, and it never draws anything mid-screen.
        /// </summary>
        void PostKabloomToast(int cashed)
        {
            if (_status == null || !_status.IsLocalUser || _status.AutoPilotEnabled) return;
            GameToastAPI.Post(GameToastSituation.BloomrushKabloom, _status.Domain,
                              _status.PlayerName, cashed.ToString());
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
