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
    /// The Manta's bomb kit - STING's state (the 2026-11 overhaul, MANTA_STING_KABLOOM.md). Skimming gathers ENERGY
    /// (<see cref="AddSkimCharge"/>; the Space card shows it), a graze spends one bomb of it to plant
    /// (<see cref="TryPlantOnVessel"/> / <see cref="TryPlantOnLifeform"/>), and every planted bomb rides ONE shared fuse
    /// (the Charge card shows it) that each new bomb resets - so the whole board always goes off together:
    ///  • the fuse runs out - every bomb blooms small (<see cref="MantaDetonation.Fuse"/>);
    ///  • the Manta hits a prism - every bomb blooms small AND all the energy is lost (<see cref="OnPrismImpact"/>);
    ///  • an omni crystal - every bomb blooms big, times a multiplier for how many there are
    ///    (<see cref="DetonateBoard"/> with <see cref="MantaDetonation.OmniCrystal"/>): press your luck.
    /// It also keeps the Manta's decoys (MASS): one elemental decoy, and at Mass 5 one omni decoy beside it.
    ///
    /// PASSIVE ability ⇒ the config SO is wired DIRECTLY here (no input binding exists for <c>CollectBoundActions</c>).
    ///
    /// SIMULATION AUTHORITY: bombs are local objects, like projectiles. Exactly one machine simulates a given Manta's
    /// bombs and energy - the machine that owns the vessel, or the only machine there is single-player. Every bomb and
    /// energy entry point gates on <see cref="IsSimAuthority"/>; peers learn about blooms through
    /// <see cref="MantaBombNetworkRelay"/>. Decoys are the exception: they are spawned by a crystal effect that is
    /// already broadcast, so every peer keeps its own.
    /// </summary>
    public sealed class MantaStingActionExecutor : ShipActionExecutorBase
    {
        [Header("Config (wired directly — Sting has no input event)")]
        [SerializeField] MantaStingConfigSO config;

        [Header("Refs")]
        [Tooltip("For blast DI + the peer relay. Empty resolves from the vessel root.")]
        [SerializeField] VesselImpactor vesselImpactor;

        [Tooltip("ResourceSystem slot mirroring the ENERGY for generic gauges (energy/capacity, normalized). " +
                 "-1 disables the mirror.")]
        [SerializeField] int bayResourceIndex = 0;

        [Inject] GameDataSO gameData;
        [Inject] StatsManager statsManager;

        /// <summary>Energy changed (gathered, spent, lost).</summary>
        public event Action OnBayChanged;
        /// <summary>The planted-bomb set changed, or the shared fuse moved.</summary>
        public event Action OnPlantedChanged;

        // ── Juice beats (a HUD reacts to a BEAT differently from a VALUE).
        /// <summary>A skim just paid energy.</summary>
        public event Action OnSkimCharged;
        /// <summary>The energy crossed a whole bomb - "you may plant".</summary>
        public event Action OnBombArmed;
        /// <summary>A bomb went onto a target (and the shared fuse reset).</summary>
        public event Action OnBombPlanted;
        /// <summary>An omni crystal cashed the board; the argument is how many bombs are cascading.</summary>
        public event Action<int> OnKabloom;
        /// <summary>The board went off, for any reason; the count is how many bombs.</summary>
        public event Action<MantaDetonation, int> OnBoardDetonated;
        /// <summary>A rival took one of this Manta's decoys.</summary>
        public event Action OnDecoySprung;

        static readonly Dictionary<IVesselStatus, MantaStingActionExecutor> Registry = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Registry.Clear();

        IVesselStatus _status;
        MantaBombNetworkRelay _relay;
        float _energy;
        float _fuseDeadline;
        float _fuseLength = 1f;
        readonly List<MantaBomb> _planted = new();
        readonly Dictionary<int, float> _lastChargeTimeByPrism = new();
        readonly Dictionary<int, float> _lastPlantAttemptByTarget = new();
        MantaDecoyCrystal _elementalDecoy;
        MantaDecoyCrystal _omniDecoy;

        public MantaStingConfigSO Config => config;
        public IVesselStatus Status => _status;

        /// <summary>Whole bombs' worth of energy - how many plants the Manta can make right now.</summary>
        public int ArmedBombs => Mathf.FloorToInt(_energy + 1e-4f);
        /// <summary>Energy, in bombs (fractional).</summary>
        public float Energy => _energy;
        /// <summary>Most energy the Manta can hold, in bombs.</summary>
        public int Capacity => config ? config.EnergyCapacity : 1;
        /// <summary>True while an elemental decoy this Manta left is standing (the HUD's Mass card).</summary>
        public bool HasElementalDecoy => _elementalDecoy;
        /// <summary>True while an omni decoy this Manta left is standing (Mass 5, the HUD's Mass card).</summary>
        public bool HasOmniDecoy => _omniDecoy;
        public IReadOnlyList<MantaBomb> PlantedBombs => _planted;

        /// <summary>When the shared fuse runs out (Time.time). Meaningless with nothing planted.</summary>
        public float FuseDeadline => _fuseDeadline;
        /// <summary>The full length the shared fuse was last reset to, seconds.</summary>
        public float FuseLength => _fuseLength;

        /// <summary>Seconds left on the shared fuse, or -1 with nothing planted.</summary>
        public float FuseRemaining => LiveBombCount() > 0 ? Mathf.Max(0f, _fuseDeadline - Time.time) : -1f;

        /// <summary>The shared fuse as a 0-1 fraction of its full length (1 = just reset), or 0 with nothing planted.</summary>
        public float Fuse01 => LiveBombCount() > 0 ? Mathf.Clamp01((_fuseDeadline - Time.time) / Mathf.Max(0.01f, _fuseLength)) : 0f;

        /// <summary>Kept for readers of the old name: the shared fuse's remaining seconds.</summary>
        public float ShortestFuseRemaining => FuseRemaining;

        /// <summary>Resolves the bay simulating <paramref name="status"/>, when one exists here.</summary>
        public static bool TryGetFor(IVesselStatus status, out MantaStingActionExecutor executor)
        {
            executor = null;
            return status != null && Registry.TryGetValue(status, out executor) && executor;
        }

        /// <summary>
        /// Is THIS machine the one that simulates this vessel's bombs? Owner machine on the networked paths (host for
        /// AIs - <c>IsNetworkOwner</c>, never <c>IsLocalUser</c>); unconditionally yes when no network session is
        /// running (the single-player spawn path never network-spawns its Player).
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
                                 "the Manta's bomb kit is dead. Wire it on Manta.prefab's " +
                                 "MantaStingActionExecutor.");

            _energy = 0f;
            _planted.Clear();
            _lastChargeTimeByPrism.Clear();
            _lastPlantAttemptByTarget.Clear();
            RetireDecoys();
            PushBayState();
        }

        void OnDisable()
        {
            if (_status != null) Registry.Remove(_status);
            RetireDecoys();
        }

        void Update()
        {
            // The shared fuse: the whole board goes off together, so the executor fires it, never a bomb.
            if (_status == null || !config || _planted.Count == 0) return;
            if (Time.time < _fuseDeadline || !IsSimAuthority(_status)) return;
            if (LiveBombCount() == 0) return;   // every bomb is already cascading off an omni crystal

            PlayCue(config.FuseExpiredEvent);
            DetonateBoard(MantaDetonation.Fuse);
        }

        int LiveBombCount()
        {
            int n = 0;
            for (int i = 0; i < _planted.Count; i++)
                if (_planted[i] && !_planted[i].IsCascading) n++;
            return n;
        }

        // ── Energy ───────────────────────────────────────────────────────────

        /// <summary>
        /// One skim tick from <paramref name="source"/>: pays energy, per-prism throttled (a skimmer slides along a
        /// prism and re-enters it), capped at the capacity.
        /// </summary>
        public void AddSkimCharge(Prism source)
        {
            if (!config || _status == null || !IsSimAuthority(_status)) return;

            if (source)
            {
                int id = source.GetInstanceID();
                if (_lastChargeTimeByPrism.TryGetValue(id, out float last)
                    && Time.time - last < config.PerPrismEnergyCooldown)
                    return;
                _lastChargeTimeByPrism[id] = Time.time;
            }

            PayEnergy();
        }

        /// <summary>Vessel-graze energy tick (no prism identity to throttle on - uses the target).</summary>
        public void AddVesselSkimCharge(IVesselStatus target)
        {
            if (!config || _status == null || !IsSimAuthority(_status)) return;
            if (target?.Vessel?.Transform == null) return;

            int id = target.Vessel.Transform.GetInstanceID();
            if (_lastChargeTimeByPrism.TryGetValue(id, out float last)
                && Time.time - last < config.PerPrismEnergyCooldown)
                return;
            _lastChargeTimeByPrism[id] = Time.time;

            PayEnergy();
        }

        void PayEnergy()
        {
            float before = _energy;
            int armedBefore = ArmedBombs;

            _energy = Mathf.Min(Capacity, _energy + config.EnergyPerSkim);
            if (Mathf.Approximately(before, _energy)) return;   // already full

            PushBayState();
            OnSkimCharged?.Invoke();
            PlayCue(config.SkimChargeEvent);

            if (ArmedBombs > armedBefore)
            {
                OnBombArmed?.Invoke();
                PlayCue(config.BombArmedEvent);
            }
        }

        /// <summary>
        /// The Manta's hull hit a prism (<see cref="MantaDetonateByPrismEffectSO"/>): every planted bomb goes off now,
        /// at the small size, and every scrap of energy is lost.
        /// </summary>
        public void OnPrismImpact()
        {
            if (!config || _status == null || !IsSimAuthority(_status)) return;

            bool hadEnergy = _energy > 0f;
            int detonated = DetonateBoard(MantaDetonation.PrismImpact);
            if (detonated > 0 || hadEnergy) PlayCue(config.PrismImpactDetonationEvent);

            if (hadEnergy)
            {
                _energy = 0f;
                PushBayState();
            }
        }

        // ── Planting ─────────────────────────────────────────────────────────

        /// <summary>
        /// Grazed an enemy vessel - plant, if the Manta holds a bomb of energy and the target is clean. One bomb per
        /// target: an already-bombed vessel is DENIED to everyone, so the failed attempt spends nothing.
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

            var bomb = MantaBomb.Plant(root, BuildSnapshot());
            if (bomb == null) return false;

            SpendBomb();
            return true;
        }

        /// <summary>Grazed a living creature's body - plant on the creature.</summary>
        public bool TryPlantOnFauna(Fauna fauna)
        {
            if (fauna != null && !fauna.HasLiveBodyPrisms) return false;
            return TryPlantOnLifeform(fauna);
        }

        /// <summary>Plants on ANY living lifeform - a creature grazed by the skimmer, or a plant jousted through its heart.</summary>
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
            _energy = Mathf.Max(0f, _energy - 1f);
            PushBayState();
            OnBombPlanted?.Invoke();

            // The plant knock: one of the Manta's two haptics (the other is the hull-hit thud). Local human only.
            if (_status.IsLocalUser && !_status.AutoPilotEnabled) HapticController.PlayPlant();
        }

        /// <summary>Per-use snapshot at PLANT time: Contagion (Charge 5) and the shared fuse as it stands.</summary>
        MantaBombSnapshot BuildSnapshot()
        {
            var handler = _status.ElementalAbilityHandler;
            bool contagion = handler && handler.IsUpgradeActive(CosmicShore.Data.Element.Charge);

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
                FuseDeadline = _fuseDeadline,
                // Markers and cues are for the pilot flying this ship, not for a host watching its AIs.
                LocalHumanPlanter = _status.IsLocalUser && !_status.AutoPilotEnabled,
            };
        }

        // CHARGE: the planter's Charge at this plant sets how long the WHOLE board's fuse now runs.
        void ResetFuse()
        {
            _fuseLength = config ? config.FuseSecondsFor(_status) : 20f;
            _fuseDeadline = Time.time + _fuseLength;
            for (int i = 0; i < _planted.Count; i++)
                if (_planted[i]) _planted[i].SyncFuse(_fuseDeadline);
        }

        // ── The board goes off ──────────────────────────────────────────────

        /// <summary>
        /// Sets off EVERY planted bomb for <paramref name="reason"/>. A fuse or a prism impact blooms the whole board
        /// at once, small; an omni crystal cascades it nearest-first, big, every bloom times the bomb multiplier.
        /// Returns how many bombs went off.
        /// </summary>
        public int DetonateBoard(MantaDetonation reason)
        {
            if (_status == null || !IsSimAuthority(_status) || !config) return 0;

            // Walk a copy: resolving a bomb mutates _planted through NotifyBombResolved. Bombs already cascading off
            // an earlier omni crystal are spoken for and are not counted twice.
            var live = new List<MantaBomb>();
            var copy = _planted.ToArray();
            for (int i = 0; i < copy.Length; i++)
                if (copy[i] && !copy[i].IsCascading) live.Add(copy[i]);

            int count = live.Count;
            if (count == 0) return 0;

            if (reason == MantaDetonation.OmniCrystal)
            {
                // NEAREST FIRST, so the chain rolls outward from the pilot who set it off.
                Vector3 origin = _status.Vessel?.Transform ? _status.Vessel.Transform.position : transform.position;
                live.Sort((a, b) =>
                    (a.transform.position - origin).sqrMagnitude
                        .CompareTo((b.transform.position - origin).sqrMagnitude));

                float scale = config.KabloomBlastScale * config.OmniScaleMultiplier(count);
                for (int i = 0; i < live.Count; i++)
                    live[i].CommitToCascade(config.CascadeDelayFor(i, live.Count), scale);

                OnKabloom?.Invoke(count);
                PlayCue(config.KabloomEvent);
                PostKabloomToast(count);

                // "Fuses beaten" - credited on COMMIT: the pilot beat the fuse the moment they touched the crystal.
                if (statsManager) statsManager.FusesBeaten(_status.PlayerName, count);
            }
            else
            {
                for (int i = 0; i < live.Count; i++)
                    if (live[i]) live[i].Detonate(config.FuseBlastScale, config.BloomGrowSeconds, reason);
            }

            OnBoardDetonated?.Invoke(reason, count);
            OnPlantedChanged?.Invoke();
            return count;
        }

        /// <summary>Kept for the Kabloom effect's old call: the omni cash-out.</summary>
        public int DetonateAllPlanted() => DetonateBoard(MantaDetonation.OmniCrystal);

        // ── Bomb bookkeeping (called by MantaBomb) ───────────────────────────

        public void NotifyBombPlanted(MantaBomb bomb, bool resetsFuse)
        {
            if (bomb && !_planted.Contains(bomb)) _planted.Add(bomb);
            // Every new PLANT resets the shared fuse; a bomb that only MOVES (a dropped heart, a pickup) keeps the time
            // the board already had.
            if (resetsFuse) ResetFuse();
            else if (bomb) bomb.SyncFuse(_fuseDeadline);
            OnPlantedChanged?.Invoke();
        }

        public void NotifyBombResolved(MantaBomb bomb, bool detonated)
        {
            _planted.Remove(bomb);
            OnPlantedChanged?.Invoke();
        }

        /// <summary>Broadcasts one bloom to every peer so the chain reads on all screens.</summary>
        public void RelayBloomToPeers(Vector3 position, float maxScale, float growSeconds)
        {
            if (_relay && _relay.IsSpawned)
                _relay.BroadcastBloom(position, maxScale, growSeconds);
        }

        // ── Decoys (MASS) ────────────────────────────────────────────────────

        /// <summary>
        /// A crystal this Manta collected leaves a decoy behind where it was. Elemental crystals always do; the omni
        /// only at Mass 5. One of each at a time: a new decoy of a kind pops the previous one.
        /// </summary>
        public void LeaveDecoy(in CrystalImpactData data)
        {
            if (!config || _status == null || !data.Origin.Valid) return;

            bool omni = data.Element == Element.Omni;
            if (omni)
            {
                var handler = _status.ElementalAbilityHandler;
                if (!handler || !handler.IsUpgradeActive(Element.Mass)) return;
            }
            else if (data.Element is not (Element.Charge or Element.Mass or Element.Space or Element.Time))
            {
                return;
            }

            var decoy = MantaDecoyCrystal.Spawn(this, config, data, config.DecoyLifetimeFor(_status));
            if (!decoy) return;

            if (omni)
            {
                if (_omniDecoy) _omniDecoy.Pop();
                _omniDecoy = decoy;
            }
            else
            {
                if (_elementalDecoy) _elementalDecoy.Pop();
                _elementalDecoy = decoy;
            }
        }

        /// <summary>One of this Manta's decoys was taken (it morphs itself).</summary>
        public void NotifyDecoySprung(MantaDecoyCrystal decoy)
        {
            if (decoy == _omniDecoy) _omniDecoy = null;
            if (decoy == _elementalDecoy) _elementalDecoy = null;
            OnDecoySprung?.Invoke();
            PlayCue(config ? config.DecoySprungEvent : default);
        }

        /// <summary>A decoy left on its own (lifetime, popped, torn down).</summary>
        public void NotifyDecoyGone(MantaDecoyCrystal decoy)
        {
            if (decoy == _omniDecoy) _omniDecoy = null;
            if (decoy == _elementalDecoy) _elementalDecoy = null;
        }

        void RetireDecoys()
        {
            if (_omniDecoy) _omniDecoy.Pop();
            if (_elementalDecoy) _elementalDecoy.Pop();
            _omniDecoy = null;
            _elementalDecoy = null;
        }

        /// <summary>The DI container blasts spawned for this Manta resolve through.</summary>
        public Reflex.Core.Container DiContainer => vesselImpactor ? vesselImpactor.DIContainer : null;

        // ── Feedback ─────────────────────────────────────────────────────────

        /// <summary>One-shot FMOD cue at the ship, local human pilot only. Empty references ship silent by design.</summary>
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

        /// <summary>Announces the cash-out in the dedicated toast feed (silent wherever a mode authored nothing).</summary>
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
                    meter.MaxAmount * (Capacity > 0 ? Mathf.Clamp01(_energy / Capacity) : 0f));
            }
            OnBayChanged?.Invoke();
        }
    }
}
