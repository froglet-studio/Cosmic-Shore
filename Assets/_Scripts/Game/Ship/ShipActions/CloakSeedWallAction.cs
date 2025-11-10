using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class CloakSeedWallAction : ShipAction
    {
        [Header("Cooldown")] [SerializeField] private float cooldownSeconds = 20f;

        [Header("Vessel Visibility")] 
        [SerializeField] private bool hideShipDuringCooldown = true;
        [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;

        [Header("Local vs Remote Visibility")]
        [SerializeField, Range(0f, 1f)] private float localCloakAlpha = 0.2f;
        [SerializeField] private bool remoteFullInvisible = true;

        [Header("Vessel Fade")]
        [SerializeField] private float fadeOutSeconds = 0.25f;
        [SerializeField] private float fadeInSeconds  = 0.25f;
        [SerializeField] private bool hardToggleIfAnyOpaqueAtZero = true;

        [Header("Seed Wall")]
        [SerializeField] private SeedAssemblerConfigurator seedAssemblerAction;   
        [SerializeField] private bool requireExistingTrailBlock = true;

        [Header("Ghost Vessel")]
        [SerializeField] private float  ghostLifetime       = 0f;  // 0 = same as cooldown
        [SerializeField] private float  ghostScaleMultiplier = 1f;
        [SerializeField] private Material ghostMaterialOverride;
        [Tooltip("Applied after reading vessel rotation; use (0,180,0) if baked mesh is flipped.")]
        [SerializeField] private Vector3 ghostEulerOffset = new Vector3(0f, 180f, 0f);
        [Tooltip("Enable subtle idle motion so the ghost feels alive.")]
        [SerializeField] private bool ghostIdleMotion = true;
        [SerializeField] private float ghostBobAmplitude = 0.15f;
        [SerializeField] private float ghostBobSpeed     = 1.2f;
        [SerializeField] private float ghostYawSpeed     = 10f; // deg/sec
        [Header("Model Root")]
        [SerializeField] private Transform modelRoot; 
        
        private readonly HashSet<int> _protectedBlockIds = new();
        private Game.VesselPrismController controller;
        private Coroutine _runRoutine;

        private GameObject _ghostGo;
        private bool _rendererWasHardDisabled;
        private bool _cloakActive;
        private float _cooldownEndTime;

        private Coroutine _ghostFollowRoutine;
        private Vector3 _ghostAnchorPos;
        private Transform _followTf;

        private static readonly int IDColor = Shader.PropertyToID("_Color");
        private static readonly int IDBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int IDColor1 = Shader.PropertyToID("Color1");
        private static readonly int IDColor2 = Shader.PropertyToID("Color2");
        private static readonly int IDColorMult = Shader.PropertyToID("ColorMultiplier");

        public override void Initialize(IVessel vessel)
        {
            base.Initialize(vessel);
            controller = Vessel?.VesselStatus?.VesselPrismController;
            controller.OnBlockSpawned += HandleBlockSpawned;

            // Initialize the shared seeding action
            if (seedAssemblerAction != null)
                seedAssemblerAction.Initialize(Vessel);
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

            _runRoutine = StartCoroutine(Run());
        }

        public override void StopAction() { }

        private IEnumerator Run()
        {
            if (seedAssemblerAction.StartSeed())
            {
                var seed = seedAssemblerAction.ActiveSeedBlock;
                if (seed != null) _protectedBlockIds.Add(seed.GetInstanceID());

                seedAssemblerAction.BeginBonding();
            }
            var seedBlock = seedAssemblerAction?.ActiveSeedBlock ?? GetLatestBlock();
            if (seedBlock != null && skinnedMeshRenderer != null)
            {
                CreateGhostAt(seedBlock.transform.position, seedBlock.transform.rotation);
            }

            if (hideShipDuringCooldown)
                yield return StartCoroutine(FadeShipOut());

            float wait = Mathf.Max(0.01f, cooldownSeconds);
            _cloakActive = true;
            _cooldownEndTime = Time.time + wait;

            float t = 0f;
            while (t < wait)
            {
                t += Time.deltaTime;
                yield return null;
            }

            _cloakActive = false;

            CleanupGhost();
            if (hideShipDuringCooldown)
                yield return StartCoroutine(FadeShipIn());
            
            if (seedAssemblerAction != null)
                seedAssemblerAction.StopSeedCompletely();
            _runRoutine = null;
        }

        private void HandleBlockSpawned(Prism block)
        {
            if (!_cloakActive || block == null) return;

            if (_protectedBlockIds.Contains(block.GetInstanceID())) return;
            if (block.GetComponent<Assembler>() != null) return;

            var remaining = _cooldownEndTime - Time.time;
            if (remaining > 0f)
            {
                var original = block.waitTime;
                var target   = Mathf.Max(original, remaining);
                if (!Mathf.Approximately(original, target))
                    block.waitTime = target;
            }

            block.SetTransparency(true);
            var r = block.GetComponentInChildren<Renderer>(true);
            if (r != null) r.enabled = false;
        }


        // ---------- Ghost ----------

        private void CreateGhostAt(Vector3 seedPos, Quaternion seedRot)
        {
            if (skinnedMeshRenderer == null) return;

            var baked = new Mesh();
            // do NOT bake scale; we’ll set it explicitly from the model
            skinnedMeshRenderer.BakeMesh(baked, true);

            _ghostGo = new GameObject("ShipGhost");
            var mf = _ghostGo.AddComponent<MeshFilter>();
            var mr = _ghostGo.AddComponent<MeshRenderer>();
            mf.sharedMesh = baked;

            if (ghostMaterialOverride != null)
                mr.material = new Material(ghostMaterialOverride);
            else
            {
                var live  = skinnedMeshRenderer.materials;
                var ghost = new Material[live.Length];
                for (int i = 0; i < live.Length; i++) ghost[i] = new Material(live[i]);
                mr.materials = ghost;
                for (int i = 0; i < mr.materials.Length; i++) SetMaterialAlpha(mr.materials[i], 1f);
            }

            _ghostAnchorPos = seedPos;
            _followTf = GetShipFollowTransform();

            var finalRot = ComputeGhostRotation() * Quaternion.Euler(ghostEulerOffset);
            _ghostGo.transform.SetPositionAndRotation(_ghostAnchorPos, finalRot);

            _ghostGo = new GameObject("ShipGhost");
            mf.sharedMesh = baked;
            
            _ghostGo.transform.localScale = Vector3.one; 
            _ghostGo.transform.localScale *= 1.5f;
            if (_ghostFollowRoutine != null) StopCoroutine(_ghostFollowRoutine);
            _ghostFollowRoutine = StartCoroutine(GhostFollow());
        }
        
        private void CleanupGhost()
        {
            _ghostGo.SetActive(false);
            Destroy(_ghostGo);
            _ghostGo = null;
        }

        private IEnumerator GhostFollow()
        {
            float t = 0f;
            while (_ghostGo != null)
            {
                _ghostGo.transform.position = _ghostAnchorPos;
                
                if (_followTf != null)
                {
                    var baseRot = _followTf.rotation;
                    var offsetQ = Quaternion.Euler(ghostEulerOffset);
                    _ghostGo.transform.rotation = baseRot * offsetQ;
                }
                
                if (ghostIdleMotion)
                {
                    t += Time.deltaTime;
                    var bob = Mathf.Sin(t * ghostBobSpeed) * ghostBobAmplitude;
                    _ghostGo.transform.position = _ghostAnchorPos + new Vector3(0, bob, 0);
                    _ghostGo.transform.Rotate(Vector3.up, ghostYawSpeed * Time.deltaTime, Space.World);
                }

                yield return null;
            }
        }

        private IEnumerator DestroyAfter(GameObject go, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (go != null) Destroy(go);
        }

        // ---------- Vessel fade (unchanged core) ----------

        private IEnumerator FadeShipOut()
        {
            if (!hideShipDuringCooldown || skinnedMeshRenderer == null) yield break;

            var targetAlpha = IsLocalPlayerShip() ? localCloakAlpha : remoteFullInvisible ? 0f : localCloakAlpha;

            var mats = skinnedMeshRenderer.materials;
            var anyAlphaCapable = false;
            var anyOpaque = false;

            foreach (var m in mats)
            {
                if (m == null) continue;
                if (MaterialSupportsAlpha(m)) anyAlphaCapable = true;
                if (m.GetTag("RenderType", false, "Opaque") == "Opaque") anyOpaque = true;
            }

            if (anyAlphaCapable)
                yield return StartCoroutine(FadeAlpha(mats, 1f, localCloakAlpha, Mathf.Max(0.01f, fadeOutSeconds)));

            if (!Mathf.Approximately(targetAlpha, 0f) || !anyOpaque || !hardToggleIfAnyOpaqueAtZero) yield break;
            skinnedMeshRenderer.enabled = false;
            _rendererWasHardDisabled = true;
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
            var anyAlphaCapable = mats.Any(m => m && MaterialSupportsAlpha(m));

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

        private static IEnumerator FadeAlpha(Material[] mats, float from, float to, float duration)
        {
            var t = 0f;
            while (t < duration)
            {
                var a = Mathf.Lerp(from, to, t / duration);
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

        private static bool MaterialSupportsAlpha(Material m)
        {
            return m && (m.HasProperty(IDBaseColor) || m.HasProperty(IDColor) ||
                         m.HasProperty(IDColor1) || m.HasProperty(IDColor2) ||
                         m.HasProperty(IDColorMult) || m.HasProperty("_MainTex")); // last resort gate
        }

        private static void SetMaterialAlpha(Material m, float alpha) {  }

        // ---------- Utils ----------

        private Prism GetLatestBlock()
        {
            var listA = controller?.Trail?.TrailList;
            if (listA != null && listA.Count > 0) return listA[^1];

            var trail2Field = typeof(Game.VesselPrismController).GetField("Trail2", BindingFlags.Instance | BindingFlags.NonPublic);
            var trail2 = trail2Field?.GetValue(controller) as Trail;
            if (trail2 != null && trail2.TrailList.Count > 0) return trail2.TrailList[^1];

            return null;
        }

        private Transform GetShipFollowTransform()
        {
            var statusTf = Vessel?.VesselStatus?.ShipTransform;
            if (statusTf != null) return statusTf;
            return null;
        }

        private bool IsLocalPlayerShip()
        {
            return Vessel != null && Vessel.VesselStatus.IsNetworkOwner;
        }
        
        Quaternion ComputeGhostRotation()
        {
            var shipTf   = Vessel?.VesselStatus?.ShipTransform;
            var shipUp   = shipTf ? shipTf.up : Vector3.up;
            var course   = Vessel?.VesselStatus?.Course ?? Vector3.zero;
            
            if (course.sqrMagnitude > 0.0001f)
                return Quaternion.LookRotation(course.normalized, shipUp);

            return shipTf ? shipTf.rotation : Quaternion.identity;
        }
    }
}


