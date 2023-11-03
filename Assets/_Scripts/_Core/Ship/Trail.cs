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

            // TODO: maybe circular list is not needed
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
        /// 
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
            index = IndexSafetyCheck(index, trailListCount);
            var currentBlock = TrailList[index];
            
            var nextIndex = index + incrementor;
            nextIndex = IndexSafetyCheck(nextIndex, trailListCount);
            var nextBlock = TrailList[nextIndex];
            
            var lookAheadBlocks = new List<TrailBlock> { currentBlock };

            var distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position) * (1 - lerp);

            while (distanceTravelled < distance)
            {
                distanceTravelled += distanceToNextBlock;

                lookAheadBlocks.Add(nextBlock);
                
                // Get current block
                index += incrementor;
                index = IndexSafetyCheck(index, trailListCount);
                currentBlock = TrailList[index];
                
                // Detect direction and change incrementor
                if(!isLoop) incrementor += -1;

                nextIndex = index + incrementor;
                nextIndex = IndexSafetyCheck(nextIndex, trailListCount);
                nextBlock = TrailList[nextIndex];
                
               
                
                distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position);
            }

            return lookAheadBlocks;
        }

        /// <summary>
        /// 
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

            startIndex = IndexSafetyCheck(startIndex, trailListCount);
            var currentBlock = TrailList[startIndex];
            
            var nextIndex = startIndex + incrementor;
            nextIndex = IndexSafetyCheck(nextIndex, trailListCount);
            var nextBlock = TrailList[nextIndex];
            
            //Debug.Log($"Project: {currentBlock.transform.position},{nextBlock.transform.position}");

            var distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position) * (1 - initialLerp);
            distanceTravelled += distanceToNextBlock;
            //Debug.Log($"Project - distances: {distance},{distanceToNextBlock}");

            while (distanceTravelled < distance)
            {
                
                // Get the current block
                startIndex += incrementor;
                startIndex = IndexSafetyCheck(startIndex, trailListCount);
                
                // Detect next block's direction
                if(!isLoop) incrementor *= -1;
                
                // Get the next block
                nextIndex += incrementor;
                nextIndex = IndexSafetyCheck(nextIndex, trailListCount);
                nextBlock = TrailList[nextIndex];
                
                
                // Calculate distance to the next block
                distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position);
                distanceTravelled += distanceToNextBlock;
            }

            var overflow = distanceTravelled - distance;

            // OUT PARAMETERS
            heading = (nextBlock.transform.position - currentBlock.transform.position).normalized;
            endIndex = startIndex;
            finalLerp = 1 - (overflow / (currentBlock.transform.position - nextBlock.transform.position).magnitude);
            outDirection = (TrailFollowerDirection) incrementor;

            return Vector3.Lerp(currentBlock.transform.position, nextBlock.transform.position, finalLerp);
        }

        private int IndexSafetyCheck(int index, int maxRange)
        {
            index %= maxRange;
            if (index < 0)
            {
                if (isLoop) index += maxRange;
                // TODO: fix bouncing between nearby two block when isLoop true
                else index *= -1;
            }
            return index;
        }

        // TODO: bounds checking
        public TrailBlock GetBlock(int blockIndex)
        {
            if (blockIndex < 0) return TrailList[0];
            return TrailList[blockIndex];
        }
    }

    class CircularList<T> : List<T>
    {
        int Index;

        public CircularList() : this(0) { }

        public CircularList(int index)
        {
            if (index < 0 || index >= Count)
                throw new Exception(string.Format("Index must between {0} and {1}, index was {2}", 0, Count, index));

            Index = index;
        }

        public T Current()
        {
            return this[Index];
        }

        public T Next()
        {
            Index++;
            Index %= Count;

            return this[Index];
        }

        public T Previous()
        {
            Index--;
            if (Index < 0)
                Index = Count - 1;

            return this[Index];
        }

        public void Reset()
        {
            Index = 0;
        }

        public void MoveToEnd()
        {
            Index = Count - 1;
        }
    }
}