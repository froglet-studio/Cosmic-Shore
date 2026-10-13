using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The DRAWN half of the Stoat's annihilation ripple (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1c): a spherical
    /// space-time wave that bends the scene behind its front as it passes — the ring a shell projects to, pushed
    /// outward ahead of the front and pulled in behind it, with a red/blue fringe and a rim in the owner's domain
    /// colour. Presentation only; what the front DOES is <see cref="StoatRippleExecutor"/>'s.
    ///
    /// <para><b>How it draws.</b> One back-faced sphere (the black-hole lens's own mesh,
    /// <see cref="BlackHoleLens.LensSphere"/>) circumscribing the front, with <c>CosmicShore/StoatRipple</c>, whose
    /// pass is <c>LightMode = BlackHoleLens</c>: so it is drawn by <see cref="BlackHoleLensPass"/> AFTER the
    /// transparents, from the pass's copy of the camera colour, exactly as the lens is — a ripple bends what the camera
    /// already drew, shards and trails included. While a ripple lives it holds the pass open
    /// (<see cref="BlackHoleLensPass.Acquire"/> / <c>Release</c>), so it draws whether or not a hole is still live.
    /// Per-ripple values go through a <see cref="MaterialPropertyBlock"/> on the shared material.</para>
    ///
    /// <para>Pooled (a small static stack): a ripple is born per annihilation, never per frame.</para>
    /// </summary>
    public sealed class StoatRipple : MonoBehaviour
    {
        /// <summary><c>Resources/StoatRipple.mat</c> (shader <c>CosmicShore/StoatRipple</c>).</summary>
        public const string MaterialResourcePath = "StoatRipple";

        static readonly int RadiusId = Shader.PropertyToID("_RippleRadius");
        static readonly int BandId = Shader.PropertyToID("_RippleBand");
        static readonly int BendId = Shader.PropertyToID("_RippleBend");
        static readonly int SplitId = Shader.PropertyToID("_RippleSplit");
        static readonly int TintId = Shader.PropertyToID("_RippleTint");

        static readonly Stack<StoatRipple> s_pool = new();
        static Material s_material;
        static bool s_warned;

        MeshRenderer _renderer;
        MaterialPropertyBlock _block;
        float _age, _radius, _expand, _fade, _band, _bend, _split, _glow;
        Color _tint;
        bool _acquired;

        /// <summary>
        /// A ripple at <paramref name="at"/> growing to <paramref name="radius"/>. Null (and one warning) when the
        /// material cannot be drawn — the ripple's gameplay still happens, only unseen.
        /// </summary>
        public static StoatRipple Spawn(Vector3 at, float radius, float expandSeconds, float fadeSeconds, float band,
            float bend, float split, Color tint, float glow)
        {
            var material = SharedMaterial();
            if (!material) return null;
            StoatRipple ripple = null;
            while (s_pool.Count > 0 && !ripple) ripple = s_pool.Pop();
            if (!ripple)
            {
                var go = new GameObject("StoatRipple");
                go.AddComponent<MeshFilter>().sharedMesh = BlackHoleLens.LensSphere();
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                ripple = go.AddComponent<StoatRipple>();
                ripple._renderer = renderer;
                ripple._block = new MaterialPropertyBlock();
            }
            ripple.transform.SetPositionAndRotation(at, Quaternion.identity);
            ripple._age = 0f;
            ripple._radius = Mathf.Max(0.1f, radius);
            ripple._expand = Mathf.Max(0.1f, expandSeconds);
            ripple._fade = Mathf.Max(0f, fadeSeconds);
            ripple._band = band;
            ripple._bend = bend;
            ripple._split = split;
            ripple._tint = tint;
            ripple._glow = glow;
            ripple.Apply();
            ripple.gameObject.SetActive(true);
            return ripple;
        }

        static Material SharedMaterial()
        {
            if (s_material) return s_material;
            s_material = Resources.Load<Material>(MaterialResourcePath);
            if (!s_material && !s_warned)
            {
                s_warned = true;
                CSDebug.LogWarning($"[StoatRipple] Resources/{MaterialResourcePath}.mat is missing — the Stoat's annihilation " +
                                   "ripple still strips and kills, but nothing draws it.");
            }
            return s_material;
        }

        void OnEnable()
        {
            if (_acquired) return;
            BlackHoleLensPass.Acquire();
            _acquired = true;
        }

        void OnDisable()
        {
            if (!_acquired) return;
            BlackHoleLensPass.Release();
            _acquired = false;
        }

        void Update()
        {
            _age += Time.deltaTime;
            if (StoatRippleMath.Done(_age, _expand, _fade))
            {
                gameObject.SetActive(false);
                s_pool.Push(this);
                return;
            }
            Apply();
        }

        void Apply()
        {
            float front = StoatRippleMath.Front(_age, _expand, _radius);
            float strength = StoatRippleMath.Strength(_age, _expand, _fade);
            // The sphere circumscribes the front and its outer band (the mesh is unit-DIAMETER).
            float extent = Mathf.Max(0.1f, front * (1f + _band) + 0.5f);
            transform.localScale = Vector3.one * (2f * extent);
            _block.SetFloat(RadiusId, front);
            _block.SetFloat(BandId, Mathf.Max(0.05f, front * _band));
            _block.SetFloat(BendId, _bend * strength);
            _block.SetFloat(SplitId, _split);
            var tint = _tint * (_glow * strength);
            tint.a = strength;
            _block.SetColor(TintId, tint);
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>Play-mode (re)entry: the pool and the material cache describe a scene that is gone.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_pool.Clear();
            s_material = null;
            s_warned = false;
        }
    }
}
