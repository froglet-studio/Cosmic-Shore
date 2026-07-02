using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using CosmicShore.Engine.Injection;
using CosmicShore.Engine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spawns and places the player's unlocked freestyle <see cref="Toy"/>s near the cell
    /// membrane in Menu_Main, spaced far apart. Toys are world-space stations the local vessel
    /// flies into; they have no score and no end condition.
    ///
    /// Runs entirely at runtime and self-wires: drop this on any menu GameObject and it
    /// resolves a <see cref="ToyboxSO"/> (serialized → <c>Resources/Toybox</c> → a synthesised
    /// default toybox of the built-in toys), finds the active <see cref="Cell"/> for placement
    /// (or a configured fallback when the menu has no membrane), and hands each toy the shared
    /// dependencies it needs through a <see cref="ToyContext"/> (since runtime-built toys don't
    /// receive Reflex injection).
    /// </summary>
    public class ToyboxController : MonoBehaviour
    {
        [Header("Toybox")]
        [SerializeField, Tooltip("Toybox config. If empty: loads 'Resources/Toybox', else synthesises a " +
                                 "default toybox of the built-in toys (painting, vessel changer, domain changer).")]
        ToyboxSO toybox;

        [Header("Placement — membrane")]
        [SerializeField, Tooltip("Fraction of the membrane radius at which toys sit (just inside the boundary, " +
                                 "where the vessel flies). Used when a Cell/membrane exists in the scene.")]
        float membraneFraction = 0.82f;

        [Header("Placement — fallback (no membrane)")]
        [SerializeField, Tooltip("Centre used when no Cell/membrane exists in the scene (the current Menu_Main).")]
        Vector3 fallbackCenter = Vector3.zero;

        [SerializeField, Tooltip("Ring radius used when no Cell/membrane exists in the scene.")]
        float fallbackRadius = 300f;

        [Header("Toy size")]
        [SerializeField, Tooltip("Body (visible sphere) radius for each toy, world units.")]
        float toyBodyRadius = 22f;

        [SerializeField, Tooltip("Trigger radius — how close the vessel must get to activate, world units.")]
        float toyTriggerRadius = 42f;

        [Inject] GameDataSO _gameData;
        [Inject] MenuFreestyleEventsContainerSO _freestyleEvents;

        Transform _root;
        bool _placed;
        bool _freestyleActive;
        bool _subscribed;

        // Subscribe in Start (not OnEnable): [Inject] fields are populated after Awake but
        // before Start, so OnEnable would see null _gameData/_freestyleEvents on first enable.
        void Start()
        {
            Subscribe();

            // If the menu vessel is already up (controller added/enabled after OnClientReady
            // already fired), place immediately. The _placed guard makes this idempotent.
            if (_gameData?.LocalPlayer?.Vessel != null)
                HandleClientReady();
        }

        void OnDestroy()
        {
            Unsubscribe();
            ClearToys();
        }

        void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised += HandleClientReady;

            // Fail loud on a missing freestyle-events container (matches MainMenuController).
            _freestyleEvents.OnGameStateTransitionStart.OnRaised += MarkFreestyleActive;
            _freestyleEvents.OnMenuStateTransitionStart.OnRaised += MarkFreestyleInactive;
        }

        void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            if (_gameData?.OnClientReady != null)
                _gameData.OnClientReady.OnRaised -= HandleClientReady;

            _freestyleEvents.OnGameStateTransitionStart.OnRaised -= MarkFreestyleActive;
            _freestyleEvents.OnMenuStateTransitionStart.OnRaised -= MarkFreestyleInactive;
        }

        void MarkFreestyleActive() => _freestyleActive = true;
        void MarkFreestyleInactive() => _freestyleActive = false;

        void HandleClientReady()
        {
            if (_placed) return;
            _placed = true;
            PlaceToys();
        }

        void PlaceToys()
        {
            var box = ResolveToybox();
            if (!box)
            {
                CSDebug.LogWarning("[ToyboxController] No toybox available — nothing to place.");
                return;
            }

            var unlocked = new List<ToyDefinitionSO>();
            foreach (var t in box.UnlockedToys())
                unlocked.Add(t);
            if (unlocked.Count == 0) return;

            if (!_root)
            {
                _root = new GameObject("FreestyleToybox").transform;
                _root.SetParent(transform, false);
            }

            // PORT Deviation (menu-swap arc, restore when MenuServerPlayerVesselInitializer ports): var initializer = FindFirstObjectByType<MenuServerPlayerVesselInitializer>();
            var context = new ToyContext
            {
                GameData = _gameData,
                // PORT Deviation (menu-swap arc, restore when MenuServerPlayerVesselInitializer ports): VesselInitializer = initializer,
                // PORT Deviation (menu-swap arc, restore when MenuServerPlayerVesselInitializer ports): VesselPrefabContainer = initializer ? initializer.VesselPrefabContainer : null,
                IsFreestyleActive = () => _freestyleActive,
            };

            GetPlacementFrame(out Vector3 center, out float radius);

            int autoCount = CountAutoPlaced(unlocked);
            int autoIndex = 0;
            for (int i = 0; i < unlocked.Count; i++)
            {
                var def = unlocked[i];

                float angleDeg = def.PlacementAngleDegrees;
                if (angleDeg < 0f)
                {
                    angleDeg = autoCount > 0 ? 360f * autoIndex / autoCount : 0f;
                    autoIndex++;
                }

                float rad = angleDeg * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));
                var placement = new ToyPlacement(center + dir * radius, center, toyBodyRadius, toyTriggerRadius);

                def.Spawn(_root, placement, context);
            }
        }

        static int CountAutoPlaced(List<ToyDefinitionSO> defs)
        {
            int n = 0;
            foreach (var d in defs)
                if (d.PlacementAngleDegrees < 0f) n++;
            return n;
        }

        void GetPlacementFrame(out Vector3 center, out float radius)
        {
            var cell = Cell.FindNearestActiveCell(Vector3.zero);
            if (cell && cell.MembraneRadius > 1f)
            {
                center = cell.transform.position;
                radius = cell.MembraneRadius * membraneFraction;
                return;
            }

            // No membrane in the menu — ring the configured fallback centre.
            center = fallbackCenter;
            radius = fallbackRadius;
        }

        ToyboxSO ResolveToybox()
        {
            if (toybox) return toybox;
            toybox = Resources.Load<ToyboxSO>("Toybox");
            if (toybox) return toybox;

            toybox = BuildDefaultToybox();
            return toybox;
        }

        /// <summary>
        /// Code-built fallback so the toybox works the moment this controller is in the scene,
        /// before any ToyboxSO/ToyDefinitionSO assets are authored (the editor setup tool, or a
        /// designer, can later author real assets that replace this).
        /// </summary>
        static ToyboxSO BuildDefaultToybox()
        {
            var box = ScriptableObject.CreateInstance<ToyboxSO>();

            var painting = MakeDefault<PaintingToyDefinitionSO>(
                "painting", "Fly by Numbers", "Trace a pattern with your trail.", new Color(0.20f, 0.90f, 1.00f));
            // Give the zero-config default a real shape so it actually paints (a star, auto-generated).
            var star = ScriptableObject.CreateInstance<ShapeDefinition>();
            star.shapeName = "Star";
            star.autoGeneratePreset = ShapePreset.Star;
            star.autoGenerateRadius = 100f;
            painting.SetRuntimeShape(star);
            box.AddToy(painting);

            box.AddToy(MakeDefault<VesselChangerToyDefinitionSO>(
                "vessel_changer", "Vessel Changer", "Fly through to swap your ship.", new Color(1.00f, 0.85f, 0.20f)));
            box.AddToy(MakeDefault<DomainChangerToyDefinitionSO>(
                "domain_changer", "Domain Changer", "Fly through to change your team colour.", new Color(0.85f, 0.30f, 0.90f)));
            return box;
        }

        static T MakeDefault<T>(string id, string displayName, string description, Color accent)
            where T : ToyDefinitionSO
        {
            var def = ScriptableObject.CreateInstance<T>();
            def.SetRuntimeMetadata(id, displayName, description, accent);
            return def;
        }

        void ClearToys()
        {
            // Every toy / coordinator lives under _root, so one destroy tears the whole set down.
            if (_root) Destroy(_root.gameObject);
            _root = null;
            _placed = false;
        }
    }
}
