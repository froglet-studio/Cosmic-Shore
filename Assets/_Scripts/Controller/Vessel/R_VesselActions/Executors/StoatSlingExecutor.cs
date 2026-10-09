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
    /// The Stoat's SLINGSHOT (<c>R_VesselActions/STOAT.md</c>): an attractor–repulsor WORMHOLE pair
    /// laid beside the hull — the attractor on the trigger's side, the repulsor mirrored on the other.
    ///
    /// <para><b>The orbit</b> (the drift pair, the playtest's design of 2026-10-09). PRESS lays the
    /// pair: the attractor perpendicular to the nose at the radius the squeeze asks for, the repulsor
    /// the same distance on the other side, both the same size. HOLD and the hull ORBITS the attractor:
    /// the squeeze sets the radius live (harder = tighter), the hole's horizon is a fixed fraction of
    /// it, and its strength is the one that makes that radius a circular orbit at the hull's speed
    /// (<see cref="StoatSlingMath.CircularOrbitGM"/>) — the turn is what the hole's gravity would do,
    /// held to a circle so a pilot can aim it. RELEASE and the hull slingshots out along the tangent
    /// with a boost; the pair falls together and annihilates (<see cref="BlackHoleRegistry.LetGo"/>).
    /// Keep squeezing and you keep circling, up to the config's lifetime.</para>
    ///
    /// <para><b>How the orbit flies.</b> On the simulating machine only, each frame: the heading is
    /// turned along the orbit (<c>VesselTransformer.ApplyRotation</c>, with the momentum re-aimed for the
    /// vector flight model), the hull is eased onto the radius (<c>TranslateShip</c>), and both holes are
    /// retuned to the radius (<c>SetSize</c> / <c>SetStrength</c>). The pilot's own pull from this pair is
    /// the orbit itself, so <see cref="BlackHoleVesselPull"/> skips an owned drift pair for every vessel
    /// (its owner flies the orbit; an opponent may not be moved by it — Docs/ELEMENTAL_ECONOMY.md §9).
    /// Peers lay their own copy of the pair from the replicated press; the squeeze is not replicated,
    /// so a remote copy keeps its press-time size (the pair is a local placeholder, Docs/BLACK_HOLE.md
    /// §11 "Not yet").</para>
    ///
    /// <para><b>The crystal style</b> (<c>BlackHoleConfig.crystalPairs</c>, §13) is charming-cerf's
    /// wormhole and keeps its first design: laid on RELEASE, sized by the deepest squeeze
    /// (<see cref="StoatSlingMath.Peak"/> — a let-go trigger sweeps back down before the release edge).</para>
    ///
    /// <para><b>Naming.</b> Player-facing these are WORMHOLES (attractor / repulsor). In code they are
    /// the black-hole system's types (<c>BlackHole</c> with <c>HolePolarity.Sink</c> / <c>Source</c>,
    /// the names <c>cece/charming-cerf-alf1j1</c> chose).</para>
    ///
    /// <para><b>One pair per Stoat.</b> A new press ends this hull's last pair (and slings it out of an
    /// orbit still running, so chaining triggers chains turns); other hulls' and the tool's pairs are
    /// left alone.</para>
    /// </summary>
    public sealed class StoatSlingExecutor : ShipActionExecutorBase
    {
        public enum Side { Left = 0, Right = 1 }

        [Header("Config")]
        [Tooltip("Every number of the sling: squeeze → orbit, slingshot, life, the crystal style, audio slots.")]
        [SerializeField] StoatSlingConfigSO config;

        struct Hold
        {
            public bool Holding;
            public float HeldFor;   // seconds, for devices with no analog trigger
            public float Live;      // this frame's squeeze — the orbit radius follows it
            public float Peak;      // the deepest squeeze of the hold — the boost and the crystal size
        }

        readonly Hold[] _holds = new Hold[2];
        IVesselStatus _status;
        BlackHoleRegistry.Pair _pair;
        CrystalWormhole _crystal;
        ToggleTranslationModeActionExecutor _stance;
        readonly InputEvents?[] _boundInput = new InputEvents?[2];
        readonly List<ShipActionSO> _bindScratch = new();
        float _aiLastSlingTime = float.NegativeInfinity;
        float _aiReleaseAt = float.PositiveInfinity;

        // The orbit in flight (drift pair).
        bool _orbiting;
        Side _orbitSide;
        float _orbitRadius;
        float _orbitAngle;

        /// <summary>The drift pair this hull laid last, alive or not; null before the first one.</summary>
        public BlackHoleRegistry.Pair LastPair => _pair;
        /// <summary>The crystal pair this hull slung last (the crystal style, Docs/BLACK_HOLE.md §13); null before the first.</summary>
        public CrystalWormhole LastCrystal => _crystal;
        public bool IsHolding(Side side) => _holds[(int)side].Holding;
        /// <summary>The deepest squeeze of the current (or last) hold, 0..1.</summary>
        public float Hold01(Side side) => _holds[(int)side].Peak;
        /// <summary>True while the hull is circling its attractor.</summary>
        public bool IsOrbiting => _orbiting;
        /// <summary>The radius the orbit is flying at, world units (0 when not orbiting).</summary>
        public float OrbitRadius => _orbiting ? _orbitRadius : 0f;
        /// <summary>Radians turned around the attractor in the current orbit.</summary>
        public float OrbitAngle => _orbitAngle;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _holds[0] = default;
            _holds[1] = default;
            _orbiting = false;
            _stance = TryGetComponent<ActionExecutorRegistry>(out var registry) ? registry.Get<ToggleTranslationModeActionExecutor>() : null;
            if (config == null)
                CSDebug.LogError($"[Stoat] {name}: StoatSlingExecutor has no StoatSlingConfig — the triggers will sling nothing. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset on the prefab.");
        }

        /// <summary>Trigger PRESS: the drift pair is laid now and the orbit begins; a crystal pair waits for the release.</summary>
        public void BeginHold(Side side)
        {
            int i = (int)side;
            if (_holds[i].Holding) return;
            _holds[i].Holding = true;
            _holds[i].HeldFor = 0f;
            // An autopilot writes no trigger, so its fixed squeeze is the hold from the start.
            float start = config != null && _status != null && _status.AutoPilotEnabled ? config.AutopilotHold01 : 0f;
            _holds[i].Live = start;
            _holds[i].Peak = start;
            if (config == null) return;
            PlayOneShot(config.HoldStartEvent);
            if (!BlackHoleRegistry.Config.CrystalPairs) LayOrbit(side);
        }

        /// <summary>Trigger RELEASE: slingshot out of the orbit (drift pair) or lay the crystal pair.</summary>
        public void Release(Side side)
        {
            int i = (int)side;
            if (!_holds[i].Holding) return;
            _holds[i].Holding = false;
            if (config == null || _status == null) return;
            if (_orbiting)
            {
                if (_orbitSide == side) Slingshot(boost: true);
                return;
            }
            if (BlackHoleRegistry.Config.CrystalPairs) SlingCrystal(side, _holds[i].Peak);
        }

        // ------------------------------------------------------------------ the orbit

        float OrbitSpeed()
        {
            var transformer = _status.VesselTransformer;
            float cruise = transformer ? transformer.CruiseSpeed : 0f;
            return Mathf.Max(_status.Speed, cruise * config.MinOrbitCruise, 1f);
        }

        float RadiusFor(float hold01)
        {
            // Space is reach: read live, never cached (vessel contract rule 2).
            float reach = config.OrbitReach.EvaluateLive(_status);
            return StoatSlingMath.OrbitRadius(hold01, config.OrbitRadiusWide * reach, config.OrbitRadiusTight * reach, config.HoldExponent);
        }

        void LayOrbit(Side side)
        {
            var hull = _status.Transform;
            if (hull == null) return;
            // One pair per Stoat: a press while circling slings out of the old orbit first.
            if (_orbiting) Slingshot(boost: true);
            EndLastPair();
            var bh = BlackHoleRegistry.Config;
            if (!BlackHoleRegistry.CanSpawn(2))
            {
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[Stoat] orbit refused — needs two free of {bh.MaxBlackHoles} wormholes ({BlackHoleRegistry.Count} live).");
                return;
            }

            float speed = OrbitSpeed();
            float radius = RadiusFor(_holds[(int)side].Live);
            float horizon = StoatSlingMath.OrbitHorizon(radius, config.OrbitHorizons);
            float strength = StoatSlingMath.CircularOrbitGM(speed, radius, horizon) / Mathf.Max(1e-3f, bh.GmPerStrength);
            // The attractor goes perpendicular to the nose on the pressed side — where the centre of a turn
            // that way lies — and the repulsor mirrors it. The pair's axis runs attractor → repulsor.
            var toAttractor = StoatSlingMath.PairAxis(side == Side.Left, hull.right) * -1f;
            toAttractor = Vector3.ProjectOnPlane(toAttractor, hull.forward).normalized;
            _pair = BlackHoleRegistry.SpawnPair(hull.position, -toAttractor, strength, horizon, radius, config.DriftSpeed,
                config.Lifetime, hull.up, OwnerTransform(), held: true);
            if (_pair == null) return;

            _orbiting = true;
            _orbitSide = side;
            _orbitRadius = radius;
            _orbitAngle = 0f;
            // A sling is a launch: leave the hold-still stance, which would otherwise pin the hull.
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[Stoat] {side} orbit: radius {radius:F0} u at {speed:F0} u/s, horizon {horizon:F1}, strength {strength:F1}");
        }

        void StepOrbit(float dt)
        {
            var hull = _status.Transform;
            var transformer = _status.VesselTransformer;
            if (!hull || !transformer || _pair.Black == null) return;

            float speed = OrbitSpeed();
            _orbitRadius = Mathf.Lerp(_orbitRadius, RadiusFor(_holds[(int)_orbitSide].Live), 1f - Mathf.Exp(-config.RadiusFollowRate * dt));

            // Onto the radius...
            var toCentre = _pair.Black.transform.position - hull.position;
            float distance = toCentre.magnitude;
            if (distance > 1e-3f)
                transformer.TranslateShip(toCentre / distance *
                                          StoatSlingMath.RadialCorrection(distance, _orbitRadius, config.RadialCorrectionRate, dt));

            // ...and around it: the heading turns at ω = v / r, the momentum with it.
            float angle = speed / Mathf.Max(1f, _orbitRadius) * dt;
            var desired = StoatSlingMath.OrbitHeading(hull.forward, toCentre, angle);
            Quaternion.FromToRotation(hull.forward, desired).ToAngleAxis(out float degrees, out var axis);
            if (degrees > 1e-4f && axis.sqrMagnitude > 1e-8f) transformer.ApplyRotation(degrees, axis);
            transformer.SetCourseVelocity(desired);
            _orbitAngle += angle;

            // The holes follow the radius: the size the orbit sits at, the strength that makes it circular.
            float horizon = StoatSlingMath.OrbitHorizon(_orbitRadius, config.OrbitHorizons);
            float strength = StoatSlingMath.CircularOrbitGM(speed, _orbitRadius, horizon) /
                             Mathf.Max(1e-3f, BlackHoleRegistry.Config.GmPerStrength);
            Retune(_pair.Black, strength, horizon);
            Retune(_pair.White, strength, horizon);
        }

        static void Retune(BlackHole hole, float strength, float horizon)
        {
            if (hole == null || hole.IsDespawning) return;
            hole.SetStrength(strength);
            hole.SetSize(horizon);
        }

        /// <summary>
        /// Out of the orbit: the pair is let go (it closes and annihilates) and, on a real release, the hull
        /// is thrown along its tangent — harder the deeper the squeeze went.
        /// </summary>
        void Slingshot(bool boost)
        {
            if (!_orbiting) return;
            _orbiting = false;
            BlackHoleRegistry.LetGo(_pair);
            var transformer = _status.VesselTransformer;
            var hull = _status.Transform;
            if (boost && transformer && hull && MantaStingActionExecutor.IsSimAuthority(_status))
            {
                float kick = StoatSlingMath.SlingBoost(OrbitSpeed(), _holds[(int)_orbitSide].Peak, config.SlingBoostMin, config.SlingBoostMax);
                // The modifier starts at 1.5× its amount and fades over the duration (VesselTransformer).
                if (kick > 0f) transformer.ModifyVelocity(hull.forward * (kick / 1.5f), config.SlingBoostSeconds);
            }
            if (boost) PlayOneShot(config.SlingEvent);
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[Stoat] {_orbitSide} slingshot after {_orbitAngle * Mathf.Rad2Deg:F0}° at radius {_orbitRadius:F0} u");
        }

        void EndLastPair()
        {
            if (_pair != null && _pair.IsAlive) BlackHoleRegistry.Annihilate(_pair);
            if (_crystal && !_crystal.IsGone && !_crystal.IsAnnihilating) _crystal.Annihilate(BlackHoleRegistry.Config.CrystalReplaceSeconds);
        }

        // ------------------------------------------------------------------ the crystal style

        void SlingCrystal(Side side, float hold01)
        {
            var hull = _status.Transform;
            if (hull == null) return;
            var bh = BlackHoleRegistry.Config;
            float strength = StoatSlingMath.Strength(hold01, config.MinStrength, config.MaxStrength, config.HoldExponent);
            float rs = bh.HorizonRadius(strength);
            float halfGap = rs * config.HalfGapHorizons.EvaluateLive(_status);
            var midpoint = StoatSlingMath.Midpoint(hull.position, hull.forward, rs, config.AheadHorizons);
            var axis = StoatSlingMath.PairAxis(side == Side.Left, hull.right);

            EndLastPair();
            if (!BlackHoleRegistry.CanSpawn(2))
            {
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[Stoat] sling refused — needs two free of {bh.MaxBlackHoles} wormholes ({BlackHoleRegistry.Count} live).");
                return;
            }
            // charming-cerf's wormhole: its mouths carry this pilot alone, its wells pull this hull alone.
            var riders = new List<IPlayer>(1);
            if (_status.Player != null) riders.Add(_status.Player);
            _crystal = BlackHoleRegistry.SpawnCrystalPair(midpoint, axis, halfGap,
                BlackHoleRegistry.CrystalSettings(strength, 0f, hull.up, riders), OwnerTransform());
            if (_crystal == null) return;
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();
            PlayOneShot(config.SlingEvent);
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (config == null || _status == null) return;
            var input = _status.InputStatus;
            bool analog = input != null && input.ActiveInputDevice == InputDeviceType.Gamepad;
            bool autopilot = _status.AutoPilotEnabled;
            Track(Side.Left, input != null ? input.LeftTriggerAnalog : 0f, analog, autopilot);
            Track(Side.Right, input != null ? input.RightTriggerAnalog : 0f, analog, autopilot);

            if (_orbiting)
            {
                // The pair ended under us (annihilated, or held past its lifetime and let go on its own).
                if (_pair == null || !_pair.IsAlive) _orbiting = false;
                else if (!_pair.Held) Slingshot(boost: true);
                else if (MantaStingActionExecutor.IsSimAuthority(_status)) StepOrbit(Time.deltaTime);
            }

            if (autopilot && MantaStingActionExecutor.IsSimAuthority(_status)) AutopilotSling();
        }

        /// <summary>
        /// The sling an autopilot cannot press (the arcade rule: never assume an AI can use a human's
        /// input). When the AI's target sits at least <c>aiSlingMinTurnDegrees</c> off the nose and far
        /// enough away, it presses the trigger on the side it wants to turn, holds for as long as the
        /// orbit takes to swing the nose round to the target, and lets go — both edges through the
        /// REPLICATED path, so every peer runs the same sling a human's would. Simulating machine only.
        /// </summary>
        void AutopilotSling()
        {
            var handler = _status.ActionHandler;
            if (!handler) return;
            if (_orbiting)
            {
                if (Time.time < _aiReleaseAt) return;
                _aiReleaseAt = float.PositiveInfinity;
                if (ResolveBoundInput(_orbitSide, handler, out var held)) handler.StopShipControllerActionsReplicated(held);
                return;
            }
            if (config.AiSlingMinTurnDegrees <= 0f) return;
            if (Time.time - _aiLastSlingTime < config.AiSlingIntervalSeconds) return;
            var ai = _status.AIPilot;
            var hull = _status.Transform;
            if (!ai || !hull) return;
            var local = hull.InverseTransformPoint(ai.TargetPosition);
            if (!StoatSlingMath.TryAutopilotSide(local, config.AiSlingMinTurnDegrees, config.AiSlingMinDistance, out bool attractorLeft))
                return;
            var side = attractorLeft ? Side.Left : Side.Right;
            if (!ResolveBoundInput(side, handler, out var ie)) return;
            _aiLastSlingTime = Time.time;
            float turn = Mathf.Abs(Mathf.Atan2(local.x, local.z));
            if (BlackHoleRegistry.Config.CrystalPairs)
            {
                handler.PerformShipControllerActionsReplicated(ie);   // the squeeze
                handler.StopShipControllerActionsReplicated(ie);      // the sling
                return;
            }
            _aiReleaseAt = Time.time + StoatSlingMath.AutopilotHoldSeconds(turn, OrbitSpeed(), RadiusFor(config.AutopilotHold01));
            handler.PerformShipControllerActionsReplicated(ie);       // the squeeze: the orbit begins
        }

        /// <summary>Which input event this side's sling is bound to on THIS vessel — read off the
        /// binding maps and retried until it succeeds (they fill after executors initialize,
        /// vessel contract rule 6).</summary>
        bool ResolveBoundInput(Side side, R_VesselActionHandler handler, out InputEvents ie)
        {
            int i = (int)side;
            if (_boundInput[i].HasValue) { ie = _boundInput[i].Value; return true; }
            foreach (InputEvents candidate in System.Enum.GetValues(typeof(InputEvents)))
            {
                _bindScratch.Clear();
                handler.CollectBoundActions(candidate, _bindScratch);
                foreach (var action in _bindScratch)
                    if (action is StoatSlingActionSO sling && sling.Side == side)
                    {
                        _boundInput[i] = candidate;
                        _bindScratch.Clear();
                        ie = candidate;
                        return true;
                    }
            }
            _bindScratch.Clear();
            ie = default;
            return false;
        }

        void Track(Side side, float rawAnalog, bool analog, bool autopilot)
        {
            int i = (int)side;
            if (!_holds[i].Holding) return;
            _holds[i].HeldFor += Time.deltaTime;
            float sample = StoatSlingMath.Hold01(rawAnalog, _holds[i].HeldFor, analog, config.HoldRampSeconds, autopilot, config.AutopilotHold01);
            _holds[i].Live = sample;
            _holds[i].Peak = StoatSlingMath.Peak(_holds[i].Peak, sample);
        }

        /// <summary>The transform <see cref="BlackHoleVesselPull"/> knows this vessel by (its
        /// <c>VesselStatus</c>'s).</summary>
        Transform OwnerTransform() => _status is Component c ? c.transform : _status.Transform;

        void PlayOneShot(EventReference reference)
        {
            if (reference.IsNull) return;
            var audio = AudioSystem.Instance;
            if (audio) audio.PlaySFXEvent(reference, transform.position);
        }

        // A vessel swap or despawn drops the squeeze and lets an orbiting pair go (it closes and annihilates).
        void OnDisable()
        {
            if (_orbiting)
            {
                _orbiting = false;
                BlackHoleRegistry.LetGo(_pair);
            }
            _holds[0] = default;
            _holds[1] = default;
            _boundInput[0] = null;
            _boundInput[1] = null;
            _aiReleaseAt = float.PositiveInfinity;
        }
    }
}
