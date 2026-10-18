using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    public class GunTransformer : MonoBehaviour
    {
        [SerializeField] float radius = 20f;
        float constant;

        // Filled each frame by the List overload: the same active children GetComponentsInChildren returned, with no
        // new array per frame (the per-frame allocation scan's only real Update finding, 2026-10-16).
        readonly System.Collections.Generic.List<Transform> _children = new();

        [RequireInterface(typeof(IVesselStatus))]
        [SerializeField] MonoBehaviour shipInstance;
        [SerializeField] Transform gunFocus;

        IInputStatus InputStatus => (shipInstance as IVesselStatus).InputStatus;

        void Start()
        {
            var children = GetComponentsInChildren<Transform>();
            constant = 2 * Mathf.PI / (children.Length - 1); // -1 because this is in list;
            InputStatus.RightClampedPosition.SqrMagnitude();
        }

        void Update()
        {
            var i = 0;
            GetComponentsInChildren(false, _children);
            for (int c = 0; c < _children.Count; c++)
            {
                var child = _children[c];
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