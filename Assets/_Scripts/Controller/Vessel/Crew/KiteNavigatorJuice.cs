using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>The board, the bodies and the pilot's camera this frame, as the juice needs them (no engine objects).</summary>
    internal struct KiteJuiceFrame
    {
        public float Now, ViewDepth;
        public Vector3 Origin, Up, Right, Forward;   // the board: a point (x, z) on it is Origin + Right x + Forward z
        public Vector3 Kite;                         // the Wrasse, in the world
        public bool HasHull;
        public Vector3 HullPosition;                 // the Grouper (the Time juice lands on it)
        public Quaternion HullRotation;
        public bool HasPilot;
        public Vector3 PilotPosition, PilotUp;       // the pilot's camera (its view's juice faces it)

        public Vector3 BoardToWorld(float x, float z) => Origin + Right * x + Forward * z;
        public Vector3 OnHull(Vector3 local) => HullPosition + HullRotation * local;
    }

    /// <summary>The numbers the juice reads (KiteNavigatorConfigSO and the flower's layout).</summary>
    internal struct KiteJuiceSettings
    {
        public float MoteSize, SpaceSpinDegreesPerSecond, KiteRadius, MoteSpread;
        public float FizzleSeconds, SpaceRideSeconds, MassMorphSeconds, TimeFlightSeconds, ChargeFlightSeconds;
        public Vector3 MassPrismScale;
        public int PetalCount;
        public float PetalSpacing;
        public Color[] Elements;                     // each element's mote colour (Charge, Mass, Space, Time)
    }

    /// <summary>
    /// The kite's juice (round 10; Garrett, 2026-10-21: "lets loose as many of the flat ring effects as we can and juice up
    /// each effect. when more than one is lassoed it should fizzle with no ring. space symbols should explode with
    /// explosion. mass symbols should look like they are morphing into the 3d mass. time should morph on to the grouper.
    /// charge should morph onto the wrasse. ... bespoke, unique, dynamic, and polished.").
    ///
    /// <para><b>The motes ARE the effect.</b> Every <see cref="KiteBurst"/> a loop raises carries the motes it
    /// lassoed, and each mote's five petals are spawned exactly where its flower drew them, then choreographed:</para>
    /// <list type="bullet">
    /// <item><b>Fizzle</b> (more than one element): the petals sputter in place, drain to grey, wilt and droop down the
    /// view, flickering out. No ring, no outline.</item>
    /// <item><b>Space</b>: the petals spiral into the loop's centre through the blast's fuse, flash white-hot as the real
    /// AOEExplosion goes off, and ride its shock front out, streaking and cooling back to lime as they fade; white shards
    /// lead the front and a five-petal star flares at the core.</item>
    /// <item><b>Mass</b>: each petal folds into its mote and becomes a lit block that rises out of the plane, grows and turns
    /// into the very place and attitude of one of the cluster's prisms (the seat lays them as the blocks arrive).</item>
    /// <item><b>Time</b>: the petals spiral onto the Grouper as accelerating streaks and land on its hull as glinting chips
    /// that flash and fade; the saved boosts show when they land.</item>
    /// <item><b>Charge</b>: the petals home onto the Wrasse, swing into orbit at the charge pips' radius and warm into the
    /// pips' gold; the pips show when they arrive.</item>
    /// <item>A popped prism bursts into white shards; a steal streams gold shards from the rival into the Wrasse; an Omni
    /// drop blooms each cluster open.</item>
    /// </list>
    ///
    /// <para><b>How it draws.</b> One dynamic, vertex-coloured mesh per camera per frame (each bit has its own colour, alpha
    /// and flash, which the instanced materials cannot), built from the petal outlines the motes draw with. The kite view
    /// lays petals flat on the board, just under its lens; the pilot view turns them to face its camera, so the Time streaks
    /// and the Mass blocks read in 3D from the chase camera too. Blocks are shaded per face. Every list is reused: no
    /// allocation per frame. It reads a <see cref="KiteJuiceFrame"/> snapshot and <see cref="KiteJuiceSettings"/>, never the
    /// seat itself, so <c>Tools/Build/kite_navigator_lab/juice_sheet.sh</c> compiles this very file outside Unity and renders
    /// every effect frame by frame.</para>
    /// </summary>
    sealed class KiteNavigatorJuice
    {
        enum Act : byte { Sputter, Implode, Shock, Flash, Fold, Morph, Stream, Home, Shard, Bloom }

        struct Bit
        {
            public Act Act;
            public int Shape;          // 0..3 the element's petal, 4 a shard
            public float T0, Delay, Life;
            public Vector3 P0, P1, Ctrl, Dir, Local;
            public Quaternion R0, R1;
            public Vector3 Scale1;
            public float Angle, Spin, Size, Seed, Speed, Side;
            public bool WithPetal, Orbit;
            public Color Tint;
        }

        const int MaxBits = 1400, MaxBoxes = 64, ShardShape = 4;
        // AOEExplosion's fuse and run (KiteNavigator.BlastDelay / BlastDuration): the Space juice rides the real blast
        const float BlastDelay = 0.2f, BlastDuration = 3f;
        const float Fold = 0.22f, Settle = 0.28f, Land = 0.4f, OrbitTime = 0.45f, Kick = 12f;
        static readonly Color White = new(1f, 1f, 1f, 1f);
        static readonly Color Ash = new(0.55f, 0.57f, 0.6f, 1f);
        static readonly Color Gold = new(1f, 0.82f, 0.32f, 1f);
        static readonly Vector3[] ShardOutline = { new(0f, 0f, 1.3f), new(0.75f, 0f, 0.25f), new(0f, 0f, -1f), new(-0.75f, 0f, 0.25f) };

        readonly Vector3[][] _shapes = new Vector3[5][];
        readonly float[] _petalOut, _petalScale;
        readonly List<Bit> _bits = new();
        readonly List<(float polar, Vector3 p, float deg, float size)> _spokes = new();
        readonly System.Random _rng = new(20261021);
        readonly bool _linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

        // the meshes being built this frame: [0] the kite view, [1] the pilot view
        readonly List<Vector3>[] _v = { new(), new() };
        readonly List<Color>[] _c = { new(), new() };
        readonly List<int>[] _t = { new(), new() };
        KiteJuiceFrame _f;
        KiteJuiceSettings _s;
        Vector3 _up, _right, _fwd;
        float _petalLift, _boxLift;

        // what the board shows of the charge and the saved boosts: held back until the petals carrying them arrive
        float _chargeRevealAt, _shownCharge, _boostsRevealAt;
        int _shownBoosts;

        internal KiteNavigatorJuice(Vector3[][] petalOutlines, float[] petalCentreOut, float[] petalScale)
        {
            for (int e = 0; e < 4; e++) _shapes[e] = petalOutlines[e];
            _shapes[ShardShape] = ShardOutline;
            _petalOut = petalCentreOut;
            _petalScale = petalScale;
        }

        internal int Count => _bits.Count;

        // ------------------------------------------------------------------ what the board shows meanwhile

        internal float ShownCharge(float actual, float now)
        {
            _shownCharge = now >= _chargeRevealAt ? actual : Mathf.Min(_shownCharge, actual);
            return _shownCharge;
        }

        internal int ShownBoosts(int actual, float now)
        {
            _shownBoosts = now >= _boostsRevealAt ? actual : Mathf.Min(_shownBoosts, actual);
            return _shownBoosts;
        }

        // ------------------------------------------------------------------ spawning

        /// <summary>Turn every burst the seat raised this frame into its juice, and hand the bursts back.</summary>
        internal void Drain(List<KiteBurst> bursts, System.Action<KiteBurst> recycle, in KiteJuiceFrame frame, in KiteJuiceSettings settings)
        {
            _f = frame; _s = settings;
            _up = frame.Up; _right = frame.Right; _fwd = frame.Forward;
            for (int i = 0; i < bursts.Count; i++)
            {
                Spawn(bursts[i], frame.Now);
                recycle(bursts[i]);
            }
            bursts.Clear();
        }

        float Rand(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

        void Push(in Bit b)
        {
            if (_bits.Count < MaxBits) _bits.Add(b);
        }

        // where a mote's petal i is drawn this moment (KiteNavigatorView.Flower's layout), and its size
        void PetalAt(in KiteBurstMote m, int i, float now, out Vector3 pos, out float deg, out float size)
        {
            float r = _s.MoteSize;
            deg = m.Spin + i * _s.PetalSpacing + (m.Element == 2 ? now * _s.SpaceSpinDegreesPerSecond : 0f);
            float a = deg * Mathf.Deg2Rad;
            var dir = _right * Mathf.Sin(a) + _fwd * Mathf.Cos(a);
            pos = _f.BoardToWorld(m.X, m.Z) + dir * (_petalOut[m.Element] * r);
            size = _petalScale[m.Element] * r;
        }

        void Spawn(KiteBurst b, float now)
        {
            var cfg = _s;
            Vector3 up = _up, right = _right, fwd = _fwd;
            int petals = _s.PetalCount;
            switch (b.Kind)
            {
                case KiteBurstKind.Fizzle:
                    foreach (var m in b.Motes)
                        for (int i = 0; i < petals; i++)
                        {
                            PetalAt(m, i, now, out var p, out float deg, out float size);
                            Push(new Bit
                            {
                                Act = Act.Sputter, Shape = m.Element, T0 = now, Delay = Rand(0f, 0.08f), Life = cfg.FizzleSeconds,
                                P0 = p, Angle = deg, Size = size, Seed = Rand(0f, 100f), Spin = Rand(-240f, 240f),
                                Dir = right * Rand(-9f, 9f),
                            });
                        }
                    break;

                case KiteBurstKind.Space:
                {
                    var centre = b.World;
                    // every petal leaves on its own spoke of one even, radial blast: sorted by where it sat round the centre,
                    // so each still flies roughly away from where it was, but the motes' clumps spread into a full sphere
                    _spokes.Clear();
                    foreach (var m in b.Motes)
                        for (int i = 0; i < petals; i++)
                        {
                            PetalAt(m, i, now, out var p, out float deg, out float size);
                            var from = Vector3.ProjectOnPlane(p - centre, up);
                            float polar = from.sqrMagnitude > 1e-4f ? Mathf.Atan2(Vector3.Dot(from, right), Vector3.Dot(from, fwd)) : Rand(-3f, 3f);
                            _spokes.Add((polar, p, deg, size));
                        }
                    _spokes.Sort((x, y) => x.polar.CompareTo(y.polar));
                    float first = _spokes.Count > 0 ? _spokes[0].polar * Mathf.Rad2Deg : 0f;
                    for (int k = 0; k < _spokes.Count; k++)
                    {
                        var (_, p, deg, size) = _spokes[k];
                        float spoke = first + k * 360f / _spokes.Count + Rand(-9f, 9f);
                        Push(new Bit
                        {
                            Act = Act.Implode, Shape = 2, T0 = now, Life = BlastDelay + cfg.SpaceRideSeconds,
                            P0 = p, P1 = centre, Dir = Quaternion.AngleAxis(spoke, up) * fwd,
                            Angle = deg, Size = size, Spin = Rand(420f, 760f) * (Rand(0f, 1f) < 0.5f ? -1f : 1f),
                            Speed = Rand(0.62f, 1.05f), Side = Rand(0f, 1f) < 0.5f ? -1f : 1f, Seed = b.MaxScale,
                        });
                    }
                    // white shards lead the front, and a star flares at the core, the moment the blast fires
                    for (int i = 0; i < 18; i++)
                        Push(new Bit
                        {
                            Act = Act.Shock, Shape = ShardShape, T0 = now, Delay = BlastDelay, Life = 0.55f,
                            P1 = centre, Dir = Quaternion.AngleAxis(i * 20f + Rand(-8f, 8f), up) * fwd,
                            Size = Rand(1.2f, 2.2f), Speed = Rand(1.08f, 1.35f), Seed = b.MaxScale,
                        });
                    for (int i = 0; i < petals; i++)
                        Push(new Bit
                        {
                            Act = Act.Flash, Shape = 2, T0 = now, Delay = BlastDelay, Life = 0.18f, P1 = centre,
                            Angle = i * _s.PetalSpacing + Rand(0f, 72f), Size = cfg.MoteSize,
                        });
                    break;
                }

                case KiteBurstKind.Mass:
                {
                    int count = b.Motes.Count * petals, boxes = Mathf.Min(MaxBoxes, b.Targets.Count);
                    for (int j = 0; j < Mathf.Max(count, boxes); j++)
                    {
                        var m = b.Motes[(j % count) / petals];
                        PetalAt(m, j % count % petals, now, out var p, out float deg, out float size);
                        var home = _f.BoardToWorld(m.X, m.Z);
                        var tumble = Quaternion.Euler(Rand(0f, 360f), Rand(0f, 360f), Rand(0f, 360f));
                        if (j < boxes)
                        {
                            var (pos, rot) = b.Targets[j];
                            Push(new Bit
                            {
                                Act = Act.Morph, Shape = 1, T0 = now, Delay = Rand(0f, 0.06f), Life = cfg.MassMorphSeconds + Settle,
                                P0 = p, Ctrl = home, P1 = pos, R0 = tumble, R1 = rot, Scale1 = cfg.MassPrismScale,
                                Angle = deg, Size = size, WithPetal = j < count, Spin = Rand(-200f, 200f),
                                Speed = Rand(0.9f, 1.15f),
                            });
                        }
                        else
                            Push(new Bit { Act = Act.Fold, Shape = 1, T0 = now, Life = Fold + 0.12f, P0 = p, Ctrl = home, Angle = deg, Size = size });
                    }
                    break;
                }

                case KiteBurstKind.Time:
                {
                    float last = now;
                    int k = 0;
                    foreach (var m in b.Motes)
                        for (int i = 0; i < petals; i++, k++)
                        {
                            PetalAt(m, i, now, out var p, out float deg, out float size);
                            float delay = k * 0.035f + Rand(0f, 0.1f), flight = cfg.TimeFlightSeconds * Rand(0.85f, 1.15f);
                            Push(new Bit
                            {
                                Act = Act.Stream, Shape = 3, T0 = now, Delay = delay, Life = flight + Land, Speed = flight,
                                P0 = p, Local = new Vector3(Rand(-1.7f, 1.7f), Rand(-0.6f, 1.6f), Rand(-4.6f, 4.6f)),
                                Angle = deg, Size = size, Side = Rand(0f, 1f) < 0.5f ? -1f : 1f, Spin = Rand(-180f, 180f),
                            });
                            last = Mathf.Max(last, now + delay + flight);
                        }
                    _boostsRevealAt = Mathf.Max(_boostsRevealAt, last);
                    break;
                }

                case KiteBurstKind.Charge:
                {
                    float last = now;
                    int k = 0;
                    var kite = KiteWorld();
                    foreach (var m in b.Motes)
                        for (int i = 0; i < petals; i++, k++)
                        {
                            PetalAt(m, i, now, out var p, out float deg, out float size);
                            float delay = k * 0.03f + Rand(0f, 0.08f), flight = cfg.ChargeFlightSeconds * Rand(0.85f, 1.15f);
                            var chord = Vector3.ProjectOnPlane(kite - p, up);
                            var side = Vector3.Cross(up, chord.sqrMagnitude > 1e-4f ? chord.normalized : fwd) * (k % 2 == 0 ? 1f : -1f);
                            Push(new Bit
                            {
                                Act = Act.Home, Shape = 0, T0 = now, Delay = delay, Life = flight + OrbitTime, Speed = flight,
                                P0 = p, Ctrl = p + side * (chord.magnitude * Rand(0.35f, 0.7f)) - chord * 0.15f, Orbit = true,
                                Angle = Rand(0f, 360f), Size = size, Spin = Rand(420f, 620f), Tint = Element(0),
                            });
                            last = Mathf.Max(last, now + delay + flight + 0.15f);
                        }
                    _chargeRevealAt = Mathf.Max(_chargeRevealAt, last);
                    break;
                }

                case KiteBurstKind.Steal:
                {
                    var kite = KiteWorld();
                    for (int i = 0; i < 9; i++)
                    {
                        var chord = Vector3.ProjectOnPlane(kite - b.World, up);
                        var side = Vector3.Cross(up, chord.sqrMagnitude > 1e-4f ? chord.normalized : fwd);
                        Push(new Bit
                        {
                            Act = Act.Home, Shape = ShardShape, T0 = now, Delay = i * 0.025f, Life = 0.5f + 0.15f, Speed = Rand(0.4f, 0.55f),
                            P0 = b.World, Ctrl = b.World + side * (chord.magnitude * Rand(-0.6f, 0.6f)) + up * Rand(-4f, 4f),
                            Angle = Rand(0f, 360f), Size = Rand(1.4f, 2.2f), Tint = Gold,
                        });
                    }
                    break;
                }

                case KiteBurstKind.Pop:
                    for (int i = 0; i < 6; i++)
                        Push(new Bit
                        {
                            Act = Act.Shard, Shape = ShardShape, T0 = now, Life = Rand(0.32f, 0.5f), P0 = b.World,
                            Dir = (Quaternion.AngleAxis(i * 60f + Rand(-18f, 18f), up) * fwd + up * Rand(-0.3f, 0.3f)).normalized,
                            Speed = Rand(45f, 85f), Size = Rand(0.9f, 1.6f), Spin = Rand(-720f, 720f), Angle = Rand(0f, 360f),
                            Tint = White,
                        });
                    break;

                case KiteBurstKind.Bloom:
                    for (int i = 0; i < 10; i++)
                        Push(new Bit
                        {
                            Act = Act.Bloom, Shape = b.Element, T0 = now, Delay = Rand(0f, 0.08f), Life = Rand(0.5f, 0.75f), P0 = b.World,
                            Dir = Quaternion.AngleAxis(i * 36f + Rand(-28f, 28f), up) * fwd, Speed = cfg.MoteSpread * Rand(0.3f, 1.25f),
                            Size = _petalScale[b.Element] * cfg.MoteSize * 1.3f, Spin = Rand(-300f, 300f), Angle = i * 36f,
                        });
                    break;
            }
        }

        Vector3 KiteWorld() => _f.Kite;

        // ------------------------------------------------------------------ the frame

        // what the view's meshes are built from: [0] the kite view, [1] the pilot view
        internal List<Vector3> Vertices(int view) => _v[view];
        internal List<Color> Colors(int view) => _c[view];
        internal List<int> Triangles(int view) => _t[view];

        /// <summary>Advance every bit and build this frame's two vertex lists.</summary>
        internal void Build(in KiteJuiceFrame frame, in KiteJuiceSettings settings)
        {
            _f = frame; _s = settings;
            float now = frame.Now;
            for (int k = 0; k < 2; k++) { _v[k].Clear(); _c[k].Clear(); _t[k].Clear(); }
            _up = frame.Up; _right = frame.Right; _fwd = frame.Forward;
            _petalLift = frame.ViewDepth - 3f;
            _boxLift = frame.ViewDepth - 14f;   // a block is up to 5 u long: keep it clear of the lens

            for (int i = _bits.Count - 1; i >= 0; i--)
            {
                var b = _bits[i];
                float t = now - b.T0 - b.Delay;
                if (t > b.Life || !Draw(b, Mathf.Max(0f, t), t < 0f))
                {
                    _bits[i] = _bits[_bits.Count - 1];
                    _bits.RemoveAt(_bits.Count - 1);
                }
            }
        }

        Color Element(int e) => _s.Elements != null && e < _s.Elements.Length ? _s.Elements[e] : White;

        static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));
        static float EaseOutBack(float x) { const float c = 1.9f; x -= 1f; return 1f + (c + 1f) * x * x * x + c * x * x; }
        static Color WithAlpha(Color c, float a) { c.a *= Mathf.Clamp01(a); return c; }

        // one bit's pose this moment; false once it has nothing left to show
        bool Draw(in Bit b, float t, bool waiting)
        {
            switch (b.Act)
            {
                case Act.Sputter: return Sputter(b, t);
                case Act.Implode: return Implode(b, t);
                case Act.Shock:
                {
                    if (waiting) return true;
                    float r = KiteNavigatorCore.BlastRadius(b.Seed, t + BlastDelay, BlastDelay, BlastDuration) * b.Speed
                              + Kick * 1.4f * (1f - Mathf.Exp(-7f * t));
                    float u = t / b.Life;
                    var c = Color.Lerp(White, Element(2), Smooth(0.15f, 0.8f, u));
                    Petal(ShardShape, b.P1 + b.Dir * r, b.Dir, 0f, b.Size * 0.6f, b.Size * (1.8f + 2.4f * (1f - u)), WithAlpha(c, 1f - Smooth(0.4f, 1f, u)));
                    return true;
                }
                case Act.Flash:
                {
                    if (waiting) return true;
                    float u = t / b.Life, a = (b.Angle + 160f * u) * Mathf.Deg2Rad;
                    var dir = _right * Mathf.Sin(a) + _fwd * Mathf.Cos(a);
                    float s = b.Size * Mathf.Lerp(1f, 2.6f, Mathf.Sqrt(u));
                    Petal(2, b.P1 + dir * s * 0.9f, dir, 0f, s * 0.55f, s, WithAlpha(Color.Lerp(White, Element(2), u), 1f - u * u * u));
                    return true;
                }
                case Act.Fold:
                {
                    float u = Mathf.Clamp01(t / Fold);
                    Petal(b.Shape, Vector3.Lerp(b.P0, b.Ctrl, Mathf.SmoothStep(0f, 1f, u)), Vector3.zero, b.Angle + 90f * u,
                          b.Size * (1f - 0.8f * u), b.Size * (1f - 0.8f * u), WithAlpha(Element(1), 1f - Smooth(0.6f, 1.4f, t / Fold)));
                    return true;
                }
                case Act.Morph: return Morph(b, t);
                case Act.Stream: return Stream(b, t, waiting);
                case Act.Home: return Home(b, t, waiting);
                case Act.Shard:
                {
                    const float drag = 6f;
                    float u = t / b.Life, travel = b.Speed * (1f - Mathf.Exp(-drag * t)) / drag;
                    Petal(ShardShape, b.P0 + b.Dir * travel, b.Dir, 0f, b.Size * 0.55f, b.Size * (1f + 2.2f * Mathf.Exp(-drag * t)),
                          WithAlpha(Color.Lerp(b.Tint, Element(0), u * 0.5f), 1f - u * u));
                    return true;
                }
                case Act.Bloom:
                {
                    float u = t / b.Life, open = EaseOutBack(Mathf.Clamp01(t / 0.32f));
                    var c = Color.Lerp(White, Element(b.Shape), Smooth(0f, 0.25f, u));
                    Petal(b.Shape, b.P0 + b.Dir * (b.Speed * open), b.Dir, 0f, b.Size * (1f - 0.5f * u), b.Size * (1f - 0.5f * u),
                          WithAlpha(c, 1f - Smooth(0.45f, 1f, u)));
                    return true;
                }
            }
            return false;
        }

        // FIZZLE: sputter in place, drain to grey, wilt, droop down the view, flicker out
        bool Sputter(in Bit b, float t)
        {
            float u = t / b.Life, s = b.Seed;
            float shake = 0.8f * (1f - Smooth(0.15f, 0.35f, t));
            var jitter = (_right * Mathf.Sin(73f * t + s) + _fwd * Mathf.Cos(61f * t + 1.7f * s)) * shake;
            float fall = Mathf.Max(0f, t - 0.22f);
            var pos = b.P0 + jitter - _fwd * (38f * fall * fall) + b.Dir * fall;
            var c = Color.Lerp(Element(b.Shape), Ash, Smooth(0f, 0.35f, t));
            float flicker = t < 0.22f ? 1f : 0.62f + 0.38f * Mathf.Sin(52f * t + 6f * s);
            float wilt = 1f - 0.45f * Smooth(0.2f, 0.7f, u);
            float size = b.Size * (1f - 0.55f * Smooth(0.3f, 1f, u));
            Petal(b.Shape, pos, Vector3.zero, b.Angle + b.Spin * fall, size * wilt, size, WithAlpha(c, (1f - Smooth(0.5f, 1f, u)) * flicker));
            return true;
        }

        // SPACE: spiral into the centre through the fuse, flash white as the blast fires, ride its front out, cool and fade
        bool Implode(in Bit b, float t)
        {
            float fuse = BlastDelay;
            if (t < fuse)
            {
                float e = Mathf.Pow(t / fuse, 3f);
                var offset = Quaternion.AngleAxis(150f * e * b.Side, _up) * (b.P0 - b.P1) * (1f - e);
                var c = Color.Lerp(Element(2), White, 0.6f * e);
                float s = b.Size * (1f - 0.35f * e);
                Petal(2, b.P1 + offset, Vector3.zero, b.Angle + b.Spin * t, s, s, c);
                return true;
            }
            float v = t - fuse, ride = b.Life - fuse, u = v / ride;
            // the shock front, plus a kick out of the detonation: the front itself starts slowly (it takes 3 s to grow)
            float r = KiteNavigatorCore.BlastRadius(b.Seed, t, fuse, BlastDuration) * b.Speed
                      + Kick * b.Speed * (1f - Mathf.Exp(-7f * v));
            var hot = Color.Lerp(White, Element(2), Smooth(0.05f, 0.35f, u));
            float streak = 1.3f + 2.6f * Mathf.Exp(-5f * v);
            Petal(2, b.P1 + b.Dir * r, b.Dir, 0f, b.Size * 1.1f * (1f - 0.4f * u), b.Size * streak, WithAlpha(hot, 1f - Smooth(0.35f, 1f, u)));
            return true;
        }

        // MASS: fold into the mote as a block appears, then the block rises out of the plane, grows and turns into its prism
        bool Morph(in Bit b, float t)
        {
            float morph = _s.MassMorphSeconds, fly = Mathf.Max(0.05f, morph - Fold);
            var colour = Element(1);
            Vector3 pos; Quaternion rot; Vector3 scale;
            float cube = b.Size * 0.85f;
            if (t < Fold)
            {
                float u = t / Fold;
                pos = Vector3.Lerp(b.P0, b.Ctrl, Mathf.SmoothStep(0f, 1f, u));
                rot = b.R0 * Quaternion.Euler(0f, b.Spin * t, 0f);
                scale = Vector3.one * (cube * EaseOutBack(u));
                if (b.WithPetal)
                    Petal(1, pos, Vector3.zero, b.Angle + 120f * u, b.Size * (1f - 0.7f * u), b.Size * (1f - 0.7f * u), WithAlpha(colour, 1f - u));
            }
            else if (t < morph)
            {
                float u = Mathf.Clamp01((t - Fold) / fly), e = u < 0.5f ? 4f * u * u * u : 1f - Mathf.Pow(-2f * u + 2f, 3f) / 2f;
                float dist = Vector3.Distance(b.Ctrl, b.P1);
                pos = Vector3.Lerp(b.Ctrl, b.P1, e) + _up * ((0.3f * dist + 5f) * Mathf.Sin(Mathf.PI * u) * b.Speed);
                rot = Quaternion.Slerp(b.R0 * Quaternion.Euler(0f, b.Spin * t, 0f), b.R1, Smooth(0.25f, 1f, u));
                scale = Vector3.LerpUnclamped(Vector3.one * cube, b.Scale1, EaseOutBack(u));
                colour = Color.Lerp(colour, White, 0.25f * Smooth(0.7f, 1f, u));
            }
            else
            {
                float v = (t - morph) / Settle;
                pos = b.P1; rot = b.R1;
                scale = b.Scale1 * (1f + 0.18f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, v * 1.6f)));
                colour = WithAlpha(Color.Lerp(White, Element(1), Smooth(0f, 0.5f, v)), 1f - Smooth(0.35f, 1f, v));
            }
            Box(pos, rot, scale, colour);
            return true;
        }

        // TIME: wait as a petal, then spiral onto the Grouper as an accelerating streak and land on its hull as a chip
        bool Stream(in Bit b, float t, bool waiting)
        {
            if (!_f.HasHull) return false;
            var colour = Element(3);
            if (waiting)
            {
                Petal(3, b.P0, Vector3.zero, b.Angle, b.Size, b.Size, colour);
                return true;
            }
            float flight = b.Speed;
            var target = _f.OnHull(b.Local);
            if (t < flight)
            {
                float u = t / flight;
                var pos = StreamAt(b, target, u);
                var ahead = StreamAt(b, target, Mathf.Min(1f, u + 0.03f)) - pos;
                float speed = ahead.magnitude / (0.03f * flight);
                Petal(3, pos, ahead, 0f, b.Size * Mathf.Lerp(0.8f, 0.45f, u), b.Size * (1f + Mathf.Min(4f, speed / 90f)),
                      Color.Lerp(colour, White, 0.35f * u * u));
                return true;
            }
            float v = (t - flight) / Land;
            var chip = Color.Lerp(White, colour, Smooth(0f, 0.4f, v));
            float pop = b.Size * (0.95f + 0.5f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, v * 2.5f))) * (1f - 0.4f * v);
            Petal(3, target, Vector3.zero, b.Angle + b.Spin * v, pop, pop, WithAlpha(chip, 1f - Smooth(0.4f, 1f, v)));
            return true;
        }

        Vector3 StreamAt(in Bit b, Vector3 target, float u)
        {
            float e = u * u * (1.6f - 0.6f * u);                     // accelerating, arriving at full pace
            var chord = target - b.P0;
            var side = Vector3.Cross(_up, chord);
            float len = chord.magnitude, swirl = Mathf.Sin(Mathf.PI * u) * (1f - 0.35f * u);
            return b.P0 + chord * e + (side.sqrMagnitude > 1e-6f ? side.normalized : _right) * (b.Side * 0.4f * len * swirl)
                   + _up * (0.08f * len * Mathf.Sin(Mathf.PI * u));
        }

        // CHARGE (and a steal's gold): wait, home onto the Wrasse on a curve, then orbit it at the pips' radius warming to gold
        bool Home(in Bit b, float t, bool waiting)
        {
            var kite = KiteWorld();
            if (waiting)
            {
                if (b.Shape == ShardShape) return true;
                Petal(b.Shape, b.P0, Vector3.zero, b.Angle, b.Size, b.Size, b.Tint);
                return true;
            }
            float flight = b.Speed;
            if (t < flight)
            {
                float u = Mathf.Pow(t / flight, 1.5f);
                var pos = Bezier(b.P0, b.Ctrl, kite, u);
                var ahead = Bezier(b.P0, b.Ctrl, kite, Mathf.Min(1f, u + 0.04f)) - pos;
                float s = b.Shape == ShardShape ? b.Size : b.Size * Mathf.Lerp(1f, 0.6f, u);
                Petal(b.Shape, pos, ahead, 0f, s * 0.7f, s * (1.4f + 1.6f * u), Color.Lerp(b.Tint, White, 0.3f * u));
                return true;
            }
            float v = t - flight;
            if (!b.Orbit)
            {
                float f = v / (b.Life - flight);
                Petal(b.Shape, kite, Vector3.zero, b.Angle, b.Size * (1f + f), b.Size * (1f + f), WithAlpha(White, 1f - f));
                return true;
            }
            var cfg = _s;
            float w = v / OrbitTime, ang = (b.Angle + b.Spin * v) * Mathf.Deg2Rad;
            float radius = cfg.KiteRadius * 1.9f * (1f + 0.7f * Mathf.Exp(-9f * v));
            var dir = _right * Mathf.Sin(ang) + _fwd * Mathf.Cos(ang);
            var tangent = Vector3.Cross(_up, dir) * Mathf.Sign(b.Spin);
            var c = Color.Lerp(b.Tint, Gold, Smooth(0f, 0.6f, w));
            float size = Mathf.Lerp(b.Size * 0.6f, cfg.KiteRadius * 0.18f, Smooth(0f, 1f, w));
            Petal(0, kite + dir * radius, tangent, 0f, size, size * 1.6f, WithAlpha(c, 1f - Smooth(0.6f, 1f, w)));
            return true;
        }

        static Vector3 Bezier(Vector3 a, Vector3 c, Vector3 b, float u)
        {
            float v = 1f - u;
            return v * v * a + 2f * v * u * c + u * u * b;
        }

        // ------------------------------------------------------------------ geometry

        Color Lin(Color c) => _linear ? new Color(Mathf.GammaToLinearSpace(c.r), Mathf.GammaToLinearSpace(c.g), Mathf.GammaToLinearSpace(c.b), c.a) : c;

        // A flat shape (a petal or a shard) at pos, its tip along `tip` (or turned `angle` degrees when tip is zero), sx wide
        // and sz long: flat on the board in the kite view, facing the camera in the pilot view.
        void Petal(int shape, Vector3 pos, Vector3 tip, float angle, float sx, float sz, Color colour)
        {
            if (colour.a <= 0.003f || sx <= 1e-3f || sz <= 1e-3f) return;
            var lin = Lin(colour);
            Emit(0, shape, pos + _up * _petalLift, _up, _fwd, tip, angle, sx, sz, lin);
            if (_f.HasPilot)
            {
                var toCam = _f.PilotPosition - pos;
                if (toCam.sqrMagnitude > 1e-4f) Emit(1, shape, pos, toCam.normalized, _f.PilotUp, tip, angle, sx, sz, lin);
            }
        }

        void Emit(int k, int shape, Vector3 at, Vector3 normal, Vector3 reference, Vector3 tip, float angle, float sx, float sz, Color c)
        {
            var dir = Vector3.ProjectOnPlane(tip, normal);
            if (dir.sqrMagnitude < 1e-6f)
            {
                var r = Vector3.ProjectOnPlane(reference, normal);
                if (r.sqrMagnitude < 1e-6f) r = Vector3.ProjectOnPlane(Vector3.forward, normal);
                dir = Quaternion.AngleAxis(angle, normal) * r;
            }
            var rot = Quaternion.LookRotation(dir.normalized, normal);
            var outline = _shapes[shape];
            var v = _v[k]; var cs = _c[k]; var tris = _t[k];
            int start = v.Count;
            v.Add(at); cs.Add(c);
            for (int i = 0; i < outline.Length; i++)
            {
                var o = outline[i];
                v.Add(at + rot * new Vector3(o.x * sx, 0f, o.z * sz));
                cs.Add(c);
            }
            for (int i = 1; i <= outline.Length; i++)
            {
                tris.Add(start); tris.Add(start + i); tris.Add(start + i % outline.Length + 1);
            }
        }

        static readonly Vector3[] FaceNormals = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        static readonly float[] FaceShade = { 1f, 0.42f, 0.74f, 0.62f, 0.86f, 0.54f };

        // A lit block (a Mass prism-to-be): each face shaded by which way it faces, so it reads as 3D from any camera.
        void Box(Vector3 pos, Quaternion rot, Vector3 scale, Color colour)
        {
            if (colour.a <= 0.003f) return;
            for (int k = 0; k < 2; k++)
            {
                if (k == 1 && !_f.HasPilot) break;
                var at = k == 0 ? pos + _up * _boxLift : pos;
                var v = _v[k]; var cs = _c[k]; var tris = _t[k];
                for (int f = 0; f < 6; f++)
                {
                    var n = FaceNormals[f];
                    var a = Mathf.Abs(n.x) > 0.5f ? Vector3.forward : Vector3.right;
                    var bAxis = Vector3.Cross(n, a);
                    var shade = Lin(new Color(colour.r * FaceShade[f], colour.g * FaceShade[f], colour.b * FaceShade[f], colour.a));
                    int start = v.Count;
                    for (int i = 0; i < 4; i++)
                    {
                        float sa = (i == 0 || i == 3) ? -0.5f : 0.5f, sb = i < 2 ? -0.5f : 0.5f;
                        var local = n * 0.5f + a * sa + bAxis * sb;
                        v.Add(at + rot * Vector3.Scale(local, scale));
                        cs.Add(shade);
                    }
                    tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                    tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
                }
            }
        }
    }
}
