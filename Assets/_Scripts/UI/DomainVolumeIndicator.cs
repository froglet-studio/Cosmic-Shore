using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using TMPro;
using Unity.Profiling;
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
    /// each domain owning a fixed 1/3 (two of six edges) - Jade top, Ruby lower-left,
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
    /// Reads <see cref="Cell.GetControlVolume"/>, <see cref="Cell.FrenzyEnterVolume"/>
    /// and <see cref="Cell.ResolvedThresholds"/> - volume is the spine, so the gauge shows
    /// exactly the measure that decides the cell. In a cell with NO nucleus control zone that
    /// is whole-cell volume against the phase ladder (every arcade arena today); in one WITH a
    /// nucleus control zone it is each domain's share of the nucleus CLAIM, because that is
    /// what <see cref="Cell.DominantDomain"/> is reading and a gauge must not be able to
    /// disagree with the control it draws.
    ///
    /// The cell is re-resolved on every sample from the local player's vessel position — the one
    /// whose visible MEMBRANE contains them, falling back to the nearest active cell when they
    /// are between membranes. It is NOT latched, because the Arkway flies you from one cell into
    /// the next and a latched gauge stays pinned to the one you started in.
    /// </summary>
    [DisallowMultipleComponent]
    public class DomainVolumeIndicator : MonoBehaviour
    {
        [Header("Gauge (optional - self-constructed when empty)")]
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

        // Push gating: SetState delta-checks downstream at 0.002, so pushes below
        // that epsilon are guaranteed no-ops — skip them here and the whole Update
        // becomes near-free in steady state. Colors are resolved on the 0.25s
        // sample cadence (theme swaps are rare), not per frame.
        const float ConvergedEpsilon = 0.0005f;
        const float CyclePushEpsilon = 0.002f;

        // Attribution split (PERFORMANCE_OPTIMIZATION.md TODO C2): Sample is the
        // 0.25s cell read — its cost is really Cell.VolumeSum when this component
        // is the first volume reader of the recompute interval; Push is the
        // per-frame lerp + SetState residual.
        static readonly ProfilerMarker s_sampleMarker = new("DomainVolumeIndicator.Sample");
        static readonly ProfilerMarker s_pushMarker = new("DomainVolumeIndicator.Push");
        Color _jadeColor = Color.white, _rubyColor = Color.white, _goldColor = Color.white;
        float _lastPushedCycle = -1f;
        bool _statePushPending;

        // Intermediate phase enter thresholds as fractions of FrenzyEnter (ascending):
        // where the concentric rings sit. With the 3-phase ladder there is exactly one
        // boundary strictly inside the frenzy extent - Restless (Frenzy itself sits at the
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
            _lastPushedCycle = -1f;
            _statePushPending = false;
            RefreshDomainColors();
            PushState(_spawnCycle);
        }

        void Update()
        {
            if (Time.unscaledTime >= _nextSampleAt)
            {
                _nextSampleAt = Time.unscaledTime + sampleIntervalSeconds;
                using (s_sampleMarker.Auto())
                {
                    SampleTargets();
                    RefreshDomainColors();
                }
                _statePushPending = true;
            }
            using (s_pushMarker.Auto())
            {
                StepTowardTargets();
            }
        }

        // ------------------------------------------------------------------
        //  Self-construction (zero-authoring path)
        // ------------------------------------------------------------------

        void EnsureHexGraphic()
        {
            if (hexGraphic) return;

            // The pause button already owns an Image graphic, and two Graphics can't
            // share a GameObject - so the gauge lives on a full-rect child.
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
        /// still works - clearing sprite + zeroing color is enough to make it
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

            // Volume is the spine (locked invariant), and the gauge reads the volume that
            // actually DECIDES this cell - Cell.GetControlVolume, the same source
            // Cell.DominantDomain reads. That is one branch, not two gauges:
            //
            //   • NO nucleus control zone (every arcade arena today): whole-cell live VOLUME -
            //     every prism contributes, trail, flora and fauna bodies alike - against the
            //     volume phase ladder, so the wedges mirror exactly what drives the cell's
            //     phase. Unchanged.
            //   • WITH a nucleus control zone (Brood Rush, the Arkway's traversal cells): the
            //     ENVIRONMENT volume laid INSIDE the nucleus - the territorial claim - as each
            //     domain's SHARE of that claim. The phase ladder is a whole-cell measure and
            //     says nothing about the claim, so its ring is hidden rather than drawn at a
            //     scale it does not describe.
            //
            // Feeding the whole-cell read into a nucleus cell was the defect: the gauge could
            // show one domain leading while DominantDomain held the cell for another, with
            // nothing wrong on either side.
            float jade = cell.GetControlVolume(Domains.Jade);
            float ruby = cell.GetControlVolume(Domains.Ruby);
            float gold = cell.GetControlVolume(Domains.Gold);

            if (cell.HasNucleusControlZone)
            {
                // Share of the claim. An almost-empty nucleus reading as one full wedge is
                // honest: a single prism in there really does hold the whole cell.
                float claim = jade + ruby + gold;
                if (claim > 0f)
                {
                    _jadeTarget = jade / claim;
                    _rubyTarget = ruby / claim;
                    _goldTarget = gold / claim;
                }
                else
                {
                    _jadeTarget = _rubyTarget = _goldTarget = 0f;
                }
                _hasThresholds = false;
            }
            else if (cell.FrenzyEnterVolume > 0f)
            {
                float frenzy = cell.FrenzyEnterVolume;

                // Per-domain radial fill = that domain's mass as a fraction of the
                // frenzy threshold. A single domain reaching the full threshold (which
                // alone trips frenzy) fills its sector all the way to the centre.
                _jadeTarget = Mathf.Clamp01(jade / frenzy);
                _rubyTarget = Mathf.Clamp01(ruby / frenzy);
                _goldTarget = Mathf.Clamp01(gold / frenzy);

                // Concentric ring position: the Restless enter threshold as a fraction of
                // FrenzyEnter. A wedge reaching the ring has, by construction, Restless's
                // worth of mass - so the wedge passing through it IS that domain pushing
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
            bool converging =
                Mathf.Abs(_jadeNow - _jadeTarget) > ConvergedEpsilon ||
                Mathf.Abs(_rubyNow - _rubyTarget) > ConvergedEpsilon ||
                Mathf.Abs(_goldNow - _goldTarget) > ConvergedEpsilon;

            if (converging)
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
            }

            // Spawn-cycle fraction is read LIVE each frame (cheap Time.time math)
            // so the ring sweeps smoothly between the 0.25s volume samples. Use the
            // cached cell to avoid re-running cell resolution every frame.
            float cycle = _cachedCell ? _cachedCell.FaunaSpawnCycleFraction : _spawnCycle;
            bool cycleMoved = Mathf.Abs(cycle - _lastPushedCycle) >= CyclePushEpsilon;

            // Steady state (fills converged, ring between epsilon steps, no fresh
            // sample) pushes nothing — the downstream delta gate would have
            // discarded it anyway.
            if (!converging && !cycleMoved && !_statePushPending) return;
            _statePushPending = false;
            PushState(cycle);
        }

        void PushState(float cycle)
        {
            if (!hexGraphic) return;
            _lastPushedCycle = cycle;
            // SetState rebuilds the mesh only on a meaningful delta.
            hexGraphic.SetState(_jadeNow, _rubyNow, _goldNow, _jadeColor, _rubyColor, _goldColor,
                                _dominant, cycle, _hasThresholds ? _thresholdFracs : null);
        }

        // ------------------------------------------------------------------
        //  Cell + color resolution
        // ------------------------------------------------------------------

        /// <summary>
        /// The cell the player is in RIGHT NOW, re-resolved on every sample (4 Hz) rather than
        /// latched.
        ///
        /// It used to cache the first answer forever, which is correct exactly while a scene has
        /// one cell and the player never leaves it — true of every arcade mode, and false of the
        /// Arkway, whose whole subject is flying from one cell into the next. There the gauge
        /// stayed pinned to the home cell for the entire voyage: three domain wedges at zero and
        /// a fauna-spawn ring that never moved, which reads as a broken gauge rather than as a
        /// gauge reading somewhere else. Two live cell registries (<see cref="Cell.Active"/> via
        /// the two finders) make the re-read a walk over a handful of cells, so the latch was
        /// buying almost nothing.
        ///
        /// The last good answer is kept as the fallback so the gauge holds its reading through a
        /// frame where nothing resolves (a cell mid-strike, the vessel mid-swap) instead of
        /// blanking.
        /// </summary>
        Cell ResolveCell()
        {
            if (explicitCell) return explicitCell;

            Transform vesselT = gameData?.LocalPlayer?.Vessel?.Transform;
            Vector3 at = vesselT != null ? vesselT.position : Vector3.zero;

            // MEMBRANE first, not ContainsPosition. "Which cell am I in" is a question about
            // the boundary the player can SEE, and ContainsPosition answers with the SENSING
            // radius, which a config may widen well past the membrane so fauna can find mass
            // across a big arena (SenseRadiusOverride). That is the right answer for prism
            // registration and the wrong one for a HUD: a wide-sensing cell can swallow a
            // neighbouring world and the gauge then reports a cell the player is nowhere near.
            var inside = Cell.FindCellByMembrane(at);
            if (inside) return _cachedCell = inside;

            // Outside every membrane (the Arkway's open water between cells): the nearest is
            // the one being left or approached, which beats blanking the gauge.
            var nearest = Cell.FindNearestActiveCell(at);
            if (nearest) return _cachedCell = nearest;

            return _cachedCell;
        }

        void RefreshDomainColors()
        {
            // Canonical source - the same path MultiplayerHUD and every other
            // domain-tinted UI uses. The serialized override exists for prefabs that
            // want to lock to a specific theme variant during pitch demos. Resolved
            // on the sample cadence, not per frame — theme swaps are rare.
            var colorSet = themeDataOverride
                ? themeDataOverride.ColorSet
                : gameData?.ThemeManagerData?.ColorSet;

            // Last-resort neutral so the gauge stays visible if the theme container
            // genuinely isn't wired yet (e.g. tooling scenes). The DI registration in
            // AppManager makes this branch unreachable in shipping flows.
            if (colorSet == null)
            {
                _jadeColor = _rubyColor = _goldColor = Color.white;
                return;
            }

            _jadeColor = ResolveDomainColor(colorSet, Domains.Jade);
            _rubyColor = ResolveDomainColor(colorSet, Domains.Ruby);
            _goldColor = ResolveDomainColor(colorSet, Domains.Gold);
        }

        static Color ResolveDomainColor(SO_ColorSet colorSet, Domains domain)
        {
            if (!colorSet.TryGetColorSetByDomain(domain, out var dcs) || dcs == null)
                return Color.white;
            // TrailHighlightColor is the BRIGHT IDENTITY hue (cyan/magenta/orange
            // in the original palette) - the same field VesselHelper uses for trail
            // identity and the only color set field that gives each domain a
            // recognizable face. OutsideBlockColor is the dim outer shell of a
            // prism, which for Ruby reads as a near-black indigo - that's what the
            // user spotted as "fire instead of Ruby" (Gold's warm hue showing
            // through next to Ruby's near-black).
            var c = dcs.TrailHighlightColor;
            c.a = 0.95f;
            return c;
        }
    }
}
