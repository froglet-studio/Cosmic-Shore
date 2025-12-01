using CosmicShore.Game;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore
{
    public class CommandVesselTransformer : VesselTransformer
    {

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            VesselStatus.InputStatus.CommandStickControls = true;
            speed = .1f;
        }

        protected override void MoveShip()
        {
            transform.position = Vector3.Lerp(transform.position, InputStatus.ThreeDPosition, speed * Time.deltaTime);
            VesselStatus.Course = InputStatus.ThreeDPosition - transform.position;
        }

        protected override void RotateShip()
        {
            if (SafeLookRotation.TryGet(VesselStatus.Course, Vector3.back, out var newRotation, this))
                transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, LERP_AMOUNT * Time.deltaTime);
        }

    }
}