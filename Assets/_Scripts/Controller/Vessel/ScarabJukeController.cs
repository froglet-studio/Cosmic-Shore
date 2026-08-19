using System;
using System.Collections;
using CosmicShore.Utility;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's juke (design: R_VesselActions/SCARAB.md §3.4) — the Sparrow strafing roll's
    /// sibling, with three deliberate differences:
    ///
    /// 1. RIGHT stick, not left. The Scarab steers single-stick, so the right stick is otherwise
    ///    unused; pushing it to the perimeter fires a lateral shove in the stick's direction.
    ///    Uses the normalized (radially clamped) stick, never the eased one — per-axis easing
    ///    makes diagonal magnitudes direction-dependent (the Sparrow learned this).
    /// 2. COOLDOWN-armed, not boost-armed. The Scarab has no boost button; the juke re-arms on a
    ///    plain input-pacing cooldown (nothing in the world is removed by it), displayed as a
    ///    binary pip via <see cref="OnJukeChargeChanged"/> — the Sparrow rollChargeIndicator
    ///    pattern exactly.
    /// 3. It is an attack surface: displacement rides the owner-authoritative NetworkTransform
    ///    and the 360° spin stays local (the Sparrow's replication posture), but the FIRE moment
    ///    itself now makes one owner→server round-trip (<see cref="NotifyJukeFired_ServerRpc"/>)
    ///    so the server's replica opens <see cref="IsJukeStrikeWindowOpen"/> — the window Scarab
    ///    Scramble's juke-steal reads on the server. Without it a remote client's steal never
    ///    registered (the ball's strike path is server-side). The fuller Juke_ServerRpc carrying
    ///    direction + Charge snapshot remains the Phase 2.5 upgrade path.
    ///
    /// Kinematics are the Sparrow's exactly: ModifyVelocity displacement orthogonal to travel,
    /// visual-child 360° smoothstep spin (the camera reads the root), a small REAL root bank via
    /// ApplyRotation, and BlockRotationOverride so bridging trail prisms lay travel-aligned.
    /// Owner-driven; autopilot vessels produce no stick input, so the juke is inert for AI
    /// (trigger synthesis is the standing Phase 2.5 backlog item).
    /// </summary>
    public class ScarabJukeController : NetworkBehaviour
    {
        [Header("Trigger")]
        [Tooltip("Stick radial magnitude treated as 'at the perimeter'. 1 = full deflection " +
                 "only. Uses the normalized (radially clamped) right-stick vector.")]
        [SerializeField, Range(0.5f, 1f)] float perimeterThreshold = 1f;
        [Tooltip("Flip the CW/CCW visual-roll mapping if it reads backwards in playtest.")]
        [SerializeField] bool invertRollDirection;
        [Tooltip("Seconds between jukes. ZERO by design: the dash itself is free and always " +
                 "available — only the cavitation blast that rides it is cooldown-gated (that " +
                 "cooldown lives on the blast, scaled by CHARGE). Left as a knob rather than " +
                 "deleted so a mode could pace the dash if it ever needed to.")]
        [SerializeField, Min(0f)] float jukeCooldownSeconds = 0f;

        [Header("Juke")]
        [SerializeField, Min(0.1f)] float jukeDurationSeconds = 0.5f;
        [Tooltip("Very small REAL roll applied to the vessel root over the juke, same " +
                 "handedness as the visual spin. 0 = visual-only.")]
        [SerializeField, Range(0f, 30f)] float rootRollDegrees = 15f;
        [Tooltip("Peak sideways displacement speed injected through ModifyVelocity (world " +
                 "units/second; the transformer clamps its channel at 100).")]
        [SerializeField, Min(0f)] float jukeSpeed = 80f;
        [Tooltip("The transform that visually rolls. Defaults to the model's Animator " +
                 "transform, then the vessel root's first child.")]
        [SerializeField] Transform rollVisualTarget;

        const float ThresholdEpsilon = 0.005f;

        IVesselStatus _status;
        bool _rolling;
        bool _jukeArmed;
        float _lastJukeTime = float.NegativeInfinity;
        Quaternion _visualRestRotation;

        /// <summary>Raised when the juke arms (true, cooldown elapsed) or is spent (false, the
        /// instant a juke fires). The HUD binds this to the Charge-row strike icon's pip.</summary>
        public event Action<bool> OnJukeChargeChanged;

        /// <summary>True while a juke is available.</summary>
        public bool IsJukeArmed => _jukeArmed;

        /// <summary>
        /// True while the dash's displacement window is live — the window in which a ball
        /// contact counts as a JUKE STRIKE. Scarab Scramble's steal reads this
        /// (<c>AstroLeagueBall.IsJukeStrike</c>): the committed dash converts a locked ball,
        /// the casual bump never does. The ball's strike path runs on the SERVER, so a remote
        /// pilot's fire opens the window on the server's replica via
        /// <see cref="NotifyJukeFired_ServerRpc"/> — the strike test then reads the same
        /// property on either machine.
        /// </summary>
        public bool IsJukeStrikeWindowOpen => Time.time - _lastJukeTime <= jukeDurationSeconds;

        /// <summary>
        /// OWNER -> SERVER: this pilot just fired a juke; open the strike window on the
        /// server's replica so the (server-side) ball strike path can see the dash. Same
        /// owner-detects / server-records family as <c>Player.ReportFaunaKill_ServerRpc</c>.
        /// RequireOwnership stays at its default (true), so only the juking pilot's owner can
        /// open their own window; the window's LENGTH is the server's serialized
        /// <see cref="jukeDurationSeconds"/>, never a client-supplied number. Arrival is one
        /// half-RTT after the local fire, which shifts the window late by the same latency the
        /// dashed vessel's NetworkTransform pose arrives with — the two travel together.
        /// </summary>
        [ServerRpc]
        void NotifyJukeFired_ServerRpc()
        {
            _lastJukeTime = Time.time;
        }

        /// <summary>Raised the instant a juke fires, carrying the world-space dash DIRECTION.
        /// <see cref="ScarabCavitationBlast"/> rides this so the blast leaves along the dash —
        /// the dash itself stays free and the blast keeps its own (CHARGE-scaled) cooldown, so
        /// declining to fire the punch never blocks the dodge.</summary>
        public event Action<Vector3> OnJukeFired;

        void Awake()
        {
            _status = GetComponent<VesselStatus>();
        }

        void Update()
        {
            if (_status == null) return;

            // Cooldown re-arm: pure input pacing off the fire timestamp.
            if (!_jukeArmed && Time.time - _lastJukeTime >= jukeCooldownSeconds)
                SetJukeArmed(true);

            if (!_jukeArmed || _rolling) return;
            if (_status.AutoPilotEnabled) return;

            var input = _status.InputStatus;
            if (input == null) return;

            // RIGHT stick — the Scarab's steering is left-stick-only, so this collides with
            // nothing (verified: no InputEvent is raised from right-stick deflection; the
            // straight-line gesture events fold stick components in but the Scarab leaves
            // them unbound).
            var stick = input.RightNormalizedJoystickPosition;
            if (stick.magnitude < perimeterThreshold - ThresholdEpsilon) return;

            float rollSign = (stick.x >= 0f ? 1f : -1f) * (invertRollDirection ? -1f : 1f);

            var transformer = _status.VesselTransformer;
            if (!transformer) return;

            // Stick deflection picks the orthogonal shove direction, projected onto the plane
            // orthogonal to travel so the displacement never adds forward/backward speed.
            // (The restricted-stance fallback is kept from the donor for safety — the Scarab
            // ships no stance, but a mode could restrict any vessel.)
            bool restricted = _status.IsTranslationRestricted;
            var ship = _status.ShipTransform ? _status.ShipTransform : transform;
            Vector3 shove = ship.right * stick.x + ship.up * stick.y;
            shove = Vector3.ProjectOnPlane(shove, restricted ? transform.forward : _status.Course);
            if (shove.sqrMagnitude < 1e-4f)
                shove = ship.right * rollSign;

            CSDebug.Log($"[ScarabJuke] Fired: {(rollSign > 0f ? "CW" : "CCW")}, " +
                        $"stick ({stick.x:F2}, {stick.y:F2}), dir {shove.normalized}");

            _lastJukeTime = Time.time;
            SetJukeArmed(false);
            // Mirror the fire onto the server's replica so the juke-steal window exists where
            // the ball strike is resolved. Host/local sessions are already the server copy.
            if (IsSpawned && !IsServer)
                NotifyJukeFired_ServerRpc();
            transformer.ModifyVelocity(shove.normalized * jukeSpeed, jukeDurationSeconds,
                                       ignoresTranslationRestriction: true);
            OnJukeFired?.Invoke(shove.normalized);
            StartCoroutine(RollRoutine(rollSign, transformer));
        }

        void SetJukeArmed(bool armed)
        {
            if (armed == _jukeArmed) return;
            _jukeArmed = armed;
            OnJukeChargeChanged?.Invoke(armed);
        }

        IEnumerator RollRoutine(float rollSign, VesselTransformer transformer)
        {
            _rolling = true;

            var visual = ResolveVisualTarget();
            var visualStart = visual ? visual.localRotation : Quaternion.identity;
            _visualRestRotation = visualStart;

            // Roll about the VESSEL's flight forward as seen from the visual target's local
            // frame — the model's authored axes needn't align with flight forward.
            Vector3 localRollAxis = visual
                ? visual.InverseTransformDirection(transform.forward)
                : Vector3.forward;
            if (localRollAxis.sqrMagnitude < 1e-6f)
                localRollAxis = Vector3.forward;

            float elapsed = 0f;

            while (elapsed < jukeDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / jukeDurationSeconds);
                float angle = rollSign * 360f * (t * t * (3f - 2f * t)); // smoothstep 0→360

                if (visual)
                    visual.localRotation = visualStart * Quaternion.AngleAxis(angle, localRollAxis);

                if (rootRollDegrees > 0f)
                    transformer.ApplyRotation(
                        rollSign * rootRollDegrees * (Time.deltaTime / jukeDurationSeconds),
                        transform.forward);

                // Bridging prisms orient along the actual travel direction while the
                // displacement is live (replicates via the owner-written n_BlockRotation).
                if (_status.IsTranslationRestricted)
                {
                    transformer.BlockRotationOverride = null;
                }
                else
                {
                    var travel = _status.Speed * _status.Course + transformer.VelocityShift;
                    transformer.BlockRotationOverride = travel.sqrMagnitude > 1e-4f
                        ? (Quaternion?)Quaternion.LookRotation(travel.normalized, transform.up)
                        : null;
                }

                yield return null;
            }

            if (visual) visual.localRotation = visualStart;
            transformer.BlockRotationOverride = null;
            _rolling = false;
        }

        Transform ResolveVisualTarget()
        {
            if (rollVisualTarget) return rollVisualTarget;
            var animator = GetComponentInChildren<Animator>();
            if (animator) return rollVisualTarget = animator.transform;
            return rollVisualTarget = transform.childCount > 0 ? transform.GetChild(0) : null;
        }

        void OnDisable()
        {
            // Never leave a half-applied juke behind (pooling / vessel swap safety).
            StopAllCoroutines();
            if (_status?.VesselTransformer)
                _status.VesselTransformer.BlockRotationOverride = null;
            if (rollVisualTarget && _rolling)
                rollVisualTarget.localRotation = _visualRestRotation;
            _rolling = false;

            // A re-enabled vessel starts recharging from now, not armed — the HUD re-seeds
            // from IsJukeArmed at init.
            _lastJukeTime = Time.time;
            SetJukeArmed(false);
        }
    }
}
