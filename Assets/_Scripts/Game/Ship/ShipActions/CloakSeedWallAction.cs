using System.Collections;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class CloakSeedWallAction : ShipAction
    {
        #region Config

        [Header("Cooldown")] [SerializeField] private float cooldownSeconds = 20f;

        [Header("Ship Visibility")] [SerializeField]
        private bool hideShipDuringCooldown = true;

        [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;

        [Header("Local vs Remote Visibility")]
        [Tooltip("Alpha seen by the LOCAL player while cloaked.")]
        [SerializeField, Range(0f, 1f)]
        private float localCloakAlpha = 0.2f; // 20%

        [Tooltip("If TRUE, non-local ships will be 0% while cloaked.")] [SerializeField]
        private bool remoteFullInvisible = true;

        [Header("Ship Fade")] [SerializeField] private float fadeOutSeconds = 0.25f;
        [SerializeField] private float fadeInSeconds = 0.25f;

        [Tooltip("If any sub-material is Opaque AND target alpha is 0, disable SMR during cloak.")] [SerializeField]
        private bool hardToggleIfAnyOpaqueAtZero = true;

        [Header("Seed Wall")] [SerializeField] private Assembler assemblerTypeSource;
        [SerializeField] private int assemblerDepth = 50;

        [Header("Ghost Ship")]
        [Tooltip("Seconds to keep the ghost. Default ties to cooldown; 0 = same as cooldown.")]
        [SerializeField]
        private float ghostLifetime = 0f;

        [Tooltip("Extra scale on the ghost, 1 = same size.")] [SerializeField]
        private float ghostScaleMultiplier = 1f;

        [Tooltip("Optional material override for ghost (else copies your SMR shared materials).")] [SerializeField]
        private Material ghostMaterialOverride;

        [Header("Safety")] [SerializeField] private bool requireExistingTrailBlock = true;

        #endregion

        #region State
        private TrailSpawner _spawner;
        private Coroutine _runRoutine;

        private Assembler _seedAssembler;
        private TrailBlock _seedBlock;

        private GameObject _ghostGo;
        private bool _rendererWasHardDisabled = false;
        private bool _cloakActive = false;
        private float _cooldownEndTime = 0f;

        private static readonly int IDColor = Shader.PropertyToID("_Color");
        private static readonly int IDBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int IDColor1 = Shader.PropertyToID("Color1");
        private static readonly int IDColor2 = Shader.PropertyToID("Color2");
        private static readonly int IDColorMult = Shader.PropertyToID("ColorMultiplier");

        #endregion

        #region Lifecycle

        public override void Initialize(IShip ship)
        {
            base.Initialize(ship);
            _spawner = Ship?.ShipStatus?.TrailSpawner;
            _spawner.OnBlockSpawned += HandleBlockSpawned;
            Debug.Log("[CloakSeedWallAction] Initialized with TrailSpawner: " + _spawner);
        }

        public override void StartAction()
        {
            if (_runRoutine != null) return;

            var latest = GetLatestBlock();
            if (latest == null && requireExistingTrailBlock)
            {
                Debug.LogWarning("[CloakSeedWallAction] No trail block found to plant seed on.");
                return;
            }

            Debug.Log("[CloakSeedWallAction] Starting action...");
            _runRoutine = StartCoroutine(Run(latest));
        }

        public override void StopAction() { }

        #endregion

        #region Flow
        
        private IEnumerator Run(TrailBlock latestBlock)
        {
            _seedBlock = latestBlock ?? GetLatestBlock();
            if (_seedBlock != null)
            {
                var assemblerType = assemblerTypeSource.GetType();
                _seedAssembler = _seedBlock.GetComponent(assemblerType) as Assembler;
                if (_seedAssembler == null)
                {
                    _seedAssembler = _seedBlock.gameObject.AddComponent(assemblerType) as Assembler;
                }

                if (_seedAssembler != null)
                {
                    _seedAssembler.Depth = assemblerDepth;
                    _seedAssembler.SeedBonding();
                }
            }

            if (_seedBlock != null && skinnedMeshRenderer != null)
            {
                CreateGhostAt(_seedBlock.transform.position, _seedBlock.transform.rotation);
            }
            
            if (hideShipDuringCooldown)
            {
                yield return StartCoroutine(FadeShipOut());
            }
            
            var wait = Mathf.Max(0.01f, cooldownSeconds);
            _cloakActive = true;
            _cooldownEndTime = Time.time + wait;

            var t = 0f;
            while (t < wait)
            {
                t += Time.deltaTime;
                yield return null;
            }

            _cloakActive = false;

            if (hideShipDuringCooldown)
            {
                yield return StartCoroutine(FadeShipIn());
            }
            
            if (_seedBlock != null)
            {
                _seedBlock.ActivateSuperShield();
            }

            if (_ghostGo != null) Destroy(_ghostGo);
            _runRoutine = null;
        }

        #endregion

        private void HandleBlockSpawned(TrailBlock block)
        {
            if (!_cloakActive || block == null) return;

            var remaining = _cooldownEndTime - Time.time;
            if (remaining <= 0f) return;

            var original = block.waitTime;
            var target = Mathf.Max(original, remaining);

            if (!Mathf.Approximately(original, target))
            {
                block.waitTime = target;
                Debug.Log($"[CloakSeedWallAction] Extended waitTime {original:0.00} → {target:0.00} (remaining {remaining:0.00}s) on {block.name}");
            }
            else
            {
                Debug.Log(
                    $"[CloakSeedWallAction] Block already has sufficient waitTime ({original:0.00}s) on {block.name}");
            }

            // keep visuals hidden just in case
            block.SetTransparency(true);
            var r = block.GetComponentInChildren<Renderer>(true);
            if (r != null) r.enabled = false;
        }

        #region Ghost ship

        private void CreateGhostAt(Vector3 seedPos, Quaternion seedRot)
        {
            if (skinnedMeshRenderer == null) return;
            
            var baked = new Mesh();
#if UNITY_2020_2_OR_NEWER
            skinnedMeshRenderer.BakeMesh(baked, true);
#else
    skinnedMeshRenderer.BakeMesh(baked);
#endif

            _ghostGo = new GameObject("ShipGhost");
            var mf = _ghostGo.AddComponent<MeshFilter>();
            var mr = _ghostGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = baked;
            
            if (ghostMaterialOverride != null)
            {
                mr.material = new Material(ghostMaterialOverride);
            }
            else
            {
                var liveMats = skinnedMeshRenderer.materials;
                var ghostMats = new Material[liveMats.Length];
                for (int i = 0; i < liveMats.Length; i++)
                    ghostMats[i] = new Material(liveMats[i]);
                mr.materials = ghostMats;
                
                foreach (var t in mr.materials)
                    SetMaterialAlpha(t, 1f);
            }

            var shipTf = (Ship != null ? Ship.Transform : skinnedMeshRenderer.transform);
            var fwd = shipTf.forward;
            var up = shipTf.up;
            _ghostGo.transform.SetPositionAndRotation(seedPos, Quaternion.LookRotation(fwd, up));
            
            _ghostGo.transform.localScale = Vector3.one;
            if (Mathf.Abs(ghostScaleMultiplier - 1f) > 0.0001f)
                _ghostGo.transform.localScale *= ghostScaleMultiplier;

            float life = ghostLifetime > 0f ? ghostLifetime : cooldownSeconds;
            if (life > 0f) StartCoroutine(DestroyAfter(_ghostGo, life));

            Debug.Log("[CloakSeedWallAction] Spawned ShipGhost (independent materials, alpha=1)");
        }
        
        private IEnumerator DestroyAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null) Destroy(go);
        }

        #endregion

        #region Ship fade

        private IEnumerator FadeShipOut()
        {
            if (!hideShipDuringCooldown || skinnedMeshRenderer == null) yield break;

            // decide target alpha: local vs remote
            float targetAlpha = IsLocalPlayerShip() ? localCloakAlpha : (remoteFullInvisible ? 0f : localCloakAlpha);

            var mats = skinnedMeshRenderer.materials; // instances
            bool anyAlphaCapable = false;
            bool anyOpaque = false;

            foreach (var m in mats)
            {
                if (m == null) continue;
                if (MaterialSupportsAlpha(m)) anyAlphaCapable = true;
                if (m.GetTag("RenderType", false, "Opaque") == "Opaque") anyOpaque = true;
            }

            if (anyAlphaCapable)
                yield return StartCoroutine(FadeAlpha(mats, 1f, targetAlpha, Mathf.Max(0.01f, fadeOutSeconds)));

            // If we want 0% and some mats are opaque → hard toggle to fully hide for that viewer
            if (Mathf.Approximately(targetAlpha, 0f) && anyOpaque && hardToggleIfAnyOpaqueAtZero)
            {
                skinnedMeshRenderer.enabled = false;
                _rendererWasHardDisabled = true;
                Debug.Log("[CloakSeedWallAction] Hard-disabled SMR due to opaque mats & target 0%");
            }
        }

        private IEnumerator FadeShipIn()
        {
            if (!hideShipDuringCooldown || skinnedMeshRenderer == null) yield break;

            var mats = skinnedMeshRenderer.materials;

            if (_rendererWasHardDisabled)
            {
                skinnedMeshRenderer.enabled = true;
                _rendererWasHardDisabled = false;
            }

            // restore to 1
            bool anyAlphaCapable = false;
            foreach (var m in mats)
                if (m && MaterialSupportsAlpha(m))
                {
                    anyAlphaCapable = true;
                    break;
                }

            if (anyAlphaCapable)
                yield return StartCoroutine(FadeAlpha(mats, GetCurrentAlpha(mats, 1f), 1f,
                    Mathf.Max(0.01f, fadeInSeconds)));
        }

        private float GetCurrentAlpha(Material[] mats, float defaultAlpha)
        {
            foreach (var m in mats)
            {
                if (m == null) continue;
                if (m.HasProperty(IDBaseColor)) return m.GetColor(IDBaseColor).a;
                if (m.HasProperty(IDColor)) return m.GetColor(IDColor).a;
                if (m.HasProperty(IDColor1)) return m.GetColor(IDColor1).a;
                if (m.HasProperty(IDColor2)) return m.GetColor(IDColor2).a;
                if (m.HasProperty(IDColorMult)) return m.GetFloat(IDColorMult);
            }

            return defaultAlpha;
        }

        private IEnumerator FadeAlpha(Material[] mats, float from, float to, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                float a = Mathf.Lerp(from, to, t / duration);
                foreach (var m in mats)
                    if (m != null)
                        SetMaterialAlpha(m, a);
                t += Time.deltaTime;
                yield return null;
            }

            foreach (var m in mats)
                if (m != null)
                    SetMaterialAlpha(m, to);
        }

        private bool MaterialSupportsAlpha(Material m)
        {
            return m.HasProperty(IDBaseColor) || m.HasProperty(IDColor)
                                               || m.HasProperty(IDColor1) || m.HasProperty(IDColor2)
                                               || m.HasProperty(IDColorMult);
        }

        private void SetMaterialAlpha(Material m, float alpha)
        {
            if (m.HasProperty(IDBaseColor))
            {
                var c = m.GetColor(IDBaseColor);
                c.a = alpha;
                m.SetColor(IDBaseColor, c);
                return;
            }

            if (m.HasProperty(IDColor))
            {
                var c = m.GetColor(IDColor);
                c.a = alpha;
                m.SetColor(IDColor, c);
                return;
            }

            bool g = false;
            if (m.HasProperty(IDColor1))
            {
                var c1 = m.GetColor(IDColor1);
                c1.a = alpha;
                m.SetColor(IDColor1, c1);
                g = true;
            }

            if (m.HasProperty(IDColor2))
            {
                var c2 = m.GetColor(IDColor2);
                c2.a = alpha;
                m.SetColor(IDColor2, c2);
                g = true;
            }

            if (g) return;

            if (m.HasProperty(IDColorMult)) m.SetFloat(IDColorMult, alpha);
        }

        private bool IsLocalPlayerShip()
        {
            return Ship != null && Ship.ShipStatus.IsOwner;
        }

        #endregion

        #region Utils

        private TrailBlock GetLatestBlock()
        {
            var listA = _spawner.Trail?.TrailList;
            if (listA != null && listA.Count > 0) return listA[^1];

            var trail2Field = typeof(TrailSpawner).GetField("Trail2", BindingFlags.Instance | BindingFlags.NonPublic);
            var trail2 = trail2Field?.GetValue(_spawner) as Trail;
            if (trail2 != null && trail2.TrailList.Count > 0) return trail2.TrailList[^1];

            return null;
        }

        #endregion
    }
}