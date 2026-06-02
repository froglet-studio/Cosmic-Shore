using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Three-wedge volume indicator that lives on the pause / freestyle-toggle button.
    /// Each wedge fills 1/3 of a circle (120°), one per playable Domain (Jade, Ruby,
    /// Gold). The wedge's fillAmount tracks the domain's per-cell prism count
    /// normalized to the cell's Rabid-enter threshold — so when the SUM of the three
    /// wedges visually approaches a full circle, the cell is about to enter Level2
    /// aggression (frenzy).
    ///
    /// Reads:
    ///   - <see cref="Cell.GetDomainBlockCount"/> for the per-domain mass signal
    ///   - <see cref="Cell.RabidEnterThreshold"/> for the "max" (frenzy point)
    /// Resolves the cell via <see cref="Cell.FindCellContaining"/> using the local
    /// player's vessel position, falling back to the first active cell — so this
    /// works in Menu_Main (single cell at origin) and gameplay scenes without per-
    /// scene rewiring.
    ///
    /// Wedge geometry (per Image): Radial360 fill, centred sprite, optionally rotated
    /// to its 120° start angle (Jade 0°, Ruby -120°, Gold -240°). The component sets
    /// the rotations on enable if <see cref="autoArrangeWedgeRotations"/> is true, so
    /// authoring just needs three overlapping radial Images on the prefab.
    /// </summary>
    [DisallowMultipleComponent]
    public class DomainVolumeIndicator : MonoBehaviour
    {
        [Header("Wedge Images (one per playable domain)")]
        [Tooltip("Radial360 Image — Jade wedge. fillMethod=Radial360, fillOrigin=Top, fillCenter=true.")]
        [SerializeField] Image jadeWedge;
        [Tooltip("Radial360 Image — Ruby wedge.")]
        [SerializeField] Image rubyWedge;
        [Tooltip("Radial360 Image — Gold wedge.")]
        [SerializeField] Image goldWedge;

        [Header("Theme")]
        [Tooltip("Source for per-domain colors. Optional — falls back to the explicit fallback colors below if missing.")]
        [SerializeField] ThemeManagerDataContainerSO themeData;
        [Tooltip("Used as the wedge tint when themeData is missing or the domain lookup fails.")]
        [SerializeField] Color jadeFallback = new(0.20f, 0.85f, 0.55f, 1f);
        [SerializeField] Color rubyFallback = new(0.95f, 0.25f, 0.30f, 1f);
        [SerializeField] Color goldFallback = new(0.95f, 0.80f, 0.20f, 1f);

        [Header("Animation")]
        [Tooltip("Seconds between samples. The cell phase tick is 0.5s; 0.25s keeps the wedge ahead of phase transitions without burning CPU.")]
        [Min(0.05f)] [SerializeField] float sampleIntervalSeconds = 0.25f;
        [Tooltip("Lerp speed for fillAmount changes between samples. 0 = instant; higher = smoother.")]
        [Min(0f)] [SerializeField] float fillLerpSpeed = 8f;
        [Tooltip("If true, sets each wedge's local Z rotation so the three wedges sit at 0/120/240°. Off = use the authored prefab rotations.")]
        [SerializeField] bool autoArrangeWedgeRotations = true;

        [Header("Cell resolution")]
        [Tooltip("If assigned, the indicator reads from this cell directly. Leave null to auto-resolve via the local player's vessel position (or the first active cell as a last resort).")]
        [SerializeField] Cell explicitCell;

        // Each wedge can fill at most a third of the full circle — 120°.
        const float WedgeMaxFill = 1f / 3f;

        [Inject] GameDataSO gameData;

        Cell _cachedCell;
        float _nextSampleAt;
        float _jadeTarget, _rubyTarget, _goldTarget;
        float _jadeNow, _rubyNow, _goldNow;

        // ------------------------------------------------------------------
        //  Lifecycle
        // ------------------------------------------------------------------

        void OnEnable()
        {
            ApplyDomainColors();
            if (autoArrangeWedgeRotations) ArrangeWedgeRotations();

            // Snap to zero on first enable so the indicator doesn't pop in mid-fill.
            _jadeNow = _rubyNow = _goldNow = 0f;
            _jadeTarget = _rubyTarget = _goldTarget = 0f;
            ApplyFillImmediate();
            _nextSampleAt = 0f;
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextSampleAt)
            {
                _nextSampleAt = Time.unscaledTime + sampleIntervalSeconds;
                SampleTargets();
            }
            StepTowardTargets();
        }

        // ------------------------------------------------------------------
        //  Sampling
        // ------------------------------------------------------------------

        void SampleTargets()
        {
            var cell = ResolveCell();
            if (!cell)
            {
                _jadeTarget = _rubyTarget = _goldTarget = 0f;
                return;
            }

            int rabid = cell.RabidEnterThreshold;
            if (rabid <= 0)
            {
                _jadeTarget = _rubyTarget = _goldTarget = 0f;
                return;
            }

            // Each wedge's max angular extent is 1/3 of the circle. Mapping the raw
            // count fraction (count_d / RabidEnter) into that range with a clamp
            // means: when a single domain's mass equals the FULL frenzy threshold,
            // that domain's wedge maxes out (1/3 of the circle). When mass is
            // evenly distributed across domains and totals RabidEnter, the three
            // 1/3 wedges sum to a full circle = visible frenzy.
            _jadeTarget = Mathf.Clamp((float)cell.GetDomainBlockCount(Domains.Jade) / rabid, 0f, WedgeMaxFill);
            _rubyTarget = Mathf.Clamp((float)cell.GetDomainBlockCount(Domains.Ruby) / rabid, 0f, WedgeMaxFill);
            _goldTarget = Mathf.Clamp((float)cell.GetDomainBlockCount(Domains.Gold) / rabid, 0f, WedgeMaxFill);
        }

        void StepTowardTargets()
        {
            if (fillLerpSpeed <= 0f)
            {
                _jadeNow = _jadeTarget;
                _rubyNow = _rubyTarget;
                _goldNow = _goldTarget;
            }
            else
            {
                float t = 1f - Mathf.Exp(-fillLerpSpeed * Time.unscaledDeltaTime);
                _jadeNow = Mathf.Lerp(_jadeNow, _jadeTarget, t);
                _rubyNow = Mathf.Lerp(_rubyNow, _rubyTarget, t);
                _goldNow = Mathf.Lerp(_goldNow, _goldTarget, t);
            }
            ApplyFillImmediate();
        }

        void ApplyFillImmediate()
        {
            if (jadeWedge) jadeWedge.fillAmount = _jadeNow;
            if (rubyWedge) rubyWedge.fillAmount = _rubyNow;
            if (goldWedge) goldWedge.fillAmount = _goldNow;
        }

        // ------------------------------------------------------------------
        //  Cell resolution
        // ------------------------------------------------------------------

        Cell ResolveCell()
        {
            // Explicit override wins — covers single-scene cases (Menu_Main) where a
            // designer wants the indicator tied to one specific cell.
            if (explicitCell) return explicitCell;

            // Cache: cell references are stable in a scene, so a one-frame lookup
            // covers the long run. If a cached cell gets destroyed (round reset),
            // fall through and re-resolve.
            if (_cachedCell) return _cachedCell;

            // Prefer the cell containing the local player's vessel. Falls through to
            // the nearest active cell if the player hasn't spawned yet or is between
            // cells.
            var vessel = gameData?.LocalPlayer?.Vessel;
            if (vessel)
            {
                _cachedCell = Cell.FindCellContaining(vessel.transform.position);
                if (_cachedCell) return _cachedCell;
            }

            _cachedCell = Cell.FindNearestActiveCell(vessel ? vessel.transform.position : Vector3.zero);
            return _cachedCell;
        }

        // ------------------------------------------------------------------
        //  Authoring helpers
        // ------------------------------------------------------------------

        void ApplyDomainColors()
        {
            // SOAP-style fallback: themeData is OPTIONAL; if missing the explicit
            // fallback colors keep the wedges visible. This is a HUD — failing loud
            // here would just blank the indicator on prefab variants that haven't
            // been re-wired yet.
            Color jade = jadeFallback, ruby = rubyFallback, gold = goldFallback;
            if (themeData && themeData.ColorSet)
            {
                if (themeData.ColorSet.TryGetColorSetByDomain(Domains.Jade, out var jcs) && jcs != null) jade = jcs.OutsideBlockColor;
                if (themeData.ColorSet.TryGetColorSetByDomain(Domains.Ruby, out var rcs) && rcs != null) ruby = rcs.OutsideBlockColor;
                if (themeData.ColorSet.TryGetColorSetByDomain(Domains.Gold, out var gcs) && gcs != null) gold = gcs.OutsideBlockColor;
            }
            if (jadeWedge) jadeWedge.color = jade;
            if (rubyWedge) rubyWedge.color = ruby;
            if (goldWedge) goldWedge.color = gold;
        }

        void ArrangeWedgeRotations()
        {
            // Three wedges at 0°/120°/240° local Z. Unity's Radial360 image fills
            // clockwise from fillOrigin (Top by default), so rotating the Image by
            // -120° moves its starting edge clockwise by 120° — putting the next
            // wedge's start where the previous wedge's max-fill ends.
            SetLocalZ(jadeWedge, 0f);
            SetLocalZ(rubyWedge, -120f);
            SetLocalZ(goldWedge, -240f);
        }

        static void SetLocalZ(Image img, float zDegrees)
        {
            if (!img) return;
            var rt = img.rectTransform;
            var e = rt.localEulerAngles;
            e.z = zDegrees;
            rt.localEulerAngles = e;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Live-preview color and rotation changes in the inspector.
            ApplyDomainColors();
            if (autoArrangeWedgeRotations) ArrangeWedgeRotations();
        }
#endif
    }
}
