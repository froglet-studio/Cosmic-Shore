using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class GunTransformer : MonoBehaviour
    {
        [SerializeField] float radius = 20f;
        float constant;

        [RequireInterface(typeof(IShipStatus))]
        [SerializeField] MonoBehaviour shipInstance;
        [SerializeField] Transform gunFocus;

        IInputStatus InputStatus => (shipInstance as IShipStatus).InputStatus;

        void Start()
        {
            var children = GetComponentsInChildren<Transform>();
            constant = 2 * Mathf.PI / (children.Length - 1); // -1 because this is in list;
            InputStatus.RightClampedPosition.SqrMagnitude();
        }

        void Update()
        {
            var i = 0;
            foreach (var child in GetComponentsInChildren<Transform>())
            {
                if (child == transform)
                    continue;
                var j = i * constant + (Mathf.PI)/2 - Mathf.Atan2(InputStatus.RightNormalizedJoystickPosition.y,
                                                   InputStatus.RightNormalizedJoystickPosition.x);
                i++;

                child.transform.localPosition = Vector3.Slerp(child.transform.localPosition,radius * new Vector3(Mathf.Sin(j), Mathf.Cos(j), 0),Time.deltaTime*3);
                gunFocus.localPosition = Vector3.Lerp(gunFocus.localPosition, new Vector3(0,0, 300*InputStatus.RightNormalizedJoystickPosition.SqrMagnitude()+70),Time.deltaTime);
                child.LookAt(gunFocus);
            }
        }
    }
}