using CosmicShore.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace CosmicShore
{

    public enum GyroidBlockType
    {
        AB = 1,
        BC = 2,
        CD = 3,
        DE = 4,
        EF = 5,
        EG = 6,
        BA = 7,
        AF = 8,
        FG = 9,
        GEs = 10,
        EsC = 11,
        EsD = 12,
    }

    public class GyroidAssembler : MonoBehaviour
    {
        

        //public bool IsActive = false;
        private Vector3 scale; // Scale of the TrailBlock

        private Vector3 globalBondSiteTopLeft;// Global position of Bond Site A
        private Vector3 globalBondSiteTopRight;// Global position of Bond Site B
        private Vector3 globalBondSiteBottomLeft;
        private Vector3 globalBondSiteBottomRight;

        [HideInInspector] public GyroidBondMate TopLeftMate;
        [HideInInspector] public GyroidBondMate TopRightMate;
        [HideInInspector] public GyroidBondMate BottomLeftMate;
        [HideInInspector] public GyroidBondMate BottomRightMate;

        [HideInInspector] public bool TopLeftIsBonded = false;
        [HideInInspector] public bool TopRightIsBonded = false;
        [HideInInspector] public bool BottomLeftIsBonded = false;
        [HideInInspector] public bool BottomRightIsBonded = false;

        [HideInInspector] public HashSet<GyroidAssembler> MateList = new();
        [HideInInspector] public GyroidAssembler preferedBlock;

        public TrailBlock GyroidBlock;
        public GyroidBlockType BlockType = GyroidBlockType.BC;

        private float snapDistance = .3f;
        float separationDistance = 3f;
        [SerializeField] int colliderTheshold = 1;
        [SerializeField] float radius = 40f;

        void Start()
        {
            GyroidBlock = GetComponent<TrailBlock>();
            scale = GyroidBlock.TargetScale;
        }

        public void StartBonding()
        {
            StartCoroutine(LookForMates());
        }

        public void StartBonding(int depth)
        {
            StartCoroutine(LookForMates(depth));
        }

        public void AcceptBond()
        {
            GyroidBlock.ActivateSuperShield();
            GyroidBlock.Grow();
        }

        public void ClearMateList()
        {
            foreach (var mate in MateList)
            {
                mate.MateList.Remove(this);
            }
            MateList.Clear();
            TopLeftIsBonded = false;
            TopRightIsBonded = false;
            BottomLeftIsBonded = false;
            BottomRightIsBonded = false;
        }

        public GyroidBondMate CreateGyroidBondMate(GyroidAssembler mate, GyroidBlockType blockType, CornerSiteType siteType)
        {
            if (GyroidBondMateDataContainer.BondMateDataMap.TryGetValue((blockType, siteType), out var bondMateData))
            {
                return new GyroidBondMate
                {
                    Mate = mate, // This will be set at runtime
                    Substrate = bondMateData.Substrate,
                    Bondee = bondMateData.Bondee,
                    DeltaPosition = bondMateData.DeltaPosition,
                    DeltaUp = bondMateData.DeltaUp,
                    DeltaForward = bondMateData.DeltaForward,
                    BlockType = bondMateData.BlockType,
                    isTail = bondMateData.isTail
                };
            }

            // throw error if data is not found
            throw new System.Exception($"GyroidBondMateData not found for blockType: {blockType} and siteType: {siteType}");
        }

        void PrepareMate(GyroidBondMate gyroidBondMate)
        {
            if (gyroidBondMate.Mate)
            {
                gyroidBondMate.Mate.BlockType = gyroidBondMate.BlockType;

                switch (gyroidBondMate.Bondee)
                {
                    case CornerSiteType.TopLeft:
                        gyroidBondMate.Mate.TopLeftMate = CreateGyroidBondMate(this, gyroidBondMate.BlockType, CornerSiteType.TopLeft);
                        break;
                    case CornerSiteType.TopRight:
                        gyroidBondMate.Mate.TopRightMate = CreateGyroidBondMate(this, gyroidBondMate.BlockType, CornerSiteType.TopRight);
                        break;
                    case CornerSiteType.BottomLeft:
                        gyroidBondMate.Mate.BottomLeftMate = CreateGyroidBondMate(this, gyroidBondMate.BlockType, CornerSiteType.BottomLeft);
                        break;
                    case CornerSiteType.BottomRight:
                        gyroidBondMate.Mate.BottomRightMate = CreateGyroidBondMate(this, gyroidBondMate.BlockType, CornerSiteType.BottomRight);
                        break;
                }
                MateList.Add(gyroidBondMate.Mate);
                gyroidBondMate.Mate.MateList.Add(this);
                Coroutine coroutine = StartCoroutine(UpdateMate(gyroidBondMate));
                updateCoroutineDict[gyroidBondMate] = coroutine;
            }
        }

        private bool AreAllActiveMatesBonded(bool[] activeMates)
        {
            // Checking each mate's bonded status if they are active
            if (activeMates[0] && !TopLeftIsBonded) return false;     
            if (activeMates[1] && !TopRightIsBonded) return false;    
            if (activeMates[2] && !BottomLeftIsBonded) return false;  
            if (activeMates[3] && !BottomRightIsBonded) return false; 

            return true; // All active mates are bonded
        }


        void UpdateBondingStatus(BondMate mate, bool isBonded, ref int depth)
        {
            if (isBonded && mate.Mate.MateList.Count < 2)
            {
                mate.Mate.StartBonding(--depth);
                mate.Mate.gameObject.layer = LayerMask.NameToLayer("Mound");
            }
        }


        IEnumerator LookForMates(int depth = -1)
        {


            bool[] activeMates = new bool[]               { false, true, true, false };
            if (depth == 0) StopAllCoroutines();
            //else if (depth == 1) activeMates = new bool[] { false, true, true, false };
            //else if (depth == 2) activeMates = new bool[] { true, true, true, true };
            //else if (depth == 3) activeMates = new bool[] { true, true, true, true };

            while (true)
            {
                if (GyroidBlock == null)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                yield return new WaitForSeconds(1f);
                // TopLeftMate
                if (activeMates[0] && TopLeftMate.Mate == null)
                {
                    TopLeftMate = FindClosestMate(CalculateGlobalBondSite(CornerSiteType.TopLeft), CornerSiteType.TopLeft);
                    PrepareMate(TopLeftMate);
                }

                // TopRightMate
                if (activeMates[1] && TopRightMate.Mate == null)
                {
                    TopRightMate = FindClosestMate(CalculateGlobalBondSite(CornerSiteType.TopRight), CornerSiteType.TopRight);
                    PrepareMate(TopRightMate);
                }

                // BottomLeftMate
                if (activeMates[2] && BottomLeftMate.Mate == null)
                {
                    BottomLeftMate = FindClosestMate(CalculateGlobalBondSite(CornerSiteType.BottomLeft), CornerSiteType.BottomLeft);
                    PrepareMate(BottomLeftMate);
                }

                // BottomRightMate
                if (activeMates[3] && BottomRightMate.Mate == null)
                {
                    BottomRightMate = FindClosestMate(CalculateGlobalBondSite(CornerSiteType.BottomRight), CornerSiteType.BottomRight);
                    PrepareMate(BottomRightMate);
                }
                yield return new WaitForSeconds(1f);

                
                UpdateBondingStatus(TopLeftMate, TopLeftIsBonded, ref depth);
                UpdateBondingStatus(TopRightMate, TopRightIsBonded, ref depth);
                UpdateBondingStatus(BottomLeftMate, BottomLeftIsBonded, ref depth);
                UpdateBondingStatus(BottomRightMate, BottomRightIsBonded, ref depth);

                if (AreAllActiveMatesBonded(activeMates))
                {
                    StopAllCoroutines();
                    GyroidBlock.Grow();
                }
            }
        }

        Dictionary<GyroidBondMate, Coroutine> updateCoroutineDict = new Dictionary<GyroidBondMate, Coroutine>();

        IEnumerator UpdateMate(GyroidBondMate gyroidBondMate)
        {
            while (true)
            {
                yield return null;
                if (gyroidBondMate.Mate != null)
                {
                    MoveMateToSite(gyroidBondMate, CalculateGlobalBondSite(gyroidBondMate.Substrate));
                    RotateMate(gyroidBondMate, false);
                }
            }
        }

        // Helper method to calculate local bond site
        Vector3 CalculateBondSite(CornerSiteType site)
        {
            return GyroidBondMateDataContainer.GetBondMateData(BlockType, site).DeltaPosition * separationDistance;
        }

        // Helper method to convert local position to global position
        private Vector3 CalculateGlobalPosition(Vector3 localPosition)
        {
            return localPosition.x * transform.right + localPosition.y * transform.up + localPosition.z * transform.forward + transform.position;
        }

        // Method with switch case to update and return a specific global bond site
        public Vector3 CalculateGlobalBondSite(CornerSiteType site)
        {
            Vector3 localBondSite = CalculateBondSite(site);
            Vector3 globalBondSite;

            switch (site)
            {
                case CornerSiteType.TopLeft:
                    globalBondSiteTopLeft = CalculateGlobalPosition(localBondSite);
                    globalBondSite = globalBondSiteTopLeft;
                    break;

                case CornerSiteType.TopRight:
                    globalBondSiteTopRight = CalculateGlobalPosition(localBondSite);
                    globalBondSite = globalBondSiteTopRight;
                    break;

                case CornerSiteType.BottomLeft:
                    globalBondSiteBottomLeft = CalculateGlobalPosition(localBondSite);
                    globalBondSite = globalBondSiteBottomLeft;
                    break;

                case CornerSiteType.BottomRight:
                    globalBondSiteBottomRight = CalculateGlobalPosition(localBondSite);
                    globalBondSite = globalBondSiteBottomRight;
                    break;

                default:
                    throw new ArgumentException("Invalid corner site type");
            }

            return globalBondSite;
        }

        // this method so if checks if this is in each struct in the list
        private bool IsMate(GyroidAssembler mateComponent)
        {
            return mateComponent.MateList == null ? false : mateComponent.MateList.Count > 0;
        }

        // this method generalize both of the methods above
        private GyroidBondMate FindClosestMate(Vector3 bondSite, CornerSiteType siteType)
        {
            if (preferedBlock)
            {
                var Mate = CreateGyroidBondMate(preferedBlock, BlockType, siteType); 
                preferedBlock = null;
                return Mate;
            }

                float closestDistance = float.MaxValue;
            GyroidAssembler closest = null;
            bool isTail = GyroidBondMateDataContainer.GetBondMateData(BlockType, siteType).isTail;
            CornerSiteType bondee = CornerSiteType.TopRight;
            var colliders = Physics.OverlapSphere(bondSite, radius); // Adjust radius as needed
            if (colliders.Length < colliderTheshold)
            {
                return new GyroidBondMate { Mate = null };
            }
            foreach (var potentialMate in colliders) // Adjust radius as needed
            {
                GyroidAssembler mateComponent = potentialMate.GetComponent<GyroidAssembler>();
                if (mateComponent == null)
                {
                    var trailBlock = potentialMate.GetComponent<TrailBlock>();
                    if (trailBlock != null)
                    {
                        Boid boid = trailBlock.GetComponentInParent<Boid>();
                        if (boid != null)
                        {
                            trailBlock.transform.parent = GyroidBlock.transform.parent;
                            boid.isKilled = true;
                        }
                        trailBlock.TargetScale = scale;
                        trailBlock.maxScale = GyroidBlock.maxScale;
                        trailBlock.growthVector = GyroidBlock.growthVector;
                        trailBlock.Steal(GyroidBlock.Player, GyroidBlock.Team);
                        trailBlock.ChangeSize();
                        mateComponent = trailBlock.transform.gameObject.AddComponent<GyroidAssembler>();
                        mateComponent.GyroidBlock = trailBlock;
                    }
                    else continue;
                }

                float sqrDistance = (bondSite - mateComponent.transform.position).sqrMagnitude;

                if (IsMate(mateComponent) && mateComponent != this)
                {
                    
                    if (Vector3.SqrMagnitude(transform.position - mateComponent.transform.position) < snapDistance  //block younger and in this block's position then clear its mate list
                        && mateComponent.GyroidBlock.TrailBlockProperties.TimeCreated > GyroidBlock.TrailBlockProperties.TimeCreated)
                    {
                        mateComponent.StopAllCoroutines();
                        mateComponent.ClearMateList();
                    }
                    if (sqrDistance < snapDistance) // if block is  already  in position supershield it.
                    {
                        mateComponent.GyroidBlock.ActivateSuperShield();
                        return CreateGyroidBondMate(mateComponent, BlockType, siteType);
                    }
                }
                if (!IsMate(mateComponent) && mateComponent != this)
                {
                    if (sqrDistance < closestDistance)
                    {
                        closestDistance = sqrDistance;
                        closest = mateComponent;
                        bondee = isTail ? CornerSiteType.BottomRight : CornerSiteType.TopLeft;
                    }
                }
            }
            return CreateGyroidBondMate(closest, BlockType, siteType);
        }

        private void MoveMateToSite(GyroidBondMate mate, Vector3 bondSite)
        {
            {
                var initialPosition = mate.Mate.transform.position;
                var directionToMate = bondSite - initialPosition;
                
                if (directionToMate.sqrMagnitude < snapDistance)
                {
                    RotateMate(mate, true);
                    mate.Mate.transform.position = bondSite;
                    StopCoroutine(updateCoroutineDict[mate]);
                    updateCoroutineDict.Remove(mate);
                    switch (mate.Substrate)
                    {
                        case CornerSiteType.TopLeft:
                            TopLeftIsBonded = true;
                            break;
                        case CornerSiteType.TopRight:
                            TopRightIsBonded = true;
                            break;
                        case CornerSiteType.BottomLeft:
                            BottomLeftIsBonded = true;
                            break;
                        case CornerSiteType.BottomRight:
                            BottomRightIsBonded = true;
                            break;
                    }   
                }
                else mate.Mate.transform.position += directionToMate * Time.deltaTime;
            }
        }

        private void RotateMate(GyroidBondMate mate, bool isSnapping)
        {
            Quaternion targetRotation = Quaternion.LookRotation(mate.DeltaForward.x * transform.right + mate.DeltaForward.y * transform.up + mate.DeltaForward.z * transform.forward + transform.forward,
                                                                mate.DeltaUp.x * transform.right + mate.DeltaUp.y * transform.up + mate.DeltaUp.z * transform.forward + transform.up);  // TODO: pull this into setup, so it is only called once.
            mate.Mate.transform.rotation = isSnapping ?
                targetRotation :
                Quaternion.Lerp(mate.Mate.transform.rotation, targetRotation, Time.deltaTime); // Adjust rotation speed as needed
        }
    }
}

