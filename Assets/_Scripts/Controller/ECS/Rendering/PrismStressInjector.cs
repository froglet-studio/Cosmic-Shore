#if UNITY_EDITOR || DEVELOPMENT_BUILD
using CosmicShore.Gameplay;
using CosmicShore.Utility.PerformanceBenchmark;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CosmicShore.ECS
{
    /// <summary>
    /// Dev-only stress injector. Press F10 in ANY scene — the Menu_Main lava-lamp, the
    /// benchmark scene, a race — to toggle a cloud of instanced stress prisms in front of
    /// the main camera, ON TOP of whatever the scene is already doing. This measures the
    /// instanced render path under real game load without any scene authoring.
    ///
    /// The cloud borrows mesh + material from a live Prism in the scene (so it draws with
    /// the scene's actual themed material and batches with the real trail prisms) and spawns
    /// through PrismRenderStressTest, which publishes its numbers to the DiagnosticsHUD
    /// (F7 toggle / F6 advanced). Auto-spawns in editor and development builds only —
    /// compiled out of release entirely.
    /// </summary>
    public class PrismStressInjector : MonoBehaviour
    {
        const string StatsSection = "Debug";

        static PrismStressInjector _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoSpawn()
        {
            if (_instance != null) return;
            var go = new GameObject("[PrismStressInjector]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PrismStressInjector>();
        }

        [SerializeField, Tooltip("Entities in the injected cloud. Editable at runtime on the [PrismStressInjector] object.")]
        private int injectCount = 50_000;
        [SerializeField, Tooltip("Half-extent of the spawn cube.")]
        private float spawnRadius = 600f;
        [SerializeField, Tooltip("How far in front of the main camera the cloud centers.")]
        private float spawnDistance = 700f;

        GameObject _cloud;

        void Start()
        {
            DiagnosticsHUD.SetStat(StatsSection, "F10 stress", "off");
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb[Key.F10].wasPressedThisFrame) return;
            Toggle();
        }

        void Toggle()
        {
            if (_cloud != null)
            {
                Destroy(_cloud); // PrismRenderStressTest.OnDestroy releases entities + its HUD section
                _cloud = null;
                DiagnosticsHUD.SetStat(StatsSection, "F10 stress", "off");
                return;
            }

            var (donorMesh, donorMaterial) = FindDonor();
            if (donorMesh == null || donorMaterial == null)
            {
                Debug.LogWarning("[PrismStressInjector] No live Prism found to borrow mesh/material from — fly and lay some trail first, then press F10 again.");
                return;
            }

            var cam = Camera.main;
            Vector3 center = cam != null
                ? cam.transform.position + cam.transform.forward * spawnDistance
                : Vector3.zero;

            _cloud = new GameObject("[PrismStressCloud]");
            _cloud.transform.position = center;
            var stress = _cloud.AddComponent<PrismRenderStressTest>();
            stress.Configure(donorMesh, donorMaterial, injectCount, spawnRadius);

            DiagnosticsHUD.SetStat(StatsSection, "F10 stress", $"ON ({injectCount:N0})");
        }

        (Mesh mesh, Material material) FindDonor()
        {
            // Debug tool, fired on a key press — the object scan is not a hot path.
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
            DiagnosticsHUD.ClearStats(StatsSection);
            if (_instance == this) _instance = null;
        }
    }
}
#endif
