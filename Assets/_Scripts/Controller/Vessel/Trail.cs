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

        /// <summary>
        /// The point on <paramref name="p"/> the RIDE follows: the block's centre corrected
        /// back onto the SPINE - the path the laying vessel's own centre actually flew - by
        /// subtracting the EXACT world-space offset the lay applied
        /// (<see cref="Prism.TrailLayOffset"/>, stamped by the spawner).
        ///
        /// Why the stamp, and never a reconstruction from the block's own geometry - both
        /// reconstructions have been tried here and both put the ride on a corkscrew:
        ///
        ///  * A line at fixed distance along the block's right (the centres, the inner edge)
        ///    inherits the layer's ROLL: each block is offset along the ship's right AT LAY
        ///    TIME, so a rolling layer (the Squirrel, constantly) braids each ribbon into a
        ///    HELIX around its flight path, radius ~10u and up. Riding it at speed IS
        ///    "orbiting like crazy".
        ///  * Undoing the offset along <c>block.right</c> fails differently: the offset rides
        ///    the SHIP's right, but the block's ROTATION can be a travel-aligned override
        ///    (drift and barrel-roll bridging prisms - `BlockRotationOverride`), so for
        ///    exactly the blocks a drifting Squirrel lays, `block.right` is the wrong axis
        ///    and the "recovered spine" bobs and swings ribbon to ribbon.
        ///
        /// The stamped vector is immune to both, and to width changes after the lay (the
        /// payoff GROWS ridden blocks - a recovery that reads the live width would shift
        /// under the rider as it pays). Both ribbons of a gapped pair map to the SAME spine,
        /// which is correct: the pair is one wake, and whichever ribbon you touch, the road
        /// is the path the vessel flew. Blocks with no stamp (spawnable lays, ungapped
        /// wakes) ride their centres.
        /// </summary>
        public Vector3 RidePoint(Prism p)
        {
            return p.TrailLayOffset.sqrMagnitude > 1e-8f
                ? p.transform.position - p.TrailLayOffset
                : p.transform.position;
        }

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
            // Un-stamp membership FIRST: a prism left pointing at a cleared container is a
            // lie ("member of an empty list") that the prismscape topology acts on - it reads
            // as a one-block prismscape and routes riders onto the surface follower. After
            // this, the prisms are honest container-less singletons and classify by census.
            for (int i = 0; i < TrailList.Count; i++)
            {
                var block = TrailList[i];
                if (block && block.Trail == this) block.AssignTrail(null);
            }

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
        /// A block the ride can traverse. A DESTROYED prism still qualifies - its object stays
        /// in place as a restorable skeleton, so the geometry is intact and the rider can
        /// (and does, via the payoff) restore it in passing. What does NOT qualify is a hole:
        /// a null entry (teardown), a pooled-away object (inactive), or a prism the pool has
        /// REUSED into a new life elsewhere (its membership stamp no longer names this trail -
        /// its transform is wherever its new life put it, and walking through it would fling
        /// the ride across the map). Holes are BRIDGED: the walks splice the segment across to
        /// the next survivor so the ride stays continuous over missing prisms. Membership
        /// matters now that trails OUTLIVE their vessel - a persistent trail's list can
        /// accumulate reused entries over a long session.
        /// </summary>
        bool IsRidable(Prism p) => p && p.gameObject.activeInHierarchy && p.Trail == this;

        /// <summary>
        /// Advance <paramref name="index"/> one RIDABLE block along <paramref name="incrementor"/>,
        /// skipping holes, with the same end semantics as <see cref="IndexSafetyCheck"/>
        /// (loops wrap, open ribbons reflect - a reflection flips the incrementor, which is
        /// how the follower's park logic hears about the end). False = no OTHER ridable block
        /// exists; index and incrementor are left untouched.
        /// </summary>
        bool TryStepRidable(ref int index, ref int incrementor, int count)
        {
            int origin = index;
            int idx = index, inc = incrementor;
            for (int hops = 0; hops < count; hops++)
            {
                idx += inc;
                (idx, inc) = IndexSafetyCheck(idx, inc, count);
                if (idx == origin) continue;   // bounced back onto the starting block
                if (IsRidable(TrailList[idx]))
                {
                    index = idx;
                    incrementor = inc;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// The ribbon's axis at <paramref name="index"/> in INDEX ORDER (toward the head),
        /// central difference over the neighbouring blocks. Zero when the trail is too short
        /// or the neighbourhood is missing. This is the stable facing reference the ride
        /// resolves throttle direction against.
        /// </summary>
        public Vector3 HeadingAt(int index)
        {
            int last = TrailList.Count - 1;
            if (last < 1) return Vector3.zero;
            index = Mathf.Clamp(index, 0, last);
            var a = TrailList[Mathf.Max(index - 1, 0)];
            var b = TrailList[Mathf.Min(index + 1, last)];
            if (!IsRidable(a) || !IsRidable(b)) return Vector3.zero;
            Vector3 h = RidePoint(b) - RidePoint(a);
            return h.sqrMagnitude > 1e-6f ? h.normalized : Vector3.zero;
        }

        /// <summary>
        /// Look Ahead
        /// Looking ahead of the trail. Bridges holes (see <see cref="IsRidable"/>): the
        /// returned list contains only traversable blocks, with missing prisms spliced over.
        /// Fewer than two entries means the ride has nowhere to go - the caller parks.
        /// </summary>
        public List<Prism> LookAhead(int index, float lerp, TrailFollowerDirection direction, float distance)
        {
            var incrementor = (int)direction;
            var distanceTravelled = 0f;
            var trailListCount = TrailList.Count;

            (index, incrementor) = IndexSafetyCheck(index, incrementor, trailListCount);
            if (!IsRidable(TrailList[index]))
            {
                lerp = 0f;
                if (!TryStepRidable(ref index, ref incrementor, trailListCount))
                    return new List<Prism>();
            }
            var currentBlock = TrailList[index];

            int nextIndex = index;
            if (!TryStepRidable(ref nextIndex, ref incrementor, trailListCount))
                return new List<Prism> { currentBlock };
            var nextBlock = TrailList[nextIndex];

            var lookAheadBlocks = new List<Prism> { currentBlock };
            var distanceToNextBlock = Vector3.Magnitude(RidePoint(nextBlock) - RidePoint(currentBlock)) * (1 - lerp);

            while (distanceTravelled < distance)
            {
                distanceTravelled += distanceToNextBlock;
                lookAheadBlocks.Add(nextBlock);

                currentBlock = nextBlock;
                if (!TryStepRidable(ref nextIndex, ref incrementor, trailListCount)) break;
                nextBlock = TrailList[nextIndex];

                distanceToNextBlock = Vector3.Magnitude(RidePoint(nextBlock) - RidePoint(currentBlock));
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

            // A hole at the ride's own index (the ridden prism's object was reclaimed): step
            // to the next survivor and restart the segment there. If the whole ribbon is
            // holes, park where the bookkeeping stands.
            if (!IsRidable(TrailList[startIndex]))
            {
                initialLerp = 0f;
                if (!TryStepRidable(ref startIndex, ref incrementor, trailListCount))
                {
                    endIndex = startIndex;
                    finalLerp = 0f;
                    outDirection = (TrailFollowerDirection)incrementor;
                    heading = Vector3.zero;
                    var pinned = TrailList[startIndex];
                    return pinned ? RidePoint(pinned) : Vector3.zero;
                }
            }
            var currentBlock = TrailList[startIndex];

            int nextIndex = startIndex;
            if (!TryStepRidable(ref nextIndex, ref incrementor, trailListCount))
            {
                // The only ridable block - park on it.
                endIndex = startIndex;
                finalLerp = 0f;
                outDirection = (TrailFollowerDirection)incrementor;
                heading = Vector3.zero;
                return RidePoint(currentBlock);
            }
            var nextBlock = TrailList[nextIndex];

            var distanceToNextBlock = Vector3.Magnitude(RidePoint(nextBlock) - RidePoint(currentBlock)) * (1 - initialLerp);
            distanceTravelled += distanceToNextBlock;

            while (distanceTravelled < distance)
            {
                // Advance the segment: the far block becomes the near block. (The near block
                // failing to advance was the historic per-prism jerk: any frame that crossed a
                // boundary lerped along a chord from the frame's ORIGINAL block, cutting the
                // corner, and snapped back next frame - at exactly the trail's block
                // periodicity.) Steps bridge holes, so a destroyed-and-reclaimed prism splices
                // out of the ribbon instead of breaking the walk.
                startIndex = nextIndex;
                currentBlock = nextBlock;

                if (!TryStepRidable(ref nextIndex, ref incrementor, trailListCount))
                {
                    // Ran out of ridable blocks mid-walk - park on the last survivor.
                    endIndex = startIndex;
                    finalLerp = 0f;
                    outDirection = (TrailFollowerDirection)incrementor;
                    heading = Vector3.zero;
                    return RidePoint(currentBlock);
                }
                nextBlock = TrailList[nextIndex];

                distanceToNextBlock = Vector3.Magnitude(RidePoint(nextBlock) - RidePoint(currentBlock));
                distanceTravelled += distanceToNextBlock;
            }

            var overflow = distanceTravelled - distance;
            var nextPosition = RidePoint(nextBlock);
            var currentPosition = RidePoint(currentBlock);
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
            Vector3 p0 = ControlBlockPosition(endIndex - incrementor, trailListCount, currentPosition);
            Vector3 p3 = ControlBlockPosition(nextIndex + incrementor, trailListCount, nextPosition);

            Vector3 tangent = CatmullRomTangent(p0, currentPosition, nextPosition, p3, finalLerp);
            heading = tangent.sqrMagnitude > 1e-10f
                ? tangent.normalized
                : (gapMag > 1e-5f ? blockGap / gapMag : Vector3.zero);

            return CatmullRom(p0, currentPosition, nextPosition, p3, finalLerp);
        }

        /// <summary>
        /// An OUTER spline control point: wraps on a loop, clamps on an open ribbon, and falls
        /// back to the adjacent segment endpoint when the control block is a hole - degrading
        /// the arc toward the segment locally, which keeps the ride smooth over missing prisms.
        /// </summary>
        Vector3 ControlBlockPosition(int index, int count, Vector3 fallback)
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
            var block = TrailList[index];
            return IsRidable(block) ? RidePoint(block) : fallback;
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