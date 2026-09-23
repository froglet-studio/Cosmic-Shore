using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// One LIGHT: a region of space a force is acting on, or is about to, expressed in the shape
    /// that force actually sweeps. Mass standing inside one is <b>LIT</b>.
    ///
    /// <para><b>Why this is a fundamental and not a view effect.</b> Several systems independently
    /// wanted to say the same sentence - "my force is reaching that mass, and it is mine" - and
    /// each was about to say it its own way. THREE ship: the Dolphin's Echo Sight (a pending
    /// blast), the Sparrow's proximity fuze (an armed warhead) and every own-domain explosion
    /// passthrough (a blast that arrived and spared it - which covers the Scarab's swept plate
    /// too, since that blast spares its own domain). They differ only in WHEN the force lands,
    /// which is a property of the producer; what they share is the volume, the owner and the
    /// light. A skim field was built as a fourth and cut - see <c>Docs/LIT.md</c>, which records
    /// the two questions that cut it.</para>
    ///
    /// <para><b>One predicate, three shapes.</b> <see cref="Contains"/> is the single CPU
    /// transcription of the three Burst sweeps in <c>PrismSpatialIndex</c>
    /// (<c>AOESpatialQueryJob</c>, <c>AOEConicSweepQueryJob</c>, <c>AOECylinderSweepQueryJob</c>)
    /// and of the GPU half in <c>PrismDestructionSight.hlsl</c>. <see cref="BlastVolume"/> - which
    /// predates this struct and is still the Dolphin blast's own carrier - now DELEGATES its cone
    /// test here rather than keeping a second copy, because the codebase already had three
    /// transcriptions of that arm and a fourth was how the preview and the damage would have
    /// drifted apart.</para>
    ///
    /// <para><b>There is deliberately no reader for gameplay yet.</b> Nothing asks "is this prism
    /// lit?" to decide an outcome. The state is published and drawn; consuming it is a later,
    /// separate decision, and it carries a replication problem this struct does not solve - a
    /// light's SIZE is a function of owner-local element levels and locally-simulated resources,
    /// so two machines agree on a boundary prism only to within a tick. Any future combo must
    /// resolve on the owning machine and report (the <c>Player.ReportFaunaKill_ServerRpc</c>
    /// family), never evaluate <see cref="Contains"/> independently per peer and act.</para>
    /// </summary>
    public struct LitVolume
    {
        /// <summary>Weighting toward the volume's boundary. Mirrors <c>PRISM_LIT_EDGE_POWER</c>.</summary>
        public const float EdgePower = 2f;

        /// <summary>Fill floor at the volume's core, so deep mass still reads as marked. Mirrors <c>PRISM_LIT_CORE_FILL</c>.</summary>
        public const float CoreFill = 0.35f;

        /// <summary>Which of the three swept shapes this is.</summary>
        public LitShape Shape;

        /// <summary>The emitter: a cone's apex, a sphere's centre, a cylinder's start-plane centre.</summary>
        public Vector3 Origin;

        /// <summary>Unit sweep direction. Ignored for <see cref="LitShape.Sphere"/>.</summary>
        public Vector3 Axis;

        /// <summary>
        /// Unit vector perpendicular to <see cref="Axis"/>, along which a cone's capsule
        /// cross-section is elongated. Ignored for sphere and cylinder.
        /// </summary>
        public Vector3 GapeAxis;

        /// <summary>
        /// Shape-dependent, and the reason <see cref="Shape"/> travels with it:
        /// cone <c>(height, coreRadiusPerUnitDepth, halfLengthPerUnitDepth)</c>,
        /// sphere <c>(radius, -, -)</c>, cylinder <c>(reach, radius, mirrored ? 1 : 0)</c>.
        /// </summary>
        public Vector3 Params;

        /// <summary>False on a default-constructed volume, so an unreported light is never tested.</summary>
        public bool IsValid;

        /// <summary>
        /// The reach this volume extends to along its own axis (a cone's height, a sphere's
        /// radius, a cylinder's swept depth). Zero or less means "nothing to light", which every
        /// publish path treats as OFF - the same sentinel the shader branches on.
        /// </summary>
        public readonly float Reach => Params.x;

        // ---------------- Constructors ----------------

        /// <summary>A cone with a capsule cross-section - <c>AOEConicExplosion</c>'s swept volume.</summary>
        public static LitVolume Cone(Vector3 apex, Vector3 axis, Vector3 gapeAxis,
            float height, float tanCorePerUnit, float tanGapePerUnit) => new()
        {
            Shape = LitShape.Cone,
            Origin = apex,
            Axis = axis,
            GapeAxis = gapeAxis,
            Params = new Vector3(height, tanCorePerUnit, tanGapePerUnit),
            IsValid = true,
        };

        /// <summary>A sphere - an ordinary AOE blast, or the Sparrow warhead's proximity fuze.</summary>
        public static LitVolume Sphere(Vector3 centre, float radius) => new()
        {
            Shape = LitShape.Sphere,
            Origin = centre,
            // Axis/GapeAxis are unread for a sphere, but kept unit so a slot written here and
            // later rewritten as a cone can never leave a zero axis behind for the shader to
            // normalize-divide by.
            Axis = Vector3.forward,
            GapeAxis = Vector3.right,
            Params = new Vector3(radius, 0f, 0f),
            IsValid = true,
        };

        /// <summary>
        /// A swept cylinder with flat caps and constant radius - the Scarab's cavitation plate.
        /// <paramref name="mirrored"/> reflects it through the start plane, exactly as
        /// <c>AOECylinderSweepQueryJob.Mirrored</c> does.
        /// </summary>
        public static LitVolume Cylinder(Vector3 origin, Vector3 axis, float reach, float radius,
            bool mirrored) => new()
        {
            Shape = LitShape.Cylinder,
            Origin = origin,
            Axis = axis,
            GapeAxis = Vector3.right,
            Params = new Vector3(reach, radius, mirrored ? 1f : 0f),
            IsValid = true,
        };

        // ---------------- The predicate ----------------

        /// <summary>
        /// Is <paramref name="worldPoint"/> standing inside this volume, and how deep?
        ///
        /// <paramref name="fill01"/> comes back on the same edge-weighted curve the shader uses,
        /// so anything highlighted through the CPU path and the prisms around it brighten together
        /// instead of reading as two effects that happen to share a trigger.
        ///
        /// Each arm's FIRST test is also its cheap reject (one dot and a compare), which is what
        /// makes a bank of these affordable to walk per prism on the GPU - there is deliberately
        /// no separate bounding sphere to maintain.
        /// </summary>
        public readonly bool Contains(Vector3 worldPoint, out float fill01)
        {
            fill01 = 0f;
            if (!IsValid || Params.x <= 0f) return false;

            Vector3 rel = worldPoint - Origin;

            return Shape switch
            {
                LitShape.Sphere => ContainsSphere(rel, out fill01),
                LitShape.Cylinder => ContainsCylinder(rel, out fill01),
                _ => ContainsCone(rel, out fill01),
            };
        }

        /// <summary>
        /// The cone arm, kept expression for expression identical to the transcription it
        /// replaced (<c>BlastVolume.Contains</c>) and to <c>PrismLitFill</c>'s cone branch: clamp
        /// onto the cross-section's SEGMENT first, then measure distance to that point - that
        /// ordering is what makes the ends round, and copying it rather than approximating with a
        /// plain cone is why a preview lights exactly the mass the blast would reach.
        /// </summary>
        readonly bool ContainsCone(Vector3 rel, out float fill01)
        {
            fill01 = 0f;

            float height = Params.x;

            // The near clip is the apex: mass (and pilots) BEHIND the emitter are never inside,
            // even though the axis extends backwards mathematically.
            float s = Vector3.Dot(rel, Axis);
            if (s <= 0f || s > height) return false;

            float coreRadius = Params.y * s;
            if (coreRadius <= 0f) return false;

            Vector3 radial = rel - Axis * s;
            float halfLength = Params.z * s;
            float along = Mathf.Clamp(Vector3.Dot(radial, GapeAxis), -halfLength, halfLength);
            Vector3 offAxis = radial - GapeAxis * along;

            float d = offAxis.magnitude;
            if (d > coreRadius) return false;

            fill01 = Fill(d, coreRadius);
            return true;
        }

        readonly bool ContainsSphere(Vector3 rel, out float fill01)
        {
            fill01 = 0f;

            float radius = Params.x;
            float dSq = rel.sqrMagnitude;
            if (dSq > radius * radius) return false;

            fill01 = Fill(Mathf.Sqrt(dSq), radius);
            return true;
        }

        readonly bool ContainsCylinder(Vector3 rel, out float fill01)
        {
            fill01 = 0f;

            float reach = Params.x;
            float radius = Params.y;
            if (radius <= 0f) return false;

            // MIRRORED tests |axial|, so one volume claims the two slabs [-reach, 0] and
            // [0, reach] together - the same expression AOECylinderSweepQueryJob uses.
            float s = Vector3.Dot(rel, Axis);
            float axial = Params.z > 0f ? Mathf.Abs(s) : s;
            if (axial < 0f || axial > reach) return false;

            // Radial band about the axis: a true cylinder, flat end caps. Measured from the
            // SIGNED projection, not the mirrored one, or a reflected point's radial arm is
            // taken from the wrong side of the plane.
            Vector3 radial = rel - Axis * s;
            float d = radial.magnitude;
            if (d > radius) return false;

            fill01 = Fill(d, radius);
            return true;
        }

        /// <summary>
        /// The one fill curve. Weighted toward the boundary so a volume's silhouette is drawn onto
        /// the mass rather than flooding it, with a floor so deep mass still reads as marked.
        /// </summary>
        static float Fill(float distance, float extent)
        {
            float edge = Mathf.Clamp01(distance / extent);
            return Mathf.Lerp(CoreFill, 1f, Mathf.Pow(edge, EdgePower));
        }
    }
}
