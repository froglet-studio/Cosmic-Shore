using CosmicShore.Core;
using CosmicShore.Gameplay;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using Unity.Collections;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects; // EnvironmentColorSetExtensions.ScaleRGB
namespace CosmicShore.Gameplay
{
    [System.Serializable]
    public class CrystalModelData
    {
        public GameObject model;
        public Material defaultMaterial;
        public Material explodingMaterial;
        public Material inactiveMaterial;
        public SpaceCrystalAnimator spaceCrystalAnimator;
    }

    public class Crystal : CellItem
    {
        [Inject] AudioSystem audioSystem;

        #region Inspector Fields
        [SerializeField]
        CellRuntimeDataSO cellData;
        
        [SerializeField] 
        public CrystalProperties crystalProperties;
        
        [SerializeField] float sphereRadius = 100;
        public float SphereRadius => sphereRadius;

        [SerializeField] protected GameObject SpentCrystalPrefab;

        [SerializeField] protected List<CrystalModelData> crystalModels;
        [SerializeField] protected bool allowVesselImpactEffect = true;
        [SerializeField] bool allowRespawnOnImpact;

        [SerializeField]
        [Tooltip("Seconds the collectability colour takes to cross-fade when this crystal's " +
                 "state changes - a lifeform's heart going blue → lime as death drops it. " +
                 "Matches the prism domain-change transition. 0 snaps.")]
        float colorTransitionSeconds = 0.8f;

        [Header("Data Containers")]
        [SerializeField] protected ThemeManagerDataContainerSO _themeManagerData;

        #endregion
        
        public List<CrystalModelData> CrystalModels => crystalModels;

        public CrystalManager CrystalManager { get; protected set; }
        public bool IsExploding { get; private set; }

        // ── Embedded lifeform heart ──────────────────────────────────────────
        // While a lifeform is alive its elemental crystal rides INSIDE the body (the heart).
        // SetEmbeddedIn enables the crystal's collider so a vessel can JOUST the heart (the
        // Squirrel's joust: destroys opposing-domain lifeforms when moving faster; its Space
        // level-5 upgrade levels up allies instead), while the impact chain gates on IsEmbedded
        // so an embedded heart is never skim-collected or treated as a free-floating pickup.
        // ActivateCrystal (death) clears it - the crystal then drops as the normal collectible
        // powerup (mass conserved).

        /// <summary>The living lifeform (flora or fauna) this crystal is embedded in; null once dropped/free.</summary>
        public ILifeFormEntity EmbeddedIn { get; private set; }

        /// <summary>True while this crystal is a living lifeform's heart (not yet dropped).</summary>
        public bool IsEmbedded => EmbeddedIn != null;

        // The embedded heart's trigger is INFLATED so a vessel passing through the creature
        // reliably clips it at flight speed (the authored radius is a pickup hitbox, tiny and
        // buried inside the body). Restored to the authored radius when the crystal drops.
        const float EmbeddedHeartRadiusMultiplier = 2.5f;
        float _authoredColliderRadius = -1f;

        /// <summary>
        /// Marks this crystal as a living lifeform's heart and enables its collider (inflated,
        /// so the joust reliably lands) so vessels can joust it. Called by lifeforms right
        /// after LifeFormCrystal.EnsureElementalCrystal.
        ///
        /// This is also where a heart gets its SIZE: every lifeform heart in the game passes
        /// through here, so sizing it from the owner's level here is what makes the size a
        /// function of level alone rather than of whatever scale each species' prefab happened
        /// to author (Docs/ECOSYSTEM.md §33). Later level changes re-apply the same curve.
        /// </summary>
        public void SetEmbeddedIn(ILifeFormEntity owner)
        {
            EmbeddedIn = owner;
            if (owner != null) LifeFormCrystal.ApplyLevelSize(this, owner.Level);
            var col = GetComponent<SphereCollider>();
            if (!col) return;
            if (_authoredColliderRadius < 0f) _authoredColliderRadius = col.radius;
            col.radius = _authoredColliderRadius * (owner != null ? EmbeddedHeartRadiusMultiplier : 1f);
            col.enabled = owner != null;
            ApplyColorSetTint(); // heart = blue-white neutral while it lives
        }

        // ── Active-crystal registry ──────────────────────────────────────────
        // Lets systems (e.g. HexRaceObjectiveProvider) enumerate live crystals without a
        // per-call FindObjectsByType scene scan. Maintained via OnEnable/OnDisable so it
        // works for both pooled (SetActive) and Instantiate/Destroy lifecycles.
        static readonly List<Crystal> s_active = new();

        /// <summary>Live crystals currently enabled in the scene. Read-only - do not mutate.</summary>
        public static IReadOnlyList<Crystal> Active => s_active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() => s_active.Clear();

        protected virtual void OnDestroy()
        {
            // Pair every PrismTimerManager.ScheduleAction with a cancel (see the tint transition
            // block below) - the manager does not deduplicate per owner.
            PrismTimerManager.Instance?.CancelScheduledActions(this);

            // LEAVE THE CELL'S LISTS. DestroyCrystal() used to be the only thing that did this, so
            // a crystal destroyed ANY OTHER WAY - its root torn down, a scene closed, a preview
            // arena struck - stayed in CellItems and Crystals forever. Those live on a
            // ScriptableObject ASSET, which outlives every scene, so the dangling entry is
            // permanent for the session and surfaces later as a MissingReferenceException in
            // whoever iterates next (AIPilot.UpdateCellContent, the boid controller, the
            // SnowChanger). Removing here covers every route by construction.
            if (cellData) cellData.TryRemoveItem(this);
        }

        protected virtual void OnEnable()
        {
            if (!s_active.Contains(this)) s_active.Add(this);
        }

        protected virtual void OnDisable()
        {
            s_active.Remove(this);

            // Land the end pair rather than freezing part-way: the driver dies with the
            // component and the scheduled settle is cancelled with it, so a crystal disabled
            // mid-fade would otherwise keep a half-lerped colour when it comes back.
            if (_tintTransitionActive)
            {
                var bright = _tintToBright;
                var dull = _tintToDull;
                StopTintTransition();
                WriteTint(bright, dull);
            }
        }

        protected virtual void Start()
        {
            crystalProperties.crystalValue = crystalProperties.fuelAmount * transform.lossyScale.x;
            ApplyColorSetTint();
        }

        // ── Color set (single source: SO_ColorSet via the theme container) ───
        // Crystal COLOR signals WHO can collect it (element identity is shape, never color):
        //   • domain crystal (Jade/Ruby/Gold)  → that domain's crystal colors - only it collects
        //   • embedded lifeform heart          → blue-white neutral (BlueColors) - nobody collects
        //   • free pickup (drop / omni / cell) → lime CTA (EnvironmentColors) - anyone collects
        // Applies to omni and all four elemental crystals. Colors come LIVE from the theme's
        // ColorSet, per-renderer via MaterialPropertyBlock - never renderer.material clones.

        static MaterialPropertyBlock s_tintBlock;

        // The property every crystal shader exposes for its dissolve - the same one FadeIn drives
        // to bloom a crystal IN (CrystalGraph, ChargeCrystal). A capture drives it back to 0.
        static readonly int OpacityID = Shader.PropertyToID("_opacity");

        /// <summary>The (bright, dull) pair this crystal's CURRENT state resolves to. False when
        /// the theme container or its color set is unwired - the caller keeps whatever it had.</summary>
        bool TryResolveTintColors(out Color bright, out Color dull)
        {
            bright = default;
            dull = default;
            if (!_themeManagerData || _themeManagerData.ColorSet == null) return false;
            var colors = _themeManagerData.ColorSet;

            bool domainOwned = ownDomain is Domains.Jade or Domains.Ruby or Domains.Gold;
            if (domainOwned && colors.TryGetColorSetByDomain(ownDomain, out var domainSet) && domainSet != null)
            {
                bright = domainSet.BrightCrystalColor;
                dull = domainSet.DullCrystalColor;
                return true;
            }

            if (IsEmbedded)
            {
                // A living lifeform's heart: the blue-white neutral range - no domain can take it.
                if (!colors.TryGetColorSetByDomain(Domains.Blue, out var neutralSet) || neutralSet == null) return false;
                bright = neutralSet.BrightCrystalColor;
                dull = neutralSet.DullCrystalColor;
                return true;
            }

            // Free collectible: the lime CTA - any domain can collect it now.
            if (colors.EnvironmentColors == null) return false;
            bright = colors.EnvironmentColors.BrightCTA;
            dull = colors.EnvironmentColors.DarkCTA;

            // The OMNI is the hero pickup and wears the CTA at full strength; the four
            // elementals ride the same lime, dimmed, so the omni is the brightest crystal
            // on screen. Brightness is the ONLY difference - a scalar cannot move the hue,
            // so all five stay in one lime family by construction.
            if (crystalProperties.IsElemental)
            {
                float dim = colors.EnvironmentColors.ElementalCrystalDimming;
                bright = bright.ScaleRGB(dim);
                dull = dull.ScaleRGB(dim);
            }
            return true;
        }

        /// <summary>
        /// Paints all crystal models with the current state's pair immediately. No-op when the
        /// theme container or color set is unwired.
        ///
        /// This is the RE-ASSERT path - a birth, a domain preview, a material lerp settling -
        /// where the pair being written is the one already showing, so there is nothing to
        /// travel. A state change the player should SEE goes through
        /// <see cref="TransitionColorSetTint"/> instead.
        /// </summary>
        protected void ApplyColorSetTint()
        {
            if (!TryResolveTintColors(out var bright, out var dull)) return;

            // A transition already heading for this pair owns the block, so do not snap it to the
            // end - re-assert what is on screen instead. Two re-asserts land mid-flight: Start,
            // one frame after ActivateCrystal enables the component, and the material lerp
            // reaching its tail.
            if (_tintTransitionActive && bright == _tintToBright && dull == _tintToDull)
            {
                if (TryGetDisplayedTint(out var liveBright, out var liveDull))
                    WriteTint(liveBright, liveDull);
                return;
            }

            StopTintTransition();
            WriteTint(bright, dull);
        }

        /// <summary>
        /// Cross-fades to the current state's pair from an EXPLICIT start pair, over
        /// <paramref name="seconds"/>. For a caller that has to read the displayed colours before
        /// it disturbs them - <see cref="ActivateCrystal"/> binds new materials and drops the
        /// block before the new state is painted, the same ordering constraint
        /// MaterialPropertyAnimator has when it reads start colours before binding the end-state
        /// material. Snaps when there is nothing to travel.
        /// </summary>
        void TransitionColorSetTint(Color fromBright, Color fromDull, float seconds)
        {
            if (!TryResolveTintColors(out var bright, out var dull)) return;

            if (seconds > 0f && isActiveAndEnabled && (fromBright != bright || fromDull != dull))
            {
                StartTintTransition(fromBright, fromDull, bright, dull, seconds);
                return;
            }

            StopTintTransition();
            WriteTint(bright, dull);
        }

        // ── Clock-timed tint transitions ─────────────────────────────────────
        // The same shape as a prism domain change (MaterialPropertyAnimator.ClockColorTransition,
        // Docs/PRISM_ANIMATION.md): the STATE goes final at the start - a dropped heart is
        // collectable the instant it drops, colour is only how it READS - the start pair is
        // stamped ONCE against PrismClock, every pair in between is computed analytically from
        // that stamp rather than accumulated, and PrismTimerManager fires ONE settle at the
        // analytically-known end. An interruption re-stamps from the analytic current, so a
        // second state change mid-fade departs from what is actually on screen.
        //
        // It differs from the prism path in ONE respect, deliberately. A prism hands the
        // interpolation to the GPU because thousands animate at once and its graphs carry the
        // clock wiring; the crystal shaders carry none (Docs/PALETTE.md section 2.2 audits all
        // of them), so a crystal's pair is pushed from the CPU. That is bounded by the crystals
        // actually TRANSITIONING - a heart changes colour once, when it dies - and is cheaper
        // than the cloned-material lerp it runs alongside. The scheduled settle is what makes
        // the end state independent of the driver: interrupt the coroutine and the crystal still
        // lands on its final colour.

        bool _tintTransitionActive;
        float _tintT0, _tintDuration;
        Color _tintFromBright, _tintFromDull;
        Color _tintToBright, _tintToDull;
        Coroutine _tintDriver;

        // The pair last written to the block - the resting displayed value, and the start state
        // a transition departs from.
        bool _tintWritten;
        Color _tintBright, _tintDull;

        /// <summary>Analytic displayed pair of an in-flight transition - computed on demand from
        /// the stamp, never tracked per frame. False once the transition has run its course.</summary>
        bool TryGetTransitionCurrent(out Color bright, out Color dull)
        {
            bright = default;
            dull = default;
            if (!_tintTransitionActive || _tintDuration <= 0f) return false;

            float now = PrismClock.Now;
            if (now >= _tintT0 + _tintDuration) return false;

            float p = Mathf.Clamp01((now - _tintT0) / _tintDuration);
            float t = p * p * (3f - 2f * p); // smoothstep - matches the prism color lerp
            bright = Color.Lerp(_tintFromBright, _tintToBright, t);
            dull = Color.Lerp(_tintFromDull, _tintToDull, t);
            return true;
        }

        /// <summary>What is on screen right now: the analytic current of a live transition, else
        /// the resting pair. False when this crystal has never been tinted.</summary>
        bool TryGetDisplayedTint(out Color bright, out Color dull)
        {
            if (TryGetTransitionCurrent(out bright, out dull)) return true;
            bright = _tintBright;
            dull = _tintDull;
            return _tintWritten;
        }

        void StartTintTransition(Color fromBright, Color fromDull, Color toBright, Color toDull, float duration)
        {
            StopTintTransition();

            _tintTransitionActive = true;
            _tintT0 = PrismClock.Now;
            _tintDuration = duration;
            _tintFromBright = fromBright;
            _tintFromDull = fromDull;
            _tintToBright = toBright;
            _tintToDull = toDull;

            WriteTint(fromBright, fromDull); // frame 0 is the start pair - never a flash of the end
            _tintDriver = StartCoroutine(DriveTintTransition());
            PrismTimerManager.EnsureInstance().ScheduleAction(this, duration, SettleTint);
        }

        IEnumerator DriveTintTransition()
        {
            while (TryGetTransitionCurrent(out var bright, out var dull))
            {
                WriteTint(bright, dull);
                yield return null;
            }
            _tintDriver = null;
        }

        /// <summary>The ONE scheduled settle: land the end pair and drop the stamp.</summary>
        void SettleTint()
        {
            if (!_tintTransitionActive) return;
            _tintTransitionActive = false;
            _tintDriver = null;
            WriteTint(_tintToBright, _tintToDull);
        }

        void StopTintTransition()
        {
            if (_tintDriver != null)
            {
                StopCoroutine(_tintDriver);
                _tintDriver = null;
            }
            if (!_tintTransitionActive) return;
            _tintTransitionActive = false;
            PrismTimerManager.Instance?.CancelScheduledActions(this);
        }

        /// <summary>Pushes one pair to every model's property block, and records it as the
        /// resting displayed value. <paramref name="opacity01"/> additionally drives the shader
        /// dissolve (the capture's absorb) - omit it and the material's own opacity stands.</summary>
        void WriteTint(Color bright, Color dull, float? opacity01 = null)
        {
            if (crystalModels == null) return;

            _tintBright = bright;
            _tintDull = dull;
            _tintWritten = true;

            s_tintBlock ??= new MaterialPropertyBlock();
            foreach (var modelData in crystalModels)
            {
                if (modelData?.model == null || !modelData.model.TryGetComponent<Renderer>(out var renderer)) continue;
                var mat = renderer.sharedMaterial;
                if (!mat) continue;
                var props = FindColorPropertyNames(mat);

                // A shader with no colour pair still has to receive the dissolve, so the
                // colour write is skipped rather than the whole renderer.
                if (props.bright == null && !opacity01.HasValue) continue;

                renderer.GetPropertyBlock(s_tintBlock);
                if (props.bright != null)
                {
                    s_tintBlock.SetColor(props.bright, bright);
                    s_tintBlock.SetColor(props.dull, dull);
                }
                if (opacity01.HasValue) s_tintBlock.SetFloat(OpacityID, opacity01.Value);
                renderer.SetPropertyBlock(s_tintBlock);
            }
        }

        // ── Capture ──────────────────────────────────────────────────────────
        // A capture SUPERSEDES collectability signalling. The crystal is being taken, so a
        // blue → lime crossing still in flight has nothing left to say, and - decisively - two
        // writers on one property block would fight frame by frame. This is not a rare overlap:
        // the Squirrel's joust frees a heart and auto-collects it in the same beat, so the
        // crossing is ALWAYS live when that capture starts.
        //
        // BeginCaptureVisual freezes the crossing where the eye last saw it and latches that pair
        // as the flare's base, so the capture departs from what is on screen rather than snapping
        // to the colour the crossing was heading for.

        bool _captureHasColors;
        Color _captureBaseBright, _captureBaseDull;

        /// <summary>
        /// Hands the tint block to the capture: stops any in-flight collectability cross-fade and
        /// latches the DISPLAYED pair as the base every later
        /// <see cref="ApplyCaptureVisual"/> frame flares from. Call once, at the moment of collection.
        /// </summary>
        public void BeginCaptureVisual()
        {
            // Read what is on screen BEFORE stopping the crossing - StopTintTransition drops the
            // stamp the analytic current is computed from.
            _captureHasColors = TryGetDisplayedTint(out _captureBaseBright, out _captureBaseDull)
                             || TryResolveTintColors(out _captureBaseBright, out _captureBaseDull);
            StopTintTransition();
        }

        /// <summary>
        /// Paints one frame of a CAPTURE: the latched base colours scaled by
        /// <paramref name="flareMultiplier"/> (a brightness flare in linear HDR - the element's hue
        /// survives, per Docs/PALETTE.md, where washing toward white would read as a different
        /// crystal) and the opacity driven to <paramref name="opacity01"/> for the dissolve that
        /// ends the absorb.
        ///
        /// Goes through <see cref="WriteTint"/> like every other tint write, so the block keeps ONE
        /// writer and the recorded resting pair stays honest. Nothing is restored afterwards
        /// because the crystal is destroyed at the end of the capture.
        /// </summary>
        public void ApplyCaptureVisual(float flareMultiplier, float opacity01)
        {
            if (_captureHasColors)
                WriteTint(_captureBaseBright.ScaleRGB(flareMultiplier),
                          _captureBaseDull.ScaleRGB(flareMultiplier), opacity01);
            else
                WriteTint(_tintBright, _tintDull, opacity01);
        }

        /// <summary>Clears the tint override on one model so a material color lerp is visible.
        /// The block no longer describes what is on screen after this, so the resting pair is
        /// forgotten too - a later cross-fade would otherwise depart from a colour that has not
        /// been displayed since the clear.</summary>
        void ClearColorSetTint(GameObject model)
        {
            _tintWritten = false;
            if (model && model.TryGetComponent<Renderer>(out var renderer))
                renderer.SetPropertyBlock(null);
        }

        public void InjectDependencies(CrystalManager cm) => CrystalManager = cm;

        public bool CanBeCollected(Domains shipDomain) => ownDomain == Domains.Blue || ownDomain == shipDomain;

        /// <summary>
        /// Paints this crystal in <paramref name="domain"/>'s crystal colours immediately, with
        /// none of the material lerp <see cref="ChangeDomain"/> plays. Used by the deploy PREVIEW
        /// (the Dolphin's ghost crystal) so it announces whose it will be the moment it blooms,
        /// instead of reading as the lime free-for-all pickup it is not going to be.
        ///
        /// Safe while the component is disabled: the tint rides a MaterialPropertyBlock over the
        /// shared material, so it needs no coroutine and clones nothing.
        /// </summary>
        public void ApplyDomainPreview(Domains domain)
        {
            ownDomain = domain;
            ApplyColorSetTint();
        }

        public struct ExplodeParams
        {
            public Vector3 Course;
            public float Speed;
            public FixedString64Bytes PlayerName;
        }

        public void NotifyManagerToExplodeCrystal(ExplodeParams explodeParams) =>
            CrystalManager.ExplodeCrystal(Id, explodeParams);
        
        public void Respawn()
        {
            // A manager-less mint (e.g. the freestyle conveyor toy's local pickups) has no manager
            // to respawn through - collect once and destroy.
            if (!allowRespawnOnImpact || CrystalManager == null)
            {
                DestroyCrystal();
                return;
            }

            CrystalManager.RespawnCrystal(Id);
        }

        public void DestroyCrystal()
        {
            if (cellData) cellData.TryRemoveItem(this);
            Destroy(gameObject);
        }
        
        public void DeactivateModels()
        {
            foreach (var model in crystalModels)
            {
                model.model.SetActive(true);
                model.model.GetComponent<FadeIn>().StartFadeIn();
            }
        }

        public void MoveToNewPos(Vector3 newPos)
        {
            transform.SetPositionAndRotation(newPos, Quaternion.identity);
        }

        public void Vacuum(Vector3 newPosition, float vaccumAmount)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                newPosition,
                vaccumAmount * Time.deltaTime / transform.lossyScale.x);
        }

        //the following is a public method that can be called to grow the crystal
        public void GrowCrystal(float duration, float targetScale)
        {
            StartCoroutine(Grow(duration, targetScale));
        }

        // the following grow coroutine is used to grow the crystal when it changes size
        IEnumerator Grow(float duration, float targetScale)
        {
            float elapsedTime = 0.0f;
            Vector3 startScale = transform.localScale;
            Vector3 targetScaleVector = new Vector3(targetScale, targetScale, targetScale);

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / duration);

                transform.localScale = Vector3.Lerp(startScale, targetScaleVector, t);

                yield return null;
            }

            transform.localScale = targetScaleVector;
        }
        
        /// <summary>
        /// Sprays this crystal's spent husk along the collector's course.
        ///
        /// <paramref name="huskScale"/> overrides the size the husk is spawned at; the default
        /// (zero) uses the crystal's own live scale. The override exists for the ELEMENTAL capture,
        /// which fires this burst at the end of a flight that has already shrunk the crystal into
        /// the hull - without it the payoff would be sized by the flourish that preceded it rather
        /// than by the crystal the pilot actually picked up. The networked path takes the default.
        /// </summary>
        public void Explode(ExplodeParams explodeParams, Vector3 huskScale = default)
        {
            if (IsExploding)
                return;

            IsExploding = true;
            WaitForImpact().Forget();

            if (huskScale == default) huskScale = transform.lossyScale;
            var playerName = explodeParams.PlayerName.ToString();
            foreach (var modelData in crystalModels)
            {
                var model = modelData.model;

                // Pooled husk checkout — no Instantiate or material clone in the
                // pickup frame. Impact animates its shader state per-renderer via
                // MaterialPropertyBlock over the shared exploding material.
                var impact = SpentCrystalPoolManager.GetPooledOrInstantiate(
                    SpentCrystalPrefab, transform.position, transform.rotation);
                if (!impact) continue;

                impact.transform.localScale = huskScale;

                if (crystalProperties.Element == Element.Space && modelData.spaceCrystalAnimator != null)
                {
                    var spentAnimator = impact.GetComponent<SpaceCrystalAnimator>();
                    var thisAnimator = model.GetComponent<SpaceCrystalAnimator>();
                    if (spentAnimator && thisAnimator)
                        spentAnimator.timer = thisAnimator.timer;
                }

                impact.HandleImpact(
                    explodeParams.Course * explodeParams.Speed, modelData.explodingMaterial, playerName);
            }

            PlayExplosionAudio();
        }

        /// <summary>
        /// The pickup sound. Falls back to <see cref="AudioSystem.Instance"/> because the injected
        /// field is null on every crystal that was NOT part of a loaded scene: a lifeform's heart
        /// is `Instantiate`d by the cell's spawners, and nothing under Controller/Environment calls
        /// `GameObjectInjector.InjectRecursive`. So this was a silent no-op for the entire ecology's
        /// crystal drops - which is most of the crystals in the game - while reading as wired.
        /// Same accessor the elemental powerup effect already uses one frame earlier.
        /// </summary>
        void PlayExplosionAudio()
        {
            var audio = audioSystem != null ? audioSystem : AudioSystem.Instance;
            if (audio != null)
                audio.PlayGameplaySFX(GameplaySFXCategory.CrystalCollect, transform.position);
        }

        /// <summary>
        /// Moves this heart out of its dying owner and onto the cell WITHOUT freeing it: it stays
        /// <see cref="IsEmbedded"/>, so it remains uncollectable and keeps the neutral heart tint.
        ///
        /// Used by a progressive wither, where the heart is the LAST thing standing and only
        /// becomes collectable when the wither reaches the core (Docs/ECOSYSTEM.md §26). Re-homing
        /// it at the START of the death is what makes that deferral safe: a crystal still parented
        /// to the husk cannot be rescued once the husk is destroyed, so an interrupted wither
        /// (a cell drain, a manager removing the husk) would silently lose it and break the
        /// every-lifeform-drops-a-crystal invariant. Reparenting preserves world pose, and a
        /// withering creature holds still, so the heart does not appear to move.
        /// </summary>
        public void DetachHeartToCell()
        {
            if (cellData && cellData.Cell)
                transform.parent = cellData.Cell.transform;
        }

        public void ActivateCrystal()
        {
            // The pair on screen RIGHT NOW is where the heart → pickup cross-fade departs from,
            // and it has to be read before anything below disturbs it: clearing EmbeddedIn
            // changes what the state resolves to, and each material lerp drops the block on its
            // way in. Same ordering constraint as MaterialPropertyAnimator's start colors.
            bool hadTint = TryGetDisplayedTint(out var fromBright, out var fromDull);

            EmbeddedIn = null; // no longer a living heart - it's a free collectible now
            transform.parent = cellData.Cell.transform;
            var dropCol = gameObject.GetComponent<SphereCollider>();
            if (_authoredColliderRadius > 0f) dropCol.radius = _authoredColliderRadius;
            dropCol.enabled = true;
            enabled = true;

            for (int i = 0; i < crystalModels.Count; i++)
            {
                var modelData = crystalModels[i];
                var model = modelData.model;

                model.GetComponent<Renderer>().material = modelData.inactiveMaterial;
                StartCoroutine(LerpCrystalMaterialCoroutine(model, ResolveActivationMaterial(modelData, i)));
            }

            // The state just flipped from "living heart" to "collectible", so repaint NOW - after
            // the lerps above, which drop the block on their way in. A lifeform's heart crosses
            // blue → lime here, which is the signal that it can be picked up, so it TRAVELS: one
            // clock-stamped cross-fade rather than a snap. Explicit rather than left to Start
            // (which only fires because a heart's Crystal component is authored disabled) or to
            // the material lerp's tail (which is skipped outright when a model has no target
            // material) - either way the crystal would otherwise stay blue while collectable.
            if (hadTint)
                TransitionColorSetTint(fromBright, fromDull, colorTransitionSeconds);
            else
                ApplyColorSetTint();
        }

        /// <summary>
        /// The material a crystal settles into as it activates. A DOMAIN-OWNED crystal settles into
        /// that domain's crystal material - the same look Skim Race's track crystals wear (they
        /// reach it via <see cref="SpawnableCrystal"/> → <see cref="ChangeDomain"/>) - because
        /// crystal colour is what tells a pilot who may collect it. An unowned crystal keeps its
        /// authored neutral material and stays free for anyone.
        /// </summary>
        Material ResolveActivationMaterial(CrystalModelData modelData, int index)
        {
            bool domainOwned = ownDomain is Domains.Jade or Domains.Ruby or Domains.Gold;
            if (!domainOwned || !_themeManagerData) return modelData.defaultMaterial;

            // Unity's implicit bool, not ?? - a destroyed material is FAKE-null, which ?? happily
            // passes through, and the lerp would then bail and strand the crystal on inactiveMaterial.
            var teamMaterial = _themeManagerData.GetTeamCrystalMaterial(ownDomain, index);
            return teamMaterial ? teamMaterial : modelData.defaultMaterial;
        }

        public void ChangeDomain(Domains newDomain, float duration = -1)
        {
            if (ownDomain == newDomain)
                return;
            if (newDomain == Domains.Blue)
            {
                for (int i = 0; i < crystalModels.Count; i++)
                {
                    StartCoroutine(LerpCrystalMaterialCoroutine(crystalModels[i].model, crystalModels[i].defaultMaterial, 1));
                }
                ownDomain = newDomain;
                return;
            }
            ownDomain = newDomain;
            for (int i = 0; i < crystalModels.Count; i++)
            {
                StartCoroutine(LerpCrystalMaterialCoroutine(crystalModels[i].model, _themeManagerData.GetTeamCrystalMaterial(ownDomain, i), 1));
            }
            if (duration != -1) StartCoroutine(DecayingTheftCoroutine(duration));
        }

        IEnumerator DecayingTheftCoroutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            ChangeDomain(Domains.Blue);
        }

        protected IEnumerator LerpCrystalMaterialCoroutine(GameObject model, Material targetMaterial, float lerpDuration = 2f)
        {
            Renderer renderer = model.GetComponent<Renderer>();
            if (renderer == null)
                yield break;

            // The theme's per-domain material sets are populated at runtime by ThemeManager; a
            // crystal minted before that (or in a scene with no theme) has nothing to lerp TO.
            // Keep the authored material rather than nulling the renderer out.
            if (targetMaterial == null)
                yield break;

            // The property-block tint would override the animated material colors - drop it for
            // the lerp; the current domain's tint is reapplied once the material settles.
            ClearColorSetTint(model);

            Material tempMaterial = new Material(renderer.material);
            renderer.material = tempMaterial;

            // Detect which color property names the source and target shaders use.
            // Regular crystal shaders use _BrightCrystalColor/_DullCrystalColor,
            // while InverseDynamicFresnelGraph uses _BrightColor/_DullColor.
            var srcProps = FindColorPropertyNames(tempMaterial);
            var dstProps = FindColorPropertyNames(targetMaterial);

            bool canLerp = srcProps.bright != null && dstProps.bright != null
                           && srcProps.bright == dstProps.bright;

            if (canLerp)
            {
                Color startBright = tempMaterial.GetColor(srcProps.bright);
                Color startDull = tempMaterial.GetColor(srcProps.dull);
                Color targetBright = targetMaterial.GetColor(dstProps.bright);
                Color targetDull = targetMaterial.GetColor(dstProps.dull);

                float elapsedTime = 0.0f;
                while (elapsedTime < lerpDuration)
                {
                    elapsedTime += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsedTime / lerpDuration);

                    tempMaterial.SetColor(srcProps.bright, Color.Lerp(startBright, targetBright, t));
                    tempMaterial.SetColor(srcProps.dull, Color.Lerp(startDull, targetDull, t));

                    yield return null;
                }
            }

            renderer.material = targetMaterial;

            // Update the explodingMaterial for the matching crystal model entry
            for (int i = 0; i < crystalModels.Count; i++)
            {
                if (crystalModels[i].model == model)
                {
                    crystalModels[i].explodingMaterial = targetMaterial;
                    break;
                }
            }

            Destroy(tempMaterial);
            ApplyColorSetTint();
        }

        private static (string bright, string dull) FindColorPropertyNames(Material mat)
        {
            if (mat.HasProperty("_BrightCrystalColor") && mat.HasProperty("_DullCrystalColor"))
                return ("_BrightCrystalColor", "_DullCrystalColor");
            if (mat.HasProperty("_BrightColor") && mat.HasProperty("_DullColor"))
                return ("_BrightColor", "_DullColor");
            return (null, null);
        }
        
        /// <summary>
        /// This is to forbid multiple impacts due to multiple vessel colliders
        /// </summary>
        async UniTask WaitForImpact()
        {
            await UniTask.WaitForSeconds(0.5f);
            IsExploding = false;
        }
    }
}