using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Shiva himself (Assets/_Scripts/Controller/Arcade/TANDAVA.md §0): a humongous figure of prisms to the waist, with
    /// eyes and the third eye, Vasuki round his neck, the crescent in his hair and his matted locks. It is sampled from
    /// bones exactly as the lab draws it (Docs/Studios/ShivaLab/app.js layFigure): the same samples in every form, so a
    /// change of form, the clap and the jaw are one morph. DRAWN, never laid: it moves every frame (the dance, the clap,
    /// his turn), it sits inside his shield where nothing can touch it, and thousands of moving prism GameObjects would
    /// cost a collider and a transform write each - one <c>Graphics.RenderMeshInstanced</c> per colour and batch instead,
    /// with a GPU-instancing clone of the prism material per colour (the asset is never written: the SnowChanger rule).
    /// When the third shell breaks it comes apart: every prism flies out from his chest and fades.
    /// </summary>
    public sealed class ShivaFigure : MonoBehaviour
    {
        enum Part { Bone = 0, Ball = 1, Vasuki = 2, Moon = 3, Eye = 4, ThirdEye = 5, Lock = 6 }
        enum Hue { Body = 0, Hair = 1, Eye = 2, ThirdEye = 3, Vasuki = 4, Moon = 5 }
        const int Hues = 6, Batch = 1023, Locks = 10, LockLinks = 9, VasukiLinks = 46, MoonLinks = 16;

        struct Sample { public Part Part; public int Index; public float U, Theta, Angle; public Vector3 Dir; public int Side; public Hue Hue; }

        // the bones and balls (app.js BONES / BALLS): the joint names are ShivaJoints' fields
        static readonly (int a, int b, float r0, float r1)[] Bones =
        {
            (0, 1, 140, 168), (1, 2, 168, 62), (2, 3, 46, 40), (1, 5, 70, 52), (1, 6, 70, 52),
            (5, 7, 44, 34), (7, 8, 34, 26), (6, 9, 44, 34), (9, 10, 34, 26),
            (5, 11, 44, 34), (11, 12, 34, 26), (6, 13, 44, 34), (13, 14, 34, 26),
        };
        static readonly (int joint, float r, float sy, Hue hue)[] Balls =
        {
            (3, 84, 1.12f, Hue.Body), (4, 48, 1f, Hue.Hair), (8, 30, 1f, Hue.Body), (10, 30, 1f, Hue.Body), (12, 30, 1f, Hue.Body), (14, 30, 1f, Hue.Body),
        };
        static readonly ProfilerMarker s_draw = new("ShivaFigure.Draw");

        ShivaSettingsSO _settings;
        readonly List<Sample> _samples = new();
        Vector3[] _pos, _nrm, _vel;
        readonly Vector3[] _j = new Vector3[17];
        Matrix4x4[][] _matrices;
        int[] _count;
        Material[] _materials;
        RenderParams[] _params;
        Vector3 _origin;
        float _wonAt = -1f;

        /// <summary>Lay out the samples once (the spacing is the settings'), and make the per-colour materials.</summary>
        public void Initialize(ShivaSettingsSO settings, Vector3 origin)
        {
            _settings = settings;
            _origin = origin;
            _samples.Clear();
            float sp = Mathf.Max(4f, settings.FigureSpacing);
            var rest = ShivaDirectorCore.Figure(0, 0f, 0f, 0f, 0f);
            Fill(rest);
            for (int b = 0; b < Bones.Length; b++)
            {
                var (ja, jb, r0, r1) = Bones[b];
                float len = (_j[jb] - _j[ja]).magnitude;
                int nu = Mathf.Max(2, Mathf.RoundToInt(len / sp)), nt = Mathf.Max(6, Mathf.RoundToInt(2f * Mathf.PI * Mathf.Max(r0, r1) / sp));
                for (int i = 0; i < nu; i++)
                    for (int k = 0; k < nt; k++)
                        _samples.Add(new Sample { Part = Part.Bone, Index = b, U = (i + 0.5f) / nu, Theta = (k + (i % 2) * 0.5f) / nt * 2f * Mathf.PI, Hue = Hue.Body });
            }
            for (int b = 0; b < Balls.Length; b++)
            {
                var (_, r, sy, hue) = Balls[b];
                int n = Mathf.RoundToInt(4f * Mathf.PI * r * r * sy / (sp * sp));
                for (int i = 0; i < n; i++)
                {
                    float y = 1f - 2f * (i + 0.5f) / n, rr = Mathf.Sqrt(1f - y * y), th = i * 2.39996f;
                    _samples.Add(new Sample { Part = Part.Ball, Index = b, Dir = new Vector3(rr * Mathf.Cos(th), y, rr * Mathf.Sin(th)), Hue = hue });
                }
            }
            for (int i = 0; i < VasukiLinks; i++) _samples.Add(new Sample { Part = Part.Vasuki, Angle = i / (float)VasukiLinks * 2f * Mathf.PI, Hue = Hue.Vasuki });
            for (int i = 0; i < MoonLinks; i++) _samples.Add(new Sample { Part = Part.Moon, Angle = -1.1f + 2.2f * i / (MoonLinks - 1f), Hue = Hue.Moon });
            _samples.Add(new Sample { Part = Part.Eye, Side = -1, Hue = Hue.Eye });
            _samples.Add(new Sample { Part = Part.Eye, Side = 1, Hue = Hue.Eye });
            _samples.Add(new Sample { Part = Part.ThirdEye, Hue = Hue.ThirdEye });
            for (int l = 0; l < Locks; l++) for (int i = 0; i < LockLinks; i++) _samples.Add(new Sample { Part = Part.Lock, Index = l, Side = i, Hue = Hue.Hair });

            _pos = new Vector3[_samples.Count]; _nrm = new Vector3[_samples.Count]; _vel = new Vector3[_samples.Count];
            _matrices = new Matrix4x4[Hues][]; _count = new int[Hues];
            var perHue = new int[Hues];
            foreach (var s in _samples) perHue[(int)s.Hue]++;
            for (int h = 0; h < Hues; h++) _matrices[h] = new Matrix4x4[Mathf.Max(1, perHue[h])];
            MakeMaterials();
        }

        /// <summary>How many prisms the figure draws (the prism budget's line for the doc).</summary>
        public int PrismCount => _samples.Count;

        void MakeMaterials()
        {
            _materials = new Material[Hues];
            _params = new RenderParams[Hues];
            if (!_settings.FigureMaterial || !_settings.FigureMesh)
            {
                CSDebug.LogError("[Shiva] ShivaSettings has no figure mesh / material - re-run Tools/Build/author_shiva_assets.py. He will not be drawn.");
                return;
            }
            var bright = new[] { _settings.BodyBright, _settings.HairColor, _settings.EyeColor, _settings.ThirdEyeColor, _settings.VasukiColor, _settings.MoonColor };
            for (int h = 0; h < Hues; h++)
            {
                var m = new Material(_settings.FigureMaterial) { name = _settings.FigureMaterial.name + " (Shiva " + (Hue)h + ")", enableInstancing = true };
                Color b = bright[h], d = h == (int)Hue.Body ? _settings.BodyDark : b * 0.25f;
                if (m.HasProperty(BrightId)) m.SetColor(BrightId, b);
                if (m.HasProperty(DarkId)) m.SetColor(DarkId, d);
                if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, b);
                if (m.HasProperty(ColorId)) m.SetColor(ColorId, b);
                _materials[h] = m;
                _params[h] = new RenderParams(m) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false,
                    worldBounds = new Bounds(_origin, Vector3.one * 2400f) };
            }
        }

        static readonly int BrightId = Shader.PropertyToID("_BrightColor"), DarkId = Shader.PropertyToID("_DarkColor"),
            BaseColorId = Shader.PropertyToID("_BaseColor"), ColorId = Shader.PropertyToID("_Color");

        void OnDestroy()
        {
            if (_materials == null) return;
            foreach (var m in _materials) if (m) Destroy(m);
        }

        /// <summary>He comes apart from now on: every prism flies out from his chest.</summary>
        public void Shatter()
        {
            if (_wonAt >= 0f || _pos == null) return;
            _wonAt = Time.time;
            var chest = _origin + _j[1];
            for (int i = 0; i < _pos.Length; i++)
            {
                var d = _pos[i] - chest;
                _vel[i] = (d.sqrMagnitude > 1e-4f ? d.normalized : Random.onUnitSphere) * (140f + 520f * ((i * 7919) % 101) / 101f);
            }
        }

        /// <summary>A handful of his prisms' world positions (where the win's gold shards leave from).</summary>
        public void SamplePositions(List<Vector3> into, int count)
        {
            into.Clear();
            if (_pos == null || _pos.Length == 0) return;
            int step = Mathf.Max(1, _pos.Length / Mathf.Max(1, count));
            for (int i = 0; i < _pos.Length && into.Count < count; i += step) into.Add(_pos[i]);
        }

        /// <summary>Pose and draw him for this frame. Every peer calls it from the replicated state.</summary>
        public void Draw(int form, float clapK, float danceT, float mouthOpen, float yaw, float time)
        {
            if (_settings == null || _materials == null || _materials[0] == null) return;
            using (s_draw.Auto())
            {
                float wt = _wonAt >= 0f ? Time.time - _wonAt : 0f;
                if (_wonAt < 0f) Pose(form, clapK, danceT, mouthOpen, yaw, time);
                float fade = _wonAt >= 0f ? Mathf.Max(0f, 1f - wt / 5f) : 1f;
                if (fade <= 0f) return;
                for (int h = 0; h < Hues; h++) _count[h] = 0;
                float sp = Mathf.Max(4f, _settings.FigureSpacing) * 0.98f;
                for (int i = 0; i < _samples.Count; i++)
                {
                    var s = _samples[i];
                    float big = s.Part == Part.Eye ? 1.6f : s.Part == Part.ThirdEye ? 1.9f : s.Part is Part.Moon or Part.Vasuki ? 0.8f : s.Part == Part.Lock ? 0.9f : 1f;
                    float sc = sp * big * fade;
                    var p = _pos[i] + _vel[i] * wt;
                    var n = _nrm[i];
                    var rot = n.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(n) : Quaternion.identity;
                    int hue = (int)s.Hue;
                    _matrices[hue][_count[hue]++] = Matrix4x4.TRS(p, rot, new Vector3(sc, sc, sc * 0.28f + 2f));
                }
                for (int h = 0; h < Hues; h++)
                    for (int start = 0; start < _count[h]; start += Batch)
                        Graphics.RenderMeshInstanced(_params[h], _settings.FigureMesh, 0, _matrices[h], Mathf.Min(Batch, _count[h] - start), start);
            }
        }

        // ── the pose (app.js layFigure, in his own frame, then turned to his yaw) ──
        void Fill(in ShivaJoints j)
        {
            _j[0] = V(j.Waist); _j[1] = V(j.Chest); _j[2] = V(j.Neck); _j[3] = V(j.Head); _j[4] = V(j.Bun); _j[5] = V(j.ShoulderL); _j[6] = V(j.ShoulderR);
            _j[7] = V(j.ElbowUL); _j[8] = V(j.HandUL); _j[9] = V(j.ElbowUR); _j[10] = V(j.HandUR); _j[11] = V(j.ElbowLL); _j[12] = V(j.HandLL);
            _j[13] = V(j.ElbowLR); _j[14] = V(j.HandLR); _j[15] = V(j.Mouth); _j[16] = V(j.ThirdEye);
        }

        void Pose(int form, float clapK, float danceT, float mouthOpen, float yaw, float time)
        {
            Fill(ShivaDirectorCore.Figure(form, clapK, danceT, mouthOpen, 0f));
            var turn = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f);
            for (int i = 0; i < _samples.Count; i++)
            {
                var s = _samples[i];
                Vector3 p, n;
                switch (s.Part)
                {
                    case Part.Bone:
                    {
                        var (ja, jb, r0, r1) = Bones[s.Index];
                        Vector3 a = _j[ja], d = _j[jb] - a; float len = d.magnitude; Vector3 dir = len > 1e-4f ? d / len : Vector3.up;
                        var refv = Mathf.Abs(dir.y) > 0.9f ? Vector3.right : Vector3.up;
                        var e1 = Vector3.Cross(dir, refv).normalized; var e2 = Vector3.Cross(dir, e1);
                        float r = r0 + (r1 - r0) * s.U;
                        n = e1 * Mathf.Cos(s.Theta) + e2 * Mathf.Sin(s.Theta);
                        p = a + dir * (len * s.U) + n * r;
                        break;
                    }
                    case Part.Ball:
                    {
                        var (joint, r, sy, _) = Balls[s.Index];
                        n = s.Dir;
                        var off = new Vector3(s.Dir.x * r, s.Dir.y * r * sy, s.Dir.z * r);
                        if (s.Index == 0 && mouthOpen > 0.01f && s.Dir.z > 0.25f && s.Dir.y < -0.05f) off.y -= mouthOpen * 46f * (0.5f + s.Dir.z);   // the jaw drops
                        p = _j[joint] + off;
                        break;
                    }
                    case Part.Vasuki:
                    {
                        var c = _j[2]; float rr = 74f + 6f * Mathf.Sin(s.Angle * 3f + time * 2f);
                        p = new Vector3(c.x + rr * Mathf.Cos(s.Angle), c.y - 8f + 10f * Mathf.Sin(s.Angle * 2f + time), c.z + rr * Mathf.Sin(s.Angle));
                        n = (p - c).normalized;
                        break;
                    }
                    case Part.Moon:
                    {
                        var c = _j[4] + new Vector3(42f, 28f, 10f);
                        p = new Vector3(c.x + 34f * Mathf.Cos(s.Angle + 1.57f) * 0.7f, c.y + 34f * Mathf.Sin(s.Angle + 1.57f), c.z + 6f); n = Vector3.forward;
                        break;
                    }
                    case Part.Eye: p = _j[3] + new Vector3(s.Side * 30f, 4f, 86f); n = Vector3.forward; break;
                    case Part.ThirdEye: p = _j[16]; n = Vector3.forward; break;
                    default:
                    {
                        // the matted locks fall to his shoulders when still and fly out in the dance
                        float ang = s.Index / (float)Locks * 2f * Mathf.PI, fly = form == 1 ? 1f : form == 2 ? 0.4f : 0.12f, k = (s.Side + 1) / (float)LockLinks;
                        var o = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang) * 0.6f - 0.4f);
                        float wave = Mathf.Sin(time * 2.2f + s.Index + s.Side * 0.5f) * 18f * fly;
                        p = _j[3] + new Vector3(o.x * (60f + k * (40f + 260f * fly)), 30f - k * (150f - 120f * fly) + wave, o.z * (60f + k * (30f + 140f * fly)) - 20f);
                        n = o.normalized;
                        break;
                    }
                }
                _pos[i] = _origin + turn * p;
                _nrm[i] = turn * n;
            }
        }

        static Vector3 V(SVector3 v) => new(v.X, v.Y, v.Z);
    }
}
