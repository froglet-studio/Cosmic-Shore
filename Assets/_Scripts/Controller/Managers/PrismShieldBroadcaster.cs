using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Phase 1 of the shield effect redesign.
    ///
    /// Coalesces per-prism shield state changes into a small number of spatial
    /// "shield events" and fires one procedural shockwave ring + one SFX per
    /// event, instead of N per-prism animations + N SFX plays. When 200 prisms
    /// shield together from the same AOE, the player sees and hears a single
    /// wave event, not 200 overlapping smooth color tweens.
    ///
    /// Concerns addressed:
    ///   (a) Per-prism shield SFX are de-duplicated to one per coalesced event.
    ///       Material animation duration is also shortened at the state manager
    ///       level so the animation job queue drains faster.
    ///   (b) The expanding ring visual + single loud SFX hit reads as an event,
    ///       rather than a subtle simultaneous color lerp on many prisms.
    ///   (c) Shielded *steady state* readability is deferred to a follow-up
    ///       shader pass — this phase only changes the transition moment.
    ///
    /// This class is fully code-driven: no prefabs, no materials, no shaders
    /// required. It auto-creates itself on first use (EnsureInstance pattern
    /// matching PrismTimerManager) and builds its shockwave pool at runtime
    /// from LineRenderers with a Sprites/Default material.
    /// </summary>
    public class PrismShieldBroadcaster : Singleton<PrismShieldBroadcaster>
    {
        public static PrismShieldBroadcaster EnsureInstance()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("[PrismShieldBroadcaster]");
            go.AddComponent<PrismShieldBroadcaster>();
            return Instance;
        }

        [Header("Coalescing")]
        [Tooltip("Shield state changes for the same domain within this world-space radius in the same frame merge into a single shockwave event.")]
        [SerializeField] private float coalesceRadius = 6f;

        [Header("Activation Shockwave")]
        [SerializeField] private float activateStartRadius = 0.5f;
        [SerializeField] private float activateEndRadius = 9f;
        [SerializeField] private float activateDuration = 0.35f;
        [SerializeField] private float activateStartWidth = 0.9f;
        [SerializeField] private float activateEndWidth = 0.12f;

        [Header("Deactivation Shockwave")]
        [SerializeField] private float deactivateStartRadius = 5f;
        [SerializeField] private float deactivateEndRadius = 0.2f;
        [SerializeField] private float deactivateDuration = 0.22f;
        [SerializeField] private float deactivateStartWidth = 0.15f;
        [SerializeField] private float deactivateEndWidth = 0.6f;

        [Header("Visual")]
        [SerializeField] private int poolSize = 32;
        [SerializeField] private int segmentCount = 48;
        [Tooltip("Extra emissive bump on top of the domain color. Higher values = hotter ring.")]
        [SerializeField] private float colorIntensity = 1.4f;

        [Header("SFX")]
        [Tooltip("When true, PrismStateManager skips its per-prism shield SFX — the broadcaster plays one SFX per coalesced event instead.")]
        public bool OwnsShieldSfx = true;

        private struct PendingEvent
        {
            public Vector3 OriginWS;
            public Domains Domain;
            public bool IsSuper;
        }

        private struct ActiveRing
        {
            public Transform Root;
            public LineRenderer Line;
            public float StartTime;
            public float Duration;
            public float StartRadius;
            public float EndRadius;
            public float StartWidth;
            public float EndWidth;
            public Color StartColor;
            public Color EndColor;
            public bool InUse;
        }

        private readonly List<PendingEvent> _pendingActivations = new(32);
        private readonly List<PendingEvent> _pendingDeactivations = new(32);
        private bool[] _coalesceConsumed = new bool[64];

        private ActiveRing[] _pool;
        private Material _sharedLineMaterial;
        private Transform _poolRoot;
        private Camera _cachedCamera;

        public override void Awake()
        {
            base.Awake();
            if (Instance != this) return;
            BuildPool();
        }

        private void BuildPool()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            _sharedLineMaterial = new Material(shader) { enableInstancing = false };

            var poolGo = new GameObject("[ShockwavePool]");
            _poolRoot = poolGo.transform;
            _poolRoot.SetParent(transform, false);

            _pool = new ActiveRing[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                var go = new GameObject($"Shockwave_{i}");
                go.transform.SetParent(_poolRoot, false);
                go.SetActive(false);

                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.loop = true;
                lr.positionCount = segmentCount;
                lr.sharedMaterial = _sharedLineMaterial;
                lr.numCapVertices = 0;
                lr.numCornerVertices = 0;
                lr.textureMode = LineTextureMode.Stretch;
                lr.alignment = LineAlignment.View;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;
                lr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                lr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

                // Unit circle in local XY plane. Radius is applied via transform scale;
                // billboarding is applied by copying the main camera's rotation each frame.
                for (int s = 0; s < segmentCount; s++)
                {
                    float t = (float)s / segmentCount * Mathf.PI * 2f;
                    lr.SetPosition(s, new Vector3(Mathf.Cos(t), Mathf.Sin(t), 0f));
                }

                _pool[i] = new ActiveRing { Root = go.transform, Line = lr, InUse = false };
            }
        }

        // ---- Public API ----

        public void ReportShieldActivation(Vector3 originWS, Domains domain, bool isSuper = false)
        {
            _pendingActivations.Add(new PendingEvent { OriginWS = originWS, Domain = domain, IsSuper = isSuper });
        }

        public void ReportShieldDeactivation(Vector3 prismPosWS, Domains domain)
        {
            _pendingDeactivations.Add(new PendingEvent { OriginWS = prismPosWS, Domain = domain });
        }

        // ---- Frame pump ----

        private void LateUpdate()
        {
            if (_pendingActivations.Count > 0) CoalesceAndFire(_pendingActivations, activation: true);
            if (_pendingDeactivations.Count > 0) CoalesceAndFire(_pendingDeactivations, activation: false);
            AnimateRings();
        }

        private void CoalesceAndFire(List<PendingEvent> list, bool activation)
        {
            int count = list.Count;
            if (_coalesceConsumed.Length < count) _coalesceConsumed = new bool[Mathf.NextPowerOfTwo(count)];
            for (int i = 0; i < count; i++) _coalesceConsumed[i] = false;

            float r2 = coalesceRadius * coalesceRadius;
            bool playedSfxThisBatch = false;

            for (int i = 0; i < count; i++)
            {
                if (_coalesceConsumed[i]) continue;
                var e = list[i];
                Vector3 center = e.OriginWS;
                bool anySuper = e.IsSuper;
                int merges = 1;

                for (int j = i + 1; j < count; j++)
                {
                    if (_coalesceConsumed[j]) continue;
                    var o = list[j];
                    if (o.Domain != e.Domain) continue;
                    if ((o.OriginWS - e.OriginWS).sqrMagnitude > r2) continue;

                    center += o.OriginWS;
                    anySuper |= o.IsSuper;
                    merges++;
                    _coalesceConsumed[j] = true;
                }

                center /= merges;
                FireShockwave(center, e.Domain, activation, anySuper);

                if (OwnsShieldSfx && !playedSfxThisBatch && AudioSystem.Instance != null)
                {
                    AudioSystem.Instance.PlayGameplaySFX(
                        activation ? GameplaySFXCategory.ShieldActivate : GameplaySFXCategory.ShieldDeactivate);
                    // One SFX per coalesced batch per frame — still allows distinct
                    // activation and deactivation events in the same frame to each
                    // play, because this flag is scoped to a single CoalesceAndFire call.
                    playedSfxThisBatch = true;
                }
            }

            list.Clear();
        }

        private void FireShockwave(Vector3 originWS, Domains domain, bool activation, bool isSuper)
        {
            int idx = FindFreeRing();
            if (idx < 0) return;

            ref var ring = ref _pool[idx];
            ring.Root.gameObject.SetActive(true);
            ring.Root.position = originWS;
            ring.StartTime = Time.time;
            ring.InUse = true;

            Color tint = GetDomainColor(domain) * colorIntensity;
            if (isSuper) tint = Color.Lerp(tint, Color.white, 0.5f);
            tint.a = 1f;

            if (activation)
            {
                ring.StartRadius = activateStartRadius;
                ring.EndRadius = activateEndRadius;
                ring.StartWidth = activateStartWidth;
                ring.EndWidth = activateEndWidth;
                ring.Duration = activateDuration;
                ring.StartColor = tint;
                var endColor = tint; endColor.a = 0f;
                ring.EndColor = endColor;
            }
            else
            {
                ring.StartRadius = deactivateStartRadius;
                ring.EndRadius = deactivateEndRadius;
                ring.StartWidth = deactivateStartWidth;
                ring.EndWidth = deactivateEndWidth;
                ring.Duration = deactivateDuration;
                var startColor = tint; startColor.a = 0.85f;
                ring.StartColor = startColor;
                var endColor = tint; endColor.a = 0f;
                ring.EndColor = endColor;
            }
        }

        private int FindFreeRing()
        {
            for (int i = 0; i < _pool.Length; i++)
                if (!_pool[i].InUse) return i;

            // Pool exhausted — reclaim the oldest ring.
            int oldest = 0;
            float oldestT = float.MaxValue;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_pool[i].StartTime < oldestT)
                {
                    oldestT = _pool[i].StartTime;
                    oldest = i;
                }
            }
            _pool[oldest].InUse = false;
            _pool[oldest].Root.gameObject.SetActive(false);
            return oldest;
        }

        private void AnimateRings()
        {
            if (_cachedCamera == null) _cachedCamera = Camera.main;
            Quaternion billboard = _cachedCamera != null ? _cachedCamera.transform.rotation : Quaternion.identity;

            float now = Time.time;
            for (int i = 0; i < _pool.Length; i++)
            {
                ref var ring = ref _pool[i];
                if (!ring.InUse) continue;

                float t = (now - ring.StartTime) / Mathf.Max(0.0001f, ring.Duration);
                if (t >= 1f)
                {
                    ring.InUse = false;
                    ring.Root.gameObject.SetActive(false);
                    continue;
                }

                // Ease out quad — sharp impact, soft settle.
                float eased = 1f - (1f - t) * (1f - t);
                float radius = Mathf.Lerp(ring.StartRadius, ring.EndRadius, eased);
                float width = Mathf.Lerp(ring.StartWidth, ring.EndWidth, eased);
                Color c = Color.Lerp(ring.StartColor, ring.EndColor, eased);

                ring.Root.localScale = new Vector3(radius, radius, radius);
                ring.Root.rotation = billboard;

                ring.Line.startWidth = width;
                ring.Line.endWidth = width;
                ring.Line.startColor = c;
                ring.Line.endColor = c;
            }
        }

        private static Color GetDomainColor(Domains domain)
        {
            switch (domain)
            {
                case Domains.Jade: return new Color(0.15f, 0.95f, 0.45f, 1f);
                case Domains.Ruby: return new Color(1f, 0.22f, 0.38f, 1f);
                case Domains.Gold: return new Color(1f, 0.82f, 0.18f, 1f);
                case Domains.Blue: return new Color(0.28f, 0.62f, 1f, 1f);
                default: return new Color(0.9f, 0.9f, 0.9f, 1f);
            }
        }

        private void OnDestroy()
        {
            if (_sharedLineMaterial != null) Destroy(_sharedLineMaterial);
        }
    }
}
