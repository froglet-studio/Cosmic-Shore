using Unity.Mathematics;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ECS;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Rhino's HULL is a plough: a prism it rams is shoved aside, to whichever side of the
    /// hull's centreline it already sat on (or to the side a held trigger steers it,
    /// <see cref="RhinoPloughSteerExecutor"/>), far enough to clear the hull — it is never destroyed.
    /// Since 2026-11-07 the plough IS the Rhino (the sword split off into the Swordfish). Since the
    /// 2026-11-12 rework: Mass (Heft) throws further, Charge carries a thrown prism on to strike another
    /// (Charge's rival hit is the Gore, <see cref="RhinoGoreBySkimmerEffectSO"/>), and two level-5s (Unstoppable on
    /// Space, Berm on Mass) gate here; Space widens the blade (<see cref="RhinoPloughBlade"/>).
    /// Replaces <see cref="VesselDamagePrismEffectSO"/> in the Rhino's vessel container only — every
    /// other hull still rams as before. Design + verification: <c>R_VesselActions/RHINO_PLOUGH.md</c>.
    ///
    /// <para>The move follows the platform's live-prism mover contract (the same one
    /// <c>ThreatGrove.PoseAndFly</c> and <c>BuilderPrismWorld.Settle</c> use): the transform —
    /// and with it the collider, spatial index and shell — is FINAL this frame, and the visual
    /// slides out from where it was on the prism clock's flight stamp
    /// (Docs/PRISM_ANIMATION.md §5 C5). So the hull leaves contact on the very next physics step
    /// and nothing per-frame runs on the CPU while the prism slides.</para>
    ///
    /// <para><b>A shoved prism is a MOVING prism</b> (<see cref="PrismMotion"/>, Docs/PRISM_MOTION.md): if its
    /// path out of the furrow runs into another prism, it stops there and both are destroyed, scored to the
    /// Rhino's pilot. Only a clear path completes the shove.</para>
    ///
    /// <para>Runs on every peer, like the damage it replaces (prism contact is simulated per peer);
    /// the side is read off the peer's own hull pose, so it agrees everywhere except for a prism
    /// struck dead on the centreline.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "RhinoPloughPrismEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Prism/RhinoPloughPrismEffectSO")]
    public sealed class RhinoPloughPrismEffectSO : VesselPrismEffectSO
    {
        [Header("Clearance")]
        [Tooltip("Air left between the hull's side and the shoved prism's near face, in world units. " +
                 "The clearance itself is measured from the hull colliders and the prism's own size, " +
                 "so this is only the gap on top of just-touching.")]
        [SerializeField, Min(0f)] float clearanceMargin = 1f;

        [Header("Throw")]
        [Tooltip("Extra sideways distance per unit of ram speed, in seconds (distance = speed x this). " +
                 "A faster Rhino throws the furrow wider. 0 = every prism lands just clear of the hull.")]
        [SerializeField, Min(0f)] float throwSecondsOfSpeed = 0.12f;   // tripled 2026-11-09 (Garrett)

        [Tooltip("Ceiling on the speed-driven extra throw, in world units, so a ramp-boosted Rhino " +
                 "(~1,200 u/s) does not fling mass across the arena.")]
        [SerializeField, Min(0f)] float maxThrowDistance = 90f;   // tripled 2026-11-09 (Garrett)

        [Header("Heft (Mass)")]
        [Tooltip("MASS (Heft): how much further a heavier plough throws - world units added to every throw, at any speed, on top of " +
                 "the speed-driven throw above. 0 at rest (the plough as it shipped), 60 at Mass 10, floored at 0 " +
                 "in the deficit band (Space until 2026-11-12: a heavier hull pushes further). Read at the Rhino's replicated level, so every peer lands the prism alike.")]
        [SerializeField] ElementalFloat furrowDistance = ElementalFloat.Multiplier(0f, 60f, Element.Mass, 0f);   // tripled 2026-11-09, moved to Mass 2026-11-12 (Garrett)

        [Tooltip("SPACE level-5 'Unstoppable' (the fleet's Space shield-breaker slot): with this element's upgrade active the plough moves SUPER-shielded " +
                 "mass too (without it super-shielded prisms are the immovable tier and only jiggle). None = never.")]
        [SerializeField] Element unstoppableUpgradeElement = Element.Space;

        [Header("Carry (Charge)")]
        [Tooltip("CHARGE: how far past its landing point a thrown prism still flies on to strike another (a thrown " +
                 "prism that hits one destroys both, PrismMotion). 0 at rest (it strikes only what is in its way), " +
                 "40 u at Charge 10, floored at 0. Only the strike reaches further: a prism that strikes nothing " +
                 "still lands where the furrow puts it.")]
        [SerializeField] ElementalFloat carryDistance = ElementalFloat.Multiplier(0f, 40f, Element.Charge, 0f);

        [Tooltip("'Avalanche' (retired as Charge's level-5 on 2026-11-12, when Gore Steal took the slot; the asset authors None): with this element's upgrade active a strike carries on - the next " +
                 "prism down the line within the carry is thrown on too, and its own strike can throw the next. " +
                 "None = never.")]
        [SerializeField] Element avalancheUpgradeElement = Element.None;

        [Tooltip("How many further prisms one Avalanche can throw on, after the first strike.")]
        [SerializeField, Min(0)] int avalancheLinks = 3;

        [Header("Berm (Mass)")]
        [Tooltip("MASS level-5 'Berm': with this element's upgrade active a ploughed prism LANDS SHIELDED, so the " +
                 "furrow banks into walls of armour. Only prisms that land; one that strikes another still breaks. " +
                 "(Mass itself widens the blade: RhinoPloughBlade.) None = never.")]
        [SerializeField] Element bermUpgradeElement = Element.Mass;

        [Header("Slide")]
        [Tooltip("Seconds the prism's VISUAL takes to slide from where it was to where it now rests. " +
                 "Animation only: its collider is already at rest on the frame of the ram.")]
        [SerializeField, Min(0.01f)] float slideSeconds = 0.25f;

        [Header("Attach")]
        [Tooltip("ON (default): a vessel RIDING a trail does not also plough it - riding and ramming " +
                 "are the same contact. Same guard as VesselDamagePrismEffectSO.")]
        [SerializeField] bool skipWhileAttached = true;

        public override void Execute(VesselImpactor impactor, PrismImpactor prismImpactee)
        {
            var status = impactor.Vessel.VesselStatus;
            if (skipWhileAttached && status.IsAttached) return;

            var prism = prismImpactee.Prism;
            if (!prism || prism.destroyed) return;

            // Super-shielded mass is the immovable tier (arena edge linings among it): it
            // deflects - the same jiggle a damage hit gets - and stays exactly where it is,
            // unless the Space level-5 (Unstoppable) is active.
            // Each level-5 gate is the replicated unlock bit, read per contact (vessel skill rule 3).
            var abilities = status.ElementalAbilityHandler;
            bool unstoppable = unstoppableUpgradeElement != Element.None && abilities &&
                               abilities.IsUpgradeActive(unstoppableUpgradeElement);
            if (!unstoppable && prism.AbsorbSuperShieldHit(status.Speed)) return;

            var hull = status.ShipTransform;
            Vector3 right = hull.right;
            Vector3 origin = hull.position;
            Vector3 prismPos = prism.transform.position;

            float lateral = Vector3.Dot(prismPos - origin, right);
            float side = RhinoPlough.SteeredSide(lateral, SteeredSide(status));

            float hullReach = RhinoPlough.HullReach(impactor.HullColliders, origin, right, side);
            float prismHalf = RhinoPlough.PrismHalfExtentAlong(prism, right);
            float clearance = hullReach + prismHalf + clearanceMargin;
            float throwDistance = RhinoPlough.ThrowDistance(status.Speed, throwSecondsOfSpeed, maxThrowDistance,
                furrowDistance.EvaluateReplicated(status));

            // Idempotent by construction: a prism already clear of the hull is not shoved again,
            // so the second trigger callback of a multi-collider hull (or the shell tier and the
            // box tier both reporting one ram) is a no-op rather than a double throw.
            float shove = RhinoPlough.ShoveToward(lateral, side, clearance, throwDistance);
            if (shove == 0f) return;

            // Every prism thrown buys stampede time for the next omni crystal (RhinoStampedeByCrystalEffectSO).
            RhinoStampede.CountPlough(status);

            var throwSettings = new ThrowSettings
            {
                Carry = Mathf.Max(0f, carryDistance.EvaluateReplicated(status)),
                LinksLeft = avalancheUpgradeElement != Element.None && abilities &&
                            abilities.IsUpgradeActive(avalancheUpgradeElement) ? avalancheLinks : 0,
                Berm = bermUpgradeElement != Element.None && abilities && abilities.IsUpgradeActive(bermUpgradeElement),
                Unstoppable = unstoppable,
            };
            Vector3 land = prismPos + right * shove;
            Throw(prism, prismPos, land, throwSettings, status);
        }

        struct ThrowSettings
        {
            public float Carry;
            public int LinksLeft;
            public bool Berm;
            public bool Unstoppable;
        }

        /// <summary>
        /// Moves <paramref name="prism"/> from <paramref name="from"/> toward <paramref name="land"/>. Its path is
        /// swept <see cref="ThrowSettings.Carry"/> further than the landing point: a prism in the way (or within the
        /// carry) is struck - a shielded pair bounces, anything else breaks both (and, with Avalanche, throws the
        /// next prism down the line on). A clear path lands it at <paramref name="land"/>.
        /// </summary>
        void Throw(Prism prism, Vector3 from, Vector3 land, ThrowSettings settings, IVesselStatus status)
        {
            Vector3 step = land - from;
            Vector3 dir = step.sqrMagnitude > 1e-8f ? step.normalized : Vector3.zero;
            Vector3 reach = land + dir * settings.Carry;
            if (PrismMotion.TrySweep(prism, from, reach, out var struck, out var contact))
            {
                if (PrismMotion.Bounces(prism, struck))
                {
                    Rebound(prism, from, reach, struck, contact, settings, status);
                    return;
                }

                // Avalanche: find the next prism down the line BEFORE this pair breaks (the probe sweeps the
                // struck prism's own box on along the throw).
                Prism next = null;
                if (settings.LinksLeft > 0 && settings.Carry > 0f && dir != Vector3.zero)
                {
                    Vector3 sp = struck.transform.position;
                    if (PrismMotion.TrySweep(struck, sp, sp + dir * settings.Carry, out var n, out _) &&
                        n && n != prism && !n.destroyed)
                        next = n;
                }

                // Prism motion's collision rule: it stops where it met the other prism, and both go.
                Place(prism, contact);
                PrismMotion.Annihilate(prism, struck, (reach - from) / slideSeconds, status.Domain, status.PlayerName,
                    PrismMotion.SingleMoverLevel());

                if (next && !next.destroyed &&
                    (settings.Unstoppable || next.prismProperties is not { IsSuperShielded: true }))
                {
                    var chained = settings;
                    chained.LinksLeft--;
                    Vector3 np = next.transform.position;
                    Throw(next, np, np + dir * settings.Carry, chained, status);
                }
                return;
            }

            Land(prism, from, land, settings.Berm);
        }

        /// <summary>
        /// A shielded collision bounces (<see cref="PrismMotion.Bounces(Prism, Prism)"/>): the thrown prism meets the other
        /// at <paramref name="contact"/> and the rest of the throw rebounds off it (<see cref="PrismBounce.Rebound"/>), and
        /// neither is destroyed. A second prism in the way of the rebound stops it there, bouncing again, or both go if
        /// neither of that pair is shielded.
        /// </summary>
        void Rebound(Prism prism, Vector3 from, Vector3 end, Prism struck, Vector3 contact, ThrowSettings settings,
            IVesselStatus status)
        {
            Vector3 step = end - from;
            float t = step.sqrMagnitude > 1e-8f ? Vector3.Dot(contact - from, step) / step.sqrMagnitude : 1f;
            Vector3 normal = PrismBounce.Normal(contact, struck.transform.position, step);
            Vector3 rebound = PrismBounce.Rebound(contact, step, t, normal, PrismMotion.BounceRestitution);
            if (PrismMotion.TrySweep(prism, contact, rebound, out var second, out var secondContact))
            {
                if (!PrismMotion.Bounces(prism, second))
                {
                    Place(prism, secondContact);
                    PrismMotion.Annihilate(prism, second, (rebound - contact) / slideSeconds, status.Domain, status.PlayerName,
                        PrismMotion.SingleMoverLevel());
                    return;
                }
                rebound = secondContact;
            }
            Land(prism, from, rebound, settings.Berm);
        }

        void Land(Prism prism, Vector3 from, Vector3 at, bool berm)
        {
            Slide(prism, from, at, slideSeconds);
            // Berm (Mass L5): the furrow banks into armour. ActivateShield is the prism's own state change, and
            // like the move it runs per peer from the same replicated upgrade bit.
            if (berm && !prism.destroyed && prism.prismProperties is not { IsShielded: true } &&
                prism.prismProperties is not { IsSuperShielded: true })
                prism.ActivateShield();
        }

        static float SteeredSide(IVesselStatus status)
        {
            var handler = status.ActionHandler;
            var execs = handler ? handler.Executors : null;
            var steer = execs ? execs.Get<RhinoPloughSteerExecutor>() : null;
            return steer ? steer.SteeredSide : 0f;
        }

        static void Place(Prism prism, Vector3 at)
        {
            prism.transform.position = at;
            if (prism.prismProperties != null) prism.prismProperties.position = at;
            prism.NotifyPositionChanged();
        }

        static void Slide(Prism prism, Vector3 start, Vector3 end, float seconds)
        {
            // prismProperties.position is kept in step the way FullAutoBlockShoot's mover keeps it -
            // nothing reads it today, but a cached position that lies is the next reader's bug.
            Place(prism, end);

            Vector3 flight = end - start;
            if (flight.sqrMagnitude < 1e-4f) return;
            // StampFlight's full vector is v * 2T / pi, so v = flight * pi / (2T).
            Vector3 v = flight * (Mathf.PI / (2f * seconds));
            var fv = new float3(v.x, v.y, v.z);
            Vector3 local;
            if (!PrismRenderService.StampFlight(in prism.RenderHandle, PrismClock.Now, seconds, fv))
            {
                // No Entities Graphics on this device (Docs/PRISM_ANIMATION.md §4.11): the same stamp and
                // envelope on the MeshRenderer, which clears itself at arrival. False everywhere else.
                if (!PrismMeshRendererStamps.StampFlight(prism.StampRenderer, PrismClock.Now, seconds, in fv))
                    return;
                local = prism.transform.InverseTransformPoint(start);
                PrismMeshRendererStamps.ResetBounds(prism.StampRenderer);
                PrismMeshRendererStamps.EncapsulateBoundsPoint(prism.StampRenderer,
                    new float3(local.x, local.y, local.z), 1f);
                return;
            }
            // The render bounds sit on the new anchor; cover the start too, or the sliding visual
            // frustum-culls against its resting box (BuilderPrismWorld.Settle does the same).
            local = prism.transform.InverseTransformPoint(start);
            PrismRenderService.EncapsulateBoundsPoint(in prism.RenderHandle,
                new float3(local.x, local.y, local.z), 1f);
        }
    }

    /// <summary>
    /// The plough's geometry, kept pure so it is testable without a scene
    /// (<c>RhinoPloughTests</c>). Every distance is measured along ONE axis — the hull's right —
    /// because a plough throws to the side, never up or down.
    /// </summary>
    public static class RhinoPlough
    {
        /// <summary>+1 for a prism right of the centreline, -1 left. Dead centre goes right — an
        /// arbitrary but fixed tie-break, so the result never depends on float noise sign.</summary>
        public static float SideOf(float lateral) => lateral >= 0f ? 1f : -1f;

        /// <summary>
        /// The signed distance to move a prism along the hull's right axis. A prism whose centre
        /// already sits at least <paramref name="clearance"/> off the centreline is not moved (0);
        /// otherwise it lands at <c>clearance + throwDistance</c> on its own side.
        /// </summary>
        public static float LateralShove(float lateral, float clearance, float throwDistance)
            => ShoveToward(lateral, SideOf(lateral), clearance, throwDistance);

        /// <summary>
        /// The side a prism is thrown to: the STEERED side while a trigger is held (+1 right, -1 left), or the side
        /// it already sat on when <paramref name="steered"/> is 0 (<see cref="RhinoPloughSteerExecutor"/>).
        /// </summary>
        public static float SteeredSide(float lateral, float steered)
            => steered > 0f ? 1f : steered < 0f ? -1f : SideOf(lateral);

        /// <summary>
        /// The signed distance to move a prism along the hull's right axis so it lands on <paramref name="side"/>.
        /// A prism already at least <paramref name="clearance"/> out on that side is not moved (0); otherwise it lands
        /// at <c>clearance + throwDistance</c> on that side - across the centreline when the plough is steered away
        /// from the side it sat on.
        /// </summary>
        public static float ShoveToward(float lateral, float side, float clearance, float throwDistance)
        {
            float s = side >= 0f ? 1f : -1f;
            if (lateral * s >= clearance) return 0f;
            return s * (clearance + Mathf.Max(0f, throwDistance)) - lateral;
        }

        /// <summary>
        /// How far past the clearance a prism is thrown: the speed-driven throw (capped) plus the Space furrow, which
        /// does not depend on speed.
        /// </summary>
        public static float ThrowDistance(float speed, float throwSecondsOfSpeed, float maxThrowDistance, float furrow)
            => Mathf.Min(maxThrowDistance, Mathf.Abs(speed) * Mathf.Max(0f, throwSecondsOfSpeed)) + Mathf.Max(0f, furrow);

        /// <summary>Half-width of an oriented box along <paramref name="axis"/>: the support
        /// distance of the box from its centre in that direction.</summary>
        public static float BoxHalfExtentAlong(Vector3 axis, Vector3 boxRight, Vector3 boxUp,
                                               Vector3 boxForward, Vector3 halfSize)
            => Mathf.Abs(Vector3.Dot(axis, boxRight)) * halfSize.x
             + Mathf.Abs(Vector3.Dot(axis, boxUp)) * halfSize.y
             + Mathf.Abs(Vector3.Dot(axis, boxForward)) * halfSize.z;

        /// <summary>Half-width of an octahedron (vertices on its local axes at
        /// <paramref name="halfSize"/>) along <paramref name="axis"/> — a SHIELDED prism's shell
        /// once <paramref name="halfSize"/> is scaled by the shell's circumscribing scale.</summary>
        public static float OctahedronHalfExtentAlong(Vector3 axis, Vector3 right, Vector3 up,
                                                      Vector3 forward, Vector3 halfSize)
            => Mathf.Max(Mathf.Abs(Vector3.Dot(axis, right)) * halfSize.x,
               Mathf.Max(Mathf.Abs(Vector3.Dot(axis, up)) * halfSize.y,
                         Mathf.Abs(Vector3.Dot(axis, forward)) * halfSize.z));

        /// <summary>How far the hull reaches from <paramref name="origin"/> along
        /// <c>side * axis</c>: the furthest support point over every hull collider.</summary>
        public static float HullReach(Collider[] hull, Vector3 origin, Vector3 axis, float side)
        {
            Vector3 dir = axis * side;
            float reach = 0f;
            if (hull == null) return reach;
            foreach (var c in hull)
            {
                if (!c || !c.enabled) continue;
                Vector3 centre;
                float half;
                if (c is BoxCollider box)
                {
                    var t = box.transform;
                    centre = t.TransformPoint(box.center);
                    half = BoxHalfExtentAlong(dir, t.right, t.up, t.forward,
                        Vector3.Scale(box.size, Abs(t.lossyScale)) * 0.5f);
                }
                else
                {
                    // Anything else: its world AABB - conservative, so the prism lands a little wide.
                    var b = c.bounds;
                    centre = b.center;
                    half = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.y) * b.extents.y
                         + Mathf.Abs(dir.z) * b.extents.z;
                }
                reach = Mathf.Max(reach, Vector3.Dot(centre - origin, dir) + half);
            }
            return reach;
        }

        /// <summary>The prism's half-width along <paramref name="axis"/> — its box, or its
        /// octahedral shell when shielded (the shell is what the hull actually touches).</summary>
        public static float PrismHalfExtentAlong(Prism prism, Vector3 axis)
        {
            var t = prism.transform;
            Vector3 half = Abs(t.lossyScale) * 0.5f;
            if (prism.TryGetComponent(out BoxCollider box))
                half = Vector3.Scale(box.size, Abs(t.lossyScale)) * 0.5f;
            return prism.prismProperties is { IsShielded: true }
                ? OctahedronHalfExtentAlong(axis, t.right, t.up, t.forward,
                    half * OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE)
                : BoxHalfExtentAlong(axis, t.right, t.up, t.forward, half);
        }

        static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
