using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;
using UnityEngine.UIElements;

namespace CosmicShore
{
    public class GunRingTransformer : MonoBehaviour
    {
        [SerializeField] float radius = 20f;
        float constant;

        [RequireInterface(typeof(IVesselStatus))]
        [SerializeField] MonoBehaviour shipInstance;
        [SerializeField] Transform gunFocus;
        [SerializeField] float UnitsPerSec = 3;

        IInputStatus InputStatus => (shipInstance as IVesselStatus).InputStatus;




        void Start()
        {
            var children = GetComponentsInChildren<Transform>();
            constant = 2 * Mathf.PI / (children.Length - 1); //Finds the radian so all point are equally spaced | Subtract one to remove the parent
            InputStatus.RightClampedPosition.SqrMagnitude();
        }

        // Update is called once per frame
        void Update()
        {
            var i = 0;

            foreach (var child in GetComponentsInChildren<Transform>())
            {

                if (child == transform)
                {
                    continue;
                }

                //Compute an angle offset from the joystick direction, rotated 90°, and spaced apart by a multiple of constant
                var RotatedAngle = i * constant + (Mathf.PI) / 2 - Mathf.Atan2(InputStatus.RightNormalizedJoystickPosition.y,InputStatus.RightNormalizedJoystickPosition.x); 
                i++;



                /* 
                  Smoothly move the child toward its target position on a circular formation that rotates based on the right joystick’s direction. 
                  The target point is determined by angle 'RotatedAngle' and radius, while Slerp ensures smooth, frame-rate independent motion along the circle 
                */
                child.transform.localPosition = Vector3.Slerp(child.transform.localPosition, radius * new Vector3(Mathf.Sin(RotatedAngle), Mathf.Cos(RotatedAngle), 0), Time.deltaTime * UnitsPerSec);


                /* 
                   Smoothly adjust the gun's focus position forward or backward based on how far the right joystick is pushed. 
                   The farther the stick is tilted, the greater the Z offset (up to 300 units), with a base offset of 70.
                */
                gunFocus.localPosition = Vector3.Lerp(gunFocus.localPosition, new Vector3(0, 0, 300 * InputStatus.RightNormalizedJoystickPosition.SqrMagnitude() + 70), Time.deltaTime);

                // Orient child to face the gun focus.
                child.LookAt(gunFocus);

            }
        }
    }
}
