using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class GunTransformer : MonoBehaviour
    {
        [SerializeField] float radius = 20f;
        float constant;

        [RequireInterface(typeof(IVesselStatus))]
        [SerializeField] MonoBehaviour shipInstance;
        [SerializeField] Transform gunFocus;

        IInputStatus InputStatus => (shipInstance as IVesselStatus).InputStatus;

        // Cached to avoid per-frame GetComponentsInChildren allocation
        private Transform[] _children;

        void Start()
        {
            CacheChildren();
            InputStatus.RightClampedPosition.SqrMagnitude();
        }

        void CacheChildren()
        {
            var all = GetComponentsInChildren<Transform>();
            // Filter out self — count first to avoid List allocation
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != transform) count++;
            }
            _children = new Transform[count];
            int idx = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != transform) _children[idx++] = all[i];
            }
            constant = 2 * Mathf.PI / _children.Length;
        }

        void Update()
        {
            for (int i = 0; i < _children.Length; i++)
            {
                var child = _children[i];
                if (!child) continue;

                var j = i * constant + (Mathf.PI) / 2 - Mathf.Atan2(InputStatus.RightNormalizedJoystickPosition.y,
                                                   InputStatus.RightNormalizedJoystickPosition.x);

                child.localPosition = Vector3.Slerp(child.localPosition, radius * new Vector3(Mathf.Sin(j), Mathf.Cos(j), 0), Time.deltaTime * 3);
                gunFocus.localPosition = Vector3.Lerp(gunFocus.localPosition, new Vector3(0, 0, 300 * InputStatus.RightNormalizedJoystickPosition.SqrMagnitude() + 70), Time.deltaTime);
                child.LookAt(gunFocus);
            }
        }
    }
}
