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
        public float AnimationProgress { get; set; } = 1f;
        public Action OnAnimationComplete { get; set; }

        public Color StartBrightColor { get; private set; }
        public Color TargetBrightColor { get; private set; }
        public Color StartDarkColor { get; private set; }
        public Color TargetDarkColor { get; private set; }
        public Vector3 StartSpread { get; private set; }
        public Vector3 TargetSpread { get; private set; }

        // float4 mirrors of the color endpoints, converted ONCE when the animation
        // is (re)targeted — MaterialStateManager lerps these every animated frame,
        // and the old per-frame property-read + Color→float4 conversions (8 per
        // animator per frame) were pure constant overhead in its fused pass.
        internal Unity.Mathematics.float4 StartBright4 { get; private set; }
        internal Unity.Mathematics.float4 TargetBright4 { get; private set; }
        internal Unity.Mathematics.float4 StartDark4 { get; private set; }
        internal Unity.Mathematics.float4 TargetDark4 { get; private set; }

        // Last colors actually displayed, written by MaterialStateManager each
        // animated frame. This is the interruption start-state for BOTH render
        // paths — the entity path has no MaterialPropertyBlock to read back from.
        public Color CurrentBrightColor { get; internal set; }
        public Color CurrentDarkColor { get; internal set; }
        public Vector3 CurrentSpread { get; internal set; }

        /// <summary>Owning prism — MaterialStateManager routes animated colors to
        /// its companion render entity when the instanced path is active.</summary>
        internal Prism CachedPrism => cachedPrism;

        public float Duration { get; private set; }
        
        private bool isAnimating;
        public bool IsAnimating
        {
            get => isAnimating;
            set
            {
                if (isAnimating != value)
                {
                    isAnimating = value;
                    if (isAnimating)
                    {
                        MaterialStateManager.Instance?.OnAnimatorStartAnimating(this);
                    }
                    else
                    {
                        MaterialStateManager.Instance?.OnAnimatorStopAnimating(this);
                    }
                }
            }
        }

        private Material activeTransparentMaterial;
        private Material activeOpaqueMaterial;
        private bool isRegistered;
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
            TryRegisterWithManager();
        }

        private void Start()
        {
            if (!isRegistered)
            {
                TryRegisterWithManager();
            }
        }

        private void TryRegisterWithManager()
        {
            if (MaterialStateManager.Instance != null && !isRegistered)
            {
                MaterialStateManager.Instance.RegisterAnimator(this);
                isRegistered = true;
            }
        }

        private void OnEnable()
        {
            TryRegisterWithManager();
        }

        private void OnDisable()
        {
            if (MaterialStateManager.Instance != null && isRegistered)
            {
                MaterialStateManager.Instance.UnregisterAnimator(this);
                isRegistered = false;
            }

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
        // When live, a transition is: bind the END-STATE material immediately
        // (gameplay-final-at-start — the entity's per-instance overrides snap to
        // the new material's authored values, which ARE the lerp targets), stamp
        // {t0, duration, start colors} once, and schedule ONE settle at t0+dur.
        // MaterialStateManager is never engaged for clock transitions.
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

            if (TryClockColorTransition(transparentMaterial, opaqueMaterial, duration, onComplete))
                return;

            // If already animating, capture current state as start state. Uses the
            // manager-tracked current values (works for both the MPB path and the
            // entity path, which has no property block to read back).
            if (IsAnimating)
            {
                StartBrightColor = CurrentBrightColor;
                StartDarkColor = CurrentDarkColor;
                StartSpread = CurrentSpread;
            }
            else
            {
                var currentMaterial = MeshRenderer.sharedMaterial;
                StartBrightColor = currentMaterial.GetColor(BrightColorId);
                StartDarkColor = currentMaterial.GetColor(DarkColorId);
                StartSpread = currentMaterial.GetVector(SpreadId);
            }

            // Seed the tracked currents so an interruption before the first
            // animated frame still has a valid start state.
            CurrentBrightColor = StartBrightColor;
            CurrentDarkColor = StartDarkColor;
            CurrentSpread = StartSpread;

            // Set target values
            TargetBrightColor = transparentMaterial.GetColor(BrightColorId);
            TargetDarkColor = transparentMaterial.GetColor(DarkColorId);
            TargetSpread = transparentMaterial.GetVector(SpreadId);

            // One-time conversions for the manager's per-frame lerp.
            StartBright4 = new Unity.Mathematics.float4(StartBrightColor.r, StartBrightColor.g, StartBrightColor.b, StartBrightColor.a);
            TargetBright4 = new Unity.Mathematics.float4(TargetBrightColor.r, TargetBrightColor.g, TargetBrightColor.b, TargetBrightColor.a);
            StartDark4 = new Unity.Mathematics.float4(StartDarkColor.r, StartDarkColor.g, StartDarkColor.b, StartDarkColor.a);
            TargetDark4 = new Unity.Mathematics.float4(TargetDarkColor.r, TargetDarkColor.g, TargetDarkColor.b, TargetDarkColor.a);

            Duration = duration;
            AnimationProgress = 0f;
            IsAnimating = true;
            OnAnimationComplete = () =>
            {
                activeTransparentMaterial = transparentMaterial;
                activeOpaqueMaterial = opaqueMaterial;
                
                if (MeshRenderer != null && cachedPrism != null &&
                    cachedPrism.prismProperties != null)
                {
                    MeshRenderer.sharedMaterial = cachedPrism.prismProperties.IsTransparent ?
                        transparentMaterial : opaqueMaterial;
                    cachedPrism.SyncRenderMaterial();
                }

                onComplete?.Invoke();
            };
        }

        /// <summary>
        /// The clock-material path of <see cref="UpdateMaterial"/>. Returns false
        /// (caller falls back to the legacy MaterialStateManager lerp) when the
        /// clock path is dark, the prism renders through the GameObject fallback,
        /// or the entity stamp fails.
        /// </summary>
        bool TryClockColorTransition(Material transparentMaterial, Material opaqueMaterial, float duration, Action onComplete)
        {
            if (!PrismRenderService.ClockAnimationEnabled) return false;
            if (cachedPrism == null || !cachedPrism.UsesEntityColorSink) return false;

            float now = PrismClock.Now;

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
            TargetBrightColor = transparentMaterial.GetColor(BrightColorId);
            TargetDarkColor = transparentMaterial.GetColor(DarkColorId);
            TargetSpread = transparentMaterial.GetVector(SpreadId);

            // Gameplay-final-at-start: bind the end-state material NOW. The entity's
            // color overrides snap to its authored values via SyncRenderMaterial
            // (refreshColors — IsAnimating stays false on the clock path).
            activeTransparentMaterial = transparentMaterial;
            activeOpaqueMaterial = opaqueMaterial;
            bool transparent = cachedPrism.prismProperties != null && cachedPrism.prismProperties.IsTransparent;
            MeshRenderer.sharedMaterial = transparent ? transparentMaterial : opaqueMaterial;
            cachedPrism.SyncRenderMaterial();

            if (!PrismRenderService.StampColorTransition(in cachedPrism.RenderHandle, now, duration,
                    PrismRenderService.ToFloat4(StartBrightColor),
                    PrismRenderService.ToFloat4(StartDarkColor),
                    PrismRenderService.ToFloat3(StartSpread)))
            {
                // Entity lost between the sink check and the stamp — the material is
                // already the end state; report unhandled so the legacy lerp runs
                // (it will start from the tracked currents seeded below).
                CurrentBrightColor = StartBrightColor;
                CurrentDarkColor = StartDarkColor;
                CurrentSpread = StartSpread;
                return false;
            }

            _clockColorActive = true;
            _clockColorT0 = now;
            _clockColorDuration = duration;
            _clockFromBright = StartBrightColor;
            _clockFromDark = StartDarkColor;
            _clockFromSpread = StartSpread;

            // Keep the tracked currents sane for handoff paths that read them.
            CurrentBrightColor = StartBrightColor;
            CurrentDarkColor = StartDarkColor;
            CurrentSpread = StartSpread;

            // Touchpoint 3: ONE scheduled settle — clear the stamp (invisible: at
            // t >= end the shader lerp already equals the bound material) and fire
            // the caller's completion.
            var timers = PrismTimerManager.EnsureInstance();
            timers.CancelScheduledActions(this);
            timers.ScheduleAction(this, duration, () =>
            {
                _clockColorActive = false;
                if (cachedPrism != null)
                    PrismRenderService.ClearColorTransitionStamp(in cachedPrism.RenderHandle);
                onComplete?.Invoke();
            });
            return true;
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
            if (IsAnimating)
            {
                PropertyBlock.SetColor(BrightColorId, CurrentBrightColor);
                PropertyBlock.SetColor(DarkColorId, CurrentDarkColor);
                PropertyBlock.SetVector(SpreadId, new Vector4(CurrentSpread.x, CurrentSpread.y, CurrentSpread.z, 0));
            }
            else if (TryGetClockColorCurrent(out var bright, out var dark, out var spread))
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
            if (MaterialStateManager.Instance != null && isRegistered)
            {
                MaterialStateManager.Instance.UnregisterAnimator(this);
                isRegistered = false;
            }
            OnAnimationComplete = null;
            PrismTimerManager.Instance?.CancelScheduledActions(this);
        }
    }
}