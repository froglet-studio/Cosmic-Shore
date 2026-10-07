using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Globalization;
using CosmicShore.Utility.PerformanceBenchmark;
#endif

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The DiagnosticsHUD console surface for black holes (Docs/BLACK_HOLE.md §6): spawn one
    /// with a strength and an optional position and velocity, retune it, drive it, list them,
    /// despawn one or all — from ANY scene, because the HUD auto-spawns everywhere and so does
    /// this. Editor and development builds only; the shell compiles empty in a release player
    /// (the <c>DiagnosticsHUD</c> pattern, Docs/CONDITIONAL_COMPILATION.md Pattern 1).
    ///
    /// Commands (<c>blackhole</c>, alias <c>bh</c>):
    /// <code>
    ///   blackhole spawn &lt;strength&gt; [x y z] [vx vy vz]   spawn at (x,y,z) — default: ahead of the camera
    ///   blackhole here &lt;strength&gt;                        spawn at the main camera's position
    ///   blackhole move &lt;id&gt; &lt;vx&gt; &lt;vy&gt; &lt;vz&gt;             set a hole's velocity
    ///   blackhole strength &lt;id&gt; &lt;value&gt;                 retune a hole
    ///   blackhole spin &lt;id&gt; &lt;ax&gt; &lt;ay&gt; &lt;az&gt;              set the frame-dragging axis
    ///   blackhole list                                   every live hole and its numbers
    ///   blackhole despawn &lt;id&gt; | all                     eased release, then destroy
    /// </code>
    /// Numbers parse with the invariant culture, so a decimal point works on every locale.
    /// </summary>
    public class BlackHoleConsole : MonoBehaviour
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        const string StatsSection = "BlackHole";
        const string CommandName = "blackhole";
        const string Alias = "bh";
        const string Usage = "usage: blackhole spawn <strength> [x y z] [vx vy vz] | here <strength> | " +
                             "move <id> <vx> <vy> <vz> | strength <id> <v> | spin <id> <ax> <ay> <az> | " +
                             "list | despawn <id>|all";
        /// <summary>Where a spawn lands when no position is given: this far ahead of the camera.</summary>
        const float SpawnAheadDistance = 300f;

        static BlackHoleConsole _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (_instance != null) return;
            var go = new GameObject("[BlackHoleConsole]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _instance = go.AddComponent<BlackHoleConsole>();
        }

        void Start()
        {
            DiagnosticsHUD.RegisterCommand(CommandName, Handle);
            DiagnosticsHUD.RegisterCommand(Alias, Handle);
            DiagnosticsHUD.SetStat(StatsSection, "holes", "none — cmd: blackhole spawn <strength> [x y z]");
        }

        void OnDestroy()
        {
            DiagnosticsHUD.UnregisterCommand(CommandName);
            DiagnosticsHUD.UnregisterCommand(Alias);
            DiagnosticsHUD.ClearStats(StatsSection);
            if (_instance == this) _instance = null;
        }

        float _nextStats;

        void Update()
        {
            if (Time.unscaledTime < _nextStats) return;
            _nextStats = Time.unscaledTime + 0.5f;
            int n = BlackHoleRegistry.Count;
            DiagnosticsHUD.SetStat(StatsSection, "holes", n == 0
                ? "none — cmd: blackhole spawn <strength> [x y z]"
                : $"{n} live, {BlackHoleGravityField.BodyCount:N0} bodies, {BlackHoleGravityField.CapturedTotal:N0} captured, " +
                  $"{BlackHoleWarp.ResidentPrismCount} warp residents, {BlackHoleVesselPull.PulledVesselCount} vessels pulled");
        }

        static bool TryFloat(string s, out float v) =>
            float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);

        static bool TryVector(string[] args, int at, out Vector3 v)
        {
            v = Vector3.zero;
            if (args.Length < at + 3) return false;
            if (!TryFloat(args[at], out float x) || !TryFloat(args[at + 1], out float y) || !TryFloat(args[at + 2], out float z))
                return false;
            v = new Vector3(x, y, z);
            return true;
        }

        static string Describe(BlackHole h) =>
            $"#{h.Id} strength {h.Strength:F1} GM {h.GM:F0} horizon {h.HorizonRadius:F1} influence {h.InfluenceRadius:F0} " +
            $"at ({h.transform.position.x:F0}, {h.transform.position.y:F0}, {h.transform.position.z:F0}) " +
            $"v ({h.Velocity.x:F1}, {h.Velocity.y:F1}, {h.Velocity.z:F1})";

        string Handle(string[] args)
        {
            if (args.Length == 0) return Usage;

            switch (args[0].ToLowerInvariant())
            {
                case "spawn":
                {
                    if (args.Length < 2 || !TryFloat(args[1], out float strength) || strength < 0f)
                        return Usage;
                    Vector3 position;
                    if (!TryVector(args, 2, out position))
                    {
                        var cam = Camera.main;
                        position = cam != null
                            ? cam.transform.position + cam.transform.forward * SpawnAheadDistance
                            : Vector3.zero;
                    }
                    TryVector(args, 5, out var velocity);
                    var hole = BlackHoleRegistry.Spawn(position, strength, velocity);
                    return hole == null ? "spawn refused (see console)" : "spawned " + Describe(hole);
                }
                case "here":
                {
                    if (args.Length < 2 || !TryFloat(args[1], out float strength) || strength < 0f)
                        return Usage;
                    var cam = Camera.main;
                    var position = cam != null ? cam.transform.position : Vector3.zero;
                    var hole = BlackHoleRegistry.Spawn(position, strength);
                    return hole == null ? "spawn refused (see console)" : "spawned " + Describe(hole);
                }
                case "move":
                {
                    if (args.Length < 5 || !int.TryParse(args[1], out int id) || !TryVector(args, 2, out var velocity))
                        return Usage;
                    var hole = BlackHoleRegistry.Find(id);
                    if (hole == null) return $"no black hole #{id}";
                    hole.Velocity = velocity;
                    return "moving " + Describe(hole);
                }
                case "strength":
                {
                    if (args.Length < 3 || !int.TryParse(args[1], out int id) || !TryFloat(args[2], out float strength))
                        return Usage;
                    var hole = BlackHoleRegistry.Find(id);
                    if (hole == null) return $"no black hole #{id}";
                    hole.SetStrength(strength);
                    return "retuned " + Describe(hole);
                }
                case "spin":
                {
                    if (args.Length < 5 || !int.TryParse(args[1], out int id) || !TryVector(args, 2, out var axis))
                        return Usage;
                    var hole = BlackHoleRegistry.Find(id);
                    if (hole == null) return $"no black hole #{id}";
                    hole.SpinAxis = axis;
                    return $"#{id} spin axis {hole.SpinAxis}";
                }
                case "list":
                {
                    var holes = BlackHoleRegistry.Holes;
                    if (holes.Count == 0) return "no black holes live";
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < holes.Count; i++)
                    {
                        if (holes[i] == null) continue;
                        if (sb.Length > 0) sb.Append(" | ");
                        sb.Append(Describe(holes[i]));
                    }
                    sb.Append($" | bodies {BlackHoleGravityField.BodyCount:N0}, captured {BlackHoleGravityField.CapturedTotal:N0}");
                    return sb.ToString();
                }
                case "despawn":
                {
                    if (args.Length < 2) return Usage;
                    if (args[1].ToLowerInvariant() == "all")
                    {
                        int n = BlackHoleRegistry.Count;
                        BlackHoleRegistry.DespawnAll();
                        return $"despawning {n} black hole(s)";
                    }
                    if (!int.TryParse(args[1], out int id)) return Usage;
                    return BlackHoleRegistry.Despawn(id) ? $"despawning #{id}" : $"no black hole #{id}";
                }
                default:
                    return Usage;
            }
        }
#endif
    }
}
