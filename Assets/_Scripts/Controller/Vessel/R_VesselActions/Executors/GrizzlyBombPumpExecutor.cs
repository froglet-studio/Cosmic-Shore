using CosmicShore.Core;
using CosmicShore.Utility;
using Reflex.Injectors;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Grizzly's BOMB PUMP — both triggers blow a bomb behind the hull and the blast kicks
    /// the ship forward along its nose. See GRIZZLY_BOMB_PUMP.md.
    ///
    /// <para><b>Size is pressure, never time.</b> A bomb's size is the PEAK analog pressure of
    /// the pull that fired it, so a feather tap and a full squeeze share one cooldown. Each
    /// trigger owns its own cooldown, which is what makes alternating LT → RT the rhythm.</para>
    ///
    /// <para><b>The peak is tracked from the raw input, not from the action callbacks.</b> A
    /// press/release reaches this executor only after the owner → server → everyone round trip
    /// (<c>R_VesselActionHandler</c>), so a tap shorter than one round trip would release
    /// before its press arrived and the callbacks alone would see a pressure of zero. Instead
    /// <see cref="Update"/> watches each trigger's analog value every frame on the simulating
    /// machine and records the peak of every pull; a release callback just consumes it.</para>
    ///
    /// <para><b>Only the simulating machine blows bombs</b> (the human owner, or the server for
    /// an AI). It applies the kick — the vessel's transform replicates — and relays the blast
    /// through <see cref="GrizzlyBombNetworkRelay"/> so every peer spawns the same-sized
    /// explosion locally (the Manta bomb model). Replays on other peers are ignored here.</para>
    ///
    /// <para><b>The kick is applied directly, not by riding the blast.</b> The cannon's
    /// self-launch needs <c>AffectSelfOverride = true</c>, and that same flag makes a blast
    /// destroy the pilot's own-domain prisms — a bomb that went off behind the hull every
    /// half-second would shred the Grizzly's own trail. So the bomb spares its own domain and
    /// the executor pushes the hull itself.</para>
    /// </summary>
    public sealed class GrizzlyBombPumpExecutor : ShipActionExecutorBase
    {
        [Header("Config")]
        [SerializeField, Tooltip("Shared tuning for both triggers. Required — without it the bomb pump is dead.")]
        GrizzlyBombPumpConfigSO config;

        /// <summary>HUD hook: a bomb blew. Params = side (0 left / 1 right), size 0..1.</summary>
        public event System.Action<GrizzlyBombActionSO.TriggerSide, float> OnBombBlown;

        /// <summary>Analog value a pull must exceed to count. Matches the input strategies' trigger deadzone.</summary>
        const float GestureThreshold = 0.05f;

        struct TriggerState
        {
            public bool InGesture;        // analog currently above threshold
            public float GesturePeak;     // peak of the pull in progress
            public float CompletedPeak;   // peak of the last finished pull, not yet consumed
            public bool Armed;            // a press callback arrived and is waiting for release
            public float LastBombTime;
        }

        IVesselStatus _status;
        VesselImpactor _vesselImpactor;
        GrizzlyBombNetworkRelay _relay;
        readonly TriggerState[] _sides = new TriggerState[2];

        public GrizzlyBombPumpConfigSO Config => config;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _vesselImpactor = GetComponentInParent<VesselImpactor>();
            _relay = GetComponentInParent<GrizzlyBombNetworkRelay>();
            ResetSides();

            if (!config)
                CSDebug.LogError("[GrizzlyBombPump] No GrizzlyBombPumpConfigSO wired on the executor — " +
                                 "the Grizzly's triggers blow nothing. Wire it on Grizzly.prefab's " +
                                 "GrizzlyBombPumpExecutor.");
        }

        void OnDisable() => ResetSides();

        void ResetSides()
        {
            for (int i = 0; i < _sides.Length; i++)
                _sides[i] = new TriggerState { LastBombTime = float.NegativeInfinity };
        }

        // ── Pressure tracking (simulating machine only) ───────────────────────

        void Update()
        {
            if (_status == null || !IsSimAuthority(_status)) return;
            var input = _status.InputStatus;
            if (input == null) return;

            if (IsAutopilotDriven)
            {
                AutopilotPump(input);
                return;
            }

            Track(ref _sides[0], input.LeftTriggerAnalog);
            Track(ref _sides[1], input.RightTriggerAnalog);
        }

        // ── Autopilot drive ───────────────────────────────────────────────────

        /// <summary>True while an autopilot flies this hull - an AI player, a released companion,
        /// or the menu's lava-lamp autopilot. Gated on the PILOT, not on the player being an AI,
        /// so every autopilot flies the same kit a human does (the Manta's autopilot Soar rule,
        /// REDLINE.md §5).</summary>
        bool IsAutopilotDriven => _status.AIPilot != null && _status.AIPilot.AutoPilotEnabled;

        /// <summary>
        /// The pump an autopilot cannot press. Alternates LT and RT on each trigger's own
        /// cooldown - the older clock first, so the rhythm is the human's one bomb every half
        /// cooldown - with a size from <see cref="GrizzlyBombPumpConfigSO.AutopilotPumpSize"/>:
        /// full on a straight stick, the smallest bomb at a full deflection. Reads the stick
        /// AIPilot last wrote (a frame stale at worst, harmless against a 0.8 s kick). No pressure
        /// tracking and no press/release round trip: this IS the simulating machine, and the
        /// relay replays each bomb to every peer exactly as it does a human's.
        /// </summary>
        void AutopilotPump(IInputStatus input)
        {
            if (!config || config.AiPumpStickBand <= 0f) return;

            int side = _sides[0].LastBombTime <= _sides[1].LastBombTime ? 0 : 1;
            if (Time.time - _sides[side].LastBombTime < config.CooldownPerTrigger) return;
            _sides[side].LastBombTime = Time.time;

            float stick = Mathf.Max(Mathf.Abs(input.XSum), Mathf.Abs(input.YSum));
            Blow((GrizzlyBombActionSO.TriggerSide)side, config.AutopilotPumpSize(stick));
        }

        static void Track(ref TriggerState s, float analog)
        {
            if (analog > GestureThreshold)
            {
                if (!s.InGesture) { s.InGesture = true; s.GesturePeak = analog; }
                else if (analog > s.GesturePeak) s.GesturePeak = analog;
            }
            else if (s.InGesture)
            {
                s.InGesture = false;
                s.CompletedPeak = Mathf.Max(s.CompletedPeak, s.GesturePeak);
                s.GesturePeak = 0f;
            }
        }

        // ── Action callbacks (arrive on every peer after the round trip) ──────

        public void OnPress(GrizzlyBombActionSO.TriggerSide side)
        {
            if (_status == null || !IsSimAuthority(_status)) return;
            _sides[(int)side].Armed = true;
        }

        public void OnRelease(GrizzlyBombActionSO.TriggerSide side)
        {
            if (!config || _status == null || !IsSimAuthority(_status)) return;

            ref var s = ref _sides[(int)side];
            if (!s.Armed) return;
            s.Armed = false;

            // The pull this release belongs to: still in progress (round trip beat the finger),
            // or the one that just finished. Consume it either way so it pays out once.
            float pressure = s.InGesture ? Mathf.Max(s.GesturePeak, s.CompletedPeak) : s.CompletedPeak;
            s.CompletedPeak = 0f;
            if (s.InGesture) s.GesturePeak = 0f;

            // No analog source at all (touch, or a strategy that never writes the analog
            // channel) reads 0 here: a press with no pressure reading is a full press, not none.
            if (pressure <= GestureThreshold) pressure = 1f;

            // Fixed cooldown per trigger — the size of the bomb never touches it.
            if (Time.time - s.LastBombTime < config.CooldownPerTrigger) return;
            s.LastBombTime = Time.time;

            Blow(side, config.SizeForPressure(pressure));
        }

        // ── The bomb ──────────────────────────────────────────────────────────

        void Blow(GrizzlyBombActionSO.TriggerSide side, float size01)
        {
            var vesselTf = _status.Transform;
            if (!vesselTf) return;

            // Blowing a bomb from turret stance un-plants first, the same way Rush does,
            // so the kick is not swallowed by the translation hold.
            if (_status.IsTranslationRestricted && _status.Vessel is VesselController controller)
            {
                controller.SetTranslationRestricted(false);
                GetComponent<ActionExecutorRegistry>()?.Get<GrizzlyDigInActionExecutor>()?.ReapplyRegen();
            }

            float sideSign = side == GrizzlyBombActionSO.TriggerSide.Left ? -1f : 1f;
            var position = vesselTf.position
                           - vesselTf.forward * config.SpawnBehindDistance
                           + vesselTf.right * (config.SpawnSideOffset * sideSign);
            var rotation = vesselTf.rotation;
            float scale = config.BlastScaleForSize(size01);

            SpawnBlast(config, _status, position, rotation, scale,
                _vesselImpactor ? _vesselImpactor.DIContainer : null);

            _status.VesselTransformer?.ModifyVelocity(
                vesselTf.forward * config.KickForSize(size01), config.KickDuration);

            if (!config.BombEvent.IsNull && AudioSystem.Instance)
                AudioSystem.Instance.PlaySFXEvent(config.BombEvent, position);

            if (_relay && _relay.IsSpawned)
                _relay.BroadcastBomb(position, rotation, scale);

            OnBombBlown?.Invoke(side, size01);
        }

        /// <summary>
        /// Spawns one bomb blast. Shared by the simulating machine and the relay's replay on
        /// every other peer, so the two cannot drift. The blast spares the pilot's own domain
        /// (AffectSelfOverride = false): own trail armours instead of breaking and teammates are
        /// untouched, while enemy mass and pilots inside it are hit as by any Grizzly blast.
        /// </summary>
        public static void SpawnBlast(GrizzlyBombPumpConfigSO cfg, IVesselStatus status,
            Vector3 position, Quaternion rotation, float scale, Reflex.Core.Container di)
        {
            if (!cfg || cfg.AoePrefabs == null || status == null) return;

            foreach (var prefab in cfg.AoePrefabs)
            {
                if (!prefab) continue;
                var spawned = Object.Instantiate(prefab, position, rotation);
                if (di != null)
                    GameObjectInjector.InjectRecursive(spawned.gameObject, di);
                spawned.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnDomain           = status.Domain,
                    Vessel              = status.Vessel,
                    MaxScale            = scale,
                    OverrideMaterial    = status.AOEExplosionMaterial,
                    AnnonymousExplosion = false,
                    SpawnPosition       = position,
                    SpawnRotation       = rotation,
                    AffectSelfOverride  = false,
                });
                spawned.Detonate();
            }
        }

        /// <summary>
        /// True on the machine that simulates this vessel's motion: the network owner (a human's
        /// own client, or the server for an AI), or any machine when no network session is live.
        /// Same predicate as <c>MantaStingActionExecutor.IsSimAuthority</c>.
        /// </summary>
        static bool IsSimAuthority(IVesselStatus status)
        {
            var player = status?.Player;
            if (player == null) return false;
            if (player.IsNetworkOwner) return true;
            var nm = Unity.Netcode.NetworkManager.Singleton;
            return nm == null || !nm.IsListening;
        }
    }
}
