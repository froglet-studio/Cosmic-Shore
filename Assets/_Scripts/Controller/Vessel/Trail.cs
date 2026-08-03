using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;
using System;

namespace CosmicShore.Gameplay
{
    [System.Serializable]
    public class Trail
    {
        // [Fix] Added this field so Prism.cs can access/clear the visual line
        public TrailRenderer TrailRenderer;

        bool isLoop;
        public List<Prism> TrailList { get; }
        Dictionary<Prism, ushort> trailBlockIndices;

        public Trail(bool isLoop = false)
        {
            this.isLoop = isLoop;
            TrailList = new List<Prism>();
            trailBlockIndices = new Dictionary<Prism, ushort>();
        }

        public void Add(Prism block)
        {
            if (trailBlockIndices.ContainsKey(block))
            {
                CSDebug.LogWarning($"[Trail] Attempted to add duplicate block {block.name}. Ignoring.");
                return;
            }
            trailBlockIndices.Add(block, (ushort)TrailList.Count);
            TrailList.Add(block);
            
            // Note: If you need to access prismProperties, ensure it's initialized on the block
            // block.prismProperties.Index = (ushort) trailBlockIndices.Count;
        }

        /// <summary>
        /// Detach the OLDEST prism from the trail and hand it back to the caller, keeping the
        /// index map consistent (every survivor shifts one toward the head, so
        /// <see cref="GetBlockIndex"/> and <see cref="GetBlock"/> stay in agreement for anything
        /// riding the trail).
        ///
        /// <b>This is not a general-purpose trail cap.</b> Passive removal of trail mass is
        /// forbidden platform-wide (CLAUDE.md ▸ <i>Mass is conserved</i> / <i>Don't cheat
        /// emergence</i>; the reverted <c>maxTrailBlocks</c> ring buffer is the named
        /// counter-example). The ONE caller is <see cref="WanderwayRun"/>'s rolling tether, an
        /// explicitly authorized carve-out for the Wanderway's infinite-runner illusion — see the
        /// exception recorded in <c>Docs/ECOSYSTEM.md</c> §0. Do not call it from anywhere else,
        /// and do not generalise it into a length limit on <see cref="Add"/>.
        /// </summary>
        /// <returns>The removed prism, or null when the trail is empty.</returns>
        public Prism RemoveOldest()
        {
            if (TrailList.Count == 0) return null;

            var oldest = TrailList[0];
            TrailList.RemoveAt(0);
            if (oldest) trailBlockIndices.Remove(oldest);

            for (int i = 0; i < TrailList.Count; i++)
            {
                var block = TrailList[i];
                if (block) trailBlockIndices[block] = (ushort)i;
            }

            OnOldestRemoved?.Invoke();
            return oldest;
        }

        /// <summary>
        /// Raised after <see cref="RemoveOldest"/> has shifted every surviving prism one slot
        /// toward the head. Anything holding a CACHED index into this trail (rather than a prism
        /// reference) must decrement it here or it will silently start pointing at a prism further
        /// along the ribbon — <see cref="TrailFollower"/> caches exactly such an index and advances
        /// it itself, so it rides this event.
        /// </summary>
        public event Action OnOldestRemoved;

        public void Clear()
        {
            TrailList.Clear();
            trailBlockIndices.Clear();
            
            // [Fix] Clear visual trail when logical trail clears
            if (TrailRenderer) TrailRenderer.Clear();
        }

        public int GetBlockIndex(Prism block)
        {
            if (!block || !trailBlockIndices.TryGetValue(block, out var index)) return -1;
            return index;
        }

        /// <summary>
        /// Look Ahead
        /// Looking ahead of the trail
        /// </summary>
        public List<Prism> LookAhead(int index, float lerp, TrailFollowerDirection direction, float distance)
        {
            var incrementor = (int)direction;
            var distanceTravelled = 0f;
            var trailListCount = TrailList.Count;

            (index, incrementor) = IndexSafetyCheck(index, incrementor, trailListCount);
            var currentBlock = TrailList[index];

            var nextIndex = index + incrementor;
            (nextIndex, incrementor) = IndexSafetyCheck(nextIndex, incrementor, trailListCount);
            var nextBlock = TrailList[nextIndex];

            var lookAheadBlocks = new List<Prism> { currentBlock };
            var distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position) * (1 - lerp);

            while (distanceTravelled < distance)
            {
                distanceTravelled += distanceToNextBlock;
                lookAheadBlocks.Add(nextBlock);

                index += incrementor;
                (index, incrementor) = IndexSafetyCheck(index, incrementor, trailListCount);
                currentBlock = TrailList[index];

                nextIndex = index + incrementor;
                (nextIndex, incrementor) = IndexSafetyCheck(nextIndex, incrementor, trailListCount);
                nextBlock = TrailList[nextIndex];

                distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position);
            }

            return lookAheadBlocks;
        }
        
        /// <summary>
        /// Project on Trail
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="initialLerp">Percent progress between current block and next block along direction</param>
        /// <param name="direction"></param>
        /// <param name="distance"></param>
        /// <param name="endIndex"></param>
        /// <param name="finalLerp"></param>
        /// <param name="outDirection"></param>
        /// <returns>The resultant position in space from the projection down the trail</returns>
        public Vector3 Project(int startIndex, float initialLerp, TrailFollowerDirection direction, float distance,
                               out int endIndex, out float finalLerp, out TrailFollowerDirection outDirection, out Vector3 heading)
        {
            int incrementor = (int)direction;
            var distanceTravelled = 0f;
            var trailListCount = TrailList.Count;

            (startIndex, incrementor) = IndexSafetyCheck(startIndex, incrementor, trailListCount);
            var currentBlock = TrailList[startIndex];

            var nextIndex = startIndex + incrementor;
            (nextIndex, incrementor) = IndexSafetyCheck(nextIndex, incrementor, trailListCount);
            var nextBlock = TrailList[nextIndex];

            var distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position) * (1 - initialLerp);
            distanceTravelled += distanceToNextBlock;

            while (distanceTravelled < distance)
            {
                startIndex += incrementor;
                (startIndex, incrementor) = IndexSafetyCheck(startIndex, incrementor, trailListCount);

                nextIndex += incrementor;
                (nextIndex, incrementor) = IndexSafetyCheck(nextIndex, incrementor, trailListCount);
                nextBlock = TrailList[nextIndex];

                distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position);
                distanceTravelled += distanceToNextBlock;
            }

            var overflow = distanceTravelled - distance;
            var nextPosition = nextBlock.transform.position;
            var currentPosition = currentBlock.transform.position;
            Vector3 blockGap = nextPosition - currentPosition;
           
            float gapMag = blockGap.magnitude; // one sqrt, reused by heading + finalLerp
            heading = gapMag > 1e-5f ? blockGap / gapMag : Vector3.zero; // matches Vector3.normalized's 1e-5 zero-threshold
            endIndex = startIndex;
            finalLerp = 1 - overflow / gapMag;

            outDirection = (TrailFollowerDirection)incrementor;

            return Vector3.Lerp(currentPosition, nextPosition, finalLerp);
        }

        private (int, int) IndexSafetyCheck(int index, int incrementor, int maxRange)
        {
            if (index >= maxRange)
            {
                index %= maxRange;
                if (!isLoop) incrementor *= -1;
            }

            if (index < 0)
            {
                // If the trail is looping, connect the tail block's index to current index
                if (isLoop) index += maxRange;
                // If the trail is not looping, change vessel direction and reset index to start
                else
                {
                    incrementor *= -1;
                    index = 0;
                }
            }

            return (index, incrementor);
        }

        public Prism GetBlock(int blockIndex)
        {
            if (blockIndex < 0) return TrailList[0];
            return TrailList[blockIndex];
        }
    }
}