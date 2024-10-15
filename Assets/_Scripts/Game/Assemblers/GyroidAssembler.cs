using CosmicShore.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Utility.ClassExtensions;
using UnityEngine;

namespace CosmicShore
{

    public class GyroidGrowthInfo : GrowthInfo
    {
        public GyroidBlockType BlockType;
    }

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

    public class GyroidAssembler : Assembler
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

        [HideInInspector] public bool TopLeftIsBonded;
        [HideInInspector] public bool TopRightIsBonded;
        [HideInInspector] public bool BottomLeftIsBonded;
        [HideInInspector] public bool BottomRightIsBonded;

        public override bool IsFullyBonded() =>
            TopLeftIsBonded && TopRightIsBonded && BottomLeftIsBonded && BottomRightIsBonded;

        public HashSet<GyroidAssembler> MateList = new();
        public Queue<GyroidAssembler> preferedBlocks = new();

        public override TrailBlock TrailBlock { get; set; }
        public  override Spindle Spindle { get; set; }

        public GyroidBlockType BlockType = GyroidBlockType.AB;
        public int depth = -1;
        
        
        public override int Depth
        {
            get => depth;
            set => depth = value;
        }
        public bool isSeed;

        private float snapDistance = .3f;
        [SerializeField] float separationDistance = 3f;
        [SerializeField] int colliderTheshold = 1;
        [SerializeField] float radius = 40f;

        private const int MaxColliders = 10; // This one only detects 10 collider at the same frame
        private readonly Collider[] _colliders = new Collider[MaxColliders];

        void Start()
        {
            TrailBlock = GetComponent<TrailBlock>();
            if (TrailBlock)
            {
                scale = TrailBlock.TargetScale;
                if (isSeed)
                {
                    TrailBlock.Team = Teams.Blue;
                }
            }
        }

        public override void StartBonding()
        {
            StartCoroutine(LookForMatesCoroutine());
        }

        public override GrowthInfo GetGrowthInfo()
        {
            while (true)
            {
                // Check if the block is fully bonded
                if (IsFullyBonded())
                {
                    return new GrowthInfo { CanGrow = false };
                }

                // Determine the corner site to grow from based on the availability of unmated sites
                var growthSite = GetGrowthSite();

                // Retrieve the bond mate data based on the current block type and the growth site
                if (!GyroidBondMateDataContainer.BondMateDataMap.TryGetValue((BlockType, growthSite), out var bondMateData)) return new GrowthInfo { CanGrow = false };

                // Calculate the new position and rotation based on the bond mate data
                var newPosition = CalculateGlobalBondSite(bondMateData.Substrate);
                var newRotation = CalculateRotation(CreateGyroidBondMate(this, BlockType, growthSite));

                // Check if there is already a block at the new position using Physics.CheckBox
                if (!Physics.CheckBox(newPosition, TrailBlock.transform.localScale / 2f))
                    return new GyroidGrowthInfo
                    {
                        CanGrow = true,
                        Position = newPosition,
                        Rotation = newRotation,
                        BlockType = bondMateData.BlockType,
                        Depth = depth - 1
                    };
                
                // Fill the bond site
                SetBondSiteStatus(growthSite, true);
            }
        }

        private CornerSiteType GetGrowthSite()
        {
            // Check the availability of unmated sites and return the first available one
            if (!TopRightIsBonded)
                return CornerSiteType.TopRight;
            if (!TopLeftIsBonded)
                return CornerSiteType.TopLeft;
            if (!BottomLeftIsBonded)
                return CornerSiteType.BottomLeft;
            return !BottomRightIsBonded ? CornerSiteType.BottomRight : CornerSiteType.None; // Return None if all sites are bonded
        }

        private void SetBondSiteStatus(CornerSiteType site, bool isBonded)
        {
            switch (site)
            {
                case CornerSiteType.TopRight:
                    TopRightIsBonded = isBonded;
                    break;
                case CornerSiteType.TopLeft:
                    TopLeftIsBonded = isBonded;
                    break;
                case CornerSiteType.BottomLeft:
                    BottomLeftIsBonded = isBonded;
                    break;
                case CornerSiteType.BottomRight:
                    BottomRightIsBonded = isBonded;
                    break;
            }
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
            throw new Exception($"GyroidBondMateData not found for blockType: {blockType} and siteType: {siteType}");
        }

        void PrepareMate(GyroidBondMate gyroidBondMate)
        {
            if (!gyroidBondMate.Mate) return;
            
            gyroidBondMate.Mate.BlockType = gyroidBondMate.BlockType;

            switch (gyroidBondMate.Bondee)
            {
                case CornerSiteType.TopLeft:
                    gyroidBondMate.Mate.TopLeftMate = CreateGyroidBondMate(this, gyroidBondMate.BlockType, gyroidBondMate.Bondee);
                    break;
                case CornerSiteType.TopRight:
                    gyroidBondMate.Mate.TopRightMate = CreateGyroidBondMate(this, gyroidBondMate.BlockType, gyroidBondMate.Bondee);
                    break;
                case CornerSiteType.BottomLeft:
                    gyroidBondMate.Mate.BottomLeftMate = CreateGyroidBondMate(this, gyroidBondMate.BlockType, gyroidBondMate.Bondee);
                    break;
                case CornerSiteType.BottomRight:
                    gyroidBondMate.Mate.BottomRightMate = CreateGyroidBondMate(this, gyroidBondMate.BlockType, gyroidBondMate.Bondee);
                    break;
                case CornerSiteType.None:
                default:
                    Debug.LogWarning("No Gyroid bond mate.");
                    break;
            }
            MateList.Add(gyroidBondMate.Mate);
            gyroidBondMate.Mate.MateList.Add(this);
            var coroutine = StartCoroutine(UpdateMate(gyroidBondMate));
            updateCoroutineDict[gyroidBondMate] = coroutine;
        }

        private bool AreAllActiveMatesBonded(bool[] activeMates)
        {
            // Checking each mate's bonded status if they are active
            if (activeMates[0] && !TopLeftIsBonded) return false;     
            if (activeMates[1] && !TopRightIsBonded) return false;    
            if (activeMates[2] && !BottomLeftIsBonded) return false;  
            return !activeMates[3] || BottomRightIsBonded;
            // All active mates are bonded
        }


        void UpdateBondingStatus(GyroidBondMate mate, bool isBonded)
        {
            if (isBonded && mate.Mate.MateList.Count < 2)
            {
                if (depth != 0 && preferedBlocks.Count == 0)
                {
                    mate.Mate.depth = depth;
                    mate.Mate.StartBonding();
                }
                else
                {
                    StopAllCoroutines();
                }             
            }
        }


        IEnumerator LookForMatesCoroutine()
        {
            bool[] activeMates = { false, true, true, false };

            while (true)
            {
                if (TrailBlock == null)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }
                yield return new WaitForSeconds(1f);
                // TopLeftMate
                if (depth == 0)
                {
                    CleanupMates(activeMates);
                    break;
                }
                if (activeMates[0] && TopLeftMate.Mate == null)
                {
                    TopLeftMate = FindClosestMate(CalculateGlobalBondSite(CornerSiteType.TopLeft), CornerSiteType.TopLeft);
                    PrepareMate(TopLeftMate);
                    depth--;
                }
                if (depth == 0)
                {
                    CleanupMates(activeMates);
                    break;
                }
                // TopRightMate
                if (activeMates[1] && TopRightMate.Mate == null)
                {
                    TopRightMate = FindClosestMate(CalculateGlobalBondSite(CornerSiteType.TopRight), CornerSiteType.TopRight);
                    PrepareMate(TopRightMate);
                    depth--;
                }
                if (depth == 0)
                {
                    CleanupMates(activeMates);
                    break;
                }
                // BottomLeftMate
                if (activeMates[2] && BottomLeftMate.Mate == null)
                {
                    BottomLeftMate = FindClosestMate(CalculateGlobalBondSite(CornerSiteType.BottomLeft), CornerSiteType.BottomLeft);
                    PrepareMate(BottomLeftMate);
                    depth--;
                }
                if (depth == 0)
                {
                    CleanupMates(activeMates);
                    break;
                }
                // BottomRightMate
                if (activeMates[3] && BottomRightMate.Mate == null)
                {
                    BottomRightMate = FindClosestMate(CalculateGlobalBondSite(CornerSiteType.BottomRight), CornerSiteType.BottomRight);
                    PrepareMate(BottomRightMate);
                    depth--;
                }
                if (depth == 0)
                {
                    CleanupMates(activeMates);
                    break;
                }
                yield return new WaitForSeconds(1f);

                CleanupMates(activeMates);
            }
        }

        void CleanupMates(bool[] activeMates)
        {
            UpdateBondingStatus(TopLeftMate, TopLeftIsBonded);
            UpdateBondingStatus(TopRightMate, TopRightIsBonded);
            UpdateBondingStatus(BottomLeftMate, BottomLeftIsBonded);
            UpdateBondingStatus(BottomRightMate, BottomRightIsBonded);

            if (!AreAllActiveMatesBonded(activeMates)) return;
            
            StopAllCoroutines();
            TrailBlock.Grow();
        }

        Dictionary<GyroidBondMate, Coroutine> updateCoroutineDict = new Dictionary<GyroidBondMate, Coroutine>();

        IEnumerator UpdateMate(GyroidBondMate gyroidBondMate)
        {
            var targetRotation = CalculateRotation(gyroidBondMate);
            while (gyroidBondMate.Mate)
            {
                yield return null;
                MoveMateToSite(gyroidBondMate, targetRotation, CalculateGlobalBondSite(gyroidBondMate.Substrate));
                RotateMate(gyroidBondMate, targetRotation, false);
            }
        }
        
        // Helper method to calculate local bond site
        Vector3 CalculateBondSite(CornerSiteType site)
        {
            return GyroidBondMateDataContainer.GetBondMateData(BlockType, site).DeltaPosition * separationDistance;
        }

        // Method with switch case to update and return a specific global bond site
        public Vector3 CalculateGlobalBondSite(CornerSiteType site)
        {
            Vector3 localBondSite = CalculateBondSite(site);
            Vector3 globalBondSite;

            switch (site)
            {
                case CornerSiteType.TopLeft:
                    globalBondSiteTopLeft = transform.ToGlobal(localBondSite);
                    globalBondSite = globalBondSiteTopLeft;
                    break;

                case CornerSiteType.TopRight:
                    globalBondSiteTopRight = transform.ToGlobal(localBondSite);
                    globalBondSite = globalBondSiteTopRight;
                    break;

                case CornerSiteType.BottomLeft:
                    globalBondSiteBottomLeft = transform.ToGlobal(localBondSite);
                    globalBondSite = globalBondSiteBottomLeft;
                    break;

                case CornerSiteType.BottomRight:
                    globalBondSiteBottomRight = transform.ToGlobal(localBondSite);
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
            return mateComponent.MateList is { Count: > 0 };
        }
        // this checks IsMate and then checks each bond such as TopLeftIsBonded to see if it is bonded to anything i.e bonds > 0
        public bool IsBonded()
        {
            return IsMate(this) || TopLeftIsBonded || TopRightIsBonded || BottomLeftIsBonded || BottomRightIsBonded;
        }
 
        // this method generalize both of the methods above
        private GyroidBondMate FindClosestMate(Vector3 bondSite, CornerSiteType siteType)
        {
            if (preferedBlocks.Count > 0)
            {
                Debug.Log($"GyroidAssembler: Preferred Block, Depth: {depth}");
                var mate = CreateGyroidBondMate(preferedBlocks.Dequeue(), BlockType, siteType); 
                return mate;
            }

            Debug.Log($"GyroidAssembler: No Preferred Block, Depth: {depth}");

            var closestDistance = float.MaxValue;
            GyroidAssembler closest = null;
            _ = GyroidBondMateDataContainer.GetBondMateData(BlockType, siteType).isTail;

            Physics.OverlapSphereNonAlloc(bondSite, radius, _colliders);
            if (_colliders.Length < colliderTheshold)
            {
                return new GyroidBondMate { Mate = null };
            }
            foreach (var potentialMate in _colliders) // Adjust radius as needed
            {
                var mateComponent = potentialMate.GetComponent<GyroidAssembler>();
                if (mateComponent == null)
                {
                    var trailBlock = potentialMate.GetComponent<TrailBlock>();
                    if (trailBlock != null)
                    {
                        mateComponent = ConvertBlock(trailBlock);
                    }
                    else continue;
                }

                var sqrDistance = (bondSite - mateComponent.transform.position).sqrMagnitude;

                if (IsMate(mateComponent) && mateComponent != this)
                {
                    
                    if (Vector3.SqrMagnitude(transform.position - mateComponent.transform.position) < snapDistance  //block younger and in this block's position then clear its mate list
                        && mateComponent.TrailBlock.TrailBlockProperties.TimeCreated > TrailBlock.TrailBlockProperties.TimeCreated)
                    {
                        mateComponent.StopAllCoroutines();
                        mateComponent.ClearMateList();
                    }
                    if (sqrDistance < snapDistance) // if block is  already  in position supershield it.
                    {
                        mateComponent.TrailBlock.ActivateSuperShield();
                        return CreateGyroidBondMate(mateComponent, BlockType, siteType);
                    }
                }
                if (!IsMate(mateComponent) && mateComponent != this)
                {
                    if (sqrDistance < closestDistance)
                    {
                        closestDistance = sqrDistance;
                        closest = mateComponent;
                    }
                }
            }
            return CreateGyroidBondMate(closest, BlockType, siteType);
        }

        GyroidAssembler ConvertBlock(TrailBlock trailBlock)
        {
            HealthBlock healthBlock = trailBlock.GetComponent<HealthBlock>();
            if (healthBlock != null)
            {
                healthBlock.Reparent(TrailBlock.transform.parent);
            }
            trailBlock.TargetScale = scale;
            trailBlock.MaxScale = TrailBlock.MaxScale;
            trailBlock.GrowthVector = TrailBlock.GrowthVector;
            trailBlock.Steal(TrailBlock.Player, TrailBlock.Team);
            trailBlock.ChangeSize();
            var mateComponent = trailBlock.gameObject.AddComponent<GyroidAssembler>();
            mateComponent.TrailBlock = trailBlock;
            return mateComponent;
        }

        private void MoveMateToSite(GyroidBondMate mate, Quaternion targetRotation, Vector3 bondSite)
        {
            {
                var initialPosition = mate.Mate.transform.position;
                var directionToMate = bondSite - initialPosition;
                
                if (directionToMate.sqrMagnitude < snapDistance)
                {
                    RotateMate(mate, targetRotation, true);
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
                        case CornerSiteType.None:
                        default:
                            Debug.LogWarning("No Gyroid bond mate.");
                            break;
                    }   
                }
                else mate.Mate.transform.position += directionToMate * Time.deltaTime;
            }
        }

        private Quaternion CalculateRotation(GyroidBondMate mate)
        {
            Quaternion targetRotation = Quaternion.LookRotation(mate.DeltaForward.x * transform.right + mate.DeltaForward.y * transform.up + mate.DeltaForward.z * transform.forward + transform.forward,
                                                                mate.DeltaUp.x * transform.right + mate.DeltaUp.y * transform.up + mate.DeltaUp.z * transform.forward + transform.up);
            return targetRotation;
        }

        private void RotateMate(GyroidBondMate mate, Quaternion targetRotation, bool isSnapping)
        {
            mate.Mate.transform.rotation = isSnapping ? targetRotation :
                Quaternion.Lerp(mate.Mate.transform.rotation, targetRotation, Time.deltaTime); // Adjust rotation speed as needed
        }
    }
}

