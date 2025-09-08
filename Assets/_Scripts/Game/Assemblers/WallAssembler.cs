using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class WallAssembler : Assembler
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
            Left,
            None
        }

        [SerializeField] private float ownPullSpeed = 4.0f;
        [SerializeField] private float opponentPullSpeed = 1.0f;

        [SerializeField] private float ownRotateSpeed = 8.0f;
        [SerializeField] private float opponentRotateSpeed = 3.0f;

        //public bool IsActive = false;
        private Vector3 scale; // Scale of the TrailBlock
        private Vector3 BondSiteTop;
        private Vector3 BondSiteRight;
        private Vector3 BondSiteBottom;
        private Vector3 BondSiteLeft;

        private Vector3 globalBondSiteTop; // Global position of Bond Site A
        private Vector3 globalBondSiteRight; // Global position of Bond Site B
        private Vector3 globalBondSiteBottom;
        private Vector3 globalBondSiteLeft;

        [HideInInspector] public BondMate TopMate;
        [HideInInspector] public WallAssembler RightMate;
        [HideInInspector] public BondMate BottomMate;
        [HideInInspector] public WallAssembler LeftMate;

        [HideInInspector] public bool TopIsBonded;
        [HideInInspector] public bool RightIsBonded;
        [HideInInspector] public bool BottomIsBonded;
        [HideInInspector] public bool LeftIsBonded;

        public override bool IsFullyBonded() => TopIsBonded && RightIsBonded && BottomIsBonded && LeftIsBonded;

        [HideInInspector] public HashSet<WallAssembler> MateList = new();
        public override TrailBlock TrailBlock { get; set; }
        public override Spindle Spindle { get; set; }

        private float snapDistance = .2f;
        float separationDistance = 2f;
        [SerializeField] int colliderTheshold = 25;
        [SerializeField] float radius = 40f;
        bool isStopped = true;

        int depth = -1;

        public override int Depth
        {
            get { return depth; }
            set { depth = value; }
        }

        void Start()
        {
            TrailBlock = GetComponent<TrailBlock>();
            if (TrailBlock)
            {
                scale = TrailBlock.TargetScale;
                CalculateGlobalBondSites();
            }
        }

        public override void SeedBonding()
        {
            StartBonding();
        }

        public override void StartBonding()
        {
            isStopped = false;
            StartCoroutine(LookForMates());
        }

        //the following method calculates rotation using SiteType. Is is similiar to "Quaternion CalculateRotation(BondMate mate)"
        private Quaternion CalculateRotation(SiteType site)
        {
            Vector3 up, forward;

            switch (site)
            {
                case SiteType.Top:
                    up = transform.up;
                    forward = transform.forward;
                    break;
                case SiteType.Right:
                    up = transform.right;
                    forward = transform.forward;
                    break;
                case SiteType.Bottom:
                    up = -transform.up;
                    forward = transform.forward;
                    break;
                case SiteType.Left:
                    up = -transform.right;
                    forward = transform.forward;
                    break;
                default:
                    return Quaternion.identity;
            }

            // Adjust the rotation to match the herringbone pattern, if needed
            if (site == SiteType.Top || site == SiteType.Bottom)
            {
                // For vertical connections, rotate 90 degrees around the forward axis
                Quaternion additionalRotation = Quaternion.AngleAxis(90, forward);
                up = additionalRotation * up;
            }

            return Quaternion.LookRotation(forward, up);
        }


        //the following method takes in a SiteType and a bool and sets the bond site status
        private void SetBondSiteStatus(SiteType site, bool status)
        {
            switch (site)
            {
                case SiteType.Top:
                    TopIsBonded = status;
                    break;
                case SiteType.Right:
                    RightIsBonded = status;
                    break;
                case SiteType.Bottom:
                    BottomIsBonded = status;
                    break;
                case SiteType.Left:
                    LeftIsBonded = status;
                    break;
            }
        }

        private Vector3 CalculatePosition(SiteType site)
        {
            Vector3 offset = Vector3.zero;

            switch (site)
            {
                case SiteType.Top:
                    offset = transform.up * (scale.y + separationDistance);
                    break;
                case SiteType.Right:
                    offset = transform.right * (scale.x + separationDistance);
                    break;
                case SiteType.Bottom:
                    offset = -transform.up * (scale.y + separationDistance);
                    break;
                case SiteType.Left:
                    offset = -transform.right * (scale.x + separationDistance);
                    break;
                default:
                    break;
            }

            return transform.position + offset;
        }

        public override GrowthInfo GetGrowthInfo()
        {
            if (IsFullyBonded())
            {
                return new GrowthInfo { CanGrow = false };
            }

            CalculateGlobalBondSites();

            // Prioritize growing in alternating directions
            SiteType[] growthPriority = { SiteType.Right, SiteType.Left };
            foreach (var site in growthPriority)
            {
                if (!IsBonded(site))
                {
                    Vector3 newPosition = CalculatePosition(site);
                    Quaternion newRotation = CalculateRotation(site);

                    if (!Physics.CheckBox(newPosition, TrailBlock.transform.localScale / 2f))
                    {
                        SetBondSiteStatus(site, true);
                        return new GrowthInfo
                        {
                            CanGrow = true,
                            Position = newPosition,
                            Rotation = newRotation,
                            Depth = Depth - 1
                        };
                    }
                    else
                    {
                        SetBondSiteStatus(site, true);
                    }
                }
            }

            return new GrowthInfo { CanGrow = false };
        }


        private bool IsBonded(SiteType site)
        {
            switch (site)
            {
                case SiteType.Top: return TopIsBonded;
                case SiteType.Right: return RightIsBonded;
                case SiteType.Bottom: return BottomIsBonded;
                case SiteType.Left: return LeftIsBonded;
                default: return false;
            }
        }

        public void ClearMateList()
        {
            foreach (var mate in MateList)
            {
                mate.MateList.Remove(this);
            }

            MateList.Clear();
            TopIsBonded = false;
            RightIsBonded = false;
            BottomIsBonded = false;
            LeftIsBonded = false;
        }

        public void ReplaceMateList(WallAssembler newWallAssembler)
        {
            foreach (var mate in MateList)
            {
                mate.MateList.Add(newWallAssembler);
            }
        }

        IEnumerator LookForMates()
        {
            while (!isStopped)
            {
                while (true)
                {
                    if (TrailBlock == null)
                    {
                        yield return new WaitForSeconds(1f);
                        continue;
                    }

                    if (TopMate.Mate == null)
                    {
                        TopMate = FindClosestMate(globalBondSiteTop, SiteType.Top);

                        if (TopMate.Mate)
                        {
                            TopMate.Mate.RightMate = this;
                            MateList.Add(TopMate.Mate);
                            TopMate.Mate.MateList.Add(this);
                            updateTopMate = StartCoroutine(UpdateTopMate());
                        }
                    }

                    yield return new WaitForSeconds(1f);
                    if (BottomMate.Mate == null)
                    {
                        BottomMate = FindClosestMate(globalBondSiteBottom, SiteType.Bottom);
                        if (BottomMate.Mate)
                        {
                            BottomMate.Mate.LeftMate = this;
                            MateList.Add(BottomMate.Mate);
                            BottomMate.Mate.MateList.Add(this);
                            updateBottomMate = StartCoroutine(UpdateBottomMate());
                        }
                    }

                    yield return new WaitForSeconds(1f);
                    if (TopIsBonded && BottomIsBonded)
                    {
                        //Debug.Log("Bonded Top and Bottom");
                        StopAllCoroutines();
                        TrailBlock.Grow();
                        if (TopMate.Mate.MateList.Count < 2)
                        {
                            TopMate.Mate.StartBonding();
                        }

                        if (BottomMate.Mate.MateList.Count < 2)
                        {
                            BottomMate.Mate.StartBonding();
                        }
                    }
                }
            }
        }

        Coroutine updateTopMate;

        IEnumerator UpdateTopMate()
        {
            while (true)
            {
                yield return null;
                if (TopMate.Mate != null)
                {
                    RotateMate(TopMate, false);
                    MoveMateToSite(TopMate, globalBondSiteTop);
                }
            }
        }

        Coroutine updateBottomMate;

        IEnumerator UpdateBottomMate()
        {
            while (true)
            {
                yield return null;

                if (BottomMate.Mate != null)
                {
                    RotateMate(BottomMate, false);
                    MoveMateToSite(BottomMate, globalBondSiteBottom);
                }
            }
        }

        void CalculateBondSites()
        {
            // Using the bond site calculations from WallAssembler
            BondSiteTop = ((scale.y / 2) + separationDistance) * transform.up;
            BondSiteRight = ((scale.y / 2) - (scale.x / 2)) * transform.up +
                            (((scale.x / 2) + separationDistance) * transform.right);
            BondSiteBottom = -((scale.y / 2) + separationDistance) * transform.up;
            BondSiteLeft = -(((scale.y / 2) - (scale.x / 2)) * transform.up) -
                           ((scale.x / 2) + separationDistance) * transform.right;
        }

        public void CalculateGlobalBondSites()
        {
            CalculateBondSites();

            globalBondSiteTop = transform.position + BondSiteTop;
            globalBondSiteRight = transform.position + BondSiteRight;
            globalBondSiteBottom = transform.position + BondSiteBottom;
            globalBondSiteLeft = transform.position + BondSiteLeft;
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
            var colliders = Physics.OverlapSphere(bondSite, radius); // Adjust radius as needed
            if (colliders.Length < colliderTheshold) return new BondMate { Mate = null };
            foreach (var potentialMate in colliders) // Adjust radius as needed
            {
                WallAssembler mateComponent = potentialMate.GetComponent<WallAssembler>();
                if (mateComponent == null)
                {
                    var trailBlock = potentialMate.GetComponent<TrailBlock>();
                    if (trailBlock != null)
                    {
                        HealthBlock healthBlock = trailBlock.GetComponent<HealthBlock>();
                        if (healthBlock != null)
                        {
                            healthBlock.Reparent(TrailBlock.transform.parent);
                        }

                        trailBlock.TargetScale = scale;
                        trailBlock.MaxScale = TrailBlock.MaxScale;
                        trailBlock.GrowthVector = TrailBlock.GrowthVector;
                        // trailBlock.Steal(TrailBlock.PlayerName, TrailBlock.Team, true);
                        trailBlock.ChangeSize();
                        mateComponent = trailBlock.transform.gameObject.AddComponent<WallAssembler>();
                    }
                    else continue;
                }

                if (IsMate(mateComponent) && mateComponent != this)
                {
                    if (Vector3.Distance(transform.position, mateComponent.transform.position) < snapDistance
                        && mateComponent.TrailBlock.TrailBlockProperties.TimeCreated >
                        TrailBlock.TrailBlockProperties.TimeCreated)
                    {
                        mateComponent.StopAllCoroutines();
                        mateComponent.ReplaceMateList(this);
                        mateComponent.ClearMateList();
                    }

                    if (siteType == SiteType.Top &&
                        (bondSite - mateComponent.globalBondSiteRight).sqrMagnitude < snapDistance)
                    {
                        //Debug.Log("ReFound MateRight");
                        mateComponent.TrailBlock.ActivateShield();
                        return new BondMate { Mate = mateComponent, Substrate = siteType, Bondee = SiteType.Right };
                    }

                    if (siteType == SiteType.Bottom &&
                        (bondSite - mateComponent.globalBondSiteLeft).sqrMagnitude < snapDistance)
                    {
                        //Debug.Log("ReFound MateLeft");
                        mateComponent.TrailBlock.MakeDangerous();
                        return new BondMate { Mate = mateComponent, Substrate = siteType, Bondee = SiteType.Left };
                    }
                }

                if (!IsMate(mateComponent) && mateComponent != this)
                {
                    if (siteType == SiteType.Top)
                    {
                        float distance = (bondSite - mateComponent.globalBondSiteRight).sqrMagnitude;
                        if (distance < closestDistance)
                        {
                            //Debug.Log("Found MateRight");
                            closestDistance = distance;
                            closest = mateComponent;
                            bondee = SiteType.Right;
                        }
                    }
                    else if (siteType == SiteType.Bottom)
                    {
                        float distance = (bondSite - mateComponent.globalBondSiteLeft).sqrMagnitude;
                        if (distance < closestDistance)
                        {
                            //Debug.Log("Found MateLeft");
                            closestDistance = distance;
                            closest = mateComponent;
                            bondee = SiteType.Left;
                        }
                    }
                }
            }

            return new BondMate { Mate = closest, Substrate = siteType, Bondee = bondee };
        }

        private void MoveMateToSite(BondMate mate, Vector3 bondSite)
        {
            // ensure bond sites are up to date for both nodes
            CalculateGlobalBondSites();
            mate.Mate.CalculateGlobalBondSites();
            mate.Mate.CalculateBondSites(); // to access BondSiteRight/Left

            // target world-space position for the mate's *bond site* to land at 'bondSite'
            Vector3 mateLocalOffset = (mate.Bondee == SiteType.Right) ? mate.Mate.BondSiteRight : mate.Mate.BondSiteLeft;
            Vector3 targetPos = bondSite - mateLocalOffset;

            // choose speeds based on whether this is own team or opponent team
            bool isOwnTeam = (mate.Mate.TrailBlock.Team == TrailBlock.Team);
            float moveSpeed = isOwnTeam ? ownPullSpeed : opponentPullSpeed;

            // move towards target at a fixed speed (frame-rate independent)
            mate.Mate.transform.position = Vector3.MoveTowards(
                mate.Mate.transform.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );

            // simple snap check
            float distSqr = (mate.Mate.transform.position - targetPos).sqrMagnitude;
            if (distSqr < snapDistance)
            {
                // snap rotation & position
                RotateMate(mate, true);        // hard snap rot
                CalculateBondSites();
                mate.Mate.transform.position = targetPos;

                // mark bonded side
                if (mate.Substrate == SiteType.Top)
                {
                    if (updateTopMate != null) StopCoroutine(updateTopMate);
                    TopIsBonded = true;
                }
                else if (mate.Substrate == SiteType.Bottom)
                {
                    if (updateBottomMate != null) StopCoroutine(updateBottomMate);
                    BottomIsBonded = true;
                }
                
                var mateTB = mate.Mate.TrailBlock;
                if (mateTB != null && mateTB.Team != TrailBlock.Team)
                {
                    mateTB.Steal(TrailBlock.PlayerName, TrailBlock.Team, true);
                }
            }
        }


        Quaternion CalculateRotation(BondMate mate)
        {
            int signRight = mate.Bondee == SiteType.Right ? 1 : -1;
            int signTop = mate.Substrate == SiteType.Top ? 1 : -1;
            return Quaternion.LookRotation(transform.forward, signRight * signTop * transform.right);
        }

        private void RotateMate(BondMate mate, bool isSnapping)
        {
            var targetRotation = CalculateRotation(mate);

            if (isSnapping)
            {
                mate.Mate.transform.rotation = targetRotation;
            }
            else
            {
                bool isOwnTeam = (mate.Mate.TrailBlock.Team == TrailBlock.Team);
                float rotSpeed = isOwnTeam ? ownRotateSpeed : opponentRotateSpeed;

                mate.Mate.transform.rotation = Quaternion.Slerp(
                    mate.Mate.transform.rotation,
                    targetRotation,
                    Time.deltaTime * rotSpeed
                );
            }

            mate.Mate.CalculateGlobalBondSites();
            CalculateGlobalBondSites();
        }

        public override void StopBonding()
        {
            isStopped = true;
            if (updateTopMate != null) { StopCoroutine(updateTopMate); updateTopMate = null; }
            if (updateBottomMate != null) { StopCoroutine(updateBottomMate); updateBottomMate = null; }

            ClearMateList();
            TopMate = default;
            BottomMate = default;
            RightMate = null;
            LeftMate  = null;
        
            Debug.Log("WallAssembler stopped bonding");
        }

        public void StopAssembly()
        {
            if (isStopped) return;
            if (updateTopMate != null)
            {
                TopMate.Mate.StopAssembly();
                StopCoroutine(updateTopMate);
            }

            if (updateBottomMate != null)
            {
                BottomMate.Mate.StopAssembly();
                StopCoroutine(updateBottomMate);
            }

            LeftMate.StopAssembly();
            RightMate.StopAssembly();
            isStopped = true;
            Debug.Log("Assembly Stopped");
        }
    }
}