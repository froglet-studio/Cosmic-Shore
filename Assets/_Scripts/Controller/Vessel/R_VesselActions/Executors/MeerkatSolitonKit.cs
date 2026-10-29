using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Meerkat's four abilities beyond the bare wormhole: <b>Garrett's kit</b> (Garrett's design, approved by Yash
    /// 2026-10-28; <c>R_VesselActions/MEERKAT.md</c> §10), ported from the Vessel Studio's Meerkat page, Kit "Garrett's kit"
    /// (<c>Docs/Studios/VesselStudio/meerkat.html</c>; the arithmetic is <see cref="MeerkatSolitonMath"/>). The wormhole IS the
    /// soliton: a standing tunnel anyone may ride (any Meerkat: <c>MeerkatWormholeExecutor.TryRideAnySoliton</c>).
    /// <list type="bullet">
    /// <item><b>Space, the soliton</b> (RT, the wormhole's own press): its surface area (round 23's <c>areaL0</c> /
    /// <c>areaL10</c>, <see cref="MeerkatWormholeMath.SurfaceAreaThroat"/>) and its swallow and fire (<see cref="MeerkatSwallow"/>)
    /// are the artifact's round-23 port, not this kit's. The kit adds <b>L5 Steal</b> (<see cref="Stamp"/> sets the bit,
    /// <c>MeerkatSwallow.Swallow</c> steals): what it swallows becomes the Meerkat's domain the moment its run starts.</item>
    /// <item><b>Time, passive</b>: the ride through EVERY soliton (its own or anyone's) rushes faster, the throat's multiplier
    /// × Time's <c>rush</c> (<see cref="RideMid"/>). <b>L5 Boost out</b>: every soliton is left at <c>boostOut</c> × the exit
    /// (<see cref="RideExit"/>).</item>
    /// <item><b>Charge, Sentinel mode</b> (Y, <see cref="MeerkatSentinelModeActionSO"/>): while on, each new soliton leaves a
    /// sentinel at its way in (<see cref="MeerkatSentinels"/>), which guards it, zaps rivals in its guard and shields its own
    /// domain's prisms there; a soliton with a sentinel stands as long as its sentinel. The pool is <c>sentinelPool</c> (3);
    /// <b>L5 Six sentinels</b>: <c>sentinelPoolL5</c> (6).</item>
    /// <item><b>Mass, passive</b>: each new soliton sets down an exit portal, six prisms on a hexagon at the way out, sized by
    /// Mass's <c>portalSize</c> (<see cref="MeerkatExitPortal"/>). <b>L5 Danger portal</b>: they are danger prisms.</item>
    /// </list>
    ///
    /// <para><b>Where it runs.</b> The owner (the simulating machine, <see cref="MantaStingActionExecutor.IsSimAuthority"/>)
    /// decides each soliton's kit when it forms it (<see cref="Stamp"/>: a sentinel or not, the L5 bits, the portal's size)
    /// and publishes it with the tunnel (<see cref="MeerkatWormholeNetworkRelay"/>, protocol 23). Every peer then lays the same
    /// portal, stands the same sentinels and steals the same swallowed prisms from the same tunnels, on its own prisms (prisms
    /// are per-peer, as every mover of the prism-motion fundamental is: <c>Docs/PRISM_MOTION.md</c> §4). A zap's petals are
    /// decided on the Meerkat's owner and settled on the victim's (<see cref="ElementalTransfer.ApplyAllAuthoritative"/>),
    /// as every scored hit is.</para>
    ///
    /// <para>Not built (and why): the sentinel SEATS and the HOP need dynamic crew seats, which the crew system does not have
    /// yet; a sentinel is always AI-guarded. A non-Meerkat hull cannot ride a soliton yet (its transformer has no ride).</para>
    /// </summary>
    public sealed class MeerkatSolitonKit : ShipActionExecutorBase
    {
        [Header("Config")]
        [Tooltip("The Meerkat's config (the same asset as the wormhole's): the kit's numbers are its Kit sections.")]
        [SerializeField] MeerkatWormholeConfigSO config;

        [Tooltip("The game data the players (the rivals a sentinel zaps) are read from.")]
        [SerializeField] GameDataSO gameData;

        /// <summary>The kit bits a soliton carries, decided by its owner (<see cref="Stamp"/>) and replicated with it.</summary>
        public const byte KitSentinel = 1, KitSixSentinels = 2, KitDangerPortal = 4, KitSteal = 8;

        IVesselStatus _status;
        MeerkatWormholeExecutor _wormhole;
        readonly MeerkatSentinels _sentinels = new();
        readonly MeerkatExitPortal _portal = new();
        readonly List<ShipActionSO> _bindScratch = new();
        InputEvents? _sentinelInput;
        float _aiPressAt = -10f;
        float _firedSoundAt = -10f, _zapSoundAt = -10f;

        public MeerkatWormholeConfigSO Config => config;
        public GameDataSO GameData => gameData;
        public IVesselStatus Status => _status;
        public MeerkatWormholeExecutor Wormhole => _wormhole;

        /// <summary>Charge: while true, each new soliton leaves a sentinel (Y toggles it).</summary>
        public bool SentinelMode { get; private set; }
        /// <summary>Sentinels standing now, and the pool they come from (the HUD's Charge card).</summary>
        public int SentinelCount => _sentinels.Count;
        public int SentinelPoolNow => MeerkatSolitonMath.PoolSize(SixSentinelsUnlocked, config ? config.SentinelPool : 3,
            config ? config.SentinelPoolL5 : 6);
        /// <summary>How many of the last exit portal's six prisms still stand (the HUD's Mass card).</summary>
        public int PortalStanding => _portal.Standing;

        R_VesselElementalAbilityHandler Abilities => _status?.ElementalAbilityHandler;
        bool IsAuthority => _status != null && MantaStingActionExecutor.IsSimAuthority(_status);

        // ------------------------------------------------------------------ the four L5 gates (the replicated unlock bits)

        /// <summary>Charge L5, Six sentinels.</summary>
        bool SixSentinelsUnlocked => Abilities && Abilities.IsUpgradeActive(Element.Charge);
        /// <summary>Mass L5, Danger portal (a silenced Meerkat's portal comes out plain, SILENCE.md).</summary>
        bool DangerPortalUnlocked => Abilities && Abilities.IsUpgradeActive(Element.Mass) && !VesselSilence.IsSilenced(_status);
        /// <summary>Space L5, Steal.</summary>
        bool StealUnlocked => Abilities && Abilities.IsUpgradeActive(Element.Space);
        /// <summary>Time L5, Boost out.</summary>
        bool BoostOutUnlocked => Abilities && Abilities.IsUpgradeActive(Element.Time);

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _wormhole = GetComponent<MeerkatWormholeExecutor>();
            SentinelMode = false;
            _sentinelInput = null;
            _aiPressAt = -10f;
            _sentinels.Clear(this, false);
            _portal.Clear();
            if (config == null)
                CSDebug.LogError($"[Meerkat] {name}: MeerkatSolitonKit has no MeerkatWormholeConfig - the kit does nothing. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Meerkat/MeerkatWormholeConfig.asset on the prefab.");
        }

        // ------------------------------------------------------------------ hooks MeerkatWormholeExecutor calls

        /// <summary>Time: the ride's throat multiplier with the rush (every soliton, its own or anyone's).</summary>
        public float RideMid(float rideMidMultiplier) =>
            config && _status != null ? MeerkatSolitonMath.RideMid(rideMidMultiplier, config.Rush.EvaluateLive(_status)) : rideMidMultiplier;

        /// <summary>Time L5: the ride's exit multiplier with Boost out.</summary>
        public float RideExit(float rideExitMultiplier) =>
            config ? MeerkatSolitonMath.RideExit(rideExitMultiplier, BoostOutUnlocked, config.BoostOut) : rideExitMultiplier;

        /// <summary>
        /// The owner, as it forms <paramref name="t"/>: what this soliton carries (a sentinel or not, the L5 bits, the
        /// portal's size), so every peer does the same with it.
        /// </summary>
        public void Stamp(MeerkatWormholeExecutor.Tunnel t)
        {
            if (!config || t == null) return;
            byte kit = 0;
            if (SentinelMode) kit |= KitSentinel;
            if (SixSentinelsUnlocked) kit |= KitSixSentinels;
            if (DangerPortalUnlocked) kit |= KitDangerPortal;
            if (StealUnlocked) kit |= KitSteal;
            t.Kit = kit;
            t.PortalSize = Mathf.Max(0.05f, config.PortalSize.EvaluateReplicated(_status));
        }

        /// <summary>Every peer, once a soliton has formed (the owner's own, or its published copy): Mass sets down its exit
        /// portal; a sentinel stands at its way in if its owner had Sentinel mode on (the page's <c>kitSolitonMade</c>).</summary>
        public void OnFormed(MeerkatWormholeExecutor.Tunnel t)
        {
            if (!config || t == null) return;
            if (_portal.Lay(this, t)) PlayAt(config.PortalEvent, t.At(t.L, out _));
            if ((t.Kit & KitSentinel) != 0)
            {
                int pool = MeerkatSolitonMath.PoolSize((t.Kit & KitSixSentinels) != 0, config.SentinelPool, config.SentinelPoolL5);
                _sentinels.Place(this, t, pool);
            }
        }

        // ------------------------------------------------------------------ Charge: the toggle

        /// <summary>Y: Sentinel mode on or off. Off, no new sentinels are placed; the standing ones stay.</summary>
        public void ToggleSentinelMode() => SentinelMode = !SentinelMode;

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (!config || _status == null || !_wormhole) return;
            float dt = Time.deltaTime;
            _sentinels.Sweep(this);
            _sentinels.Zap(this, dt);
            _portal.Prune();
            if (IsAuthority && _status.AutoPilotEnabled) AutopilotSentinelMode();
        }

        /// <summary>The AI (input only, /vessel-ai): it turns Sentinel mode on through the replicated press path, as a person
        /// presses Y; the rest of the kit is passive or automatic.</summary>
        void AutopilotSentinelMode()
        {
            if (SentinelMode || !config.AiWormhole || Time.time - _aiPressAt < 1f) return;
            _aiPressAt = Time.time;   // the press round-trips through the server: do not press again before it lands
            var handler = _status.ActionHandler;
            if (handler && ResolveSentinelInput(handler, out var ie))
            {
                handler.PerformShipControllerActionsReplicated(ie);
                handler.StopShipControllerActionsReplicated(ie);
                return;
            }
            ToggleSentinelMode();
        }

        /// <summary>Which input event the Sentinel mode action is bound to on THIS vessel, retried until it succeeds (the
        /// binding maps fill after executors initialize, vessel contract rule 6).</summary>
        bool ResolveSentinelInput(R_VesselActionHandler handler, out InputEvents ie)
        {
            if (_sentinelInput.HasValue) { ie = _sentinelInput.Value; return true; }
            foreach (InputEvents candidate in System.Enum.GetValues(typeof(InputEvents)))
            {
                _bindScratch.Clear();
                handler.CollectBoundActions(candidate, _bindScratch);
                foreach (var action in _bindScratch)
                    if (action is MeerkatSentinelModeActionSO)
                    {
                        _sentinelInput = candidate;
                        _bindScratch.Clear();
                        ie = candidate;
                        return true;
                    }
            }
            _bindScratch.Clear();
            ie = default;
            return false;
        }

        // ------------------------------------------------------------------ a zap on a rival hull

        /// <summary>
        /// A sentinel's zap on a rival hull, under the LOCKED PvP rule: the ONLY thing it does is take petals, as a scored hit.
        /// Decided on exactly one machine, the one that owns this Meerkat; admitted through <see cref="CombatHitDrain.TryAdmit"/>
        /// as a Bullet (a warded victim is neither scored on nor robbed; an admitted hit SILENCES the victim for what it is
        /// worth, <c>SILENCE.md</c>; a silenced Meerkat's zaps are refused); then Charge's <c>zapPetals</c> on each element,
        /// a sub-petal tick through the economy's accrual (only whole petals ever leave), EJECTED as free crystals.
        /// </summary>
        internal void ZapVessel(IVesselStatus victim, Vector3 from)
        {
            var at = victim.Transform.position;
            if (Time.time - _zapSoundAt >= 0.18f)   // the page rate-limits the zap's sound to about five a second
            {
                _zapSoundAt = Time.time;
                PlayAt(config.ZapEvent, at);
            }
            if (!ElementalTransfer.IsDecidedHere(_status)) return;
            if (_status.Player is { IsNetworkOwner: false }) return;
            if (!CombatHitDrain.TryAdmit(victim, _status, CombatHitClass.Bullet, config.ZapCooldownSeconds,
                    ElementalDebuffSources.Other, out int superseded))
                return;
            float petals = Mathf.Max(0f, config.ZapPetals.EvaluateReplicated(_status));   // 0.1 -> 0.25 of a petal by Charge
            var throwAt = at - from;
            ElementalTransfer.ApplyAllAuthoritative(ElementalTransferForm.Eject, victim, _status,
                petals * CombatHitDrain.NormalizedPerLevel, throwAt.sqrMagnitude > 1e-6f ? throwAt.normalized * 30f : Vector3.zero,
                ElementalDebuffSources.Other);
            config.OnCombatHitLanded.Raise(new CombatHitStats
            {
                ShooterName = _status.PlayerName,
                VictimName = victim.PlayerName,
                HitClass = CombatHitClass.Bullet,
                SupersededRank = superseded,
            });
        }

        internal void OnPrismFired(Vector3 at)
        {
            if (Time.time - _firedSoundAt < 0.05f) return;   // a whole tube fired at once is one sound
            _firedSoundAt = Time.time;
            PlayAt(config.PrismFiredEvent, at);
        }

        internal void OnSentinelPlaced(Vector3 at) => PlayAt(config.SentinelPlacedEvent, at);
        internal void OnSentinelLost(Vector3 at) => PlayAt(config.SentinelLostEvent, at);

        static void PlayAt(EventReference reference, Vector3 at)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, at);
        }

        // A vessel swap or despawn: the sentinels go (their solitons go with the wormhole's), and what the solitons carry
        // stays where it is.
        void OnDisable()
        {
            _sentinels.Clear(this, false);
            _portal.Clear();
            SentinelMode = false;
        }

        void OnDestroy() => _sentinels.DisposeGhost();
    }
}
