using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using FMODUnity;
using Reflex.Attributes;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's wrecking ball and chain: owns the chain physics
    /// (<see cref="ThresherChainSolver"/> + <see cref="ThresherChainLinks"/>), resolves what the
    /// ball and the chain do to prisms, and draws and voices all of it.
    ///
    /// <b>One step per frame, from inside the move step.</b> <see cref="ThresherVesselTransformer"/>
    /// calls <see cref="StepChain"/> through the base transformer's external-acceleration seam. If no
    /// transformer stepped it this frame (a hull whose transformer is off — a remote replica after a
    /// pilot swap), <see cref="LateUpdate"/> steps it from the hull's observed motion.
    ///
    /// <b>The ball is always destructive, and prisms are binary.</b> Every frame the ball's path is
    /// swept as a sphere through <see cref="PrismSpatialIndex.QuerySegment"/> (never colliders — a
    /// fast ball would tunnel, and prism collider LOD switches them off), nearest-first:
    /// <list type="bullet">
    /// <item><b>Below smash speed</b> the ball wears its pilot's DOMAIN colour. A rival prism is
    /// destroyed (the ball keeps <c>crushKeep</c> of its speed); a prism of its own domain is
    /// BOUNCED off.</item>
    /// <item><b>At smash speed</b> the ball goes RED and destroys every prism it hits — its own
    /// domain's included — and each hit sets off an explosion that does the same (the Grizzly's
    /// blast with <c>AffectSelfOverride</c> on). The ball ploughs on, keeping <c>plough</c>.</item>
    /// <item><b>Space L5 "Breaker"</b>: a red ball's DIRECT hit destroys super-shielded prisms
    /// (the sanctioned DeactivateShields → Damage(devastate) sequence).</item>
    /// </list>
    /// Every kill goes through <see cref="Prism.Damage"/> with the ball's own velocity as the impact
    /// vector — the Rhino sword's contact-velocity idea at its core — so shields pop and
    /// super-shields hold as for every other hull.
    ///
    /// <b>Enemy vessels: petals only</b> (the LOCKED PvP rule). The ball's swept path and every chain
    /// link reach an enemy hull; a contact is a scored <see cref="CombatHitClass.Strike"/> that
    /// STEALS <c>vesselHitPetals</c> (Charge-scaled) petals per element through
    /// <see cref="CombatHitDrain.TryAdmit"/>, decided on the machine that owns this hull. Nothing
    /// else touches the victim. Charge L5 "Magnet" draws the free ball toward the nearest enemy hull.
    ///
    /// <b>The chain slices and shields</b>, taut or slack. Every rival prism its links touch is
    /// <see cref="Prism.Slice"/>d along the plane the chain is sweeping, with debris at the touching
    /// link's own velocity, and every prism of its OWN domain it touches is shielded. A shielded rival
    /// loses its shield to the first cut and dies to the next; super-shielded mass refuses.
    ///
    /// <b>The chain's kills charge the ball.</b> Every prism the chain slices banks
    /// <c>energyPerSlice</c> in the ball; the ball's next hit on a RIVAL prism releases the bank as
    /// an explosion even below smash speed (and a smash's own explosion spends it), its diameter
    /// growing with the bank.
    ///
    /// <b>Mass Mode</b> (A, a toggle). The bank is never released: the ball STEALS the rival prisms
    /// it touches (each steal banks <c>energyPerSteal</c>), SHIELDS its own domain's, lays a trail
    /// of its own prisms behind it, and makes no explosion at any speed. Leave Mass Mode and the
    /// next rival hit spends everything it banked.
    ///
    /// <b>Windmill</b> (an omni crystal, <see cref="ThresherWindmillByCrystalEffectSO"/>): for
    /// <c>windmillSeconds</c> the ball circles the ship red-hot across its line of flight.
    ///
    /// <b>Networking: none of its own</b> (the Gibbon's shape). Press and release round-trip through
    /// <c>R_VesselActionHandler</c>, so every peer runs the same chain from the same inputs; a peer
    /// can disagree on a marginal hit by a frame of drift.
    /// </summary>
    public sealed class ThresherExecutor : ShipActionExecutorBase
    {
        [Tooltip("Every Thresher dial: chain physics, elements, explosion, chain cut, feel, look.")]
        [SerializeField] ThresherConfigSO config;

        [Header("Audio (LOCKED FMOD convention: ship EMPTY, never a borrowed event)")]
        [Tooltip("Heavy thud on a smash (ball at smash speed).")]
        [SerializeField] EventReference smashEvent;
        [Tooltip("A slow ball crushing a rival prism.")]
        [SerializeField] EventReference crushEvent;
        [Tooltip("The ball bouncing off a prism of its own domain.")]
        [SerializeField] EventReference bounceEvent;
        [Tooltip("The chain slicing a prism.")]
        [SerializeField] EventReference sliceEvent;
        [Tooltip("Clunk when the ball plants and becomes the pivot.")]
        [SerializeField] EventReference plantEvent;
        [Tooltip("Yank when the left trigger releases the pivot and the ball is dragged after the ship.")]
        [SerializeField] EventReference yankEvent;

        [Header("Materials (optional - runtime fallbacks when empty)")]
        [Tooltip("Ball material: ThresherBallMaterial (ThresherBallFresnelShader). Its body and rim colours are driven " +
                 "per frame through a MaterialPropertyBlock (_DarkColor / _BrightColor).")]
        [SerializeField] Material ballMaterial;
        [Tooltip("Skid-trail material (and the chain's fallback). Must honour vertex colour (Sprites/Default does).")]
        [SerializeField] Material lineMaterial;
        [Tooltip("Chain material: ThresherChainMaterial (ThresherChainFresnelShader - a fresnel TUBE drawn on the " +
                 "chain's ribbon). Body and rim colours are driven per frame through a MaterialPropertyBlock.")]
        [SerializeField] Material chainMaterial;

        [Inject] GameDataSO _gameData;

        IVesselStatus _status;
        ActionExecutorRegistry _registry;
        VesselImpactor _impactor;
        ThresherChainSolver _solver;
        ThresherChainLinks _links;
        ThresherChainSettings _baseSettings;
        ThresherChainSettings _settings;
        bool _seeded;
        bool _payOut;
        bool _plantHeld;
        bool _massMode;
        float _energy;
        /// <summary>The launch direction the AIM camera chose for the pending plant release, taken
        /// by the transformer on the release edge (<see cref="TryConsumeReleaseDirection"/>).</summary>
        Vector3? _releaseDirection;

        // Mass Mode: the ball's own trail. A container of our own so the prisms it lays are a ribbon
        // (rideable, identity-testable) and never members of the hull's wake.
        readonly Trail _ballTrail = new Trail();
        float _trailOwed;

        int _lastStepFrame = -1;
        Vector3 _lastShipPosition;
        Vector3 _lastShipVelocity;

        // Smash bookkeeping
        int _combo;
        float _coolTime;
        float _freezeUntilUnscaled;
        float _nextExplosionTime;
        static bool s_timeScaleHitStopActive;
        readonly List<Prism> _candidates = new List<Prism>(64);
        readonly List<(float t, Prism prism)> _hits = new List<(float, Prism)>(32);
        HashSet<Prism> _touching = new HashSet<Prism>();
        HashSet<Prism> _touchingNext = new HashSet<Prism>();
        readonly HashSet<Prism> _cutThisFrame = new HashSet<Prism>();

        CancellationTokenSource _cts;

        // ------------------------------------------------------------------ public surface

        public bool IsPivoting => _solver != null && _solver.Mode == ThresherMode.Pivot;
        public ThresherMode Mode => _solver?.Mode ?? ThresherMode.Free;
        /// <summary>True while a MULTIPLAYER hit-stop is holding this hull and its ball still.</summary>
        public bool IsVesselFrozen => Time.unscaledTime < _freezeUntilUnscaled;
        /// <summary>Releasing the right trigger now would crack the ball past smash speed.</summary>
        public bool IsReady { get; private set; }
        public float BallSpeed => _solver?.BallSpeed ?? 0f;
        public Vector3 BallPosition => _solver?.BallPosition ?? transform.position;
        /// <summary>The ball is at smash speed: red, destroys anything, explodes.</summary>
        public bool IsHot => _solver != null && ThresherChainSolver.IsSmash(_solver.BallSpeed, _settings);
        /// <summary>Ball speed as a fraction of smash speed — the HUD heat gauge, and the ball's brightening.</summary>
        public float Gauge01 => _settings.SmashSpeed > 0f ? Mathf.Clamp01(BallSpeed / _settings.SmashSpeed) : 0f;
        /// <summary>How far the chain is let out, 0 (reeled in) to 1 (let out to its current reach).</summary>
        public float ChainOut01 => _solver == null || _settings.MaxLength <= _settings.RestLength ? 0f
            : Mathf.Clamp01((_solver.Length - _settings.RestLength) / (_settings.MaxLength - _settings.RestLength));
        /// <summary>The planted orbit's speed as a fraction of its cap; 0 when not planted.</summary>
        public float Spin01 => IsPivoting && _settings.LockMaxSpeed > 0f ? Mathf.Clamp01(_solver.LockSpeed / _settings.LockMaxSpeed) : 0f;
        public int Combo => _combo;
        /// <summary>Mass Mode (A): the ball steals, shields and lays a trail, and banks its energy.</summary>
        public bool IsMassMode => _massMode;
        /// <summary>Energy banked by the chain's kills (and Mass Mode steals) for the next explosion.</summary>
        public float Energy => _energy;
        public float Energy01 => config && config.EnergyMax > 0f ? Mathf.Clamp01(_energy / config.EnergyMax) : 0f;
        /// <summary>The bank as explosion RADIUS gained: 0 = a plain blast, 1 = the bank has bought the
        /// full <c>energyMaxDiameterScale</c>. The Space card's gauge.</summary>
        public float EnergyRadius01
        {
            get
            {
                float cap = EnergyCap();
                return cap > 1f ? Mathf.Clamp01((EnergyScale(_energy) - 1f) / (cap - 1f)) : 0f;
            }
        }
        /// <summary>A release NOW would be perfect (the ship reaches the launch point inside the window).</summary>
        public bool IsTimingPerfect { get; private set; }
        /// <summary>An aimed release is waiting for the ship to reach its launch point.</summary>
        public bool IsLaunchArmed => _solver != null && _solver.LaunchArmed;
        /// <summary>An omni crystal has the ball windmilling round the ship.</summary>
        public bool IsWindmilling => _solver != null && _solver.Mode == ThresherMode.Windmill;
        /// <summary>The local pilot is spectating a planted spin and the sticks aim the release, not the orbit.</summary>
        public bool IsAiming => _spectating;
        /// <summary>The ball's colour this frame (LINEAR; a UI consumer converts with .gamma).</summary>
        public Color BallColorNow => _shownValid ? _shownHue : config && _solver != null ? BallColor(out _) : Color.white;
        /// <summary>The READY lime (LINEAR).</summary>
        public Color ReadyColorNow => config ? ReadyColor() : Color.green;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _registry = GetComponentInParent<ActionExecutorRegistry>(true);
            _impactor = _status != null && _status.Transform ? _status.Transform.GetComponent<VesselImpactor>() : null;
            RebuildSolver();
            _seeded = false;
            _payOut = false;
            _plantHeld = false;
            _massMode = false;
            _energy = 0f;
            _releaseDirection = null;
            _trailOwed = 0f;
            _combo = 0;
            _touching.Clear();
            _touchingNext.Clear();
        }

        void RebuildSolver()
        {
            _baseSettings = config ? config.Dials.ToSettings() : new ThresherDials().ToSettings();
            _settings = _baseSettings;
            if (_solver == null) _solver = new ThresherChainSolver(_settings);
            else _solver.Settings = _settings;
            int links = config ? config.ChainLinks : 24;
            if (_links == null || _links.Count != links + 1) _links = new ThresherChainLinks(links);
        }

        /// <summary>The four element scalings, read live (never cached) and folded into this frame's
        /// settings. Read through <c>EvaluateReplicated</c>, not <c>EvaluateLive</c>: every peer
        /// simulates this ball, and element levels themselves do not replicate, so a remote copy
        /// reading its own (empty) ResourceSystem would fly a different ball. Only TIME lives in the
        /// chain's settings (the spin-up rate and cap); Charge, Mass and Space are read where they
        /// are spent (<see cref="TryHitVessel"/>, <see cref="LayTrailPrism"/>, <see cref="EnergyCap"/>).</summary>
        void ApplyElementScaling()
        {
            _settings = _baseSettings;
            if (!config || _status == null) return;
            float spin = Mathf.Max(0.05f, config.SpinMultiplier.EvaluateReplicated(_status));
            _settings.LockSpinRate *= spin;
            _settings.LockMaxSpeed *= spin;
            _solver.Settings = _settings;
        }

        /// <summary>SPACE: the largest explosion-diameter multiplier the bank can buy.</summary>
        float EnergyCap()
            => config && _status != null ? Mathf.Max(1f, config.EnergyMaxDiameterScale.EvaluateReplicated(_status)) : 4f;

        // The four level-5 upgrades, each read through the replicated unlock bits at the moment
        // of use (a per-hit / per-release snapshot), never cached.
        R_VesselElementalAbilityHandler Abilities => _status?.ElementalAbilityHandler;
        /// <summary>Charge L5 "Magnet": the free ball is drawn toward the nearest enemy vessel.</summary>
        bool MagnetActive => Abilities && Abilities.IsUpgradeActive(Element.Charge);
        /// <summary>Mass L5 "Armoured Trail": the prisms the ball lays in Mass Mode are laid shielded.</summary>
        bool ArmouredTrailActive => Abilities && Abilities.IsUpgradeActive(Element.Mass);
        /// <summary>Space L5 "Breaker": a red ball's direct hit destroys super-shielded prisms.</summary>
        bool BreakerActive => Abilities && Abilities.IsUpgradeActive(Element.Space);
        /// <summary>Time L5 "Perfect Release": a launch timed inside the window leaves with a boost.</summary>
        bool PerfectReleaseActive => Abilities && Abilities.IsUpgradeActive(Element.Time);

        // ------------------------------------------------------------------ input edges

        public void BeginPayOut() => _payOut = true;
        public void EndPayOut() => _payOut = false;

        public void BeginPlant()
        {
            if (_solver == null) return;
            _plantHeld = true;
            _solver.Plant();
        }

        public void EndPlant()
        {
            if (_solver == null) return;
            _plantHeld = false;
            bool wasPivot = _solver.Mode == ThresherMode.Pivot;

            // AIMED release: while the camera spectates, the aim names ONE point on the circle (the
            // point whose tangent is the aim, centred on screen). Releasing ARMS the launch: the ship
            // keeps circling and leaves from exactly that point, along the aim — the pilot picks the
            // point, the solver does the timing. Unaimed (a slow orbit, a remote replica, the
            // autopilot) it leaves on the orbit's tangent at once, as before.
            if (wasPivot && _spectating && _aimDir.sqrMagnitude > 0.5f)
            {
                // Everyone gets the timing window's no-wait launch; Time L5 adds the boost to it.
                _solver.ArmLaunch(_aimDir, config ? config.PerfectWindowSeconds : 0f,
                                  PerfectReleaseActive && config ? config.PerfectBoost : 1f);
                return;   // the yank plays when the launch happens (StepChain)
            }
            _solver.Release(_lastShipVelocity);
            if (wasPivot) PlayAt(yankEvent, _lastShipPosition);
        }

        /// <summary>The aimed launch direction of the armed launch that just left the orbit, once; the
        /// transformer faces the hull along it on the release edge (grip then puts the momentum on
        /// the nose the same frame).</summary>
        public bool TryConsumeReleaseDirection(out Vector3 direction)
        {
            direction = _releaseDirection ?? Vector3.zero;
            bool has = _releaseDirection.HasValue;
            _releaseDirection = null;
            return has;
        }

        /// <summary>A (Mass Mode): toggle. Every peer runs the press, so every peer toggles.</summary>
        public void ToggleMassMode()
        {
            _massMode = !_massMode;
            _trailOwed = 0f;
        }

        /// <summary>An omni crystal: the ball windmills round the ship (<c>windmillSeconds</c>).
        /// Run on every peer by the vessel crystal-effect dispatch.</summary>
        public void StartWindmill()
        {
            if (_solver == null || !config || _status == null || _status.Transform == null) return;
            if (!_seeded) EnsureSeeded(_status.Transform.position, _lastShipVelocity);
            _solver.StartWindmill(_status.Transform.position, config.WindmillSeconds);
        }

        void OnEnable()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
        }

        void OnDisable()
        {
            // The release edge never arrives for a vessel whose input is paused or handed to the
            // autopilot, so a held trigger must not stay held across a disable.
            _payOut = false;
            if (_solver != null && (_plantHeld || _solver.LaunchArmed)) _solver.Release(_lastShipVelocity);
            _plantHeld = false;
            _releaseDirection = null;
            _freezeUntilUnscaled = 0f;
            _cts?.Cancel();
            ReleaseCamera();
            SetVisualsActive(false);
        }

        void OnDestroy()
        {
            ReleaseCamera();
            _cts?.Dispose();
            _cts = null;
            DestroyVisuals();
        }

        // ------------------------------------------------------------------ the step

        /// <summary>
        /// One chain step. Called by <see cref="ThresherVesselTransformer"/> from inside its move
        /// step; returns the ship's velocity after the rope (towing) or the orbit (pivot).
        /// <paramref name="pivotSteer"/> is the pilot's stick as a world direction (magnitude 0..1)
        /// that tilts a planted orbit at the low <c>pivotSteer</c> rate; ignored while towing.
        /// </summary>
        public Vector3 StepChain(Vector3 shipPosition, Vector3 shipVelocity, float shipCruise, float dt,
                                 Vector3 pivotSteer = default)
        {
            if (_solver == null) RebuildSolver();
            ApplyElementScaling();
            EnsureSeeded(shipPosition, shipVelocity);

            if (MagnetActive) PullBallToEnemy(dt);
            var result = _solver.Step(shipPosition, shipVelocity, _payOut, shipCruise, dt, pivotSteer);
            _lastStepFrame = Time.frameCount;
            _lastShipPosition = shipPosition;
            _lastShipVelocity = result.ShipVelocity;

            if (result.Planted) PlayAt(plantEvent, _solver.Pivot);
            if (result.Launched)
            {
                _releaseDirection = result.LaunchDirection;
                PlayAt(yankEvent, shipPosition);
            }
            if (_solver.Mode != ThresherMode.Pivot)
            {
                SweepBall(result.BallFrom, _solver.BallPosition);
                if (_massMode) LayBallTrail(result.BallFrom, _solver.BallPosition, shipPosition);
            }
            HitVesselsAlong(result.BallFrom, _solver.BallPosition, _settings.BallRadius, _solver.BallVelocity);

            _links.Step(shipPosition + result.ShipVelocity * dt, _solver.BallPosition, _solver.Length,
                        iterations: ThresherChainLinks.IterationsFor(_links.Count - 1));
            CutWithChain(dt);

            IsTimingPerfect = _spectating && config && _solver.Mode == ThresherMode.Pivot &&
                              _solver.SecondsToLaunchPoint(shipPosition, _aimDir) <= config.PerfectWindowSeconds;

            UpdateCombo(dt);
            IsReady = _payOut && _solver.Mode == ThresherMode.Free &&
                      _solver.PredictReelCrackSpeed(shipPosition, result.ShipVelocity)
                          >= _settings.SmashSpeed * (config ? config.ReadyMargin : 1f);
            return result.ShipVelocity;
        }

        void EnsureSeeded(Vector3 shipPosition, Vector3 shipVelocity)
        {
            // Seed on first use, and RE-seed after a teleport (respawn, pose set): a ball left
            // three chain-lengths away would otherwise be hauled back across the arena by the rope.
            bool stranded = _seeded &&
                (_solver.BallPosition - shipPosition).sqrMagnitude >
                (3f * _settings.MaxLength) * (3f * _settings.MaxLength);
            if (_seeded && !stranded) return;
            Vector3 forward = _status != null && _status.Transform ? _status.Transform.forward : Vector3.forward;
            _solver.Reset(shipPosition, forward, shipVelocity);
            _links.Reset(shipPosition, _solver.BallPosition);
            _seeded = true;
            _touching.Clear();
        }

        void LateUpdate()
        {
            if (_solver == null || _status == null || _status.Transform == null) return;

            // Fallback step for a hull nobody moved through the transformer this frame.
            if (_lastStepFrame != Time.frameCount && !IsVesselFrozen && Time.deltaTime > 0f)
            {
                Vector3 pos = _status.Transform.position;
                Vector3 vel = _seeded ? (pos - _lastShipPosition) / Time.deltaTime : Vector3.zero;
                StepChain(pos, vel, _status.Speed, Time.deltaTime);
            }

            UpdateVisuals();
            UpdateCamera();
        }

        // ------------------------------------------------------------------ the ball

        void SweepBall(Vector3 from, Vector3 to)
        {
            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable || _status == null) return;

            float radius = _settings.BallRadius;
            const float candidateExtent = 8f;   // prism half-extent allowance; refined below
            index.QuerySegment(from, to, radius + candidateExtent, _candidates);

            Vector3 ab = to - from;
            float abLenSq = ab.sqrMagnitude;
            _hits.Clear();
            for (int i = 0; i < _candidates.Count; i++)
            {
                var prism = _candidates[i];
                if (!prism || prism.destroyed) continue;
                Vector3 centre = prism.transform.position;
                float t = abLenSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(centre - from, ab) / abLenSq) : 0f;
                float contact = radius + 0.5f * prism.transform.lossyScale.magnitude;
                if ((centre - (from + ab * t)).sqrMagnitude > contact * contact) continue;
                _hits.Add((t, prism));
            }
            _hits.Sort((a, b) => a.t.CompareTo(b.t));

            Domains own = PilotDomain();
            bool breaker = BreakerActive;

            _touchingNext.Clear();
            for (int i = 0; i < _hits.Count; i++)
            {
                var prism = _hits[i].prism;
                if (!prism || prism.destroyed) continue;
                // The fleet's one own-trail rule: a vessel never hits mass it laid moments ago.
                if (SelfTrailContactConfigSO.SuppressesSkimContact(prism, _status)) continue;

                _touchingNext.Add(prism);
                if (_touching.Contains(prism)) continue;   // one hit per contact

                Vector3 at = from + ab * _hits[i].t;
                float speed = _solver.BallSpeed;
                bool hot = ThresherChainSolver.IsSmash(speed, _settings);
                bool ownDomain = prism.Domain == own;

                if (_massMode)
                {
                    // Mass Mode builds instead of breaking, at any speed: it shields its own and
                    // steals the rival's, keeps all its speed, and never explodes.
                    MassModeTouch(prism, ownDomain, at);
                    continue;
                }

                if (ownDomain && !hot)
                {
                    // Bounce: the ball never harms its own domain unless it is red-hot.
                    Vector3 normal = at - prism.transform.position;
                    if (_solver.Bounce(at, normal)) PlayAt(bounceEvent, at);
                    break;   // the ball's path this frame ends at the bounce
                }

                Vector3 impact = _solver.BallVelocity * (config ? config.DebrisRestitution : 1f / 3f);
                float debrisLimit = config ? config.DebrisSpeedLimit : 200f;
                // Space L5 "Breaker": a red ball's direct hit breaks super-shielded mass, through the
                // sanctioned sequence (shields off, then a devastating hit - Prism.Damage's own doc).
                if (hot && breaker && prism.prismProperties is { IsSuperShielded: true })
                {
                    prism.DeactivateShields(impact, debrisLimit);
                    prism.Damage(impact, _status.Domain, _status.PlayerName, devastate: true, debrisSpeedLimit: debrisLimit);
                }
                else
                    prism.Damage(impact, _status.Domain, _status.PlayerName, debrisSpeedLimit: debrisLimit);
                _solver.RegisterHit(ThresherChainSolver.KeepAfterHit(speed, _settings));

                // A rival hit releases the chain's banked energy even below smash speed; a smash
                // explodes anyway, and spends the bank on the way.
                float banked = _energy;
                bool exploded = (hot || (!ownDomain && banked > 0f)) && Explode(at);
                if (hot) OnSmash(at, speed, exploded, banked);
                else if (exploded) OnEnergyBlast(at, banked);
                else OnCrush(at);
            }

            (_touching, _touchingNext) = (_touchingNext, _touching);
        }

        void OnSmash(Vector3 at, float speed, bool exploded, float banked)
        {
            float heat = ThresherChainSolver.Heat01(speed, _settings);
            _combo++;
            _coolTime = 0f;
            PlayAt(smashEvent, at);
            if (!IsLocalPilot() || !config) return;

            if (_combo == 1)
                HitStop(Mathf.Lerp(config.HitStopMinSeconds, config.HitStopMaxSeconds, heat));
            float bank = exploded ? EnergyScale(banked) : 1f;
            Shake(Mathf.Lerp(config.ShakeMin, config.ShakeMax, heat) * (_combo == 1 ? 1f : 0.6f) * bank, config.ShakeSeconds);
            HapticController.PlaySkim(Mathf.Lerp(config.HapticFloor01, 1f, heat));
        }

        /// <summary>A slow ball's rival hit that released the chain's bank: the blast's own beat.</summary>
        void OnEnergyBlast(Vector3 at, float banked)
        {
            PlayAt(smashEvent, at);
            if (!IsLocalPilot() || !config) return;
            float scale = EnergyScale(banked);
            Shake(config.ShakeMin * scale, config.ShakeSeconds);
            HapticController.PlaySkim(Mathf.Clamp01(config.HapticFloor01 * scale));
        }

        /// <summary>The explosion-diameter multiplier a bank of <paramref name="energy"/> buys.</summary>
        float EnergyScale(float energy)
            => config ? Mathf.Min(EnergyCap(), 1f + config.EnergyDiameterPerUnit * Mathf.Max(0f, energy)) : 1f;

        void Bank(float amount)
        {
            if (!config || amount <= 0f) return;
            _energy = Mathf.Min(config.EnergyMax, _energy + amount);
        }

        /// <summary>Mass Mode's touch: shield own-domain mass (once — an already armoured prism is
        /// left alone), steal rival mass (a shielded rival sheds its shield first and changes hands
        /// on the next touch, the fleet's steal rule; super-shielded mass refuses).</summary>
        void MassModeTouch(Prism prism, bool ownDomain, Vector3 at)
        {
            var props = prism.prismProperties;
            if (ownDomain)
            {
                if (props == null || props.IsShielded || props.IsSuperShielded) return;
                prism.ActivateShield();
                return;
            }
            if (props != null && props.IsSuperShielded) return;
            prism.Steal(_status.PlayerName, _status.Domain);
            if (prism.Domain == _status.Domain) Bank(config ? config.EnergyPerSteal : 0f);
            PlayAt(crushEvent, at);
        }

        /// <summary>
        /// Mass Mode's trail: one prism of the pilot's domain per <c>massTrailSpacing</c> of the
        /// ball's travel, laid along its path, through the SAME spawn channel and prism type the
        /// hull's own trail uses (so it is ordinary, conserved trail mass). Owed in distance and paid
        /// in whole prisms, capped per frame with the excess DROPPED (vessel rule 17's shape), so a
        /// hitch never dumps a burst. Stamped with <c>ownerID</c>, so the fleet's own-fresh-trail
        /// rule keeps the ball and chain from hitting what they just laid.
        /// </summary>
        void LayBallTrail(Vector3 from, Vector3 to, Vector3 shipPosition)
        {
            var spawner = _status?.VesselPrismController;
            var channel = spawner ? spawner.PrismSpawnChannel : null;
            if (!config || !channel) return;

            Vector3 path = to - from;
            float travelled = path.magnitude;
            if (travelled < 1e-4f) return;
            Vector3 dir = path / travelled;

            float spacing = config.MassTrailSpacing;
            _trailOwed += travelled;
            int lays = Mathf.Min(Mathf.FloorToInt(_trailOwed / spacing), config.MassTrailMaxPerFrame);
            if (lays <= 0) return;
            _trailOwed = Mathf.Min(_trailOwed - lays * spacing, spacing);

            Vector3 up = Vector3.ProjectOnPlane(shipPosition - to, dir);
            if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.up, dir);
            if (!SafeLookRotation.TryGet(dir, up.normalized, out var rot, this, logError: false)) return;

            for (int i = 0; i < lays; i++)
            {
                // Spread this frame's lays back along its path, newest at the ball.
                Vector3 pos = Vector3.Lerp(from, to, (i + 1f) / lays);
                LayTrailPrism(channel, spawner.SpawnPrismType, pos, rot);
            }
        }

        /// <summary>One ball-trail prism. MASS sizes it (<c>massTrailScale</c>, every axis, read at
        /// lay time); Mass L5 "Armoured Trail" lays it shielded (per-lay snapshot of the unlock bit).</summary>
        void LayTrailPrism(PrismEventChannelWithReturnSO channel, PrismType type, Vector3 pos, Quaternion rot)
        {
            Vector3 scale = config.MassTrailPrismScale * Mathf.Max(0.1f, config.MassTrailScale.EvaluateReplicated(_status));
            var ret = channel.RaiseEvent(new PrismEventData
            {
                ownDomain = _status.Domain,
                Rotation = rot,
                SpawnPosition = pos,
                Scale = scale,
                PrismType = type,
            });
            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism)) return;

            prism.TargetScale = scale;
            prism.ownerID = _status.PlayerName;
            prism.ChangeTeam(_status.Domain);
            prism.waitTime = config.MassTrailWaitTime;
            if (ArmouredTrailActive) prism.prismProperties.IsShielded = true;   // Initialize applies it
            _ballTrail.Add(prism);
            prism.prismProperties.Index = (ushort)Mathf.Min(_ballTrail.TrailList.Count - 1, ushort.MaxValue);
            prism.Initialize(_status.PlayerName);
            // A Mass-scaled prism is a stated size: admit it past the pool's scale window AFTER
            // Initialize (whose reset re-clamps it), or a big one is trimmed with no error.
            prism.AdmitTargetScale(scale);
            prism.TargetScale = scale;
            // AFTER Initialize: pool-reuse reset clears trail membership (AssignTrail's contract).
            prism.AssignTrail(_ballTrail);
        }

        // ------------------------------------------------------------------ enemy vessels (petals only)

        /// <summary>
        /// Every ENEMY hull within <paramref name="radius"/> + <c>vesselHitRadius</c> of the segment
        /// <paramref name="a"/>→<paramref name="b"/> takes a hit (<see cref="TryHitVessel"/>). The
        /// ball's swept path and each chain link both come through here.
        /// </summary>
        void HitVesselsAlong(Vector3 a, Vector3 b, float radius, Vector3 velocity)
        {
            if (!config || _status == null || !_gameData || _gameData.Players == null) return;
            float reach = radius + config.VesselHitRadius;
            Vector3 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var victim = EnemyStatus(players[i]);
                if (victim == null) continue;
                Vector3 p = victim.Transform.position;
                float t = abLenSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLenSq) : 0f;
                if ((p - (a + ab * t)).sqrMagnitude > reach * reach) continue;
                TryHitVessel(victim, velocity);
            }
        }

        /// <summary>The player's vessel status if it is a live ENEMY of this pilot (another pilot,
        /// another domain), else null.</summary>
        IVesselStatus EnemyStatus(IPlayer player)
        {
            var victim = player?.Vessel?.VesselStatus;
            if (victim == null || ReferenceEquals(victim, _status)) return null;
            if (victim.Domain == _status.Domain || !victim.Transform) return null;
            return victim;
        }

        /// <summary>
        /// One contact on an enemy hull, under the LOCKED PvP rule: the ONLY thing it does is take
        /// petals, as a scored combat hit. Decided on exactly one machine — the one that owns this
        /// hull (every peer simulates the ball, so every peer sees the contact) — admitted through
        /// <see cref="CombatHitDrain.TryAdmit"/> (a warded victim is neither scored on nor robbed;
        /// <c>vesselHitCooldownSeconds</c> per victim), then a Strike-form STEAL of
        /// <c>vesselHitPetals</c> (CHARGE) petals per element and the score on the stats channel.
        /// </summary>
        void TryHitVessel(IVesselStatus victim, Vector3 velocity)
        {
            if (!ElementalTransfer.IsDecidedHere(_status)) return;
            if (_status.Player is { IsNetworkOwner: false }) return;
            if (!CombatHitDrain.TryAdmit(victim, _status, CombatHitClass.Strike, config.VesselHitCooldownSeconds,
                                         ElementalDebuffSources.VesselContact, out int superseded))
                return;

            float petals = Mathf.Max(0f, config.VesselHitPetals.EvaluateReplicated(_status));
            ElementalTransfer.ApplyAllAuthoritative(CombatHitDrain.FormFor(CombatHitClass.Strike), victim, _status,
                petals * CombatHitDrain.NormalizedPerLevel, velocity, ElementalDebuffSources.VesselContact);
            config.OnCombatHitLanded.Raise(new CombatHitStats
            {
                ShooterName = _status.PlayerName,
                VictimName = victim.PlayerName,
                HitClass = CombatHitClass.Strike,
                SupersededRank = superseded,
            });
        }

        /// <summary>Charge L5 "Magnet": draw a free ball toward the nearest enemy hull within
        /// <c>magnetRange</c>. It moves only OUR ball — never the other vessel (PvP is petals only).</summary>
        void PullBallToEnemy(float dt)
        {
            if (!config || _status == null || !_gameData || _gameData.Players == null || _solver == null) return;
            Vector3 ball = _solver.BallPosition;
            float best = config.MagnetRange * config.MagnetRange;
            Vector3 target = default;
            bool found = false;
            var players = _gameData.Players;
            for (int i = 0; i < players.Count; i++)
            {
                var victim = EnemyStatus(players[i]);
                if (victim == null) continue;
                float d = (victim.Transform.position - ball).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                target = victim.Transform.position;
                found = true;
            }
            if (found) _solver.AttractBall(target, config.MagnetAcceleration, dt);
        }

        void OnCrush(Vector3 at)
        {
            PlayAt(crushEvent, at);
            if (config && IsLocalPilot())
                Shake(config.ShakeMin * config.CrushShakeFraction, config.ShakeSeconds * 0.5f);
        }

        /// <summary>
        /// The smash explosion: it destroys every domain's prisms (<c>AffectSelfOverride</c> true, the
        /// Grizzly charged shot's setting).
        /// Rate-limited so a hot ball through a row makes a string of blasts, not one per prism.
        /// The chain's banked energy rides the blast that actually goes off (its diameter ×
        /// <see cref="EnergyScale"/>) and is spent by it; a rate-limited hit keeps the bank for the
        /// next one. Returns true if an explosion was spawned.
        /// </summary>
        bool Explode(Vector3 at)
        {
            if (!config || config.SmashExplosions is not { Length: > 0 } || _status == null) return false;
            if (Time.time < _nextExplosionTime) return false;
            _nextExplosionTime = Time.time + config.ExplosionMinInterval;

            float diameter = config.ExplosionDiameter * EnergyScale(_energy);
            _energy = 0f;
            Vector3 v = _solver.BallVelocity;
            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain = _status.Domain,
                AnnonymousExplosion = false,
                Vessel = _status.Vessel,
                OverrideMaterial = _status.AOEExplosionMaterial,
                MaxScale = diameter,
                SpawnPosition = at,
                SpawnRotation = v.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(v) : Quaternion.identity,
                AffectSelfOverride = true,
            };
            ExplosionHelper.CreateExplosion(config.SmashExplosions, init, _impactor ? _impactor.DIContainer : null);
            return true;
        }

        // ------------------------------------------------------------------ the chain

        /// <summary>
        /// The chain, taut or slack: every RIVAL prism a link touches is sliced along the plane the
        /// chain is sweeping (and banks energy for the ball's next blast when it dies), and every
        /// prism of the pilot's OWN domain it touches is shielded. A shielded rival loses its shield
        /// to the first cut and dies to the next; super-shielded mass refuses. The fleet's
        /// own-fresh-trail grace applies to both: what this pilot laid moments ago is left alone.
        /// Each link also reaches enemy HULLS (<see cref="HitVesselsAlong"/>).
        /// </summary>
        void CutWithChain(float dt)
        {
            if (!config || _status == null || _links == null) return;

            var pts = _links.Points;
            for (int s = 0; s < pts.Length - 1; s++)
                HitVesselsAlong(pts[s], pts[s + 1], config.ChainCutRadius,
                    0.5f * (_links.NodeVelocity(s, dt) + _links.NodeVelocity(s + 1, dt)));

            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;

            Domains own = PilotDomain();
            float cut = config.ChainCutRadius;
            const float candidateExtent = 8f;
            _cutThisFrame.Clear();

            int n = pts.Length;
            for (int s = 0; s < n - 1; s++)
            {
                Vector3 a = pts[s], b = pts[s + 1];
                index.QuerySegment(a, b, cut + candidateExtent, _candidates);
                if (_candidates.Count == 0) continue;

                Vector3 ab = b - a;
                float abLenSq = ab.sqrMagnitude;
                for (int i = 0; i < _candidates.Count; i++)
                {
                    var prism = _candidates[i];
                    if (!prism || prism.destroyed || _cutThisFrame.Contains(prism)) continue;
                    var props = prism.prismProperties;
                    bool ownDomain = prism.Domain == own;
                    // Nothing to do to armoured own mass, or to a super-shielded rival.
                    if (ownDomain && (props == null || props.IsShielded || props.IsSuperShielded)) continue;
                    if (!ownDomain && props != null && props.IsSuperShielded) continue;
                    if (SelfTrailContactConfigSO.SuppressesSkimContact(prism, _status)) continue;

                    Vector3 centre = prism.transform.position;
                    float t = abLenSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(centre - a, ab) / abLenSq) : 0f;
                    float contact = cut + 0.5f * prism.transform.lossyScale.magnitude;
                    Vector3 onChain = a + ab * t;
                    if ((centre - onChain).sqrMagnitude > contact * contact) continue;

                    _cutThisFrame.Add(prism);
                    if (ownDomain)
                    {
                        prism.ActivateShield();
                        continue;
                    }

                    // The link's own velocity is the blade's: the chain point moves between the
                    // hull's velocity and the ball's (a lever arm), so the debris does too.
                    Vector3 v = Vector3.Lerp(_links.NodeVelocity(s, dt), _links.NodeVelocity(s + 1, dt), t);
                    Vector3 cutNormal = Vector3.Cross(ab, v);
                    if (cutNormal.sqrMagnitude < 1e-8f) cutNormal = Vector3.Cross(ab, Vector3.up);
                    if (cutNormal.sqrMagnitude < 1e-8f) cutNormal = Vector3.right;

                    prism.Slice(v * config.ChainDebrisRestitution, _status.Domain, _status.PlayerName,
                                onChain, cutNormal.normalized, debrisSpeedLimit: config.DebrisSpeedLimit);
                    if (prism.destroyed) Bank(config.EnergyPerSlice);   // a cut that only popped a shield banks nothing
                    PlayAt(sliceEvent, onChain);
                }
            }
        }

        // ------------------------------------------------------------------ feedback

        Domains PilotDomain() => _status?.Player != null ? _status.Domain : Domains.Blue;

        bool IsLocalPilot()
            => _status?.Player != null && _status.IsLocalUser && !_status.AutoPilotEnabled;

        void UpdateCombo(float dt)
        {
            if (_combo == 0) return;
            if (ThresherChainSolver.IsSmash(_solver.BallSpeed, _settings)) { _coolTime = 0f; return; }
            _coolTime += dt;
            if (_coolTime >= (config ? config.ComboGraceSeconds : 0.4f)) _combo = 0;
        }

        void PlayAt(EventReference evt, Vector3 at)
        {
            if (evt.IsNull) return;
            _registry?.AudioSystem?.PlaySFXEvent(evt, at);
        }

        static void Shake(float intensity, float duration)
        {
            if (CameraManager.Instance != null &&
                CameraManager.Instance.GetActiveController() is CustomCameraController cam)
                cam.Shake(intensity, duration);
        }

        /// <summary>
        /// One hit-stop per whip. SOLO: a brief global time-scale drop (the AstroLeague ball's
        /// pattern, restoring to constants so it cannot clobber a pause). MULTIPLAYER: a local
        /// timescale would freeze every peer's world on this client, so only this hull and its
        /// ball hold still.
        /// </summary>
        void HitStop(float seconds)
        {
            if (seconds <= 0f) return;
            if (IsSoloSession()) RunTimeScaleHitStop(seconds).Forget();
            else _freezeUntilUnscaled = Time.unscaledTime + seconds;
        }

        static bool IsSoloSession()
        {
            var nm = NetworkManager.Singleton;
            return nm == null || !nm.IsListening || nm.ConnectedClientsIds.Count <= 1;
        }

        async UniTaskVoid RunTimeScaleHitStop(float seconds)
        {
            if (s_timeScaleHitStopActive || PauseSystem.Paused) return;
            s_timeScaleHitStopActive = true;
            float scale = config ? config.HitStopTimeScale : 0.05f;
            float baseFixedDelta = Time.fixedDeltaTime / Mathf.Max(Time.timeScale, 0.0001f);
            Time.timeScale = scale;
            Time.fixedDeltaTime = baseFixedDelta * Mathf.Max(scale, 0.0001f);
            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(seconds), ignoreTimeScale: true,
                                    cancellationToken: _cts?.Token ?? CancellationToken.None);
            }
            catch (OperationCanceledException) { }
            finally
            {
                Time.timeScale = PauseSystem.Paused ? 0f : 1f;
                Time.fixedDeltaTime = baseFixedDelta;
                s_timeScaleHitStopActive = false;
            }
        }

        // ------------------------------------------------------------------ camera

        // The chase camera this hull is driving (null when it drives none), the distance it has
        // eased it to, and the spectate vantage's frame while detached.
        CustomCameraController _cam;
        float _camDistance;
        float _camWritten = float.NaN;   // the last distance this hull wrote (negative z)
        bool _spectating;
        float _sightZoom01;
        ThresherLaunchSight _sight;
        // The eased entry/exit: progress 0..1, the pose it started from, and whether we are easing OUT.
        float _camEase;
        Vector3 _easeFromPos;
        Quaternion _easeFromRot = Quaternion.identity;
        bool _exiting;
        float _spectateQualifyTime;
        // The AIM while spectating: where a release sends the ship, and the aim camera's "above".
        Vector3 _aimDir, _aimUp;

        /// <summary>
        /// The local pilot's camera, two jobs (<c>THRESHER.md</c> § Camera):
        /// <list type="bullet">
        /// <item><b>Keep the ball in frame.</b> The chase distance is pulled back as far as the
        /// rendered ball needs (<see cref="ThresherCameraFraming.RequiredDistance"/>), FAST, and
        /// eased back toward the prefab's own distance SLOWLY — never nearer than it.</item>
        /// <item><b>Spectate and AIM a spin.</b> Once a planted orbit circles faster than
        /// <c>spectateSpinRate</c>, the camera stops riding the spinning hull and looks DOWN on the
        /// orbit from over the shoulder of the launch path; the sticks then turn that aim (and the
        /// camera with it) instead of tilting the orbit, a line shows the path, and releasing LT
        /// flies the ship off along it at once (<see cref="EndPlant"/>). It re-attaches when the
        /// plant is released or the spin falls back under the release fraction.</item>
        /// </list>
        /// Pose only, through <see cref="CustomCameraController.SetCameraDistance"/> and
        /// <see cref="CustomCameraController.Spectate"/>; FOV belongs to the speed tunnel and is
        /// only READ here. Remote replicas and AI hulls never touch a camera.
        /// </summary>
        void UpdateCamera()
        {
            if (!config || !config.CameraFraming || !_seeded || !IsLocalPilot()) { ReleaseCamera(); return; }

            var cam = CameraManager.Instance != null
                ? CameraManager.Instance.GetActiveController() as CustomCameraController
                : null;
            if (cam != _cam)
            {
                ReleaseCamera();
                _cam = cam;
                if (_cam) _camDistance = Mathf.Abs(_cam.GetCameraDistance());
            }
            if (!_cam || !_cam.Camera || _cam.PlacementAnchor.HasValue) return;

            // Only while the camera is following THIS hull: a cinematic or end-of-match camera that
            // has been pointed elsewhere is not ours to move.
            Transform ship = _status.Transform;
            Transform followed = _cam.FollowTarget;
            if (!followed || (followed != ship && !followed.IsChildOf(ship))) { ReleaseCamera(); return; }

            float dt = Time.unscaledDeltaTime;
            float ballRadius = _settings.BallRadius * config.BallVisualScale;
            float halfFovV = 0.5f * _cam.Camera.fieldOfView * Mathf.Deg2Rad;
            float halfFovH = Mathf.Atan(Mathf.Tan(halfFovV) * Mathf.Max(0.1f, _cam.Camera.aspect));

            // Zoom. The prefab's distance is re-read every frame, so a settings change still owns it.
            float neutral = Mathf.Abs(_cam.NeutralOffsetZ);
            if (neutral <= 0f) neutral = _camDistance;
            Vector3 ballLocal = ship.InverseTransformDirection(_solver.BallPosition - ship.position);
            float height = _cam.GetFollowOffset().y;
            float need = ThresherCameraFraming.RequiredDistance(ballLocal, ballRadius, height,
                halfFovV, halfFovH, config.CameraFramingMargin, config.CameraMinAhead);
            // The REACH envelope (ThresherCameraFraming.ReachDistance): what a ball anywhere on the
            // chain's horizontal circle at its CURRENT length would need. Without it the target
            // tracked the ball round every swing (abeam 161 u, ahead 15 u at full let-out) and the
            // camera pumped in and out at the swing's rhythm; with it the distance follows the
            // winch, and the ball's own position only adds (a ball looped overhead).
            need = Mathf.Max(need, ThresherCameraFraming.ReachDistance(_solver.Length,
                ballRadius, height, halfFovV, halfFovH, config.CameraFramingMargin, config.CameraMinAhead));
            float target = Mathf.Clamp(need, neutral, Mathf.Max(neutral, config.CameraMaxDistance));
            _camDistance = ThresherCameraFraming.Ease(_camDistance, target,
                config.CameraZoomOutRate, config.CameraZoomInRate, dt);
            _camWritten = -_camDistance;
            _cam.SetCameraDistance(_camWritten);
            _cam.MaxFollowTurnRate = config.CameraMaxFollowTurnRate;

            // Spectate.
            float spin = IsPivoting ? ThresherCameraFraming.SpinRate(_solver.LockSpeed, _solver.Length) : 0f;
            // Detach only once the spin has held over the threshold for spectateEngageSeconds, so a
            // tap of LT does not bob the camera up toward the vantage and straight back down.
            bool qualifies = IsPivoting && config.SpectateSpinRate > 0f && spin >= config.SpectateSpinRate;
            _spectateQualifyTime = qualifies ? _spectateQualifyTime + dt : 0f;
            if (!_spectating && qualifies && _spectateQualifyTime >= config.SpectateEngageSeconds)
                BeginSpectate(ship.position);
            else if (_spectating && (!IsPivoting || spin < config.SpectateSpinRate * config.SpectateReleaseFraction))
                EndSpectate(smooth: true);

            if (!_spectating)
            {
                if (_exiting) TickExit(dt);
                return;
            }

            // AIM. The sticks turn the launch direction in screen terms (right is right, the hull's
            // pitch sign up/down) until a release ARMS it; then the point is chosen and held.
            var input = _status.InputStatus;
            if (input != null && !_solver.LaunchArmed)
                ThresherCameraFraming.TurnAim(ref _aimDir, ref _aimUp, input.XSum, input.YSum,
                    config.AimYawRate * Mathf.Deg2Rad, config.AimPitchRate * Mathf.Deg2Rad, dt);
            // The whole spin turns with the camera: the orbit is laid into the plane the aim lives in.
            _solver.SteerOrbitPlane(_aimUp);

            // The launch point sits on the circle the ship is ACTUALLY flying (LaunchPoint), not on
            // the chain's length, which runs ahead of the orbit while the winch pays out.
            Vector3 launch = _solver.LaunchPoint(ship.position, _aimDir);
            // Follow behind the launch point ON the trajectory, at the hull's own chase distance and
            // (shipped) no height offset: the trajectory is the screen centre - the crosshair.
            float follow = config.AimCameraDistance > 0f ? config.AimCameraDistance
                : Mathf.Max(1f, Mathf.Abs(_cam.NeutralOffsetZ));
            Vector3 eye = ThresherCameraFraming.LaunchCameraPosition(launch, _aimDir, _aimUp, follow,
                config.AimElevationDegrees);
            if (!SafeLookRotation.TryGet(launch - eye, _aimUp, out var look, this, logError: false))
                look = _cam.transform.rotation;
            // ENTER smoothly: ease from the pose the camera was ACTUALLY in when the spin was
            // detected to the launch vantage, on this executor's own clock. The camera's blend only
            // hands over (spectateBlendSeconds, short) from a vantage that starts exactly where the
            // camera already is - so the still-spinning chase pose underneath never leaks in.
            _camEase = Mathf.Min(1f, _camEase + dt / Mathf.Max(0.01f, config.SpectateEnterSeconds));
            ShowVantage(_easeFromPos, _easeFromRot, eye, look, _camEase);

            // The launch sight: the crosshair (lime while the timing is right) and the Time-petal
            // zoom window down the same trajectory, zoomed by LT's depth alone.
            _sightZoom01 = Mathf.MoveTowards(_sightZoom01,
                ThresherCameraFraming.ZoomTarget(input != null ? input.LeftTriggerAnalog : 0f, config.SightZoomDeadzone),
                config.SightZoomResponse * dt);
            if (!_sight) _sight = ThresherLaunchSight.Create(name);
            _sight.Tick(launch, _aimDir, _aimUp,
                Mathf.Lerp(config.SightWideFieldOfView, config.SightZoomFieldOfView, _sightZoom01),
                IsTimingPerfect ? ReadyColor().gamma : new Color(1f, 1f, 1f, 0.9f));
        }

        /// <summary>Start aiming at the moment of detaching: the aim starts as the way the camera
        /// was facing laid into the orbit plane (so the launch camera looks the way the chase camera
        /// did), and the aim's "above" is the orbit's axis on the camera's own side, so the launch
        /// camera rises over the orbit rather than dropping under it.</summary>
        void BeginSpectate(Vector3 shipPosition)
        {
            Vector3 pivot = _solver.Pivot;
            Vector3 outward = shipPosition - pivot;
            Vector3 normal = Vector3.Cross(outward, _lastShipVelocity);
            if (normal.sqrMagnitude < 1e-6f || outward.sqrMagnitude < 1e-6f) return;
            normal.Normalize();
            if (Vector3.Dot(normal, _cam.transform.position - pivot) < 0f) normal = -normal;

            Vector3 aim = Vector3.ProjectOnPlane(_cam.transform.forward, normal);
            if (aim.sqrMagnitude < 1e-6f) aim = _lastShipVelocity;
            if (aim.sqrMagnitude < 1e-6f) return;

            _aimDir = aim.normalized;
            _aimUp = normal;
            _sightZoom01 = 0f;
            CaptureEaseStart();
            _exiting = false;
            _spectating = true;
        }

        /// <summary>
        /// Leave the launch view. <paramref name="smooth"/> (the launch, or the spin falling away):
        /// keep holding a vantage and EASE it from where the camera is to the live chase pose behind
        /// the hull (<see cref="TickExit"/>), then hand back to the follow camera once the two agree.
        /// Otherwise (a teardown: disable, swap, lost pilot) drop the vantage at once.
        /// </summary>
        void EndSpectate(bool smooth = false)
        {
            if (_spectating) _solver?.SteerOrbitPlane(Vector3.zero);   // the orbit's plane is the ship's again
            if (_sight) _sight.Hide();
            bool was = _spectating;
            _spectating = false;
            if (smooth && was && _cam)
            {
                CaptureEaseStart();
                _exiting = true;
                return;
            }
            _exiting = false;
            if (_cam && (was || _cam.Spectate.HasValue)) _cam.Spectate = null;
        }

        /// <summary>
        /// One frame of the eased EXIT: from the launch vantage the camera was holding to the chase
        /// pose the follow camera will take behind the hull — computed from the hull's live pose and
        /// the camera's own follow offset, so a hull still swinging onto its new heading is tracked,
        /// not chased by a turn-capped frame. At the end the follow frame is snapped to the hull
        /// (<see cref="CustomCameraController.SnapToTarget"/>) and the vantage released over the
        /// short hand-over blend, between two poses that already coincide.
        /// </summary>
        void TickExit(float dt)
        {
            Transform target = _cam ? _cam.FollowTarget : null;
            if (!target) { _exiting = false; if (_cam) _cam.Spectate = null; return; }
            Vector3 offset = _cam.GetFollowOffset();
            Vector3 eye = target.position + target.rotation * offset;
            if (!SafeLookRotation.TryGet(target.position - eye, target.up, out var look, this, logError: false))
                look = target.rotation;
            _camEase = Mathf.Min(1f, _camEase + dt / Mathf.Max(0.01f, config.SpectateExitSeconds));
            ShowVantage(_easeFromPos, _easeFromRot, eye, look, _camEase);
            if (_camEase < 1f) return;
            _cam.SnapToTarget();
            _cam.SpectateBlendSeconds = config.SpectateBlendSeconds;
            _cam.Spectate = null;
            _exiting = false;
        }

        void CaptureEaseStart()
        {
            _easeFromPos = _cam.transform.position;
            _easeFromRot = _cam.transform.rotation;
            _camEase = 0f;
        }

        /// <summary>Hold the vantage at <c>SmoothStep(t)</c> of the way from one pose to another.</summary>
        void ShowVantage(Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float t)
        {
            float w = Mathf.SmoothStep(0f, 1f, t);
            Vector3 pos = Vector3.Lerp(fromPos, toPos, w);
            Quaternion rot = Quaternion.Slerp(fromRot, toRot, w);
            _cam.SpectateBlendSeconds = config.SpectateBlendSeconds;
            _cam.Spectate = new CustomCameraController.SpectateView
            {
                Position = pos,
                LookAt = pos + rot * Vector3.forward * 100f,
                Up = rot * Vector3.up,
            };
        }

        /// <summary>Hand the camera back: no vantage, and the prefab's own distance — unless
        /// something else has written the distance since this hull last did (a vessel swap's
        /// settings landing first), in which case that write stands.</summary>
        void ReleaseCamera()
        {
            _spectateQualifyTime = 0f;
            if (!_cam) { _cam = null; _spectating = false; _exiting = false; _camWritten = float.NaN; if (_sight) _sight.Hide(); return; }
            EndSpectate();
            _cam.MaxFollowTurnRate = 0f;
            float neutral = _cam.NeutralOffsetZ;
            if (neutral < 0f && _cam.GetCameraDistance() == _camWritten) _cam.SetCameraDistance(neutral);
            _cam = null;
            _camWritten = float.NaN;
        }

        // ------------------------------------------------------------------ colours

        SO_ColorSet Palette => _gameData && _gameData.ThemeManagerData ? _gameData.ThemeManagerData.ColorSet : null;

        Color DomainColor(Domains domain)
        {
            var palette = Palette;
            if (domain == Domains.Blue || !palette) return config.FallbackDomainColor;
            return palette.GetDomainSignalColor(domain);
        }

        /// <summary>The LIT domain colour: the domain's shielded-prism rim, the brightest (HDR)
        /// colour the palette authors for it.</summary>
        Color LitColor(Domains domain)
        {
            var palette = Palette;
            if (domain == Domains.Blue || !palette ||
                !palette.TryGetPrismKindColors(domain, PrismKind.Shielded, out var rim, out _))
                return DomainColor(domain) * 2.5f;
            return rim;
        }

        Color DangerColor()
        {
            var palette = Palette;
            if (!palette) return config.FallbackDangerColor;
            var c = palette.GetDangerSignalColor();
            return c.a > 0f ? c : config.FallbackDangerColor;
        }

        Color ReadyColor()
        {
            var palette = Palette;
            if (!palette) return config.FallbackReadyColor;
            var c = palette.GetCtaSignalColor();
            return c.a > 0f ? c : config.FallbackReadyColor;
        }

        /// <summary>The ball's colour this frame: the pilot's domain colour, brightening as it nears
        /// smash speed; RED once it gets there (destructive to all); LIT domain colour instead of
        /// red with the Charge upgrade.</summary>
        Color BallColor(out float emission)
        {
            Domains domain = PilotDomain();
            // Mass Mode never smashes, so it never shows red: a builder's ball wears its domain's
            // LIT colour (it is armouring its own mass) at a steady glow.
            if (_massMode)
            {
                emission = 1.5f;
                return LitColor(domain);
            }
            if (IsHot)
            {
                float heat = ThresherChainSolver.Heat01(_solver.BallSpeed, _settings);
                emission = Mathf.Lerp(2.5f, 4f, heat);
                return DangerColor();
            }
            float g = Gauge01;
            emission = Mathf.Lerp(0.15f, 1.2f, g * g);
            return DomainColor(domain) * Mathf.Lerp(0.45f, 1f, g);
        }

        /// <summary>
        /// The fresnel ball's two colours (<c>ThresherBallFresnelShader</c>): the ball's HUE
        /// (<see cref="BallColor"/>) normalised by its max channel, so brightness is set by the
        /// ball's state and never by how bright the palette happened to author the hue. Cool, the
        /// body rises from under the bloom threshold toward the 0.5 clamp as the ball nears smash
        /// speed and the rim from a soft edge toward 1.0; hot, the body sits ON the clamp (the whole
        /// ball blooms) and the rim whitens with heat. Nothing exceeds 1.0: tonemapping is None,
        /// so a channel over 1 clips and shifts the hue (Docs/PALETTE.md §2.2).
        /// </summary>
        void BallShade(out Color body, out Color rim)
        {
            BallShadeForState(out body, out rim);
            // The bank: the rim leans toward the CTA lime as the chain's kills charge the next
            // explosion, so a charged ball is legible before it hits anything.
            float e = Energy01;
            if (e > 0f) rim = Color.Lerp(rim, WithMaxChannel(ReadyColor(), config.BallRimHot), e * config.EnergyRimTint);
        }

        void BallShadeForState(out Color body, out Color rim)
        {
            Color hue = BallColor(out _);
            float g = Gauge01;
            if (_massMode)
            {
                body = WithMaxChannel(hue, config.BallBodyNearSmash);
                rim = WithMaxChannel(hue, config.BallRimHot);
                return;
            }
            if (IsHot)
            {
                float heat = ThresherChainSolver.Heat01(_solver.BallSpeed, _settings);
                body = WithMaxChannel(hue, config.BallBodyHot);
                rim = Color.Lerp(WithMaxChannel(hue, config.BallRimHot),
                                 new Color(config.BallRimHot, config.BallRimHot, config.BallRimHot, 1f),
                                 heat * config.BallHotRimWhiten);
                return;
            }
            float k = g * g;
            body = WithMaxChannel(hue, Mathf.Lerp(config.BallBodyCool, config.BallBodyNearSmash, k));
            rim = WithMaxChannel(hue, Mathf.Lerp(config.BallRimCool, config.BallRimHot, k));
        }

        /// <summary>The chain tube's two colours: an iron body (<c>chainColor</c>), and a rim in the
        /// ball's hue — dim while slack, brighter while taut (the state in which it cuts) — that
        /// turns the palette's CTA lime while READY. A target: the drawn colour eases to it.</summary>
        void ChainShade(out Color body, out Color rim)
        {
            body = config.ChainColor;
            body.a = 1f;
            if (IsReady)
            {
                rim = WithMaxChannel(ReadyColor(), config.ChainRimReady);
                return;
            }
            bool taut = _solver.Mode == ThresherMode.Pivot || _solver.IsTaut;
            rim = WithMaxChannel(BallColor(out _), taut ? config.ChainRimTaut : config.ChainRimSlack);
        }

        static Color WithMaxChannel(Color c, float max)
        {
            float m = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            return m > 1e-4f
                ? new Color(c.r / m * max, c.g / m * max, c.b / m * max, 1f)
                : new Color(max, max, max, 1f);
        }

        // ------------------------------------------------------------------ visuals

        // All runtime-built and cosmetic; explicitly a prototype look, replaced wholesale when art
        // lands. Every peer draws its own copy.
        GameObject _root;
        Transform _ball;
        MeshRenderer[] _ballRenderers;
        LineRenderer _chain;
        Vector3[] _chainDrawn;
        TrailRenderer _skid;
        TextMeshPro _comboLabel;
        MaterialPropertyBlock _mpb;
        MaterialPropertyBlock _chainMpb;

        // The colours actually DRAWN. Every state change (domain -> red at smash, slack -> taut,
        // -> READY lime) is a quick ease to the new colour (colorBlendRate), never a cut or a
        // flicker: the change is the signal, and a strobe read as noise.
        bool _shownValid;
        Color _shownHue, _shownBody, _shownRim, _shownChainRim;
        float _comboShownScale;
        int _comboShown;

        static Material s_lineMaterial, s_ballMaterial;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");

        void EnsureVisuals()
        {
            if (_root) return;
            var lineMat = LineMaterialOrFallback();
            var ballMat = BallMaterialOrFallback();
            if (!lineMat || !ballMat) return;   // headless: no graphics, no visuals

            _root = new GameObject($"ThresherVisuals::{name}");
            _mpb = new MaterialPropertyBlock();

            // The ball: a heavy core plus six studs, so it reads as a chunky wrecking ball and its
            // roll is visible.
            _ball = new GameObject("Ball").transform;
            _ball.SetParent(_root.transform, false);
            var parts = new List<MeshRenderer>();
            parts.Add(MakePrimitive(PrimitiveType.Sphere, _ball, Vector3.zero, Vector3.one, ballMat));
            Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var a in axes)
                parts.Add(MakePrimitive(PrimitiveType.Cube, _ball, a * 0.5f, Vector3.one * 0.32f, ballMat));
            _ballRenderers = parts.ToArray();

            _chain = MakeLine("Chain", chainMaterial ? chainMaterial : lineMat, false);
            _chain.alignment = LineAlignment.View;   // the fresnel tube is computed across a VIEW-aligned ribbon
            _chainMpb = new MaterialPropertyBlock();


            var skidGo = new GameObject("SkidTrail");
            skidGo.transform.SetParent(_ball, false);
            _skid = skidGo.AddComponent<TrailRenderer>();
            _skid.sharedMaterial = lineMat;
            _skid.time = config ? config.SkidTrailSeconds : 0.6f;
            _skid.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _skid.emitting = false;

            var labelGo = new GameObject("Combo", typeof(RectTransform));
            labelGo.transform.SetParent(_root.transform, false);
            _comboLabel = labelGo.AddComponent<TextMeshPro>();
            _comboLabel.alignment = TextAlignmentOptions.Center;
            _comboLabel.text = string.Empty;
            if (TMP_Settings.defaultFontAsset) _comboLabel.font = TMP_Settings.defaultFontAsset;
        }

        static MeshRenderer MakePrimitive(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            if (go.TryGetComponent(out Collider c)) Destroy(c);   // cosmetic: the sweep is the physics
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mr;
        }

        LineRenderer MakeLine(string label, Material mat, bool loop)
        {
            var go = new GameObject(label);
            go.transform.SetParent(_root.transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            return lr;
        }

        Material LineMaterialOrFallback()
        {
            if (lineMaterial) return lineMaterial;
            if (s_lineMaterial) return s_lineMaterial;
            var shader = Shader.Find("Sprites/Default");
            if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) return null;
            s_lineMaterial = new Material(shader) { name = "ThresherLine (runtime)", renderQueue = 3000 };
            return s_lineMaterial;
        }

        Material BallMaterialOrFallback()
        {
            if (ballMaterial) return ballMaterial;
            if (s_ballMaterial) return s_ballMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) return null;
            s_ballMaterial = new Material(shader) { name = "ThresherBall (runtime)" };
            return s_ballMaterial;
        }

        void SetVisualsActive(bool active)
        {
            if (!active) _shownValid = false;   // re-shown at the live colours, not eased from stale ones
            if (_root) _root.SetActive(active);
            if (!active && _sight) _sight.Hide();
        }

        void DestroyVisuals()
        {
            if (_root) Destroy(_root);
            _root = null;
            if (_sight) Destroy(_sight.gameObject);
            _sight = null;
        }

        void UpdateVisuals()
        {
            if (!_seeded || config == null) return;
            EnsureVisuals();
            if (!_root) return;
            if (!_root.activeSelf) _root.SetActive(true);

            float dt = Time.deltaTime;
            Vector3 ship = _status.Transform.position;
            Vector3 ballPos = _solver.BallPosition;
            float speed = _solver.BallSpeed;
            float r = _settings.BallRadius * config.BallVisualScale;

            // Ball: position, roll, colour.
            _ball.position = ballPos;
            _ball.localScale = Vector3.one * (2f * r);
            Vector3 v = _solver.BallVelocity;
            if (v.sqrMagnitude > 1e-4f && r > 1e-4f)
            {
                Vector3 axis = Vector3.Cross(ship - ballPos, v);
                if (axis.sqrMagnitude < 1e-6f) axis = Vector3.Cross(Vector3.up, v);
                // Capped: a white-hot ball truly rolls ~7000 deg/s, which at 60 fps strobes the studs
                // backwards (wagon-wheel) — read as jitter. The cap keeps the roll legible.
                float rollDegPerSec = Mathf.Min(speed / r * Mathf.Rad2Deg, config.BallMaxVisualSpin);
                if (axis.sqrMagnitude > 1e-6f)
                    _ball.rotation = Quaternion.AngleAxis(rollDegPerSec * dt, axis.normalized) * _ball.rotation;
            }

            Color targetHue = BallColor(out float emission);
            BallShade(out Color targetBody, out Color targetRim);
            ChainShade(out Color chainBody, out Color targetChainRim);
            if (!_shownValid)
            {
                (_shownHue, _shownBody, _shownRim, _shownChainRim) = (targetHue, targetBody, targetRim, targetChainRim);
                _shownValid = true;
            }
            else
            {
                // Unscaled: the smash's hit-stop slows time exactly when the ball turns red, and the
                // turn to red should land inside it.
                float k = 1f - Mathf.Exp(-config.ColorBlendRate * Time.unscaledDeltaTime);
                _shownHue = Color.Lerp(_shownHue, targetHue, k);
                _shownBody = Color.Lerp(_shownBody, targetBody, k);
                _shownRim = Color.Lerp(_shownRim, targetRim, k);
                _shownChainRim = Color.Lerp(_shownChainRim, targetChainRim, k);
            }
            Color ballColor = _shownHue;
            Color body = _shownBody, rim = _shownRim, chainRim = _shownChainRim;
            _mpb.Clear();
            _mpb.SetColor(DarkColorId, body);
            _mpb.SetColor(BrightColorId, rim);
            // The runtime fallback material (no ball material assigned) is URP/Unlit.
            _mpb.SetColor(BaseColorId, ballColor);
            _mpb.SetColor(ColorId, ballColor);
            _mpb.SetColor(EmissionColorId, ballColor * emission);
            for (int i = 0; i < _ballRenderers.Length; i++)
                if (_ballRenderers[i]) _ballRenderers[i].SetPropertyBlock(_mpb);

            // Chain: the gameplay links, drawn as a fresnel tube at the width that CUTS. Iron body;
            // the rim takes the ball's hue, brighter while taut (it is slicing), lime at READY.
            _chain.startWidth = _chain.endWidth = 2f * config.ChainCutRadius;
            _chainMpb.Clear();
            _chainMpb.SetColor(DarkColorId, chainBody);
            _chainMpb.SetColor(BrightColorId, chainRim);
            _chain.SetPropertyBlock(_chainMpb);
            _chain.startColor = _chain.endColor = chainRim;   // the vertex-colour fallback material
            // Drawn as a smooth curve through the gameplay links (ThresherChainLinks.Smooth), so a
            // slack chain bends instead of showing a kink at every link.
            int drawn = ThresherChainLinks.SmoothedCount(_links.Count, config.ChainRenderSubdivisions);
            if (_chainDrawn == null || _chainDrawn.Length != drawn) _chainDrawn = new Vector3[drawn];
            int written = ThresherChainLinks.Smooth(_links.Points, config.ChainRenderSubdivisions, _chainDrawn);
            if (_chain.positionCount != written) _chain.positionCount = written;
            _chain.SetPositions(_chainDrawn);


            // Skid trail while planting.
            if (_skid)
            {
                _skid.emitting = _solver.Mode == ThresherMode.Skidding;
                _skid.startWidth = r * 1.4f;
                _skid.endWidth = 0f;
                _skid.startColor = ballColor;
                _skid.endColor = new Color(ballColor.r, ballColor.g, ballColor.b, 0f);
            }

            DrawCombo(ballPos, r, dt, ballColor);
        }

        void DrawCombo(Vector3 ballPos, float r, float dt, Color ballColor)
        {
            if (!_comboLabel) return;
            if (_combo >= 2)
            {
                if (_combo != _comboShown)
                {
                    _comboShown = _combo;
                    _comboLabel.text = "x" + _combo;
                    _comboShownScale = 1.6f;   // pop on each new hit
                }
                _comboShownScale = Mathf.MoveTowards(_comboShownScale, 1f, dt * 4f);
                _comboLabel.fontSize = r * 2.2f * (1f + 0.08f * Mathf.Min(_combo, 12)) * _comboShownScale;
                _comboLabel.color = ballColor;
                _comboLabel.transform.position = ballPos + Vector3.up * (r * 3f);
                var cam = Camera.main;
                if (cam)
                {
                    Vector3 away = _comboLabel.transform.position - cam.transform.position;
                    if (away.sqrMagnitude > 1e-6f)
                        _comboLabel.transform.rotation = Quaternion.LookRotation(away, cam.transform.up);
                }
                _comboLabel.enabled = true;
            }
            else
            {
                _comboShown = 0;
                _comboLabel.enabled = false;
            }
        }
    }
}
