using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Swell" - Cleave's intensity-2 arena: seven great CORRUGATED SHEETS, stacked through the
    /// cell at six different angles, each rolling on its own wavelength. Where the panes
    /// (intensity 1) teach that a sword rewards committing to a line, the swell teaches the thing
    /// that makes that interesting - <b>a surface has a GRAIN, and the line only pays if you read
    /// it.</b>
    ///
    /// Every sheet ripples along one in-plane direction (<c>Across</c>) and is therefore smooth
    /// along the other (<c>Along</c>). Fly a trough and the deck stays level under the blade for
    /// its whole length - the longest uninterrupted cut in the mode. Fly the same sheet ninety
    /// degrees off and you are climbing and dropping through every ridge, clipping a handful of
    /// prisms per crest and nothing in between. Same sheet, same speed, several times the score:
    /// the difference is entirely whether the pilot found the grain.
    ///
    /// Four things make that legible instead of merely true:
    ///
    ///   • <b>The grain is PAINTED.</b> A prism's domain is chosen by where it sits on the wave -
    ///     Gold on the crests, Jade in the troughs, Blue on the flanks - so the corrugation reads
    ///     as colour banding from right across the arena, before you are close enough to see the
    ///     relief. Colour carries information here; it is not decoration. (Scoring does not care:
    ///     every non-roster prism is hostile to everyone, in any colour.)
    ///   • <b>The crests are the traps.</b> The only <see cref="PrismKind.Danger"/> prisms in this
    ///     arena sit on the ridge lines. So the grain is worth reading TWICE - the trough is both
    ///     the richest cut and the safe one, and a pilot carving across the grain is the one who
    ///     keeps catching the punishment. It is a small trap budget on purpose
    ///     (<see cref="DangerEveryNthCrestPrism"/> of the prisms that are already near a peak).
    ///   • <b>Sheets have an UNDERSIDE.</b> A zero-thickness surface is invisible edge-on and a
    ///     blade can pass clean through the plane of it without touching anything. A half-density
    ///     reef layer hangs <see cref="ReefDrop"/> below each sheet with its planks turned
    ///     CROSSWISE, so the sheet has body, reads from any angle, and pays a second time to a cut
    ///     that goes deep enough.
    ///   • <b>Sheets FRAY rather than ending.</b> The void threshold ramps up with distance from a
    ///     sheet's centre (<see cref="EdgeFrayStart"/>), so a sheet dissolves into open water
    ///     instead of stopping at a hard circular rim. The panes are framed slabs and say so; the
    ///     swell is weather.
    ///
    /// The working disc is shrunk by the wave amplitude (<see cref="Sheet.Rho"/>) rather than
    /// clipped afterwards: a displaced point must still be inside
    /// <see cref="SliceArenaGeometry.OuterRadius"/>, and solving that up front means no prism is
    /// ever generated and thrown away.
    ///
    /// Nothing is <see cref="PrismKind.Shielded"/> or <see cref="PrismKind.SuperShielded"/> - see
    /// CLEAVE.md for why (an AI can never energize its blade, so unbreakable mass is mass an
    /// all-AI domain cannot score against). Deterministic per seed like every cell environment.
    /// </summary>
    public class SpawnableSwell : CellEnvironmentSpawnableBase
    {
        const float R = SliceArenaGeometry.OuterRadius;

        // ── The deck ─────────────────────────────────────────────────────────
        /// <summary>Spacing ALONG a ridge - under <see cref="PlankLength"/>, so a trough line is
        /// continuous mass rather than a dotted line.</summary>
        const float PlankStep = 12f;
        const float PlankLength = 16f;
        /// <summary>Spacing ACROSS the grain, i.e. ridge to ridge sampling.</summary>
        const float RibStep = 15f;

        /// <summary>The reef: a half-density layer this far below the deck along its own normal,
        /// laid crosswise.</summary>
        const float ReefDrop = 13f;

        // ── Fraying ──────────────────────────────────────────────────────────
        const float VoidThreshold = 0.22f;
        const float VoidFreq = 0.014f;
        /// <summary>Fraction of a sheet's radius at which the edge fray begins. Inside this the
        /// sheet is solid weather; outside it the void threshold climbs to 1 at the rim.</summary>
        const float EdgeFrayStart = 0.78f;

        // ── Traps ────────────────────────────────────────────────────────────
        /// <summary>|sin| above which a prism counts as "on the ridge" for trap purposes.</summary>
        const float CrestBand = 0.93f;
        const int DangerEveryNthCrestPrism = 5;
        /// <summary>|sin| above/below which a prism is painted as crest / trough.</summary>
        const float PaintBand = 0.55f;

        /// <summary>
        /// One sheet. The normal is aimed exactly as a pane's is; <c>GrainDeg</c> then spins the
        /// RIDGE direction inside the plane. <c>Wavelength</c>/<c>Amplitude</c> are the primary
        /// corrugation (across the grain); <c>SwellLength</c>/<c>SwellAmp</c> are a weaker second
        /// wave ALONG the ridges, which is what stops the sheet reading as machined sheet metal
        /// and makes the troughs themselves rise and fall as you fly them.
        /// </summary>
        readonly struct SheetSpec
        {
            public readonly float TiltDeg, AzimuthDeg, OffsetFraction, GrainDeg;
            public readonly float Wavelength, Amplitude, SwellLength, SwellAmp, PhaseDeg;

            public SheetSpec(float tilt, float azimuth, float offset, float grain,
                             float wavelength, float amplitude,
                             float swellLength, float swellAmp, float phase)
            {
                TiltDeg = tilt; AzimuthDeg = azimuth; OffsetFraction = offset; GrainDeg = grain;
                Wavelength = wavelength; Amplitude = amplitude;
                SwellLength = swellLength; SwellAmp = swellAmp; PhaseDeg = phase;
            }
        }

        /// <summary>
        /// Seven sheets, authored rather than generated for the same reason the panes are: which
        /// approach is cheap and where the arena is thick is a gameplay surface. The wavelengths
        /// deliberately span 3x (78 to 240) - a short-wave sheet is a washboard whose troughs are
        /// barely wider than the vessel, a long-wave sheet is a pair of enormous valleys you can
        /// boost down, and having both in one arena is what makes "read the grain" a skill rather
        /// than a habit.
        /// </summary>
        static readonly SheetSpec[] SheetSpecs =
        {
            //             tilt  azim   offset grain  wavelen amp  swellLen swellAmp phase
            new SheetSpec(   0f,   0f,  0.00f,   0f,    240f,  54f,   430f,  20f,    0f),
            new SheetSpec(  37f,  63f, -0.31f,  64f,    150f,  40f,   360f,  16f,   70f),
            new SheetSpec(  72f, 141f,  0.26f,  18f,     96f,  27f,   300f,  13f,  145f),
            new SheetSpec(  53f, 218f, -0.18f,  81f,    186f,  46f,   410f,  18f,  220f),
            new SheetSpec(  88f, 289f,  0.35f,  41f,     78f,  22f,   260f,  11f,  300f),
            new SheetSpec(  26f, 338f, -0.40f,  27f,    124f,  33f,   330f,  15f,   35f),
            new SheetSpec(  61f,  19f,  0.14f,  73f,    168f,  43f,   380f,  17f,  255f),
        };

        protected override int DefaultSeed => 392;

        protected override int BuildParameterHash() => System.HashCode.Combine(
            nameof(SpawnableSwell), R, PlankStep, PlankLength, RibStep, ReefDrop,
            System.HashCode.Combine(VoidThreshold, VoidFreq, EdgeFrayStart, CrestBand,
                                    DangerEveryNthCrestPrism, PaintBand),
            SheetHash());

        static int SheetHash()
        {
            int h = 17;
            foreach (var s in SheetSpecs)
                h = h * 31 + System.HashCode.Combine(
                    System.HashCode.Combine(s.TiltDeg, s.AzimuthDeg, s.OffsetFraction, s.GrainDeg),
                    s.Wavelength, s.Amplitude, s.SwellLength, s.SwellAmp, s.PhaseDeg);
            return h;
        }

        protected override int LayCapacity => 16000;

        /// <summary>A sheet's resolved frame plus its wave terms. <c>Along</c> is the direction
        /// ridges RUN (and therefore the flyable line); <c>Across</c> is the direction the
        /// corrugation varies along.</summary>
        readonly struct Sheet
        {
            public readonly Vector3 Normal, Along, Across, Centre;
            public readonly float Rho;                       // usable in-plane radius
            public readonly float K, Amp, KSwell, SwellAmp, Phase;

            public Sheet(in SheetSpec spec)
            {
                var orientation = Quaternion.AngleAxis(spec.AzimuthDeg, Vector3.up)
                                * Quaternion.AngleAxis(spec.TiltDeg, Vector3.forward);
                Normal = (orientation * Vector3.up).normalized;

                var seedDir = Mathf.Abs(Vector3.Dot(Normal, Vector3.forward)) > 0.95f
                    ? Vector3.right : Vector3.forward;
                var e1 = Vector3.Cross(Normal, seedDir).normalized;
                var e2 = Vector3.Cross(Normal, e1);

                float g = spec.GrainDeg * Mathf.Deg2Rad;
                Along = (e1 * Mathf.Cos(g) + e2 * Mathf.Sin(g)).normalized;
                // Cross(Along, Across) == Normal, so (Along, Across, Normal) is right-handed and
                // the surface normal below comes out on the authored side rather than inverted.
                Across = Vector3.Cross(Normal, Along);

                float offset = spec.OffsetFraction * R;
                Centre = Normal * offset;

                // Shrink the working disc by everything the waves can add along the normal, so a
                // DISPLACED point is still inside the arena. Solving it here beats generating
                // prisms and rejecting them, and it keeps the offline budget model exact.
                float reach = Mathf.Abs(offset) + spec.Amplitude + spec.SwellAmp + ReefDrop;
                Rho = Mathf.Sqrt(Mathf.Max(1f, R * R - reach * reach));

                K = 2f * Mathf.PI / spec.Wavelength;
                Amp = spec.Amplitude;
                KSwell = 2f * Mathf.PI / spec.SwellLength;
                SwellAmp = spec.SwellAmp;
                Phase = spec.PhaseDeg * Mathf.Deg2Rad;
            }

            /// <summary>Signed wave height at (along, across), in [-1, 1] before amplitude.</summary>
            public float Crest(float across) => Mathf.Sin(K * across + Phase);

            public float Height(float along, float across) =>
                Amp * Crest(across) + SwellAmp * Mathf.Sin(KSwell * along);

            public Vector3 At(float along, float across) =>
                Centre + Along * along + Across * across + Normal * Height(along, across);

            /// <summary>Exact dP/d(along): the ridge direction, tipped by the secondary swell.</summary>
            public Vector3 TangentAlong(float along) =>
                Along + Normal * (SwellAmp * KSwell * Mathf.Cos(KSwell * along));

            /// <summary>Exact dP/d(across): climbs the corrugation.</summary>
            public Vector3 TangentAcross(float across) =>
                Across + Normal * (Amp * K * Mathf.Cos(K * across + Phase));

            public Vector3 SurfaceNormal(float along, float across) =>
                Vector3.Cross(TangentAlong(along), TangentAcross(across)).normalized;
        }

        protected override void BuildEnvironment()
        {
            for (int s = 0; s < SheetSpecs.Length; s++)
                BuildSheet(new Sheet(SheetSpecs[s]), s);
        }

        void BuildSheet(in Sheet sheet, int sheetIndex)
        {
            int ribs = Mathf.Max(1, Mathf.FloorToInt(sheet.Rho / RibStep));

            for (int rib = -ribs; rib <= ribs; rib++)
            {
                float across = rib * RibStep;
                float half = Mathf.Sqrt(Mathf.Max(0f, sheet.Rho * sheet.Rho - across * across));
                if (half < PlankLength) continue;

                float crest = sheet.Crest(across);
                var deckDom = crest > PaintBand ? Domains.Gold
                            : crest < -PaintBand ? Domains.Jade
                            : Domains.Blue;
                bool onRidge = Mathf.Abs(crest) > CrestBand;

                int planks = Mathf.Max(1, Mathf.FloorToInt(half / PlankStep));
                float stagger = (rib & 1) == 0 ? 0f : PlankStep * 0.5f;
                bool reefRib = (rib & 1) == 0;      // the reef is half density, by rib

                for (int i = -planks; i <= planks; i++)
                {
                    float along = i * PlankStep + stagger;
                    if (Mathf.Abs(along) > half) continue;

                    var pos = sheet.At(along, across);
                    if (Frayed(pos, along, across, sheet.Rho)) continue;

                    var tangent = sheet.TangentAlong(along);
                    var normal = sheet.SurfaceNormal(along, across);

                    // Traps ride the ridge line only, thinned so a crest is a hazard to respect
                    // rather than a wall. Indexed on the global walk so they spread along the
                    // ridge instead of clustering at one end.
                    bool danger = onRidge
                        && (sheetIndex * 17 + rib * 101 + i) % DangerEveryNthCrestPrism == 0;

                    Emit(pos, SpawnPoint.LookRotation(tangent, normal),
                        Jit(new Vector3(3.4f, 3.4f, PlankLength)), deckDom,
                        danger ? PrismKind.Danger : PrismKind.Plain);

                    if (!reefRib) continue;

                    // The underside. Planks turned CROSSWISE (forward = the across-tangent) so the
                    // reef reads as a truss under the deck rather than as a second copy of it.
                    var reefPos = pos - normal * ReefDrop;
                    Emit(reefPos, SpawnPoint.LookRotation(sheet.TangentAcross(across), normal),
                        Jit(new Vector3(3.0f, 3.0f, PlankLength)), Domains.Ruby);
                }
            }
        }

        /// <summary>
        /// Void test. Below <see cref="EdgeFrayStart"/> of the sheet's radius this is the ordinary
        /// weathering cull; beyond it the threshold climbs linearly to 1, so the sheet thins out
        /// and disappears instead of ending at a rim.
        /// </summary>
        bool Frayed(Vector3 pos, float along, float across, float rho)
        {
            float t = Mathf.Sqrt(along * along + across * across) / rho;
            float threshold = t <= EdgeFrayStart
                ? VoidThreshold
                : Mathf.Lerp(VoidThreshold, 1f, (t - EdgeFrayStart) / (1f - EdgeFrayStart));

            return N01(pos.x * VoidFreq, pos.y * VoidFreq, pos.z * VoidFreq, 3) < threshold;
        }
    }
}
