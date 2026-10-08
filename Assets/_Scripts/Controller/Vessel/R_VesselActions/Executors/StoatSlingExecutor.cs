using CosmicShore.Core;
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
    /// squeeze is sampled every frame while held and the last sample is what slings
    /// (the <c>GibbonTetherExecutor</c> lesson).</para>
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
            public float Hold01;    // the last sample; what slings
        }

        readonly Hold[] _holds = new Hold[2];
        IVesselStatus _status;
        BlackHoleRegistry.Pair _pair;
        ToggleTranslationModeActionExecutor _stance;

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
            _holds[i].Hold01 = 0f;
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
        }

        void Track(Side side, float rawAnalog, bool analog, bool autopilot)
        {
            int i = (int)side;
            if (!_holds[i].Holding) return;
            _holds[i].HeldFor += Time.deltaTime;
            _holds[i].Hold01 = StoatSlingMath.Hold01(rawAnalog, _holds[i].HeldFor, analog, config.HoldRampSeconds,
                autopilot, config.AutopilotHold01);
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
        }
    }
}
