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
    /// The Stoat's SLINGSHOT (<c>R_VesselActions/STOAT.md</c>): squeeze a trigger, let go, and an
    /// attractor–repulsor WORMHOLE pair is laid across the hull on its own horizontal — the attractor
    /// on the side you pressed, the repulsor on the other. The hull falls toward the attractor and is
    /// shoved off the repulsor, and that asymmetric push is the slingshot; the squeeze's depth is the
    /// pair's size. It can be slung from a standstill: the pair is laid in the hull's frame, not
    /// thrown from its velocity, and a sling ends the hold-still stance so the pull can launch it.
    ///
    /// <para><b>Naming.</b> Player-facing these are WORMHOLES (attractor / repulsor). In code they
    /// are still the black-hole system's types (<c>BlackHole</c> with <c>HolePolarity.Black</c> /
    /// <c>White</c>) — the real wormhole mechanics are being built on <c>cece/charming-cerf-alf1j1</c>
    /// on those same types, so a rename here would only collide with it.</para>
    ///
    /// <para><b>Why the hold is tracked per frame, not read at release.</b> The release edge is
    /// raised when the trigger crosses back below the deadzone, so it reads ~0 by the time
    /// <see cref="Release"/> runs — sampling it there would sling every pair at minimum size. The
    /// squeeze is sampled every frame while held and the DEEPEST sample is what slings
    /// (<see cref="StoatSlingMath.Peak"/>): the latest one is no better than reading at release,
    /// because a let-go trigger sweeps back down through several frames before the edge fires.</para>
    ///
    /// <para><b>One pair per Stoat.</b> A new sling annihilates THIS hull's previous pair first, so
    /// a pilot can chain nudges without filling the registry's hole budget; other hulls' and the
    /// tool's pairs are left alone. The pair is the wormhole system's — its drift, annihilation and
    /// pass-through are <c>BlackHoleRegistry</c>'s — and so is its networking (none yet, Docs/BLACK_HOLE.md
    /// §11 "Not yet"): the holes exist on the machine that pressed the trigger. The press and
    /// release edges do round-trip through <c>R_VesselActionHandler</c>, so a peer runs this same
    /// code for a remote Stoat and lays its own copy of the pair at the replicated pose.</para>
    /// </summary>
    public sealed class StoatSlingExecutor : ShipActionExecutorBase
    {
        public enum Side { Left = 0, Right = 1 }

        [Header("Config")]
        [Tooltip("Every number of the sling: hold → size, geometry, life, audio slots.")]
        [SerializeField] StoatSlingConfigSO config;

        struct Hold
        {
            public bool Holding;
            public float HeldFor;   // seconds, for devices with no analog trigger
            public float Hold01;    // the deepest sample of this hold; what slings
        }

        readonly Hold[] _holds = new Hold[2];
        IVesselStatus _status;
        BlackHoleRegistry.Pair _pair;
        ToggleTranslationModeActionExecutor _stance;
        readonly InputEvents?[] _boundInput = new InputEvents?[2];
        readonly List<ShipActionSO> _bindScratch = new();
        float _aiLastSlingTime = float.NegativeInfinity;

        /// <summary>The pair this hull slung last, alive or not; null before the first sling.</summary>
        public BlackHoleRegistry.Pair LastPair => _pair;
        public bool IsHolding(Side side) => _holds[(int)side].Holding;
        public float Hold01(Side side) => _holds[(int)side].Hold01;

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status = shipStatus;
            _holds[0] = default;
            _holds[1] = default;
            _stance = TryGetComponent<ActionExecutorRegistry>(out var registry) ? registry.Get<ToggleTranslationModeActionExecutor>() : null;
            if (config == null)
                CSDebug.LogError($"[Stoat] {name}: StoatSlingExecutor has no StoatSlingConfig — the triggers will sling nothing. " +
                                 "Wire Assets/_SO_Assets/VesselActions/Stoat/StoatSlingConfig.asset on the prefab.");
        }

        /// <summary>Trigger PRESS: the squeeze begins at a touch.</summary>
        public void BeginHold(Side side)
        {
            int i = (int)side;
            if (_holds[i].Holding) return;
            _holds[i].Holding = true;
            _holds[i].HeldFor = 0f;
            // An autopilot presses and releases in one step (no frame to sample a squeeze in), so
            // its fixed squeeze is the hold from the start.
            _holds[i].Hold01 = config != null && _status != null && _status.AutoPilotEnabled ? config.AutopilotHold01 : 0f;
            if (config != null) PlayOneShot(config.HoldStartEvent);
        }

        /// <summary>Trigger RELEASE: sling the pair at the size the squeeze reached.</summary>
        public void Release(Side side)
        {
            int i = (int)side;
            if (!_holds[i].Holding) return;
            _holds[i].Holding = false;
            if (config == null || _status == null) return;

            var hull = _status.Transform;
            if (hull == null) return;

            var bh = BlackHoleRegistry.Config;
            float strength = StoatSlingMath.Strength(_holds[i].Hold01, config.MinStrength, config.MaxStrength, config.HoldExponent);
            float rs = bh.HorizonRadius(strength);
            // Space is reach: read live, never cached (vessel contract rule 2).
            float halfGap = rs * config.HalfGapHorizons.EvaluateLive(_status);
            var midpoint = StoatSlingMath.Midpoint(hull.position, hull.forward, rs, config.AheadHorizons);
            var axis = StoatSlingMath.PairAxis(side == Side.Left, hull.right);

            if (_pair != null && _pair.IsAlive) BlackHoleRegistry.Annihilate(_pair);
            // A full budget is a normal answer here (another Stoat, the tool), so ask quietly first.
            if (!BlackHoleRegistry.CanSpawn(2))
            {
                CSDebug.LogVerbose(CSLogChannel.BlackHole,
                    $"[Stoat] sling refused — needs two free of {bh.MaxBlackHoles} wormholes ({BlackHoleRegistry.Count} live).");
                return;
            }
            // Spin about the hull's up so the frame drag turns on the plane the pair lies in. The pair
            // is OWNED by this hull, so its pull moves this Stoat and no other vessel (§9 of the
            // elemental economy: a vessel may not move an opposing vessel).
            _pair = BlackHoleRegistry.SpawnPair(midpoint, axis, strength, 0f, halfGap, config.DriftSpeed, config.Lifetime, hull.up,
                OwnerTransform());
            if (_pair == null) return;
            // A sling is a launch: leave the hold-still stance, which would otherwise ignore the pull.
            if (_status.IsTranslationRestricted && _stance) _stance.EndStance();
            PlayOneShot(config.SlingEvent);
            CSDebug.LogVerbose(CSLogChannel.BlackHole,
                $"[Stoat] {side} sling: hold {_holds[i].Hold01:F2} → strength {strength:F1} (r_s {rs:F1}), " +
                $"attractor on the {(side == Side.Left ? "left" : "right")}, half-gap {halfGap:F1}");
        }

        void Update()
        {
            if (config == null || _status == null) return;
            var input = _status.InputStatus;
            bool analog = input != null && input.ActiveInputDevice == InputDeviceType.Gamepad;
            bool autopilot = _status.AutoPilotEnabled;
            Track(Side.Left, input != null ? input.LeftTriggerAnalog : 0f, analog, autopilot);
            Track(Side.Right, input != null ? input.RightTriggerAnalog : 0f, analog, autopilot);

            if (autopilot && MantaStingActionExecutor.IsSimAuthority(_status)) AutopilotSling();
        }

        /// <summary>
        /// The sling an autopilot cannot press (the arcade rule: never assume an AI can use a
        /// human's input). When the AI's target sits at least <c>aiSlingMinTurnDegrees</c> off the
        /// nose on the hull's horizontal, and is far enough for a sling to matter, it lays the
        /// pair with the ATTRACTOR on the side it wants to turn — a press and a release through
        /// the REPLICATED path, so every peer runs the same sling a human's squeeze would have
        /// (the Grizzly's autopilot bomb is the model). Simulating machine only.
        /// </summary>
        void AutopilotSling()
        {
            if (config.AiSlingMinTurnDegrees <= 0f) return;
            if (Time.time - _aiLastSlingTime < config.AiSlingIntervalSeconds) return;
            var ai = _status.AIPilot;
            var hull = _status.Transform;
            var handler = _status.ActionHandler;
            if (!ai || !hull || !handler) return;
            if (!StoatSlingMath.TryAutopilotSide(hull.InverseTransformPoint(ai.TargetPosition), config.AiSlingMinTurnDegrees,
                    config.AiSlingMinDistance, out bool blackOnLeft)) return;
            var side = blackOnLeft ? Side.Left : Side.Right;
            if (!ResolveBoundInput(side, handler, out var ie)) return;
            _aiLastSlingTime = Time.time;
            handler.PerformShipControllerActionsReplicated(ie);   // the squeeze
            handler.StopShipControllerActionsReplicated(ie);      // the sling
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
            _holds[i].Hold01 = StoatSlingMath.Peak(_holds[i].Hold01, StoatSlingMath.Hold01(rawAnalog, _holds[i].HeldFor, analog,
                config.HoldRampSeconds, autopilot, config.AutopilotHold01));
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

        // A vessel swap or despawn drops the squeeze; the pair, if any, lives out its own lifetime.
        void OnDisable()
        {
            _holds[0] = default;
            _holds[1] = default;
            _boundInput[0] = null;
            _boundInput[1] = null;
        }
    }
}
