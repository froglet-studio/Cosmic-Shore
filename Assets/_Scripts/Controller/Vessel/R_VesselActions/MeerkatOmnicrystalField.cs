using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The OMNICRYSTALS, the Meerkat's Omnicrystal ability's pickups (<c>R_VesselActions/MEERKAT.md</c> §7), the Vessel
    /// Studio's <c>placeOmni</c> / <c>stepOmni</c> / <c>drawOmni</c> (<c>Docs/Studios/VesselStudio/meerkat.html</c>, round
    /// 16): <c>omniCount</c> of them, gold, on the course line half way between rings; a Meerkat flying within
    /// <see cref="PickRadius"/> u of one takes it and its roots grow (<see cref="MeerkatWormholeExecutor"/>).
    ///
    /// <para><b>Where they are.</b> In Burrow Run, half way along evenly spaced legs of the course
    /// (<see cref="ForCourse"/>, from <c>BurrowRunController.OnCourseRaised</c>). In a scene with no course (the Toybox's
    /// Vessel Changer) the first local Meerkat lays them ahead of itself (<see cref="EnsureFreestyle"/>), and lays them
    /// again ahead of it a few seconds after the last is taken, so the ability can be tried there. The page draws a gold
    /// faceted core in a turning wire shell; this draws a gold core and a fainter shell through the wormhole's additive
    /// material.</para>
    ///
    /// <para><b>Who sees what.</b> The pilot who takes one takes it on its own machine; every other peer hears it in the
    /// roots it grows (<see cref="MarkTaken"/>). In the Toybox each machine lays its own.</para>
    /// </summary>
    public sealed class MeerkatOmnicrystalField : MonoBehaviour
    {
        /// <summary>A hull this close takes one (the page's <c>OMNI_PICK</c>).</summary>
        public const float PickRadius = 18f;
        const int Max = 12;
        const float CoreSize = 5.5f, ShellSize = 8.5f, RefillSeconds = 6f;

        static MeerkatOmnicrystalField s_instance;

        /// <summary>The field in this scene, if one was laid.</summary>
        public static MeerkatOmnicrystalField Instance => s_instance;

        readonly List<Vector3> _legs = new();   // the course's ring positions in order (one lap), when it has one
        readonly List<Vector3> _home = new();
        readonly List<bool> _alive = new();
        bool _freestyle, _placed;
        float _refillAt = -1f;
        Transform _anchor;
        static Mesh s_core;
        static Material s_mat;

        /// <summary>Burrow Run: omnicrystals half way between the course's rings (<paramref name="rings"/>, one lap, in order).</summary>
        public static MeerkatOmnicrystalField ForCourse(Transform parent, IReadOnlyList<Vector3> rings)
        {
            var f = Make(parent);
            f._legs.Clear();
            f._legs.AddRange(rings);
            f._freestyle = false;
            f._placed = false;
            return f;
        }

        /// <summary>Round 25: the field was laid on a race course (Burrow Run), so its network fills the race shell.</summary>
        public bool HasCourse => !_freestyle && _legs.Count > 0;

        /// <summary>Round 25: the farthest of the course's rings from <paramref name="centre"/> (0 with no course).</summary>
        public float FarthestRingFrom(Vector3 centre)
        {
            float far = 0f;
            for (int i = 0; i < _legs.Count; i++) far = Mathf.Max(far, Vector3.Distance(_legs[i], centre));
            return far;
        }

        /// <summary>A scene with no course: omnicrystals laid ahead of <paramref name="anchor"/> (a local Meerkat).</summary>
        public static MeerkatOmnicrystalField EnsureFreestyle(Transform anchor)
        {
            if (s_instance) return s_instance;
            var f = Make(null);
            f._freestyle = true;
            f._anchor = anchor;
            f._placed = false;
            return f;
        }

        static MeerkatOmnicrystalField Make(Transform parent)
        {
            if (s_instance) return s_instance;
            var go = new GameObject("MeerkatOmnicrystals");
            if (parent) go.transform.SetParent(parent, false);
            s_instance = go.AddComponent<MeerkatOmnicrystalField>();
            return s_instance;
        }

        /// <summary>Lay <paramref name="count"/> omnicrystals (the first Meerkat's <c>omniCount</c>).</summary>
        void Place(int count)
        {
            _home.Clear();
            _alive.Clear();
            int n = Mathf.Clamp(count, 0, Max);
            if (!_freestyle)
            {
                int m = _legs.Count;
                for (int i = 0; i < n && m > 1; i++)
                {
                    int leg = Mathf.FloorToInt((i + 0.5f) * m / n) % m;   // evenly spaced legs, half way along each
                    _home.Add((_legs[leg] + _legs[(leg + 1) % m]) * 0.5f);
                    _alive.Add(true);
                }
            }
            else if (_anchor)
            {
                Vector3 p = _anchor.position, f = _anchor.forward, r = _anchor.right;
                for (int i = 0; i < n; i++)   // ahead of the Meerkat, 200 u apart, a little to either side
                {
                    _home.Add(p + f * (300f + 200f * i) + r * ((i & 1) == 0 ? 50f : -50f));
                    _alive.Add(true);
                }
            }
            _placed = true;
        }

        /// <summary>A hull at <paramref name="at"/> takes the omnicrystal it is within <see cref="PickRadius"/> of, if any.</summary>
        public bool TryTake(Vector3 at, out int index)
        {
            index = -1;
            for (int i = 0; i < _home.Count; i++)
            {
                if (!_alive[i] || (_home[i] - at).sqrMagnitude > PickRadius * PickRadius) continue;
                _alive[i] = false;
                index = i;
                return true;
            }
            return false;
        }

        /// <summary>Another peer's pilot took omnicrystal <paramref name="index"/>.</summary>
        public void MarkTaken(int index)
        {
            if (index >= 0 && index < _alive.Count) _alive[index] = false;
        }

        void Update()
        {
            if (!_placed)
            {
                int count = -1;
                foreach (var e in MeerkatWormholeExecutor.Executors)
                    if (e && e.Config) { count = e.Config.OmniCount; break; }
                if (count >= 0) Place(count);
                return;
            }
            if (!_freestyle || !_anchor) return;
            bool any = false;
            for (int i = 0; i < _alive.Count; i++) any |= _alive[i];
            if (any) { _refillAt = -1f; return; }
            if (_refillAt < 0f) _refillAt = Time.time + RefillSeconds;
            else if (Time.time >= _refillAt) { _refillAt = -1f; _placed = false; }   // laid again ahead of the Meerkat
        }

        void LateUpdate()
        {
            if (_home.Count == 0 || !Resolve()) return;
            var rp = new RenderParams(s_mat) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false };
            float t = Time.time;
            for (int i = 0; i < _home.Count; i++)
            {
                if (!_alive[i]) continue;
                var spin = Quaternion.Euler((t * 0.6f + i) * Mathf.Rad2Deg, (t * 1.1f + i) * Mathf.Rad2Deg, 0f);
                float pulse = 1f + 0.08f * Mathf.Sin(t * 3f + i);
                Graphics.RenderMesh(rp, s_core, 0, Matrix4x4.TRS(_home[i], spin, Vector3.one * (CoreSize * pulse)));
                var back = Quaternion.Euler(-t * 0.4f * Mathf.Rad2Deg, (-t * 0.8f - i) * Mathf.Rad2Deg, 0f);
                Graphics.RenderMesh(rp, s_core, 1, Matrix4x4.TRS(_home[i], back, Vector3.one * ShellSize));
            }
        }

        /// <summary>The core (an icosahedron, its faces shaded, gold-white) and its shell (the same, faint), as two
        /// submeshes of one mesh drawn through the wormhole's additive material.</summary>
        static bool Resolve()
        {
            if (!s_mat) s_mat = Resources.Load<Material>(MeerkatWormholeView.AdditiveMaterialPath);
            if (!s_mat) return false;
            if (s_core) return true;
            float g = (1f + Mathf.Sqrt(5f)) / 2f;
            var v = new[]
            {
                new Vector3(-1, g, 0), new Vector3(1, g, 0), new Vector3(-1, -g, 0), new Vector3(1, -g, 0),
                new Vector3(0, -1, g), new Vector3(0, 1, g), new Vector3(0, -1, -g), new Vector3(0, 1, -g),
                new Vector3(g, 0, -1), new Vector3(g, 0, 1), new Vector3(-g, 0, -1), new Vector3(-g, 0, 1),
            };
            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };
            var pos = new List<Vector3>();
            var col = new List<Color>();
            var core = new List<int>();
            var shell = new List<int>();
            var gold = new Color(1f, 0.85f, 0.45f);
            for (int s = 0; s < 2; s++)
            for (int i = 0; i < f.Length; i += 3)
            {
                var a = v[f[i]].normalized; var b = v[f[i + 1]].normalized; var c = v[f[i + 2]].normalized;
                var n = Vector3.Cross(b - a, c - a).normalized;
                float k = s == 0 ? 0.55f + 0.45f * Mathf.Abs(Vector3.Dot(n, new Vector3(0.3f, 0.8f, 0.5f).normalized)) : 0.12f;   // faceted, lit
                var cc = gold * k;
                cc.a = 1f;
                int at = pos.Count;
                pos.Add(a); pos.Add(b); pos.Add(c);
                col.Add(cc); col.Add(cc); col.Add(cc);
                (s == 0 ? core : shell).AddRange(new[] { at, at + 1, at + 2 });
            }
            s_core = new Mesh { name = "MeerkatOmnicrystal", hideFlags = HideFlags.DontSave, subMeshCount = 2 };
            s_core.SetVertices(pos);
            s_core.SetColors(col);
            s_core.SetTriangles(core, 0);
            s_core.SetTriangles(shell, 1);
            s_core.RecalculateBounds();
            return true;
        }

        void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad() => s_instance = null;
    }
}
