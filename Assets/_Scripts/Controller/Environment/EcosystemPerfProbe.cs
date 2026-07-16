using System.Text;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Closes the ecosystem perf loop: logs parseable <c>[ECOSIM] prisms=… fauna=… fps=…</c>
    /// lines so the headless tuner (<c>Tools/ecosim/ecosim.py</c>) can be recalibrated to
    /// real frame cost without a human eyeballing the profiler. Copy a steady-state line
    /// into <c>Tools/ecosim/calibration.csv</c> and re-run ecosim; the model then predicts
    /// FPS from the actual numbers.
    ///
    /// Reads the ecosystem state with zero wiring: smoothed unscaled FPS from frame deltas,
    /// and per-cell prism + live-fauna counts from <see cref="Cell"/>'s static active-cell
    /// registry (so it works in any scene with a Cell - menu or gameplay - with no [Inject]
    /// and no serialized references).
    ///
    /// Usage: drop on any GameObject in the scene you want to measure (e.g. Menu_Main), or
    /// define the <c>ECOSIM_PROBE</c> scripting symbol to auto-spawn one. It is inert in
    /// shipping builds unless explicitly added, and never affects gameplay - read-only.
    /// </summary>
    [DisallowMultipleComponent]
    public class EcosystemPerfProbe : MonoBehaviour
    {
        [Tooltip("Seconds between log lines. The FPS reported is averaged over this window.")]
        [Min(0.5f)] [SerializeField] float logIntervalSeconds = 3f;

        [Tooltip("Also draw a tiny on-screen readout (top-left) in addition to the Console log.")]
        [SerializeField] bool onScreenReadout = true;

        int _frames;
        float _elapsed;
        string _lastLine = "";
        readonly StringBuilder _sb = new();

#if ECOSIM_PROBE
        // Opt-in auto-spawn: set the ECOSIM_PROBE scripting define to get a probe in every
        // scene without editing it. Never compiled into a normal build.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            var go = new GameObject("EcosystemPerfProbe (auto)");
            go.AddComponent<EcosystemPerfProbe>();
            DontDestroyOnLoad(go);
        }
#endif

        void Update()
        {
            _frames++;
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < logIntervalSeconds) return;

            float fps = _frames / Mathf.Max(1e-4f, _elapsed);
            _frames = 0;
            _elapsed = 0f;

            SampleAndLog(fps);
        }

        void SampleAndLog(float fps)
        {
            // Sum prisms + live fauna across every active cell (one cell in the menu;
            // several in WildlifeBlitz). LiveFauna only counts lineage-registered
            // creatures - exactly what drives the OverlapSphere cost.
            int prisms = 0, fauna = 0, cells = 0;
            float volume = 0f;
            var phases = new System.Text.StringBuilder();
            foreach (var cell in Cell.ActiveCellsSnapshot)
            {
                if (!cell) continue;
                cells++;
                prisms += cell.LiveBlockCount;
                fauna += cell.LiveFauna.Count;
                volume += cell.LiveVolume;
                if (phases.Length > 0) phases.Append('/');
                phases.Append(cell.Phase);
            }

            // Collider telemetry from the LOD sweep: active (near-focus) colliders vs
            // live prisms - the §4 budget made observable. 0/0 = LOD idle (no foci).
            int near = PrismColliderLodManager.LastNearCount;
            int live = PrismColliderLodManager.LastLiveCount;

            // Instanced-rendering telemetry: companion render entities currently
            // alive (Docs/PRISM_ECS_MIGRATION.md). ents≈prisms ⇒ the batched path
            // is carrying the population; ents=0 ⇒ legacy MeshRenderer path.
            int renderEntities = CosmicShore.ECS.PrismRenderService.LiveEntityCount;

            _lastLine = $"[ECOSIM] prisms={prisms} volume={volume:F0} colliders={near}/{live} " +
                        $"ents={renderEntities} fauna={fauna} phase={phases} fps={fps:F1}" +
                        (cells > 1 ? $"  (cells={cells})" : "");
            Debug.Log(_lastLine);
        }

        void OnGUI()
        {
            if (!onScreenReadout || string.IsNullOrEmpty(_lastLine)) return;
            _sb.Clear();
            _sb.Append(_lastLine);
            var style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(10, 10, 600, 24), _sb.ToString(), style);
        }
    }
}
