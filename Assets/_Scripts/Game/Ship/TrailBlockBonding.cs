using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CosmicShore
{
    public struct TrailBlockBondingAndIsRight
    {
        public TrailBlockBonding trailBlockBonding;
        public bool isRight;
    }

    public class TrailBlockBonding : MonoBehaviour
    {
        public bool isActive = false;
        private Vector3 scale; // Scale of the TrailBlock
        private Vector3 BondSiteTop;
        private Vector3 BondSiteRight;
        private Vector3 BondSiteBottom;
        private Vector3 BondSiteLeft;


        private Vector3 globalBondSiteTop; // Global position of Bond Site A
        private Vector3 globalBondSiteRight; // Global position of Bond Site B
        private Vector3 globalBondSiteBottom;
        private Vector3 globalBondSiteLeft; 

        public TrailBlockBondingAndIsRight TopMate;
        public TrailBlockBonding RightMate;
        public TrailBlockBondingAndIsRight BottomMate;
        public TrailBlockBonding LeftMate;

        public bool TopIsBonded = false;
        public bool RightIsBonded = false;         
        public bool BottomIsBonded = false;
        public bool LeftIsBonded = false;

        public HashSet<TrailBlockBonding> MateList = new(); 

        private float snapDistance = 2f;
        float separationDistance = 6f;// Threshold distance to snap into position

        void Start()
        {
            TrailBlock trailBlock = GetComponent<TrailBlock>();
            if (trailBlock.Trail.TrailList.Count%10 == 0) isActive = true;
            scale = trailBlock.TargetScale;
            CalculateBondSites();
            CalculateGlobalBondSites();
        }

        void FixedUpdate()
        {
            if (!isActive) return;
            if (TopMate.trailBlockBonding == null)
            {
                TopMate = FindClosestMate(globalBondSiteTop); 
                
                if (TopMate.trailBlockBonding)
                {
                    if (TopMate.isRight) TopMate.trailBlockBonding.RightMate = this;
                    else TopMate.trailBlockBonding.LeftMate = this;
                    MateList.Add(TopMate.trailBlockBonding);
                    TopMate.trailBlockBonding.MateList.Add(this);
                }


                //if (TopMate.trailBlockBonding != null)
                //{
                //    TopIsBonded = true;

                //    //if (IsCloseEnoughToSnap(closestMate))
                //    //{
                //    //    SnapToPosition(closestMate);
                //    //    topIsBonded = true;
                //    //    CalculateGlobalBondSites(); // Update again after snapping
                //    //}
                //}
            }
            else
            {
                MoveTowardsMate(TopMate, globalBondSiteTop);
                RotateTowardsMate(TopMate);
                CalculateGlobalBondSites();
            }

            if (BottomMate.trailBlockBonding == null)
            {
                BottomMate = FindClosestMate(globalBondSiteBottom);
                if (BottomMate.trailBlockBonding)
                {
                    if (BottomMate.isRight) BottomMate.trailBlockBonding.RightMate = this;
                    else BottomMate.trailBlockBonding.LeftMate = this;
                    MateList.Add(BottomMate.trailBlockBonding);
                    BottomMate.trailBlockBonding.MateList.Add(this);
                }
            }
            else
            {
                MoveTowardsMate(BottomMate, globalBondSiteBottom);
                RotateTowardsMate(BottomMate);
                CalculateGlobalBondSites();
            }
        }

        // Rest of the script remains the same

        void CalculateBondSites()
        {
            // Using the bond site calculations from WallAssembler
            BondSiteTop = ((scale.y/2) + separationDistance) * transform.up;
            BondSiteRight = ((scale.y / 2) - (scale.x/2)) * transform.up + (((scale.x/2) + separationDistance) * transform.right);
            BondSiteBottom = -((scale.y / 2) + separationDistance) * transform.up;
            BondSiteLeft = -(((scale.y / 2) - (scale.x / 2)) * transform.up) - ((scale.x/2) + separationDistance) * transform.right;
        }
        void CalculateGlobalBondSites()
        {
            globalBondSiteTop = transform.position + transform.TransformDirection(BondSiteTop);
            globalBondSiteRight = transform.position + transform.TransformDirection(BondSiteRight);
            globalBondSiteBottom = transform.position + transform.TransformDirection(BondSiteBottom);
            globalBondSiteLeft = transform.position + transform.TransformDirection(BondSiteLeft);
        }


        // this method so if checks if this is in each struct in the list
        private bool IsMate(TrailBlockBonding mateComponent)
        {
            return mateComponent.MateList.Count > 0;
        }


        // this method generalize both of the methods above
        private TrailBlockBondingAndIsRight FindClosestMate(Vector3 bondSite)
        {
            float closestDistance = float.MaxValue;
            TrailBlockBonding closest = null;
            bool isRight = false;
            var colliders = Physics.OverlapSphere(bondSite, 20); // Adjust radius as needed
            if (colliders.Length < 50) return new TrailBlockBondingAndIsRight { trailBlockBonding = null, isRight = false };
            foreach (var potentialMate in colliders) // Adjust radius as needed
            {
                var mateComponent = potentialMate.GetComponent<TrailBlockBonding>();

                if (mateComponent != null && mateComponent != this && !IsMate(mateComponent))
                {
                    if (!mateComponent.RightIsBonded)
                    {
                        float distance = Vector3.Distance(bondSite, mateComponent.globalBondSiteRight);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closest = mateComponent;
                            isRight = true;
                        }
                    }
                    if (!mateComponent.LeftIsBonded)
                    {
                        float distance = Vector3.Distance(bondSite, mateComponent.globalBondSiteLeft);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            closest = mateComponent;
                            isRight = false;
                        }
                    }
                }
            }
            if (closest)
            {
                if (!closest.RightIsBonded) closest.RightIsBonded = isRight;
                if (!closest.LeftIsBonded) closest.LeftIsBonded = !isRight;
            }
            // this returns a TrailBlockBondingAndIsRight with the closest and the isRight bool
            return new TrailBlockBondingAndIsRight { trailBlockBonding = closest, isRight = isRight };
        }

        // this method returns a bool that is true if TopMate is not null and is equal to the mateComponent
        private bool IsTopMate(TrailBlockBonding mateComponent)
        {
            //Debug.Log($"TrailBlockBonding: TopMate {TopMate.trailBlockBonding != null && TopMate.trailBlockBonding.Equals(mateComponent)}");
            return TopMate.trailBlockBonding != null && TopMate.trailBlockBonding.Equals(mateComponent); 
        }

        // now for bottom mate
        private bool IsBottomMate(TrailBlockBonding mateComponent)
        {
            //Debug.Log($"TrailBlockBonding: BottomMate {BottomMate.trailBlockBonding != null && BottomMate.trailBlockBonding.Equals(mateComponent)}");
            return BottomMate.trailBlockBonding != null && BottomMate.trailBlockBonding.Equals(mateComponent);
        }

        // this method is a generalized version of MoveTowardsMate()
        private void MoveTowardsMate(TrailBlockBondingAndIsRight mate, Vector3 bondSite)
        {
            Vector3 directionToMate;
            if (mate.isRight) directionToMate = mate.trailBlockBonding.globalBondSiteRight - bondSite;
            else directionToMate = mate.trailBlockBonding.globalBondSiteLeft - bondSite;
            if (directionToMate.sqrMagnitude < snapDistance)
            {
                isActive = false;
                mate.trailBlockBonding.isActive = false;

            }
            //transform.position += directionToMate * Time.deltaTime; // Adjust speed as needed
            mate.trailBlockBonding.transform.position -= directionToMate * Time.deltaTime; // Adjust speed as needed
            mate.trailBlockBonding.isActive = true;                                                                               
        }

        private void RotateTowardsMate(TrailBlockBondingAndIsRight mate)
        {
            int signRight = mate.isRight ? -1 : 1;
            int signTop = IsTopMate(mate.trailBlockBonding) ? 1 : -1;
            Quaternion targetRotation = Quaternion.LookRotation(mate.trailBlockBonding.transform.forward, signRight * signTop * mate.trailBlockBonding.transform.right);
            //var angle = Quaternion.Angle(transform.rotation, targetRotation);
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