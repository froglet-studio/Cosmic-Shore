using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
using System;

namespace CosmicShore.Gameplay
{
    [System.Serializable]
    public class Trail
    {
        // [Fix] Added this field so Prism.cs can access/clear the visual line
        public TrailRenderer TrailRenderer;

        /// <summary>
        /// The DIMENSION of the prismscape this container holds, declared by whoever laid it.
        /// Defaults to <see cref="PrismscapeDimension.Trail"/> - the vessel wake, the class's
        /// namesake 1D ribbon. But `Trail` is also the general lay container
        /// (`PrismTrailBuilder` stamps `block.Trail` on every builder-laid prism), so a
        /// spawnable that borrows it for a 2D shell MUST declare that here
        /// (`SpawnableGyroid` / `SpawnableSchwarzPSurface` set `Surface`) - "has a Trail" is
        /// membership evidence, never shape evidence. Read through
        /// <c>PrismscapeTopology.DimensionOf</c>, which is what routes the Urchin's ride
        /// between sliding ALONG a ribbon and rolling ACROSS a shell.
        /// </summary>
        public PrismscapeDimension Dimension { get; set; } = PrismscapeDimension.Trail;

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

                // Re-read the segment's NEAR block. This line was missing for the walk's whole
                // life: currentBlock stayed pinned at the frame's ORIGINAL block while
                // nextBlock advanced, so any frame that crossed a boundary measured - and then
                // LERPED ALONG - a chord from the original block to the new next block, cutting
                // the corner at a parameter computed against the wrong length. The rider
                // visibly jumped toward another prism for exactly one frame and snapped back
                // when the next frame re-derived from (endIndex, finalLerp) - a jerk at
                // precisely the trail's block periodicity.
                currentBlock = TrailList[startIndex];

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
            endIndex = startIndex;
            finalLerp = 1 - overflow / gapMag;

            outDirection = (TrailFollowerDirection)incrementor;

            // The projected point rides a CATMULL-ROM arc through the block centres rather
            // than the raw polyline: a straight lerp is positionally continuous but kinks
            // DIRECTION at every block centre, which at speed reads as a tick at the trail's
            // block periodicity - the opposite of the rail-slide feel the 1D ride is for.
            // Bookkeeping (endIndex + finalLerp) stays segment-linear; only the returned
            // position and heading smooth. Outer control points clamp at an open ribbon's
            // ends (P0==P1 degrades the arc to the segment, which is correct there).
            Vector3 p0 = ControlBlockPosition(endIndex - incrementor, trailListCount);
            Vector3 p3 = ControlBlockPosition(nextIndex + incrementor, trailListCount);

            Vector3 tangent = CatmullRomTangent(p0, currentPosition, nextPosition, p3, finalLerp);
            heading = tangent.sqrMagnitude > 1e-10f
                ? tangent.normalized
                : (gapMag > 1e-5f ? blockGap / gapMag : Vector3.zero);

            return CatmullRom(p0, currentPosition, nextPosition, p3, finalLerp);
        }

        /// <summary>An OUTER spline control point: wraps on a loop, clamps on an open ribbon.</summary>
        Vector3 ControlBlockPosition(int index, int count)
        {
            if (isLoop)
            {
                index %= count;
                if (index < 0) index += count;
            }
            else
            {
                index = Mathf.Clamp(index, 0, count - 1);
            }
            return TrailList[index].transform.position;
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (p2 - p0) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        static Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * ((p2 - p0)
                + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t
                + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
        }

        private (int, int) IndexSafetyCheck(int index, int incrementor, int maxRange)
        {
            if (index >= maxRange)
            {
                if (isLoop)
                {
                    index %= maxRange;
                }
                else
                {
                    // REFLECT off the head, never wrap. The old `index %= maxRange` sent an
                    // overstep at the head back to index 0 - THE FAR TAIL - so a rider walking
                    // past the newest prism was handed a phantom segment spanning the whole
                    // trail as the crow flies. Project() then advanced finalLerp by
                    // 1/thatDistance per frame: the vessel inched along an invisible chord at
                    // a few hundredths of its speed, which plays as "attached but frozen".
                    // The bounce partner of stepping past count-1 is count-2, exactly as
                    // stepping below 0 bounces to the start.
                    incrementor *= -1;
                    index = Mathf.Max(0, maxRange - 2);
                }
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