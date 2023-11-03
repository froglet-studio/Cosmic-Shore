using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core
{
    public class Trail
    {
        // TODO: fix bouncing between nearby two block when isLoop true
        bool isLoop;
        public List<TrailBlock> TrailList { get; }
        Dictionary<TrailBlock, int> trailBlockIndices;

        public Trail(bool isLoop = false)
        {
            this.isLoop = isLoop;
            TrailList = new List<TrailBlock>();
            trailBlockIndices = new Dictionary<TrailBlock, int>();
        }

        public void Add(TrailBlock block)
        {
            trailBlockIndices.Add(block, TrailList.Count);
            TrailList.Add(block);
            block.Index = block.TrailBlockProperties.Index = trailBlockIndices.Count;
        }

        public int GetBlockIndex(TrailBlock block)
        {
            return trailBlockIndices[block];
        }

        /// <summary>
        /// Look Ahead
        /// Looking ahead of the trail
        /// // TODO: Could use some generalized methods because Look Ahead logically similar to Project method
        /// </summary>
        /// <param name="index"></param>
        /// <param name="lerp"></param>
        /// <param name="direction"></param>
        /// <param name="distance"></param>
        /// <returns></returns>
        public List<TrailBlock> LookAhead(int index, float lerp, TrailFollowerDirection direction, float distance)
        {
            var incrementor = (int) direction;   // Fun bit of cleverness, enum forward is 1 and backward is -1
            var distanceTravelled = 0f;
            
            var trailListCount = TrailList.Count;
            (index, incrementor) = IndexSafetyCheck(index, incrementor,trailListCount);
            var currentBlock = TrailList[index];
            
            var nextIndex = index + incrementor;
            (nextIndex, incrementor) = IndexSafetyCheck(nextIndex, incrementor,trailListCount);
            var nextBlock = TrailList[nextIndex];
            
            var lookAheadBlocks = new List<TrailBlock> { currentBlock };

            var distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position) * (1 - lerp);

            while (distanceTravelled < distance)
            {
                distanceTravelled += distanceToNextBlock;

                lookAheadBlocks.Add(nextBlock);
                
                // Get current block
                index += incrementor;
                (index, incrementor) = IndexSafetyCheck(index, incrementor, trailListCount);
                // Debug.LogWarningFormat("{0} - {1} - {2}: {3}", nameof(Trail), nameof(LookAhead), nameof(index), index.ToString());
                currentBlock = TrailList[index];
                
                // Detect direction and change incrementor
                // if(!isLoop) incrementor *= -1;

                // Get the next block
                nextIndex = index + incrementor;
                (nextIndex, incrementor) = IndexSafetyCheck(nextIndex, incrementor,trailListCount);
                nextBlock = TrailList[nextIndex];
                // Debug.LogWarningFormat("{0} - {1} - {2}: {3}", nameof(Trail), nameof(LookAhead), nameof(nextIndex),nextIndex.ToString());
               
                
                distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position);
            }

            return lookAheadBlocks;
        }

        /// <summary>
        /// Project on Trail
        /// // TODO: Please give a more descriptive name to the method if possible
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
            int incrementor = (int)direction;   // Fun bit of cleverness, enum forward is 1 and backward is -1
            var distanceTravelled = 0f;
            var trailListCount = TrailList.Count;

            (startIndex, incrementor) = IndexSafetyCheck(startIndex, incrementor,trailListCount);
            var currentBlock = TrailList[startIndex];
            
            var nextIndex = startIndex + incrementor;
            (nextIndex, incrementor) = IndexSafetyCheck(nextIndex, incrementor , trailListCount);
            var nextBlock = TrailList[nextIndex];
            
            //Debug.Log($"Project: {currentBlock.transform.position},{nextBlock.transform.position}");

            var distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position) * (1 - initialLerp);
            distanceTravelled += distanceToNextBlock;
            //Debug.Log($"Project - distances: {distance},{distanceToNextBlock}");

            while (distanceTravelled < distance)
            {
                
                // Get the current block
                startIndex += incrementor;
                (startIndex, incrementor) = IndexSafetyCheck(startIndex, incrementor, trailListCount);
                // Debug.LogWarningFormat("{0} - {1} - {2}: {3}", nameof(Trail), nameof(Project), nameof(startIndex),startIndex.ToString());
                
                // Detect next block's direction
                // if(!isLoop) incrementor *= -1;
                
                // Get the next block
                nextIndex += incrementor;
                (nextIndex, incrementor) = IndexSafetyCheck(nextIndex, incrementor,trailListCount);
                nextBlock = TrailList[nextIndex];
                // Debug.LogWarningFormat("{0} - {1} - {2}: {3}", nameof(Trail), nameof(Project), nameof(nextIndex),nextIndex.ToString());
                
                
                // Calculate distance to the next block
                distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position);
                distanceTravelled += distanceToNextBlock;
            }

            var overflow = distanceTravelled - distance;

            // OUT PARAMETERS
            var nextPosition = nextBlock.transform.position;
            var currentPosition = currentBlock.transform.position;
            
            heading = (nextPosition - currentPosition).normalized;
            endIndex = startIndex;
            finalLerp = 1 - (overflow / (currentPosition - nextPosition).magnitude);
            outDirection = (TrailFollowerDirection) incrementor;

            return Vector3.Lerp(currentPosition, nextPosition, finalLerp);
        }

        /// <summary>
        /// Index Safety Check
        /// Make sure the index is not out of range
        /// </summary>
        /// <param name="index">Block index on trail</param>
        /// <param name="maxRange">The maximum trail list range</param>
        /// <returns></returns>
        private (int, int) IndexSafetyCheck(int index, int incrementor, in int maxRange)
        {
            // This makes sure the index is not out of maximum range
            if (index >= maxRange)
            {
                index %= maxRange;
                // If the trail is not looping, change the ship's direction
                if (!isLoop) incrementor *= -1;
            }
            
            // When index is less than 0
            if (index < 0)
            {
                // If the trail is looping, connect the tail block's index to current index
                if (isLoop) index += maxRange;
                // If the trail is not looping, change ship direction and reset index to start
                else {incrementor *= -1;
                    index = 0;
                }
            }
            // if(index >= maxRange)
            
            return (index, incrementor);
        }

        // TODO: bounds checking
        public TrailBlock GetBlock(int blockIndex)
        {
            if (blockIndex < 0) return TrailList[0];
            return TrailList[blockIndex];
        }
    }
}