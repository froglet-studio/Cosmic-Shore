using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Meerkat's flight (<c>R_VesselActions/MEERKAT.md</c>): one thumb, as every one-thumb hull
    /// (<see cref="SingleStickVesselTransformer"/>: the left stick's Ease(2 × stick) yaws and pitches at full throttle and
    /// banks into the turn), so the right thumb is always free to shape the Soliton Tunnel. The Vessel Studio's numbers live on
    /// the prefab: <c>pitchYaw</c> 120 °/s is <c>PitchScaler</c> = <c>YawScaler</c>, the bank <c>bankShare</c> × <c>roll</c>
    /// (0.12 × 130 = 15.6 °/s) is <c>RollScaler</c>, <c>cruise</c> 60 is <c>DefaultThrottleScaler</c>.
    ///
    /// <para><b>The slip-and-slide.</b> Inside a Soliton Tunnel (<see cref="MeerkatSolitonExecutor.StepRide"/>) or a Soliton
    /// Network (<see cref="MeerkatSolitonExecutor.StepNetwork"/>) the slide replaces both the turn and the move: radial
    /// gravity presses the hull to the inner wall, it slides and now and then bounces, RT gives a small push, and the left
    /// stick does nothing; on the way out it leaves with the velocity it had × the exit multiplier (the slingshot), and the
    /// turn picks up from that heading.</para>
    ///
    /// <para><b>The AI shaping its reticle</b> holds its line (the page's left stick at rest while the right one bends):
    /// <see cref="MeerkatSolitonExecutor.AIHoldsLine"/> skips the turn for those frames.</para>
    /// </summary>
    public class MeerkatVesselTransformer : SingleStickVesselTransformer, IHullContactGate
    {
        /// <summary>
        /// Round 25 (Yash, 2026-10-28: "anything that passes through the tunnel doesn't interact with the outer world ... It's
        /// like going through another dimension ... When it comes out of the hole, then it interactable again"): while a tunnel
        /// or a Soliton Network flies this hull it is PHASED - its touch meets no prism and takes no crystal, and no prism or crystal
        /// touches it (<see cref="IHullContactGate"/>, read by both halves of every contact, the skimmer's too). A race ring
        /// threaded inside still counts (it is a sweep, not a touch).
        /// </summary>
        public bool Phased
        {
            get
            {
                var wormhole = Wormhole;
                return wormhole && (wormhole.IsRiding || wormhole.IsInNetwork);
            }
        }

        public bool TakesCrystalsByContact => !Phased;
        public bool TakesPrismsByContact => !Phased;

        MeerkatSolitonExecutor _wormhole;
        bool _resolved;
        bool _rodeLastFrame;

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            _resolved = false;
            _rodeLastFrame = false;
        }

        MeerkatSolitonExecutor Wormhole
        {
            get
            {
                if (_resolved && _wormhole) return _wormhole;
                _wormhole = GetComponentInChildren<MeerkatSolitonExecutor>(true);
                _resolved = _wormhole;
                return _wormhole;
            }
        }

        protected override void RotateShip()
        {
            var wormhole = Wormhole;
            if (wormhole && (wormhole.IsRiding || wormhole.IsInNetwork)) return;   // MoveShip flies the slide (tunnel or network), facing and all
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
                if (cameOut) LeaveRide(TrackedExitSpeed(rideSpeed, wormhole.RideExitMultiplier));
                return;
            }
            if (wormhole && wormhole.StepNetwork(Time.deltaTime, out position, out rotation, out rideSpeed, out heading, out cameOut))
            {
                transform.SetPositionAndRotation(position, rotation);
                accumulatedRotation = rotation;
                VesselStatus.Course = heading;
                VesselStatus.Speed = rideSpeed;
                _rodeLastFrame = true;
                if (cameOut) LeaveRide(TrackedExitSpeed(rideSpeed, wormhole.RideExitMultiplier));   // the network's way out is published too
                return;
            }
            if (_rodeLastFrame) LeaveRide(speed);
            base.MoveShip();
        }

        /// <summary>Out of the tunnel: fly on from where it left the hull, at the speed it went in with.
        /// <paramref name="trackedSpeed"/> is in the transformer's own units (<see cref="VesselTransformer.TrackedSpeed"/>).</summary>
        void LeaveRide(float trackedSpeed)
        {
            _rodeLastFrame = false;
            AdoptCurrentMotion();   // the facing (and the vector model) from where the tunnel left the hull
            SetInitialSpeed(trackedSpeed);
        }

        /// <summary>
        /// The ride's way-out speed (a PUBLISHED speed, <c>VesselStatus.Speed</c>: the entry speed read after the
        /// throttle modifier and the warp field, times rideExitMultiplier) as the tracked speed <see cref="MoveShip"/>
        /// multiplies by those two again. Writing the published speed straight into the tracked one applied them twice,
        /// so every tunnel ratcheted the hull by rideExit x throttle modifier x warp; the lava lamp's AI chains a tunnel
        /// every couple of seconds and the eased fall back to cruise (1.5/s) could not keep up, so the hull ran away to
        /// infinity (worst in a warp field's far side, where the scale grows the further out it flies). Never more than
        /// rideExitMultiplier x the cruise it is heading for, so nothing a tunnel is fed can compound.
        /// </summary>
        float TrackedExitSpeed(float publishedExitSpeed, float exitMultiplier)
        {
            float scale = throttleMultiplier * WarpFieldRuntime.ScaleAt(transform.position);
            float tracked = scale > 1e-3f ? publishedExitSpeed / scale : publishedExitSpeed;
            float cap = Mathf.Max(0f, Mathf.Max(CurrentThrottleTarget, CruiseSpeed)) * Mathf.Max(1f, exitMultiplier);
            if (float.IsNaN(tracked) || float.IsInfinity(tracked)) tracked = cap;
            return Mathf.Clamp(tracked, 0f, cap);
        }
    }
}
