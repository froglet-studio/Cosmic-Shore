using System;
using System.Collections.Generic;

// Pure C# - no UnityEngine - so the same file compiles and RUNS headless against the Omni Combine Lab
// (Tools/Build/omni_combine_parity/). The Unity side (OmniCombinePlayer) parses the JSON with
// JsonUtility and copies the positions this writes into its meshes.
namespace CosmicShore.Utility
{
    // ------------------------------------------------------------------ data (JsonUtility schema)
    // Authored by Tools/Build/author_omni_combine_asset.py into
    // Assets/_SO_Assets/Environment/OmniCombine/OmniCombineData.json, in Unity's axes. Field names are the
    // JSON keys: do not rename one without re-running the generator.

    [Serializable] public sealed class OmniCombineData
    {
        public string source;
        public string[] order;
        public OmniCombineMeshData omni;
        public OmniCombineCrystalData[] crystals;
        public OmniCombineSquaresData squares;
        public OmniCombineDepthData depth;
        public OmniCombineFlipData flip;
        public OmniCombineArcData arcs;
        public OmniCombineTumbleData tumble;
        public OmniCombineSpinData spin;
        /// <summary>Row-major 3x3: turns the game's OmniCrystalTriangles.asset onto this omni's triangle plates.</summary>
        public float[] omniTriangleRotation;
    }

    [Serializable] public sealed class OmniCombineMeshData
    {
        public float[] verts;
        public int[] tris;
        public int[] plate;
        /// <summary>Per omni plate: the index into <see cref="OmniCombineData.order"/> of the element that lands there.</summary>
        public int[] plateOwner;
    }

    [Serializable] public sealed class OmniCombineCrystalData
    {
        public string element;
        public float[] basis;
        public int[] tris;
        public int[] plate;
        /// <summary>Per plate: the omni plate it lands on (Charge 5 and Mass 3 share one).</summary>
        public int[] plateSite;
        public float[] offset;
        /// <summary>Mass: stage units per unit of the game's MassCrystalPrisms mesh this crystal IS (0 for the art file's).</summary>
        public float meshScale;
        /// <summary>Per vertex: where it lands in the omni (Charge/Mass wedges, Time's rigid pose, Space's Key 1).</summary>
        public float[] final;
        /// <summary>Charge/Mass: the pose after each petal's back sucks in (Mass: the basis).</summary>
        public float[] stage1;
        /// <summary>Charge/Mass: per plate, the site axis the petals untwist about, and by how many degrees.</summary>
        public float[] plateAxis;
        public float[] plateTwist;
        /// <summary>Time: per plate, the rigid turn onto its slot (w, x, y, z).</summary>
        public float[] plateQuat;
        public OmniCombineIdleData idle;
    }

    [Serializable] public sealed class OmniCombineIdleData
    {
        public float fps;
        public string[] names;
        public OmniCombineDeltaData[] keys;
        /// <summary>Frame-major, one weight per key.</summary>
        public float[] weights;
    }

    [Serializable] public sealed class OmniCombineDeltaData { public float[] delta; }

    [Serializable] public sealed class OmniCombineSquaresData
    {
        public float fps;
        public OmniCombineSquareKeyData[] keys;
        public float[] weights;
    }

    [Serializable] public sealed class OmniCombineSquareKeyData
    {
        public int[] verts;
        public float[] delta;
    }

    [Serializable] public sealed class OmniCombineDepthData
    {
        public float open, seal, power, omniReach, omniSizeStart, massReach, massSizeStart;
        /// <summary>The thickness baked into MassCrystalPrisms / OmniCrystalTriangles. The combine morphs the THIN
        /// prisms the art's plates are and the converter thickens them in the shader, so a landed Mass prism
        /// matches the triangle mesh it hands off to.</summary>
        public float massThickness, omniThickness;
    }

    [Serializable] public sealed class OmniCombineFlipData
    {
        public float loop, first, stagger;
        public OmniCombineFlipCurves lead, later;
    }

    /// <summary>Each curve is flat (time, value) pairs; an empty one evaluates to the curve's rest value.</summary>
    [Serializable] public sealed class OmniCombineFlipCurves { public float[] flip, scale, dip, slide; }

    [Serializable] public sealed class OmniCombineArcData { public float speed, length, duty; }

    [Serializable] public sealed class OmniCombineTumbleData { public float speed; public float[] direction; }

    [Serializable] public sealed class OmniCombineSpinData { public float cycleSpeed, pause; }

    // ------------------------------------------------------------------ the knobs one play reads
    /// <summary>
    /// One combine's choreography. The defaults are the Omni Combine Lab's round-2 SHIPPED values
    /// (Docs/Studios/OMNI_COMBINE_LAB.md); <c>OmniCombineTuningSO</c> draws a fresh set per play.
    /// </summary>
    public struct OmniCombineParams
    {
        public int Seed;
        /// <summary>Seconds the four idle, each doing its own effect, before the first sets off.</summary>
        public double Lead;
        /// <summary>0 the art file's row, 1 a ring, 2 a tetrahedron.</summary>
        public int Layout;
        /// <summary>Start distance in omni radii.</summary>
        public double Spread;
        /// <summary>Turn of the whole start layout about the vertical, radians (the lab has none).</summary>
        public double LayoutYaw;
        public double Stagger, Flight, Ease, Arc, Swirl, Tumble;
        public double MorphStart, MorphLength, SiteSpread, Suck, EffectFade;
        /// <summary>Offset added to the flip wave, spin and squares clocks, so two plays never flip in step.</summary>
        public double EffectPhase;
        /// <summary>The Mass prisms' depth effect is drawn by the game's depth-prism shader (the converter), not
        /// moved here on the CPU (the lab, whose stateless model this core otherwise reproduces).</summary>
        public bool ShaderDepth;
        /// <summary>The Charge crystal turns as it idles (the lab's port of JustRotate on CrystalCharge.prefab). The
        /// converter turns it off (designer, 2026-10-13: "a vestigial rotation that we don't want").</summary>
        public bool IdleTumble;
        /// <summary>The woken omni moves as the GAME's omni does (Omni Motion Lab round 5, the converter): no squares weave
        /// here - the caller turns the squares on its cadence - and the flip wave plays only the wave the caller cues
        /// (<see cref="OmniCombineCore.OmniPose(double, in OmniCombineParams, double, D3, float[], float[], double, long)"/>).
        /// Off in the lab, so parity holds.</summary>
        public bool GameMotion;
        /// <summary>Every crystal but the first comes in with a bit of extra spacing and comes down to perfect spacing
        /// (designer, 2026-10-13: "the time rhombuses come in with a bit of extra spacing and then come down to perfect
        /// spacing in order to socket in from the outside"): each plate morphs toward its slot pushed out this fraction of
        /// the slot's radius, and HOLDS that until the crystal is centred on the omni (its flight is over), then eases it
        /// to nothing over <see cref="SocketSeconds"/> (designer, 2026-10-14: "keep the triangles at maximum column height
        /// till we get the crystal centered then we can decrease the radial distance so they socket into place ... you can
        /// do this a bit more for the others"), so its last motion is straight in. The morph itself is untouched. 0 (the
        /// lab) = none. The Mass crystal's far larger clearance is its depth columns (<see cref="MassRaise"/>).</summary>
        public double SocketSpacing;
        /// <summary>Seconds a crystal takes to socket in once it is centred on the omni: its plates' extra spacing comes
        /// down to nothing, and the Mass prisms, held up their columns all the way in (<see cref="MassRaise"/>), come down
        /// them into their slots. Every crystal is centred before it sockets, so the motion is radial about the omni's own centre:
        /// a plate moves straight in through its own slot and never across a neighbour's. <see cref="OmniCombineCore.MorphEnds"/>
        /// waits for the last one. 0 (the lab) = none.</summary>
        public double SocketSeconds;
        /// <summary>How far up its depth column each Mass prism stands while the crystal flies in, 0 (sealed, the lab) .. 1
        /// (the top of the column): the clearance its thick prisms need to come straight down into their slots instead of
        /// sliding along the shell into them (designer, 2026-10-14: "keep the triangles at maximum column height till we
        /// get the crystal centered"). Below 1 on purpose: a prism at the top of its column, with the crystal nearly centred,
        /// stands out where the waiting Time crystal is (Tools/Build/converter_round5_harness, section 9).</summary>
        public double MassRaise;
        /// <summary>Radians each crystal's flight turns about the vertical on the way in (index = element; null, the lab =
        /// none), so it arrives from the side the camera will be on and the rhombi that have to cross the omni do it behind
        /// it (designer, 2026-10-13: "the solution i want has all its clipping occluded"). The turn starts at nothing - a
        /// crystal sets off from exactly where it idles - and is complete by <see cref="OmniCombineCore.ApproachTurnEnds"/>
        /// of its flight, well before its shell meets the omni. <see cref="OmniCombineCore.FaceCamera"/> sets it.
        /// The turn MOVES WITH THE CAMERA (designer, 2026-10-13: "optimize the camera side approach to move with the
        /// camera"): on top of it the path turns by however far the camera has circled since it was aimed
        /// (<see cref="CameraAzimuth"/> - <see cref="ApproachCameraAzimuth"/>), so a crystal stays on the camera's side
        /// all the way in, however fast or slow the camera goes.</summary>
        public double[] ApproachTurn;
        /// <summary>The camera's azimuth about the stage's vertical when <see cref="ApproachTurn"/> was aimed (radians,
        /// unwrapped: it keeps counting past a whole turn).</summary>
        public double ApproachCameraAzimuth;
        /// <summary>The camera's azimuth NOW, on the same unwrapped count; the caller sets it every frame. With
        /// <see cref="ApproachTurn"/> null it is ignored.</summary>
        public double CameraAzimuth;
        /// <summary>Each crystal's own arrival elevation (radians above the stage's horizon, index = element), measured
        /// when aimed. The flight's start height and its upward arc bring a crystal in from well above the camera
        /// (designer, 2026-10-13: "the mass triangles especially come in from the top which presents visible clipping to
        /// the camera viewing from the side"); the path is tipped by <see cref="CameraElevation"/> minus this, so it
        /// arrives at the camera's height as well as from its side.</summary>
        public double[] ApproachElevation;
        /// <summary>The camera's elevation above the stage's horizon NOW (radians); the caller sets it every frame.</summary>
        public double CameraElevation;
        /// <summary>How far above the camera's own height a crystal arrives (radians; 0 = along the camera's line of
        /// sight, negative = below it).</summary>
        public double ApproachAboveCamera;

        public static OmniCombineParams LabDefault => new OmniCombineParams
        {
            Seed = 7, Lead = 2.5, Layout = 2, Spread = 3, LayoutYaw = 0,
            Stagger = 1.2, Flight = 4.6, Ease = 2, Arc = 0.4, Swirl = 0, Tumble = 0.25,
            MorphStart = 0.1, MorphLength = 0.8, SiteSpread = 0.5, Suck = 0.35, EffectFade = 0.7, EffectPhase = 0,
            IdleTumble = true,
        };
    }

    /// <summary>
    /// The four elemental crystals idling with their own effects, flying in one after another and turning
    /// into their shares of the omni crystal, which then wakes with all four effects - the Omni Combine Lab's
    /// <c>pose</c> / <c>omniPose</c> (Docs/Studios/OmniCombineLab.html), ported line for line so a seed plays
    /// the same combine in both. Every number it reads comes from the generated data; nothing here is tuned.
    ///
    /// Pure and stateless: the pose at time t is a function of (t, params, the camera's direction), so a
    /// scrub, a replay and the headless parity test all agree.
    /// </summary>
    public sealed class OmniCombineCore
    {
        public const int Space = 0, Charge = 1, Mass = 2, Time = 3;
        /// <summary>The designer's order (2026-10-13): Space lays the negative space, Time locks in last.</summary>
        public static readonly string[] Order = { "Space", "Charge", "Mass", "Time" };
        /// <summary>The lab's seeding order (its data order), kept so one seed means one combine everywhere.</summary>
        static readonly int[] RngOrder = { Charge, Mass, Space, Time };
        const int TimeFlipSalt = 11, OmniFlipSalt = 23;
        readonly double[] _slerp = new double[4];

        public readonly OmniCombineData Data;
        public readonly double OmniRadius;
        readonly CrystalModel[] _crystals = new CrystalModel[4];
        readonly Plate[] _omniPlates;
        readonly FlipRig _timeRig, _omniRig;
        readonly D3 _tumbleAxis;
        readonly double _tumbleSpeed;
        readonly double[] _squareWeights, _spinWeights;

        // per-seed draws
        int _seededWith = int.MinValue;
        readonly D3[] _tumbleAxes = new D3[4];

        public OmniCombineCore(OmniCombineData data)
        {
            Data = data ?? throw new ArgumentNullException(nameof(data));
            if (data.crystals == null || data.crystals.Length != 4)
                throw new ArgumentException("the combine needs exactly four crystals");
            for (int k = 0; k < 4; k++)
            {
                var c = Array.Find(data.crystals, x => x.element == Order[k])
                        ?? throw new ArgumentException($"no {Order[k]} crystal in the combine data");
                _crystals[k] = new CrystalModel(k, c);
            }
            var ov = data.omni.verts;
            for (int i = 0; i < ov.Length; i += 3)
                OmniRadius = Math.Max(OmniRadius, Math.Sqrt(ov[i] * ov[i] + ov[i + 1] * ov[i + 1] + ov[i + 2] * ov[i + 2]));
            _omniPlates = Plate.Of(ov, data.omni.plate);
            _timeRig = new FlipRig(_crystals[Time].Plates, AllPlates(_crystals[Time].Plates.Length));
            var omniTime = new List<int>();
            for (int p = 0; p < _omniPlates.Length; p++) if (data.omni.plateOwner[p] == Time) omniTime.Add(p);
            _omniRig = new FlipRig(_omniPlates, omniTime.ToArray());
            var dir = data.tumble.direction;
            _tumbleAxis = new D3(dir[0], dir[1], dir[2]).Unit;
            _tumbleSpeed = data.tumble.speed * Math.PI / 180;
            _squareWeights = new double[data.squares.keys.Length];
            var idle = _crystals[Space].Data.idle;
            _spinWeights = new double[idle?.keys?.Length ?? 0];
        }

        public int VertexCount(int element) => _crystals[element].N;
        public OmniCombineCrystalData CrystalData(int element) => _crystals[element].Data;
        public int StartVertexCount(bool omni) => (omni ? _omniRig : _timeRig).Axes.Length;
        public int[] RingSizes(bool omni, int start)
        {
            var rig = omni ? _omniRig : _timeRig;
            var sizes = new List<int>();
            foreach (var step in rig.Plans[start].Values)
            {
                while (sizes.Count <= step.Ring) sizes.Add(0);
                sizes[step.Ring]++;
            }
            return sizes.ToArray();
        }

        /// <summary>How far through a crystal's eased flight its approach turn (<see cref="OmniCombineParams.ApproachTurn"/>)
        /// is complete: while the crystal is still well clear of the omni.</summary>
        public const double ApproachTurnEnds = 0.7;
        /// <summary>The point of a crystal's flight the camera is aimed for: its shell is meeting the omni.</summary>
        const double ArrivalAt = 0.85;

        public static double Departs(int element, in OmniCombineParams p) => p.Lead + element * p.Stagger;
        /// <summary>When crystal <paramref name="element"/>'s flight is over: it is centred on the omni, every plate morphed.</summary>
        public static double Centred(int element, in OmniCombineParams p) => Departs(element, p) + p.Flight;
        /// <summary>When the last crystal (Time) has socketed in: the omni takes over from here.</summary>
        public static double MorphEnds(in OmniCombineParams p) => Centred(Time, p) + Math.Max(0, p.SocketSeconds);

        /// <summary>How far crystal <paramref name="element"/> has socketed in at <paramref name="t"/>: 0 until it is
        /// centred on the omni, then eased to 1 over <see cref="OmniCombineParams.SocketSeconds"/> (at once with none).</summary>
        public static double SocketIn(int element, double t, in OmniCombineParams p)
        {
            double since = t - Centred(element, p);
            return p.SocketSeconds > 0 ? Smooth(since / p.SocketSeconds) : since >= 0 ? 1 : 0;
        }

        /// <summary>
        /// Where the Mass prisms are on their depth columns at <paramref name="t"/> once the crystal has set off (0 at the
        /// top of a column, 1 sealed): raised by <see cref="OmniCombineParams.MassRaise"/> all the way in, and down into
        /// their slots as the crystal sockets in (designer, 2026-10-14: "for mass lets keep the triangles at maximum column
        /// height till we get the crystal centered then we can decrease the radial distance so the socket into place
        /// instead of clipping into place ... mass it should be done much more due to the extra clearance needed from their
        /// thickness"). A column is radial about the crystal's own centre, and once it is centred that is the omni's, so
        /// each prism comes straight down through its own triangle hole. The caller blends the idle depth effect into this
        /// as the crystal sets off.
        /// </summary>
        public static double MassColumnPlace(double t, in OmniCombineParams p) =>
            1 - Clamp01(p.MassRaise) * (1 - SocketIn(Mass, t, p));

        /// <summary>
        /// Every vertex of every crystal at combine time <paramref name="t"/>, in the stage's frame:
        /// <paramref name="positions"/>[element] holds xyz per vertex (sized by <see cref="VertexCount"/>).
        /// <paramref name="effect"/>[element] is how strongly that crystal's idle effect still runs (1 while
        /// it waits, 0 once it has set off) - the Unity side fades the Charge discharge by it.
        /// <paramref name="towardCamera"/> is the unit direction from the stage centre to the camera.
        /// </summary>
        public void Pose(double t, in OmniCombineParams p, D3 towardCamera, float[][] positions, float[] effect) =>
            Pose(t, p, towardCamera, positions, effect, null, null);

        /// <summary>As above, also writing each crystal's flight centre (<paramref name="centres"/>, 3 per crystal) and
        /// how far each of its plates has morphed (<paramref name="plateMorph"/>[element][plate], 0..1).</summary>
        public void Pose(double t, in OmniCombineParams p, D3 towardCamera, float[][] positions, float[] effect,
                         float[] centres, float[][] plateMorph)
        {
            Seed(p.Seed);
            double te = t + p.EffectPhase;
            for (int k = 0; k < 4; k++)
            {
                var c = _crystals[k];
                var outPos = positions[k];
                double t0 = Departs(k, p), u = Clamp01((t - t0) / p.Flight);
                double fxIdle = 1 - Smooth((t - t0) / p.EffectFade);
                double socket = k > 0 && p.SocketSpacing > 0 ? SocketIn(k, t, p) : 0;
                effect[k] = (float)Math.Max(0, fxIdle);
                Frame(k, t, p, out var centre, out double spin, out double ang);
                double mS = Math.Min(p.MorphStart, 1 - p.MorphLength);
                bool idleSpin = c.Data.idle != null && c.Data.idle.keys != null && c.Data.idle.keys.Length > 0 && fxIdle > 0;
                if (idleSpin) SpinWeights(te, c.Data.idle.names, _spinWeights);
                for (int q = 0; q < c.Plates.Length; q++)
                {
                    var pl = c.Plates[q];
                    double start = mS + p.SiteSpread * c.PlateR[q] * (1 - p.MorphLength - mS);
                    double w = Smooth((u - start) / p.MorphLength);
                    if (plateMorph != null) plateMorph[k][q] = (float)w;
                    Flip flip = default;
                    double seatScale = 1;
                    D3 seatLift = default;
                    if (fxIdle > 0 && k == Time) flip = FlipPose(_timeRig, q, pl, te, TimeFlipSalt, fxIdle);
                    if (fxIdle > 0 && k == Mass && !p.ShaderDepth)
                    {
                        double sd = SeatOf(pl.Dir, towardCamera);
                        seatScale = 1 + (Data.depth.massSizeStart - 1) * (1 - sd) * fxIdle;
                        seatLift = pl.C * ((Data.depth.massReach - 1) * (1 - sd) * fxIdle);
                    }
                    // A bit of extra spacing, held until the crystal is centred and then brought down to nothing: its slot,
                    // pushed out along itself.
                    double extra = k > 0 && p.SocketSpacing > 0 ? w * p.SocketSpacing * (1 - socket) : 0;
                    foreach (int i in pl.Idx)
                    {
                        D3 b = V(c.Data.basis, i);
                        if (idleSpin)
                            for (int key = 0; key < _spinWeights.Length; key++)
                                b += V(c.Data.idle.keys[key].delta, i) * (_spinWeights[key] * fxIdle);
                        if (flip.Valid) b = ApplyFlip(flip, pl, b);
                        if (seatScale != 1) b = pl.C + (b - pl.C) * seatScale + seatLift;
                        D3 v;
                        switch (k)
                        {
                            case Time:
                            {
                                var rq = SlerpIdentity(c.Quat[q], w, _slerp);
                                var cc = D3.Lerp(pl.C, c.FinalCentre[q], w);
                                v = cc + QuatRotate(rq, b - pl.C);
                                break;
                            }
                            case Charge:
                            {
                                double s1 = Smooth(w / p.Suck), s2 = Smooth((w - p.Suck) / (1 - p.Suck));
                                var v1 = D3.Lerp(b, b + (V(c.Data.stage1, i) - V(c.Data.basis, i)), s1);
                                v = D3.Lerp(RotAxis(c.Axis[q], c.Twist[q] * s2, v1), V(c.Data.final, i), s2);
                                break;
                            }
                            case Mass:
                                v = D3.Lerp(RotAxis(c.Axis[q], c.Twist[q] * w, b), V(c.Data.final, i), w);
                                break;
                            default:
                                v = D3.Lerp(b, V(c.Data.final, i), w);
                                break;
                        }
                        if (extra != 0) v += c.FinalCentre[q] * extra;
                        if (spin != 0) v = RotAxis(_tumbleAxis, spin, v);
                        if (ang != 0) v = RotAxis(_tumbleAxes[k], ang, v);
                        v += centre;
                        outPos[3 * i] = (float)v.X;
                        outPos[3 * i + 1] = (float)v.Y;
                        outPos[3 * i + 2] = (float)v.Z;
                    }
                }
                if (centres != null)
                {
                    centres[3 * k] = (float)centre.X;
                    centres[3 * k + 1] = (float)centre.Y;
                    centres[3 * k + 2] = (float)centre.Z;
                }
            }
        }

        /// <summary>
        /// The finished omni at time <paramref name="t"/> doing its four effects, ramped in by
        /// <paramref name="fx"/> (0 = the still omni every crystal landed on): the flip wave on its rhombi,
        /// the squares weave, the depth prisms on its triangles. (The discharge on its pentagons is the
        /// shader's.) Writes xyz per omni vertex.
        /// </summary>
        public void OmniPose(double t, in OmniCombineParams p, double fx, D3 towardCamera, float[] positions) =>
            OmniPose(t, p, fx, towardCamera, positions, null, -1, -1);

        /// <summary>As above; with <see cref="OmniCombineParams.GameMotion"/> the pose starts from <paramref name="rest"/>
        /// (the omni's vertices as the caller draws them at rest - thicker plates; null = the data's) and the flip wave
        /// plays wave <paramref name="wave"/>, <paramref name="waveSeconds"/> into the profile's own clock (a negative wave
        /// holds every rhombus at rest), as <c>CrystalFlipWave</c> plays a cued wave.</summary>
        public void OmniPose(double t, in OmniCombineParams p, double fx, D3 towardCamera, float[] positions,
                             float[] rest, double waveSeconds, long wave)
        {
            var ov = p.GameMotion && rest != null ? rest : Data.omni.verts;
            Array.Copy(ov, positions, ov.Length);
            if (fx <= 0) return;
            double te = t + p.EffectPhase;
            if (!p.GameMotion) SquaresWeights(te, Data.squares, _squareWeights);
            for (int k = 0; k < Data.squares.keys.Length && !p.GameMotion; k++)
            {
                var key = Data.squares.keys[k];
                double w = _squareWeights[k] * fx;
                for (int n = 0; n < key.verts.Length; n++)
                {
                    int j = key.verts[n];
                    positions[3 * j] += (float)(key.delta[3 * n] * w);
                    positions[3 * j + 1] += (float)(key.delta[3 * n + 1] * w);
                    positions[3 * j + 2] += (float)(key.delta[3 * n + 2] * w);
                }
            }
            for (int q = 0; q < _omniPlates.Length; q++)
            {
                var pl = _omniPlates[q];
                int owner = Data.omni.plateOwner[q];
                if (owner == Time)
                {
                    if (p.GameMotion && wave < 0) continue;
                    var f = p.GameMotion ? FlipPoseAt(_omniRig, q, pl, wave, waveSeconds, OmniFlipSalt, fx)
                                         : FlipPose(_omniRig, q, pl, te, OmniFlipSalt, fx);
                    if (!f.Valid) continue;
                    foreach (int i in pl.Idx) Write(positions, i, ApplyFlip(f, pl, V(ov, i)));
                }
                else if (owner == Mass && !p.ShaderDepth)
                {
                    double s = SeatOf(pl.Dir, towardCamera);
                    double sc = 1 + (Data.depth.omniSizeStart - 1) * (1 - s) * fx;
                    var lift = pl.C * ((Data.depth.omniReach - 1) * (1 - s) * fx);
                    foreach (int i in pl.Idx) Write(positions, i, pl.C + (V(ov, i) - pl.C) * sc + lift);
                }
            }
        }

        /// <summary>
        /// The rigid part of crystal <paramref name="k"/>'s pose at <paramref name="t"/>: an UNMORPHED point v of its basis
        /// is drawn at <c>RotAxis(tumbleAxis, tumble, RotAxis(spinAxis, spin, v)) + centre</c>, exactly as
        /// <see cref="Pose(double, in OmniCombineParams, D3, float[][], float[], float[], float[][])"/> draws it. The
        /// converter carries the Charge crystal's seam-walk light on it while the crystal waits.
        /// </summary>
        public void CrystalFrame(int k, double t, in OmniCombineParams p, out D3 centre, out D3 spinAxis, out double spin,
                                 out D3 tumbleAxis, out double tumble)
        {
            Seed(p.Seed);
            Frame(k, t, p, out centre, out spin, out tumble);
            spinAxis = _tumbleAxis;
            tumbleAxis = _tumbleAxes[k];
        }

        void Frame(int k, double t, in OmniCombineParams p, out D3 centre, out double spin, out double tumble)
        {
            double t0 = Departs(k, p), u = Clamp01((t - t0) / p.Flight), e = EaseIO(u, p.Ease);
            D3 s = StartOf(k, p);
            double left = 1 - e;
            double aim = p.ApproachTurn != null ? Smooth(e / ApproachTurnEnds) : 0;
            double turn = aim > 0 ? (p.ApproachTurn[k] + (p.CameraAzimuth - p.ApproachCameraAzimuth)) * aim : 0;
            centre = RotAxis(D3.Up, p.Swirl * 2 * Math.PI * left + turn, s * left);
            centre = new D3(centre.X, centre.Y + p.Arc * s.Length * Math.Sin(Math.PI * e), centre.Z);
            if (aim > 0 && p.ApproachElevation != null)
            {
                // Tip the path in the vertical plane that faces the camera: about the horizontal axis across its view.
                double az = p.CameraAzimuth;
                // cross(toward the camera (sin az, 0, cos az), up): turning by +angle about it lifts the path.
                var across = new D3(-Math.Cos(az), 0, Math.Sin(az));
                centre = RotAxis(across, (p.CameraElevation + p.ApproachAboveCamera - p.ApproachElevation[k]) * aim, centre);
            }
            tumble = p.Tumble * 2 * Math.PI * left;
            // Charge tumbles while it idles; the accumulated turn unwinds over the flight so it lands in the omni's frame.
            // Only the turn modulo a whole turn unwinds (the same pose), so a long wait never spins it back for laps.
            spin = k == Charge && p.IdleTumble ? (_tumbleSpeed * Math.Min(t, t0)) % (2 * Math.PI) * (1 - e) : 0;
        }

        /// <summary>
        /// Sets <see cref="OmniCombineParams.ApproachTurn"/> so every crystal but the first arrives from the camera's side:
        /// a crystal coming in from one side has to bring the rhombi on its far side through the omni, and arriving from
        /// the camera's side puts them behind it. <paramref name="cameraAzimuth"/> is the camera's azimuth about the stage's
        /// vertical (<c>atan2(x, z)</c> of the direction toward it, unwrapped) at play time <paramref name="now"/>, and it
        /// turns at <paramref name="cameraRadiansPerSecond"/>. From then on each path follows the live camera
        /// (<see cref="OmniCombineParams.CameraAzimuth"/>); the rate only picks the shorter way round for the camera's
        /// expected travel, so a crystal never sweeps the long way to meet it. Space keeps its path.
        /// </summary>
        public void FaceCamera(ref OmniCombineParams p, double cameraAzimuth, double cameraRadiansPerSecond, double now)
        {
            var turns = new double[4];
            var elevations = new double[4];
            for (int k = 1; k < 4; k++)
            {
                // The height it arrives from on its own path (the start's height plus the arc), before any aim.
                var straight = p;
                straight.ApproachTurn = null;
                Frame(k, Departs(k, p) + ArrivalAt * p.Flight, straight, out var at, out _, out _);
                elevations[k] = Math.Asin(Math.Max(-1, Math.Min(1, at.Y / Math.Max(1e-12, at.Length))));
                // The swirl unwinds by the end, so a crystal arrives from its start's own azimuth.
                D3 s = StartOf(k, p);
                double arrives = Departs(k, p) + ArrivalAt * p.Flight;
                double travel = cameraRadiansPerSecond * (arrives - now);
                // Where the camera is expected to be as it arrives, met the shorter way; the live camera adds the travel.
                turns[k] = WrapAngle(cameraAzimuth + travel - Math.Atan2(s.X, s.Z)) - travel;
            }
            p.ApproachTurn = turns;
            p.ApproachElevation = elevations;
            p.ApproachCameraAzimuth = cameraAzimuth;
            p.CameraAzimuth = cameraAzimuth;
        }

        /// <summary>An angle wrapped into (-pi, pi]: the shorter way round.</summary>
        public static double WrapAngle(double a)
        {
            a %= 2 * Math.PI;
            if (a > Math.PI) a -= 2 * Math.PI;
            else if (a <= -Math.PI) a += 2 * Math.PI;
            return a;
        }

        // ------------------------------------------------------------------ the four effects
        // Mass: each prism rides its column by depth (stateless "sealed by depth"; the game's ratchet keeps state).
        double SeatOf(D3 dir, D3 towardCamera)
        {
            double d = D3.Dot(dir, towardCamera);
            double e = Clamp01((d - Data.depth.open) / (Data.depth.seal - Data.depth.open));
            return Math.Pow(e * e * (3 - 2 * e), Data.depth.power);
        }

        // Space: SpaceCrystalAnimator's 5-point spin then 3-point spin, each two half keys over 1/cycleSpeed s, then a pause.
        void SpinWeights(double t, string[] names, double[] into)
        {
            var sp = Data.spin;
            double per = 1 / sp.cycleSpeed + sp.pause;
            double n = Math.Floor(t / per), ph = (t - n * per) * sp.cycleSpeed;
            bool five = ((long)n & 1) == 0;
            double w1 = ph < 1 ? Clamp01(2 * ph) : 0, w2 = ph < 1 ? Clamp01(2 * ph - 1) : 0;
            for (int k = 0; k < into.Length; k++)
                into[k] = names[k].StartsWith("5Point", StringComparison.Ordinal) == five
                    ? (names[k].Contains("1st") ? w1 : w2)
                    : 0;
        }

        // The omni's squares weave (CrystalSpaceSquaresSO.WeightsAt): linear between frames, held across a jump.
        static void SquaresWeights(double t, OmniCombineSquaresData sq, double[] into)
        {
            int nk = sq.keys.Length, frames = sq.weights.Length / nk;
            double fp = ((t * sq.fps) % frames + frames) % frames;
            int i0 = (int)Math.Floor(fp), i1 = (i0 + 1) % frames;
            double a = fp - i0;
            for (int k = 0; k < nk; k++)
                if (Math.Abs(sq.weights[i1 * nk + k] - sq.weights[i0 * nk + k]) > 0.5) a = 0;
            for (int k = 0; k < nk; k++)
                into[k] = sq.weights[i0 * nk + k] + (sq.weights[i1 * nk + k] - sq.weights[i0 * nk + k]) * a;
        }

        // Time: the flip wave as the game plans it (FlipWave.Plan, TimeCrystalFlipWaveProfile.asset).
        struct Flip { public bool Valid; public D3 Axis, Shift; public double Angle, Scale; }

        static double CurveAt(float[] keys, double t, double empty)
        {
            if (keys == null || keys.Length < 2) return empty;
            if (t <= keys[0]) return keys[1];
            for (int i = 2; i + 1 < keys.Length; i += 2)
                if (t <= keys[i])
                {
                    double t0 = keys[i - 2], v0 = keys[i - 1], t1 = keys[i], v1 = keys[i + 1];
                    return v0 + (v1 - v0) * (t - t0) / (t1 - t0);
                }
            return keys[keys.Length - 1];
        }

        /// <summary>A new start vertex every loop, drawn from the other eleven (CrystalFlipWave.LateUpdate), seeded.
        /// Each loop's start follows from the last, so the rig carries the walk forward from the loop it last
        /// answered instead of replaying it from loop 0 (which grew without bound while the converter idles).</summary>
        static int FlipStart(FlipRig rig, long loopIndex, int salt)
        {
            int n = rig.Axes.Length;
            if (rig.StartLoop < 0 || loopIndex < rig.StartLoop)
            {
                rig.StartLoop = 0;
                rig.Start = (int)Math.Floor(Hash(salt, 0) * n);
            }
            for (long k = rig.StartLoop + 1; k <= loopIndex; k++)
                rig.Start = (rig.Start + 1 + (int)Math.Floor(Hash(salt, (int)k) * (n - 1))) % n;
            rig.StartLoop = Math.Max(rig.StartLoop, loopIndex);
            return rig.Start;
        }

        Flip FlipPose(FlipRig rig, int p, Plate pl, double t, int salt, double fx)
        {
            var fl = Data.flip;
            long loop = (long)Math.Floor(t / fl.loop);
            return FlipPoseAt(rig, p, pl, loop, t - loop * fl.loop, salt, fx);
        }

        /// <summary>Plate <paramref name="p"/> in wave <paramref name="loop"/>, <paramref name="local"/> seconds into it.</summary>
        Flip FlipPoseAt(FlipRig rig, int p, Plate pl, long loop, double local, int salt, double fx)
        {
            var fl = Data.flip;
            var step = rig.Plans[FlipStart(rig, loop, salt)][p];
            double tk = local - (fl.first + fl.stagger * step.Ring);
            if (tk < 0) return default;
            var cv = step.Ring == 0 ? fl.lead : fl.later;
            return new Flip
            {
                Valid = true,
                Axis = step.Axis,
                Angle = Math.PI * CurveAt(cv.flip, tk, 0) * fx,
                Scale = 1 + (CurveAt(cv.scale, tk, 1) - 1) * fx,
                Shift = (step.Toward * CurveAt(cv.slide, tk, 0) + pl.Dir * -CurveAt(cv.dip, tk, 0)) * (pl.Reach * fx),
            };
        }

        static D3 ApplyFlip(in Flip f, Plate pl, D3 b) => pl.C + f.Shift + RotAxis(f.Axis, f.Angle, b - pl.C) * f.Scale;

        // ------------------------------------------------------------------ layout and seeding
        D3 StartOf(int k, in OmniCombineParams p)
        {
            double d = p.Spread * OmniRadius;
            D3 s;
            if (p.Layout == 0) s = V(_crystals[k].Data.offset, 0) * (p.Spread / 3);
            else if (p.Layout == 1)
            {
                double a = k * Math.PI / 2 + Math.PI / 4;
                s = new D3(Math.Cos(a) * d, 0, Math.Sin(a) * d);
            }
            else
            {
                double kk = d / Math.Sqrt(3);
                s = k switch
                {
                    0 => new D3(kk, kk, kk),
                    1 => new D3(kk, -kk, -kk),
                    2 => new D3(-kk, kk, -kk),
                    _ => new D3(-kk, -kk, kk),
                };
            }
            return p.LayoutYaw != 0 ? RotAxis(D3.Up, p.LayoutYaw, s) : s;
        }

        void Seed(int seed)
        {
            if (seed == _seededWith) return;
            _seededWith = seed;
            var rnd = new Mulberry32(seed);
            foreach (int k in RngOrder)
            {
                var c = _crystals[k];
                double z = 2 * rnd.Next() - 1, a = 2 * Math.PI * rnd.Next(), s = Math.Sqrt(1 - z * z);
                _tumbleAxes[k] = new D3(s * Math.Cos(a), z, s * Math.Sin(a));
                var siteR = new Dictionary<int, double>();
                for (int q = 0; q < c.Plates.Length; q++)
                {
                    int key = k == Charge || k == Mass ? c.Data.plateSite[q] : q;
                    if (!siteR.TryGetValue(key, out double r)) siteR[key] = r = rnd.Next();
                    c.PlateR[q] = r;
                }
            }
        }

        // ------------------------------------------------------------------ models
        sealed class CrystalModel
        {
            public readonly OmniCombineCrystalData Data;
            public readonly int N;
            public readonly Plate[] Plates;
            public readonly D3[] FinalCentre, Axis;
            public readonly double[] Twist, PlateR;
            public readonly double[][] Quat;

            public CrystalModel(int element, OmniCombineCrystalData d)
            {
                Data = d;
                N = d.basis.Length / 3;
                if (d.final == null || d.final.Length != d.basis.Length || d.plate.Length != N)
                    throw new ArgumentException($"{d.element}: basis, final and plate disagree on the vertex count");
                Plates = Plate.Of(d.basis, d.plate);
                PlateR = new double[Plates.Length];
                FinalCentre = new D3[Plates.Length];
                for (int q = 0; q < Plates.Length; q++)
                {
                    D3 sum = default;
                    foreach (int i in Plates[q].Idx) sum += V(d.final, i);
                    FinalCentre[q] = sum * (1.0 / Plates[q].Idx.Length);
                }
                if (element == Charge || element == Mass)
                {
                    if (d.stage1 == null || d.stage1.Length != d.basis.Length || d.plateAxis == null || d.plateAxis.Length != 3 * Plates.Length)
                        throw new ArgumentException($"{d.element}: the wedge morph needs stage1, plateAxis and plateTwist");
                    Axis = new D3[Plates.Length];
                    Twist = new double[Plates.Length];
                    for (int q = 0; q < Plates.Length; q++)
                    {
                        Axis[q] = V(d.plateAxis, q);
                        Twist[q] = d.plateTwist[q] * Math.PI / 180;
                    }
                }
                if (element == Time)
                {
                    if (d.plateQuat == null || d.plateQuat.Length != 4 * Plates.Length)
                        throw new ArgumentException("Time: the rigid lock-in needs a quaternion per rhombus");
                    Quat = new double[Plates.Length][];
                    for (int q = 0; q < Plates.Length; q++)
                        Quat[q] = new double[] { d.plateQuat[4 * q], d.plateQuat[4 * q + 1], d.plateQuat[4 * q + 2], d.plateQuat[4 * q + 3] };
                }
            }
        }

        /// <summary>A connected component: its centre, outward direction, long diagonal and half its length.</summary>
        sealed class Plate
        {
            public int[] Idx;
            public D3 C, Dir, Diag;
            public double Reach;

            public static Plate[] Of(float[] points, int[] plateIds)
            {
                int np = 0;
                foreach (int p in plateIds) np = Math.Max(np, p + 1);
                var lists = new List<int>[np];
                for (int p = 0; p < np; p++) lists[p] = new List<int>();
                for (int i = 0; i < plateIds.Length; i++) lists[plateIds[i]].Add(i);
                var plates = new Plate[np];
                for (int p = 0; p < np; p++)
                {
                    var idx = lists[p].ToArray();
                    D3 c = default;
                    var rad = new double[idx.Length];
                    double lo = double.MaxValue, hi = double.MinValue;
                    for (int k = 0; k < idx.Length; k++)
                    {
                        var v = V(points, idx[k]);
                        c += v;
                        rad[k] = v.Length;
                        lo = Math.Min(lo, rad[k]);
                        hi = Math.Max(hi, rad[k]);
                    }
                    c *= 1.0 / idx.Length;
                    double mid = (hi + lo) / 2;
                    var outer = new List<int>();
                    for (int k = 0; k < idx.Length; k++) if (rad[k] > mid) outer.Add(idx[k]);
                    // the outline of the outer face, ordered round its centre (the lab's order, so its diagonal
                    // comes out with the same sign and the flip rig lists its start vertices in the same order)
                    D3 oc = default;
                    foreach (int i in outer) oc += V(points, i);
                    oc *= 1.0 / outer.Count;
                    var n = c.Unit;
                    var r1 = (V(points, outer[0]) - oc).Unit;
                    var r2 = D3.Cross(n, r1);
                    var keys = new double[outer.Count];
                    var sorted = outer.ToArray();
                    for (int k = 0; k < sorted.Length; k++)
                    {
                        var dv = V(points, sorted[k]) - oc;
                        keys[k] = Math.Atan2(D3.Dot(dv, r2), D3.Dot(dv, r1));
                    }
                    Array.Sort(keys, sorted);
                    double best = 0;
                    var ax = new D3(1, 0, 0);
                    foreach (int a in sorted)
                        foreach (int b in sorted)
                        {
                            var dv = V(points, a) - V(points, b);
                            double l = dv.Length;
                            if (l > best) { best = l; ax = dv * (1 / l); }
                        }
                    plates[p] = new Plate { Idx = idx, C = c, Dir = n, Diag = ax, Reach = best / 2 };
                }
                return plates;
            }
        }

        readonly struct FlipStep
        {
            public readonly int Ring;
            public readonly D3 Axis, Toward;
            public FlipStep(int ring, D3 axis, D3 toward) { Ring = ring; Axis = axis; Toward = toward; }
        }

        /// <summary>
        /// The flip wave's possible start vertices and, per start, each rhombus's ring and turn. A rhombus's centre
        /// is a two-fold axis; the five-fold vertices at the ends of its long diagonal lie atan(1/phi) = 31.72 deg
        /// away along it (rhombic triacontahedron); the 60 candidates cluster to 12. Rings: heights within 0.05.
        /// </summary>
        sealed class FlipRig
        {
            public readonly D3[] Axes;
            public readonly Dictionary<int, FlipStep>[] Plans;
            /// <summary>The flip walk's last answered loop and its start vertex (<see cref="FlipStart"/>).</summary>
            public long StartLoop = -1;
            public int Start;

            public FlipRig(Plate[] plates, int[] ids)
            {
                double a5 = Math.Atan(1 / ((1 + Math.Sqrt(5)) / 2)), near = Math.Cos(4 * Math.PI / 180);
                var axes = new List<D3>();
                foreach (int p in ids)
                    foreach (int sgn in new[] { 1, -1 })
                    {
                        var u = (plates[p].Dir * Math.Cos(a5) + plates[p].Diag * (sgn * Math.Sin(a5))).Unit;
                        if (!axes.Exists(x => D3.Dot(x, u) > near)) axes.Add(u);
                    }
                Axes = axes.ToArray();
                Plans = new Dictionary<int, FlipStep>[Axes.Length];
                for (int s = 0; s < Axes.Length; s++)
                {
                    var start = Axes[s];
                    var hs = new double[ids.Length];
                    for (int k = 0; k < ids.Length; k++) hs[k] = D3.Dot(plates[ids[k]].Dir, start);
                    var sortedH = (double[])hs.Clone();
                    Array.Sort(sortedH);
                    Array.Reverse(sortedH);
                    var levels = new List<double>();
                    foreach (double h in sortedH) if (levels.Count == 0 || levels[levels.Count - 1] - h > 0.05) levels.Add(h);
                    var plan = new Dictionary<int, FlipStep>();
                    for (int k = 0; k < ids.Length; k++)
                    {
                        var pl = plates[ids[k]];
                        double h = hs[k];
                        int ring = 0;
                        for (int i = 0; i < levels.Count; i++) if (Math.Abs(levels[i] - h) < Math.Abs(levels[ring] - h)) ring = i;
                        var toward = (start - pl.Dir * h).Unit;
                        var lng = pl.Diag;
                        var sht = D3.Cross(pl.Dir, lng).Unit;
                        var axis = Math.Abs(D3.Dot(lng, toward)) < Math.Abs(D3.Dot(sht, toward)) ? lng : sht;
                        if (D3.Dot(D3.Cross(axis, pl.Dir), toward) > 0) axis = axis * -1;
                        plan[ids[k]] = new FlipStep(ring, axis, toward);
                    }
                    Plans[s] = plan;
                }
            }
        }

        static int[] AllPlates(int n)
        {
            var ids = new int[n];
            for (int i = 0; i < n; i++) ids[i] = i;
            return ids;
        }

        // ------------------------------------------------------------------ maths (the lab's, in doubles)
        static D3 V(float[] a, int i) => new D3(a[3 * i], a[3 * i + 1], a[3 * i + 2]);

        static void Write(float[] a, int i, D3 v)
        {
            a[3 * i] = (float)v.X;
            a[3 * i + 1] = (float)v.Y;
            a[3 * i + 2] = (float)v.Z;
        }

        public static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;

        public static double Smooth(double x)
        {
            x = Clamp01(x);
            return x * x * (3 - 2 * x);
        }

        static double EaseIO(double u, double k)
        {
            u = Clamp01(u);
            return u < 0.5 ? 0.5 * Math.Pow(2 * u, k) : 1 - 0.5 * Math.Pow(2 * (1 - u), k);
        }

        /// <summary>Rodrigues: <paramref name="v"/> turned <paramref name="ang"/> radians about the unit <paramref name="axis"/>.</summary>
        static D3 RotAxis(D3 axis, double ang, D3 v)
        {
            double c = Math.Cos(ang), s = Math.Sin(ang), d = D3.Dot(axis, v);
            return v * c + D3.Cross(axis, v) * s + axis * (d * (1 - c));
        }

        static D3 QuatRotate(double[] q, D3 v)
        {
            var u = new D3(q[1], q[2], q[3]);
            var t = D3.Cross(u, v) * 2;
            return v + t * q[0] + D3.Cross(u, t);
        }

        /// <summary>The fraction <paramref name="t"/> of the turn <paramref name="q"/>, from no turn, written into
        /// <paramref name="into"/> (reused per vertex: Pose runs every frame).</summary>
        static double[] SlerpIdentity(double[] q, double t, double[] into)
        {
            double w = Math.Max(-1, Math.Min(1, q[0])), ang = 2 * Math.Acos(Math.Abs(w)), s = Math.Sin(ang / 2), sgn = w < 0 ? -1 : 1;
            if (s < 1e-6)
            {
                into[0] = 1;
                into[1] = into[2] = into[3] = 0;
                return into;
            }
            double a = ang * t / 2, k = Math.Sin(a) / s;
            into[0] = Math.Cos(a);
            into[1] = q[1] * sgn * k;
            into[2] = q[2] * sgn * k;
            into[3] = q[3] * sgn * k;
            return into;
        }

        /// <summary>The lab's integer hash, bit for bit (Math.imul / &gt;&gt;&gt; semantics).</summary>
        public static double Hash(int a, int b)
        {
            unchecked
            {
                int h = a * 374761393 ^ b * 668265263;
                h = (h ^ (int)((uint)h >> 13)) * 1274126177;
                return (uint)(h ^ (int)((uint)h >> 16)) / 4294967296.0;
            }
        }

        /// <summary>The lab's mulberry32, bit for bit.</summary>
        public sealed class Mulberry32
        {
            int _a;
            public Mulberry32(int seed) { _a = seed; }

            public double Next()
            {
                unchecked
                {
                    _a += 0x6D2B79F5;
                    int t = (_a ^ (int)((uint)_a >> 15)) * (1 | _a);
                    t = (t + (t ^ (int)((uint)t >> 7)) * (61 | t)) ^ t;
                    return (uint)(t ^ (int)((uint)t >> 14)) / 4294967296.0;
                }
            }
        }
    }

    /// <summary>A double-precision 3-vector for <see cref="OmniCombineCore"/> (the lab computes in doubles).</summary>
    public readonly struct D3
    {
        public readonly double X, Y, Z;
        public D3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static readonly D3 Up = new D3(0, 1, 0);
        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);
        public D3 Unit { get { double l = Length; return l > 0 ? this * (1 / l) : this; } }
        public static D3 operator +(D3 a, D3 b) => new D3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static D3 operator -(D3 a, D3 b) => new D3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static D3 operator *(D3 a, double s) => new D3(a.X * s, a.Y * s, a.Z * s);
        public static double Dot(D3 a, D3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        public static D3 Cross(D3 a, D3 b) => new D3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        public static D3 Lerp(D3 a, D3 b, double t) => a + (b - a) * t;
    }
}
