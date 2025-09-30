using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    /// <summary>
    /// Runtime executor for Cloak + Seed Wall.
    /// Lives on the ship under ActionExecutorRegistry.
    /// </summary>
    public class CloakSeedWallActionExecutor : ShipActionExecutorBase
    {
        // ===== HUD / external subscribers =====
        public event Action OnCloakStarted;
        public event Action OnCloakEnded;
        public event Action<float> OnFadeProgress; // 0..1 during fade out/in

        [Header("Scene Refs")]
        [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;
        [SerializeField] private Transform modelRoot;
        [Tooltip("If your seed is a separate action/executor, we call into it via the registry.")]
        [SerializeField] private SeedAssemblerActionExecutor seedAssemblerExecutor; 
        [SerializeField] private SeedWallActionSO seedWallConfig;

        // Runtime
        IVesselStatus _status;
        Game.PrismSpawner  _spawner;
        ActionExecutorRegistry _registry;

        readonly HashSet<int> _protectedBlockIds = new();

        CloakSeedWallActionSO _so;
        Coroutine _runRoutine;
        bool _cloakActive;
        float _cooldownEndTime;

        // Ghost
        GameObject _ghostGo;
        Coroutine  _ghostFollowRoutine;
        Vector3    _ghostAnchorPos;
        Transform  _followTf;
        bool       _rendererWasHardDisabled;

        // Shader IDs
        static readonly int IDColor      = Shader.PropertyToID("_Color");
        static readonly int IDBaseColor  = Shader.PropertyToID("_BaseColor");
        static readonly int IDColor1     = Shader.PropertyToID("Color1");
        static readonly int IDColor2     = Shader.PropertyToID("Color2");
        static readonly int IDColorMult  = Shader.PropertyToID("ColorMultiplier");

        public override void Initialize(IVesselStatus shipStatus)
        {
            _status   = shipStatus;
            _spawner  = shipStatus?.PrismSpawner;
            _registry = GetComponent<ActionExecutorRegistry>();

            if (_spawner != null)
                _spawner.OnBlockSpawned += HandleBlockSpawned;

            // Resolve the seed assembler executor from the registry if not wired directly.
            if (seedAssemblerExecutor == null && _registry != null)
                seedAssemblerExecutor = _registry.Get<SeedAssemblerActionExecutor>();
        }

        void OnDestroy()
        {
            if (_spawner != null)
                _spawner.OnBlockSpawned -= HandleBlockSpawned;
        }

        // ===== API called from SO =====

        public void Begin(CloakSeedWallActionSO so, IVesselStatus status)
        {
            if (_runRoutine != null) return;

            _so = so;

            if (_so.RequireExistingTrailBlock && GetLatestBlock() == null)
            {
                Debug.LogWarning("[CloakSeedWall] No trail block found to plant seed on.");
                return;
            }

            _runRoutine = StartCoroutine(Run());
        }

        public void End()
        {

        }

        // ===== Core routine =====

        IEnumerator Run()
        {
            if (seedAssemblerExecutor != null && seedAssemblerExecutor.StartSeed(seedWallConfig, _status))
            {
                var seed = seedAssemblerExecutor.ActiveSeedBlock;
                if (seed) _protectedBlockIds.Add(seed.GetInstanceID());
                seedAssemblerExecutor.BeginBonding();
            }
            var seedBlock = seedAssemblerExecutor?.ActiveSeedBlock ?? GetLatestBlock();
            if (seedBlock && skinnedMeshRenderer)
                CreateGhostAt(seedBlock.transform.position, seedBlock.transform.rotation);

            // 3) Fade out ship (optional)
            if (_so.HideShipDuringCooldown)
                yield return FadeShipOut();

            // 4) Cloak active window
            var wait = Mathf.Max(0.01f, _so.CooldownSeconds);
            var lifetime = _so.GhostLifetime > 0f ? _so.GhostLifetime : wait;

            _cloakActive      = true;
            _cooldownEndTime  = Time.time + wait;
            OnCloakStarted?.Invoke();

            // schedule ghost destroy if lifetime set
            if (lifetime > 0f && _ghostGo != null)
                StartCoroutine(DestroyAfter(_ghostGo, lifetime));

            float t = 0f;
            while (t < wait)
            {
                t += Time.deltaTime;
                yield return null;
            }

            _cloakActive = false;
            OnCloakEnded?.Invoke();

            // 5) Cleanup ghost and fade back in
            CleanupGhost();
            if (_so.HideShipDuringCooldown)
                yield return FadeShipIn();

            // 6) Stop seed
            if (seedAssemblerExecutor != null)
                seedAssemblerExecutor.StopSeedCompletely();

            _runRoutine = null;
        }

        void HandleBlockSpawned(Prism block)
        {
            if (!_cloakActive || !block) return;
            if (_protectedBlockIds.Contains(block.GetInstanceID())) return;
            if (block.GetComponent<Assembler>()) return;

            var remaining = _cooldownEndTime - Time.time;
            if (remaining > 0f)
            {
                var original = block.waitTime;
                var target   = Mathf.Max(original, remaining);
                if (!Mathf.Approximately(original, target))
                    block.waitTime = target;
            }

            block.SetTransparency(true);

            // hide visuals of freshly spawned blocks until cloak ends
            var r = block;
            if (r) r.gameObject.SetActive(false);
        }

        // ===== Ghost =====

        void CreateGhostAt(Vector3 seedPos, Quaternion seedRot)
        {
            if (!skinnedMeshRenderer) return;

            var baked = new Mesh();
            skinnedMeshRenderer.BakeMesh(baked, true);

            _ghostGo = new GameObject("ShipGhost");
            var mf = _ghostGo.AddComponent<MeshFilter>();
            var mr = _ghostGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = baked;

            if (_so.GhostMaterialOverride != null)
            {
                mr.material = new Material(_so.GhostMaterialOverride);
            }
            else
            {
                var live  = skinnedMeshRenderer.materials;
                var ghost = new Material[live.Length];
                
                for (int i = 0; i < live.Length; i++)
                    ghost[i] = new Material(live[i]);
                
                mr.materials = ghost;
                
                foreach (var t in mr.materials)
                    SetMaterialAlpha(t, 1f);
            }

            _ghostAnchorPos = seedPos;
            _followTf       = GetShipFollowTransform();

            var finalRot = ComputeGhostRotation() * Quaternion.Euler(_so.GhostEulerOffset);
            _ghostGo.transform.SetPositionAndRotation(_ghostAnchorPos, finalRot);

            // scale = model root scale * multiplier (fallback to 1)
            var baseScale = modelRoot ? modelRoot.lossyScale : Vector3.one;
            var s = Mathf.Max(0.0001f, _so.GhostScaleMultiplier);
            _ghostGo.transform.localScale = new Vector3(baseScale.x * s, baseScale.y * s, baseScale.z * s);

            if (_ghostFollowRoutine != null) StopCoroutine(_ghostFollowRoutine);
            _ghostFollowRoutine = StartCoroutine(GhostFollow());
        }

        void CleanupGhost()
        {
            if (_ghostFollowRoutine != null)
            {
                StopCoroutine(_ghostFollowRoutine);
                _ghostFollowRoutine = null;
            }
            if (_ghostGo != null)
            {
                Destroy(_ghostGo);
                _ghostGo = null;
            }
        }

        IEnumerator GhostFollow()
        {
            float t = 0f;
            while (_ghostGo != null)
            {
                _ghostGo.transform.position = _ghostAnchorPos;

                if (_followTf != null)
                {
                    var baseRot = _followTf.rotation;
                    var offsetQ = Quaternion.Euler(_so.GhostEulerOffset);
                    _ghostGo.transform.rotation = baseRot * offsetQ;
                }

                if (_so.GhostIdleMotion)
                {
                    t += Time.deltaTime;
                    var bob = Mathf.Sin(t * _so.GhostBobSpeed) * _so.GhostBobAmplitude;
                    _ghostGo.transform.position = _ghostAnchorPos + new Vector3(0, bob, 0);
                    _ghostGo.transform.Rotate(Vector3.up, _so.GhostYawSpeed * Time.deltaTime, Space.World);
                }

                yield return null;
            }
        }

        IEnumerator DestroyAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go) Destroy(go);
        }

        // ===== Fade =====

        IEnumerator FadeShipOut()
        {
            if (!_so.HideShipDuringCooldown || skinnedMeshRenderer == null) yield break;

            var targetAlpha = IsLocalPlayerShip()
                ? _so.LocalCloakAlpha
                : (_so.RemoteFullInvisible ? 0f : _so.LocalCloakAlpha);

            var mats = skinnedMeshRenderer.materials;
            var anyAlphaCapable = mats.Any(MaterialSupportsAlpha);
            var anyOpaque = mats.Any(m => m && m.GetTag("RenderType", false, "Opaque") == "Opaque");

            if (anyAlphaCapable)
                yield return FadeAlpha(mats, GetCurrentAlpha(mats, 1f), targetAlpha, Mathf.Max(0.01f, _so.FadeOutSeconds), true);

            if (Mathf.Approximately(targetAlpha, 0f) && anyOpaque && _so.HardToggleIfAnyOpaqueAtZero)
            {
                skinnedMeshRenderer.enabled = false;
                _rendererWasHardDisabled = true;
            }
        }

        IEnumerator FadeShipIn()
        {
            if (!_so.HideShipDuringCooldown || !skinnedMeshRenderer) yield break;

            var mats = skinnedMeshRenderer.materials;

            if (_rendererWasHardDisabled)
            {
                skinnedMeshRenderer.enabled = true;
                _rendererWasHardDisabled = false;
            }

            var anyAlphaCapable = mats.Any(MaterialSupportsAlpha);
            if (anyAlphaCapable)
                yield return FadeAlpha(mats, GetCurrentAlpha(mats, 1f), 1f, Mathf.Max(0.01f, _so.FadeInSeconds), false);
        }

        float GetCurrentAlpha(Material[] mats, float defaultAlpha)
        {
            foreach (var m in mats)
            {
                if (!m) continue;
                if (m.HasProperty(IDBaseColor)) return m.GetColor(IDBaseColor).a;
                if (m.HasProperty(IDColor))     return m.GetColor(IDColor).a;
                if (m.HasProperty(IDColor1))    return m.GetColor(IDColor1).a;
                if (m.HasProperty(IDColor2))    return m.GetColor(IDColor2).a;
                if (m.HasProperty(IDColorMult)) return m.GetFloat(IDColorMult);
            }
            return defaultAlpha;
        }

        IEnumerator FadeAlpha(Material[] mats, float from, float to, float duration, bool isFadingOut)
        {
            float t = 0f;
            while (t < duration)
            {
                var a = Mathf.Lerp(from, to, t / duration);
                foreach (var m in mats)
                    if (m != null) SetMaterialAlpha(m, a);

                OnFadeProgress?.Invoke(isFadingOut ? (1f - (t / duration)) : (t / duration));
                t += Time.deltaTime;
                yield return null;
            }

            foreach (var m in mats)
                if (m != null) SetMaterialAlpha(m, to);

            OnFadeProgress?.Invoke(isFadingOut ? 0f : 1f);
        }

        static bool MaterialSupportsAlpha(Material m)
        {
            return m && (m.HasProperty(IDBaseColor) || m.HasProperty(IDColor) ||
                         m.HasProperty(IDColor1) || m.HasProperty(IDColor2) ||
                         m.HasProperty(IDColorMult) || m.HasProperty("_MainTex"));
        }

        static void SetMaterialAlpha(Material m, float alpha)
        {
            if (!m) return;

            // Try common properties in priority order
            if (m.HasProperty(IDBaseColor))
            {
                var c = m.GetColor(IDBaseColor); c.a = alpha; m.SetColor(IDBaseColor, c); return;
            }
            if (m.HasProperty(IDColor))
            {
                var c = m.GetColor(IDColor); c.a = alpha; m.SetColor(IDColor, c); return;
            }
            if (m.HasProperty(IDColor1))
            {
                var c = m.GetColor(IDColor1); c.a = alpha; m.SetColor(IDColor1, c); return;
            }
            if (m.HasProperty(IDColor2))
            {
                var c = m.GetColor(IDColor2); c.a = alpha; m.SetColor(IDColor2, c); return;
            }
            if (m.HasProperty(IDColorMult))
            {
                m.SetFloat(IDColorMult, alpha); return;
            }

            // Fallback: if it has _Color at all
            if (m.HasProperty("_Color"))
            {
                var c = m.GetColor("_Color"); c.a = alpha; m.SetColor("_Color", c);
            }
        }

        // ===== Utils =====

        Prism GetLatestBlock()
        {
            var listA = _spawner?.Trail?.TrailList;
            if (listA != null && listA.Count > 0) return listA[^1];

            // compatibility with private Trail2 field (as in original)
            var trail2Field = typeof(Game.PrismSpawner).GetField("Trail2", BindingFlags.Instance | BindingFlags.NonPublic);
            var trail2 = trail2Field?.GetValue(_spawner) as Trail;
            if (trail2 != null && trail2.TrailList.Count > 0) return trail2.TrailList[^1];

            return null;
        }

        Transform GetShipFollowTransform()
        {
            return _status?.ShipTransform != null ? _status.ShipTransform : null;
        }

        bool IsLocalPlayerShip()
        {
            return _status != null && _status.IsOwnerClient;
        }

        Quaternion ComputeGhostRotation()
        {
            var shipTf = _status?.ShipTransform;
            var shipUp = shipTf ? shipTf.up : Vector3.up;
            var course = _status?.Course ?? Vector3.zero;

            if (course.sqrMagnitude > 0.0001f)
                return Quaternion.LookRotation(course.normalized, shipUp);

            return shipTf ? shipTf.rotation : Quaternion.identity;
        }
    }
}