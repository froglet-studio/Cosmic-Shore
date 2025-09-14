using System.Collections;
using System.Collections.Generic;
using CosmicShore.Game;
using UnityEngine;
using UnityEngine.Device;

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
            Quaternion newRotation = Quaternion.LookRotation(VesselStatus.Course, Vector3.back);
            transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, LERP_AMOUNT * Time.deltaTime);
        }

    }
}
