using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>Voyage</b> toy (Docs/Voyages/SLICE.md): fly it and your squad deploys - you fly your seat's vessel, AI
    /// wingmates fly the others, each with its slot's pilot, artifact and element - and the job board stands around you,
    /// one job from each seated pilot. Thread a job and the Ark sails the lanes to it; at each stop the next board
    /// stands, and the jobs chain through the news each outcome writes. Dock at home to keep what you found. Fly the toy
    /// again (or the harbour station) to give the voyage up.
    ///
    /// <para>It is the Arkway with a route: the Ark, the corridor, the leash and the payloads are the Arkway's own
    /// (<see cref="ArkwayRun"/>), sailed from <see cref="withArk"/>'s settings. The map, crew, jobs and every line are
    /// <see cref="VoyageContentSO"/>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Toy_Voyage", menuName = "ScriptableObjects/Toys/Voyage Toy")]
    public class VoyageToyDefinitionSO : ToyDefinitionSO
    {
        [Header("Voyage - what it sails")]
        [SerializeField, Tooltip("The Ark and corridor settings (the Arkway's own asset, Wander_WithArk). Empty = code " +
                                 "defaults with no prism prefab, so no Ark can be laid and the voyage refuses to sail.")]
        ArkwaySettingsSO withArk;

        [SerializeField, Tooltip("The map, crew, jobs and lines. Empty = the shipped Resources/Voyages/VoyageContent.")]
        VoyageContentSO content;

        [Header("Voyage - the board")]
        [SerializeField, Min(40f), Tooltip("How far from you (or the Ark) a job's beacon stands, on its destination's bearing.")]
        float boardDistance = 260f;

        [SerializeField, Min(20f), Tooltip("Least gap between two beacons; a beacon on a crowded bearing turns aside.")]
        float beaconSpacing = 110f;

        [SerializeField, Min(2f), Tooltip("A job beacon's body radius.")]
        float beaconBodyRadius = 9f;

        [SerializeField, Min(4f), Tooltip("A job beacon's trigger (and ring) radius.")]
        float beaconTriggerRadius = 26f;

        [SerializeField, Tooltip("A standing job's beacon colour.")]
        Color jobColor = new(0.70f, 1f, 0.35f, 1f);

        [SerializeField, Tooltip("The way home's beacon colour.")]
        Color homeColor = new(1f, 0.78f, 0.25f, 1f);

        [Header("Voyage - the squad")]
        [SerializeField, Range(0f, 1f), Tooltip("Element level a seat starts in its slot's element (normalized: 0.1 is one " +
                                                "petal, 0.5 the level-5 twist). Raised to, never lowered.")]
        float seedElementLevel = 0.5f;

        [SerializeField, Min(200f), Tooltip("An AI wingmate further than this from the Ark is brought back to its flank.")]
        float wingmateLeash = 1600f;

        [Header("Voyage - arrivals")]
        [SerializeField, Min(80f), Tooltip("How far from the Ark a vault shell or a wreck stands.")]
        float findDistance = 520f;

        [SerializeField, Min(4f), Tooltip("A vault shell's radius.")]
        float vaultRadius = 26f;

        [SerializeField, Range(8, 200), Tooltip("Super-shielded prisms in a vault shell.")]
        int vaultPrisms = 40;

        [SerializeField, Tooltip("Each vault prism's scale.")]
        Vector3 vaultPrismScale = new(5f, 5f, 8f);

        [SerializeField, Range(0.05f, 1f), Tooltip("Share of a shell's super-shields that must be cracked before it opens.")]
        float vaultOpenShare = 0.5f;

        [SerializeField, Min(50f), Tooltip("How close to the Ark the pilot must stay for a core's hold to run.")]
        float holdRadius = 700f;

        [Header("Voyage - the chart")]
        [SerializeField, Min(10f), Tooltip("The chart's width in world units: the HyperSea map floating beside the Ark.")]
        float chartSize = 90f;

        [SerializeField, Tooltip("Where the chart floats, in the Ark's frame (x starboard, y up, z bow), world units.")]
        Vector3 chartOffset = new(-160f, 70f, 0f);

        [SerializeField, Tooltip("A port's (and a harbour's, before its domain colour) marker colour.")]
        Color portColor = new(0.96f, 0.94f, 0.86f, 1f);

        [SerializeField, Tooltip("A vault field's marker colour.")]
        Color vaultColor = new(0.55f, 0.75f, 1f, 1f);

        [SerializeField, Tooltip("A core's marker colour.")]
        Color coreColor = new(1f, 0.45f, 0.35f, 1f);

        [SerializeField, Tooltip("A lane's colour.")]
        Color laneColor = new(1f, 1f, 1f, 0.35f);

        public override ToyCategory Category => ToyCategory.World;

        public ArkwaySettingsSO WithArk => withArk;
        public VoyageContentSO Content => content ? content : VoyageContentSO.Load();
        public float BoardDistance => boardDistance;
        public float BeaconSpacing => beaconSpacing;
        public float BeaconBodyRadius => beaconBodyRadius;
        public float BeaconTriggerRadius => beaconTriggerRadius;
        public Color JobColor => jobColor;
        public Color HomeColor => homeColor;
        public float SeedElementLevel => seedElementLevel;
        public float WingmateLeash => wingmateLeash;
        public float FindDistance => findDistance;
        public float VaultRadius => vaultRadius;
        public int VaultPrisms => vaultPrisms;
        public Vector3 VaultPrismScale => vaultPrismScale;
        public float VaultOpenShare => vaultOpenShare;
        public float HoldRadius => holdRadius;
        public float ChartSize => chartSize;
        public Vector3 ChartOffset => chartOffset;
        public Color PortColor => portColor;
        public Color VaultColor => vaultColor;
        public Color CoreColor => coreColor;
        public Color LaneColor => laneColor;

        /// <summary>The Ark's settings for a voyage, the corridor's worlds taken from the content's cell pool when it
        /// names any.</summary>
        public ArkwayConfig BuildArkConfig(VoyageContentSO voyage)
        {
            string label = voyage ? voyage.Lines.VoyageName : DisplayName;
            var cfg = withArk ? withArk.BuildConfig(label) : new ArkwayConfig { DisplayName = label };
            if (voyage && voyage.CellPool is { Count: > 0 }) cfg.Cells = voyage.CellPool;
            return cfg;
        }

        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context)
        {
            var go = ToyFactory.CreateRoot(Id, parent, placement, AccentColor);
            var toy = go.AddComponent<VoyageToy>();
            toy.Configure(this);
            toy.Initialize(this, context, placement);
        }
    }
}
