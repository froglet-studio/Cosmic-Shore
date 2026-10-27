using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Meerkat's flight (<c>R_VesselActions/MEERKAT.md</c>): one thumb, as every one-thumb hull
    /// (<see cref="SingleStickVesselTransformer"/>: the left stick's Ease(2 × stick) yaws and pitches at full throttle and
    /// banks into the turn), so the right thumb is always free to shape the wormhole. The Vessel Studio's numbers live on
    /// the prefab: <c>pitchYaw</c> 120 °/s is <c>PitchScaler</c> = <c>YawScaler</c>, the bank <c>bankShare</c> × <c>roll</c>
    /// (0.12 × 130 = 15.6 °/s) is <c>RollScaler</c>, <c>cruise</c> 60 is <c>DefaultThrottleScaler</c>.
    ///
    /// <para><b>The ride.</b> While a tunnel flies the hull (<see cref="MeerkatWormholeExecutor.StepRide"/>) it replaces
    /// both the turn and the move: the hull is placed along the tunnel, faces along it, and reports the ride's speed and
    /// heading. On the way out it keeps the speed it went in with (the page's <c>gateOut</c>), and the turn picks up from
    /// the tunnel's last heading.</para>
    ///
    /// <para><b>The AI shaping its reticle</b> holds its line (the page's left stick at rest while the right one bends):
    /// <see cref="MeerkatWormholeExecutor.AIHoldsLine"/> skips the turn for those frames.</para>
    /// </summary>
    public class MeerkatVesselTransformer : SingleStickVesselTransformer
    {
        MeerkatWormholeExecutor _wormhole;
        bool _resolved;
        bool _rodeLastFrame;

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            _resolved = false;
            _rodeLastFrame = false;
        }

        MeerkatWormholeExecutor Wormhole
        {
            get
            {
                if (_resolved && _wormhole) return _wormhole;
                _wormhole = GetComponentInChildren<MeerkatWormholeExecutor>(true);
                _resolved = _wormhole;
                return _wormhole;
            }
        }

        protected override void RotateShip()
        {
            var wormhole = Wormhole;
            if (wormhole && wormhole.IsRiding) return;   // MoveShip flies the ride, facing and all
            if (wormhole && wormhole.AIHoldsLine)
            {
                VesselStatus.Course = transform.forward;
                return;
            }
            base.RotateShip();
        }

        protected override void MoveShip()
        {
            var wormhole = Wormhole;
            if (wormhole && wormhole.StepRide(Time.deltaTime, transform.rotation, out var position, out var rotation,
                    out float rideSpeed, out var heading, out bool cameOut))
            {
                transform.SetPositionAndRotation(position, rotation);
                accumulatedRotation = rotation;
                VesselStatus.Course = heading;
                VesselStatus.Speed = rideSpeed;
                _rodeLastFrame = true;
                if (cameOut) LeaveRide(rideSpeed);
                return;
            }
            if (_rodeLastFrame) LeaveRide(speed);
            base.MoveShip();
        }

        /// <summary>Out of the tunnel: fly on from where it left the hull, at the speed it went in with.</summary>
        void LeaveRide(float exitSpeed)
        {
            _rodeLastFrame = false;
            SetInitialSpeed(exitSpeed);
            AdoptCurrentMotion();
            SetInitialSpeed(exitSpeed);
        }
    }
}
