using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's flight: an ordinary two-stick vector-model flyer towing a wrecking ball.
    ///
    /// It inherits <see cref="VesselTransformer"/> and overrides SEAMS, never <c>MoveShip</c>, so
    /// the danger-prism slow, knockback, the speed tunnel and every future fleet-wide change reach
    /// it for free. The chain is solved by <see cref="ThresherExecutor"/> (which owns the pure
    /// <see cref="ThresherChainSolver"/>) and enters the move step through exactly one door,
    /// <see cref="ComputeExternalAcceleration"/>, in the same frame and order as thrust.
    ///
    /// Two modes:
    /// <list type="bullet">
    /// <item><b>Towing</b> (and while the ball skids to a plant): the sticks fly the ship exactly
    /// like a Squirrel. The rope's tug changes the ship's velocity, and the hull is turned WITH
    /// that change — grip snaps momentum onto the nose every frame (convergence 1), so a tug that
    /// did not also turn the nose would be erased on the next frame and the heavy ball would have
    /// no pull on your line at all.</item>
    /// <item><b>Pivot</b> (left trigger held, ball planted): the ship orbits the ball. Thrust and
    /// grip are switched off (<see cref="ComputeNoseAcceleration"/> 0, <see cref="NoseConvergence"/>
    /// 0) so the solver owns the velocity outright, and the hull is turned to face the orbit's
    /// tangent so that on release you fly off along it — not along wherever the nose last
    /// pointed. The sticks keep a LOW-sensitivity say (<see cref="PivotSteer"/>): they lean the
    /// orbit's plane at under half the free-flight turn rate. Once the spin is fast enough that the
    /// local pilot's camera SPECTATES it, the sticks aim the release instead
    /// (<see cref="ThresherExecutor.IsAiming"/>) and the orbit is left alone; releasing then faces
    /// the hull straight along the aim.</item>
    /// </list>
    ///
    /// <b>The exit buff</b> (2026-10-30): for <c>launchExitSeconds</c> after an AIMED launch the hull travels
    /// <c>launchExitSpeedMultiplier</c> x what the physics gives it, easing back to 1x, to make up the time the swing
    /// spent in place. It rides the fleet's own output-only channel (<c>throttleMultiplier</c>, which scales the
    /// frame's travel and never the momentum), so the flight model is untouched; <see cref="MoveShip"/> wraps the
    /// base move only to hand the extra travel to the ball and chain (<see cref="ThresherExecutor.CarryRig"/>).
    ///
    /// <b>Hit-stop</b> in a multiplayer session cannot touch <c>Time.timeScale</c> (it would
    /// freeze every peer's world on this client), so there the executor freezes THIS hull and its
    /// ball instead: <see cref="Update"/> skips the frame outright.
    /// </summary>
    public class ThresherVesselTransformer : VesselTransformer
    {
        [Header("Thresher")]
        [Tooltip("The executor that owns the chain. Lives on the ShipActions object, next to the ActionExecutorRegistry.")]
        [SerializeField] ThresherExecutor thresher;

        [Tooltip("While orbiting a planted ball, how fast the hull turns to face the orbit's tangent (1/s). " +
                 "High, because on release the ship flies off along the nose: the lag is the angle a release " +
                 "would bend inward. The release itself snaps the nose onto the velocity, so this only shapes " +
                 "how the hook-on reads.")]
        [SerializeField, Min(0f)] float pivotFacingRate = 25f;

        Vector3 _lastShipVelocity;
        bool _wasPivoting;
        // The planted orbit's spin, captured each planted frame, and the wind-down that follows a release
        // (releaseSpinDownSeconds): the hull keeps turning about the orbit's axis and eases to a stop.
        Vector3 _orbitAxis;
        float _orbitRate;
        Vector3 _spinAxis;
        float _spinRate0, _spinT, _spinDuration;
        // The aimed launch's exit buff: seconds since the launch (negative = none), and this frame's multiplier.
        float _exitT = -1f;
        float _exitApplied = 1f;

        bool Pivoting => thresher && thresher.IsPivoting;
        ThresherConfigSO Config => thresher ? thresher.Config : null;

        protected override void Update()
        {
            // Local hit-stop (multiplayer): the hull and its ball hold still for a few frames.
            if (thresher && thresher.IsVesselFrozen) return;
            // Analog triggers: read the triggers BEFORE the hull turns and moves, so a full pull that plants
            // the ball this frame is already a plant when RotateShip and the move run.
            if (thresher) thresher.PollTriggers(transform.position, transform.up, Time.deltaTime);
            base.Update();
        }

        public override void ResetTransformer()
        {
            base.ResetTransformer();
            _lastShipVelocity = Vector3.zero;
            _wasPivoting = false;
            _orbitRate = 0f;
            _spinDuration = 0f;
            _exitT = -1f;
            _exitApplied = 1f;
        }

        /// <summary>The base move, then the exit buff's extra travel handed to the ball and chain: the hull went
        /// <c>1 - 1/k</c> of this frame's travel further than the physics moved it, and the rig goes with it.</summary>
        protected override void MoveShip()
        {
            _exitApplied = 1f;
            base.MoveShip();
            if (_exitApplied > 1f && thresher && VesselStatus != null)
                thresher.CarryRig(VesselStatus.Course * (VesselStatus.Speed * Time.deltaTime * (1f - 1f / _exitApplied)));
        }

        /// <summary>How long a wind-down from <paramref name="rate"/> (rad/s) takes: releaseSpinDownSeconds,
        /// shortened so the extra turn it adds (rate x time / 2) never exceeds releaseSpinDownMaxDegrees.</summary>
        static float SpinDownTime(ThresherConfigSO cfg, float rate)
            => Mathf.Max(1e-3f, Mathf.Min(cfg.ReleaseSpinDownSeconds,
                   2f * cfg.ReleaseSpinDownMaxDegrees * Mathf.Deg2Rad / Mathf.Max(1e-3f, Mathf.Abs(rate))));

        protected override void RotateShip()
        {
            bool pivoting = Pivoting;

            if (!pivoting)
            {
                if (_wasPivoting)
                {
                    Vector3 aimed = Vector3.zero;
                    bool aimedRelease = thresher && thresher.TryConsumeReleaseDirection(out aimed);
                    var cfg = Config;
                    if (cfg && cfg.ReleaseSpinDownSeconds > 0f && Mathf.Abs(_orbitRate) > 1e-4f)
                    {
                        // Wind the spin down instead of stopping it dead: the hull keeps turning about the
                        // orbit's axis and eases to a stop (an aimed launch left early by the angle this adds).
                        _spinAxis = _orbitAxis;
                        _spinRate0 = _orbitRate;
                        _spinT = 0f;
                        _spinDuration = SpinDownTime(cfg, _orbitRate);
                    }
                    // The release edge: fly off along the orbit's TANGENT. Grip closes the whole angle
                    // between momentum and nose on the next move step, so a nose still lagging the
                    // tangent would bend the fling inward by exactly that lag.
                    // An AIMED release (the spectate camera's aim) replaces the tangent outright.
                    else if (aimedRelease)
                        FaceAlong(aimed, 1f);
                    else if (_lastShipVelocity.sqrMagnitude > 1e-4f)
                        FaceAlong(_lastShipVelocity, 1f);
                    _wasPivoting = false;
                }
                if (_spinDuration > 0f)
                {
                    float dt = Time.deltaTime;
                    float u = _spinT / _spinDuration;
                    float w = _spinRate0 * (1f - u * u * (3f - 2f * u));
                    transform.rotation = Quaternion.AngleAxis(w * dt * Mathf.Rad2Deg, _spinAxis) * transform.rotation;
                    accumulatedRotation = transform.rotation;
                    _spinT += dt;
                    if (_spinT >= _spinDuration) _spinDuration = 0f;
                    return;   // the wind-down owns the hull for its 0.1-0.3 s
                }
                base.RotateShip();
                return;
            }

            _wasPivoting = true;
            _spinDuration = 0f;
            if (_lastShipVelocity.sqrMagnitude > 1e-4f)
                FaceAlong(_lastShipVelocity, 1f - Mathf.Exp(-pivotFacingRate * Time.deltaTime));
        }

        /// <summary>Turn the hull toward <paramref name="direction"/> by the minimal rotation
        /// (parallel transport), so the camera's roll does not spin with the orbit.</summary>
        void FaceAlong(Vector3 direction, float fraction)
        {
            Quaternion delta = Quaternion.FromToRotation(transform.forward, direction.normalized);
            Quaternion target = delta * transform.rotation;
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Mathf.Clamp01(fraction));
            accumulatedRotation = transform.rotation;
        }

        /// <summary>
        /// The sticks while planted, as the world direction the NOSE would move under them in free
        /// flight (yaw right = +right, pitch = −up, the base <c>Yaw</c>/<c>Pitch</c> sign
        /// convention), magnitude clamped to 1. The solver turns only its across-the-orbit part
        /// into a slow tilt of the orbit plane (<c>pivotSteer</c>, 45°/s at full stick against free
        /// flight's 120°/s): the spin stays the ship's, the stick leans it. Zero while towing — the
        /// base rotation already flies the hull then.
        /// </summary>
        Vector3 PivotSteer()
        {
            // While the camera spectates, the sticks AIM the release (the executor reads them).
            if (!Pivoting || InputStatus == null || thresher.IsAiming) return Vector3.zero;
            return Vector3.ClampMagnitude(
                transform.right * InputStatus.XSum - transform.up * InputStatus.YSum, 1f);
        }

        protected override float NoseConvergence(float dt)
            => Pivoting ? 0f : base.NoseConvergence(dt);

        protected override float ComputeNoseAcceleration(float dt)
            => Pivoting ? 0f : base.ComputeNoseAcceleration(dt);

        protected override Vector3 ComputeExternalAcceleration(Vector3 velocity, float dt)
        {
            if (!thresher) return Vector3.zero;
            var cfg = Config;
            bool spinDown = cfg && cfg.ReleaseSpinDownSeconds > 0f;
            bool keepUp = cfg && cfg.HullFollowsPlantedVelocity;

            // An ARMED launch with a wind-down leaves early by the angle the wind-down will still turn, less
            // the hull's lag behind its velocity, so the ship lands on the aim instead of past it.
            float lead = 0f;
            if (spinDown && thresher.IsPivoting && thresher.LaunchArmed &&
                thresher.TryGetOrbitSpin(transform.position, out _, out float rate))
            {
                float lag = Vector3.Angle(transform.forward, _lastShipVelocity) * Mathf.Deg2Rad;
                lead = Mathf.Max(1e-4f, Mathf.Abs(rate) * SpinDownTime(cfg, rate) * 0.5f - lag);
            }
            thresher.SetLaunchLead(lead);

            Vector3 shaped = thresher.StepChain(transform.position, velocity, ComputeThrottleTarget(), dt, PivotSteer());
            _lastShipVelocity = shaped;

            // The exit buff: from the aimed launch's own frame. The multiplier is rebuilt from the throttle modifiers
            // every frame before the move, so scaling it here scales only this frame's travel. A new plant ends it.
            if (thresher.LaunchedThisStep) _exitT = 0f;
            if (_exitT >= 0f)
            {
                float k = cfg ? ThresherChainSolver.LaunchExitMultiplier(_exitT, cfg.LaunchExitSpeedMultiplier, cfg.LaunchExitSeconds) : 1f;
                if (thresher.IsPivoting || k <= 1f) _exitT = -1f;
                else
                {
                    throttleMultiplier *= k;
                    _exitApplied = k;
                    _exitT += dt;
                }
            }
            if (thresher.TryGetOrbitSpin(transform.position, out Vector3 axis, out float spin)) { _orbitAxis = axis; _orbitRate = spin; }

            // Towing: turn the hull with the tug (see the class docs) — the same minimal rotation
            // applied to the commanded rotation, so the pilot's next stick input starts from it.
            // hullFollowsPlantedVelocity: also while planted, so the hull keeps up with this frame's own turn
            // rather than facing last frame's velocity. On a wind-down's launch frame the spin turns the hull,
            // unless the hull keeps up, when this bend IS the frame's share of the orbit's turn.
            bool turnHull = (!thresher.IsPivoting || keepUp) && !(thresher.LaunchedThisStep && spinDown && !keepUp);
            if (turnHull && velocity.sqrMagnitude > 1e-4f && shaped.sqrMagnitude > 1e-4f)
            {
                Quaternion bend = Quaternion.FromToRotation(velocity, shaped);
                transform.rotation = bend * transform.rotation;
                accumulatedRotation = bend * accumulatedRotation;
            }

            return shaped - velocity;
        }
    }
}
