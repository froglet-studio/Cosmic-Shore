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
    /// Drives the hexagonal domain-volume gauge on the universal in-game pause /
    /// freestyle-toggle button. Reads the local player's cell each tick and feeds
    /// per-domain fill fractions + the dominant domain + the cell's phase thresholds
    /// to a <see cref="DomainVolumeHexGraphic"/>.
    ///
    /// Look (universal across menu and all gameplay scenes): a pointy-top hexagon,
    /// each domain owning a fixed 1/3 (two of six edges) — Jade top, Ruby lower-left,
    /// Gold lower-right. Each sector always spans its full angular width; the colored
    /// band fills RADIALLY INWARD toward the centre as the domain's mass approaches the
    /// Frenzy threshold, the centre being the frenzy state. Concentric threshold
    /// rings mark each cell phase boundary; as a wedge fills inward it passes through
    /// them, the crossed ring brightening to signal that domain is pushing the cell into
    /// the next aggression zone.
    ///
    /// ZERO-AUTHORING (ElementalBarsView pattern): when no graphic is wired the
    /// component creates a full-rect child <see cref="DomainVolumeHexGraphic"/> at
    /// runtime, and hides the host button's authored face. MenuMiniGameHUD and
    /// MiniGameHUD self-attach this to their pause button and hand over the injected
    /// GameDataSO.
    ///
    /// Reads <see cref="Cell.GetDomainVolume"/>, <see cref="Cell.FrenzyEnterVolume"/>
    /// and <see cref="Cell.ResolvedThresholds"/> — volume is the spine, so the gauge
    /// shows exactly the measure that drives the phase ladder; resolves the cell via
    /// the local player's vessel position, falling back to the nearest active cell.
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

        // Intermediate phase enter thresholds as fractions of FrenzyEnter (ascending):
        // where the concentric rings sit. With the 3-phase ladder there is exactly one
        // boundary strictly inside the frenzy extent — Restless (Frenzy itself sits at the
        // centre / boundary hexagon). Refreshed each sample so a config swap (or late cell
        // resolution) is picked up.
        readonly float[] _thresholdFracs = new float[1];
        bool _hasThresholds;

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

            _jadeNow = _rubyNow = _goldNow = 0f;
            _jadeTarget = _rubyTarget = _goldTarget = 0f;
            _hasThresholds = false;
            _nextSampleAt = 0f;
            PushState();
        }

        void Update()
        {
            // Invisible gauges must not dirty the canvas: menu-mode "hide" is
            // CanvasGroup-alpha-only (the GameObject stays active), so without this
            // gate the ring sweep keeps rebuilding batches behind an invisible HUD
            // during all normal menu browsing.
            if (hexGraphic && hexGraphic.canvasRenderer.GetInheritedAlpha() <= 0.001f)
                return;

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
            //
            // Sub-canvas isolation (the ObjectiveIndicator.CreateRuntime pattern): the
            // ring sweep dirties this graphic continuously, and without a dedicated
            // Canvas every rebuild re-batches the whole parent canvas — the shared
            // game HUD, or in Menu_Main the ENTIRE menu UI canvas.
            var go = new GameObject("DomainVolumeHex (auto)", typeof(RectTransform), typeof(Canvas));
            var rt = (RectTransform)go.transform;
            rt.SetParent(transform, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            // Inherit the parent's sort order — the gauge draws where the button sits.
            go.GetComponent<Canvas>().overrideSorting = false;

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
                _dominant = -1;
                _hasThresholds = false;
                return;
            }

            // Volume is the spine (locked invariant): the gauge reads per-domain live
            // VOLUME — every prism contributes (trail, flora, fauna bodies) — against
            // the volume phase ladder, mirroring exactly what drives the cell's phase.
            float frenzy = cell.FrenzyEnterVolume;
            float jade = cell.GetDomainVolume(Domains.Jade);
            float ruby = cell.GetDomainVolume(Domains.Ruby);
            float gold = cell.GetDomainVolume(Domains.Gold);

            if (frenzy > 0f)
            {
                // Per-domain radial fill = that domain's mass as a fraction of the
                // frenzy threshold. A single domain reaching the full threshold (which
                // alone trips frenzy) fills its sector all the way to the centre.
                _jadeTarget = Mathf.Clamp01(jade / frenzy);
                _rubyTarget = Mathf.Clamp01(ruby / frenzy);
                _goldTarget = Mathf.Clamp01(gold / frenzy);

                // Concentric ring position: the Restless enter threshold as a fraction of
                // FrenzyEnter. A wedge reaching the ring has, by construction, Restless's
                // worth of mass — so the wedge passing through it IS that domain pushing
                // the cell into the hunting band. Frenzy sits at fraction 1 (centre /
                // boundary hexagon), so it needs no separate ring.
                var t = cell.ResolvedThresholds;
                float denom = Mathf.Max(1f, t.FrenzyEnterVolume);
                _thresholdFracs[0] = t.RestlessEnterVolume / denom;
                _hasThresholds = true;
            }
            else
            {
                _jadeTarget = _rubyTarget = _goldTarget = 0f;
                _hasThresholds = false;
            }

            // Dominant domain → centre hexagon tint. -1 when the cell is empty.
            _dominant = ResolveDominant(jade, ruby, gold);

            // Fauna spawn cycle progress. Cell drives this from the spawner's
            // periodic loop; 0 = just spawned, 1 = about to spawn (in the
            // dominant domain's color).
            _spawnCycle = cell.FaunaSpawnCycleFraction;
        }

        static int ResolveDominant(float jade, float ruby, float gold)
        {
            if (jade <= 0f && ruby <= 0f && gold <= 0f) return -1;
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
            // Spawn-cycle fraction is read LIVE each frame (cheap Time.time math)
            // so the ring sweeps smoothly between the 0.25s volume samples. Use the
            // cached cell to avoid re-running cell resolution every frame.
            // Quantized to 1/128ths (2 steps per ring segment): the raw fraction
            // advances every frame, and unquantized it defeats SetState's epsilon —
            // a mesh rebuild nearly every frame on short spawn periods. Quantized,
            // rebuild cadence is capped at 128/period Hz (~11 Hz on the menu cell).
            float cycle = _cachedCell ? _cachedCell.FaunaSpawnCycleFraction : _spawnCycle;
            cycle = Mathf.Round(cycle * 128f) * (1f / 128f);
            // SetState rebuilds the mesh only on a meaningful delta.
            hexGraphic.SetState(_jadeNow, _rubyNow, _goldNow, jadeC, rubyC, goldC, _dominant, cycle,
                                _hasThresholds ? _thresholdFracs : null);
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
