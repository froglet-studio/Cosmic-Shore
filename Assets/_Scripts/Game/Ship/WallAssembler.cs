using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public struct BondMate
    {
        public WallAssembler Mate;
        public SiteType Substrate;
        public SiteType Bondee;
    }

    public enum SiteType
    {
        Top,
        Right,
        Bottom,
        Left
    }

    public class WallAssembler : MonoBehaviour
    {
        int wallDepth = 0;
        public bool IsActive = false;
        private Vector3 scale; // Scale of the TrailBlock
        private Vector3 BondSiteTop;
        private Vector3 BondSiteRight;
        private Vector3 BondSiteBottom;
        private Vector3 BondSiteLeft;

        private Vector3 globalBondSiteTop; // Global position of Bond Site A
        private Vector3 globalBondSiteRight; // Global position of Bond Site B
        private Vector3 globalBondSiteBottom;
        private Vector3 globalBondSiteLeft;

        public BondMate TopMate;
        public WallAssembler RightMate;
        public BondMate BottomMate;
        public WallAssembler LeftMate;

        public bool TopIsBonded = false;
        public bool RightIsBonded = false;
        public bool BottomIsBonded = false;
        public bool LeftIsBonded = false;

        public HashSet<WallAssembler> MateList = new();

        private float snapDistance = .1f;
        float separationDistance = 1f;// Threshold distance to snap into position

        void Start()
        {
            TrailBlock trailBlock = GetComponent<TrailBlock>();
            if (trailBlock.Trail.TrailList.Count % 10 == 0) IsActive = true;
            scale = trailBlock.TargetScale;
            CalculateBondSites();
            CalculateGlobalBondSites();
        }

        void FixedUpdate()
        {
            if (!IsActive) return;
            if (TopMate.Mate == null)
            {
                TopMate = FindClosestMate(globalBondSiteTop, SiteType.Top);

                if (TopMate.Mate)
                {
                    TopMate.Mate.RightMate = this;
                    MateList.Add(TopMate.Mate);
                    TopMate.Mate.MateList.Add(this);
                }
            }
            else
            {
                RotateMate(TopMate);
                MoveMateToSite(TopMate, globalBondSiteTop);
            }

            if (BottomMate.Mate == null)
            {
                BottomMate = FindClosestMate(globalBondSiteBottom, SiteType.Bottom);
                if (BottomMate.Mate)
                {
                    BottomMate.Mate.LeftMate = this;
                    MateList.Add(BottomMate.Mate);
                    BottomMate.Mate.MateList.Add(this);
                }
            }
            else
            {
                RotateMate(BottomMate);
                MoveMateToSite(BottomMate, globalBondSiteBottom);
            }

            if (TopIsBonded && BottomIsBonded)
            {
                Debug.Log("Bonded Top and Bottom");
                IsActive = false;
                TopMate.Mate.IsActive = true;
                BottomMate.Mate.IsActive = true;
                TopMate.Mate.wallDepth = wallDepth + 1;
                BottomMate.Mate.wallDepth = wallDepth + 1;
            }
        }

        // Rest of the script remains the same

        void CalculateBondSites()
        {
            // Using the bond site calculations from WallAssembler
            BondSiteTop = ((scale.y / 2) + separationDistance) * transform.up;
            BondSiteRight = ((scale.y / 2) - (scale.x / 2)) * transform.up + (((scale.x / 2) + separationDistance) * transform.right);
            BondSiteBottom = -((scale.y / 2) + separationDistance) * transform.up;
            BondSiteLeft = -(((scale.y / 2) - (scale.x / 2)) * transform.up) - ((scale.x / 2) + separationDistance) * transform.right;
        }
        public void CalculateGlobalBondSites()
        {
            globalBondSiteTop = transform.position + transform.TransformDirection(BondSiteTop);
            globalBondSiteRight = transform.position + transform.TransformDirection(BondSiteRight);
            globalBondSiteBottom = transform.position + transform.TransformDirection(BondSiteBottom);
            globalBondSiteLeft = transform.position + transform.TransformDirection(BondSiteLeft);
        }


        // this method so if checks if this is in each struct in the list
        private bool IsMate(WallAssembler mateComponent)
        {
            return mateComponent.MateList == null ? false : mateComponent.MateList.Count > 0;
        }

        // this method generalize both of the methods above
        private BondMate FindClosestMate(Vector3 bondSite, SiteType siteType)
        {
            float closestDistance = float.MaxValue;
            WallAssembler closest = null;
            SiteType bondee = SiteType.Right;
            var colliders = Physics.OverlapSphere(bondSite, 20); // Adjust radius as needed
            if (colliders.Length < 50) return new BondMate { Mate = null};
            foreach (var potentialMate in colliders) // Adjust radius as needed
            {
                var mateComponent = potentialMate.GetComponent<WallAssembler>();
                if (mateComponent.wallDepth > wallDepth)
                {
                    IsActive = false;
                    return new BondMate { Mate = null };
                }
                if (mateComponent != null && IsMate(mateComponent) && mateComponent != this)
                {
                    if (Vector3.Distance(bondSite, mateComponent.globalBondSiteRight) < snapDistance)
                    {
                        Debug.Log("ReFound MateRight");
                        return new BondMate { Mate = mateComponent, Substrate = siteType, Bondee = SiteType.Right};
                    }
                    if (Vector3.Distance(bondSite, mateComponent.globalBondSiteLeft) < snapDistance)
                    {
                        Debug.Log("ReFound MateLeft");
                        return new BondMate { Mate = mateComponent, Substrate = siteType, Bondee = SiteType.Left };
                    }
                }
                else if (mateComponent != null && mateComponent != this)
                {
                    if (siteType == SiteType.Top)
                    {
                        float distance = Vector3.Distance(bondSite, mateComponent.globalBondSiteRight);
                        if (distance < closestDistance)
                        {
                            Debug.Log("Found MateRight");
                            closestDistance = distance;
                            closest = mateComponent;
                            bondee = SiteType.Right;
                        }
                    }
                    else if (siteType == SiteType.Bottom)
                    {
                        float distance = Vector3.Distance(bondSite, mateComponent.globalBondSiteLeft);
                        if (distance < closestDistance)
                        {
                            Debug.Log("Found MateLeft");
                            closestDistance = distance;
                            closest = mateComponent;
                            bondee = SiteType.Left;
                        }
                    }
                }
            }
            //if (closest)
            //{
            //    if (!closest.RightIsBonded) closest.RightIsBonded = isRight;
            //    if (!closest.LeftIsBonded) closest.LeftIsBonded = !isRight;
            //}
            // this returns a TrailBlockBondingAndIsRight with the closest and the isRight bool
            return new BondMate { Mate = closest, Substrate = siteType,  Bondee = bondee };
        }

        // this method is a generalized version of MoveTowardsMate()
        private void MoveMateToSite(BondMate mate, Vector3 bondSite)
        {
            var initialPosition = mate.Bondee == SiteType.Right ? mate.Mate.globalBondSiteRight : mate.Mate.globalBondSiteLeft;
            var directionToMate = bondSite - initialPosition;
            if (directionToMate.sqrMagnitude < snapDistance)
            {
                Debug.Log("Snapped");
                //mate.Mate.IsActive = true;
                if (mate.Substrate == SiteType.Top) TopIsBonded = true;
                else if (mate.Substrate == SiteType.Bottom) BottomIsBonded = true;
            }
            mate.Mate.transform.position += directionToMate * Time.deltaTime;
            mate.Mate.CalculateGlobalBondSites();
            CalculateGlobalBondSites();
        }

        private void RotateMate(BondMate mate)
        {
            int signRight = mate.Bondee == SiteType.Right ? 1 : -1;
            int signTop = mate.Substrate == SiteType.Top ? 1 : -1;
            Quaternion targetRotation = Quaternion.LookRotation(transform.forward, signRight * signTop * transform.right);
            //var angle = Quaternion.Angle(transform.rotation, targetRotation);
            mate.Mate.transform.rotation = Quaternion.Slerp(mate.Mate.transform.rotation, targetRotation, Time.deltaTime); // Adjust rotation speed as needed
            mate.Mate.CalculateGlobalBondSites();
            CalculateGlobalBondSites();
        }

        private bool IsCloseEnoughToSnap(WallAssembler mate)
        {
            return Vector3.Distance(globalBondSiteTop, mate.globalBondSiteRight) < snapDistance;
        }

        private void SnapToPosition(WallAssembler mate)
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
    }
}
