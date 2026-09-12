using UnityEngine;
using System;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.ECS;

namespace CosmicShore.Gameplay
{
    public class MaterialPropertyAnimator : MonoBehaviour
    {
        [Header("Data Containers")]
        [SerializeField] ThemeManagerDataContainerSO _themeManagerData;

        private static readonly int BrightColorId = Shader.PropertyToID("_BrightColor");
        private static readonly int DarkColorId = Shader.PropertyToID("_DarkColor");
        private static readonly int SpreadId = Shader.PropertyToID("_Spread");

        public MaterialPropertyBlock PropertyBlock { get; private set; }
        public MeshRenderer MeshRenderer { get; private set; }

        public Color StartBrightColor { get; private set; }
        public Color TargetBrightColor { get; private set; }
        public Color StartDarkColor { get; private set; }
        public Color TargetDarkColor { get; private set; }
        public Vector3 StartSpread { get; private set; }
        public Vector3 TargetSpread { get; private set; }

        private Material activeTransparentMaterial;
        private Material activeOpaqueMaterial;
        private Prism cachedPrism;
        private bool materialsDirty;

        private void Awake()
        {
            // Cache components
            MeshRenderer = GetComponent<MeshRenderer>();
            cachedPrism = GetComponent<Prism>();

            if (MeshRenderer == null)
            {
                CSDebug.LogError($"MeshRenderer missing on {gameObject.name}");
                enabled = false;
                return;
            }

            PropertyBlock = new MaterialPropertyBlock();
        }

        private void OnDisable()
        {
            // Clock path: a pooled-out prism must not fire a stale settle later.
            _clockColorActive = false;
            PrismTimerManager.Instance?.CancelScheduledActions(this);
        }

        private bool ValidateMaterials()
        {
            if (!materialsDirty && activeTransparentMaterial != null && activeOpaqueMaterial != null)
                return true;

            if (cachedPrism == null)
                return false;

            try
            {
                var team = cachedPrism.Domain;
                activeOpaqueMaterial = _themeManagerData.GetTeamBlockMaterial(team);
                activeTransparentMaterial = _themeManagerData.GetTeamTransparentBlockMaterial(team);
                
                if (activeOpaqueMaterial != null && activeTransparentMaterial != null && MeshRenderer != null)
                {
                    if (cachedPrism.prismProperties != null && cachedPrism.prismProperties.IsTransparent)
                        MeshRenderer.sharedMaterial = activeTransparentMaterial;
                    else
                        MeshRenderer.sharedMaterial = activeOpaqueMaterial;
                    cachedPrism.SyncRenderMaterial();
                }

                materialsDirty = false;
                return true;
            }
            catch (Exception e)
            {
                CSDebug.LogError($"Error validating materials: {e.Message}");
                return false;
            }
        }

        // ------------------------------------------------------------------
        // Clock-material color transitions (Docs/PRISM_ANIMATION.md, LOCKED law).
        // A transition is: bind the END-STATE material immediately
        // (gameplay-final-at-start — the entity's per-instance overrides snap to
        // the new material's authored values, which ARE the lerp targets), stamp
        // {t0, duration, start colors} once, and schedule ONE settle at t0+dur.
        // The legacy per-frame manager pass was deleted in the D2 pass.
        // ------------------------------------------------------------------

        bool _clockColorActive;
        float _clockColorT0;
        float _clockColorDuration;
        Color _clockFromBright;
        Color _clockFromDark;
        Vector3 _clockFromSpread;

        /// <summary>Analytic displayed colors of an in-flight clock transition —
        /// the interruption start-state and the exotic-handoff pin, computed on
        /// demand (never tracked per frame).</summary>
        internal bool TryGetClockColorCurrent(out Color bright, out Color dark, out Vector3 spread)
        {
            if (!_clockColorActive || _clockColorDuration <= 0f)
            {
                bright = default; dark = default; spread = default;
                return false;
            }
            float now = PrismClock.Now;
            if (now >= _clockColorT0 + _clockColorDuration)
            {
                bright = default; dark = default; spread = default;
                return false;
            }
            float p = Mathf.Clamp01((now - _clockColorT0) / _clockColorDuration);
            float t = p * p * (3f - 2f * p); // smoothstep — matches PrismColorLerp
            bright = Color.Lerp(_clockFromBright, TargetBrightColor, t);
            dark = Color.Lerp(_clockFromDark, TargetDarkColor, t);
            spread = Vector3.Lerp(_clockFromSpread, TargetSpread, t);
            return true;
        }

        public void UpdateMaterial(Material transparentMaterial, Material opaqueMaterial, float duration = 0.8f, Action onComplete = null)
        {
            if (!enabled || MeshRenderer == null) return;

            if (transparentMaterial == null || opaqueMaterial == null)
            {
                CSDebug.LogError($"Invalid materials provided to {gameObject.name}");
                return;
            }

            if (!ValidateMaterials()) return;

            // STRICT MODE (Docs/PRISM_ANIMATION.md, no-fallback law): the clock
            // transition is the ONLY implementation.
            ClockColorTransition(transparentMaterial, opaqueMaterial, duration, onComplete);
        }

        /// <summary>
        /// Snap the renderer to a material pair with no clock lerp. Used by
        /// environment-pool reuse to restore Domains.Blue so the next ChangeTeam
        /// is a real Blue → domain stamp. Works while inactive and before Awake.
        /// Does not early-out on <c>!enabled</c>.
        /// </summary>
        public void BindMaterialsImmediate(Material transparentMaterial, Material opaqueMaterial)
        {
            if (MeshRenderer == null) MeshRenderer = GetComponent<MeshRenderer>();
            if (cachedPrism == null) cachedPrism = GetComponent<Prism>();

            if (MeshRenderer == null)
            {
                CSDebug.LogError($"MeshRenderer missing on {gameObject.name}");
                return;
            }
            if (transparentMaterial == null || opaqueMaterial == null)
            {
                CSDebug.LogError($"Invalid materials provided to {gameObject.name}");
                return;
            }

            var timers = PrismTimerManager.EnsureInstance();
            timers.CancelScheduledActions(this);
            _clockColorActive = false;
            if (cachedPrism != null && PrismRenderService.IsHandleUsable(in cachedPrism.RenderHandle))
                PrismRenderService.ClearColorTransitionStamp(in cachedPrism.RenderHandle);

            bool transparent = cachedPrism != null && cachedPrism.prismProperties != null &&
                               cachedPrism.prismProperties.IsTransparent;
            var bindMaterial = transparent ? transparentMaterial : opaqueMaterial;

            activeTransparentMaterial = transparentMaterial;
            activeOpaqueMaterial = opaqueMaterial;
            materialsDirty = false;
            MeshRenderer.sharedMaterial = bindMaterial;
            cachedPrism?.SyncRenderMaterial();

            if (bindMaterial.HasProperty(BrightColorId))
                StartBrightColor = TargetBrightColor = bindMaterial.GetColor(BrightColorId);
            if (bindMaterial.HasProperty(DarkColorId))
                StartDarkColor = TargetDarkColor = bindMaterial.GetColor(DarkColorId);
            if (bindMaterial.HasProperty(SpreadId))
                StartSpread = TargetSpread = bindMaterial.GetVector(SpreadId);
        }

        static readonly int ColorStartTimeId = Shader.PropertyToID("_ColorStartTime");

        /// <summary>
        /// The ONLY implementation of a prism color/state transition (STRICT MODE —
        /// no legacy fallback). Binds the end-state material immediately
        /// (gameplay-final-at-start), stamps the clock lerp when the companion
        /// entity can carry it, and schedules ONE settle. When the entity sink is
        /// unavailable (exotic morph window, pre-first-show prism, instanced path
        /// down) the bind is the whole transition — a one-shot to the end state;
        /// spawn-paint of a fresh prism is a creation, not a recolor, so that is
        /// silent; a genuinely missing render path screams via diagnostics.
        /// </summary>
        void ClockColorTransition(Material transparentMaterial, Material opaqueMaterial, float duration, Action onComplete)
        {
            float now = PrismClock.Now;
            bool transparent = cachedPrism != null && cachedPrism.prismProperties != null &&
                               cachedPrism.prismProperties.IsTransparent;
            var bindMaterial = transparent ? transparentMaterial : opaqueMaterial;

            // Fail loud on an unwired graph — the transition will SNAP (no fallback).
            if (!bindMaterial.HasProperty(ColorStartTimeId))
                PrismClockDiagnostics.WarnUnwiredMaterial(bindMaterial, "_ColorStartTime", this);

            // Start colors: analytic current of an in-flight clock transition,
            // else the currently bound material's authored values. Must be read
            // BEFORE the end-state material is bound below.
            if (TryGetClockColorCurrent(out var curBright, out var curDark, out var curSpread))
            {
                StartBrightColor = curBright;
                StartDarkColor = curDark;
                StartSpread = curSpread;
            }
            else
            {
                var currentMaterial = MeshRenderer.sharedMaterial;
                StartBrightColor = currentMaterial.GetColor(BrightColorId);
                StartDarkColor = currentMaterial.GetColor(DarkColorId);
                StartSpread = currentMaterial.GetVector(SpreadId);
            }

            // Targets = the end-state material's authored values (the material the
            // shader lerps toward — no _Target* properties exist by design).
            TargetBrightColor = bindMaterial.GetColor(BrightColorId);
            TargetDarkColor = bindMaterial.GetColor(DarkColorId);
            TargetSpread = bindMaterial.GetVector(SpreadId);

            // Gameplay-final-at-start: bind the end-state material NOW. The entity's
            // color overrides snap to its authored values via SyncRenderMaterial.
            activeTransparentMaterial = transparentMaterial;
            activeOpaqueMaterial = opaqueMaterial;
            MeshRenderer.sharedMaterial = bindMaterial;
            cachedPrism?.SyncRenderMaterial();

            // A new transition supersedes any pending settle.
            var timers = PrismTimerManager.EnsureInstance();
            timers.CancelScheduledActions(this);
            _clockColorActive = false;

            bool stamped = cachedPrism != null && cachedPrism.UsesEntityColorSink &&
                PrismRenderService.StampColorTransition(in cachedPrism.RenderHandle, now, duration,
                    PrismRenderService.ToFloat4(StartBrightColor),
                    PrismRenderService.ToFloat4(StartDarkColor),
                    PrismRenderService.ToFloat3(StartSpread));

            if (stamped)
            {
                _clockColorActive = true;
                _clockColorT0 = now;
                _clockColorDuration = duration;
                _clockFromBright = StartBrightColor;
                _clockFromDark = StartDarkColor;
                _clockFromSpread = StartSpread;

                // Touchpoint 3: ONE scheduled settle — clear the stamp (invisible: at
                // t >= end the shader lerp already equals the bound material) and fire
                // the caller's completion.
                timers.ScheduleAction(this, duration, () =>
                {
                    _clockColorActive = false;
                    if (cachedPrism != null)
                        PrismRenderService.ClearColorTransitionStamp(in cachedPrism.RenderHandle);
                    onComplete?.Invoke();
                });
            }
            else if (onComplete != null)
            {
                // No entity sink (exotic window / pre-show / instanced down): the
                // bind above IS the transition. Preserve completion timing.
                timers.ScheduleAction(this, duration, () => onComplete());
            }
        }

        public void SetTransparency(bool transparent)
        {
            if (MeshRenderer != null && ValidateMaterials())
            {
                MeshRenderer.sharedMaterial = transparent ? activeTransparentMaterial : activeOpaqueMaterial;
                cachedPrism.prismProperties.IsTransparent = transparent;
                cachedPrism.SyncRenderMaterial();
            }
        }

        public void MarkMaterialsDirty()
        {
            materialsDirty = true;
        }

        /// <summary>
        /// Writes the currently displayed colors into the renderer's
        /// MaterialPropertyBlock. Used when rendering hands off from the
        /// companion entity to the GameObject (octahedron engage): the MPB may
        /// hold colors from long before the entity path took over, and a stale
        /// block would flash for a frame. Mid-animation we pin the tracked
        /// current values; at rest we clear so the base sharedMaterial (which
        /// the completed animation already matched) shows through.
        /// </summary>
        internal void FlushDisplayedColorsToRenderer()
        {
            if (MeshRenderer == null || PropertyBlock == null) return;
            PropertyBlock.Clear();
            if (TryGetClockColorCurrent(out var bright, out var dark, out var spread))
            {
                // Mid-flight clock transition handing off to the GameObject renderer
                // (shield engage): pin the analytically-current colors so the frame
                // can't flash the already-bound end-state material's colors.
                PropertyBlock.SetColor(BrightColorId, bright);
                PropertyBlock.SetColor(DarkColorId, dark);
                PropertyBlock.SetVector(SpreadId, new Vector4(spread.x, spread.y, spread.z, 0));
            }
            MeshRenderer.SetPropertyBlock(PropertyBlock);
        }

        private void OnDestroy()
        {
            PrismTimerManager.Instance?.CancelScheduledActions(this);
        }
    }
}