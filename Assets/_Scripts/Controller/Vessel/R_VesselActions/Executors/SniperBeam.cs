using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Serpent sniper shot's <b>tracer</b> — a domain-coloured line down the round's own path
    /// plus a flare where it stopped, fading out over a fraction of a second.
    ///
    /// <para><b>Why it exists at all.</b> A hitscan is over in the frame it fires: there is no
    /// projectile to watch, and at 3,000 u the prism that died is a few pixels. Without a tracer
    /// the only evidence a shot happened is a camera kick, which is exactly the "I didn't notice
    /// the shot" report this was written for. The tracer is not decoration — it is the whole of
    /// the weapon's readable output.</para>
    ///
    /// <para><b>It is drawn on EVERY peer</b>, because the shot itself resolves on every peer:
    /// <c>R_VesselActionHandler</c> round-trips the press, so <c>SniperShotActionExecutor</c> runs
    /// its hitscan everywhere and the beam rides along with no networking of its own. A rifle
    /// nobody but the shooter can see is a rifle nobody can learn to dodge.</para>
    ///
    /// <para><b>It FADES rather than vanishing</b> (continuity of existence — CLAUDE.md: nothing
    /// the player can see may pop out of existence), and it is drawn at the cone's OWN radius at
    /// each end, so the beam is a picture of the volume the shot actually tested. A tracer thinner
    /// than the cone teaches the pilot to aim at something the weapon does not use.</para>
    ///
    /// <para><b>Pooled, with shared materials.</b> One instance is rented per shot and returns
    /// itself when its fade ends; the two materials are static, because a
    /// <c>new Material(...)</c> per shot leaks one material per trigger pull for the life of the
    /// session. Pool entries are re-validated on rent, since a scene load destroys them.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SniperBeam : MonoBehaviour
    {
        static readonly List<SniperBeam> Pool = new();
        static Material _lineMaterial;
        static Material _flareMaterial;
        static Mesh _flareMesh;
        static Transform _root;

        LineRenderer _line;
        Transform _flare;
        MeshRenderer _flareRenderer;
        MaterialPropertyBlock _block;

        Color _colour;
        float _startTime;
        float _beamSeconds;
        float _flareSeconds;
        float _flareRadius;

        /// <summary>
        /// Draw one tracer. <paramref name="startWidth"/>/<paramref name="endWidth"/> are the
        /// cone's radii at the muzzle and at the stop point. A non-positive
        /// <paramref name="beamSeconds"/> draws nothing, which is how the effect is switched off
        /// from the config asset.
        /// </summary>
        public static void Fire(Vector3 origin, Vector3 end, float startWidth, float endWidth,
            Color colour, float beamSeconds, float flareRadius, float flareSeconds, bool hit)
        {
            if (beamSeconds <= 0f) return;
            if (LineMaterial() == null) return;   // no graphics device — see LineMaterial
            var beam = Rent();
            if (beam == null) return;
            beam.Play(origin, end, startWidth, endWidth, colour, beamSeconds,
                      hit ? flareRadius : 0f, hit ? flareSeconds : 0f);
        }

        static SniperBeam Rent()
        {
            for (int i = Pool.Count - 1; i >= 0; i--)
            {
                var candidate = Pool[i];
                // A scene load destroys pooled instances; the Unity-null check is what keeps a
                // stale entry from being handed out as a live one.
                if (candidate == null) { Pool.RemoveAt(i); continue; }
                Pool.RemoveAt(i);
                return candidate;
            }
            return Create();
        }

        static SniperBeam Create()
        {
            if (_root == null)
            {
                var rootGo = new GameObject("SniperBeams") { hideFlags = HideFlags.DontSave };
                _root = rootGo.transform;
            }

            var go = new GameObject("SniperBeam");
            go.transform.SetParent(_root, false);
            var beam = go.AddComponent<SniperBeam>();
            beam.Build();
            go.SetActive(false);
            return beam;
        }

        void Build()
        {
            _block = new MaterialPropertyBlock();

            _line = gameObject.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = 2;
            _line.numCapVertices = 2;
            _line.alignment = LineAlignment.View;
            _line.textureMode = LineTextureMode.Stretch;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.generateLightingData = false;
            _line.sharedMaterial = LineMaterial();

            var flareGo = new GameObject("Flare");
            _flare = flareGo.transform;
            _flare.SetParent(transform, false);
            flareGo.AddComponent<MeshFilter>().sharedMesh = FlareMesh();
            _flareRenderer = flareGo.AddComponent<MeshRenderer>();
            _flareRenderer.sharedMaterial = FlareMaterial();
            _flareRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _flareRenderer.receiveShadows = false;
        }

        void Play(Vector3 origin, Vector3 end, float startWidth, float endWidth, Color colour,
            float beamSeconds, float flareRadius, float flareSeconds)
        {
            _colour = colour;
            _beamSeconds = Mathf.Max(0.01f, beamSeconds);
            _flareSeconds = Mathf.Max(0f, flareSeconds);
            _flareRadius = Mathf.Max(0f, flareRadius);
            _startTime = Time.time;

            gameObject.SetActive(true);
            _line.SetPosition(0, origin);
            _line.SetPosition(1, end);
            _line.startWidth = Mathf.Max(0.05f, startWidth);
            _line.endWidth = Mathf.Max(0.05f, endWidth);

            _flare.position = end;
            _flare.localScale = Vector3.one * (_flareRadius * 2f);
            _flareRenderer.enabled = _flareSeconds > 0f && _flareRadius > 0f;

            ApplyFade(0f);
        }

        void Update()
        {
            float elapsed = Time.time - _startTime;
            if (elapsed >= Mathf.Max(_beamSeconds, _flareSeconds))
            {
                Release();
                return;
            }
            ApplyFade(elapsed);
        }

        void ApplyFade(float elapsed)
        {
            // Squared falloff: a tracer reads as a flash rather than as a bar being switched off.
            float beam01 = 1f - Mathf.Clamp01(elapsed / _beamSeconds);
            float beamAlpha = beam01 * beam01;

            var head = _colour; head.a = beamAlpha;
            var tail = _colour; tail.a = beamAlpha * 0.35f;
            _line.startColor = head;
            _line.endColor = tail;
            _line.enabled = beamAlpha > 0.001f;

            if (!_flareRenderer.enabled) return;
            float flare01 = 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _flareSeconds));
            var flareColour = _colour;
            flareColour.a = flare01 * flare01;
            _block.Clear();
            _block.SetColor(BaseColorId, flareColour);
            _block.SetColor(ColorId, flareColour);
            _flareRenderer.SetPropertyBlock(_block);
            // The flare SHRINKS as it fades, so a kill reads as a burst collapsing rather than a
            // ball dimming in place.
            _flare.localScale = Vector3.one * (_flareRadius * 2f * Mathf.Lerp(0.4f, 1f, flare01));
        }

        void Release()
        {
            gameObject.SetActive(false);
            if (!Pool.Contains(this)) Pool.Add(this);
        }

        void OnDestroy()
        {
            Pool.Remove(this);
        }

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        static Material LineMaterial()
        {
            if (_lineMaterial != null) return _lineMaterial;
            // Sprites/Default is always in the build and is the one always-available shader that
            // honours a LineRenderer's per-vertex colour, which is how the fade is applied without
            // a material write per frame. (SpawnableCord takes the same route.)
            var shader = Shader.Find("Sprites/Default")
                      ?? Shader.Find("Universal Render Pipeline/Unlit");
            // A null shader means no graphics device or a stripped build (a headless server runs
            // this method too, because the shot resolves on every peer). new Material(null) logs an
            // error per shot; returning null makes the tracer a silent no-op there instead.
            if (shader == null) return null;
            _lineMaterial = new Material(shader) { name = "SniperBeamLine" };
            _lineMaterial.renderQueue = 3000;
            return _lineMaterial;
        }

        static Material FlareMaterial()
        {
            if (_flareMaterial != null) return _flareMaterial;
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Sprites/Default");
            if (shader == null) return null;
            _flareMaterial = new Material(shader) { name = "SniperBeamFlare" };
            _flareMaterial.SetFloat("_Surface", 1);
            _flareMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _flareMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            _flareMaterial.SetInt("_ZWrite", 0);
            _flareMaterial.EnableKeyword("_ALPHABLEND_ON");
            _flareMaterial.renderQueue = 3000;
            return _flareMaterial;
        }

        static Mesh FlareMesh()
        {
            if (_flareMesh != null) return _flareMesh;
            // A sphere rather than a billboarded quad: it reads the same from every angle, so the
            // flare needs no per-frame camera lookup and cannot face the wrong way in a PIP.
            var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            // Deactivated BEFORE anything can step: CreatePrimitive attaches a SphereCollider and
            // Destroy is deferred to end of frame, so a live collider would otherwise sit at the
            // world origin for a frame and could be hit by anything passing through it.
            probe.SetActive(false);
            _flareMesh = probe.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(probe); else DestroyImmediate(probe);
            return _flareMesh;
        }
    }
}
