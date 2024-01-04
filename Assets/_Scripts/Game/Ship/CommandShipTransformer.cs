using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class CommandShipTransformer : ShipTransformer
    {
        // Start is called before the first frame update
        //protected override void Start()
        //{
            
        //}

        //// Update is called once per frame
        //protected override void Update()
        //{
        
        //}

        protected override void MoveShip()
        {
            speed = 1;
            var newPosition = inputController.LeftClampedPosition;
  
            //Debug.Log($"commandshiptransformer inputController.LeftJoystickHome {inputController.LeftJoystickHome}");
            //Debug.Log($"commandshiptransformer inputController.RightJoystickHome {inputController.RightJoystickHome}");
            //Debug.Log($"commandshiptransformer inputController.LeftJoystickStart {inputController.LeftJoystickStart}");
            //Debug.Log($"commandshiptransformer inputController.RightJoystickStart {inputController.RightJoystickStart}");
            //Debug.Log($"commandshiptransformer inputController.LeftClampedPosition {inputController.LeftClampedPosition}");
            //Debug.Log($"commandshiptransformer inputController.RightClampedPosition {inputController.RightClampedPosition}");
          
            //transform.position = Vector3.Lerp(transform.position, newPosition, speed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, newPosition, speed * Time.deltaTime);
        }

        protected override void RotateShip()
        {
            Quaternion newRotation = Quaternion.LookRotation(shipStatus.Course, Vector3.up);
            transform.rotation = Quaternion.Lerp(
                                        transform.rotation,
                                        newRotation,
                                        lerpAmount * Time.deltaTime);

        }

    }
}
