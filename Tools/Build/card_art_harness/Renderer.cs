// A small deterministic software renderer for card art.
//
// It exists because a card background is a picture of the ARENA and the arena is data: the shipped
// generators run beside this file and emit prism poses, so the picture can be made without the
// editor, re-made whenever a generator changes, and compared byte for byte in a --check. It is NOT
// the game's renderer and does not pretend to be - it draws the SAME geometry in the SAME palette
// with the prism shader's defining read (a dark base face lerped toward a bright rim by fresnel,
// Docs/PALETTE.md §2), so the card reads as the mode's world rather than as a render of it.
//
// Pipeline: triangles (boxes, rings, spheres) -> z-buffered, perspective-correct raster at a
// supersampled size -> fog toward the backdrop -> membrane shell (analytic) -> bloom (bright-pass,
// separable blur) -> ACES tonemap -> sRGB -> box-filter down. Every step is plain float math in a
// fixed order, so the same spec produces the same bytes on every machine the SDK runs on.
using System;
using System.Collections.Generic;

namespace CardArt
{
    public struct V3
    {
        public double x, y, z;
        public V3(double x, double y, double z) { this.x = x; this.y = y; this.z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static V3 operator -(V3 a) => new V3(-a.x, -a.y, -a.z);
        public static V3 operator *(V3 a, double f) => new V3(a.x * f, a.y * f, a.z * f);
        public static V3 operator *(double f, V3 a) => a * f;
        public static double Dot(V3 a, V3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static V3 Cross(V3 a, V3 b) => new V3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public double Len => Math.Sqrt(x * x + y * y + z * z);
        public V3 Norm { get { double l = Len; return l > 1e-12 ? this * (1.0 / l) : new V3(0, 0, 0); } }
        public static V3 Lerp(V3 a, V3 b, double t) => a + (b - a) * t;
        public static V3 Mul(V3 a, V3 b) => new V3(a.x * b.x, a.y * b.y, a.z * b.z);
    }

    /// <summary>What a surface is painted with. Base/rim are LINEAR HDR (the colour set's own
    /// values); emissive adds light that ignores shading (nucleus, crystals, gate rings).</summary>
    public sealed class Material
    {
        public V3 Base, Rim, Emissive;
        public double RimPower = 2.2;   // fresnel exponent: how far toward the silhouette the rim sits
        public double EdgeGlow = 0.9;   // box-face edge highlight strength (0 = none)
        public double EdgeWidth = 0.09; // fraction of the face's smaller extent
        public double Alpha = 1.0;      // < 1 = screen-door coverage (the game's dither, Docs/LIT.md)
    }

    public struct Tri
    {
        public V3 a, b, c;           // world positions
        public double ua, va, ub, vb, uc, vc; // face-local uv (0..1) for the edge highlight
        public double faceW, faceH;  // world size of the face the triangle belongs to
        public V3 n;                 // flat face normal
        public Material m;
    }

    public sealed class Scene
    {
        readonly List<Tri> _tris = new();
        public int TriangleCount => _tris.Count;
        public V3 BoundsMin = new V3(double.MaxValue, double.MaxValue, double.MaxValue);
        public V3 BoundsMax = new V3(double.MinValue, double.MinValue, double.MinValue);
        public List<(V3 centre, double radius, V3 colour, double strength)> Shells = new();
        /// <summary>One point per bounded primitive (a box's centre, a ring's rim) - what the
        /// camera frames. The bounds box alone over-frames a sparse arena: one outlier prism or a
        /// long thin rail makes the diagonal the size of the whole cell.</summary>
        public readonly List<V3> Samples = new();

        void Grow(V3 p)
        {
            BoundsMin = new V3(Math.Min(BoundsMin.x, p.x), Math.Min(BoundsMin.y, p.y), Math.Min(BoundsMin.z, p.z));
            BoundsMax = new V3(Math.Max(BoundsMax.x, p.x), Math.Max(BoundsMax.y, p.y), Math.Max(BoundsMax.z, p.z));
        }

        void Quad(V3 p0, V3 p1, V3 p2, V3 p3, Material m, bool bound)
        {
            var n = V3.Cross(p1 - p0, p3 - p0).Norm;
            double w = (p1 - p0).Len, h = (p3 - p0).Len;
            _tris.Add(new Tri { a = p0, b = p1, c = p2, ua = 0, va = 0, ub = 1, vb = 0, uc = 1, vc = 1, faceW = w, faceH = h, n = n, m = m });
            _tris.Add(new Tri { a = p0, b = p2, c = p3, ua = 0, va = 0, ub = 1, vb = 1, uc = 0, vc = 1, faceW = w, faceH = h, n = n, m = m });
            if (bound) { Grow(p0); Grow(p1); Grow(p2); Grow(p3); }
        }

        /// <summary>An oriented box: centre, three axes (already scaled to HALF extents).</summary>
        public void Box(V3 c, V3 ax, V3 ay, V3 az, Material m, bool bound = true)
        {
            if (bound) Samples.Add(c);
            V3 P(int sx, int sy, int sz) => c + ax * sx + ay * sy + az * sz;
            Quad(P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), m, bound);     // +z
            Quad(P(1, -1, -1), P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), m, bound); // -z
            Quad(P(1, -1, 1), P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), m, bound);     // +x
            Quad(P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), m, bound); // -x
            Quad(P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), P(-1, 1, -1), m, bound);     // +y
            Quad(P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), P(-1, -1, 1), m, bound); // -y
        }

        /// <summary>A torus: ring of radius R and tube radius r about axis n.</summary>
        public void Ring(V3 c, V3 n, double R, double r, Material m, int seg = 48, int side = 8, bool bound = true)
        {
            n = n.Norm;
            var t = Math.Abs(n.y) < 0.9 ? V3.Cross(n, new V3(0, 1, 0)).Norm : V3.Cross(n, new V3(1, 0, 0)).Norm;
            var b = V3.Cross(n, t);
            if (bound) for (int k = 0; k < 8; k++) Samples.Add(c + (t * Math.Cos(k * Math.PI / 4) + b * Math.Sin(k * Math.PI / 4)) * R);
            V3 P(int i, int j)
            {
                double a = 2 * Math.PI * i / seg, s = 2 * Math.PI * j / side;
                var radial = t * Math.Cos(a) + b * Math.Sin(a);
                return c + radial * (R + r * Math.Cos(s)) + n * (r * Math.Sin(s));
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < side; j++)
                    Quad(P(i, j), P(i + 1, j), P(i + 1, j + 1), P(i, j + 1), m, bound);
        }

        /// <summary>A UV sphere, drawn smooth-ish (flat faces are small at this tessellation).</summary>
        public void Sphere(V3 c, double r, Material m, int lat = 24, int lon = 48, bool bound = true)
        {
            if (bound) { Samples.Add(c + new V3(r, 0, 0)); Samples.Add(c - new V3(r, 0, 0)); }
            V3 P(int i, int j)
            {
                double th = Math.PI * i / lat, ph = 2 * Math.PI * j / lon;
                return c + new V3(Math.Sin(th) * Math.Cos(ph), Math.Cos(th), Math.Sin(th) * Math.Sin(ph)) * r;
            }
            var mm = new Material { Base = m.Base, Rim = m.Rim, Emissive = m.Emissive, RimPower = m.RimPower, EdgeGlow = 0, Alpha = m.Alpha };
            for (int i = 0; i < lat; i++)
                for (int j = 0; j < lon; j++)
                    Quad(P(i, j), P(i, j + 1), P(i + 1, j + 1), P(i + 1, j), mm, bound);
        }

        /// <summary>A cone (a blast, a sight): apex, axis, length, half-angle in degrees.</summary>
        public void Cone(V3 apex, V3 axis, double length, double halfDeg, Material m, int seg = 40, bool bound = false)
        {
            axis = axis.Norm;
            var t = Math.Abs(axis.y) < 0.9 ? V3.Cross(axis, new V3(0, 1, 0)).Norm : V3.Cross(axis, new V3(1, 0, 0)).Norm;
            var b = V3.Cross(axis, t);
            double r = length * Math.Tan(halfDeg * Math.PI / 180);
            var mm = new Material { Base = m.Base, Rim = m.Rim, Emissive = m.Emissive, RimPower = m.RimPower, EdgeGlow = 0, Alpha = m.Alpha };
            var baseC = apex + axis * length;
            for (int i = 0; i < seg; i++)
            {
                double a0 = 2 * Math.PI * i / seg, a1 = 2 * Math.PI * (i + 1) / seg;
                var p0 = baseC + (t * Math.Cos(a0) + b * Math.Sin(a0)) * r;
                var p1 = baseC + (t * Math.Cos(a1) + b * Math.Sin(a1)) * r;
                var n = V3.Cross(p0 - apex, p1 - apex).Norm;
                _tris.Add(new Tri { a = apex, b = p0, c = p1, faceW = 1, faceH = 1, n = n, m = mm });
            }
            if (bound) { Grow(apex); Grow(baseC); }
        }

        /// <summary>A see-through fresnel shell drawn analytically (the cell membrane).</summary>
        public void Shell(V3 c, double r, V3 colour, double strength) => Shells.Add((c, r, colour, strength));

        public IReadOnlyList<Tri> Tris => _tris;
    }

    public sealed class Camera
    {
        public V3 Pos, Fwd, Right, Up;
        public double TanHalfFov, Aspect;
        public Camera(V3 pos, V3 target, V3 up, double vfovDeg, double aspect)
        {
            Pos = pos; Fwd = (target - pos).Norm;
            Right = V3.Cross(up, Fwd).Norm;
            if (Right.Len < 0.5) Right = V3.Cross(new V3(1, 0, 0), Fwd).Norm;
            Up = V3.Cross(Fwd, Right);
            TanHalfFov = Math.Tan(vfovDeg * Math.PI / 360.0); Aspect = aspect;
        }
        public V3 Ray(double sx, double sy, int w, int h) // sx,sy pixel centre coords
        {
            double nx = (2 * (sx + 0.5) / w - 1) * TanHalfFov * Aspect;
            double ny = (1 - 2 * (sy + 0.5) / h) * TanHalfFov;
            return (Fwd + Right * nx + Up * ny).Norm;
        }
    }

    public sealed class Look
    {
        public V3 SkyTop = new V3(0.0025, 0.003, 0.010), SkyBottom = new V3(0.006, 0.003, 0.014);
        public V3 Nebula = new V3(0.016, 0.006, 0.030);
        public V3 LightDir = new V3(-0.35, 0.8, -0.45).Norm;
        public double FogDistance = 0;       // 0 = derive from the scene's extent
        public double FogStrength = 0.55;
        public double Exposure = 1.15;
        public double BloomThreshold = 0.9, BloomStrength = 0.55;
        public int Stars = 900;
        public int Seed = 1;
    }

    public static class Renderer
    {
        public static byte[] Render(Scene scene, Camera cam, Look look, int width, int height, int ss)
        {
            int W = width * ss, H = height * ss;
            var col = new V3[W * H];
            var depth = new double[W * H];
            for (int i = 0; i < depth.Length; i++) depth[i] = double.PositiveInfinity;

            // ── backdrop ───────────────────────────────────────────────────────────
            var rng = new Random(look.Seed);
            for (int y = 0; y < H; y++)
            {
                double t = (double)y / (H - 1);
                var sky = V3.Lerp(look.SkyTop, look.SkyBottom, t);
                for (int x = 0; x < W; x++)
                {
                    double u = (double)x / W - 0.62, v = t - 0.38;
                    double neb = Math.Exp(-(u * u * 3.2 + v * v * 6.0)) * 0.9 + Math.Exp(-((u + 0.55) * (u + 0.55) * 5 + (v - 0.35) * (v - 0.35) * 9)) * 0.45;
                    col[y * W + x] = sky + look.Nebula * neb;
                }
            }
            for (int s = 0; s < look.Stars; s++)
            {
                int x = rng.Next(W), y = rng.Next(H);
                double b = Math.Pow(rng.NextDouble(), 6) * 2.2 + 0.05;
                var tint = rng.NextDouble() < 0.25 ? new V3(0.7, 0.8, 1.2) : new V3(1.1, 1.0, 0.9);
                col[y * W + x] = col[y * W + x] + tint * b;
            }
            var backdrop = (V3[])col.Clone();

            // ── geometry ───────────────────────────────────────────────────────────
            double extent = (scene.BoundsMax - scene.BoundsMin).Len;
            double fogDist = look.FogDistance > 0 ? look.FogDistance : Math.Max(1, extent) * 0.9;
            double near = 0.05;
            double fx = W / (2.0 * cam.TanHalfFov * cam.Aspect), fy = H / (2.0 * cam.TanHalfFov);

            foreach (var tri in scene.Tris)
            {
                // camera space
                var A = ToCam(tri.a, cam); var B = ToCam(tri.b, cam); var C = ToCam(tri.c, cam);
                if (A.z < near && B.z < near && C.z < near) continue;
                if (A.z < near || B.z < near || C.z < near) continue; // near-plane crossers are rare at card distances; drop
                double ax = W * 0.5 + A.x / A.z * fx, ay = H * 0.5 - A.y / A.z * fy;
                double bx = W * 0.5 + B.x / B.z * fx, by = H * 0.5 - B.y / B.z * fy;
                double cx = W * 0.5 + C.x / C.z * fx, cy = H * 0.5 - C.y / C.z * fy;
                double area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                if (Math.Abs(area) < 1e-9) continue;
                int minX = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
                int maxX = Math.Min(W - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
                int minY = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
                int maxY = Math.Min(H - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
                if (minX > maxX || minY > maxY) continue;

                // flat per-face terms
                var n = tri.n;
                var centre = (tri.a + tri.b + tri.c) * (1.0 / 3.0);
                var toCam = (cam.Pos - centre).Norm;
                if (V3.Dot(n, toCam) < 0) n = -n; // two-sided: a lay's winding is not guaranteed
                double ndv = Math.Abs(V3.Dot(n, toCam));
                double fres = Math.Pow(1 - ndv, tri.m.RimPower);
                double lambert = 0.55 + 0.45 * Math.Max(0, V3.Dot(n, look.LightDir));
                var surface = V3.Lerp(tri.m.Base * lambert, tri.m.Rim, Math.Min(1, fres * 0.85 + 0.08)) + tri.m.Emissive;
                double minDim = Math.Max(1e-6, Math.Min(tri.faceW, tri.faceH));
                double ew = tri.m.EdgeWidth * minDim;
                double iaz = 1 / A.z, ibz = 1 / B.z, icz = 1 / C.z;

                for (int py = minY; py <= maxY; py++)
                {
                    double sy = py + 0.5;
                    for (int px = minX; px <= maxX; px++)
                    {
                        double sx = px + 0.5;
                        double w0 = ((bx - sx) * (cy - sy) - (by - sy) * (cx - sx)) / area;
                        double w1 = ((cx - sx) * (ay - sy) - (cy - sy) * (ax - sx)) / area;
                        double w2 = 1 - w0 - w1;
                        if (w0 < 0 || w1 < 0 || w2 < 0) continue;
                        double iz = w0 * iaz + w1 * ibz + w2 * icz;
                        double z = 1 / iz;
                        int idx = py * W + px;
                        if (z >= depth[idx]) continue;
                        if (tri.m.Alpha < 1 && Dither(px, py) >= tri.m.Alpha) continue;
                        depth[idx] = z;

                        var c = surface;
                        if (tri.m.EdgeGlow > 0)
                        {
                            double u = (w0 * tri.ua * iaz + w1 * tri.ub * ibz + w2 * tri.uc * icz) * z;
                            double v = (w0 * tri.va * iaz + w1 * tri.vb * ibz + w2 * tri.vc * icz) * z;
                            double du = Math.Min(u, 1 - u) * tri.faceW, dv = Math.Min(v, 1 - v) * tri.faceH;
                            double e = Math.Min(du, dv);
                            if (e < ew) c = c + tri.m.Rim * (tri.m.EdgeGlow * (1 - e / ew));
                        }
                        double f = look.FogStrength * (1 - Math.Exp(-z / fogDist));
                        col[idx] = V3.Lerp(c, backdrop[idx] * 0.6 + look.SkyBottom * 0.4, f);
                    }
                }
            }

            // ── membrane shells (analytic, additive, depth-aware on the near half) ──────
            foreach (var (centre, radius, colour, strength) in scene.Shells)
            {
                for (int py = 0; py < H; py++)
                    for (int px = 0; px < W; px++)
                    {
                        var d = cam.Ray(px, py, W, H);
                        var oc = cam.Pos - centre;
                        double bq = V3.Dot(oc, d), cq = V3.Dot(oc, oc) - radius * radius;
                        double disc = bq * bq - cq;
                        if (disc <= 0) continue;
                        double sq = Math.Sqrt(disc);
                        double t0 = -bq - sq, t1 = -bq + sq;
                        int idx = py * W + px;
                        // grazing factor: how close the ray runs to the tangent
                        double closest = Math.Sqrt(Math.Max(0, V3.Dot(oc, oc) - bq * bq)) / radius;
                        // From INSIDE the membrane every ray crosses the far wall, so the rim term
                        // would wash the whole frame; there it is a whisper, not an outline.
                        double inside = V3.Dot(oc, oc) < radius * radius ? 0.12 : 1.0;
                        double rim = (Math.Pow(Math.Clamp(closest, 0, 1), 12) + 0.01) * strength * inside;
                        double add = 0;
                        // far wall first (dimmer), then the near wall - each only where it is in
                        // front of whatever geometry already owns the pixel
                        if (t1 > 0 && t1 * V3.Dot(d, cam.Fwd) < depth[idx]) add += rim * 0.55;
                        if (t0 > 0 && t0 * V3.Dot(d, cam.Fwd) < depth[idx]) add += rim;
                        if (add > 0) col[idx] = col[idx] + colour * add;
                    }
            }

            // ── bloom ──────────────────────────────────────────────────────────────
            var bright = new V3[W * H];
            for (int i = 0; i < col.Length; i++)
            {
                var c = col[i]; double l = 0.2126 * c.x + 0.7152 * c.y + 0.0722 * c.z;
                bright[i] = l > look.BloomThreshold ? c * ((l - look.BloomThreshold) / l) : new V3(0, 0, 0);
            }
            int radius2 = Math.Max(2, (int)(Math.Min(W, H) * 0.012));
            for (int pass = 0; pass < 3; pass++) { BlurH(bright, W, H, radius2); BlurV(bright, W, H, radius2); }
            for (int i = 0; i < col.Length; i++) col[i] = col[i] + bright[i] * look.BloomStrength;

            // ── tonemap + sRGB + downsample ───────────────────────────────────────
            var outBytes = new byte[width * height * 3];
            double inv = 1.0 / (ss * ss);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    double r = 0, g = 0, b = 0;
                    for (int j = 0; j < ss; j++)
                        for (int i = 0; i < ss; i++)
                        {
                            var c = col[(y * ss + j) * W + x * ss + i] * look.Exposure;
                            r += Srgb(Aces(c.x)); g += Srgb(Aces(c.y)); b += Srgb(Aces(c.z));
                        }
                    int o = (y * width + x) * 3;
                    outBytes[o] = ToByte(r * inv); outBytes[o + 1] = ToByte(g * inv); outBytes[o + 2] = ToByte(b * inv);
                }
            return outBytes;
        }

        static V3 ToCam(V3 p, Camera c) { var d = p - c.Pos; return new V3(V3.Dot(d, c.Right), V3.Dot(d, c.Up), V3.Dot(d, c.Fwd)); }

        // 4x4 ordered dither: coverage, not blending (the prism shaders' own screen door).
        static readonly double[] Bayer = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
        static double Dither(int x, int y) => (Bayer[(y & 3) * 4 + (x & 3)] + 0.5) / 16.0;

        static double Aces(double x)
        {
            x = Math.Max(0, x);
            return Math.Clamp((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14), 0, 1);
        }
        static double Srgb(double c) => c <= 0.0031308 ? 12.92 * c : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
        static byte ToByte(double v) => (byte)Math.Clamp((int)Math.Round(v * 255.0), 0, 255);

        static void BlurH(V3[] a, int W, int H, int r)
        {
            var row = new V3[W];
            for (int y = 0; y < H; y++)
            {
                var acc = new V3(0, 0, 0);
                for (int x = -r; x <= r; x++) acc = acc + a[y * W + Math.Clamp(x, 0, W - 1)];
                for (int x = 0; x < W; x++)
                {
                    row[x] = acc * (1.0 / (2 * r + 1));
                    acc = acc - a[y * W + Math.Clamp(x - r, 0, W - 1)] + a[y * W + Math.Clamp(x + r + 1, 0, W - 1)];
                }
                Array.Copy(row, 0, a, y * W, W);
            }
        }
        static void BlurV(V3[] a, int W, int H, int r)
        {
            var colm = new V3[H];
            for (int x = 0; x < W; x++)
            {
                var acc = new V3(0, 0, 0);
                for (int y = -r; y <= r; y++) acc = acc + a[Math.Clamp(y, 0, H - 1) * W + x];
                for (int y = 0; y < H; y++)
                {
                    colm[y] = acc * (1.0 / (2 * r + 1));
                    acc = acc - a[Math.Clamp(y - r, 0, H - 1) * W + x] + a[Math.Clamp(y + r + 1, 0, H - 1) * W + x];
                }
                for (int y = 0; y < H; y++) a[y * W + x] = colm[y];
            }
        }
    }
}
