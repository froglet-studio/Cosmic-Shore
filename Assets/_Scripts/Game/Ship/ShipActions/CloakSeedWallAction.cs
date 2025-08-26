using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using CosmicShore.Core;
using CosmicShore.Game;
using UnityEngine;

namespace CosmicShore
{
    public class CloakSeedWallAction : ShipAction
    {
        #region Config
        [Header("Cooldown")]
        [SerializeField] private float cooldownSeconds = 20f; // you said 20

        [Header("Ship Visibility")]
        [SerializeField] private bool hideShipDuringCooldown = true;
        [SerializeField] private SkinnedMeshRenderer skinnedMeshRenderer;

        [Header("Seed Wall")]
        [SerializeField] private Assembler assemblerTypeSource;
        [SerializeField] private int assemblerDepth = 50;

        [Header("Safety")]
        [SerializeField] private bool requireExistingTrailBlock = true;
        #endregion

        #region State
        private TrailSpawner _spawner;
        private Coroutine _runRoutine;

        private Assembler  _seedAssembler;
        private TrailBlock _seedBlock;

        private float _cooldownEndTime;

        private static readonly int ID_Color     = Shader.PropertyToID("_Color");
        private static readonly int ID_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ID_Color1    = Shader.PropertyToID("Color1");
        private static readonly int ID_Color2    = Shader.PropertyToID("Color2");
        private static readonly int ID_ColorMult = Shader.PropertyToID("ColorMultiplier");
        #endregion

        #region Lifecycle
        public override void Initialize(IShip ship)
        {
            base.Initialize(ship);
            _spawner = Ship?.ShipStatus?.TrailSpawner;
            if (_spawner != null)
            {
                // subscribe once; handler checks remaining cooldown each spawn
                _spawner.OnBlockSpawned += HandleBlockSpawned;
                Debug.Log("[CloakSeedWallAction] Subscribed to TrailSpawner.OnBlockSpawned");
            }
            else
            {
                Debug.LogWarning("[CloakSeedWallAction] No TrailSpawner found on ShipStatus during Initialize");
            }
        }

        // If you ever need to unhook (not required now, but safe):
        private void OnDestroy()
        {
            if (_spawner != null)
                _spawner.OnBlockSpawned -= HandleBlockSpawned;
        }

        public override void StartAction()
        {
            if (_runRoutine != null) return;

            if (_spawner == null)
            {
                Debug.LogWarning("[CloakSeedWallAction] No TrailSpawner found on ShipStatus.");
                return;
            }
            if (assemblerTypeSource == null)
            {
                Debug.LogWarning("[CloakSeedWallAction] assemblerTypeSource not assigned.");
                return;
            }

            var latest = GetLatestBlock();
            if (latest == null && requireExistingTrailBlock)
            {
                Debug.LogWarning("[CloakSeedWallAction] No trail block found to plant seed on.");
                return;
            }

            Debug.Log("[CloakSeedWallAction] Starting action...");
            _runRoutine = StartCoroutine(Run(latest));
        }

        public override void StopAction()
        {
            // per your request: leave commented out
            // if (_runRoutine != null) { StopCoroutine(_runRoutine); _runRoutine = null; }
            // (no restore needed per latest ask)
        }
        #endregion

        #region Flow
        private IEnumerator Run(TrailBlock latestBlock)
        {
            Debug.Log("[CloakSeedWallAction] Run() started");

            // Seed wall: attach assembler and start bonding immediately
            _seedBlock = latestBlock ?? GetLatestBlock();
            if (_seedBlock != null)
            {
                var assemblerType = assemblerTypeSource.GetType();
                _seedAssembler = _seedBlock.GetComponent(assemblerType) as Assembler;
                if (_seedAssembler == null)
                {
                    _seedAssembler = _seedBlock.gameObject.AddComponent(assemblerType) as Assembler;
                    Debug.Log("[CloakSeedWallAction] Added assembler of type " + assemblerType.Name);
                }
                _seedAssembler.Depth = assemblerDepth;
                Debug.Log("[CloakSeedWallAction] Calling SeedBonding() immediately...");
                _seedAssembler.SeedBonding();
            }
            else
            {
                Debug.LogWarning("[CloakSeedWallAction] No seed block found at Run()");
            }

            // Hide ship (optional)
            if (hideShipDuringCooldown)
            {
                Debug.Log("[CloakSeedWallAction] Hiding ship visuals");
                SetShipVisible(false);
            }

            // Mark cooldown window
            float wait = Mathf.Max(0.01f, cooldownSeconds);
            _cooldownEndTime = Time.time + wait;
            Debug.Log($"[CloakSeedWallAction] Cooldown window opened for {wait:0.00}s");

            // Simply wait the duration; OnBlockSpawned will extend each new block's waitTime
            float t = 0f;
            while (t < wait)
            {
                t += Time.deltaTime;
                yield return null;
            }

            // Cooldown ends
            Debug.Log("[CloakSeedWallAction] Cooldown ended");
            if (hideShipDuringCooldown) SetShipVisible(true);

            _runRoutine = null;
            Debug.Log("[CloakSeedWallAction] Run() finished");
        }
        #endregion

        #region Spawner hook
        private void HandleBlockSpawned(TrailBlock block)
        {
            // Called immediately when TrailSpawner instantiates/configures a TrailBlock
            if (block == null) return;

            // If we are inside the cooldown window, ensure this block won't show/grow until it ends
            float remaining = _cooldownEndTime - Time.time;
            if (remaining > 0f)
            {
                // Bump block.waitTime so its Start() coroutine yields long enough
                float original = block.waitTime;
                float target   = Mathf.Max(original, remaining);
                if (!Mathf.Approximately(original, target))
                {
                    block.waitTime = target;
                    Debug.Log($"[CloakSeedWallAction] Extended block.waitTime from {original:0.00} → {target:0.00} (remaining {remaining:0.00}s) on {block.name}");
                }
                else
                {
                    Debug.Log($"[CloakSeedWallAction] Block already has sufficient waitTime ({original:0.00}s) on {block.name}");
                }

                // Optional: make sure it stays invisible even if a renderer slips through early
                // (Shouldn’t be necessary because TrailBlock enables renderer only after waitTime)
                block.SetTransparency(true);
            }
        }
        #endregion

        #region Helpers
        private void SetShipVisible(bool visible)
{
    if (skinnedMeshRenderer == null)
    {
        Debug.Log("[CloakSeedWallAction] SMR is null; cannot toggle visibility");
        return;
    }

    var mats = skinnedMeshRenderer.materials;
    bool anyOpaque = false;
    for (int i = 0; i < mats.Length; i++)
    {
        var m = mats[i];
        if (m == null) continue;

        // Shader Graph generated property for URP surface type:
        // 0 = Opaque, 1 = Transparent (when exposed). If not present, fall back to RenderType tag.
        bool isOpaque = true;
        if (m.HasProperty("_Surface"))
            isOpaque = Mathf.Approximately(m.GetFloat("_Surface"), 0f);
        else
            isOpaque = m.GetTag("RenderType", false, "Opaque") == "Opaque";

        if (isOpaque) anyOpaque = true;
    }

    if (anyOpaque)
    {
        // Some materials are opaque → alpha fading won’t work → just toggle the renderer.
        skinnedMeshRenderer.enabled = visible;
        Debug.Log($"[CloakSeedWallAction] SMR.enabled = {visible} (one or more materials are Opaque)");
        return;
    }

    // If ALL materials are transparent-capable, alpha fade them (your BlueBaseShipMaterial).
    float alpha = visible ? 1f : 0f;
    bool touched = false;
    foreach (var m in mats)
    {
        if (m == null) continue;

        if (m.HasProperty("_BaseColor"))
        { var c = m.GetColor("_BaseColor"); c.a = alpha; m.SetColor("_BaseColor", c); touched = true; continue; }

        if (m.HasProperty("_Color"))
        { var c = m.GetColor("_Color"); c.a = alpha; m.SetColor("_Color", c); touched = true; continue; }

        bool g = false;
        if (m.HasProperty("Color1")) { var c1 = m.GetColor("Color1"); c1.a = alpha; m.SetColor("Color1", c1); g = true; }
        if (m.HasProperty("Color2")) { var c2 = m.GetColor("Color2"); c2.a = alpha; m.SetColor("Color2", c2); g = true; }
        if (g) { touched = true; continue; }

        if (m.HasProperty("ColorMultiplier")) { m.SetFloat("ColorMultiplier", alpha); touched = true; continue; }
    }

    if (!touched)
    {
        // Safety fallback
        skinnedMeshRenderer.enabled = visible;
        Debug.Log($"[CloakSeedWallAction] Fallback SMR.enabled = {visible} (no fade properties found)");
    }
}

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
