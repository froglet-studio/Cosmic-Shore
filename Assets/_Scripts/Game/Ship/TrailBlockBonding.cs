using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class TrailBlockBonding : MonoBehaviour
    {
        private Vector3 scale; // Scale of the TrailBlock
        private Vector3 BondSiteTop;
        private Vector3 BondSiteRight;
        private Vector3 BondSiteBottom;
        private Vector3 BondSiteLeft;


        private Vector3 globalBondSiteTop; // Global position of Bond Site A
        private Vector3 globalBondSiteRight; // Global position of Bond Site B
        private Vector3 globalBondSiteBottom;
        private Vector3 globalBondSiteLeft; 

        private TrailBlockBonding closestMate;
        private bool isBonded = false;
        private float snapDistance = 0.1f;
        float separationDistance = 1f;// Threshold distance to snap into position

        void Start()
        {
            scale = GetComponent<TrailBlock>().TargetScale;
            CalculateBondSites();
            CalculateGlobalBondSites();
        }

        void FixedUpdate()
        {
            if (!isBonded)
            {
                closestMate = FindClosestMate();
                if (closestMate != null)
                {
                    //MoveTowardsMate(closestMate);
                    //RotateTowardsMate(closestMate);
                    CalculateGlobalBondSites(); // Update global bond sites after movement and rotation

                    if (IsCloseEnoughToSnap(closestMate))
                    {
                        //SnapToPosition(closestMate);
                        isBonded = true;
                        CalculateGlobalBondSites(); // Update again after snapping
                    }
                }
            }
        }

        // Rest of the script remains the same

        void CalculateBondSites()
        {
            // Using the bond site calculations from WallAssembler
            BondSiteTop = ((scale.y/2) + separationDistance) * transform.up;
            BondSiteRight = ((scale.y / 2) - (scale.x/2)) * transform.up + (((scale.x/2) + separationDistance) * transform.right);
            BondSiteBottom = -((scale.y / 2) + separationDistance) * transform.up;
            BondSiteLeft = -((scale.y / 2) - (scale.x / 2)) * transform.up - (((scale.x/2) + separationDistance) * transform.right);
            // If you need BondSiteC, add its calculation here
        }
        void CalculateGlobalBondSites()
        {
            globalBondSiteTop = transform.position + transform.TransformDirection(BondSiteTop);
            globalBondSiteRight = transform.position + transform.TransformDirection(BondSiteRight);
            globalBondSiteBottom = transform.position + transform.TransformDirection(BondSiteBottom);
            globalBondSiteLeft = transform.position + transform.TransformDirection(BondSiteLeft);
        }

        private TrailBlockBonding FindClosestMate()
        {
            float closestDistance = float.MaxValue;
            TrailBlockBonding closest = null;

            foreach (var potentialMate in Physics.OverlapSphere(globalBondSiteTop, 10)) // Adjust radius as needed
            {
                var mateComponent = potentialMate.GetComponent<TrailBlockBonding>();
                if (mateComponent != null && mateComponent != this && !mateComponent.isBonded)
                {
                    float distance = Vector3.Distance(globalBondSiteTop, mateComponent.globalBondSiteRight);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closest = mateComponent;
                    }
                }
            }
            return closest;
        }

        private void MoveTowardsMate(TrailBlockBonding mate)
        {
            Vector3 directionToMate = mate.globalBondSiteRight - globalBondSiteTop;
            transform.position += directionToMate.normalized * Time.deltaTime; // Adjust speed as needed
        }

        private void RotateTowardsMate(TrailBlockBonding mate)
        {
            Vector3 directionToMate = mate.globalBondSiteRight - globalBondSiteTop;
            Quaternion targetRotation = Quaternion.LookRotation(directionToMate);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime); // Adjust rotation speed as needed
        }

        private bool IsCloseEnoughToSnap(TrailBlockBonding mate)
        {
            return Vector3.Distance(globalBondSiteTop, mate.globalBondSiteRight) < snapDistance;
        }

        private void SnapToPosition(TrailBlockBonding mate)
        {
            // Ensure directionToMate is not a zero vector
            Vector3 directionToMate = mate.globalBondSiteRight - globalBondSiteTop;
            if (directionToMate != Vector3.zero)
            {
                // Correctly position the block taking into account the local offset and separation
                transform.position = mate.globalBondSiteRight - transform.TransformDirection(BondSiteTop);
                transform.rotation = Quaternion.LookRotation(directionToMate);

                // Update the global positions of the bond sites
                CalculateGlobalBondSites();
            }
        }


        void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(globalBondSiteTop, 0.1f); // Adjust size as needed
            Gizmos.DrawSphere(globalBondSiteBottom, 0.1f); // Adjust size as needed
            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(globalBondSiteRight, 0.1f);
            Gizmos.DrawSphere(globalBondSiteLeft, 0.1f); // Adjust size as needed
        }

    }
}