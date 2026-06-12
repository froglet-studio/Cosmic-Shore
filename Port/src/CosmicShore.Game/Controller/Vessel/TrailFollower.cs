
using CosmicShore.Gameplay;
using CosmicShore.Engine;
using CosmicShore.Utility;
using CosmicShore.Data;
using System.Linq;
namespace CosmicShore.Gameplay
{
    // TODO: move to enum folder
    public enum TrailFollowerDirection
    {
        Forward = 1,
        Backward = -1,
    }

    [RequireComponent(typeof(IVesselStatus))]
    public class TrailFollower : MonoBehaviour
    {
        int attachedBlockIndex;
        Trail attachedTrail;
        Domains domain;
        float percentTowardNextBlock;
        TrailFollowerDirection direction;
        public TrailFollowerDirection Direction { get { return direction; } }


        [SerializeField] float FriendlyTerrainSpeed;
        [SerializeField] float HostileTerrainSpeed;
        [SerializeField] float DestroyedTerrainSpeed;

        [HideInInspector]
        public float Throttle;

        public bool IsAttached { get { return attachedTrail != null; } }
        // PORT Deviation (V14, restore when Prism ports): public Prism AttachedPrism { get { return attachedTrail.GetBlock(attachedBlockIndex); } }
        public MonoBehaviour AttachedPrism { get { return attachedTrail.GetBlock(attachedBlockIndex); } }

        IVesselStatus vesselData;

        void Start()
        {
            // TODO: find a better way of setting team that doesn't assume a vessel
            vesselData = GetComponent<IVesselStatus>();
            domain = vesselData.Domain;
        }

        // PORT Deviation (V14, restore when Prism ports): public void Attach(Prism prism)
        public void Attach(MonoBehaviour prism)
        {
            // PORT Deviation (V14, restore when Prism ports): CSDebug.Log($"Attaching: trail:{prism.Trail}");
            // PORT Deviation (V14, restore when Prism ports): attachedTrail = prism.Trail;
            attachedBlockIndex = attachedTrail.GetBlockIndex(prism);
            percentTowardNextBlock = 0; // TODO: calculate initial percentTowardNextBlock
            direction = TrailFollowerDirection.Forward; // TODO: use dot product to capture initial direction
        }

        public void Detach()
        {
            attachedTrail = null;
        }

        public void RideTheTrail()
        {
            if (!IsAttached) return;

            var upcomingBlocks = attachedTrail.LookAhead(attachedBlockIndex, percentTowardNextBlock, direction, Throttle * FriendlyTerrainSpeed * Time.deltaTime);
            if (upcomingBlocks == null || upcomingBlocks.Count < 2) {
                CSDebug.LogWarning("Could not move TrailFollower, not enough upcoming blocks");
                return;
            }

            // TODO: percentTowardNextBlock is always positive?

            var distanceToTravel = 0f;  // <-- This is what we're calculating
            var timeRemaining = Time.deltaTime;

            var blockIndex = 0;
            var currentBlock = upcomingBlocks[blockIndex];
            var nextBlock = upcomingBlocks[blockIndex+1];

            var distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position) * (1-percentTowardNextBlock);
            var speedToNextBlock = Throttle * GetTerrainAwareBlockSpeed(currentBlock);

            speedToNextBlock *= vesselData.VesselTransformer.SpeedMultiplier;
            vesselData.Speed = speedToNextBlock;

            var timeToNextBlock = distanceToNextBlock / speedToNextBlock;

            while (timeRemaining > timeToNextBlock)
            {
                distanceToTravel += distanceToNextBlock;
                timeRemaining -= timeToNextBlock;

                currentBlock = upcomingBlocks[++blockIndex];
                nextBlock = upcomingBlocks[blockIndex + 1];

                distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position);
                speedToNextBlock = Throttle * GetTerrainAwareBlockSpeed(currentBlock);
                speedToNextBlock *= vesselData.VesselTransformer.SpeedMultiplier;
                vesselData.Speed = speedToNextBlock;

                timeToNextBlock = distanceToNextBlock / speedToNextBlock;
            }

            // Accumulate the remain
            distanceToTravel += speedToNextBlock * timeRemaining;

            // Do the movement and save the out direction
            transform.position = attachedTrail.Project(attachedBlockIndex, percentTowardNextBlock, direction, distanceToTravel,
                                                      out var newAttachedBlockIndex, out percentTowardNextBlock, out TrailFollowerDirection outDirection, out Vector3 course);
            if (newAttachedBlockIndex != attachedBlockIndex)
            {
                attachedBlockIndex = newAttachedBlockIndex;
                // PORT Deviation (V14, restore when GunVesselTransformer ports): ((GunVesselTransformer) vesselData.VesselTransformer).FinalBlockSlideEffects();
            }
            vesselData.Course = course;

            if (outDirection != direction)
            {
                // Ping ponged
                // TODO: Probably need to do other stuff here
                direction = outDirection;
            }
        }

        public void SetDirection(TrailFollowerDirection direction)
        {
            if (this.direction == direction) return;

            this.direction = direction;
            percentTowardNextBlock = 1 - percentTowardNextBlock;

            if (this.direction == TrailFollowerDirection.Forward) attachedBlockIndex--;
            else attachedBlockIndex++;
        }

        // PORT Deviation (V14, restore when Prism ports): float GetTerrainAwareBlockSpeed(Prism prism)
        float GetTerrainAwareBlockSpeed(MonoBehaviour prism)
        {
            // PORT Deviation (V14, restore when Prism ports): if (prism.destroyed)
            // PORT Deviation (V14, restore when Prism ports):     return DestroyedTerrainSpeed;

            // PORT Deviation (V14, restore when Prism ports): if (prism.Domain == domain)
            // PORT Deviation (V14, restore when Prism ports):     return FriendlyTerrainSpeed;

            return HostileTerrainSpeed;
        }
    }
}
