using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A toy root's <b>identity, made of the toy's own content</b>: a CORE (what you are right
    /// now) ringed by SATELLITES (what a pass would offer you). One rig for every toy root, so the
    /// toybox stops being six tinted spheres with names floating over them and each station can
    /// eventually drop its text label.
    ///
    /// The grammar is the <see cref="SwapToySetCoordinator{T}"/> semantic - "you are this, these
    /// are the others" - lifted onto the roots that don't unfold. It is not a new shape word: the
    /// reserved atoms (cone = turns the trail ON, jack = turns it OFF, ring = crossing commits a
    /// choice) are untouched, and an emblem is deliberately a <i>tilted ring of discrete objects
    /// around a hub</i>, never a torus and never square across your flight path.
    ///
    /// <b>Nothing is built on the spawn frame.</b> <see cref="Attach"/> creates empty holders only;
    /// a toybox-wide <see cref="ToyEmblemStreamer"/> fills one slot per frame, so the whole emblem
    /// assembles INSIDE the toy's bloom-in and no piece ever pops into existence. The core lands
    /// first for every toy (breadth-first), so identity arrives before decoration.
    ///
    /// NOTE (no compile-time guard): "assembles inside the growth" holds because
    /// <c>Toy.bloomDuration</c> (1.2s) exceeds the whole toybox's stream (~0.3s). Drop the bloom
    /// below ~0.5s and the emblem visibly assembles - a continuity-law violation.
    /// </summary>
    public sealed class ToyEmblem : MonoBehaviour
    {
        // ── The single source of emblem geometry. Everything is a multiple of the toy's own body
        // radius (R = 22 in Menu_Main), because the toybox places toys at menu scale and anything
        // authored in raw units disappears inside the body. Outer extent = (1.18 + 0.34) x R
        // = 33.4 < the 42u trigger radius and < the 41.8u label height: the emblem never reads
        // bigger than its own interaction volume, and never collides with the label.
        public const float CoreRadiusBodies = 0.46f;
        public const float OrbitRadiusBodies = 1.18f;
        public const float SatelliteRadiusBodies = 0.34f;
        public const float HaloTiltDegrees = 32f;
        public const float OrbitRateLerpSeconds = 0.8f;
        public const float LiveTickSeconds = 0.5f;
        public const float ItemBloomSeconds = 0.5f;
        public const float PlaceholderFadeSeconds = 0.4f;

        /// <summary>
        /// What a toy's emblem is made of. Slot 0 is the CORE; slots 1..N are the satellites.
        /// </summary>
        public interface IEmblemSource
        {
            /// <summary>Satellites to orbit the core. 0 is legal (the Cell Selector is core-only).</summary>
            int SatelliteCount { get; }

            /// <summary>
            /// Build slot <paramref name="slot"/> as a child of <paramref name="holder"/>, fitted to
            /// <paramref name="radius"/>. Builders that go through <see cref="ToyModelBuilder"/> MUST
            /// build unparented and <c>SetParent(holder, false)</c> afterwards - its fit step reads
            /// WORLD bounds and assumes an origin-anchored, unrotated, unit-scale root.
            ///
            /// Paint with <paramref name="shared"/> so the emblem owns exactly one material (a
            /// builder that supplies its own - prism materials, vertex-coloured lines - ignores it).
            /// Set <paramref name="heavy"/> when the build was expensive so the streamer leaves a
            /// clear frame after it. Returning false leaves the slot EMPTY, which is legal.
            /// </summary>
            bool TryBuildSlot(int slot, Transform holder, float radius, Material shared, out bool heavy);

            /// <summary>False = this emblem never changes. A CHANGED key rebuilds every slot.</summary>
            bool TryGetLiveKey(out object key);

            /// <summary>False = never re-tint. A changed tint rewrites the ONE owned material.</summary>
            bool TryGetLiveTint(out Color tint);

            /// <summary>
            /// False when this source paints its own items (real prism materials, vertex-coloured
            /// lines) and ignores the <c>shared</c> argument. The emblem then never allocates a
            /// material for it - the getter runs up to three <c>Shader.Find</c> calls, and three of
            /// the five sources would pay that for nothing.
            /// </summary>
            bool UsesSharedMaterial { get; }
        }

        IEmblemSource _source;
        float _bodyRadius;

        Transform _halo;
        ToyIdleSpin _haloSpin;
        Transform[] _holders;
        bool[] _built;
        int _nextSlot;

        GameObject _placeholder;
        bool _placeholderRetired;

        Material _sharedMaterial;
        readonly List<Mesh> _ownedMeshes = new();

        ToyEmblemStreamer _streamer;

        float _orbitRate;
        float _orbitRateTarget;
        float _orbitRateVelocity;

        float _liveTimer;
        object _liveKey;
        bool _hasLiveKey;
        Color _liveTint;
        bool _hasLiveTint;

        /// <summary>
        /// The one material every model-based item in this emblem wears. Allocated on FIRST use -
        /// never on the spawn frame, so no <c>Shader.Find</c> lands there - and destroyed with the
        /// emblem, unlike the per-call materials the colour overloads leak.
        /// </summary>
        public Material SharedMaterial =>
            _sharedMaterial ? _sharedMaterial : _sharedMaterial = ToyModelBuilder.BuildPreviewMaterial(_liveTint);

        /// <summary>Slots still waiting to be built.</summary>
        internal bool HasPending => _holders != null && _nextSlot < _holders.Length;

        /// <summary>
        /// Rig an emblem onto <paramref name="owner"/>: rescale the factory's sphere down to a
        /// fail-soft placeholder core, hang a tilted halo with one holder per slot, and queue the
        /// slots with the toybox streamer. Builds no geometry.
        /// </summary>
        public static ToyEmblem Attach(Toy owner, float bodyRadius, IEmblemSource source, float orbitRate,
            Color tint)
        {
            if (!owner || source == null) return null;

            var emblem = owner.gameObject.AddComponent<ToyEmblem>();
            emblem._source = source;
            emblem._bodyRadius = bodyRadius > 0.01f ? bodyRadius : 20f;
            emblem._orbitRate = emblem._orbitRateTarget = orbitRate;
            emblem._liveTint = tint;
            emblem.Build();
            return emblem;
        }

        void Build()
        {
            // The factory's sphere stays as a small placeholder core: a total build failure then
            // degrades to "a smaller ball", never to an empty toy. It is retired (cross-faded) the
            // moment a real core lands. This is why ToyFactory.CreateRoot never had to change.
            _placeholder = FindPlaceholder();
            if (_placeholder)
                _placeholder.transform.localScale = Vector3.one * (_bodyRadius * CoreRadiusBodies * 2f);

            var halo = new GameObject("Emblem");
            _halo = halo.transform;
            _halo.SetParent(transform, false);
            // Tilted so the orbit reads as an ellipse from any approach - a ring seen edge-on is
            // invisible, and a ring seen square-on is the reserved "fly through me" portal.
            _halo.localRotation = Quaternion.Euler(-HaloTiltDegrees, 0f, 0f);
            _haloSpin = halo.AddComponent<ToyIdleSpin>();
            _haloSpin.Configure(Vector3.forward, _orbitRate);

            int satellites = Mathf.Max(0, _source.SatelliteCount);
            _holders = new Transform[1 + satellites];
            _built = new bool[_holders.Length];

            _holders[0] = NewHolder("Core", Vector3.zero);
            float orbit = _bodyRadius * OrbitRadiusBodies;
            for (int i = 0; i < satellites; i++)
            {
                // First satellite at 6 o'clock, so 12 o'clock stays clear under the label.
                float theta = (-90f + 360f * i / satellites) * Mathf.Deg2Rad;
                _holders[i + 1] = NewHolder($"Satellite{i}",
                    new Vector3(Mathf.Cos(theta), Mathf.Sin(theta), 0f) * orbit);
            }

            // Holders start collapsed: an item lands into a zero-scale holder and blooms, so
            // nothing ever appears at full size for a frame (continuity of existence).
            foreach (var holder in _holders) holder.localScale = Vector3.zero;

            _streamer = GetComponentInParent<ToyEmblemStreamer>();
            if (_streamer) _streamer.Register(this);
        }

        Transform NewHolder(string holderName, Vector3 localPosition)
        {
            var go = new GameObject(holderName);
            go.transform.SetParent(_halo, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        GameObject FindPlaceholder()
        {
            var body = transform.Find("Body");
            if (!body) return null;
            var sphere = body.Find("Sphere");
            return sphere ? sphere.gameObject : null;
        }

        // ── Streaming ────────────────────────────────────────────────────────

        /// <summary>
        /// Build the next pending slot. One slot per call, so the streamer can spread the whole
        /// toybox across frames. Returns false when there was nothing left to build.
        /// </summary>
        internal bool TryBuildNextSlot(out bool heavy)
        {
            heavy = false;
            if (_holders == null || _nextSlot >= _holders.Length) return false;

            int slot = _nextSlot++;
            var holder = _holders[slot];
            if (!holder) return true; // consumed; nothing to build into

            float radius = _bodyRadius * (slot == 0 ? CoreRadiusBodies : SatelliteRadiusBodies);
            bool built = false;
            try
            {
                // Only sources that actually paint with it get one - see UsesSharedMaterial.
                var shared = _source.UsesSharedMaterial ? SharedMaterial : null;
                built = _source.TryBuildSlot(slot, holder, radius, shared, out heavy);
            }
            catch (System.Exception e)
            {
                // One toy's emblem must never take the toybox stream down with it.
                Debug.LogException(e, this);
            }

            _built[slot] = built;
            if (built)
            {
                EnsureSpin(holder);
                BloomHolder(holder);
                if (slot == 0) RetirePlaceholder();
            }

            if (!HasPending) OnStreamComplete();
            return true;
        }

        /// <summary>
        /// Give the item its own slow turn - but only if its builder didn't already turntable it,
        /// so nothing is double-spun.
        /// </summary>
        static void EnsureSpin(Transform holder)
        {
            if (holder.GetComponentInChildren<ToyIdleSpin>(true)) return;
            holder.gameObject.AddComponent<ToyIdleSpin>().Configure(Vector3.up, 14f);
        }

        static void BloomHolder(Transform holder) =>
            ToyFactory.ScaleInFromZero(holder, ItemBloomSeconds).Forget();

        void RetirePlaceholder()
        {
            if (_placeholderRetired || !_placeholder) return;
            _placeholderRetired = true;
            // Cross-fade, not a swap: the real core blooms in while the stand-in shrinks away.
            // WITHDRAWN, not destroyed - a later rebuild can produce nothing (pick the world you
            // are in, then pick the environment-free one) and the fallback below has to have
            // something left to bring back, or the toy becomes an invisible trigger with a label.
            AnimatePlaceholder(0f, disableAtEnd: true).Forget();
        }

        void OnStreamComplete()
        {
            foreach (bool b in _built)
                if (b) return;

            // Nothing at all could be built (no theme, no prefabs, an environment-free config).
            // Bring the stand-in back rather than leaving an empty root. Do NOT delete this branch
            // as redundant - it is the only fallback.
            RestorePlaceholder();
        }

        void RestorePlaceholder()
        {
            if (!_placeholder) return;
            _placeholderRetired = false;
            _placeholder.SetActive(true);

            // A core-only toy with nothing to show stays SMALL - that is the Cell Selector at boot,
            // where the cell is environment-free and "a small bare core" is the honest read (you
            // are in the empty one). A toy that expected satellites falls all the way back to the
            // plain body it had before emblems existed.
            float target = _source.SatelliteCount > 0
                ? _bodyRadius * 2f
                : _bodyRadius * CoreRadiusBodies * 2f;
            AnimatePlaceholder(target, disableAtEnd: false).Forget();
        }

        /// <summary>
        /// Ease the placeholder to a size (and optionally park it), never snap it - it is a body
        /// the player can already see, so the continuity law applies to its resize too.
        ///
        /// Self-superseding: it captures <see cref="_placeholderRetired"/> and bails the moment
        /// that flips, so a retire tween still in flight can never park a placeholder that a
        /// failed rebuild has just brought back.
        /// </summary>
        async UniTaskVoid AnimatePlaceholder(float targetDiameter, bool disableAtEnd)
        {
            var go = _placeholder;
            if (!go) return;

            bool epoch = _placeholderRetired;
            var ct = this.GetCancellationTokenOnDestroy();
            Vector3 start = go.transform.localScale;
            Vector3 end = Vector3.one * targetDiameter;

            float elapsed = 0f;
            while (elapsed < PlaceholderFadeSeconds)
            {
                if (!go || _placeholderRetired != epoch) return;
                elapsed += Time.unscaledDeltaTime;
                go.transform.localScale = Vector3.LerpUnclamped(start, end,
                    Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / PlaceholderFadeSeconds)));
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            if (!go || _placeholderRetired != epoch) return;
            go.transform.localScale = end;
            if (disableAtEnd) go.SetActive(false);
        }

        // ── Liveness ─────────────────────────────────────────────────────────

        void Update()
        {
            if (!Mathf.Approximately(_orbitRate, _orbitRateTarget))
            {
                _orbitRate = Mathf.SmoothDamp(_orbitRate, _orbitRateTarget, ref _orbitRateVelocity,
                    OrbitRateLerpSeconds, Mathf.Infinity, Time.unscaledDeltaTime);
                if (Mathf.Abs(_orbitRate - _orbitRateTarget) < 0.05f) _orbitRate = _orbitRateTarget;
                if (_haloSpin) _haloSpin.Configure(Vector3.forward, _orbitRate);
            }

            _liveTimer += Time.unscaledDeltaTime;
            if (_liveTimer < LiveTickSeconds) return;
            _liveTimer = 0f;

            // Polled rather than event-subscribed on purpose: the sources' own guards already know
            // when their state is mid-swap/unreadable, and there is no config-changed event to
            // subscribe to for the cell. Half a second is imperceptible for an icon.
            if (_source.TryGetLiveKey(out object key))
            {
                if (!_hasLiveKey || !Equals(key, _liveKey))
                {
                    bool first = !_hasLiveKey;
                    _hasLiveKey = true;
                    _liveKey = key;
                    if (!first) Rebuild();
                }
            }

            if (_source.TryGetLiveTint(out Color tint))
            {
                if (!_hasLiveTint || tint != _liveTint)
                {
                    _hasLiveTint = true;
                    Retint(tint);
                }
            }
        }

        /// <summary>Ease the halo's orbit to a new rate (the Wanderway's flow state). Never a snap.</summary>
        public void SetOrbitRate(float degreesPerSecond) => _orbitRateTarget = degreesPerSecond;

        /// <summary>Repaint the emblem's own material. Never touches a shared/theme asset.</summary>
        public void Retint(Color color)
        {
            _liveTint = color;
            if (!_sharedMaterial) return;
            _sharedMaterial.color = color;
            if (_sharedMaterial.HasProperty("_BaseColor")) _sharedMaterial.SetColor("_BaseColor", color);
            if (_sharedMaterial.HasProperty("_EmissionColor")) _sharedMaterial.SetColor("_EmissionColor", color * 0.6f);
        }

        /// <summary>Withdraw every item and re-queue every slot (the live key changed).</summary>
        public void Rebuild()
        {
            if (_holders == null) return;

            foreach (var holder in _holders)
            {
                if (!holder) continue;
                for (int i = holder.childCount - 1; i >= 0; i--)
                {
                    // Re-parent to the halo BEFORE the scale-out: the holder is about to be
                    // re-bloomed from zero for its replacement, and a still-parented item would be
                    // multiplied to nothing on that frame - an instant disappearance, which is the
                    // continuity law's one prohibition. Detached, it shrinks away properly.
                    var child = holder.GetChild(i);
                    child.SetParent(_halo, worldPositionStays: true);
                    ToyFactory.ScaleOutAndDestroy(child.gameObject, ItemBloomSeconds).Forget();
                }
                // NOTE: the holder's own ToyIdleSpin is deliberately LEFT ALONE. Destroy() is
                // deferred to end of frame, so a destroyed-this-frame spin is still found by
                // EnsureSpin's GetComponentInChildren - the replacement item would be skipped and
                // then the spin would die, leaving a permanently frozen core. EnsureSpin already
                // no-ops when a spin is present, so keeping it is both correct and cheaper.
            }

            // Deferred past the fade: these meshes are still being rendered by the items that are
            // shrinking away. (No shipped source both owns meshes and rebuilds, but the ordering
            // must not be a trap for the next one that does.)
            ReleaseOwnedMeshesAfter(ItemBloomSeconds).Forget();
            for (int i = 0; i < _built.Length; i++) _built[i] = false;
            _nextSlot = 0;
            if (_streamer) _streamer.RequestRefresh();
        }

        /// <summary>
        /// Hand a mesh the emblem CREATED to the emblem, so it dies with it. Meshes owned by
        /// someone else (a toy's own miniature cache) must NOT be passed here - they would be
        /// destroyed twice.
        /// </summary>
        public void Own(Mesh mesh)
        {
            if (mesh) _ownedMeshes.Add(mesh);
        }

        void ReleaseOwnedMeshes()
        {
            foreach (var mesh in _ownedMeshes)
                if (mesh) Destroy(mesh);
            _ownedMeshes.Clear();
        }

        /// <summary>Hand the current owned set to a timer, so the items rendering them can finish fading.</summary>
        async UniTaskVoid ReleaseOwnedMeshesAfter(float seconds)
        {
            var stale = new List<Mesh>(_ownedMeshes);
            _ownedMeshes.Clear();
            await UniTask.Delay(Mathf.RoundToInt(seconds * 1000f), ignoreTimeScale: true,
                cancellationToken: this.GetCancellationTokenOnDestroy());
            foreach (var mesh in stale)
                if (mesh) Destroy(mesh);
        }

        void OnDestroy()
        {
            ReleaseOwnedMeshes();
            if (_sharedMaterial) Destroy(_sharedMaterial);
            _sharedMaterial = null;
        }
    }
}
