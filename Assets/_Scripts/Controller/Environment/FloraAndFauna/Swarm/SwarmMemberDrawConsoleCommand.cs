// The `memberdraw` console command (editor and development builds): which carrier draws swarm, substrate and builder
// members on this device, and a switch to force the SM 3.5 path (SwarmMemberFallbackDraw, the WebGL2 draw) where the
// GraphicsBuffer path is supported, so the two can be compared in the editor (Docs/SWARM_FAUNA.md §14.8).
//
// It self-registers through DiagnosticsHUD.RegisterCommand, whose body is already
// #if UNITY_EDITOR || DEVELOPMENT_BUILD, so a release player registers nothing (Docs/CONDITIONAL_COMPILATION.md
// Pattern 1: the shell stays unguarded).
//
//   memberdraw         this device's path, and whether the SM 3.5 path is forced
//   memberdraw sm35    force the SM 3.5 path for renderers built from now on
//   memberdraw auto    back to the device's own choice
//
// A renderer chooses once, when its population hatches, so a switch reaches the populations built AFTER it: pick the
// cell again from the cell selector, or leave and re-enter play mode. This project's Enter Play Mode options skip the
// domain reload, so the switch survives play sessions until the next script compile; `memberdraw auto` clears it.
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>Registers and handles the <c>memberdraw</c> diagnostics command.</summary>
    public static class SwarmMemberDrawConsoleCommand
    {
        public const string CommandName = "memberdraw";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() => DiagnosticsHUD.RegisterCommand(CommandName, Handle);

        /// <summary>Handles <c>memberdraw [sm35|auto]</c>. Returns the overlay line.</summary>
        public static string Handle(string[] args)
        {
            string sub = args is { Length: > 0 } ? args[0].ToLowerInvariant() : string.Empty;
            switch (sub)
            {
                case "":
                    return Summary();
                case "sm35":
                    SwarmMemberRenderer.ForceFallback = true;
                    return "memberdraw: SM 3.5 path FORCED for populations hatched from now on - " + Summary();
                case "auto":
                    SwarmMemberRenderer.ForceFallback = false;
                    return "memberdraw: back to the device's choice for populations hatched from now on - " + Summary();
                default:
                    return $"memberdraw: unknown '{sub}' - try: memberdraw | memberdraw sm35 | memberdraw auto";
            }
        }

        static string Summary()
        {
            string device = SwarmMemberRenderer.Supported
                ? "device has vertex-stage structured buffers (SM 4.5 GraphicsBuffer path)"
                : $"device has NO vertex-stage structured buffers ({SystemInfo.graphicsDeviceType}): SM 3.5 path";
            string fallback = SwarmMemberFallbackDraw.LoadMaterial(out var whyNot) ? "available" : $"UNAVAILABLE ({whyNot})";
            return $"{device}; forced SM 3.5: {(SwarmMemberRenderer.ForceFallback ? "on" : "off")}; SM 3.5 path {fallback}";
        }
    }
}
