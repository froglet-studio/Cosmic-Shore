using CosmicShore.Core;
using CosmicShore.Game;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore
{
    public class GunRingTransformer : MonoBehaviour
    {
        [SerializeField] MonoBehaviour shipInstance;
        [SerializeField] Transform gunFocus;
        [SerializeField] GameObject pivotObject;

        [SerializeField] private float radius = 20.0f;
        [SerializeField] private float rotationSpeed = 20.0f;
        [SerializeField] private float speed = 10.0f;
        
       void Start()
        {
            foreach (var child in GetComponentsInChildren<Transform>())
            {

                if (child == transform) continue; // skip the parent itself

                // Get direction from origin to child
                Vector3 direction = (child.position - shipInstance.transform.position).normalized;

                // Move outward equally along that direction
                child.position = shipInstance.transform.position + direction * radius;


            }
        }

        
        void Update()
        {
            //This very hacky and probally will get removed when brittlestar become more finialized
            Vector2 rightStick = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;

            foreach (var child in GetComponentsInChildren<Transform>())
            {
                child.transform.RotateAround(pivotObject.transform.position, pivotObject.transform.forward, rotationSpeed * Time.deltaTime);
                Vector3 targetFocus = new Vector3(0, 0, 300f * rightStick.sqrMagnitude + 70f);
                gunFocus.localPosition = Vector3.Lerp(gunFocus.localPosition, targetFocus, Time.deltaTime * speed);
                child.LookAt(gunFocus);
            }

            }
    }
}
