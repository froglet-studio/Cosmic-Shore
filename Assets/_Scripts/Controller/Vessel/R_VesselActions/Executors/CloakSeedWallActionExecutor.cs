using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using CosmicShore.Gameplay;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.UI;
namespace CosmicShore.Gameplay
{
    public sealed class CloakSeedWallActionExecutor : ShipActionExecutorBase
    {
        [Header("Scene Refs")] 
        [SerializeField] private SkinnedMeshRenderer shipRenderer;
        [SerializeField] private SeedAssemblerActionExecutor seedAssembler;

        [Header("Events")] 
        [SerializeField] private ScriptableEventNoParam OnMiniGameTurnEnd;

        // State
        private IVesselStatus _status;
        private VesselPrismController _controller;

        private bool _isRunning;
        private float _cooldownEndTime;
        private CancellationTokenSource _runCts;

        // SO used during active cloak
        private CloakSeedWallActionSO _activeSo;

        // Ship restore cache
        private Material[] _shipOriginalShared;
        private bool _shipPrevEnabled;
        private bool _shipCloaked;
        private GameObject _serpentGhost;   // the illusion left at the cloak point
        private GameObject _revealGlow;     // the glow the hull comes back through

        private static readonly int GhostColorId = Shader.PropertyToID("_GhostColor");
        private static readonly int FadeId       = Shader.PropertyToID("_Fade");
        private static readonly int FlareId      = Shader.PropertyToID("_Flare");
        // BlockGraph's own alpha: below 1 it draws through the prism screen-door dither, at 0 not at
        // all (PrismOcclusionCorridor.hlsl, "THE prism transparency mechanism").
        private static readonly int AlphaId      = Shader.PropertyToID("_Alpha");

        // Runtime clones of the ghost material (CloakGhost), made once and destroyed with the
        // executor: one the pilot's hull wears, one the reveal glow wears.
        private Material _hullGhost;
        private Material _revealGhost;

        // One clone per trail material, kept for the executor's life so a cloak does not register
        // a fresh material with the prism renderer every time. Every clone's _Alpha is driven
        // together, so the whole trail fades as one.
        private readonly Dictionary<Material, Material> _trailClones = new();
        private float _trailAlpha = 1f;

        private sealed class TrackedPrism
        {
            public Prism Prism;
            public MeshRenderer Renderer;
            public Material Source;
            public Material Clone;
        }

        private readonly List<TrackedPrism> _cloakedPrisms = new();

        // The seed this cloak laid: the illusion collapses into it when the cloak ends.
        private Prism _seed;

        /// <summary>
        /// Is this vessel the one the person at THIS machine is flying? The cloak runs on every
        /// peer (the press is replicated), and only that pilot should still see their hull and
        /// trail. Was hard-wired to true, which gave every viewer the pilot's view and the pilot
        /// the invisible one.
        /// </summary>
        private bool IsLocalUser => _status?.Player != null && _status.Player.IsLocalUser;

        /// <summary>How much of the trail stays drawn once cloaked: a ghost of it for the pilot,
        /// nothing for everyone else.</summary>
        private float TrailCloakedAlpha => _activeSo && IsLocalUser ? _activeSo.PilotTrailAlpha : 0f;

        // ---------------- Lifecycle ----------------

        void OnEnable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDestroy()
        {
            if (_hullGhost) Destroy(_hullGhost);
            if (_revealGhost) Destroy(_revealGhost);
            foreach (var clone in _trailClones.Values)
                if (clone) Destroy(clone);
            _trailClones.Clear();
        }

        void OnDisable()
        {
            End();
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised -= OnTurnEndOfMiniGame;
            if (_controller != null) _controller.OnBlockSpawned -= HandleBlockSpawned;
        }

        void OnTurnEndOfMiniGame() => End();

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status     = shipStatus;
            _controller = _status?.VesselPrismController;

            if (_controller != null)
                _controller.OnBlockSpawned += HandleBlockSpawned;

            if (seedAssembler != null)
                seedAssembler.Initialize(_status);
        }

        // ---------------- API ----------------

        public void Toggle(CloakSeedWallActionSO so, IVesselStatus status)
        {
            if (!so || status == null) return;
            if (_isRunning || Time.time < _cooldownEndTime) return;

            _activeSo = so;

            if (!seedAssembler.StartSeed(so.SeedWallSo, status))
                return;

            _seed = seedAssembler.ActiveSeedBlock;
            seedAssembler.BeginBonding();

            _trailAlpha = 1f;
            BeginCloakVisuals();      // the illusion, and the pilot's ghost hull
            CloakExistingPrisms();    // all current trail blocks (they fade in RunAsync)

            _runCts?.Cancel();
            _runCts?.Dispose();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            RunAsync(so, _runCts.Token).Forget();
        }


        public void End()
        {
            if (_runCts != null)
            {
                try { _runCts.Cancel(); } catch { }
                _runCts.Dispose();
                _runCts = null;
            }

            // Always restore
            DestroyRevealGlow();
            RestoreShipImmediate();
            RestoreAllPrismsImmediate();
            seedAssembler?.StopSeedCompletely();

            _activeSo  = null;
            _seed      = null;
            _isRunning = false;
        }

        // ---------------- Run ----------------

        private async UniTaskVoid RunAsync(CloakSeedWallActionSO so, CancellationToken ct)
        {
            _isRunning = true;
            _cooldownEndTime = Time.time + Mathf.Max(0.01f, so.CooldownSeconds);

            try
            {
                // In: the trail dissolves and the pilot's hull settles from a flash into its ghost.
                await Blend(so.FadeInSeconds, ApplyCloakIn, ct);

                await UniTask.Delay(TimeSpan.FromSeconds(Mathf.Max(0f, so.CooldownSeconds - so.FadeInSeconds)),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    ct);

                // The illusion collapses into the seed rather than vanishing, while everything is
                // still cloaked; then the hull, the trail and the seed come back together.
                await MorphIllusionIntoSeed(so, ct);
                await Reveal(so, ct);
            }
            catch (OperationCanceledException)
            {
                // End() already restored everything, and a new cloak may own the state by the
                // time this continuation runs: touch nothing.
                return;
            }

            _isRunning = false;

            DestroyRevealGlow();
            RestoreShipImmediate();
            RestoreAllPrismsImmediate();
            seedAssembler?.StopSeedCompletely();

            _activeSo = null;
            _seed = null;

            _runCts?.Dispose();
            _runCts = null;
        }

        /// <summary>Runs <paramref name="apply"/> with 0..1 over <paramref name="seconds"/>, once
        /// per frame, ending on exactly 1.</summary>
        private static async UniTask Blend(float seconds, Action<float> apply, CancellationToken ct)
        {
            if (seconds <= 0f)
            {
                apply(1f);
                return;
            }

            float start = Time.time;
            while (true)
            {
                float u = Mathf.Clamp01((Time.time - start) / seconds);
                apply(u);
                if (u >= 1f) return;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        private static float Smooth(float u) => u * u * (3f - 2f * u);

        private void ApplyCloakIn(float u)
        {
            if (!_activeSo) return;
            float e = Smooth(u);
            SetTrailAlpha(Mathf.Lerp(1f, TrailCloakedAlpha, e));
            if (_shipCloaked && _hullGhost && IsLocalUser)
                _hullGhost.SetFloat(FlareId, _activeSo.TransitionFlare * (1f - e));
        }

        /// <summary>
        /// The cloak ends without a cut. The pilot's ghost brightens into a flash; under it the
        /// hull takes its own materials back (for everyone else it reappears), wrapped in a glow
        /// of the same ghost that then fades off it; meanwhile the trail fills back in through
        /// the dither, ending exactly solid, so swapping its materials back changes nothing.
        /// </summary>
        private async UniTask Reveal(CloakSeedWallActionSO so, CancellationToken ct)
        {
            // Fraction of the reveal spent building the flash before the hull swaps back.
            const float SwapAt = 0.4f;
            bool swapped = false;
            float from = TrailCloakedAlpha;

            await Blend(so.FadeOutSeconds, u =>
            {
                SetTrailAlpha(Mathf.Lerp(from, 1f, Smooth(u)));

                if (!swapped)
                {
                    if (u < SwapAt)
                    {
                        if (_hullGhost && IsLocalUser)
                            _hullGhost.SetFloat(FlareId, so.TransitionFlare * Smooth(u / SwapAt));
                        return;
                    }

                    swapped = true;
                    RestoreShipImmediate();
                    SpawnRevealGlow(so);
                    ReplaySeedBloom();
                }

                float v = Smooth(Mathf.InverseLerp(SwapAt, 1f, u));
                if (_revealGhost)
                {
                    _revealGhost.SetFloat(FlareId, so.TransitionFlare * (1f - v));
                    _revealGhost.SetFloat(FadeId, 1f - v);
                }
            }, ct);

            DestroyRevealGlow();
            RestoreAllPrismsImmediate();
        }

        /// <summary>
        /// Carry the illusion onto the seed: it travels to the seed, turns to the seed's pose and
        /// shrinks to the seed's size over <see cref="CloakSeedWallActionSO.IllusionMorphSeconds"/>
        /// (smoothstep), so the decoy visibly becomes the prism. Nothing may vanish in place.
        /// </summary>
        private async UniTask MorphIllusionIntoSeed(CloakSeedWallActionSO so, CancellationToken ct)
        {
            var seed = _seed;
            float seconds = so ? so.IllusionMorphSeconds : 0f;
            if (!_serpentGhost || !seed || seed.destroyed || seconds <= 0f) return;

            var t = _serpentGhost.transform;
            Vector3 p0 = t.position;
            Quaternion r0 = t.rotation;
            Vector3 s0 = t.localScale;

            float ghostSize = _serpentGhost.TryGetComponent(out MeshRenderer gr) ? gr.bounds.size.magnitude : 0f;
            float seedSize = seed.transform.lossyScale.magnitude;
            Vector3 s1 = ghostSize > 0.0001f ? s0 * (seedSize / ghostSize) : s0 * 0.1f;

            float start = Time.time;
            while (true)
            {
                if (!_serpentGhost || !seed || seed.destroyed) return;
                float u = Mathf.Clamp01((Time.time - start) / seconds);
                float e = u * u * (3f - 2f * u);
                var st = seed.transform;
                t.SetPositionAndRotation(Vector3.Lerp(p0, st.position, e), Quaternion.Slerp(r0, st.rotation, e));
                t.localScale = Vector3.Lerp(s0, s1, e);
                if (u >= 1f) return;
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
        }

        /// <summary>The seed's stellation blooms again as the illusion lands in it: the moment
        /// the decoy becomes a super-shielded prism, seen by everyone the cloak hid it from.</summary>
        private void ReplaySeedBloom()
        {
            var seed = _seed;
            if (!seed || seed.destroyed || seed.prismProperties is not { IsSuperShielded: true }) return;
            seed.ActivateSuperShield();
        }

        // ---------------- Cloak visuals ----------------

        private void BeginCloakVisuals()
        {
            if (!shipRenderer) return;
            _shipOriginalShared = shipRenderer.sharedMaterials;
            _shipPrevEnabled    = shipRenderer.enabled;
            _shipCloaked        = true;
            SpawnSerpentGhost();

            if (IsLocalUser)
            {
                // The pilot keeps their hull as a glowing outline to steer by, starting from a
                // flash that ApplyCloakIn settles, so the material swap is not a cut.
                var ghost = GhostClone(ref _hullGhost, " (Pilot)");
                if (ghost)
                {
                    ghost.SetFloat(FadeId, 1f);
                    ghost.SetFloat(FlareId, _activeSo.TransitionFlare);
                    ApplySingleMaterialAcrossRenderer(shipRenderer, ghost);
                }
                shipRenderer.enabled = true;
            }
            else
            {
                // The illusion stands exactly where the hull was, so hiding it is invisible.
                shipRenderer.enabled = false;
            }
        }

        /// <summary>The runtime clone of the ghost material in <paramref name="slot"/>, made on
        /// first use and tinted with this pilot's domain colour every time.</summary>
        private Material GhostClone(ref Material slot, string suffix)
        {
            var source = _activeSo ? _activeSo.GhostShipMaterial : null;
            if (!source) return null;
            if (!slot) slot = new Material(source) { name = source.name + suffix };

            var colorSet = _activeSo.GameData ? _activeSo.GameData.ThemeManagerData?.ColorSet : null;
            if (colorSet != null && _status != null)
                slot.SetColor(GhostColorId, colorSet.GetDomainSignalColor(_status.Domain));
            return slot;
        }

        /// <summary>
        /// A copy of the hull, in the ghost material, riding the hull for the second half of the
        /// reveal: the hull comes back under it and its glow fades off. A baked copy rather than
        /// an extra material slot because the hull has several submeshes and Unity draws an extra
        /// slot over the last one only.
        /// </summary>
        private void SpawnRevealGlow(CloakSeedWallActionSO so)
        {
            DestroyRevealGlow();
            if (!shipRenderer || !shipRenderer.enabled) return;

            var ghost = GhostClone(ref _revealGhost, " (Reveal)");
            if (!ghost) return;
            ghost.SetFloat(FadeId, 1f);
            ghost.SetFloat(FlareId, so.TransitionFlare);

            // Unscaled, in the renderer's own space, so parenting it to the renderer with an
            // identity local transform puts it exactly over the hull and lets it follow.
            var baked = new Mesh();
            shipRenderer.BakeMesh(baked, false);

            _revealGlow = new GameObject("SerpentRevealGlow");
            _revealGlow.transform.SetParent(shipRenderer.transform, false);
            _revealGlow.AddComponent<MeshFilter>().sharedMesh = baked;
            var mr = _revealGlow.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            var mats = new Material[Mathf.Max(1, baked.subMeshCount)];
            for (int i = 0; i < mats.Length; i++) mats[i] = ghost;
            mr.sharedMaterials = mats;
        }

        /// <summary>Destroys the reveal glow and its baked mesh (a runtime asset Unity never frees
        /// with the GameObject). The ghost material is the executor's and stays.</summary>
        private void DestroyRevealGlow()
        {
            if (!_revealGlow) return;
            if (_revealGlow.TryGetComponent(out MeshFilter mf) && mf.sharedMesh) Destroy(mf.sharedMesh);
            Destroy(_revealGlow);
            _revealGlow = null;
        }

        private void HandleBlockSpawned(Prism block)
        {
            if (!_isRunning || !block) return;

            // Optional: keep these blocks alive for entire cooldown
            var remaining = _cooldownEndTime - Time.time;
            if (remaining > 0f)
            {
                var original = block.waitTime;
                var target   = Mathf.Max(original, remaining);
                if (!Mathf.Approximately(original, target))
                    block.waitTime = target;
            }

            CloakOnePrism(block);
        }


        /// <summary>
        /// Destroys the ghost AND the assets it owns. The baked mesh and the material clones are
        /// runtime-created assets: Unity never frees them with the GameObject, so destroying only
        /// the GameObject leaked one skinned-hull mesh plus every hull material per cloak, per
        /// Serpent, on every peer (Toggle runs everywhere through the press RPC).
        /// </summary>
        private void DestroySerpentGhost()
        {
            if (!_serpentGhost) return;

            if (_serpentGhost.TryGetComponent(out MeshFilter mf) && mf.sharedMesh)
                Destroy(mf.sharedMesh);
            if (_serpentGhost.TryGetComponent(out MeshRenderer mr))
                foreach (var m in mr.sharedMaterials)
                    if (m) Destroy(m);

            Destroy(_serpentGhost);
            _serpentGhost = null;
        }

        private void SpawnSerpentGhost()
        {
            if (!shipRenderer) return;

            DestroySerpentGhost();

            var baked = new Mesh();
            shipRenderer.BakeMesh(baked, true);

            _serpentGhost = new GameObject("SerpentGhost");
            var mf = _serpentGhost.AddComponent<MeshFilter>();
            var mr = _serpentGhost.AddComponent<MeshRenderer>();
            mf.sharedMesh = baked;

            var live = shipRenderer.sharedMaterials;
            if (live is { Length: > 0 })
            {
                var clones = new Material[live.Length];
                for (int i = 0; i < live.Length; i++)
                    clones[i] = live[i] ? new Material(live[i]) : null;
                mr.materials = clones;
            }

            _serpentGhost.transform.SetPositionAndRotation(
                shipRenderer.transform.position,
                shipRenderer.transform.rotation
            );
            _serpentGhost.transform.localScale = shipRenderer.transform.lossyScale;
            _serpentGhost.SetActive(true);
        }
        
        private void CloakExistingPrisms()
        {
            if (_controller == null) return;

            void CloakTrail(Trail trail)
            {
                if (trail?.TrailList == null) return;
                foreach (var p in trail.TrailList)
                    if (p != null)
                        CloakOnePrism(p);
            }

            CloakTrail(_controller.Trail);

            // Optional second trail (like your old code)
            var trail2Field = typeof(VesselPrismController)
                .GetField("Trail2", BindingFlags.Instance | BindingFlags.NonPublic);
            if (trail2Field?.GetValue(_controller) is Trail t2)
                CloakTrail(t2);
        }


        // ---------------- Restore ----------------

        private void RestoreShipImmediate()
        {
            DestroySerpentGhost();

            // Only a hull this cloak changed: End() also runs at a turn end that had no cloak,
            // and must not write a never-captured "previous" state onto the hull.
            if (!shipRenderer || !_shipCloaked) return;

            shipRenderer.enabled = _shipPrevEnabled;

            if (_shipOriginalShared != null)
                shipRenderer.sharedMaterials = _shipOriginalShared;

            _shipOriginalShared = null;
            _shipCloaked = false;
        }

        /// <summary>Gives every cloaked prism its own material back, if it still wears the clone
        /// (a prism that changed state mid-cloak already re-bound its own).</summary>
        private void RestoreAllPrismsImmediate()
        {
            foreach (var t in _cloakedPrisms)
            {
                if (!t.Prism || !t.Renderer || t.Renderer.sharedMaterial != t.Clone) continue;
                t.Renderer.sharedMaterial = t.Source;
                t.Prism.SyncRenderMaterial();
            }

            _cloakedPrisms.Clear();
            _trailAlpha = 1f;
        }

        // ---------------- Trail ----------------

        private void SetTrailAlpha(float alpha)
        {
            _trailAlpha = alpha;
            foreach (var pair in _trailClones)
                if (pair.Key && pair.Value)
                    pair.Value.SetFloat(AlphaId, pair.Key.GetFloat(AlphaId) * alpha);
        }

        /// <summary>
        /// Puts a trail prism on the cloak clone of its material, at the trail's current alpha.
        /// The pilot's trail fades to <see cref="CloakSeedWallActionSO.PilotTrailAlpha"/>, a ghost
        /// of itself in its own colours (it used to be darkened, which read as a dead trail);
        /// everyone else's fades to nothing.
        /// </summary>
        private void CloakOnePrism(Prism prism)
        {
            if (!prism || !_activeSo) return;
            if (!prism.TryGetComponent(out MeshRenderer mr)) return;

            var source = mr.sharedMaterial;
            if (!source || _trailClones.ContainsValue(source) || !source.HasProperty(AlphaId)) return;

            if (!_trailClones.TryGetValue(source, out var clone) || !clone)
            {
                clone = new Material(source) { name = source.name + " (Cloak)" };
                _trailClones[source] = clone;
            }
            clone.SetFloat(AlphaId, source.GetFloat(AlphaId) * _trailAlpha);

            mr.sharedMaterial = clone;
            prism.SyncRenderMaterial();
            _cloakedPrisms.Add(new TrackedPrism { Prism = prism, Renderer = mr, Source = source, Clone = clone });
        }

        private static void ApplySingleMaterialAcrossRenderer(Renderer r, Material mat)
        {
            if (!r || !mat) return;
            var count = r.sharedMaterials is { Length: > 0 }
                ? r.sharedMaterials.Length
                : 1;
            var arr = new Material[count];
            for (int i = 0; i < count; i++) arr[i] = mat;
            r.sharedMaterials = arr;
        }
    }
}
