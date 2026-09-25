using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
using CosmicShore.Utility;
using FMODUnity;
using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent's SOLID FUEL PELLETS (Time). One press burns one pellet - a fixed amount of the
    /// vessel's fuel resource - and every burn is its OWN event with its own duration. Press again
    /// while a pellet is still burning and a second one lights beside it; the two overlap, and each
    /// adds the same increment of speed, so four pellets burning at once is four times the effect of
    /// one. Fuel refills at a fixed rate (the resource's authored <c>resourceGainRate</c>, ticked by
    /// <see cref="ResourceSystem"/>) up to a capacity of exactly four pellets.
    ///
    /// <para><b>The fuel resource IS the magazine.</b> There is no charge counter and no reload:
    /// how many pellets you hold is <c>floor(fuel / pelletCost)</c>, and a spent pellet comes back
    /// on the resource's own clock. That is what makes it a resource to MANAGE - burn one now and
    /// keep three in hand, or dump all four for a 4x overlap and fly on empty. A magazine-and-reload
    /// version replaced it for a while (four charges that came back only after the LAST burn ended
    /// plus a seven-second wait) and read as a cooldown ability rather than as a fuel tank; it is
    /// retired, not kept beside this.</para>
    ///
    /// <para><b>A release does NOT end a burn.</b> The pellet is spent at the press and burns for
    /// its full duration whatever the button does afterwards. The retired version cancelled every
    /// burn on release, which made overlap impossible for a human (you cannot press again without
    /// releasing) and zeroed every AI burn outright (<c>AIPilot</c> authors <c>Duration: 0</c>, so
    /// its StopAction arrived the same frame as its StartAction).</para>
    ///
    /// <para>Burns are tracked as END TIMES and retired in <see cref="Update"/>, never as one
    /// cancellable task per burn: nothing about a burn can be interrupted except the whole set, and
    /// a cancelled task that skips its own tail is exactly how a boost multiplier gets stranded on
    /// (vessel skill rule 13). Every teardown path goes through <see cref="ClearBurns"/>, which
    /// always writes the status back.</para>
    /// </summary>
    public class ConsumeBoostActionExecutor : ShipActionExecutorBase
    {
        [Inject] AudioSystem audioSystem;

        [Header("Config")]
        [Tooltip("The pellet config this executor burns. Wired here so the fuel HUD can read the " +
                 "pellet size before the first press; a press also adopts whichever asset fired it.")]
        [SerializeField] ConsumeBoostActionSO config;

        [Header("Events")]
        [SerializeField] private ScriptableEventBoostChanged boostChanged;
        [SerializeField] public ScriptableEventNoParam OnMiniGameTurnEnd;

        [Header("Audio")]
        [Tooltip("FMOD event played when a pellet ignites. Empty falls back to the shared " +
                 "BoostActivate category until the audio owner authors a dedicated pellet sound.")]
        [SerializeField] EventReference pelletIgniteEvent;

        /// <summary>A pellet ignited: (burn duration in seconds, pellets now burning).</summary>
        public event Action<float, int> OnPelletBurned;

        /// <summary>The number of pellets burning changed (a burn lit or burned out).</summary>
        public event Action<int> OnBurningCountChanged;

        IVesselStatus _status;
        ResourceSystem _resources;

        readonly List<float> _burnEndTimes = new();

        public ConsumeBoostActionSO Config => config;
        public int BurningCount => _burnEndTimes.Count;

        /// <summary>Which resource holds the fuel, or -1 when no config is wired yet.</summary>
        public int FuelResourceIndex => config ? config.ResourceIndex : -1;

        /// <summary>Fuel one pellet costs, as a fraction of the resource. 0 when unconfigured.</summary>
        public float PelletCost => config ? config.ResourceCost : 0f;

        /// <summary>
        /// How many pellets a FULL tank holds - derived from the resource's own capacity and the
        /// pellet size, never authored twice. 4 on the shipped Serpent (1.0 / 0.25).
        /// </summary>
        public int PelletCapacity
        {
            get
            {
                float cost = PelletCost;
                if (cost <= 0f || !TryGetFuel(out var fuel)) return 0;
                return Mathf.Max(0, Mathf.FloorToInt(fuel.MaxAmount / cost + 0.0001f));
            }
        }

        /// <summary>Pellets in the tank right now, fractional - 2.6 is two ready and one 60% refilled.</summary>
        public float PelletsHeld
        {
            get
            {
                float cost = PelletCost;
                if (cost <= 0f || !TryGetFuel(out var fuel)) return 0f;
                return fuel.CurrentAmount / cost;
            }
        }

        void OnEnable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDisable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
            ClearBurns();
        }

        void OnTurnEndOfMiniGame() => ClearBurns();

        public override void Initialize(IVesselStatus shipStatus)
        {
            // A vessel swap re-runs Initialize on a live component, so drop whatever the previous
            // pilot had burning before adopting the new status.
            ClearBurns();

            _status = shipStatus;
            _resources = shipStatus?.ResourceSystem;

            if (_status != null)
            {
                _status.BoostMultiplier = 1f;
                _status.IsBoosting = false;
            }
        }

        /// <summary>Burn one pellet, if the tank holds one.</summary>
        public void Consume(ConsumeBoostActionSO so, IVesselStatus status)
        {
            if (!so || status == null) return;
            if (_status is { IsTranslationRestricted: true }) return;

            if (config != so) config = so;

            float cost = so.ResourceCost;
            if (cost <= 0f)
            {
                CSDebug.LogWarning($"[ConsumeBoost] '{so.name}' authors resourceCost {cost}; a pellet " +
                                   "must cost fuel or the tank is bottomless. Burn refused.");
                return;
            }

            if (!TryGetFuel(out var fuel)) return;
            if (fuel.CurrentAmount + 0.0001f < cost) return;   // no whole pellet in the tank

            _resources.ChangeResourceAmount(so.ResourceIndex, -cost);

            // Time -> burn duration, read at use time (x1 at rest, x1.6 at Time 10).
            float duration = Mathf.Max(0.05f, so.BoostDuration * so.TimeDurationMultiplier(_status));
            _burnEndTimes.Add(Time.time + duration);

            PlayIgniteSound();
            ApplyMultiplier();

            OnPelletBurned?.Invoke(duration, _burnEndTimes.Count);
            OnBurningCountChanged?.Invoke(_burnEndTimes.Count);
        }

        /// <summary>
        /// The button was released. Deliberately nothing: a lit pellet burns out on its own.
        /// </summary>
        public void Release() { }

        void Update()
        {
            if (_burnEndTimes.Count == 0) return;

            float now = Time.time;
            int before = _burnEndTimes.Count;
            _burnEndTimes.RemoveAll(end => end <= now);
            if (_burnEndTimes.Count == before) return;

            ApplyMultiplier();
            OnBurningCountChanged?.Invoke(_burnEndTimes.Count);
        }

        /// <summary>
        /// Every burning pellet adds the same increment. The authored <c>boostMultiplier</c> is
        /// what ONE pellet produces (3 = three times cruise), so the increment is that minus one
        /// and n pellets give <c>1 + (m - 1) * n</c>: 3x, 5x, 7x, 9x on the shipped asset - the
        /// speed a burn ADDS is exactly n times the speed one burn adds.
        /// </summary>
        void ApplyMultiplier()
        {
            if (_status == null) return;

            int burning = _burnEndTimes.Count;
            if (burning > 0)
            {
                float perPellet = config ? Mathf.Max(0f, config.BoostMultiplier - 1f) : 2f;
                _status.IsBoosting = true;
                _status.BoostMultiplier = 1f + perPellet * burning;
            }
            else
            {
                _status.IsBoosting = false;
                _status.BoostMultiplier = 1f;
            }

            RaiseBoostChanged();
        }

        void ClearBurns()
        {
            bool hadBurns = _burnEndTimes.Count > 0;
            _burnEndTimes.Clear();

            if (_status != null)
            {
                _status.IsBoosting = false;
                _status.BoostMultiplier = 1f;
            }

            if (!hadBurns) return;
            RaiseBoostChanged();
            OnBurningCountChanged?.Invoke(0);
        }

        void RaiseBoostChanged()
        {
            if (!boostChanged || _status == null) return;

            // MaxMultiplier = 0 -> the HUD uses its own config.
            boostChanged.Raise(new BoostChangedPayload
            {
                BoostMultiplier = _status.BoostMultiplier,
                MaxMultiplier = 0f,
                SourceDomain = Domains.Blue,
                VesselStatus = _status
            });
        }

        bool TryGetFuel(out Resource fuel)
        {
            fuel = null;
            if (!_resources || !config) return false;

            int index = config.ResourceIndex;
            if ((uint)index >= (uint)_resources.Resources.Count) return false;

            fuel = _resources.Resources[index];
            return fuel != null;
        }

        void PlayIgniteSound()
        {
            var audio = audioSystem ? audioSystem : AudioSystem.Instance;
            if (!audio) return;

            if (!pelletIgniteEvent.IsNull)
                audio.PlaySFXEvent(pelletIgniteEvent, transform.position);
            else
                audio.PlayGameplaySFX(GameplaySFXCategory.BoostActivate);
        }
    }
}
