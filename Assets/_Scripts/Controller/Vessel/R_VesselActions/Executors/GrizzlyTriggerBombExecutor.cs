using System;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using Reflex.Injectors;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Grizzly's TRIGGER BOMBS - LT and RT each own one bomb, with the charged cannon's
    /// lifecycle (GRIZZLY_CHARGED_CANNON.md) and PRESSURE where the cannon has hold time:
    ///
    ///   Idle -pull-> Arming -release-> InFlight -pull-> Frozen -release-> (detonate) -> Idle
    ///
    /// <b>Only the trigger detonates a bomb</b> (design ask, 2026-10-08). There is no fuse and no
    /// contact detonation: the bomb flies THROUGH prisms - lighting them as it passes
    /// (<see cref="GrizzlyBombVisual"/>, Docs/LIT.md) - and touches nothing (its impact container,
    /// GrizzlyBombProjectileImpactContainer, is empty), and a bomb nobody freezes eases to rest
    /// at the end of its throw and HANGS there (<see cref="Projectile.HoldAtFlightEnd"/>), still
    /// InFlight, until the next pull freezes it and the release blows it.
    ///
    /// The release that fires reads the PEAK analog pressure of that pull: it sets the ammo
    /// spent, the bomb's visible size and the blast's size together (config
    /// <see cref="GrizzlyTriggerBombConfigSO"/>). Every detonation is spawned with
    /// <c>AffectSelfOverride = false</c> - the blast spares the pilot's own domain, so their own
    /// trail survives it - and the executor LAUNCHES a Grizzly inside its own blast AWAY FROM THE
    /// BOMB itself (<see cref="LaunchSelf"/>), strongest at the bomb and easing toward the edge.
    /// (The charged cannon's self-launch, through <see cref="VesselImpulseByExplosionEffectSO"/>,
    /// steers by the nose instead; the bombs were asked to push you away from them, 2026-10-08.)
    ///
    /// <para><b>Every peer simulates.</b> Presses and releases reach this executor on every
    /// peer (owner -> server -> all, <c>R_VesselActionHandler</c>), exactly as the cannon's do,
    /// and each peer fires its own local bomb. The pressure is read from
    /// <c>InputStatus.Left/RightTriggerAnalog</c>, an owner-written NetworkVariable every peer
    /// can read, so peers size the bomb from the same pull. A tap shorter than one network tick
    /// can be sampled lower on a remote peer than on the owner; the owner's blast is the one that
    /// launches the owner's hull, and the hull's transform is what replicates.</para>
    ///
    /// <para><b>Pressure is tracked from the raw input, not from the callbacks.</b> A press
    /// reaches the executor only after the round trip, so a quick pull can be over before its
    /// press arrives; <see cref="Update"/> records each pull's peak and the release consumes it.
    /// A device with no analog reading (mouse buttons, shift keys, touch, an autopilot) is a full
    /// press.</para>
    ///
    /// <para><b>A bomb never leaks.</b> A live bomb is silently returned (no blast) on turn end,
    /// disable and re-<see cref="Initialize"/>; stale-shell races are guarded with
    /// <see cref="Projectile.FlightGeneration"/> snapshots, the cannon's pattern. A bomb that
    /// can hang forever is exactly the one that could leak, so these three are the whole of its
    /// retirement: the trigger, the turn, the vessel.</para>
    /// </summary>
    public sealed class GrizzlyTriggerBombExecutor : ShipActionExecutorBase
    {
        public enum BombState { Idle, Arming, InFlight, Frozen }

        /// <summary>HUD hook: a trigger's bomb changed state.</summary>
        public event Action<GrizzlyBombActionSO.TriggerSide, BombState> OnBombStateChanged;

        /// <summary>HUD hook: a bomb left the muzzle. Params = side, size 0..1.</summary>
        public event Action<GrizzlyBombActionSO.TriggerSide, float> OnBombFired;

        /// <summary>HUD hook: a bomb went off. Params = side, size 0..1.</summary>
        public event Action<GrizzlyBombActionSO.TriggerSide, float> OnBombDetonated;

        [SerializeField, Tooltip("All trigger-bomb tuning. A missing wire means the triggers do nothing - the executor logs it.")]
        GrizzlyTriggerBombConfigSO config;

        [Header("Scene Refs")]
        [SerializeField, Tooltip("The muzzle the bombs leave from - the charged cannon's Gun (it is shared: Gun keeps no per-shot state the two would fight over).")]
        Gun gun;

        [Header("Events")]
        [SerializeField, Tooltip("A live bomb is silently returned at turn end - no surprise blasts across turns.")]
        ScriptableEventNoParam OnMiniGameTurnEnd;

        [Inject] GameDataSO gameData;

        /// <summary>Analog value a pull must exceed to count. Matches the input strategies' trigger deadzone.</summary>
        const float GestureThreshold = 0.05f;

        /// <summary>Seconds an autopilot waits for its own replicated press/release to land
        /// before it may send another for the same trigger.</summary>
        const float AiCommandTimeoutSeconds = 0.5f;

        /// <summary>The longest an autopilot holds a frozen bomb before detonating it anyway -
        /// a hull that turned away from its bomb would otherwise wait on it forever.</summary>
        const float AiMaxFrozenSeconds = 1.5f;

        sealed class Slot
        {
            public GrizzlyBombActionSO.TriggerSide Side;
            public BombState State;

            // Pressure gesture, tracked from the raw analog every frame.
            public bool InGesture;
            public float GesturePeak;
            public float CompletedPeak;

            public Projectile Shot;
            public int ShotGeneration;
            public float Size01;
            public float FrozenSince;

            // Autopilot: the input event this trigger is bound to, and an outstanding command.
            public InputEvents? BoundInput;
            public bool AiPending;
            public float AiPendingSince;
        }

        readonly Slot[] _slots =
        {
            new Slot { Side = GrizzlyBombActionSO.TriggerSide.Left },
            new Slot { Side = GrizzlyBombActionSO.TriggerSide.Right },
        };

        IVesselStatus _status;
        ResourceSystem _resources;
        bool _warnedAmmoIndex;
        float _aiLastFireTime = float.NegativeInfinity;
        readonly List<ShipActionSO> _bindScratch = new();

        public GrizzlyTriggerBombConfigSO Config => config;

        public BombState StateOf(GrizzlyBombActionSO.TriggerSide side) => _slots[(int)side].State;

        void OnEnable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised += HandleTurnEnd;
        }

        void OnDisable()
        {
            if (OnMiniGameTurnEnd)
                OnMiniGameTurnEnd.OnRaised -= HandleTurnEnd;
            ResetSlots();
        }

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _resources = shipStatus.ResourceSystem;
            if (gun) gun.Initialize(shipStatus);
            ResetSlots();

            if (!config)
                CSDebug.LogError("[GrizzlyTriggerBomb] No GrizzlyTriggerBombConfigSO wired on the executor - " +
                                 "the Grizzly's triggers do nothing. Wire it on Grizzly.prefab's " +
                                 "GrizzlyTriggerBombExecutor.");
            if (!gun)
                CSDebug.LogError("[GrizzlyTriggerBomb] No Gun wired on the executor - the Grizzly's triggers " +
                                 "cannot fire. Wire the charged cannon's Gun on Grizzly.prefab's " +
                                 "GrizzlyTriggerBombExecutor.");
        }

        void ResetSlots()
        {
            foreach (var slot in _slots)
            {
                ResolveSilently(slot);
                slot.InGesture = false;
                slot.GesturePeak = 0f;
                slot.CompletedPeak = 0f;
                slot.AiPending = false;
                slot.BoundInput = null;
                SetState(slot, BombState.Idle);
            }
        }

        void HandleTurnEnd() => ResetSlots();

        // ── Pressure tracking (every peer) + autopilot (simulating machine only) ──

        void Update()
        {
            if (_status == null) return;
            var input = _status.InputStatus;
            if (input == null) return;

            Track(_slots[0], input.LeftTriggerAnalog);
            Track(_slots[1], input.RightTriggerAnalog);

            if (IsAutopilotDriven && IsSimAuthority(_status))
                AutopilotDrive(input);
        }

        static void Track(Slot s, float analog)
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

        /// <summary>The pull a fire-release belongs to: still in progress (the round trip beat
        /// the finger) or the one that just finished. Consumed so it pays out once. No analog
        /// source at all reads 0 here, and a press with no pressure reading is a FULL press.</summary>
        static float ConsumePressure(Slot s)
        {
            float pressure = s.InGesture ? Mathf.Max(s.GesturePeak, s.CompletedPeak) : s.CompletedPeak;
            s.CompletedPeak = 0f;
            if (s.InGesture) s.GesturePeak = 0f;
            return pressure <= GestureThreshold ? 1f : pressure;
        }

        // ── Action callbacks (arrive on every peer after the round trip) ──────

        public void OnPress(GrizzlyBombActionSO.TriggerSide side)
        {
            var s = _slots[(int)side];
            switch (s.State)
            {
                case BombState.Idle:
                    SetState(s, BombState.Arming);
                    break;

                case BombState.InFlight:
                    if (ShotIsLive(s))
                    {
                        s.Shot.Freeze();
                        if (s.Shot.TryGetComponent<GrizzlyBombVisual>(out var visual)) visual.Freeze();
                        s.FrozenSince = Time.time;
                        SetState(s, BombState.Frozen);
                    }
                    else
                    {
                        // The bomb died between frames - this pull arms a new one.
                        Forget(s);
                        SetState(s, BombState.Arming);
                    }
                    break;

                case BombState.Arming:
                case BombState.Frozen:
                    break;   // presses are idempotent here
            }
        }

        public void OnRelease(GrizzlyBombActionSO.TriggerSide side)
        {
            var s = _slots[(int)side];
            switch (s.State)
            {
                case BombState.Arming:
                    Fire(s);
                    break;

                case BombState.Frozen:
                    DetonateFrozen(s);
                    break;

                case BombState.Idle:
                case BombState.InFlight:
                    break;
            }
        }

        // ── Fire ──────────────────────────────────────────────────────────────

        void Fire(Slot s)
        {
            float pressure = ConsumePressure(s);
            if (!config || !gun || _status == null || !TryGetAmmo(out float ammo))
            {
                SetState(s, BombState.Idle);
                return;
            }

            float size = config.AffordableSize(config.SizeForPressure(pressure), ammo);
            if (size < 0f)
            {
                SetState(s, BombState.Idle);   // the pool cannot buy even a minimum bomb
                return;
            }
            _resources.ChangeResourceAmount(config.AmmoIndex, -config.AmmoCostForSize(size));

            // In turret stance the bomb leaves along the gun's facing and inherits nothing; in
            // free flight it inherits the hull's course and speed (the cannon's rule), so it
            // always leaves FASTER than the Grizzly that fired it.
            var gunTf = gun.transform;
            var inherited = _status.IsTranslationRestricted ? Vector3.zero : _status.Course * _status.Speed;
            float yaw = s.Side == GrizzlyBombActionSO.TriggerSide.Left ? -config.SideYawDegrees : config.SideYawDegrees;
            var aim = Quaternion.AngleAxis(yaw, gunTf.up) * gunTf.forward;

            gun.FireGun(
                gunTf,
                config.ProjectileSpeed,
                inherited,
                config.ProjectileScaleForSize(size),
                true,                                  // ignoreCooldown - each trigger owns its rhythm
                config.ProjectileTime,
                size,                                  // Projectile.Charge carries the size to the blast
                FiringPatterns.Default,
                0,
                detachAfterSpawn: true,                // a frozen bomb must not ride the ship
                stopOnFirstPrismImpact: false,         // it flies THROUGH mass - only the trigger detonates it
                spareOwnDomain: false,
                aimDirection: aim);

            s.Shot = gun.LastProjectile;
            if (s.Shot == null)
            {
                SetState(s, BombState.Idle);
                return;
            }

            // No fuse: when the throw runs out the bomb parks where it is and waits.
            s.Shot.HoldAtFlightEnd = true;
            s.Size01 = size;
            s.ShotGeneration = s.Shot.FlightGeneration;
            if (s.Shot.TryGetComponent<GrizzlyBombVisual>(out var visual))
                visual.Arm(BombColor(s.Side), _status.Domain, config.ProjectileTime);
            s.Shot.FlightEnded += s.Side == GrizzlyBombActionSO.TriggerSide.Left
                ? HandleLeftFlightEnded
                : HandleRightFlightEnded;
            SetState(s, BombState.InFlight);

            PlayAt(config.FireEvent, gunTf.position);
            OnBombFired?.Invoke(s.Side, size);
        }

        bool TryGetAmmo(out float ammo)
        {
            ammo = 0f;
            var list = _resources != null ? _resources.Resources : null;
            int i = config.AmmoIndex;
            if (list == null || i < 0 || i >= list.Count)
            {
                if (!_warnedAmmoIndex)
                {
                    _warnedAmmoIndex = true;
                    CSDebug.LogError($"[GrizzlyTriggerBomb] Ammo index {i} is not a resource on this vessel's " +
                                     "ResourceSystem - the triggers cannot fire. Grizzly.prefab must list an " +
                                     "'Ammo' resource at that index.");
                }
                return false;
            }
            ammo = list[i].CurrentAmount;
            return true;
        }

        // ── Detonate ──────────────────────────────────────────────────────────

        void HandleLeftFlightEnded(Projectile p, bool stoppedByImpact) => HandleFlightEnded(_slots[0], p);
        void HandleRightFlightEnded(Projectile p, bool stoppedByImpact) => HandleFlightEnded(_slots[1], p);

        /// <summary>
        /// Something OTHER than the trigger ended this bomb's flight. Nothing should: the bomb
        /// holds at the end of its throw and its impact container is empty. If anything ever
        /// does, the bomb is simply gone (whoever ended it owns the pool return) - it does NOT
        /// detonate, because only the trigger detonates a bomb.
        /// </summary>
        void HandleFlightEnded(Slot s, Projectile p)
        {
            if (p != s.Shot) return;
            Forget(s);
            SetState(s, BombState.Idle);
        }

        /// <summary>The player's release on a frozen bomb: blast where it hangs, return the shell.</summary>
        void DetonateFrozen(Slot s)
        {
            if (!ShotIsLive(s))
            {
                Forget(s);
                SetState(s, BombState.Idle);
                return;
            }

            var shot = s.Shot;
            var pos = shot.transform.position;
            var rot = shot.transform.rotation;
            var di = shot.TryGetComponent<ProjectileImpactor>(out var impactor) ? impactor.DIContainer : null;
            float size = s.Size01;

            Forget(s);
            shot.ReturnToFactory();
            SetState(s, BombState.Idle);
            SpawnBlast(s, pos, rot, size, di);
        }

        /// <summary>
        /// One trigger-bomb blast. <c>AffectSelfOverride = false</c>: the blast spares the pilot's
        /// own domain (their trail shields instead of breaking, teammates are untouched) while
        /// enemy mass and pilots inside it are hit and knocked back radially as by any Grizzly
        /// blast. The pilot's own launch is applied here, AWAY from the bomb - see
        /// <see cref="LaunchSelf"/>.
        /// </summary>
        void SpawnBlast(Slot s, Vector3 pos, Quaternion rot, float size01, Reflex.Core.Container di)
        {
            if (!config || config.AoePrefabs == null || _status == null) return;

            float scale = config.BlastScaleForSize(size01);
            float blastSeconds = 0f;
            foreach (var prefab in config.AoePrefabs)
            {
                if (!prefab) continue;
                var spawned = Instantiate(prefab, pos, rot);
                if (di != null)
                    GameObjectInjector.InjectRecursive(spawned.gameObject, di);
                spawned.Initialize(new AOEExplosion.InitializeStruct
                {
                    OwnDomain           = _status.Domain,
                    Vessel              = _status.Vessel,
                    MaxScale            = scale,
                    OverrideMaterial    = _status.AOEExplosionMaterial,
                    AnnonymousExplosion = false,
                    SpawnPosition       = pos,
                    SpawnRotation       = rot,
                    AffectSelfOverride  = false,
                });
                spawned.Detonate();
                if (blastSeconds <= 0f) blastSeconds = spawned.Duration;
            }

            LaunchSelf(pos, scale, blastSeconds);

            PlayAt(config.DetonateEvent, pos);
            OnBombDetonated?.Invoke(s.Side, size01);
        }

        /// <summary>
        /// Throws the pilot AWAY from their own bomb when the blast catches them: direction is
        /// bomb -> hull, so a bomb left behind you throws you forward, one beside you throws you
        /// sideways, and one ahead of you stops you dead. Strength is the blast's own impulse
        /// (<c>scale / ExplosionDuration</c>, the AOE's <c>Impulse</c>) times
        /// <c>selfLaunchMultiplier</c>, full at the bomb and eased to <c>selfLaunchEdgeStrength</c>
        /// at the blast's edge (radius = half its scale - the AOE sphere's collider radius is 0.5),
        /// and nothing outside it. It rides <c>VesselTransformer.ModifyVelocity</c> with its own
        /// ceiling, <c>selfLaunchCeiling</c> (300 u/s): the launch was asked to throw three times
        /// as hard as the shared 100 u/s cap every other shove sits under, and raising the cap
        /// only for the launch's own lifetime keeps every knock-back the Grizzly takes where it was.
        ///
        /// <para>Applied on the SIMULATING machine only (the owner, or the server for an AI): the
        /// hull's transform is what replicates, and a peer pushing its copy of someone else's
        /// vessel would only fight that replication.</para>
        /// </summary>
        void LaunchSelf(Vector3 blastPos, float blastScale, float blastSeconds)
        {
            if (_status == null || !IsSimAuthority(_status)) return;
            var hull = _status.Transform;
            var transformer = _status.VesselTransformer;
            if (!hull || !transformer || blastSeconds <= 0f) return;

            float radius = blastScale * 0.5f;
            Vector3 away = hull.position - blastPos;
            float distance = away.magnitude;
            if (distance > radius) return;

            // Sitting on the bomb has no "away"; the one direction the pilot controls stands in.
            Vector3 direction = distance > 0.01f ? away / distance : hull.forward;
            float falloff = Mathf.Lerp(1f, config.SelfLaunchEdgeStrength, radius > 0f ? distance / radius : 0f);
            float speed = blastScale / blastSeconds * config.SelfLaunchMultiplier * falloff;

            // A dug-in Grizzly is blasted out of turret stance, as the cannon's blast does - routed
            // through the controller so the replicated flag stays in sync.
            if (_status.IsTranslationRestricted && _status.Vessel is VesselController controller)
            {
                controller.SetTranslationRestricted(false);
                if (TryGetComponent<ActionExecutorRegistry>(out var registry))
                    registry.Get<GrizzlyDigInActionExecutor>()?.ReapplyRegen();
            }

            transformer.ModifyVelocity(direction * speed, config.SelfLaunchSeconds, false, config.SelfLaunchCeiling);
        }

        /// <summary>This trigger's bomb colour: the palette's DANGER signal colour
        /// (<c>GetDangerSignalColor</c>), turned a little one way for LT and the other for RT
        /// (<see cref="GrizzlyTriggerBombConfigSO.BombColor"/>). Read live; the config's fallback
        /// danger red stands in when no theme is resolvable yet.</summary>
        Color BombColor(GrizzlyBombActionSO.TriggerSide side)
        {
            var theme = gameData ? gameData.ThemeManagerData : null;
            var colors = theme ? theme.ColorSet : null;
            var danger = colors ? colors.GetDangerSignalColor() : default;
            return config.BombColor(side == GrizzlyBombActionSO.TriggerSide.Left, danger);
        }

        static void PlayAt(FMODUnity.EventReference ev, Vector3 pos)
        {
            if (!ev.IsNull && AudioSystem.Instance)
                AudioSystem.Instance.PlaySFXEvent(ev, pos);
        }

        /// <summary>Returns a live bomb to the pool with no blast - turn end, disable, re-init.</summary>
        void ResolveSilently(Slot s)
        {
            if (!ShotIsLive(s)) { Forget(s); return; }
            var shot = s.Shot;
            Forget(s);
            shot.ReturnToFactory();
        }

        bool ShotIsLive(Slot s) =>
            s.Shot != null && s.Shot.isActiveAndEnabled && s.Shot.FlightGeneration == s.ShotGeneration;

        void Forget(Slot s)
        {
            if (s.Shot != null)
                s.Shot.FlightEnded -= s.Side == GrizzlyBombActionSO.TriggerSide.Left
                    ? HandleLeftFlightEnded
                    : HandleRightFlightEnded;
            s.Shot = null;
        }

        void SetState(Slot s, BombState next)
        {
            s.AiPending = false;   // whatever the autopilot was waiting for has landed
            if (s.State == next) return;
            s.State = next;
            OnBombStateChanged?.Invoke(s.Side, next);
        }

        // ── Autopilot drive ───────────────────────────────────────────────────

        /// <summary>True while an autopilot flies this hull - an AI player, a released companion,
        /// or the menu's lava-lamp autopilot. Gated on the PILOT, so every autopilot flies the
        /// same kit a human does (REDLINE.md §5's rule).</summary>
        bool IsAutopilotDriven => _status.AIPilot != null && _status.AIPilot.AutoPilotEnabled;

        /// <summary>
        /// The bomb-jump an autopilot cannot press. Each trigger in turn, on the simulating
        /// machine: fire a full bomb while the stick is straight (inside
        /// <see cref="GrizzlyTriggerBombConfigSO.AiFireStickBand"/>) and the pool can pay for one,
        /// freeze it <see cref="GrizzlyTriggerBombConfigSO.AiFreezeDistance"/> ahead, fly past it,
        /// and detonate it once it is <see cref="GrizzlyTriggerBombConfigSO.AiDetonateBehindDistance"/>
        /// behind - inside its blast, so the hull is thrown forward, away from it. Every step
        /// goes through the REPLICATED press/release, so every peer runs the same bomb a human's
        /// pull would have produced (an autopilot writes no analog, so every peer reads a full
        /// press).
        /// </summary>
        void AutopilotDrive(IInputStatus input)
        {
            if (!config || config.AiFireStickBand <= 0f) return;
            var handler = _status.ActionHandler;
            if (handler == null) return;

            float stick = Mathf.Max(Mathf.Abs(input.XSum), Mathf.Abs(input.YSum));
            var hull = _status.Transform;

            for (int i = 0; i < _slots.Length; i++)
            {
                var s = _slots[i];
                if (s.AiPending && Time.time - s.AiPendingSince < AiCommandTimeoutSeconds) continue;
                if (!ResolveBoundInput(s, handler)) continue;
                var ie = s.BoundInput.Value;

                switch (s.State)
                {
                    case BombState.Idle:
                        if (stick > config.AiFireStickBand) break;
                        if (Time.time - _aiLastFireTime < config.AiFireIntervalSeconds) break;
                        if (!TryGetAmmo(out float ammo) || ammo + 1e-5f < config.MaxAmmoCost) break;
                        _aiLastFireTime = Time.time;
                        Command(s);
                        handler.PerformShipControllerActionsReplicated(ie);   // arm
                        handler.StopShipControllerActionsReplicated(ie);      // fire
                        break;

                    case BombState.InFlight:
                        if (!ShotIsLive(s) || !hull) break;
                        if ((s.Shot.transform.position - hull.position).sqrMagnitude <
                            config.AiFreezeDistance * config.AiFreezeDistance) break;
                        Command(s);
                        handler.PerformShipControllerActionsReplicated(ie);   // freeze
                        break;

                    case BombState.Frozen:
                        // The launch is AWAY from the bomb, so the bomb must be BEHIND the hull
                        // when it goes: fly past it, then blow it.
                        if (ShotIsLive(s) && hull &&
                            Time.time - s.FrozenSince < AiMaxFrozenSeconds &&
                            Vector3.Dot(hull.position - s.Shot.transform.position, hull.forward) <
                            config.AiDetonateBehindDistance)
                            break;   // not far enough past it yet
                        Command(s);
                        handler.StopShipControllerActionsReplicated(ie);      // detonate
                        break;

                    case BombState.Arming:
                        // An autopilot never holds a trigger; an Arming slot is a press whose
                        // release is still in flight, or one a human left armed when autopilot
                        // took over. Release it.
                        Command(s);
                        handler.StopShipControllerActionsReplicated(ie);
                        break;
                }
            }
        }

        void Command(Slot s)
        {
            s.AiPending = true;
            s.AiPendingSince = Time.time;
        }

        /// <summary>Which input event this trigger's action is bound to on THIS vessel - read
        /// off the binding maps rather than assumed, and retried until it succeeds (the maps are
        /// populated after executors initialize, vessel contract rule 6).</summary>
        bool ResolveBoundInput(Slot s, R_VesselActionHandler handler)
        {
            if (s.BoundInput.HasValue) return true;
            foreach (InputEvents ie in Enum.GetValues(typeof(InputEvents)))
            {
                _bindScratch.Clear();
                handler.CollectBoundActions(ie, _bindScratch);
                foreach (var action in _bindScratch)
                {
                    if (action is GrizzlyBombActionSO bomb && bomb.Side == s.Side)
                    {
                        s.BoundInput = ie;
                        _bindScratch.Clear();
                        return true;
                    }
                }
            }
            _bindScratch.Clear();
            return false;
        }

        /// <summary>
        /// True on the machine that simulates this vessel's motion: the network owner (a human's
        /// own client, or the server for an AI), or any machine when no network session is live.
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
