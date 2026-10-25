// The trained 3D NEURAL CELLULAR AUTOMATON creatures (the lab's NCA lizard, whale and jellyfish) as a pure-C# core.
// Docs/NCA_CREATURES.md. No UnityEngine: the same file compiles under Unity's netstandard2.1 profile and under net8 for
// Tools/Build/nca_creature_harness, which replays the lab's own JavaScript runtime and asserts this port matches it.
//
// THE MODEL (research: Tools/NCA/nca3d.py on cece/gifted-curie-x2cpd0; runtime: Tools/Ecology/flight/creatures/
// nca_creature.src.js). A D x H x W grid of C = 16 float channels (0-2 premultiplied RGB, 3 alpha, 4-15 hidden). One step:
//   pre  = 3x3x3 dilation of {alpha > 0.1}                     (the alive mask)
//   for every cell in pre, in row-major order, one mulberry32 draw: it FIRES when draw <= fire_rate (0.5)
//   a firing cell perceives [identity, sobel_x, sobel_y, sobel_z] of every channel (64 features, zero padded),
//   runs a 64 -> 128 (ReLU) -> 16 MLP and ADDS the result to its own state
//   post = 3x3x3 dilation of {alpha > 0.1} of the new state; every cell not in (pre AND post) is zeroed.
// There is no clock input: the swim cycle (~68 steps) lives entirely in the trained network. One seed cell (alpha and
// the 12 hidden channels = 1 at the grid centre) grows the whole animal, and a hole cut in it regrows.
//
// This is the sparse step of nca_creature.src.js line for line (the same active list, the same row-major RNG order),
// with the per-cell network evaluated exactly as the lab's WebAssembly SIMD kernel (nca_kernel.c) does it, so a creature
// seeded alike follows the lab's trajectory: bit for bit against the lab's wasm backend, to ~1e-6 against its JS one. What the GAME adds lives in the glue (NcaCreatureFauna): who pays for regrowth, who bites.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Threading;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One species' trained network and body frame, read from the TextAsset Tools/Build/author_nca_creatures.py writes
    /// from the research export (results/&lt;run&gt;/weights.json): base64 little-endian float32, row-major [out, in].
    /// Immutable and shared by every individual of the species.
    /// </summary>
    public sealed class NcaVoxelWeights
    {
        public int C, Hid, D, H, W;
        public float FireRate = 0.5f;
        /// <summary>w1 [Hid, 4C] and w2 [C, Hid], row-major as trained; feature f = 4 * channel + {id, sx, sy, sz}.</summary>
        public float[] W1 = Array.Empty<float>(), B1 = Array.Empty<float>(), W2 = Array.Empty<float>(), B2 = Array.Empty<float>();
        /// <summary>The body's long axis in the grid's (W, H) plane, head first (unit), and the body centroid's offset
        /// from the grid centre in voxels - measured by the research over the eight trained poses.</summary>
        public float ForwardX = 1f, ForwardY, OffsetX, OffsetY;
        /// <summary>How far the render leans from the NCA's own colour toward the element colour (the lab's tint).</summary>
        public float Tint;
        /// <summary>Voxels (alpha &gt; 0.3) of the grown animal, measured by the harness: what "whole" means.</summary>
        public int GrownVoxels = 1;
        /// <summary>Steps from the seed to the grown animal, measured by the harness.</summary>
        public int GrowSteps = 100;
        public string Species = "";

        public int F => 4 * C;

        // The layouts the step runs on (nca_kernel.c's - the lab's WebAssembly SIMD kernel): four lanes of hidden units
        // or of output channels per Vector4, features in k * C + c order (stencil kind, then channel).
        //   W1T[f' * Hid/4 + g] = (w1[4g + l, 4c + k])_l   for f' = k * C + c
        //   W2T[h * C/4 + q]    = (w2[4q + l, h])_l
        internal Vector4[] W1T = Array.Empty<Vector4>(), B1V = Array.Empty<Vector4>(), W2T = Array.Empty<Vector4>(), B2V = Array.Empty<Vector4>();

        void BuildLanes()
        {
            int hg = Hid / 4, cg = C / 4;
            W1T = new Vector4[F * hg];
            for (int k = 0; k < 4; k++)
            for (int c = 0; c < C; c++)
            for (int g = 0; g < hg; g++)
            {
                int f = 4 * c + k, fp = k * C + c;
                W1T[fp * hg + g] = new Vector4(W1[(4 * g) * F + f], W1[(4 * g + 1) * F + f], W1[(4 * g + 2) * F + f], W1[(4 * g + 3) * F + f]);
            }
            B1V = new Vector4[hg];
            for (int g = 0; g < hg; g++) B1V[g] = new Vector4(B1[4 * g], B1[4 * g + 1], B1[4 * g + 2], B1[4 * g + 3]);
            W2T = new Vector4[Hid * cg];
            for (int h = 0; h < Hid; h++)
            for (int q = 0; q < cg; q++)
                W2T[h * cg + q] = new Vector4(W2[(4 * q) * Hid + h], W2[(4 * q + 1) * Hid + h], W2[(4 * q + 2) * Hid + h], W2[(4 * q + 3) * Hid + h]);
            B2V = new Vector4[cg];
            for (int q = 0; q < cg; q++) B2V[q] = new Vector4(B2[4 * q], B2[4 * q + 1], B2[4 * q + 2], B2[4 * q + 3]);
        }

        /// <summary>Re-derive the lane layouts after editing W1/B1/W2/B2 (the harness's negative control does).</summary>
        public void Rebuild() => BuildLanes();

        static readonly Dictionary<string, NcaVoxelWeights> s_cache = new();

        /// <summary>Parse once per text (every individual shares the weights). Throws on a malformed asset - fail loud.</summary>
        public static NcaVoxelWeights Parse(string json)
        {
            lock (s_cache)
            {
                if (s_cache.TryGetValue(json, out var w)) return w;
                w = new NcaVoxelWeights
                {
                    C = (int)Num(json, "channel_n"), Hid = (int)Num(json, "hidden"), FireRate = Num(json, "fire_rate"),
                    D = (int)Num(json, "D"), H = (int)Num(json, "H"), W = (int)Num(json, "W"),
                    ForwardX = Num(json, "forward_x"), ForwardY = Num(json, "forward_y"),
                    OffsetX = Num(json, "offset_x"), OffsetY = Num(json, "offset_y"),
                    Tint = Num(json, "tint"), GrownVoxels = Math.Max(1, (int)Num(json, "grown_voxels")),
                    GrowSteps = Math.Max(1, (int)Num(json, "grow_steps")), Species = Str(json, "species"),
                };
                if (w.C < 4 || w.C % 4 != 0 || w.Hid < 4 || w.Hid % 4 != 0 || w.D < 1 || w.H < 1 || w.W < 1)
                    throw new FormatException($"NcaVoxelWeights: bad shape C={w.C} hidden={w.Hid} grid={w.D}x{w.H}x{w.W}");
                float fn = MathF.Sqrt(w.ForwardX * w.ForwardX + w.ForwardY * w.ForwardY);
                if (!(fn > 1e-6f)) throw new FormatException("NcaVoxelWeights: forward is zero");
                w.ForwardX /= fn; w.ForwardY /= fn;
                w.W1 = Floats(json, "w1", w.Hid * w.F); w.B1 = Floats(json, "b1", w.Hid);
                w.W2 = Floats(json, "w2", w.C * w.Hid); w.B2 = Floats(json, "b2", w.C);
                w.BuildLanes();
                s_cache[json] = w;
                return w;
            }
        }

        static int ValueStart(string json, string key)
        {
            int i = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (i < 0) throw new FormatException($"NcaVoxelWeights: no \"{key}\"");
            return json.IndexOf(':', i) + 1;
        }

        static float Num(string json, string key)
        {
            int i = ValueStart(json, key);
            int j = i; while (j < json.Length && "-+.0123456789eE \t\r\n".IndexOf(json[j]) >= 0) j++;
            return float.Parse(json.Substring(i, j - i).Trim(), CultureInfo.InvariantCulture);
        }

        static string Str(string json, string key)
        {
            int i = json.IndexOf('"', ValueStart(json, key)) + 1;
            return json.Substring(i, json.IndexOf('"', i) - i);
        }

        static float[] Floats(string json, string key, int n)
        {
            int i = json.IndexOf('"', ValueStart(json, key)) + 1;
            int j = json.IndexOf('"', i);
            var bytes = Convert.FromBase64String(json.Substring(i, j - i));
            if (bytes.Length != n * 4) throw new FormatException($"NcaVoxelWeights: \"{key}\" holds {bytes.Length / 4} floats, expected {n}");
            var f = new float[n];
            Buffer.BlockCopy(bytes, 0, f, 0, bytes.Length);
            if (!BitConverter.IsLittleEndian)
                for (int q = 0; q < n; q++) { var b = BitConverter.GetBytes(f[q]); Array.Reverse(b); f[q] = BitConverter.ToSingle(b, 0); }
            return f;
        }

        // ── the body frame ───────────────────────────────────────────────────────────────────────────────────────
        // BODY coordinates are voxel units with the animal's head along +Z, its back along +Y (grid depth) and its
        // centroid at the origin - what the glue scales by the species' voxel size and hangs under the creature's
        // transform. The grid's (W, H) plane holds the long axis at `forward`, so the map is a rotation in that plane.

        /// <summary>Grid cell (z, y, x) centre to BODY coordinates.</summary>
        public void GridToBody(int z, int y, int x, out float bx, out float by, out float bz)
        {
            float lx = x + 0.5f - W * 0.5f - OffsetX, ly = z + 0.5f - D * 0.5f, lz = y + 0.5f - H * 0.5f - OffsetY;
            bx = ForwardY * lx - ForwardX * lz;
            by = ly;
            bz = ForwardX * lx + ForwardY * lz;
        }

        /// <summary>BODY coordinates to continuous grid coordinates (z, y, x) - the inverse of <see cref="GridToBody"/>.</summary>
        public void BodyToGrid(float bx, float by, float bz, out float gz, out float gy, out float gx)
        {
            float lx = ForwardY * bx + ForwardX * bz, lz = -ForwardX * bx + ForwardY * bz;
            gx = lx + W * 0.5f - 0.5f + OffsetX;
            gy = lz + H * 0.5f - 0.5f + OffsetY;
            gz = by + D * 0.5f - 0.5f;
        }

        /// <summary>A grid direction (dz, dy, dx) in BODY axes (no translation).</summary>
        public void GridDirToBody(float dz, float dy, float dx, out float bx, out float by, out float bz)
        {
            bx = ForwardY * dx - ForwardX * dy;
            by = dz;
            bz = ForwardX * dx + ForwardY * dy;
        }
    }

    /// <summary>
    /// One individual's NCA state and its sparse step - nca_creature.src.js (_begin / _flush / _finish / hit / count)
    /// in C#. Not thread-safe: one thread at a time (<see cref="NcaVoxelTicker"/> owns the hand-off).
    /// </summary>
    public sealed class NcaVoxelCore
    {
        public readonly NcaVoxelWeights Wt;
        public readonly int C, Hid, D, H, W, N, HW, F;

        float[] _s, _ns;
        readonly byte[] _pre, _post;
        readonly int[] _list, _postList;
        int[] _keep, _keepOld;
        int _listN, _postN, _keepN, _keepOldN;
        readonly int[] _q = new int[4];
        int _qn;
        readonly float[] _x, _hd;
        readonly Vector4[] _acc, _row;
        uint _rng;

        /// <summary>The 27 x 4 perception stencil (identity, sobel x, y, z), neighbour order dz, dy, dx.</summary>
        static readonly float[] K = BuildStencil();

        /// <summary>A voxel is part of the animal (drawn, counted, bitten) above this alpha - the lab's alphaThreshold.</summary>
        public float AlphaThreshold = 0.3f;
        /// <summary>Re-seed when every cell has died (the lab's showcase default). The GAME turns it off: a creature
        /// whose body is gone is dead, not reborn.</summary>
        public bool Respawn = true;
        public int Steps { get; private set; }
        public int Respawns { get; private set; }
        public long CellUpdates { get; private set; }
        /// <summary>True once a step found no live cell (with <see cref="Respawn"/> off the state stays empty).</summary>
        public bool Extinct { get; private set; }

        public NcaVoxelCore(NcaVoxelWeights weights, uint seed)
        {
            Wt = weights ?? throw new ArgumentNullException(nameof(weights));
            C = weights.C; Hid = weights.Hid; D = weights.D; H = weights.H; W = weights.W;
            HW = H * W; N = D * HW; F = 4 * C;
            _s = new float[N * C]; _ns = new float[N * C];
            _pre = new byte[N]; _post = new byte[N];
            _list = new int[N]; _postList = new int[N]; _keep = new int[N]; _keepOld = new int[N];
            _x = new float[4 * F]; _hd = new float[4 * Hid];
            _acc = new Vector4[Math.Max(4 * (C / 4), C / 4)]; _row = new Vector4[C / 4];
            _rng = seed == 0 ? 1u : seed;
            Reset();
        }

        static float[] BuildStencil()
        {
            var k = new float[27 * 4];
            float[] sm = { 1, 2, 1 }, df = { -1, 0, 1 }, e = { 0, 1, 0 };
            for (int z = 0, n = 0; z < 3; z++)
            for (int y = 0; y < 3; y++)
            for (int x = 0; x < 3; x++, n++)
            {
                k[n * 4] = e[z] * e[y] * e[x];
                k[n * 4 + 1] = sm[z] * sm[y] * df[x] / 32f;
                k[n * 4 + 2] = sm[z] * df[y] * sm[x] / 32f;
                k[n * 4 + 3] = df[z] * sm[y] * sm[x] / 32f;
            }
            return k;
        }

        /// <summary>mulberry32, exactly the lab's (so a seed draws the same fire mask).</summary>
        double Rand()
        {
            unchecked
            {
                _rng += 0x6D2B79F5u;
                uint t = (_rng ^ (_rng >> 15)) * (1u | _rng);
                t = (t + (t ^ (t >> 7)) * (61u | t)) ^ t;
                return (t ^ (t >> 14)) / 4294967296.0;
            }
        }

        /// <summary>Back to the single seed cell (alpha and the hidden channels = 1 at the grid centre), as nca3d.make_seed.</summary>
        public void Reset()
        {
            Array.Clear(_s, 0, _s.Length); Array.Clear(_ns, 0, _ns.Length);
            Array.Clear(_pre, 0, N); Array.Clear(_post, 0, N);
            _listN = 0; _postN = 0; _qn = 0; _keepOldN = 0;
            int i = ((D >> 1) * H + (H >> 1)) * W + (W >> 1);
            for (int c = 3; c < C; c++) _s[i * C + c] = 1f;
            _keep[0] = i; _keepN = 1;
            Extinct = false;
        }

        /// <summary>The state, channels-last: [cell * C + channel]. Read-only by convention (harness and frame builder).</summary>
        public float[] State => _s;
        public int[] Keep => _keep;
        public int KeepCount => _keepN;

        /// <summary>Alpha of grid cell (z, y, x); 0 outside the grid.</summary>
        public float AlphaAt(int z, int y, int x) =>
            z < 0 || z >= D || y < 0 || y >= H || x < 0 || x >= W ? 0f : _s[((z * H + y) * W + x) * C + 3];

        int Dilate(float[] st, int[] list, int n, byte[] mask, int[] outList)
        {
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                int i = list[k];
                if (!(st[i * C + 3] > 0.1f)) continue;
                int z = i / HW, y = (i - z * HW) / W, x = i - z * HW - y * W;
                int z0 = z > 0 ? z - 1 : 0, z1 = z < D - 1 ? z + 1 : z;
                int y0 = y > 0 ? y - 1 : 0, y1 = y < H - 1 ? y + 1 : y;
                int x0 = x > 0 ? x - 1 : 0, x1 = x < W - 1 ? x + 1 : x;
                for (int zz = z0; zz <= z1; zz++)
                for (int yy = y0; yy <= y1; yy++)
                {
                    int r = (zz * H + yy) * W;
                    for (int xx = x0; xx <= x1; xx++)
                    {
                        int j = r + xx;
                        if (mask[j] == 0) { mask[j] = 1; outList[m++] = j; }
                    }
                }
            }
            return m;
        }

        /// <summary>One whole NCA step (nca_creature.src.js grow(1): _begin, the fire draws, _flush, _finish).</summary>
        public void Step()
        {
            // _begin
            for (int k = 0; k < _listN; k++) _pre[_list[k]] = 0;
            int n = Dilate(_s, _keep, _keepN, _pre, _list);
            Array.Sort(_list, 0, n);   // row-major order: the lab's fire-mask RNG sequence
            for (int k = 0; k < _keepOldN; k++) Array.Clear(_ns, _keepOld[k] * C, C);
            for (int k = 0; k < n; k++) Array.Copy(_s, _list[k] * C, _ns, _list[k] * C, C);
            _keepOldN = 0;
            _listN = n;

            // fire
            double rate = Wt.FireRate;
            for (int k = 0; k < n; k++)
            {
                int i = _list[k];
                if (Rand() <= rate)
                {
                    _q[_qn++] = i;
                    if (_qn == 4) Flush();
                }
            }
            Flush();

            // _finish
            for (int k = 0; k < _postN; k++) _post[_postList[k]] = 0;
            _postN = Dilate(_ns, _list, n, _post, _postList);
            var kn = _keepOld;
            int m = 0;
            for (int k = 0; k < n; k++)
            {
                int i = _list[k];
                if (_pre[i] != 0 && _post[i] != 0) kn[m++] = i;
                else Array.Clear(_ns, i * C, C);
            }
            _keepOld = _keep; _keepOldN = _keepN; _keep = kn; _keepN = m;
            (_s, _ns) = (_ns, _s);
            Steps++;
            if (n == 0)
            {
                Extinct = true;
                if (Respawn) { Respawns++; Reset(); }
            }
        }

        /// <summary>
        /// Evaluate the queued cells (up to 4): perception, layer 1, layer 2, residual add into ns. nca_kernel.c (the lab's
        /// WebAssembly SIMD kernel) operation for operation - f32, Vector4 lanes over hidden units / output channels, the
        /// same accumulation order - so it reproduces the lab's wasm backend exactly (and its JS backend to ~1e-6).
        /// </summary>
        void Flush()
        {
            int nb = _qn;
            if (nb == 0) return;
            _qn = 0;
            var s = _s; var X = _x;
            int hg = Hid / 4, cg = C / 4;
            var w1t = Wt.W1T; var b1v = Wt.B1V; var w2t = Wt.W2T; var b2v = Wt.B2V;
            var acc = _acc;
            for (int b = 0; b < 4; b++)
            {
                int xo = b * F;
                if (b >= nb) { Array.Clear(X, xo, F); continue; }
                // perception: acc[k * cg + q] = sum over the stencil of K[n, k] * s[neighbour, 4q .. 4q + 3]
                for (int m = 0; m < 4 * cg; m++) acc[m] = Vector4.Zero;
                int i = _q[b], z = i / HW, y = (i - z * HW) / W, x = i - z * HW - y * W;
                int nIdx = 0;
                for (int dz = -1; dz <= 1; dz++)
                for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++, nIdx++)
                {
                    int zz = z + dz, yy = y + dy, xx = x + dx;
                    if (zz < 0 || zz >= D || yy < 0 || yy >= H || xx < 0 || xx >= W) continue;
                    int j = (zz * H + yy) * W + xx;
                    if (_pre[j] == 0) continue;   // outside the alive mask the state is exactly zero
                    int o = j * C;
                    for (int q = 0; q < cg; q++) _row[q] = new Vector4(s[o + 4 * q], s[o + 4 * q + 1], s[o + 4 * q + 2], s[o + 4 * q + 3]);
                    for (int k = 0; k < 4; k++)
                    {
                        float kk = K[nIdx * 4 + k];
                        if (kk == 0f) continue;
                        int a0 = k * cg;
                        for (int q = 0; q < cg; q++) acc[a0 + q] += kk * _row[q];
                    }
                }
                for (int m = 0; m < 4 * cg; m++)
                {
                    var v = acc[m];
                    int p = xo + 4 * m;
                    X[p] = v.X; X[p + 1] = v.Y; X[p + 2] = v.Z; X[p + 3] = v.W;
                }
            }

            // layer 1 (F -> Hid, ReLU): four cells per weight load
            var hd = _hd;
            for (int g = 0; g < hg; g++)
            {
                Vector4 bias = b1v[g], e0 = bias, e1 = bias, e2 = bias, e3 = bias;
                for (int f = 0, w = g; f < F; f++, w += hg)
                {
                    var wv = w1t[w];
                    e0 += wv * X[f]; e1 += wv * X[F + f]; e2 += wv * X[2 * F + f]; e3 += wv * X[3 * F + f];
                }
                e0 = Vector4.Max(e0, Vector4.Zero); e1 = Vector4.Max(e1, Vector4.Zero);
                e2 = Vector4.Max(e2, Vector4.Zero); e3 = Vector4.Max(e3, Vector4.Zero);
                int r = 4 * g;
                hd[r] = e0.X; hd[r + 1] = e0.Y; hd[r + 2] = e0.Z; hd[r + 3] = e0.W;
                r += Hid; hd[r] = e1.X; hd[r + 1] = e1.Y; hd[r + 2] = e1.Z; hd[r + 3] = e1.W;
                r += Hid; hd[r] = e2.X; hd[r + 1] = e2.Y; hd[r + 2] = e2.Z; hd[r + 3] = e2.W;
                r += Hid; hd[r] = e3.X; hd[r + 1] = e3.Y; hd[r + 2] = e3.Z; hd[r + 3] = e3.W;
            }

            // layer 2 (Hid -> C), residual add into ns; ~60 % of hidden units are off after the ReLU
            var ns = _ns;
            for (int b = 0; b < nb; b++)
            {
                for (int q = 0; q < cg; q++) acc[q] = b2v[q];
                int ho = b * Hid;
                for (int h = 0; h < Hid; h++)
                {
                    float hv = hd[ho + h];
                    if (hv == 0f) continue;
                    int w = h * cg;
                    for (int q = 0; q < cg; q++) acc[q] += w2t[w + q] * hv;
                }
                int o = _q[b] * C;
                for (int q = 0; q < cg; q++)
                {
                    var v = acc[q];
                    int p = o + 4 * q;
                    ns[p] += v.X; ns[p + 1] += v.Y; ns[p + 2] += v.Z; ns[p + 3] += v.W;
                }
            }
            CellUpdates += nb;
        }

        /// <summary>
        /// Erase every cell within <paramref name="r"/> voxels of grid point (gz, gy, gx): all channels zeroed (the lab's
        /// hit). The surviving cells regrow the hole on later steps. Returns the number of BODY voxels (alpha above
        /// <see cref="AlphaThreshold"/>) removed - the mass the cut took. Only between steps.
        /// </summary>
        public int Hit(float gz, float gy, float gx, float r)
        {
            if (!(r > 0f)) return 0;
            float r2 = r * r;
            int removed = 0;
            int z0 = Math.Max(0, (int)MathF.Floor(gz - r)), z1 = Math.Min(D - 1, (int)MathF.Ceiling(gz + r));
            int y0 = Math.Max(0, (int)MathF.Floor(gy - r)), y1 = Math.Min(H - 1, (int)MathF.Ceiling(gy + r));
            int x0 = Math.Max(0, (int)MathF.Floor(gx - r)), x1 = Math.Min(W - 1, (int)MathF.Ceiling(gx + r));
            for (int z = z0; z <= z1; z++)
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float ddz = z - gz, ddy = y - gy, ddx = x - gx;
                if (ddz * ddz + ddy * ddy + ddx * ddx > r2) continue;
                int i = (z * H + y) * W + x, o = i * C;
                if (_s[o + 3] > AlphaThreshold) removed++;
                Array.Clear(_s, o, C);
                Array.Clear(_ns, o, C);
                _pre[i] = 0;
            }
            return removed;
        }

        /// <summary>Voxels with alpha above <see cref="AlphaThreshold"/> - the body's size.</summary>
        public int Count()
        {
            int n = 0;
            for (int k = 0; k < _keepN; k++) if (_s[_keep[k] * C + 3] > AlphaThreshold) n++;
            return n;
        }

        /// <summary>Sum of every channel of every cell (a cheap whole-state fingerprint for the harness).</summary>
        public double Sum()
        {
            double sum = 0;
            for (int i = 0; i < _s.Length; i++) sum += _s[i];
            return sum;
        }

        /// <summary>
        /// Snapshot the state for drawing and for the glue's sensing (nca_creature.src.js _snap, plus the surface cull,
        /// the skin normal, the colour and the body segments the glue registers as hit points). Reads only.
        /// </summary>
        public void BuildFrame(NcaVoxelFrame f)
        {
            f.Reset();
            var s = _s;
            float thr = AlphaThreshold;
            float f0 = Wt.ForwardX, f1 = Wt.ForwardY;
            float ox = W * 0.5f - 0.5f + Wt.OffsetX, oz = H * 0.5f - 0.5f + Wt.OffsetY;
            float vh = 0, vt = 0; int nh = 0, nt = 0;
            float uMin = float.MaxValue, uMax = float.MinValue;
            int alive = 0;
            for (int k = 0; k < _keepN; k++)
            {
                int i = _keep[k], o = i * C;
                float a = s[o + 3];
                if (!(a > 0.05f)) continue;
                int z = i / HW, y = (i - z * HW) / W, x = i - z * HW - y * W;
                float ia = 1f / MathF.Max(a, 1e-3f);
                float R = Clamp01(s[o] * ia), G = Clamp01(s[o + 1] * ia), B = Clamp01(s[o + 2] * ia);
                // the lab's _skin: the trained lizard's luminance spans ~0.7 (dark marks) .. ~1.7 (pale belly)
                float rel = Clamp01(((0.6f * R + 0.9f * G + 0.5f * B) / 0.75f - 0.72f) / 0.95f);
                bool body = a > thr;
                bool interior = body
                                && AlphaAt(z, y, x - 1) > thr && AlphaAt(z, y, x + 1) > thr
                                && AlphaAt(z, y - 1, x) > thr && AlphaAt(z, y + 1, x) > thr
                                && AlphaAt(z - 1, y, x) > thr && AlphaAt(z + 1, y, x) > thr;
                // the skin's outward normal: -grad alpha (central differences, zero padded), in BODY axes
                float gx = AlphaAt(z, y, x - 1) - AlphaAt(z, y, x + 1);
                float gy = AlphaAt(z, y - 1, x) - AlphaAt(z, y + 1, x);
                float gz = AlphaAt(z - 1, y, x) - AlphaAt(z + 1, y, x);
                Wt.GridDirToBody(gz, gy, gx, out float nx, out float ny, out float nz);
                f.Add(i, a, rel, interior, nx, ny, nz);
                if (!body) continue;
                alive++;
                float lx = x - ox, lz = y - oz;
                float u = lx * f0 + lz * f1, v = -f1 * lx + f0 * lz;
                if (u > 5f) { vh += v; nh++; } else if (u < -5f) { vt += v; nt++; }
                Wt.GridToBody(z, y, x, out _, out _, out float bz);
                if (bz < uMin) uMin = bz;
                if (bz > uMax) uMax = bz;
            }
            f.Alive = alive;
            f.Bend = nh > 0 && nt > 0 ? vt / nt - vh / nh : 0f;
            f.Steps = Steps;
            f.Extinct = Extinct;
            if (alive == 0) return;

            // body segments along the long axis (0 = the head), and the head's front slab
            f.FrontZ = uMax; f.BackZ = uMin;
            int S = f.Segments;
            float span = MathF.Max(1e-3f, uMax - uMin);
            double hx = 0, hy = 0, hz = 0; int hn = 0;
            for (int k = 0; k < f.Count; k++)
            {
                int i = f.List[k];
                if (!(f.Alpha[i] > thr)) continue;
                int z = i / HW, y = (i - z * HW) / W, x = i - z * HW - y * W;
                Wt.GridToBody(z, y, x, out float bx, out float by, out float bz);
                int seg = Math.Min(S - 1, Math.Max(0, (int)((uMax - bz) / span * S)));
                f.SegN[seg]++;
                f.SegSum[3 * seg] += bx; f.SegSum[3 * seg + 1] += by; f.SegSum[3 * seg + 2] += bz;
                f.SegSq[seg] += (double)bx * bx + (double)by * by + (double)bz * bz;
                if (bz >= uMax - 2f) { hx += bx; hy += by; hz += bz; hn++; }
            }
            for (int g = 0; g < S; g++)
            {
                int n = f.SegN[g];
                if (n == 0) continue;
                double cx = f.SegSum[3 * g] / n, cy = f.SegSum[3 * g + 1] / n, cz = f.SegSum[3 * g + 2] / n;
                f.SegCentre[3 * g] = (float)cx; f.SegCentre[3 * g + 1] = (float)cy; f.SegCentre[3 * g + 2] = (float)cz;
                double var = f.SegSq[g] / n - (cx * cx + cy * cy + cz * cz);
                f.SegRadius[g] = (float)Math.Sqrt(Math.Max(0.25, var)) + 0.5f;
            }
            if (hn > 0) { f.HeadX = (float)(hx / hn); f.HeadY = (float)(hy / hn); f.HeadZ = (float)(hz / hn); }
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>
    /// A published snapshot of one NCA state (what <see cref="NcaVoxelCore.BuildFrame"/> fills): every cell with
    /// alpha &gt; 0.05 (its alpha, skin brightness, surface flag and normal), the body size, the swim bend, and the body
    /// split into <see cref="Segments"/> slabs along its long axis. Arrays are per grid cell so two frames interpolate
    /// by index; only listed cells are valid.
    /// </summary>
    public sealed class NcaVoxelFrame
    {
        public readonly int N, Segments;
        public readonly float[] Alpha, Lum, Normal;
        public readonly byte[] Interior;
        public readonly int[] List;
        public int Count;
        /// <summary>Body voxels (alpha above the core's threshold).</summary>
        public int Alive;
        /// <summary>Tail third's mean lateral offset minus the head third's (voxels) - the swim's tail beat.</summary>
        public float Bend;
        public int Steps;
        public bool Extinct;
        /// <summary>The body's extent along its long axis (BODY z, voxels): the front of the head and the tail tip.</summary>
        public float FrontZ, BackZ;
        /// <summary>The centroid of the head's front slab (BODY, voxels) - where the mouth and the heart seat are.</summary>
        public float HeadX, HeadY, HeadZ;
        public readonly int[] SegN;
        public readonly double[] SegSum, SegSq;
        /// <summary>Per segment: centroid (BODY, voxels, xyz) and RMS radius (voxels).</summary>
        public readonly float[] SegCentre, SegRadius;

        public NcaVoxelFrame(int cells, int segments)
        {
            N = cells; Segments = Math.Max(1, segments);
            Alpha = new float[N]; Lum = new float[N]; Normal = new float[3 * N]; Interior = new byte[N]; List = new int[N];
            SegN = new int[Segments]; SegSum = new double[3 * Segments]; SegSq = new double[Segments];
            SegCentre = new float[3 * Segments]; SegRadius = new float[Segments];
        }

        public void Reset()
        {
            for (int k = 0; k < Count; k++) Alpha[List[k]] = 0f;
            Count = 0; Alive = 0; Bend = 0f; FrontZ = BackZ = 0f; HeadX = HeadY = HeadZ = 0f;
            Array.Clear(SegN, 0, Segments); Array.Clear(SegSum, 0, SegSum.Length); Array.Clear(SegSq, 0, Segments);
            Array.Clear(SegCentre, 0, SegCentre.Length); Array.Clear(SegRadius, 0, Segments);
        }

        public void Add(int i, float a, float lum, bool interior, float nx, float ny, float nz)
        {
            Alpha[i] = a; Lum[i] = lum; Interior[i] = interior ? (byte)1 : (byte)0;
            float l = MathF.Sqrt(nx * nx + ny * ny + nz * nz);
            if (l > 1e-5f) { Normal[3 * i] = nx / l; Normal[3 * i + 1] = ny / l; Normal[3 * i + 2] = nz / l; }
            else { Normal[3 * i] = 0f; Normal[3 * i + 1] = 1f; Normal[3 * i + 2] = 0f; }
            List[Count++] = i;
        }

        /// <summary>Zero the drawn alpha inside a cut (so the hole shows at once, not one step later).</summary>
        public void Cut(NcaVoxelWeights w, float gz, float gy, float gx, float r)
        {
            float r2 = r * r;
            int hw = w.H * w.W;
            for (int k = 0; k < Count; k++)
            {
                int i = List[k];
                int z = i / hw, y = (i - z * hw) / w.W, x = i - z * hw - y * w.W;
                float dz = z - gz, dy = y - gy, dx = x - gx;
                if (dz * dz + dy * dy + dx * dx <= r2) Alpha[i] = 0f;
            }
        }
    }

    /// <summary>A region the creature cannot regrow until it has eaten enough to pay for it (grid coordinates).</summary>
    public struct NcaScar
    {
        public float Z, Y, X, R;
        /// <summary>Volume still owed before this scar may heal.</summary>
        public float Debt;
    }

    /// <summary>
    /// Runs one creature's NCA off the main thread: the main thread KICKS a step when the previous one is done and
    /// COLLECTS the published frame; bites and scars are applied by the main thread only while no step is in flight,
    /// so the core is never shared. Three frames rotate (previous, current - the two the render interpolates - and the
    /// one the worker is writing). The shape of SwarmTickJob without its double-buffered instance arrays.
    /// </summary>
    public sealed class NcaVoxelTicker
    {
        public readonly NcaVoxelCore Core;
        NcaVoxelFrame _work;
        public NcaVoxelFrame Prev { get; private set; }
        public NcaVoxelFrame Cur { get; private set; }
        readonly List<NcaScar> _scars = new();
        int _busy, _done;
        static readonly WaitCallback s_work = o => ((NcaVoxelTicker)o).Work();

        /// <summary>The worker's exception, if a step threw (the glue reports it and stops kicking).</summary>
        public Exception Error { get; private set; }
        /// <summary>Wall time of the last step, milliseconds (the glue's adaptive rate reads it).</summary>
        public double LastStepMs { get; private set; }

        public NcaVoxelTicker(NcaVoxelCore core, int segments)
        {
            Core = core ?? throw new ArgumentNullException(nameof(core));
            Prev = new NcaVoxelFrame(core.N, segments);
            Cur = new NcaVoxelFrame(core.N, segments);
            _work = new NcaVoxelFrame(core.N, segments);
            core.BuildFrame(Cur);
            core.BuildFrame(Prev);
        }

        public bool Busy => Volatile.Read(ref _busy) != 0;
        public IReadOnlyList<NcaScar> Scars => _scars;

        /// <summary>Run <paramref name="steps"/> whole steps now, on this thread (before the creature is shown).</summary>
        public void Grow(int steps)
        {
            if (Busy) throw new InvalidOperationException("NcaVoxelTicker.Grow while a step is in flight");
            for (int k = 0; k < steps; k++) { Core.Step(); ApplyScars(); }
            Core.BuildFrame(Cur);
            Core.BuildFrame(Prev);
        }

        /// <summary>Start one step. On the thread pool when <paramref name="threaded"/>, else inline. No-op while busy.</summary>
        public bool Kick(bool threaded)
        {
            if (Error != null || Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return false;
            if (threaded) ThreadPool.UnsafeQueueUserWorkItem(s_work, this);
            else Work();
            return true;
        }

        void Work()
        {
            var t0 = DateTime.UtcNow;
            try
            {
                Core.Step();
                ApplyScars();
                Core.BuildFrame(_work);
                Volatile.Write(ref _done, 1);
            }
            catch (Exception e) { Error = e; }
            finally
            {
                LastStepMs = (DateTime.UtcNow - t0).TotalMilliseconds;
                Volatile.Write(ref _busy, 0);
            }
        }

        /// <summary>If a step has finished since the last call, make its frame current (the old current becomes the
        /// previous). Main thread; false while a step is in flight or nothing new is published.</summary>
        public bool Collect()
        {
            if (Busy || Volatile.Read(ref _done) == 0) return false;
            _done = 0;
            var old = Prev;
            Prev = Cur;
            Cur = _work;
            _work = old;
            return true;
        }

        /// <summary>A cut at grid point (gz, gy, gx), radius <paramref name="r"/> voxels: the state and both drawn
        /// frames lose it now. Returns the body voxels removed. Main thread, only while not <see cref="Busy"/>.</summary>
        public int Hit(float gz, float gy, float gx, float r)
        {
            if (Busy) throw new InvalidOperationException("NcaVoxelTicker.Hit while a step is in flight");
            int removed = Core.Hit(gz, gy, gx, r);
            Cur.Cut(Core.Wt, gz, gy, gx, r);
            Prev.Cut(Core.Wt, gz, gy, gx, r);
            return removed;
        }

        /// <summary>Hold a wound open until it is paid for: re-cut after every step. Main thread, not while busy.</summary>
        public void AddScar(NcaScar scar)
        {
            if (Busy) throw new InvalidOperationException("NcaVoxelTicker.AddScar while a step is in flight");
            _scars.Add(scar);
        }

        /// <summary>Pay <paramref name="volume"/> toward the scars, oldest first; a scar paid in full heals (is
        /// dropped). Returns what was left over. Main thread, not while busy.</summary>
        public float PayScars(float volume)
        {
            if (Busy) throw new InvalidOperationException("NcaVoxelTicker.PayScars while a step is in flight");
            while (volume > 0f && _scars.Count > 0)
            {
                var sc = _scars[0];
                float pay = MathF.Min(volume, sc.Debt);
                sc.Debt -= pay;
                volume -= pay;
                if (sc.Debt <= 1e-4f) _scars.RemoveAt(0);
                else { _scars[0] = sc; break; }
            }
            return volume;
        }

        /// <summary>Volume still owed across every open scar.</summary>
        public float Debt
        {
            get { float d = 0f; for (int k = 0; k < _scars.Count; k++) d += _scars[k].Debt; return d; }
        }

        void ApplyScars()
        {
            for (int k = 0; k < _scars.Count; k++)
            {
                var sc = _scars[k];
                Core.Hit(sc.Z, sc.Y, sc.X, sc.R);
            }
        }
    }
}
