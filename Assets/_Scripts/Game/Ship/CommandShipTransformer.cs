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
            ship.ShipStatus.CommandStickControls = true;
            speed = .1f;
        }

        //protected override void Update()
        //{

        //}

        protected override void MoveShip()
        {
            var mapscaleX = 2f;
            var mapscaleY = 2f;
            var TwoDPosition = inputController.SingleTouchValue;
            var ThreeDPosition= new Vector3((TwoDPosition.x  - UnityEngine.Screen.width / 2) * mapscaleX, (TwoDPosition.y - UnityEngine.Screen.height / 2) * mapscaleY, 0);
  
            //Debug.Log($"commandshiptransformer inputController.LeftJoystickHome {inputController.LeftJoystickHome}");
            //Debug.Log($"commandshiptransformer inputController.RightJoystickHome {inputController.RightJoystickHome}");
            //Debug.Log($"commandshiptransformer inputController.LeftJoystickStart {inputController.LeftJoystickStart}");
            //Debug.Log($"commandshiptransformer inputController.RightJoystickStart {inputController.RightJoystickStart}");
            //Debug.Log($"commandshiptransformer inputController.LeftClampedPosition {inputController.LeftClampedPosition}");
            //Debug.Log($"commandshiptransformer inputController.RightClampedPosition {inputController.RightClampedPosition}");
          
            //transform.position = Vector3.Lerp(transform.position, newPosition, speed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, ThreeDPosition, speed * Time.deltaTime);
            shipStatus.Course = ThreeDPosition - transform.position;
        }

        protected override void RotateShip()
        {
            Quaternion newRotation = Quaternion.LookRotation(shipStatus.Course, Vector3.back);
            transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, lerpAmount * Time.deltaTime);
        }

    }
}
