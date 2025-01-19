using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Device;

namespace CosmicShore
{
    public class CommandShipTransformer : ShipTransformer
    {

        protected override void Start()
        {
            base.Start();
            Ship.ShipStatus.CommandStickControls = true;
            speed = .1f;
        }

        protected override void MoveShip()
        {
            transform.position = Vector3.Lerp(transform.position, InputStatus.ThreeDPosition, speed * Time.deltaTime);
            shipStatus.Course = InputStatus.ThreeDPosition - transform.position;
        }

        protected override void RotateShip()
        {
            Quaternion newRotation = Quaternion.LookRotation(shipStatus.Course, Vector3.back);
            transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, lerpAmount * Time.deltaTime);
        }

    }
}
