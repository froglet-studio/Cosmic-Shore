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
    /// orbit's plane at under half the free-flight turn rate, so you aim the release without
    /// fighting the spin.</item>
    /// </list>
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

        bool Pivoting => thresher && thresher.IsPivoting;

        protected override void Update()
        {
            // Local hit-stop (multiplayer): the hull and its ball hold still for a few frames.
            if (thresher && thresher.IsVesselFrozen) return;
            base.Update();
        }

        public override void ResetTransformer()
        {
            base.ResetTransformer();
            _lastShipVelocity = Vector3.zero;
            _wasPivoting = false;
        }

        protected override void RotateShip()
        {
            bool pivoting = Pivoting;

            if (!pivoting)
            {
                // The release edge: fly off along the orbit's TANGENT. Grip closes the whole angle
                // between momentum and nose on the next move step, so a nose still lagging the
                // tangent would bend the fling inward by exactly that lag.
                if (_wasPivoting && _lastShipVelocity.sqrMagnitude > 1e-4f)
                    FaceAlong(_lastShipVelocity, 1f);
                _wasPivoting = false;
                base.RotateShip();
                return;
            }

            _wasPivoting = true;
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
            if (!Pivoting || InputStatus == null) return Vector3.zero;
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

            Vector3 shaped = thresher.StepChain(transform.position, velocity, ComputeThrottleTarget(), dt, PivotSteer());
            _lastShipVelocity = shaped;

            // Towing: turn the hull with the tug (see the class docs) — the same minimal rotation
            // applied to the commanded rotation, so the pilot's next stick input starts from it.
            if (!thresher.IsPivoting && velocity.sqrMagnitude > 1e-4f && shaped.sqrMagnitude > 1e-4f)
            {
                Quaternion bend = Quaternion.FromToRotation(velocity, shaped);
                transform.rotation = bend * transform.rotation;
                accumulatedRotation = bend * accumulatedRotation;
            }

            return shaped - velocity;
        }
    }
}
