using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core
{
    public class Trail
    {
        readonly bool isLoop;
        List<TrailBlock> trailList;
        Dictionary<TrailBlock, int> trailBlockIndices;

        public Trail(bool isLoop = false)
        {
            this.isLoop = isLoop;

            // TODO: maybe circular list is not needed
            if (isLoop)
                trailList = new CircularList<TrailBlock>();
            else
                trailList = new List<TrailBlock>();
            

            trailBlockIndices = new Dictionary<TrailBlock, int>();
        }

        public void Add(TrailBlock block)
        {
            trailBlockIndices.Add(block, trailList.Count);
            trailList.Add(block);
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
            int incrementor = (int) direction;   // Fun bit of cleverness, enum forward is 1 and backward is -1
            var distanceTravelled = 0f;
            var currentBlock = trailList[index];
            var nextBlock = trailList[index + incrementor];
            var lookAheadBlocks = new List<TrailBlock> { currentBlock };

            var distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position) * (1 - lerp);

            while (distanceTravelled < distance)
            {
                distanceTravelled += distanceToNextBlock;

                lookAheadBlocks.Add(nextBlock);

                index += incrementor;
                if (index >= trailList.Count-1 || index <= 0) // End of trail encountered
                {
                    if (isLoop)
                        index %= trailList.Count;
                    else
                        incrementor *= -1;
                }

                currentBlock = trailList[index];
                nextBlock = trailList[index + incrementor];

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
            var currentBlock = trailList[startIndex];
            var nextBlock = trailList[startIndex + incrementor];

            //Debug.Log($"Project: {currentBlock.transform.position},{nextBlock.transform.position}");

            var distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position) * (1 - initialLerp);
            distanceTravelled += distanceToNextBlock;
            //Debug.Log($"Project - distances: {distance},{distanceToNextBlock}");

            while (distanceTravelled < distance)
            {
                distanceTravelled += distanceToNextBlock;

                startIndex += incrementor;
                if (startIndex >= trailList.Count - 1 || startIndex <= 0) // End of trail encountered
                {
                    if (isLoop)
                        startIndex %= trailList.Count;
                    else
                        incrementor *= -1;
                }

                currentBlock = trailList[startIndex];
                nextBlock = trailList[startIndex + incrementor];

                distanceToNextBlock = Vector3.Magnitude(nextBlock.transform.position - currentBlock.transform.position);
            }

            var overflow = distanceTravelled - distance;

            // OUT PARAMETERS
            heading = (currentBlock.transform.position - nextBlock.transform.position).normalized;
            endIndex = startIndex;
            finalLerp = 1 - (overflow / (currentBlock.transform.position - nextBlock.transform.position).magnitude);
            outDirection = (TrailFollowerDirection) incrementor;

            return Vector3.Lerp(currentBlock.transform.position, nextBlock.transform.position, finalLerp);
        }

        // TODO: bounds checking
        public TrailBlock GetBlock(int blockIndex)
        {
            return trailList[blockIndex];
        }
    }

    class CircularList<T> : List<T>
    {
        int Index;

        public CircularList() : this(0) { }

        public CircularList(int index)
        {
            if (index < 0 || index >= Count)
                throw new Exception(string.Format("Index must between {0} and {1}", 0, Count));

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