#if UNITY_EDITOR
using NUnit.Framework;
using Unity.Mathematics;
using CosmicShore.Utility;

namespace CosmicShore.Tests
{
    /// <summary>
    /// Tests for <see cref="ShieldShellMath"/> — the exact analytic narrowphase of
    /// the shielded-prism collision tier (PrismSpatialIndex shell view /
    /// PrismShellContactManager).
    ///
    /// Two layers:
    ///
    ///   1. LANDMARKS — the behaviors the shell tier exists to guarantee, on an
    ///      axis-aligned unit shell (half-extents 1, shieldScale 3):
    ///        - a probe touching a stella SPIKE TIP collides;
    ///        - a probe threaded BETWEEN spikes inside the stella's bounding box
    ///          does NOT collide (union-of-two-tetrahedra semantics — the case
    ///          every convex-hull or AABB approximation gets wrong);
    ///        - octahedron face/vertex grazes fire exactly at the surface.
    ///
    ///   2. CROSS-VALIDATED CASES — 24 randomized poses (arbitrary rotation,
    ///      non-uniform semi-axes, all three probe shapes × both shells) whose
    ///      expected results were computed by an independent ground truth
    ///      (SLSQP quadratic-program distance for sphere/capsule, LP feasibility
    ///      for boxes) during the tier's derivation; the full validation run was
    ///      7,200 random cases + landmarks with zero disagreements. These pin the
    ///      C# port to that verified behavior.
    /// </summary>
    public class ShieldShellMathTests
    {
        // Axis-aligned reference shell: authored half-extents 1, shieldScale 3 →
        // octahedron vertices at ±3 on each axis, stella spike tips at (±3,±3,±3).
        static ShieldShellMath.ShellFrame UnitShell()
            => ShieldShellMath.CreateFrame(float3.zero, quaternion.identity, new float3(3f, 3f, 3f));

        // --------------------------------------------------------------
        // Landmarks: spike tips hit
        // --------------------------------------------------------------

        [Test]
        public void Stella_SphereTouchingSpikeTip_Collides()
        {
            var f = UnitShell();
            float3 tip = new float3(3f, 3f, 3f);
            float3 outward = math.normalize(tip);
            // Sphere center 0.30 beyond the tip: r=0.31 reaches it, r=0.29 does not.
            Assert.IsTrue(ShieldShellMath.SphereOverlapsStella(in f, tip + outward * 0.30f, 0.31f));
            Assert.IsFalse(ShieldShellMath.SphereOverlapsStella(in f, tip + outward * 0.30f, 0.29f));
        }

        [Test]
        public void Stella_BoxCornerReachingSpikeTip_Collides()
        {
            var f = UnitShell();
            float3 center = new float3(3.5f, 3.5f, 3.5f); // 0.5 beyond the tip on each axis
            Assert.IsTrue(ShieldShellMath.BoxOverlapsStella(in f, center,
                new float3(0.51f, 0f, 0f), new float3(0f, 0.51f, 0f), new float3(0f, 0f, 0.51f)));
            Assert.IsFalse(ShieldShellMath.BoxOverlapsStella(in f, center,
                new float3(0.45f, 0f, 0f), new float3(0f, 0.45f, 0f), new float3(0f, 0f, 0.45f)));
        }

        // --------------------------------------------------------------
        // Landmarks: the inter-spike gap does NOT collide
        // --------------------------------------------------------------

        [Test]
        public void Stella_SphereInInterSpikeGap_DoesNotCollide()
        {
            var f = UnitShell();
            // Midpoint of two adjacent cube corners (3,3,3) [tet A] and (3,3,-3)
            // [tet B], pulled just inside the bounding box: deep inside the AABB
            // (and the convex hull), outside BOTH tetrahedra.
            float3 gap = new float3(3f, 3f, 0f) * 0.98f;
            Assert.IsFalse(ShieldShellMath.SphereOverlapsStella(in f, gap, 0.35f),
                "A sphere between two spikes inside the bounding box must not collide - " +
                "any convex-hull/AABB approximation fails this");
        }

        [Test]
        public void Stella_BoxInInterSpikeGap_DoesNotCollide()
        {
            var f = UnitShell();
            float3 gap = new float3(3f, 3f, 0f) * 0.98f;
            Assert.IsFalse(ShieldShellMath.BoxOverlapsStella(in f, gap,
                new float3(0.3f, 0f, 0f), new float3(0f, 0.3f, 0f), new float3(0f, 0f, 0.3f)));
        }

        [Test]
        public void Stella_CapsuleThreadedBetweenSpikes_DoesNotCollide_UntilFattened()
        {
            var f = UnitShell();
            // Runs along z near the bounding-cube edge (2.9, 2.9, z): outside both
            // tets for its whole length.
            float3 a = new float3(2.9f, 2.9f, -1.2f);
            float3 b = new float3(2.9f, 2.9f, 1.2f);
            Assert.IsFalse(ShieldShellMath.CapsuleOverlapsStella(in f, a, b, 0.12f));
            // Fattened enough to reach the nearest spike surface, it must hit.
            Assert.IsTrue(ShieldShellMath.CapsuleOverlapsStella(in f, a, b, 1.0f));
        }

        // --------------------------------------------------------------
        // Landmarks: octahedron surface precision + containment
        // --------------------------------------------------------------

        [Test]
        public void Octa_VertexAndFaceGrazes_FireExactlyAtSurface()
        {
            var f = UnitShell();
            // Vertex at (3,0,0): center 0.4 beyond it.
            Assert.IsTrue(ShieldShellMath.SphereOverlapsOcta(in f, new float3(3.4f, 0f, 0f), 0.41f));
            Assert.IsFalse(ShieldShellMath.SphereOverlapsOcta(in f, new float3(3.4f, 0f, 0f), 0.39f));
            // Face point (1,1,1) on plane x+y+z=3, center 0.5 along the face normal.
            float3 n = math.normalize(new float3(1f, 1f, 1f));
            float3 p = new float3(1f, 1f, 1f) + n * 0.5f;
            Assert.IsTrue(ShieldShellMath.SphereOverlapsOcta(in f, p, 0.51f));
            Assert.IsFalse(ShieldShellMath.SphereOverlapsOcta(in f, p, 0.49f));
        }

        [Test]
        public void Containment_CountsAsOverlap_BothDirections()
        {
            var f = UnitShell();
            // Small box fully inside the octahedron (no surface contact).
            Assert.IsTrue(ShieldShellMath.BoxOverlapsOcta(in f, float3.zero,
                new float3(0.2f, 0f, 0f), new float3(0f, 0.2f, 0f), new float3(0f, 0f, 0.2f)));
            // Huge sphere fully containing the shell.
            Assert.IsTrue(ShieldShellMath.SphereOverlapsOcta(in f, new float3(0.1f, 0f, 0f), 50f));
            Assert.IsTrue(ShieldShellMath.SphereOverlapsStella(in f, new float3(0.1f, 0f, 0f), 50f));
        }

        // --------------------------------------------------------------
        // Cross-validated randomized poses (independent QP/LP ground truth)
        // --------------------------------------------------------------

        static void Check(bool expected, string label, System.Func<bool> predicate)
            => Assert.AreEqual(expected, predicate(), label);

        [Test]
        public void CrossValidatedCases_MatchIndependentGroundTruth()
        {
            Check(true, "stella-capsule-0", () => { var f = ShieldShellMath.CreateFrame(new float3(-10.900367f, -18.415147f, -29.178003f), new quaternion(0.032956f, -0.066474f, 0.490046f, 0.868533f), new float3(4.595042f, 2.726356f, 8.769353f)); return ShieldShellMath.CapsuleOverlapsStella(in f, new float3(-11.220633f, -16.688311f, -22.252270f), new float3(-11.085521f, -19.154161f, -25.373965f), 1.044272f); });
            Check(false, "octa-capsule-1", () => { var f = ShieldShellMath.CreateFrame(new float3(-11.790883f, 26.708299f, -0.817713f), new quaternion(0.373558f, 0.877651f, -0.250831f, 0.165129f), new float3(8.705587f, 8.479990f, 9.199678f)); return ShieldShellMath.CapsuleOverlapsOcta(in f, new float3(-8.887773f, 30.381734f, 8.236722f), new float3(-8.538738f, 28.386640f, 7.843635f), 0.539872f); });
            Check(true, "stella-box-2", () => { var f = ShieldShellMath.CreateFrame(new float3(3.534065f, -8.931984f, -17.824637f), new quaternion(0.981571f, 0.111975f, 0.129276f, 0.085250f), new float3(6.569622f, 10.873862f, 8.275613f)); return ShieldShellMath.BoxOverlapsStella(in f, new float3(0.801721f, -9.331810f, -21.213305f), new float3(-0.890445f, 0.947933f, -1.143610f), new float3(-0.015283f, -1.379024f, -1.131166f), new float3(-0.302430f, -0.112984f, 0.141827f)); });
            Check(true, "octa-sphere-3", () => { var f = ShieldShellMath.CreateFrame(new float3(8.278397f, -6.847359f, 19.047105f), new quaternion(0.989476f, 0.080663f, -0.070156f, -0.097518f), new float3(9.916907f, 9.981205f, 0.915920f)); return ShieldShellMath.SphereOverlapsOcta(in f, new float3(1.068922f, -7.944017f, 19.834363f), 4.662934f); });
            Check(true, "stella-capsule-4", () => { var f = ShieldShellMath.CreateFrame(new float3(20.860999f, -8.830583f, -24.809750f), new quaternion(-0.243712f, 0.665185f, -0.502649f, 0.495457f), new float3(1.168362f, 10.113800f, 3.996328f)); return ShieldShellMath.CapsuleOverlapsStella(in f, new float3(20.158599f, -8.198981f, -26.263183f), new float3(21.267678f, -13.324312f, -30.330888f), 0.354539f); });
            Check(true, "stella-box-5", () => { var f = ShieldShellMath.CreateFrame(new float3(-2.728424f, 15.897948f, -10.635272f), new quaternion(-0.415077f, 0.179673f, 0.888414f, -0.078417f), new float3(1.866308f, 10.360827f, 9.117675f)); return ShieldShellMath.BoxOverlapsStella(in f, new float3(-4.512833f, 20.229903f, -9.890835f), new float3(-0.206407f, -0.247665f, -0.248702f), new float3(-0.342232f, 0.441657f, -0.155785f), new float3(0.319347f, 0.113946f, -0.378508f)); });
            Check(true, "octa-box-6", () => { var f = ShieldShellMath.CreateFrame(new float3(-11.989249f, 16.745015f, -12.162215f), new quaternion(-0.203072f, 0.423641f, -0.027989f, 0.882330f), new float3(2.136160f, 8.868992f, 5.671671f)); return ShieldShellMath.BoxOverlapsOcta(in f, new float3(-11.918923f, 16.568702f, -11.006743f), new float3(-1.454035f, 0.359064f, -1.124487f), new float3(0.050681f, -1.853201f, -0.657287f), new float3(-0.175401f, -0.076568f, 0.202356f)); });
            Check(true, "stella-sphere-7", () => { var f = ShieldShellMath.CreateFrame(new float3(-27.528654f, -2.036539f, 29.894770f), new quaternion(-0.370446f, 0.924828f, 0.011572f, 0.085613f), new float3(9.443794f, 6.261132f, 1.801130f)); return ShieldShellMath.SphereOverlapsStella(in f, new float3(-27.535446f, -7.274779f, 28.871342f), 0.863096f); });
            Check(false, "stella-sphere-8", () => { var f = ShieldShellMath.CreateFrame(new float3(19.973589f, 14.645259f, -5.728133f), new quaternion(0.618382f, -0.135290f, 0.297665f, 0.714630f), new float3(11.012077f, 5.373423f, 6.327353f)); return ShieldShellMath.SphereOverlapsStella(in f, new float3(26.152778f, 16.355697f, 2.241304f), 0.712209f); });
            Check(true, "octa-sphere-9", () => { var f = ShieldShellMath.CreateFrame(new float3(12.857499f, -6.268677f, -17.894971f), new quaternion(-0.187818f, 0.488626f, 0.800120f, 0.292877f), new float3(10.595732f, 7.559402f, 7.736155f)); return ShieldShellMath.SphereOverlapsOcta(in f, new float3(9.777521f, -5.543525f, -17.703179f), 0.264234f); });
            Check(true, "octa-capsule-10", () => { var f = ShieldShellMath.CreateFrame(new float3(28.803855f, -29.005521f, 12.821989f), new quaternion(0.205897f, 0.975079f, 0.030259f, -0.076883f), new float3(9.869722f, 2.578700f, 3.516221f)); return ShieldShellMath.CapsuleOverlapsOcta(in f, new float3(35.464168f, -33.232308f, 12.733941f), new float3(36.708299f, -32.748582f, 12.302355f), 1.415076f); });
            Check(true, "octa-box-11", () => { var f = ShieldShellMath.CreateFrame(new float3(-25.434495f, 21.099063f, 18.774993f), new quaternion(0.218532f, -0.114408f, -0.782511f, 0.571692f), new float3(6.195661f, 2.236172f, 6.779659f)); return ShieldShellMath.BoxOverlapsOcta(in f, new float3(-24.135992f, 21.867767f, 18.395069f), new float3(-1.190945f, 1.173218f, 0.573094f), new float3(1.332125f, 1.594608f, -0.496141f), new float3(-0.850938f, 0.098156f, -1.969272f)); });
            Check(true, "stella-capsule-12", () => { var f = ShieldShellMath.CreateFrame(new float3(-5.986954f, 1.109268f, 13.642967f), new quaternion(-0.569411f, 0.061362f, -0.629639f, 0.524939f), new float3(6.726306f, 5.981878f, 10.722451f)); return ShieldShellMath.CapsuleOverlapsStella(in f, new float3(-8.496936f, -3.376723f, 14.309869f), new float3(-13.199826f, -4.789145f, 2.621658f), 2.330417f); });
            Check(true, "stella-box-13", () => { var f = ShieldShellMath.CreateFrame(new float3(17.961871f, -10.285785f, 28.197546f), new quaternion(-0.435065f, 0.766527f, -0.292630f, 0.370840f), new float3(2.043327f, 7.963496f, 8.118235f)); return ShieldShellMath.BoxOverlapsStella(in f, new float3(17.413226f, -11.096188f, 28.212994f), new float3(-1.588358f, -0.140929f, -0.872190f), new float3(-0.757655f, 1.331911f, 1.164566f), new float3(0.594761f, 1.496840f, -1.324987f)); });
            Check(true, "octa-box-14", () => { var f = ShieldShellMath.CreateFrame(new float3(-0.545393f, -19.796942f, 27.496924f), new quaternion(-0.207420f, 0.846375f, 0.215092f, -0.440865f), new float3(8.302963f, 3.969884f, 2.141165f)); return ShieldShellMath.BoxOverlapsOcta(in f, new float3(2.067024f, -18.029455f, 24.794932f), new float3(-1.129421f, -1.097826f, -0.448551f), new float3(0.538283f, -0.129679f, -1.037972f), new float3(1.142562f, -1.493790f, 0.779150f)); });
            Check(true, "stella-capsule-15", () => { var f = ShieldShellMath.CreateFrame(new float3(-24.000437f, -2.109236f, -17.966490f), new quaternion(0.971205f, -0.165166f, 0.028806f, 0.169269f), new float3(9.877159f, 7.983767f, 10.683918f)); return ShieldShellMath.CapsuleOverlapsStella(in f, new float3(-26.674458f, -4.647687f, -20.508258f), new float3(-20.644439f, -5.662399f, -17.439590f), 0.216221f); });
            Check(true, "stella-sphere-16", () => { var f = ShieldShellMath.CreateFrame(new float3(18.411479f, 14.325127f, 29.466227f), new quaternion(0.479946f, 0.697491f, -0.059701f, 0.528767f), new float3(8.730227f, 1.656040f, 9.797475f)); return ShieldShellMath.SphereOverlapsStella(in f, new float3(23.273428f, 12.892990f, 23.066358f), 0.359359f); });
            Check(true, "stella-sphere-17", () => { var f = ShieldShellMath.CreateFrame(new float3(-9.105730f, -15.276194f, -11.974434f), new quaternion(0.781819f, 0.543891f, 0.272378f, -0.136936f), new float3(11.434728f, 8.433261f, 2.028954f)); return ShieldShellMath.SphereOverlapsStella(in f, new float3(-7.616124f, -13.869658f, -11.559752f), 2.638190f); });
            Check(true, "stella-sphere-18", () => { var f = ShieldShellMath.CreateFrame(new float3(-1.951004f, 12.515864f, -10.831332f), new quaternion(-0.640807f, 0.168443f, 0.147649f, 0.734298f), new float3(2.485729f, 7.180386f, 11.396447f)); return ShieldShellMath.SphereOverlapsStella(in f, new float3(-2.937574f, 15.669344f, -16.238480f), 1.173620f); });
            Check(true, "stella-box-19", () => { var f = ShieldShellMath.CreateFrame(new float3(11.339493f, -9.659293f, 26.307869f), new quaternion(0.089779f, 0.902831f, -0.000812f, 0.420517f), new float3(7.881927f, 9.591787f, 1.966672f)); return ShieldShellMath.BoxOverlapsStella(in f, new float3(12.291345f, -1.220697f, 27.262845f), new float3(-0.780776f, 0.064615f, -0.124274f), new float3(0.167681f, -0.973463f, -1.559638f), new float3(-0.296646f, -1.656876f, 1.002262f)); });
            Check(true, "octa-capsule-20", () => { var f = ShieldShellMath.CreateFrame(new float3(-2.342583f, 13.149628f, -17.146937f), new quaternion(-0.402808f, 0.650667f, 0.579154f, -0.280996f), new float3(2.594394f, 2.989003f, 6.427806f)); return ShieldShellMath.CapsuleOverlapsOcta(in f, new float3(-6.768712f, 14.273455f, -18.104918f), new float3(-8.232085f, 20.043310f, -17.524192f), 2.935787f); });
            Check(true, "octa-sphere-21", () => { var f = ShieldShellMath.CreateFrame(new float3(-24.515697f, -12.401567f, 26.207432f), new quaternion(-0.252858f, 0.083751f, 0.376317f, 0.887375f), new float3(1.410610f, 7.291485f, 8.299308f)); return ShieldShellMath.SphereOverlapsOcta(in f, new float3(-27.070079f, -9.166237f, 26.443713f), 0.597979f); });
            Check(true, "octa-capsule-22", () => { var f = ShieldShellMath.CreateFrame(new float3(16.427725f, 0.928138f, 27.184081f), new quaternion(0.038319f, -0.507053f, 0.560042f, 0.654050f), new float3(7.866933f, 5.729168f, 4.232667f)); return ShieldShellMath.CapsuleOverlapsOcta(in f, new float3(16.865681f, -0.093626f, 28.423162f), new float3(14.196725f, -0.964289f, 25.124214f), 2.326709f); });
            Check(true, "octa-box-23", () => { var f = ShieldShellMath.CreateFrame(new float3(21.960704f, 13.594946f, 22.215853f), new quaternion(-0.394923f, 0.373113f, -0.131277f, 0.829210f), new float3(11.849767f, 6.122659f, 1.131010f)); return ShieldShellMath.BoxOverlapsOcta(in f, new float3(20.364096f, 16.903060f, 20.934516f), new float3(-0.167533f, -0.021834f, 0.241377f), new float3(0.897121f, 0.664039f, 0.682734f), new float3(-0.576596f, 1.089156f, -0.301678f)); });
        }

        // --------------------------------------------------------------
        // Consistency: the stella is a strict subset of the octahedron's
        // circumscribing behavior at the core, a strict superset at the tips
        // --------------------------------------------------------------

        [Test]
        public void Stella_ContainsOctahedronCore_ButOnlyTipsBeyondIt()
        {
            var f = UnitShell();
            // The octahedron IS the two tets' intersection: any point inside the
            // octahedron is inside both tets, so inside the stella union.
            float3 corePoint = new float3(0.8f, 0.7f, -0.9f); // |x|+|y|+|z| = 2.4 < 3
            Assert.IsTrue(ShieldShellMath.SphereOverlapsOcta(in f, corePoint, 0.01f));
            Assert.IsTrue(ShieldShellMath.SphereOverlapsStella(in f, corePoint, 0.01f));
            // A spike tip region is stella-only: outside the octahedron.
            float3 nearTip = new float3(2.5f, 2.5f, 2.5f); // |x|+|y|+|z| = 7.5 > 3
            Assert.IsFalse(ShieldShellMath.SphereOverlapsOcta(in f, nearTip, 0.01f));
            Assert.IsTrue(ShieldShellMath.SphereOverlapsStella(in f, nearTip, 0.01f));
        }
    }
}
#endif
