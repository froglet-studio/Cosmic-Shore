using System;
using System.Collections.Generic;

namespace CosmicShore.Engine
{
    /// <summary>
    /// Contact resolution, limited to what the game needs (C3 census, Port/docs/ARCHITECTURE.md
    /// §13): a DYNAMIC Rigidbody's solid SphereCollider against any solid collider. In
    /// Assets/_Scripts that is the Astro League ball, against vessel hulls (spheres, oriented
    /// boxes, Rhino's capsule; every hull is on a kinematic body) and against other balls. This
    /// is not a general solver, and must not grow into one: a dynamic body whose solid collider
    /// is not a sphere is reported once and gets no contacts (bind a library behind this API
    /// instead — ARCHITECTURE_REVIEW E13).
    ///
    /// Per fixed step, after rigidbody integration and in the same pass as the triggers (the
    /// original engine's Physics.Simulate):
    ///   1. Pairs: a dynamic sphere and a solid collider whose AABBs touch (the trigger pass's
    ///      sweep), minus pairs the layer collision matrix, <see cref="Physics.IgnoreCollision"/>
    ///      or either collider's <see cref="Collider.excludeLayers"/> rule out
    ///      (<see cref="Collider.includeLayers"/> overrides the matrix).
    ///   2. Resolve, in (i, j) registration order: push the sphere out along the contact normal,
    ///      then cancel the approaching normal velocity with restitution e = the combined
    ///      bounciness (zero below <see cref="Physics.bounceThreshold"/>) and Coulomb friction on
    ///      the tangential velocity from the combined dynamic friction. A kinematic or static
    ///      collider has infinite mass and, as in PhysX, zero velocity (bodies moved through the
    ///      transform give the solver no velocity). Two dynamic spheres exchange a mass-weighted
    ///      impulse.
    ///   3. After the trigger messages: OnCollisionExit, Enter, then Stay, with the same pair
    ///      bookkeeping as triggers. The callbacks see the RESOLVED velocity, as in the original
    ///      engine (OnCollisionEnter runs after the solver).
    /// </summary>
    public sealed partial class TriggerPass
    {
        // Per live collider: its dynamic body when it is a solid sphere on a non-kinematic Rigidbody.
        Rigidbody[] _bodies = Array.Empty<Rigidbody>();
        bool _anyDynamic;
        readonly List<long> _contactCandidates = new();

        readonly List<(Collider a, Collider b)> _contactPairs = new();
        readonly HashSet<(Collider a, Collider b)> _contactSet = new();
        readonly HashSet<(Collider a, Collider b)> _contactCurrent = new();
        readonly List<ContactRecord> _contactsThisStep = new();
        static readonly HashSet<Type> s_warnedUnsupported = new();

        /// <summary>One resolved contact this step: the data both sides' Collision is built from.</summary>
        struct ContactRecord
        {
            public Collider A, B;
            public Vector3 Point, NormalToA; // normal points from B toward A
            public float Depth;
            public Vector3 VelocityA, VelocityB; // pre-resolution
            public Vector3 Impulse;               // applied to A
        }

        /// <summary>Records which live colliders are dynamic spheres (called from BuildShapes).</summary>
        void ClassifyBodies()
        {
            int n = _live.Count;
            if (_bodies.Length < n) _bodies = new Rigidbody[Math.Max(n, _bodies.Length * 2)];
            _anyDynamic = false;
            for (int i = 0; i < n; i++)
            {
                _bodies[i] = null;
                ref readonly var sh = ref _shapes[i];
                if (sh.Trigger || sh.Kind == ShapeKind.None) continue;
                var c = _live[i];
                if (c.attachedRigidbody is not { isKinematic: false } rb) continue;
                if (rb.gameObject is null || !rb.gameObject.activeInHierarchy) continue;
                if (sh.Kind != ShapeKind.Sphere)
                {
                    if (s_warnedUnsupported.Add(c.GetType()))
                        Debug.LogWarning($"[Physics] unsupported contact pair: a {c.GetType().Name} on dynamic Rigidbody '{rb.name}' gets no contacts (the contact pass resolves dynamic SPHERES only — see Port/docs/ARCHITECTURE.md §13)");
                    continue;
                }
                _bodies[i] = rb;
                _anyDynamic = true;
            }
        }

        /// <summary>Sweep hook: a solid–solid AABB touch is a contact candidate when one side is a dynamic sphere.</summary>
        void NoteSolidPair(int a, int e)
        {
            if (_bodies[a] is null && _bodies[e] is null) return;
            int lo = Math.Min(a, e), hi = Math.Max(a, e);
            _contactCandidates.Add(((long)lo << 32) | (uint)hi);
        }

        /// <summary>Step 2: resolve every candidate. True when a body moved (the sweep is then rebuilt).</summary>
        bool SolveContacts()
        {
            _contactCurrent.Clear();
            _contactsThisStep.Clear();
            if (_contactCandidates.Count == 0) return false;
            _contactCandidates.Sort();
            bool moved = false;
            foreach (long key in _contactCandidates)
            {
                int i = (int)(key >> 32), j = (int)(key & 0xFFFFFFFF);
                var a = _live[i];
                var b = _live[j];
                if (ReferenceEquals(a.gameObject, b.gameObject)) continue;
                var ra = _bodies[i];
                var rbB = _bodies[j];
                if (ra is not null && ReferenceEquals(ra, rbB)) continue; // two colliders of one body
                if (!ShouldCollide(a, b)) continue;
                if (ra is not null && SameBody(b, ra)) continue;
                if (rbB is not null && SameBody(a, rbB)) continue;

                // The dynamic sphere is A (the earlier one when both are).
                bool swap = ra is null;
                int di = swap ? j : i, oi = swap ? i : j;
                var sphereCol = _live[di];
                var otherCol = _live[oi];
                var body = _bodies[di];
                var otherBody = _bodies[oi];
                ref var sphere = ref _shapes[di];
                if (!ShapeMath.SphereContact(sphere.Center, sphere.Radius, in _shapes[oi], out var n, out float depth, out var point))
                    continue;

                var rec = new ContactRecord
                {
                    A = sphereCol, B = otherCol, Point = point, NormalToA = n, Depth = depth,
                    VelocityA = body.velocity,
                    VelocityB = otherBody is not null ? otherBody.velocity : Vector3.zero,
                };
                rec.Impulse = Resolve(body, otherBody, sphereCol, otherCol, n, depth);
                moved |= depth > 0f;
                if (depth > 0f)
                {
                    ShapeMath.TryBuild(sphereCol, out _shapes[di]);
                    if (otherBody is not null) ShapeMath.TryBuild(otherCol, out _shapes[oi]);
                }
                _contactsThisStep.Add(rec);
                _contactCurrent.Add(Ordered(a, b));
            }
            return moved;
        }

        static (Collider, Collider) Ordered(Collider a, Collider b) => a.TriggerSeq <= b.TriggerSeq ? (a, b) : (b, a);

        static bool SameBody(Collider c, Rigidbody rb) => ReferenceEquals(c.attachedRigidbody, rb);

        static bool ShouldCollide(Collider a, Collider b)
        {
            if (Physics.IsCollisionIgnored(a, b)) return false;
            return Physics.LayersInteract(a, b);
        }

        /// <summary>Push out and apply the restitution/friction impulse. Returns the impulse on the sphere's body.</summary>
        static Vector3 Resolve(Rigidbody body, Rigidbody other, Collider sphereCol, Collider otherCol, Vector3 n, float depth)
        {
            float invA = 1f / Mathf.Max(1e-4f, body.mass);
            float invB = other is not null ? 1f / Mathf.Max(1e-4f, other.mass) : 0f;
            float invSum = invA + invB;

            // Depenetration, split by inverse mass.
            if (depth > 0f)
            {
                body.transform.position += n * (depth * invA / invSum);
                if (other is not null) other.transform.position -= n * (depth * invB / invSum);
            }

            Vector3 vB = other is not null ? other.velocity : Vector3.zero;
            Vector3 rel = body.velocity - vB;
            float vn = Vector3.Dot(rel, n);
            if (vn >= 0f) return Vector3.zero; // separating already

            float e = -vn < Physics.bounceThreshold ? 0f : CombinedBounciness(sphereCol.material, otherCol.material);
            float jn = -(1f + e) * vn / invSum;
            Vector3 impulse = n * jn;

            // Coulomb friction against the tangential slip, bounded by mu * normal impulse.
            Vector3 vt = rel - n * vn;
            float slip = vt.magnitude;
            float mu = CombinedFriction(sphereCol.material, otherCol.material);
            if (mu > 0f && slip > 1e-6f)
            {
                float jt = Mathf.Min(slip / invSum, mu * jn);
                impulse -= vt / slip * jt;
            }

            body.velocity += impulse * invA;
            if (other is not null) other.velocity -= impulse * invB;
            return impulse;
        }

        // Original-engine material defaults (no material assigned).
        const float DefaultBounciness = 0f, DefaultFriction = 0.6f;

        internal static float CombinedBounciness(PhysicsMaterial a, PhysicsMaterial b)
            => Combine(a?.bounciness ?? DefaultBounciness, b?.bounciness ?? DefaultBounciness,
                Mode(a?.bounceCombine, b?.bounceCombine));

        internal static float CombinedFriction(PhysicsMaterial a, PhysicsMaterial b)
            => Combine(a?.dynamicFriction ?? DefaultFriction, b?.dynamicFriction ?? DefaultFriction,
                Mode(a?.frictionCombine, b?.frictionCombine));

        /// <summary>The original engine's precedence when the two modes differ: Average &lt; Minimum &lt; Multiply &lt; Maximum.</summary>
        static PhysicsMaterialCombine Mode(PhysicsMaterialCombine? a, PhysicsMaterialCombine? b)
        {
            static int Rank(PhysicsMaterialCombine m) => m switch
            {
                PhysicsMaterialCombine.Average => 0,
                PhysicsMaterialCombine.Minimum => 1,
                PhysicsMaterialCombine.Multiply => 2,
                _ => 3,
            };
            var ma = a ?? PhysicsMaterialCombine.Average;
            var mb = b ?? PhysicsMaterialCombine.Average;
            return Rank(ma) >= Rank(mb) ? ma : mb;
        }

        static float Combine(float a, float b, PhysicsMaterialCombine mode) => mode switch
        {
            PhysicsMaterialCombine.Minimum => Mathf.Min(a, b),
            PhysicsMaterialCombine.Maximum => Mathf.Max(a, b),
            PhysicsMaterialCombine.Multiply => a * b,
            _ => (a + b) * 0.5f,
        };

        // ── Dispatch ─────────────────────────────────────────────────

        /// <summary>
        /// Observer for every contact (collision) pair that starts, raised once per pair before its
        /// OnCollisionEnter. The parity harness (Player/ParityRun) records them; null otherwise.
        /// </summary>
        public static Action<Collider, Collider> CollisionStarted;

        /// <summary>Step 3: OnCollisionExit, Enter, then Stay (after the step's trigger messages).</summary>
        void DispatchContacts()
        {
            if (_contactPairs.Count == 0 && _contactsThisStep.Count == 0) return;

            if (_contactPairs.Count > 0)
            {
                var previous = _contactPairs.ToArray();
                _contactPairs.Clear();
                foreach (var pair in previous)
                {
                    if (_contactCurrent.Contains(pair)) { _contactPairs.Add(pair); continue; }
                    _contactSet.Remove(pair);
                    var exit = new ContactRecord { A = pair.a, B = pair.b };
                    DispatchCollision(in exit, CollisionMessage.Exit);
                }
            }

            foreach (var rec in _contactsThisStep)
            {
                var key = Ordered(rec.A, rec.B);
                if (!_contactSet.Add(key)) continue;
                _contactPairs.Add(key);
                CollisionStarted?.Invoke(rec.A, rec.B);
                DispatchCollision(in rec, CollisionMessage.Enter);
            }

            foreach (var rec in _contactsThisStep)
                DispatchCollision(in rec, CollisionMessage.Stay);
        }

        enum CollisionMessage : byte { Enter, Stay, Exit }

        static void DispatchCollision(in ContactRecord rec, CollisionMessage message)
        {
            bool hasContact = message != CollisionMessage.Exit;
            DispatchCollisionTo(rec.A, Build(in rec, rec.A, hasContact), message);
            DispatchCollisionTo(rec.B, Build(in rec, rec.B, hasContact), message);
        }

        /// <summary>The Collision <paramref name="receiver"/> sees: the OTHER collider, normal pointing toward the receiver.</summary>
        static Collision Build(in ContactRecord rec, Collider receiver, bool hasContact)
        {
            bool isA = ReferenceEquals(receiver, rec.A);
            var other = isA ? rec.B : rec.A;
            var collision = new Collision
            {
                collider = other,
                rigidbody = other.destroyedFlag ? null : other.attachedRigidbody,
                relativeVelocity = isA ? rec.VelocityB - rec.VelocityA : rec.VelocityA - rec.VelocityB,
                impulse = isA ? rec.Impulse : -rec.Impulse,
            };
            if (hasContact)
                collision.AddContact(new ContactPoint
                {
                    point = rec.Point,
                    normal = isA ? rec.NormalToA : -rec.NormalToA,
                    separation = -rec.Depth,
                    thisCollider = receiver,
                    otherCollider = other,
                    impulse = collision.impulse,
                });
            return collision;
        }

        static void DispatchCollisionTo(Collider receiver, Collision collision, CollisionMessage message)
        {
            DispatchCollisionOn(receiver.gameObject, collision, message);
            if (BodyObject(receiver) is { } body) DispatchCollisionOn(body, collision, message);
        }

        static void DispatchCollisionOn(GameObject go, Collision collision, CollisionMessage message)
        {
            if (go is null || go.IsDestroyed || !go.activeInHierarchy) return;
            var components = go.Components;
            var snapshot = new Component[components.Count];
            for (int i = 0; i < components.Count; i++) snapshot[i] = components[i];
            foreach (var component in snapshot)
            {
                if (component is not MonoBehaviour mb || mb.IsDestroyed) continue;
                switch (message)
                {
                    case CollisionMessage.Enter: mb.RunCollisionEnter(collision); break;
                    case CollisionMessage.Stay: mb.RunCollisionStay(collision); break;
                    default: mb.RunCollisionExit(collision); break;
                }
            }
        }
    }
}
