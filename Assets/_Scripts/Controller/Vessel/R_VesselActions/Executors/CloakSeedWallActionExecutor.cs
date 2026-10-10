using System;
using System.Collections.Generic;
using System.Linq;
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
        private GameObject _serpentGhost;

        // Prism alpha override (same pattern as old code)
        private static readonly int _ColorId     = Shader.PropertyToID("_Color");
        private static readonly int _BaseColorId = Shader.PropertyToID("_BaseColor");

        private sealed class TrackedPrism
        {
            public Prism Prism;
            public MaterialPropertyAnimator Animator;
            // The pilot's own view: dimmed, not hidden (see CloakOnePrism).
            public bool Shaded;
        }

        private readonly List<TrackedPrism> _cloakedPrisms = new();


        private readonly List<TrackedPrism> _spawnedDuringCloak = new();

        // The seed this cloak laid: the illusion collapses into it when the cloak ends.
        private Prism _seed;

        // The pilot's own translucent hull: a runtime clone of the ghost material at
        // PilotGhostAlpha, made once and destroyed with the executor.
        private Material _pilotGhostMaterial;

        /// <summary>
        /// Is this vessel the one the person at THIS machine is flying? The cloak runs on every
        /// peer (the press is replicated), and only that pilot should still see their hull and
        /// trail. Was hard-wired to true, which gave every viewer the pilot's view and the pilot
        /// the invisible one.
        /// </summary>
        private bool IsLocalUser => _status?.Player != null && _status.Player.IsLocalUser;

        // ---------------- Lifecycle ----------------

        void OnEnable()
        {
            if (OnMiniGameTurnEnd) OnMiniGameTurnEnd.OnRaised += OnTurnEndOfMiniGame;
        }

        void OnDestroy()
        {
            if (_pilotGhostMaterial) Destroy(_pilotGhostMaterial);
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

            BeginCloakVisuals();      // ship ghost
            CloakExistingPrisms();    // all current trail blocks

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
            _spawnedDuringCloak.Clear();

            _cooldownEndTime = Time.time + Mathf.Max(0.01f, so.CooldownSeconds);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(so.CooldownSeconds),
                    DelayType.DeltaTime,
                    PlayerLoopTiming.Update,
                    ct);
            }
            catch (OperationCanceledException)
            {
                // normal on End()
            }

            // The illusion collapses into the seed rather than vanishing, while everything is
            // still cloaked; then the hull, the trail and the seed come back together.
            if (!ct.IsCancellationRequested)
            {
                try { await MorphIllusionIntoSeed(so, ct); }
                catch (OperationCanceledException) { /* End() restores everything */ }
            }

            _isRunning = false;

            RestoreShipImmediate();
            RestoreAllPrismsImmediate();
            ReplaySeedBloom();
            seedAssembler?.StopSeedCompletely();

            _activeSo = null;
            _seed = null;

            _runCts?.Dispose();
            _runCts = null;
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
            SpawnSerpentGhost();

            if (IsLocalUser)
            {
                // The pilot keeps a translucent hull to steer by: the ghost material is authored
                // fully clear (alpha 0), which hid the ship from the person flying it.
                var ghostMat = PilotGhostMaterial(_activeSo);
                if (ghostMat) ApplySingleMaterialAcrossRenderer(shipRenderer, ghostMat);
                shipRenderer.enabled = true;
            }
            else
            {
                shipRenderer.enabled = false;
            }
        }

        private static readonly int _BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int _ColorProp     = Shader.PropertyToID("_Color");

        private Material PilotGhostMaterial(CloakSeedWallActionSO so)
        {
            var source = so ? so.GhostShipMaterial : null;
            if (!source) return null;
            if (!_pilotGhostMaterial) _pilotGhostMaterial = new Material(source) { name = source.name + " (Pilot)" };
            float alpha = so.PilotGhostAlpha;
            foreach (int id in new[] { _BaseColorProp, _ColorProp })
            {
                if (!_pilotGhostMaterial.HasProperty(id)) continue;
                var c = source.GetColor(id);
                c.a = alpha;
                _pilotGhostMaterial.SetColor(id, c);
            }
            return _pilotGhostMaterial;
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

            if (!shipRenderer) return;

            shipRenderer.enabled = _shipPrevEnabled;

            if (_shipOriginalShared != null)
                shipRenderer.sharedMaterials = _shipOriginalShared;

            _shipOriginalShared = null;
        }

        private void RestoreAllPrismsImmediate()
        {
            foreach (var t in _cloakedPrisms)
            {
                if (t?.Prism == null) continue;
                if (t.Shaded)
                {
                    t.Prism.SetColorShade(1f);
                    continue;
                }
                if (t.Animator == null) continue;

                // Next ValidateMaterials() should rebuild the team materials from ThemeManager
                t.Animator.MarkMaterialsDirty();

                if (t.Prism.prismProperties != null)
                    t.Prism.prismProperties.IsTransparent = false;

                // This will call ValidateMaterials(), pull team block materials again,
                // and then choose the opaque one
                t.Prism.SetTransparency(false);
            }

            _cloakedPrisms.Clear();
        }


        // ---------------- Alpha override helpers (copied from old working code) ----------------

        private static void ApplyAlphaToRenderer(Renderer r, float alpha)
        {
            if (!r) return;

            var mats  = r.sharedMaterials;
            int count = mats?.Length ?? 0;
            if (count == 0) count = 1; // still set a block on index 0

            for (int i = 0; i < count; i++)
            {
                var block = new MaterialPropertyBlock();
                r.GetPropertyBlock(block, i);

                Color baseColor = Color.white;
                if (mats != null && i < mats.Length && mats[i])
                {
                    var mat = mats[i];

                    if (mat.HasProperty(_BaseColorId))
                    {
                        baseColor = mat.GetColor(_BaseColorId);
                        baseColor.a = alpha;
                        block.SetColor(_BaseColorId, baseColor);
                    }
                    else if (mat.HasProperty(_ColorId))
                    {
                        baseColor = mat.GetColor(_ColorId);
                        baseColor.a = alpha;
                        block.SetColor(_ColorId, baseColor);
                    }
                    else
                    {
                        baseColor.a = alpha;
                        block.SetColor(_ColorId, baseColor);
                    }
                }
                else
                {
                    baseColor.a = alpha;
                    block.SetColor(_ColorId, baseColor);
                }

                r.SetPropertyBlock(block, i);
            }
        }

        private static void ClearAlphaOverrides(Renderer r)
        {
            if (!r) return;

            var mats  = r.sharedMaterials;
            int count = mats?.Length ?? 0;
            if (count == 0) count = 1;

            for (int i = 0; i < count; i++)
            {
                // Passing null clears overrides for that submesh index
                r.SetPropertyBlock(null, i);
            }
        }

        // ---------------- Ship helper ----------------
        
        private void CloakOnePrism(Prism prism)
        {
            if (prism == null || _activeSo == null) return;

            // The pilot sees their own trail DIMMED, so they know it is hidden from everyone else
            // without losing it; every other viewer gets the cloak below.
            if (IsLocalUser)
            {
                prism.SetColorShade(_activeSo.PilotTrailShade);
                _cloakedPrisms.Add(new TrackedPrism { Prism = prism, Shaded = true });
                return;
            }

            var anim = prism.GetComponent<MaterialPropertyAnimator>();
            if (anim == null) return;

            var transparent = _activeSo.PrismCloakTransparent;
            var opaque      = _activeSo.PrismCloakOpaque;

            // If SO not set up yet, just fall back to normal transparency toggle
            if (transparent == null || opaque == null)
            {
                prism.SetTransparency(true);
                _cloakedPrisms.Add(new TrackedPrism { Prism = prism, Animator = anim });
                return;
            }

            // Blend from current team material into the cloak pair, then turn transparent
            anim.UpdateMaterial(
                transparent,
                opaque,
                0.15f,                    // fade duration, tweak as you like
                () =>
                {
                    // Mark the prism logically as transparent if you use this flag
                    if (prism.prismProperties != null)
                        prism.prismProperties.IsTransparent = true;

                    // Now actually switch to the transparent cloak material
                    prism.SetTransparency(true);
                });

            _cloakedPrisms.Add(new TrackedPrism
            {
                Prism    = prism,
                Animator = anim
            });
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
