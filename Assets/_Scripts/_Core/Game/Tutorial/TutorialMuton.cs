using System.Collections.Generic;
using UnityEngine;
using StarWriter.Core.Input;
using StarWriter.Core.Audio;

namespace StarWriter.Core.Tutorial 
{
    public class TutorialMuton : MonoBehaviour
    {
        float lastCollisionTime;
        float collisionCoolOff = 2f;

        [SerializeField]
        GameObject spentMutonPrefab;

        [SerializeField]
        Material material;
        Material tempMaterial;

        ShipData shipData;


        public delegate void OnTutorialMutonCollisionEvent();
        public static event OnTutorialMutonCollisionEvent onMutonCollision;

        /// <summary>
        /// Generate a list of Box Collider Collision so the specific ones can be identified for use
        /// </summary>
        /// <param name="collision"></param>
        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time > lastCollisionTime + collisionCoolOff)
            {
                lastCollisionTime = Time.time;
                onMutonCollision?.Invoke();
                // make an exploding muton
                var spentMuton = Instantiate<GameObject>(spentMutonPrefab);
                spentMuton.transform.position = transform.position;
                spentMuton.transform.localEulerAngles = transform.localEulerAngles;
                tempMaterial = new Material(material);
                spentMuton.GetComponent<Renderer>().material = tempMaterial;

                GameObject ship = GameObject.FindWithTag("Player");

                //muton animation and haptics
                StartCoroutine(spentMuton.GetComponent<Impact>().ImpactCoroutine(
                    ship.transform.forward * ship.GetComponent<InputController>().speed, tempMaterial, "Player"));
                HapticController.PlayMutonCollisionHaptics();
                
            }
        }

        /// <summary>
        /// Moves the Muton to an offset in front of the Tutorial Player
        /// </summary>
        public void MoveMuton(Transform playerTransform, Vector3 spawnPointOffset)
        {
            transform.position = playerTransform.position +
                                 playerTransform.right * spawnPointOffset.x +
                                 playerTransform.up * spawnPointOffset.y +
                                 playerTransform.forward * spawnPointOffset.z;
        }
    }
}