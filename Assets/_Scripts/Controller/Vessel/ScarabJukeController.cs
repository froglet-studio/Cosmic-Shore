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
    ///    plain input-pacing cooldown (nothing in the world is removed by it). NOTE the HUD
    ///    deliberately shows NO pip for it: the cooldown ships at 0, so the dash is always
    ///    available and a readout would blink for one frame per dash (SCARAB.md §3.4 — "no
    ///    readout because no cooldown"). <see cref="OnJukeChargeChanged"/> is kept ONLY as the
    ///    binding surface for a mode that ever paces the dash via the cooldown knob below —
    ///    today it has no subscribers, on purpose.
    /// 3. It is an attack surface, so the FIRE is OWNER-ONLY and the rest is sent explicitly.
    ///    Displacement rides the owner-authoritative NetworkTransform; the fire moment makes one
    ///    owner→server round-trip (<see cref="NotifyJukeFired_ServerRpc"/>) so the server's replica
    ///    opens <see cref="IsJukeStrikeWindowOpen"/> — the window Scarab Scramble's juke-steal
    ///    reads on the server — and the server fans the cosmetic 360° spin back out
    ///    (<see cref="BroadcastJukeRoll_ClientRpc"/>). The fuller Juke_ServerRpc carrying
    ///    direction + Charge snapshot remains the Phase 2.5 upgrade path.
    ///
    ///    THE OWNER GATE IS LOAD-BEARING AND WAS MISSING. `InputStatus.RightNormalizedJoystickPosition`
    ///    is a NetworkVariable readable by Everyone, so the detection below ran on every peer off
    ///    the owner's replicated stick: N cavitation blasts per dash, a doubled Astro League ball
    ///    kick, and replicas writing velocity for a vessel they do not own. The spin appearing on
    ///    other machines was a side effect of that bug — which is why it is now broadcast on
    ///    purpose rather than left to fall out of duplicated simulation.
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
        void NotifyJukeFired_ServerRpc(float rollSign)
        {
            _lastJukeTime = Time.time;
            BroadcastJukeRoll_ClientRpc(rollSign);
        }

        /// <summary>
        /// SERVER -> EVERYONE: play the dash's 360° visual roll on the peers that did not fire it.
        /// Purely cosmetic — no displacement, no blast, no state the ball or the scoreboard reads;
        /// the transformer is deliberately not passed, so a replica cannot write velocity, root
        /// rotation, or BlockRotationOverride for a vessel it does not own.
        ///
        /// It exists because owner-gating the fire took this away. Before the gate every peer ran
        /// the fire path off the replicated stick, so the spin appeared everywhere as a side effect
        /// of the duplicate-blast bug; the spin was worth keeping and the duplicates were not, so
        /// it is sent explicitly now. The owner skips it — it already rolled locally, on the frame
        /// the stick hit the perimeter, with no round-trip.
        /// </summary>
        [ClientRpc]
        void BroadcastJukeRoll_ClientRpc(float rollSign)
        {
            if (_status == null) return;
            // The local pilot already rolled on the frame the stick hit the perimeter. Clearing
            // the echo flag here too keeps it from lingering across a mid-life pilot change
            // (Cellular Duel ChangePlayer) and eating a later genuine broadcast.
            if (_status.Player is { IsLocalPilot: true }) { _suppressNextBroadcastEcho = false; return; }
            // A host-simulated AI receives the loopback of its own broadcast one hop after its
            // fire path already started the armed roll — that echo is a duplicate, not a dash.
            if (_suppressNextBroadcastEcho) { _suppressNextBroadcastEcho = false; return; }

            if (_rolling)
            {
                // A back-to-back dash (cooldown ships 0, so the owner's earliest re-fire equals
                // the roll's own duration) whose broadcast landed inside the previous roll's
                // playback. Dropping it made the second dash read as a TELEPORT on every peer —
                // displacement with no spin, no flourish, no whoosh (review finding). RESTART the
                // cosmetic roll instead: snap the visual back to rest and spin again. Only the
                // cosmetic coroutine may be cut — an owner-armed roll holds transformer state
                // (bank suppression, block-rotation override) and must run its tail.
                if (_cosmeticRoll == null) return;
                StopCoroutine(_cosmeticRoll);
                if (rollVisualTarget) rollVisualTarget.localRotation = _visualRestRotation;
                _rolling = false;
            }
            _cosmeticRoll = StartCoroutine(RollRoutine(rollSign, null));
        }

        Coroutine _cosmeticRoll;
        bool _suppressNextBroadcastEcho;

        /// <summary>Raised the instant a juke fires, carrying the world-space dash DIRECTION.
        /// <see cref="ScarabCavitationBlast"/> rides this so the blast leaves along the dash —
        /// the dash itself stays free and the blast keeps its own (CHARGE-scaled) cooldown, so
        /// declining to fire the punch never blocks the dodge. OWNER-ONLY (the fire path is
        /// gated on IsLocalPilot) — a peer that needs the dash's visual beat listens to
        /// <see cref="OnJukeRollStarted"/> instead.</summary>
        public event Action<Vector3> OnJukeFired;

        /// <summary>
        /// Raised at the start of the dash's 360° visual roll, on EVERY machine that plays it —
        /// the owner directly, remote peers via <see cref="BroadcastJukeRoll_ClientRpc"/> —
        /// carrying the roll sign (+1 CW / −1 CCW) and the roll's duration. This is the
        /// animation layer's hook (<see cref="ScarabAnimation"/> snaps the hull's parts with the
        /// spin), which is why it keys off the VISUAL start rather than the owner-only fire:
        /// a flourish that played on one machine and not another would make the same dash read
        /// differently per spectator. Carries no authority — nothing gameplay-bearing may bind
        /// to it (the strike window and the blast ride the owner/server paths above).
        /// </summary>
        public event Action<float, float> OnJukeRollStarted;

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

            // THE STICK IS REPLICATED, SO THIS MUST BE OWNER-GATED.
            // InputStatus.RightNormalizedJoystickPosition is backed by a NetworkVariable with
            // read permission Everyone (InputStatus.n_rNorm), so EVERY peer's copy of this vessel
            // sees the owning pilot's deflection — not just the machine the stick is plugged into.
            // Without this gate the whole fire path below ran on all N peers at once: N cavitation
            // blasts per dash instead of one, a second ball kick on the server (its own blast calls
            // AstroLeagueBall.ApplyBlastServer directly while the owner's blast arrives again
            // through RequestBlastBall_ServerRpc), and a ModifyVelocity write on replicas that only
            // fights the owner-authoritative NetworkTransform.
            //
            // The gate is IsLocalPilot, not IsOwner, and that is deliberate: it is the SAME
            // predicate InputController.Update uses to decide who may CONSUME the stick, and it is
            // the one that also holds on the legacy non-networked single-player spawn path, where
            // IsSpawned is false and IsOwner would report false for a human (CLAUDE.md, IPlayer).
            // A response to local input belongs behind the same gate as the input itself.
            if (_status.Player is not { IsLocalPilot: true }) return;

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

            if (CSDebug.IsVerbose(CSLogChannel.ScarabDash))
                CSDebug.LogVerbose(CSLogChannel.ScarabDash,
                    $"[ScarabJuke] Fired: {(rollSign > 0f ? "CW" : "CCW")}, " +
                    $"stick ({stick.x:F2}, {stick.y:F2}), dir {shove.normalized}");

            _lastJukeTime = Time.time;
            SetJukeArmed(false);
            // Mirror the fire onto the server's replica so the juke-steal window exists where
            // the ball strike is resolved, and fan the VISUAL roll out to the other peers. Both
            // used to happen by accident, because every peer ran the fire path off the replicated
            // stick; now that the fire is owner-only they have to be sent deliberately. The host
            // owns the server copy already, so it broadcasts directly instead of round-tripping.
            if (IsSpawned)
            {
                if (IsServer)
                {
                    // The host executes its own ClientRpc: for a host-simulated AI (not a local
                    // pilot, so the IsLocalPilot gate does not cover it) that loopback would
                    // restart the armed roll this very method starts below. Flag it as an echo.
                    _suppressNextBroadcastEcho = true;
                    BroadcastJukeRoll_ClientRpc(rollSign);
                }
                else NotifyJukeFired_ServerRpc(rollSign);
            }
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

        /// <summary>
        /// The dash's 360° spin. <paramref name="transformer"/> is NULL on the cosmetic path
        /// (<see cref="BroadcastJukeRoll_ClientRpc"/>, a peer that does not own this vessel):
        /// everything that WRITES vessel state — the real root bank and the bridging-prism
        /// rotation override — is skipped there, leaving only the visual child's local rotation.
        /// A replica must never author either: the root rotation is the owner's NetworkTransform
        /// to write, and BlockRotationOverride is read by the owner-written prism lay.
        /// </summary>
        IEnumerator RollRoutine(float rollSign, VesselTransformer transformer)
        {
            _rolling = true;
            OnJukeRollStarted?.Invoke(rollSign, jukeDurationSeconds);

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
            float rootRollProgress = 0f;

            // The dash owns the roll axis for its duration (owner path only — a replica must
            // never write flight state). The bank-into-turn is the same rotation about the same
            // axis, and a juking pilot is usually steering: at full stick the bank's ~20-25°
            // lands on top of the authored 15° pointing the other way, so the pilot's horizon
            // tilted AGAINST the spin for as long as the juke shipped
            // (Docs/ElementalAbilitySystem/BACKLOG.md, closed by this branch).
            // ScarabVesselTransformer.Roll() already honours the flag; cleared in the tail AND
            // in OnDisable, exactly the BarrelRollController reference shape.
            if (transformer)
                transformer.BankIntoTurnSuppressed = true;

            while (elapsed < jukeDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / jukeDurationSeconds);
                float eased = t * t * (3f - 2f * t);                     // smoothstep 0→1
                float angle = rollSign * 360f * eased;

                if (visual)
                    visual.localRotation = visualStart * Quaternion.AngleAxis(angle, localRollAxis);

                if (transformer)
                {
                    // Advanced by the DELTA of the same smoothstep the spin uses, so the real
                    // bank accelerates and settles WITH the animation and the authored degrees
                    // land exactly — summing dt/duration drifted at a constant rate and
                    // overshot on the frame that ends the loop.
                    if (rootRollDegrees > 0f)
                    {
                        transformer.ApplyRotation(
                            rollSign * rootRollDegrees * (eased - rootRollProgress),
                            transform.forward);
                        rootRollProgress = eased;
                    }

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
                }

                yield return null;
            }

            if (visual) visual.localRotation = visualStart;
            if (transformer)
            {
                transformer.BlockRotationOverride = null;
                transformer.BankIntoTurnSuppressed = false;
            }
            else
            {
                _cosmeticRoll = null;
            }
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
            _cosmeticRoll = null;
            _suppressNextBroadcastEcho = false;
            if (_status?.VesselTransformer)
            {
                _status.VesselTransformer.BlockRotationOverride = null;
                _status.VesselTransformer.BankIntoTurnSuppressed = false;
            }
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
