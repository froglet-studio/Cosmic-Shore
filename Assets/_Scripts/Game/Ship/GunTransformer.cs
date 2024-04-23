using CosmicShore.Core;
using CosmicShore.Game.IO;
using PlayFab.ClientModels;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class GunTransformer : MonoBehaviour
    {
        // Start is called before the first frame update
        [SerializeField] float radius = 20f;
        float constant;
        [SerializeField] Ship ship;
        [SerializeField] Transform gunFocus;

        void Start()
        {
            var children = GetComponentsInChildren<Transform>();
            constant = 2 * Mathf.PI / (children.Length - 1); // -1 because this is in list;
            ship.InputController.RightClampedPosition.SqrMagnitude();
        }

        // Update is called once per frame
        void Update()
        {
            var i = 0;
            foreach (var child in GetComponentsInChildren<Transform>())
            {
                if (child == transform)
                    continue;
                var j = i * constant + (Mathf.PI)/2 - Mathf.Atan2(ship.InputController.RightNormalizedJoystickPosition.y,
                                                   ship.InputController.RightNormalizedJoystickPosition.x);
                i++;

                child.transform.localPosition = Vector3.Slerp(child.transform.localPosition,radius * new Vector3(Mathf.Sin(j), Mathf.Cos(j), 0),Time.deltaTime*3);
                gunFocus.localPosition = Vector3.Lerp(gunFocus.localPosition, new Vector3(0,0, 300*ship.InputController.RightNormalizedJoystickPosition.SqrMagnitude()+70),Time.deltaTime);
                child.LookAt(gunFocus);
            }
        }
    }
}
