#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CosmicShore.Gameplay;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;

namespace CosmicShore.ECS
{
    /// <summary>
    /// Dev-only stress injector, driven by the DiagnosticsHUD command console
    /// (F7 shows the HUD; type in its input field and press Enter or Run):
    ///
    ///   prisms 50000   spawn a 50,000-entity instanced stress cloud ahead of the camera
    ///   prisms         spawn with the default count
    ///   prisms off     remove the cloud
    ///
    /// Works in ANY scene — the Menu_Main lava-lamp, the benchmark scene, a race — ON TOP
    /// of whatever the scene is doing, measuring the instanced render path under real game
    /// load without scene authoring. The cloud borrows mesh + material from a live Prism
    /// (so it draws with the scene's actual themed material and batches with the real trail
    /// prisms) and spawns through PrismRenderStressTest, which publishes its numbers to the
    /// HUD. Auto-spawns in editor and development builds only — compiled out of release.
    /// </summary>
    public class PrismStressInjector : MonoBehaviour
    {
        const string StatsSection = "Debug";
        const string CommandName = "prisms";
        const string IdleHint = "off — cmd: prisms <count> | prisms off";

        static PrismStressInjector _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (_instance != null) return;
            var go = new GameObject("[PrismStressInjector]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PrismStressInjector>();
        }

        [SerializeField, Tooltip("Cloud size used when 'prisms' is run without a count.")]
        private int defaultCount = 50_000;
        [SerializeField, Tooltip("Half-extent of the spawn cube.")]
        private float spawnRadius = 600f;
        [SerializeField, Tooltip("How far in front of the main camera the cloud centers.")]
        private float spawnDistance = 700f;

        GameObject _cloud;

        void Start()
        {
            DiagnosticsHUD.RegisterCommand(CommandName, HandlePrismsCommand);
            DiagnosticsHUD.RegisterCommand("prismcolors", HandlePrismColorsCommand);
            DiagnosticsHUD.SetStat(StatsSection, "stress", IdleHint);
        }

        // A/B hook for the instanced-path color-space fix: 'prismcolors raw' reproduces
        // the too-bright pre-fix look, 'linear' forces the converted look, 'auto' returns
        // to the project default. Affects colors written AFTER the command (new trail /
        // re-spawned cloud / next theme animation), not already-written ones.
        string HandlePrismColorsCommand(string[] args)
        {
            if (args.Length == 0) return "usage: prismcolors linear | raw | auto";
            switch (args[0].ToLowerInvariant())
            {
                case "linear":
                    PrismRenderService.SetColorConversionOverride(true);
                    return "instanced colors: sRGB→linear conversion ON (affects new writes)";
                case "raw":
                    PrismRenderService.SetColorConversionOverride(false);
                    return "instanced colors: RAW/unconverted (affects new writes)";
                case "auto":
                    PrismRenderService.SetColorConversionOverride(null);
                    return "instanced colors: automatic (Linear project ⇒ convert)";
                default:
                    return "usage: prismcolors linear | raw | auto";
            }
        }

        string HandlePrismsCommand(string[] args)
        {
            if (args.Length > 0 && (args[0] == "off" || args[0] == "0"))
            {
                if (_cloud == null) return "no stress cloud active";
                Despawn();
                return "stress cloud removed";
            }

            int count = defaultCount;
            if (args.Length > 0 && (!int.TryParse(args[0], out count) || count <= 0))
                return "usage: prisms <count> | prisms off";

            var (donorMesh, donorMaterial) = FindDonor();
            if (donorMesh == null || donorMaterial == null)
                return "no live prism to borrow mesh/material from — lay some trail first";

            Despawn(); // replace any existing cloud

            var cam = Camera.main;
            Vector3 center = cam != null
                ? cam.transform.position + cam.transform.forward * spawnDistance
                : Vector3.zero;

            _cloud = new GameObject("[PrismStressCloud]");
            _cloud.transform.position = center;
            var stress = _cloud.AddComponent<PrismRenderStressTest>();
            stress.Configure(donorMesh, donorMaterial, count, spawnRadius);

            DiagnosticsHUD.SetStat(StatsSection, "stress", $"ON ({count:N0})");
            return $"spawned {count:N0} instanced stress prisms";
        }

        void Despawn()
        {
            if (_cloud == null) return;
            Destroy(_cloud); // PrismRenderStressTest.OnDestroy releases entities + its HUD section
            _cloud = null;
            DiagnosticsHUD.SetStat(StatsSection, "stress", IdleHint);
        }

        (Mesh mesh, Material material) FindDonor()
        {
            // Debug tool, fired from a console command — the object scan is not a hot path.
            foreach (var prism in FindObjectsByType<Prism>(FindObjectsSortMode.None))
            {
                if (!prism.isActiveAndEnabled) continue;
                if (prism.TryGetComponent(out MeshFilter mf) && mf.sharedMesh != null &&
                    prism.TryGetComponent(out MeshRenderer mr) && mr.sharedMaterial != null)
                    return (mf.sharedMesh, mr.sharedMaterial);
            }
            return (null, null);
        }

        void OnDestroy()
        {
            DiagnosticsHUD.UnregisterCommand(CommandName);
            DiagnosticsHUD.UnregisterCommand("prismcolors");
            DiagnosticsHUD.ClearStats(StatsSection);
            if (_instance == this) _instance = null;
        }
    }
}
#endif
