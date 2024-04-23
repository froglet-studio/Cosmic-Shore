using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class Worm : MonoBehaviour
    {
        public GameObject target;
        public float speed = 5f;
        public float rotationSpeed = 5f;
        public float whipAmplitude = 30f;
        public float whipDuration = 0.2f;
        public float dampingFactor = 0.01f;

        [SerializeField] GameObject rootBone;

        private HashSet<Transform> segments = new HashSet<Transform>();
        private List<Vector3> initialPositions = new List<Vector3>();
        private List<Quaternion> initialOrientations = new List<Quaternion>();

        [SerializeField] List<TrailBlock> shieldedBlocks;
        [SerializeField] List<TrailBlock> dangerBlocks;

        private void Start()
        {
            // Recursively traverse the hierarchy and populate the segments list
            TraverseHierarchy(rootBone.transform);

            // Save the initial positions of the segments
            SaveInitialOrientations();

            for (int i = 0; i < shieldedBlocks.Count; i++)
            {
                shieldedBlocks[i].Team = Teams.Gold;
                
            }

            for (int i = 0; i < dangerBlocks.Count; i++)
            {
                dangerBlocks[i].Team = Teams.Red;
            }

        }

        private void TraverseHierarchy(Transform parent)
        {

            foreach (Transform child in parent)
            {
                segments.Add(child.transform.parent);
                TraverseHierarchy(child);
            }
        }

        private void SaveInitialOrientations()
        {
            foreach (Transform segment in segments)
            {
                initialOrientations.Add(segment.localRotation);
            }
        }

        private void Update()
        {
            // Move the head towards the target
            Vector3 direction = (target.transform.position - transform.position).normalized;
            transform.position = Vector3.Lerp(transform.position, target.transform.position, speed * Time.deltaTime);

            // Rotate the head towards the target
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            var newRotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            //Update the tail segments
            foreach (Transform segment in segments)
            {
                segment.localRotation = newRotation;
            }

            transform.rotation = newRotation;
        }

    }
}