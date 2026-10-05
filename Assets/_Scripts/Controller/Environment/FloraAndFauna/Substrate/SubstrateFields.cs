// Round 11b (Docs/SUBSTRATE_FAUNA.md §3): the cell's STIGMERGY fields - coarse G^3 grids every species in the cell
// writes and reads, so a pack's threat deposit is a locust's danger signal with nothing wired between them.
// Port of research substrate/fields.py: f <- decay * blur(f) + deposit, a separable [a, 1-2a, a] blur per axis, decay
// on SIGNALS only (never mass). The food channel is not deposited by agents: it is rebuilt from the cell's live food
// (the glue hands it the flora it may eat) every FoodEvery ticks, so it is a pure read of conserved mass.
//
// Pure C#: compiled and run by Tools/Build/substrate_harness.
using System;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>One point of food the fields are rebuilt from (sim space, relative to the cell centre).</summary>
    public struct SubstrateFood
    {
        public Vector3 Pos;
        public float Volume;
    }

    public sealed class SubstrateFields
    {
        public const int Food = 0;
        public const int Alarm = 1;
        public const int Threat = 2;
        public const int Scent = 3;
        public const int Channels = 4;
        // research Fields.__init__: (name, blur a, decay per step). SCENT is the game's: prey a predator can smell (the
        // pack follows the locusts' scent the way the locusts follow the food's) - same blur as food, signal decay.
        static readonly float[] s_blur = { 0.25f, 0.2f, 0.2f, 0.25f };
        static readonly float[] s_decay = { 1.0f, 0.85f, 0.9f, 0.8f };
        /// <summary>float's smallest normal value (2^-126); a field value below it is flushed to 0 (see Blur).</summary>
        internal const float MinNormal = 1.17549435e-38f;

        public readonly int G;
        public readonly float R, H;
        public int FoodEvery = 10;
        readonly float[][] _ch = new float[Channels][];
        readonly Vector3[][] _grad = new Vector3[Channels][];
        readonly float[][] _pending = new float[Channels][];
        readonly float[] _a, _b;
        int _k;

        public SubstrateFields(float radius, int g = 40)
        {
            G = Math.Max(4, g); R = radius; H = 2f * radius / G;
            int n = G * G * G;
            for (int c = 0; c < Channels; c++) { _ch[c] = new float[n]; _grad[c] = new Vector3[n]; _pending[c] = new float[n]; }
            _a = new float[n]; _b = new float[n];
        }

        public int Cell(Vector3 p)
        {
            int x = Math.Clamp((int)((p.X + R) / H), 0, G - 1);
            int y = Math.Clamp((int)((p.Y + R) / H), 0, G - 1);
            int z = Math.Clamp((int)((p.Z + R) / H), 0, G - 1);
            return (x * G + y) * G + z;
        }

        public float Sample(int channel, int cell) => _ch[channel][cell];
        public Vector3 Grad(int channel, int cell) => _grad[channel][cell];

        public void Deposit(int channel, Vector3 p, float amount)
        {
            if (channel == Food) return;   // food is a READ of mass, never a deposit
            _pending[channel][Cell(p)] += amount;
        }

        /// <summary>
        /// Once per tick (research Fields.update): food rebuilt from the live food every FoodEvery ticks (4 blur passes,
        /// normalised to its max), pilots deposit a unit wake into threat, and every signal channel takes its pending
        /// deposits, blurs and decays. Gradients every 2 ticks.
        /// </summary>
        public void Update(ReadOnlySpan<SubstrateFood> food, ReadOnlySpan<SubstratePilot> pilots)
        {
            _k++;
            if (_k % FoodEvery == 1 || FoodEvery == 1)
            {
                var f = _ch[Food];
                Array.Clear(f, 0, f.Length);
                for (int i = 0; i < food.Length; i++) f[Cell(food[i].Pos)] += food[i].Volume;
                for (int pass = 0; pass < 4; pass++) Blur(f, s_blur[Food], 1f);
                float mx = 1e-6f;
                for (int i = 0; i < f.Length; i++) if (f[i] > mx) mx = f[i];
                float inv = 1f / mx;
                for (int i = 0; i < f.Length; i++) f[i] *= inv;
                Gradient(Food);
            }
            for (int j = 0; j < pilots.Length; j++) _pending[SubstrateFields.Threat][Cell(pilots[j].Pos)] += 1f;
            for (int c = 1; c < Channels; c++)
            {
                var f = _ch[c]; var p = _pending[c];
                for (int i = 0; i < f.Length; i++) { f[i] += p[i]; p[i] = 0f; }
                Blur(f, s_blur[c], s_decay[c]);
                if (_k % 2 == 0) Gradient(c);
            }
        }

        /// <summary>Separable [a, 1-2a, a] blur, one pass per axis, decay folded into the last (research _blur3_nb).</summary>
        void Blur(float[] f, float a, float decay)
        {
            int g = G, gg = G * G;
            float c = 1f - 2f * a;
            var t = _a; var u = _b;
            for (int x = 0; x < g; x++)
                for (int y = 0; y < g; y++)
                    for (int z = 0; z < g; z++)
                    {
                        int i = (x * g + y) * g + z;
                        float v = c * f[i];
                        if (x > 0) v += a * f[i - gg];
                        if (x < g - 1) v += a * f[i + gg];
                        t[i] = v;
                    }
            for (int x = 0; x < g; x++)
                for (int y = 0; y < g; y++)
                    for (int z = 0; z < g; z++)
                    {
                        int i = (x * g + y) * g + z;
                        float v = c * t[i];
                        if (y > 0) v += a * t[i - g];
                        if (y < g - 1) v += a * t[i + g];
                        u[i] = v;
                    }
            for (int x = 0; x < g; x++)
                for (int y = 0; y < g; y++)
                    for (int z = 0; z < g; z++)
                    {
                        int i = (x * g + y) * g + z;
                        float v = c * u[i];
                        if (z > 0) v += a * u[i - 1];
                        if (z < g - 1) v += a * u[i + 1];
                        v *= decay;
                        // a decaying signal ends as float dust: below float's smallest NORMAL it is subnormal, and
                        // subnormal arithmetic runs ~100x slower on x86 - a cell whose agents have died spent 30 ms a
                        // tick blurring zeros (QA-SWARM-ROUND11-9). Flushed to 0; every normal value is untouched.
                        f[i] = MathF.Abs(v) < MinNormal ? 0f : v;
                    }
        }

        /// <summary>numpy.gradient over the grid, in cell units: central differences inside, one-sided at the faces.</summary>
        void Gradient(int channel)
        {
            var f = _ch[channel]; var d = _grad[channel];
            int g = G, gg = G * G;
            for (int x = 0; x < g; x++)
                for (int y = 0; y < g; y++)
                    for (int z = 0; z < g; z++)
                    {
                        int i = (x * g + y) * g + z;
                        float gx = x == 0 ? f[i + gg] - f[i] : x == g - 1 ? f[i] - f[i - gg] : 0.5f * (f[i + gg] - f[i - gg]);
                        float gy = y == 0 ? f[i + g] - f[i] : y == g - 1 ? f[i] - f[i - g] : 0.5f * (f[i + g] - f[i - g]);
                        float gz = z == 0 ? f[i + 1] - f[i] : z == g - 1 ? f[i] - f[i - 1] : 0.5f * (f[i + 1] - f[i - 1]);
                        d[i] = new Vector3(gx, gy, gz);
                    }
        }
    }
}
