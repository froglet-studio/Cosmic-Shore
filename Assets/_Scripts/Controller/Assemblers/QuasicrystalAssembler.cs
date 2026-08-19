using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class QuasicrystalGrowthInfo : GrowthInfo
    {
        public QuasicrystalLatticeFrame Frame;
        public QuasicrystalEdge Edge;
        public QuasicrystalVertex Heart;
    }

    /// <summary>
    /// Shared lattice frame for one quasicrystal colony lineage. Created by the founder's
    /// assembler and passed by reference to every prism grown from it, so the whole colony
    /// agrees on a single mapping between the abstract quasilattice (integer Z^6 addresses)
    /// and world space, and on one occupancy registry.
    ///
    /// Occupancy is intentionally weak: an edge is only "taken" while its resident prism is
    /// alive. Mass is conserved - when fauna eat a strut, the edge frees and a reseeded
    /// survivor can regrow the wound (see AssembledFlora.ReseedBranches).
    /// </summary>
    public class QuasicrystalLatticeFrame
    {
        public Vector3 WorldOrigin;
        public Quaternion WorldRotation;
        public float WorldEdgeLength;   // world units per lattice edge

        /// <summary>The species' authored plain strut length (leafSize.x), stamped by the
        /// founder - shared here so a daughter's seed pose is pure frame arithmetic with
        /// no per-instance state to hand across (QuasicrystalAssembler.BuildSeed).</summary>
        public float PlainStrutLength = -1f;

        /// <summary>Clear radius a crystal needs around its heart vertex, world-absolute
        /// (crystals do not scale with the lattice). Stamped by the founder.</summary>
        public float HeartSeatInset;

        /// <summary>
        /// The cell this lattice was anchored in, stamped by the founding plant. The colony's
        /// claim book is cleared per cell (a Cell-Selector world swap must drop ITS lattices
        /// without silencing another cell's colony), and a frame is the only object that knows
        /// which cell it belongs to.
        /// </summary>
        public Cell Cell;

        /// <summary>World position of a lattice vertex. Derived, never stored: a vertex key
        /// is an exact integer address, so the position is one projection away and cannot
        /// drift out of step with the lattice that named it.</summary>
        public Vector3 VertexWorld(QuasicrystalVertex v) =>
            WorldOrigin + WorldRotation * (QuasicrystalLatticeData.ParUnit(v) * WorldEdgeLength);

        struct Claim
        {
            public QuasicrystalAssembler Occupant;
            public int FrameCount;
            public bool Resident;
        }

        readonly Dictionary<QuasicrystalEdge, Claim> occupied = new();

        public bool IsOccupied(QuasicrystalEdge key)
        {
            if (!occupied.TryGetValue(key, out var claim)) return false;

            if (claim.Resident)
            {
                if (claim.Occupant) return true;
                occupied.Remove(key);   // resident was consumed - the edge frees
                return false;
            }

            // Reservations only hold for the frame they were made in. AssembledFlora.Grow
            // may discard a CanGrow result (random skip), so an unconsumed reservation
            // must not block the edge forever.
            if (Time.frameCount == claim.FrameCount) return true;
            occupied.Remove(key);
            return false;
        }

        /// <summary>Same-frame hold on a growth edge, placed when GetGrowthInfo returns it.</summary>
        public void Reserve(QuasicrystalEdge key, QuasicrystalAssembler by) =>
            occupied[key] = new Claim { Occupant = by, FrameCount = Time.frameCount, Resident = false };

        /// <summary>Permanent (while alive) claim, placed once the prism actually exists.</summary>
        public void Settle(QuasicrystalEdge key, QuasicrystalAssembler resident) =>
            occupied[key] = new Claim { Occupant = resident, Resident = true };
    }

    /// <summary>
    /// Grows the icosahedral quasicrystal - the Ammann-Kramer-Neri tiling's vertex-and-edge
    /// graph, cut-and-projected from Z^6 - incrementally, strut by strut, as a flora.
    ///
    /// <para>A prism is one EDGE of the quasilattice: a strut whose local +x runs along one
    /// of the six icosahedral vertex axes. Every strut has exactly the same length (a
    /// theorem of the projection, asserted by the measuring script), so a single authored
    /// leaf serves the whole species; a strut with a HEART at one end holds back by the
    /// assembler's <see cref="heartSeatInset"/> so the plant's crystal sits in a clear
    /// twelve-ray alcove. Hearts are never adjacent (measured constant spacing 2.3840
    /// edges), so at most one end of any strut ever holds back.</para>
    ///
    /// <para>Territory is the tree rule (<see cref="QuasicrystalLatticeData.TryGetOwningHeart"/>):
    /// an edge belongs to the plant owning its canonical minus-end vertex, so every edge has
    /// exactly one designated layer and the colony's lattice is complete by construction -
    /// the measuring script's growth simulation asserts zero holes. Like the Schwarz P tile
    /// colony, the whole territory question is one line; unlike it there is no mirror
    /// transform anywhere, because Z^6 needs none (Docs/ECOSYSTEM.md 37).</para>
    /// </summary>
    public class QuasicrystalAssembler : Assembler
    {
        [Header("Quasilattice")]
        [Tooltip("World-space length of one lattice edge - the strut-to-strut repeat. The " +
                 "tiling has no subdivision levels (it is what it is), so this is the single " +
                 "scale dial; FloraVariantTuning.LatticeScale multiplies it per element.")]
        [SerializeField] float edgeLength = 24f;

        [Tooltip("Clear radius a plant's crystal needs around its heart vertex, in ABSOLUTE " +
                 "world units (crystal size is the platform's one level curve and does not " +
                 "scale with the lattice - Docs/ECOSYSTEM.md 33, 34.8). Struts touching a " +
                 "heart shorten to leave this seat; fitted by " +
                 "Tools/Build/fit_quasicrystal_strut_sizes.py.")]
        [SerializeField] float heartSeatInset = 2.6f;

        [Tooltip("Cross-structure clearance probe, as a fraction of the edge length. The " +
                 "nearest legitimate neighbouring strut midpoint is 0.526 edges away " +
                 "(adjacent axes meet at 63.43 degrees), so 0.2 keeps a 2.6x margin while " +
                 "still catching any duplicate, which lands at zero distance.")]
        [SerializeField] float overlapProbeScale = 0.2f;

        // Lattice state: programmed by the parent for grown prisms, built lazily for the seed.
        QuasicrystalLatticeFrame frame;
        QuasicrystalEdge edge;
        QuasicrystalVertex heart;

        // The species' authored PLAIN strut length (leafSize.x), pushed by
        // AssembledFlora.ApplyLatticeSpacing at every creation site. The pose math derives
        // the plain tip inset from it: inset = (edgeWorld - plainStrutLength) / 2.
        float plainStrutLength = -1f;

        int depth = -1;

        public override Prism Prism { get; set; }
        public override Spindle Spindle { get; set; }

        public override int Depth
        {
            get => depth;
            set => depth = value;
        }

        /// <summary>The colony lattice this prism belongs to. Null until seeded.</summary>
        public QuasicrystalLatticeFrame Frame => frame;

        /// <summary>The heart whose plant this prism belongs to.</summary>
        public QuasicrystalVertex Heart => heart;

        /// <summary>This prism's edge address on the lattice.</summary>
        public QuasicrystalEdge Edge => edge;

        /// <summary>
        /// Scales this element's whole lattice (FloraVariantTuning.LatticeScale). The
        /// quasilattice has no subdivision levels, so there is no argmin to keep invariant
        /// (the trap SchwarzPAssembler.ApplyLatticeScale exists to dodge) and no absolute
        /// coherence tolerance family to drag (the gyroid's 34.8 failure): the one scaled
        /// quantity is the edge length, and every other distance in the growth path is
        /// derived from it. The heart seat inset deliberately does NOT scale - crystals
        /// do not scale with the lattice.
        /// </summary>
        public void ApplyLatticeScale(float scale)
        {
            if (scale <= 0f) return;
            edgeLength *= scale;
        }

        /// <summary>
        /// Receives the species' authored plain strut length (leafSize.x) from the flora.
        /// Must be pushed at every creation site (AssembledFlora.ApplyLatticeSpacing does),
        /// because the strut pose math needs it BEFORE the first growth probe - and a
        /// heart-adjacent prism re-stamps its own length from it.
        /// </summary>
        public void ConfigureStrutLength(float authoredLeafX)
        {
            if (authoredLeafX > 0f)
            {
                plainStrutLength = authoredLeafX;
                if (frame != null && frame.PlainStrutLength <= 0f)
                    frame.PlainStrutLength = authoredLeafX;
            }
            if (frame != null) RestampStrut();
        }

        /// <summary>
        /// The visible span of an edge's strut: tip insets per endpoint (a heart endpoint
        /// holds back by the frame's HeartSeatInset so the crystal's alcove stays clear),
        /// centre at the middle of the visible span. Pure arithmetic on the address and the
        /// shared frame - the heartness of an endpoint is a closed-form window test, not a
        /// claim, so every plant computes the same span for the same edge whether or not
        /// the heart's plant exists yet.
        /// </summary>
        static void EdgeSpan(QuasicrystalLatticeFrame f, QuasicrystalEdge e,
                             out Vector3 centerWorld, out Quaternion rotationWorld, out float lengthWorld)
        {
            float edgeWorld = f.WorldEdgeLength;
            float plainLength = f.PlainStrutLength > 0f ? f.PlainStrutLength : edgeWorld;
            float plainInset = Mathf.Max(0f, (edgeWorld - plainLength) * 0.5f);
            float tipA = QuasicrystalLatticeData.IsHeart(e.MinusEnd)
                ? Mathf.Max(f.HeartSeatInset, plainInset) : plainInset;
            float tipB = QuasicrystalLatticeData.IsHeart(e.PlusEnd)
                ? Mathf.Max(f.HeartSeatInset, plainInset) : plainInset;

            lengthWorld = Mathf.Max(0.1f, edgeWorld - tipA - tipB);

            Vector3 axis = QuasicrystalLatticeData.AxisUnit(e.Axis);
            Vector3 centerLattice = QuasicrystalLatticeData.ParUnit(e.MinusEnd) +
                                    axis * ((tipA + (edgeWorld - tipB)) / (2f * edgeWorld));
            centerWorld = f.WorldOrigin + f.WorldRotation * (centerLattice * edgeWorld);
            rotationWorld = f.WorldRotation * QuasicrystalLatticeData.StrutRotationLocal(e.Axis);
        }

        /// <summary>
        /// Re-stamps this prism's own strut length from the lattice: a heart-adjacent strut
        /// is shorter than the authored plain leaf (its centre was already posed on the
        /// shifted span by GetGrowthInfo/BuildSeed). Runs before Initialize on every grown
        /// and seeded prism; only the founder's lands at its first growth probe, mid-bloom,
        /// where the re-target is carried by the ordinary grow clock. The cross-section
        /// axes keep whatever the flora stamped (the authored per-element identity).
        /// </summary>
        void RestampStrut()
        {
            if (frame == null || !Prism) return;
            EdgeSpan(frame, edge, out _, out _, out float lengthWorld);
            Vector3 scale = Prism.TargetScale;
            if (Mathf.Approximately(scale.x, lengthWorld)) return;
            scale.x = lengthWorld;
            Prism.AdmitTargetScale(scale);
            Prism.TargetScale = scale;
        }

        /// <summary>Builds the growth order that seeds a NEW plant on <paramref name="heart"/>
        /// of an existing lattice: its first prism is the edge running from the heart along
        /// axis 0, whose canonical minus-end IS the heart - so it is always this plant's own
        /// edge, and a heart is always twelve-coordinated, so the edge always exists. The
        /// pose is derived at birth from the shared frame's arithmetic; there is nothing to
        /// transcribe (the failure class the gyroid's baked seed poses paid five playtests
        /// for cannot arise - Docs/ECOSYSTEM.md 32.7, 34.6).</summary>
        public static QuasicrystalGrowthInfo BuildSeed(
            QuasicrystalLatticeFrame frame, QuasicrystalVertex heart, int depth)
        {
            if (frame == null) return null;

            var seedEdge = new QuasicrystalEdge(heart, 0);
            EdgeSpan(frame, seedEdge, out var center, out var rotation, out _);

            return new QuasicrystalGrowthInfo
            {
                CanGrow = true,
                Position = center,
                Rotation = rotation,
                Depth = depth,
                Frame = frame,
                Edge = seedEdge,
                Heart = heart
            };
        }

        void Start()
        {
            if (!Prism) Prism = GetComponent<Prism>();
        }

        /// <summary>
        /// Copies the parent's lattice frame and this prism's address on it. Called by
        /// AssembledFlora.AssemblerFactory right after the new prism is instantiated.
        /// </summary>
        public void Program(QuasicrystalGrowthInfo info)
        {
            if (!Prism) Prism = GetComponent<Prism>();
            frame = info.Frame;
            edge = info.Edge;
            heart = info.Heart;
            depth = info.Depth;
            frame.Settle(edge, this);
            RestampStrut();
        }

        public override bool IsFullyBonded()
        {
            if (frame == null) return false;

            for (int end = 0; end < 2; end++)
            {
                var v = end == 0 ? edge.MinusEnd : edge.PlusEnd;
                for (int axis = 0; axis < QuasicrystalLatticeData.AxisCount; axis++)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        var candidate = CanonicalEdge(v, axis, sign);
                        if (candidate.Equals(edge)) continue;
                        if (!IsMineToLay(candidate)) continue;
                        if (!frame.IsOccupied(candidate)) return false;
                    }
                }
            }
            return true;
        }

        public override void StartBonding()
        {
            // Flora growth drives this assembler exclusively through GetGrowthInfo; there
            // is no free-prism recruitment phase like the gyroid's mate search.
        }

        static QuasicrystalEdge CanonicalEdge(QuasicrystalVertex v, int axis, int sign) =>
            sign > 0 ? new QuasicrystalEdge(v, axis)
                     : new QuasicrystalEdge(v.Step(axis, -1), axis);

        /// <summary>
        /// ONE PLANT = ONE HEART'S TREE. An edge is this plant's to lay iff its canonical
        /// minus-end vertex belongs to this plant's heart - the whole territory question,
        /// one line, mirroring the Schwarz P tile test (Docs/ECOSYSTEM.md 34.6). Both
        /// endpoints must be accepted for the edge to exist at all.
        /// </summary>
        bool IsMineToLay(QuasicrystalEdge candidate)
        {
            if (!QuasicrystalLatticeData.IsAccepted(candidate.MinusEnd)) return false;
            if (!QuasicrystalLatticeData.IsAccepted(candidate.PlusEnd)) return false;
            return QuasicrystalLatticeData.TryGetOwningHeart(candidate.MinusEnd, out var owner) &&
                   owner.Equals(heart);
        }

        public override GrowthInfo GetGrowthInfo()
        {
            if (!Prism) Prism = GetComponent<Prism>();
            EnsureSeeded();

            // Start the sweep at a per-prism offset so the whole plant does not prefer the
            // same axis and grow a directional tendril. Deterministic (no RNG), so a prism
            // re-probed after a graze offers its edges in the same order.
            int start = SweepStartIndex();

            for (int n = 0; n < 24; n++)
            {
                int slot = (start + n) % 24;
                var v = slot < 12 ? edge.MinusEnd : edge.PlusEnd;
                int axis = (slot % 12) / 2;
                int sign = (slot % 2) == 0 ? 1 : -1;

                var candidate = CanonicalEdge(v, axis, sign);
                if (candidate.Equals(edge)) continue;
                if (!IsMineToLay(candidate)) continue;
                if (frame.IsOccupied(candidate)) continue;

                EdgeSpan(frame, candidate, out var center, out var rotation, out _);

                // World-space occupancy via PrismSpatialIndex.TryReserve - catches foreign
                // geometry the lattice registry above can't know about (vessel trails, other
                // floras, environment prisms). The lattice registry stays as this colony's
                // own bookkeeping; the spatial index is the cross-structure authority. Not
                // Physics.CheckBox, which is structurally blind to prisms inside their 0.6s
                // disabled-collider spawn window - see Docs/SPATIAL_INDEX.md. Purely
                // proportional to the edge length, so it stays correct at any LatticeScale.
                float clearRadius = overlapProbeScale * frame.WorldEdgeLength;
                var spatialIndex = PrismSpatialIndex.EnsureInstance();
                if (spatialIndex != null && spatialIndex.IsAvailable &&
                    !spatialIndex.TryReserve(center, clearRadius))
                    continue;

                frame.Reserve(candidate, this);   // upgraded to Settle by the child's Program

                return new QuasicrystalGrowthInfo
                {
                    CanGrow = true,
                    Position = center,
                    Rotation = rotation,
                    Depth = depth - 1,
                    Frame = frame,
                    Edge = candidate,
                    Heart = heart
                };
            }

            // Nothing viable this cycle. Edges are re-probed live each call, so a branch
            // culled here can still heal a wound later when ReseedBranches re-activates it
            // after fauna consume a neighbour (mass is conserved - no decay, only grazing).
            return new GrowthInfo { CanGrow = false };
        }

        int SweepStartIndex()
        {
            unchecked
            {
                int hash = edge.MinusEnd.GetHashCode() * 31 + edge.Axis * 15485863;
                return (hash & 0x7fffffff) % 24;
            }
        }

        /// <summary>
        /// Anchors a NEW colony lattice on this prism - the founder path. The seed takes the
        /// edge from the ORIGIN heart along axis 0 (the origin is a heart by construction of
        /// the measured window offset), and this transform orients the whole quasilattice:
        /// the prism's pose becomes the pose of that edge's strut.
        ///
        /// <para>Only a founder ever reaches this. A daughter is <see cref="Program"/>med
        /// with her mother's frame BY REFERENCE, and this early-returns on a non-null frame
        /// - so an entire colony shares one lattice, one world anchor and one occupancy
        /// book, and two plants cannot disagree about where a vertex is.</para>
        /// </summary>
        void EnsureSeeded()
        {
            if (frame != null) return;

            heart = QuasicrystalVertex.Zero;
            edge = new QuasicrystalEdge(heart, 0);

            var seededFrame = new QuasicrystalLatticeFrame
            {
                WorldRotation = transform.rotation *
                                Quaternion.Inverse(QuasicrystalLatticeData.StrutRotationLocal(0)),
                WorldEdgeLength = edgeLength,
                PlainStrutLength = plainStrutLength,
                HeartSeatInset = heartSeatInset
            };

            // The prism sits at the CENTRE of the seed edge's visible span (which is
            // heart-shifted - the minus end is the origin heart), so the origin is placed by
            // inverting the span arithmetic rather than assumed at the prism: with
            // WorldOrigin zero, EdgeSpan returns the rotated lattice offset of the span
            // centre, and subtracting it re-homes the origin on the prism.
            seededFrame.WorldOrigin = Vector3.zero;
            EdgeSpan(seededFrame, edge, out var centerAtOrigin, out _, out _);
            seededFrame.WorldOrigin = transform.position - centerAtOrigin;

            frame = seededFrame;
            frame.Settle(edge, this);
            RestampStrut();
        }

        /// <summary>
        /// A pure PREVIEW of the quasilattice this assembler grows - an aperiodic scaffold
        /// icon in the flora's own local space, spawning nothing. It is the real thing, not
        /// an impression: the same window test, the same integer addresses, the same span
        /// arithmetic the live frame uses. What it does NOT do is claim anything - occupancy
        /// and the cross-structure reservation are replaced by a local visited set, because
        /// a preview must not touch shared state. Growth is breadth-first, so a small budget
        /// reads as a patch of quasicrystal spreading from the seed star.
        /// </summary>
        public bool TryPreviewLattice(int budget, Vector3 tileScale, List<SpawnPoint> into,
                                      float latticeScale = 1f)
        {
            if (into == null || budget <= 0) return false;

            float scale = Mathf.Max(latticeScale, 1e-4f);
            var previewFrame = new QuasicrystalLatticeFrame
            {
                WorldOrigin = Vector3.zero,
                WorldRotation = Quaternion.identity,
                WorldEdgeLength = edgeLength * scale,
                PlainStrutLength = tileScale.x,
                HeartSeatInset = heartSeatInset
            };

            var seedEdge = new QuasicrystalEdge(QuasicrystalVertex.Zero, 0);
            var visited = new HashSet<QuasicrystalEdge> { seedEdge };
            var frontier = new Queue<QuasicrystalEdge>();
            frontier.Enqueue(seedEdge);
            AddPreviewPoint(previewFrame, seedEdge, tileScale, into);

            while (frontier.Count > 0 && into.Count < budget)
            {
                var current = frontier.Dequeue();
                for (int end = 0; end < 2 && into.Count < budget; end++)
                {
                    var v = end == 0 ? current.MinusEnd : current.PlusEnd;
                    for (int axis = 0; axis < QuasicrystalLatticeData.AxisCount && into.Count < budget; axis++)
                    {
                        for (int sign = -1; sign <= 1 && into.Count < budget; sign += 2)
                        {
                            var candidate = CanonicalEdge(v, axis, sign);
                            if (!visited.Add(candidate)) continue;
                            if (!QuasicrystalLatticeData.IsAccepted(candidate.MinusEnd) ||
                                !QuasicrystalLatticeData.IsAccepted(candidate.PlusEnd)) continue;
                            AddPreviewPoint(previewFrame, candidate, tileScale, into);
                            frontier.Enqueue(candidate);
                        }
                    }
                }
            }

            return into.Count > 1;
        }

        void AddPreviewPoint(QuasicrystalLatticeFrame f, QuasicrystalEdge e,
                             Vector3 tileScale, List<SpawnPoint> into)
        {
            EdgeSpan(f, e, out var center, out var rotation, out float lengthWorld);
            into.Add(new SpawnPoint(center, rotation,
                new Vector3(lengthWorld, tileScale.y, tileScale.z)));
        }
    }
}
