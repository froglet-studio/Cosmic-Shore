using CosmicShore.Gameplay;
using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using CosmicShore.Data;
using System.Linq;
namespace CosmicShore.Gameplay
{

    public class GyroidGrowthInfo : GrowthInfo
    {
        public GyroidBlockType BlockType;

        /// <summary>
        /// The corner site on the SUBSTRATE assembler this growth would fill. Carried so a
        /// caller that declines the site for a non-occupancy reason (the octagon colony's
        /// ownership gate - the site belongs to a neighbouring plant's territory) can tell the
        /// assembler via <see cref="GyroidAssembler.DeclineGrowthSite"/>; without that, the
        /// assembler re-offers the same site every tick and the branch stalls forever.
        /// </summary>
        public CornerSiteType Site = CornerSiteType.None;
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

        public override Prism Prism { get; set; }
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

        /// <summary>Lattice spacing this assembler bonds at. Read-only, for pure PREVIEWS of
        /// the growth pattern (flora icons) that must never instantiate anything.</summary>
        public float SeparationDistance => separationDistance;

        /// <summary>How far this assembler's lattice is stretched (FloraVariantTuning.LatticeScale).
        /// 1 for every element that keeps the prefab's spacing.</summary>
        public float LatticeScale { get; private set; } = 1f;

        /// <summary>
        /// Scales this element's whole lattice, and with it EVERY tolerance that decides
        /// lattice coherence. Set before the plant's first growth probe.
        ///
        /// <para>Scaling <see cref="separationDistance"/> alone is what shipped once and had to
        /// be reverted (Docs/ECOSYSTEM.md 33.8): a gyroid plant's coherence rides distances
        /// written in ABSOLUTE world units, all of them sized against separationDistance 3, so
        /// widening the lattice moved every real distance out from under them at once. The worst
        /// was AssembledFlora's lattice-misalignment gate, which stopped catching the twin
        /// domains it exists to catch, and the plant grew offset parallel surfaces.</para>
        ///
        /// <para>So all of them move together here:</para>
        /// <list type="bullet">
        /// <item>bond offsets, via <see cref="separationDistance"/>;</item>
        /// <item><see cref="snapDistance"/> - "is this the prism AT my bond site, or a second one
        /// beside it". It is compared against SQUARED distances, so it takes <c>scale²</c> to
        /// represent the same LINEAR tolerance;</item>
        /// <item><see cref="radius"/> - how far the mate search looks for that prism at all;</item>
        /// <item>the reservation clearRadius floor, and AssembledFlora's misalignment gate and
        /// octagon tables, which read <see cref="LatticeScale"/>.</item>
        /// </list>
        /// </summary>
        public void ApplyLatticeScale(float scale)
        {
            if (scale <= 0f || Mathf.Approximately(scale, LatticeScale)) return;

            float delta = scale / LatticeScale;
            LatticeScale = scale;

            separationDistance *= delta;
            radius *= delta;
            snapDistance *= delta * delta;   // compared against squared distances
        }

        [SerializeField] int colliderTheshold = 1;
        [SerializeField] float radius = 40f;

        // Bounds the candidates considered (and therefore ConvertBlock steals) per
        // mate search - same bound the old 10-slot OverlapSphereNonAlloc buffer gave.
        private const int MaxMateCandidates = 10;
        private readonly Collider[] _colliders = new Collider[MaxMateCandidates];

        // Mate candidates come from PrismSpatialIndex.QuerySphere; this scratch is
        // consumed within FindClosestMate on the main thread (Fauna.OverlapScratch
        // pattern), so one static list serves every assembler with zero allocation.
        private static readonly List<Prism> s_mateScratch = new(64);

        // Mound blocks built by Boid.NewBlock skip Prism.Initialize, so they never
        // register with the spatial index - their colliders on the dedicated Mound
        // layer are the only way to find them. Lazy so LayerMask resolves after
        // engine init.
        private static int s_moundLayerMask;
        private static int MoundLayerMask =>
            s_moundLayerMask != 0 ? s_moundLayerMask : s_moundLayerMask = LayerMask.GetMask("Mound");

        void Start()
        {
            Prism = GetComponent<Prism>();
            if (Prism)
            {
                scale = Prism.TargetScale;
                if (isSeed)
                {
                    Prism.Domain = Domains.Blue;
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

                // Occupancy via PrismSpatialIndex.TryReserve instead of Physics.CheckBox.
                // The physics probe was structurally blind: Prism colliders stay disabled
                // for the first Prism.waitTime (0.6s) after spawn, so concurrent branches
                // (and concurrent floras) stacked blocks on the same site. TryReserve
                // claims the site synchronously with this grow decision; the claim is
                // consumed when the spawned prism registers, or lapses after TTL if the
                // spawn never happens. clearRadius is 0.4× this bond's lattice spacing -
                // below half-spacing so legitimate neighbor sites are never blocked,
                // above any drift so a same-site duplicate always is.
                float clearRadius = Mathf.Max(2f * LatticeScale, 0.4f * (newPosition - transform.position).magnitude);
                var spatialIndex = PrismSpatialIndex.EnsureInstance();
                bool unreserved = spatialIndex == null || !spatialIndex.IsAvailable;
                // An unavailable index means growth proceeds with NO occupancy dedupe at all -
                // the one path that can double-fill a site. Counted so the colony heartbeat
                // makes it visible: a non-zero UnreservedSpawns alongside overlapping prisms IS
                // the diagnosis (and the fix is index availability, not growth logic).
                if (unreserved) GyroidColonyDiagnostics.UnreservedSpawns++;
                if (unreserved ||
                    spatialIndex.TryReserve(newPosition, clearRadius))
                    return new GyroidGrowthInfo
                    {
                        CanGrow = true,
                        Position = newPosition,
                        Rotation = newRotation,
                        BlockType = bondMateData.BlockType,
                        IsDangerous =
                            bondMateData.BlockType == GyroidBlockType.GEs ||
                            bondMateData.BlockType == GyroidBlockType.DE ||
                            bondMateData.BlockType == GyroidBlockType.EG ||
                            bondMateData.BlockType == GyroidBlockType.EsD,
                        Depth = depth - 1,
                        Site = growthSite
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

        /// <summary>
        /// Marks a growth site permanently filled WITHOUT growing it - the caller decided the
        /// site is not this plant's to grow (the octagon colony's ownership gate: it belongs to
        /// a neighbouring octagon's territory, and that plant will grow it from its own lattice
        /// at the same world position). The site's spatial-index reservation, made by
        /// <see cref="GetGrowthInfo"/>, simply lapses on its TTL - the standard
        /// skip-after-claim path every dropped grow order already takes.
        /// </summary>
        public void DeclineGrowthSite(CornerSiteType site)
        {
            if (site != CornerSiteType.None) SetBondSiteStatus(site, true);
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
                    CSDebug.LogWarning("No Gyroid bond mate.");
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
                if (Prism == null)
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
            Prism.Grow();
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
                CSDebug.Log($"GyroidAssembler: Preferred Block, Depth: {depth}");
                var mate = CreateGyroidBondMate(preferedBlocks.Dequeue(), BlockType, siteType);
                return mate;
            }

            CSDebug.Log($"GyroidAssembler: No Preferred Block, Depth: {depth}");

            // Candidates come from the spatial index (the canonical prism population -
            // no physics broadphase, no per-collider GetComponent) plus a Mound-layer
            // collider probe for the unregistered blocks Boid.NewBlock builds mounds
            // from. The old unfiltered OverlapSphereNonAlloc here also iterated its
            // full 10-slot buffer regardless of the hit count, processing stale
            // colliders from previous searches - that bug is gone with the snapshot
            // list.
            var spatialIndex = PrismSpatialIndex.EnsureInstance();
            int prismCount = spatialIndex != null && spatialIndex.IsAvailable
                ? spatialIndex.QuerySphere(bondSite, radius, s_mateScratch)
                : 0;
            int moundCount = Physics.OverlapSphereNonAlloc(bondSite, radius, _colliders, MoundLayerMask);
            if (prismCount + moundCount < colliderTheshold)
            {
                return new GyroidBondMate { Mate = null };
            }

            var closestDistance = float.MaxValue;
            GyroidAssembler closest = null;
            GyroidAssembler snapMate = null;
            int considered = 0;

            void Consider(Prism candidate)
            {
                if (snapMate != null || considered >= MaxMateCandidates) return;
                if (candidate == null || candidate.destroyed) return;
                considered++;

                var mateComponent = candidate.GetComponent<GyroidAssembler>();
                if (mateComponent == null)
                {
                    mateComponent = ConvertBlock(candidate);
                }

                var sqrDistance = (bondSite - mateComponent.transform.position).sqrMagnitude;

                if (IsMate(mateComponent) && mateComponent != this)
                {
                    if (Vector3.SqrMagnitude(transform.position - mateComponent.transform.position) < snapDistance  //block younger and in this block's position then clear its mate list
                        && mateComponent.Prism.prismProperties.TimeCreated > Prism.prismProperties.TimeCreated)
                    {
                        mateComponent.StopAllCoroutines();
                        mateComponent.ClearMateList();
                    }
                    if (sqrDistance < snapDistance) // if block is  already  in position supershield it.
                    {
                        mateComponent.Prism.ActivateSuperShield();
                        snapMate = mateComponent;
                        return;
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

            // Mound blocks first - they are the structural siblings a growing mound
            // should knit with before reaching for registered trail/flora prisms.
            for (int i = 0; i < moundCount && snapMate == null; i++)
            {
                var moundCollider = _colliders[i];
                if (moundCollider && moundCollider.TryGetComponent(out Prism moundPrism))
                    Consider(moundPrism);
            }
            for (int i = 0; i < prismCount && snapMate == null; i++)
            {
                Consider(s_mateScratch[i]);
            }

            return CreateGyroidBondMate(snapMate != null ? snapMate : closest, BlockType, siteType);
        }

        GyroidAssembler ConvertBlock(Prism prism)
        {
            HealthPrism healthPrism = prism.GetComponent<HealthPrism>();
            if (healthPrism != null)
            {
                healthPrism.Reparent(Prism.transform.parent);
            }
            // Widen BEFORE stating the size, not after: TargetScale's setter clamps per axis
            // into the VICTIM's own [minScale, maxScale] (default 0.5..10), so assigning first
            // and raising MaxScale second lets the clamp bite and the widening arrive too late
            // to undo it - a converted prism would be pinned at 10 however long this lattice's
            // prisms are. AdmitTargetScale also lowers minScale, which the plain MaxScale
            // assignment never did, so a thin lattice prism survives too.
            prism.MaxScale = Prism.MaxScale;
            prism.AdmitTargetScale(scale);
            prism.TargetScale = scale;
            prism.GrowthVector = Prism.GrowthVector;
            prism.Steal(Prism.PlayerName, Prism.Domain);
            prism.ChangeSize();
            var mateComponent = prism.gameObject.AddComponent<GyroidAssembler>();
            mateComponent.Prism = prism;
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
                    // Steered blocks must keep the spatial index honest - AOE and
                    // occupancy both read the stored position, not the transform.
                    if (mate.Mate.Prism) mate.Mate.Prism.NotifyPositionChanged();
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
                            CSDebug.LogWarning("No Gyroid bond mate.");
                            break;
                    }   
                }
                else
                {
                    mate.Mate.transform.position += directionToMate * Time.deltaTime;
                    if (mate.Mate.Prism) mate.Mate.Prism.NotifyPositionChanged();
                }
            }
        }

        private Quaternion CalculateRotation(GyroidBondMate mate)
        {
            Vector3 forward = mate.DeltaForward.x * transform.right + mate.DeltaForward.y * transform.up + mate.DeltaForward.z * transform.forward + transform.forward;
            Vector3 up = mate.DeltaUp.x * transform.right + mate.DeltaUp.y * transform.up + mate.DeltaUp.z * transform.forward + transform.up;

            if (!SafeLookRotation.TryGet(forward, up, out var targetRotation, mate.Mate ? mate.Mate.gameObject : gameObject))
            {
                // A degenerate basis means this prism keeps whatever rotation it already had -
                // exactly the "90 degrees off" twin the colony auditor hunts. Offline analysis
                // says the bond table never produces one (margins >= 0.9999), so a non-zero
                // count here fingers a post-spawn transform write corrupting the parent frame.
                GyroidColonyDiagnostics.RotationFallbacks++;
                return mate.Mate ? mate.Mate.transform.rotation : transform.rotation;
            }

            return targetRotation;
        }

        private void RotateMate(GyroidBondMate mate, Quaternion targetRotation, bool isSnapping)
        {
            mate.Mate.transform.rotation = isSnapping ? targetRotation :
                Quaternion.Lerp(mate.Mate.transform.rotation, targetRotation, Time.deltaTime); // Adjust rotation speed as needed
        }
    }
}

