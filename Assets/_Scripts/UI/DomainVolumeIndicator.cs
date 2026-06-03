using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Drives the hexagonal domain-volume gauge on the pause / freestyle-toggle
    /// button. Reads the local player's cell each tick and feeds per-domain fill
    /// fractions + the dominant domain to a <see cref="DomainVolumeHexGraphic"/>.
    ///
    /// Spec (restored): a pointy-top hexagon, each domain owning a fixed 1/3
    /// (two of six edges) — Jade top, Ruby lower-left, Gold lower-right. Each sector
    /// always spans its full angular width; the colored band fills RADIALLY INWARD
    /// toward the centre as the domain's mass approaches the frenzy (Rabid) threshold.
    /// The centre hexagon is tinted by the dominant domain.
    ///
    /// ZERO-AUTHORING (ElementalBarsView pattern): when no graphic is wired the
    /// component creates a full-rect child <see cref="DomainVolumeHexGraphic"/> at
    /// runtime, and (optionally) a diagnostic readout below the button. MenuMiniGameHUD
    /// self-attaches this to its pause button and hands over the injected GameDataSO.
    ///
    /// Reads <see cref="Cell.GetDomainBlockCount"/> and
    /// <see cref="Cell.RabidEnterThreshold"/>; resolves the cell via the local
    /// player's vessel position, falling back to the nearest active cell.
    /// </summary>
    [DisallowMultipleComponent]
    public class DomainVolumeIndicator : MonoBehaviour
    {
        [Header("Gauge (optional — self-constructed when empty)")]
        [Tooltip("The procedural hexagon gauge. Leave null to auto-create a full-rect child graphic.")]
        [SerializeField] DomainVolumeHexGraphic hexGraphic;

        [Header("Theme")]
        [Tooltip("Source for per-domain colors. Optional — falls back to the explicit fallback colors below if missing.")]
        [SerializeField] ThemeManagerDataContainerSO themeData;
        [SerializeField] Color jadeFallback = new(0.20f, 0.85f, 0.55f, 0.95f);
        [SerializeField] Color rubyFallback = new(0.95f, 0.25f, 0.30f, 0.95f);
        [SerializeField] Color goldFallback = new(0.95f, 0.80f, 0.20f, 0.95f);

        [Header("Behavior")]
        [Tooltip("Seconds between cell samples. The phase tick is 0.5s; 0.25s keeps the gauge ahead of transitions.")]
        [Min(0.05f)] [SerializeField] float sampleIntervalSeconds = 0.25f;
        [Tooltip("Lerp speed for fill changes between samples. 0 = instant; higher = smoother.")]
        [Min(0f)] [SerializeField] float fillLerpSpeed = 8f;

        [Header("Cell resolution")]
        [Tooltip("If assigned, the indicator reads from this cell directly. Leave null to auto-resolve via the local player's vessel position (or the nearest active cell).")]
        [SerializeField] Cell explicitCell;

        [Header("Diagnostics")]
        [Tooltip("Live tuning numbers: per-domain counts, total vs frenzy threshold, phase, and net prisms/sec. " +
                 "The rate is the key tuning signal — +ve while flora outgrow fauna, -ve while fauna win, ~0 when pinned.")]
        [SerializeField] TMP_Text debugReadout;
        [Tooltip("Auto-create the diagnostic readout below the button when none is wired. Turn OFF for the production HUD.")]
        [SerializeField] bool autoCreateReadout = true;
        [Min(1f)] [SerializeField] float rateWindowSeconds = 5f;

        [Inject] GameDataSO gameData;

        Cell _cachedCell;
        float _nextSampleAt;
        float _jadeTarget, _rubyTarget, _goldTarget;
        float _jadeNow, _rubyNow, _goldNow;
        int _dominant = -1;

        // Rate-of-change tracking for the diagnostic readout.
        int _lastTotal = -1;
        float _lastTotalTime;
        float _smoothedRate;

        /// <summary>
        /// Explicit dependency handoff for the AddComponent path: runtime-added
        /// components never receive Reflex scene injection, so the creator passes its
        /// injected GameDataSO here. The [Inject] attribute still covers the authored-
        /// in-scene case.
        /// </summary>
        public void SetGameData(GameDataSO data) => gameData = data;

        // ------------------------------------------------------------------
        //  Lifecycle
        // ------------------------------------------------------------------

        void OnEnable()
        {
            EnsureHexGraphic();
            EnsureReadout();

            _jadeNow = _rubyNow = _goldNow = 0f;
            _jadeTarget = _rubyTarget = _goldTarget = 0f;
            _nextSampleAt = 0f;
            PushState();
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
        //  Self-construction (zero-authoring path)
        // ------------------------------------------------------------------

        void EnsureHexGraphic()
        {
            if (hexGraphic) return;

            // The pause button already owns an Image graphic, and two Graphics can't
            // share a GameObject — so the gauge lives on a full-rect child.
            var go = new GameObject("DomainVolumeHex (auto)", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            hexGraphic = go.AddComponent<DomainVolumeHexGraphic>();
            hexGraphic.raycastTarget = false; // never intercept the button click
        }

        void EnsureReadout()
        {
            if (debugReadout || !autoCreateReadout) return;

            var go = new GameObject("DomainVolumeReadout (auto)", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -8f);
            rt.sizeDelta = new Vector2(260f, 80f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 16f;
            tmp.alignment = TextAlignmentOptions.Top;
            tmp.raycastTarget = false;
            debugReadout = tmp;
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
                _dominant = -1;
                UpdateReadout(null, 0, 0, 0, 0);
                return;
            }

            int rabid = cell.RabidEnterThreshold;
            int jade = cell.GetDomainBlockCount(Domains.Jade);
            int ruby = cell.GetDomainBlockCount(Domains.Ruby);
            int gold = cell.GetDomainBlockCount(Domains.Gold);

            if (rabid > 0)
            {
                // Per-domain radial fill = that domain's mass as a fraction of the
                // frenzy threshold. A single domain reaching the full threshold (which
                // alone trips frenzy) fills its sector all the way to the centre.
                _jadeTarget = Mathf.Clamp01((float)jade / rabid);
                _rubyTarget = Mathf.Clamp01((float)ruby / rabid);
                _goldTarget = Mathf.Clamp01((float)gold / rabid);
            }
            else
            {
                _jadeTarget = _rubyTarget = _goldTarget = 0f;
            }

            // Dominant domain → centre hexagon tint. -1 when the cell is empty.
            _dominant = ResolveDominant(jade, ruby, gold);

            UpdateReadout(cell, jade, ruby, gold, rabid);
        }

        static int ResolveDominant(int jade, int ruby, int gold)
        {
            if (jade <= 0 && ruby <= 0 && gold <= 0) return -1;
            if (jade >= ruby && jade >= gold) return 0;
            if (ruby >= gold) return 1;
            return 2;
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
            PushState();
        }

        void PushState()
        {
            if (!hexGraphic) return;
            ResolveDomainColors(out var jadeC, out var rubyC, out var goldC);
            // SetState rebuilds the mesh only on a meaningful delta, so calling it
            // every frame during a lerp is cheap once the values settle.
            hexGraphic.SetState(_jadeNow, _rubyNow, _goldNow, jadeC, rubyC, goldC, _dominant);
        }

        // ------------------------------------------------------------------
        //  Diagnostics
        // ------------------------------------------------------------------

        void UpdateReadout(Cell cell, int jade, int ruby, int gold, int rabid)
        {
            if (!debugReadout) return;

            if (!cell)
            {
                debugReadout.text = "DVI: no active cell";
                return;
            }

            int total = jade + ruby + gold;

            float now = Time.unscaledTime;
            if (_lastTotal >= 0 && now > _lastTotalTime)
            {
                float instantRate = (total - _lastTotal) / (now - _lastTotalTime);
                float alpha = Mathf.Clamp01((now - _lastTotalTime) / Mathf.Max(1f, rateWindowSeconds));
                _smoothedRate = Mathf.Lerp(_smoothedRate, instantRate, alpha);
            }
            _lastTotal = total;
            _lastTotalTime = now;

            string cfgTag = cell.HasConfigAssigned ? "" : " [NO CFG]";
            debugReadout.text =
                $"J:{jade} R:{ruby} G:{gold}\n" +
                $"{total}/{rabid} {cell.Phase}{cfgTag}\n" +
                $"{(_smoothedRate >= 0 ? "+" : "")}{_smoothedRate:F1}/s";
        }

        // ------------------------------------------------------------------
        //  Cell + color resolution
        // ------------------------------------------------------------------

        Cell ResolveCell()
        {
            if (explicitCell) return explicitCell;
            if (_cachedCell) return _cachedCell;

            Transform vesselT = gameData?.LocalPlayer?.Vessel?.Transform;
            if (vesselT != null)
            {
                _cachedCell = Cell.FindCellContaining(vesselT.position);
                if (_cachedCell) return _cachedCell;
            }

            _cachedCell = Cell.FindNearestActiveCell(vesselT != null ? vesselT.position : Vector3.zero);
            return _cachedCell;
        }

        void ResolveDomainColors(out Color jade, out Color ruby, out Color gold)
        {
            jade = jadeFallback; ruby = rubyFallback; gold = goldFallback;
            if (themeData && themeData.ColorSet)
            {
                if (themeData.ColorSet.TryGetColorSetByDomain(Domains.Jade, out var j) && j != null) jade = Opaque(j.OutsideBlockColor);
                if (themeData.ColorSet.TryGetColorSetByDomain(Domains.Ruby, out var r) && r != null) ruby = Opaque(r.OutsideBlockColor);
                if (themeData.ColorSet.TryGetColorSetByDomain(Domains.Gold, out var g) && g != null) gold = Opaque(g.OutsideBlockColor);
            }
        }

        // Theme block colors are HDR/low-alpha; force a HUD-readable alpha.
        static Color Opaque(Color c) { c.a = 0.95f; return c; }
    }
}
