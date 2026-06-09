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
        [Tooltip("Optional override. By default colors are pulled from GameDataSO.ThemeManagerData.ColorSet, matching MultiplayerHUD and every other domain-tinted UI.")]
        [SerializeField] ThemeManagerDataContainerSO themeDataOverride;

        [Header("Behavior")]
        [Tooltip("Seconds between cell samples. The phase tick is 0.5s; 0.25s keeps the gauge ahead of transitions.")]
        [Min(0.05f)] [SerializeField] float sampleIntervalSeconds = 0.25f;
        [Tooltip("Lerp speed for fill changes between samples. 0 = instant; higher = smoother.")]
        [Min(0f)] [SerializeField] float fillLerpSpeed = 8f;

        [Header("Concentric phase mode (Skim Race)")]
        [Tooltip("Draw concentric hexagon rings — one per cell phase boundary (Quiet/Settled/Restless/Frozen/Rabid) — that light up in the dominant domain's color as the cell's summed mass crosses each threshold. Off = the per-domain radial bands used in the main menu.")]
        [SerializeField] bool concentricPhaseMode = false;

        [Header("Cell resolution")]
        [Tooltip("If assigned, the indicator reads from this cell directly. Leave null to auto-resolve via the local player's vessel position (or the nearest active cell).")]
        [SerializeField] Cell explicitCell;

        [Inject] GameDataSO gameData;

        Cell _cachedCell;
        float _nextSampleAt;
        float _jadeTarget, _rubyTarget, _goldTarget;
        float _jadeNow, _rubyNow, _goldNow;
        int _dominant = -1;
        float _spawnCycle;

        // Concentric-phase-mode state. _massTarget/_massNow are the summed mass as a
        // fraction of RabidEnter (lerped like the per-domain fills). _phaseFracs are the
        // per-phase enter thresholds / RabidEnter — fixed per cell config, so they're
        // resolved at sample time and fed straight to the graphic.
        float _massTarget, _massNow;
        readonly float[] _phaseFracs = new float[5]; // Quiet, Settled, Restless, Frozen, Rabid

        /// <summary>
        /// Explicit dependency handoff for the AddComponent path: runtime-added
        /// components never receive Reflex scene injection, so the creator passes its
        /// injected GameDataSO here. The [Inject] attribute still covers the authored-
        /// in-scene case.
        /// </summary>
        public void SetGameData(GameDataSO data) => gameData = data;

        /// <summary>
        /// Runtime toggle for the concentric-phase-rings layout (Skim Race). Used by the
        /// AddComponent path (e.g. <see cref="HexRaceHUD"/>) where the component is created
        /// in code and so never receives the serialized inspector value. Authored-in-scene
        /// indicators set this via the inspector field instead.
        /// </summary>
        public void SetConcentricPhaseMode(bool enabled) => concentricPhaseMode = enabled;

        // ------------------------------------------------------------------
        //  Lifecycle
        // ------------------------------------------------------------------

        void OnEnable()
        {
            EnsureHexGraphic();

            _jadeNow = _rubyNow = _goldNow = 0f;
            _jadeTarget = _rubyTarget = _goldTarget = 0f;
            _massNow = _massTarget = 0f;
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
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            hexGraphic = go.AddComponent<DomainVolumeHexGraphic>();
            hexGraphic.raycastTarget = false; // never intercept the button click

            HideHostButtonFace();
        }

        /// <summary>
        /// Erase the pause button's authored face so it doesn't peek out around the
        /// hex. Keep the Image component so the Button's raycast/state machinery
        /// still works — clearing sprite + zeroing color is enough to make it
        /// invisible while the rect-based hit test continues to register clicks.
        /// </summary>
        void HideHostButtonFace()
        {
            // Host Image stays enabled so Button raycast keeps working; sprite +
            // color zeroed so it draws nothing.
            Image hostImage = null;
            if (TryGetComponent(out hostImage))
            {
                hostImage.sprite = null;
                hostImage.color = new Color(0f, 0f, 0f, 0f);
            }

            // Defensive cleanup of leftover graphics from previous iterations of
            // this script (the three Radial360 child Images, or any custom art
            // layer authored on top of the button face). Skip the host Image (still
            // needed for raycast) and our own hex graphic.
            foreach (var g in GetComponentsInChildren<Graphic>(true))
            {
                if (!g || g == hexGraphic || g == hostImage || g is TMP_Text) continue;
                if (g.gameObject == gameObject) continue; // never touch components on our own GO
                g.gameObject.SetActive(false);
            }
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
                _massTarget = 0f;
                _dominant = -1;
                return;
            }

            int rabid = cell.RabidEnterThreshold;
            int jade = cell.GetDomainBlockCount(Domains.Jade);
            int ruby = cell.GetDomainBlockCount(Domains.Ruby);
            int gold = cell.GetDomainBlockCount(Domains.Gold);

            // Concentric phase mode: total summed mass vs frenzy, plus each phase
            // boundary as a radius fraction. Thresholds are static per config so they
            // can be read straight here (no lerp needed on the rings themselves).
            if (concentricPhaseMode)
            {
                _massTarget = rabid > 0 ? Mathf.Clamp01((float)cell.LiveBlockCount / rabid) : 0f;
                var t = cell.ResolvedThresholds;
                float denom = Mathf.Max(1, t.RabidEnter);
                _phaseFracs[0] = t.QuietEnter / denom;
                _phaseFracs[1] = t.SettledEnter / denom;
                _phaseFracs[2] = t.RestlessEnter / denom;
                _phaseFracs[3] = t.FrozenEnter / denom;
                _phaseFracs[4] = t.RabidEnter / denom; // 1.0 by construction
            }

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

            // Fauna spawn cycle progress. Cell drives this from the spawner's
            // periodic loop; 0 = just spawned, 1 = about to spawn (in the
            // dominant domain's color).
            _spawnCycle = cell.FaunaSpawnCycleFraction;
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
                _massNow = _massTarget;
            }
            else
            {
                float t = 1f - Mathf.Exp(-fillLerpSpeed * Time.unscaledDeltaTime);
                _jadeNow = Mathf.Lerp(_jadeNow, _jadeTarget, t);
                _rubyNow = Mathf.Lerp(_rubyNow, _rubyTarget, t);
                _goldNow = Mathf.Lerp(_goldNow, _goldTarget, t);
                _massNow = Mathf.Lerp(_massNow, _massTarget, t);
            }
            PushState();
        }

        void PushState()
        {
            if (!hexGraphic) return;
            ResolveDomainColors(out var jadeC, out var rubyC, out var goldC);
            // Spawn-cycle fraction is read LIVE each frame (cheap Time.time math)
            // so the ring sweeps smoothly between the 0.25s volume samples. Use the
            // cached cell to avoid re-running cell resolution every frame.
            float cycle = _cachedCell ? _cachedCell.FaunaSpawnCycleFraction : _spawnCycle;

            if (concentricPhaseMode)
            {
                // The lit rings + centre + mass fill all take the dominant domain's hue,
                // so "which domain reached the threshold" reads straight off the color.
                Color dominantColor = _dominant switch
                {
                    0 => jadeC,
                    1 => rubyC,
                    2 => goldC,
                    _ => Color.white,
                };
                hexGraphic.SetPhaseState(_massNow, _phaseFracs, dominantColor, _dominant, cycle);
                return;
            }

            // SetState rebuilds the mesh only on a meaningful delta.
            hexGraphic.SetState(_jadeNow, _rubyNow, _goldNow, jadeC, rubyC, goldC, _dominant, cycle);
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
            // Canonical source — the same path MultiplayerHUD and every other
            // domain-tinted UI uses. The serialized override exists for prefabs that
            // want to lock to a specific theme variant during pitch demos.
            var colorSet = themeDataOverride
                ? themeDataOverride.ColorSet
                : gameData?.ThemeManagerData?.ColorSet;

            // Last-resort neutral so the gauge stays visible if the theme container
            // genuinely isn't wired yet (e.g. tooling scenes). The DI registration in
            // AppManager makes this branch unreachable in shipping flows.
            if (colorSet == null) { jade = ruby = gold = Color.white; return; }

            jade = ResolveDomainColor(colorSet, Domains.Jade);
            ruby = ResolveDomainColor(colorSet, Domains.Ruby);
            gold = ResolveDomainColor(colorSet, Domains.Gold);
        }

        static Color ResolveDomainColor(SO_ColorSet colorSet, Domains domain)
        {
            if (!colorSet.TryGetColorSetByDomain(domain, out var dcs) || dcs == null)
                return Color.white;
            // TrailHighlightColor is the BRIGHT IDENTITY hue (cyan/magenta/orange
            // in the original palette) — the same field VesselHelper uses for trail
            // identity and the only color set field that gives each domain a
            // recognizable face. OutsideBlockColor is the dim outer shell of a
            // prism, which for Ruby reads as a near-black indigo — that's what the
            // user spotted as "fire instead of Ruby" (Gold's warm hue showing
            // through next to Ruby's near-black).
            var c = dcs.TrailHighlightColor;
            c.a = 0.95f;
            return c;
        }
    }
}
