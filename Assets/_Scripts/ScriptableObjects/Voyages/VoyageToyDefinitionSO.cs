using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>voyage settings</b> (Docs/Voyages/SLICE.md): what a voyage sails and how the Voyage window draws it. Not a
    /// toy (the name is history): voyages have their own scene, <c>Voyage.unity</c>, and nothing of them stands in
    /// freestyle (Garrett, 2026-11-01: "we do not need to connect the freestyle and voyage modes"). The app shell's Voyage
    /// window draws one card per job on offer and the Ark escort's card, and Set sail loads the voyage scene with the
    /// choice (<see cref="VoyageLaunch"/>), where <see cref="VoyageHarbour"/> deploys the squad and the Ark sails.
    ///
    /// <para>The Ark, the corridor, the leash and the payloads are the Arkway's own (<see cref="ArkwayRun"/>), sailed from
    /// <see cref="withArk"/>'s settings. The map, crew, jobs and every line are <see cref="VoyageContentSO"/>. The shipped
    /// asset is <c>Resources/Voyages/Voyage</c> (<see cref="Load"/>).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "Voyage", menuName = "ScriptableObjects/Voyages/Voyage Settings")]
    public class VoyageToyDefinitionSO : ScriptableObject
    {
        /// <summary>Where the shipped settings live, under Resources.</summary>
        public const string ResourcePath = "Voyages/Voyage";

        static VoyageToyDefinitionSO s_loaded;

        /// <summary>The shipped voyage settings (<c>Resources/Voyages/Voyage</c>), or null when the asset is missing.</summary>
        public static VoyageToyDefinitionSO Load()
        {
            if (!s_loaded) s_loaded = Resources.Load<VoyageToyDefinitionSO>(ResourcePath);
            return s_loaded;
        }

        [Header("Identity")]
        [SerializeField, Tooltip("The voyage's name where no content names one.")]
        string displayName = "Voyage";

        [SerializeField, TextArea, Tooltip("One line on what a voyage is.")]
        string description;

        [SerializeField, Tooltip("The voyage's accent colour.")]
        Color accentColor = new(0.95f, 0.75f, 0.25f, 1f);

        [Header("Voyage - what it sails")]
        [SerializeField, Tooltip("The Ark and corridor settings (the Arkway's own asset, Wander_WithArk). Empty = code " +
                                 "defaults with no prism prefab, so no Ark can be laid and the voyage refuses to sail.")]
        ArkwaySettingsSO withArk;

        [SerializeField, Tooltip("The map, crew, jobs and lines. Empty = the shipped Resources/Voyages/VoyageContent.")]
        VoyageContentSO content;

        [SerializeField, Tooltip("The Ark escort: its worlds, its 1-9 difficulty tiers and its lines. Empty = no escort card.")]
        ArkEscortSO arkEscort;

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

        [SerializeField, Min(100f), Tooltip("A creature felled within this of the Ark counts for a cull (Docs/Voyages/DEPTH.md section 4).")]
        float cullRadius = 900f;

        [SerializeField, Min(20f), Tooltip("A living creature within this of the pilot counts as observed (a researcher's study).")]
        float observeRadius = 260f;

        [SerializeField, Min(100f), Tooltip("How far abeam of the Ark a rival crew's hulls stand at a stop.")]
        float rivalAbeam = 420f;

        [Header("Voyage - the galaxy map (the Voyage window's cards)")]
        [SerializeField, Tooltip("The course a card's job sails, drawn over the map.")]
        Color routeColor = new(0.70f, 1f, 0.35f, 1f);

        [SerializeField, Tooltip("Where the squad is now.")]
        Color hereColor = new(1f, 0.78f, 0.25f, 1f);

        [SerializeField, Tooltip("A port's (and a harbour's) marker colour.")]
        Color portColor = new(0.96f, 0.94f, 0.86f, 1f);

        [SerializeField, Tooltip("A vault field's marker colour.")]
        Color vaultColor = new(0.55f, 0.75f, 1f, 1f);

        [SerializeField, Tooltip("A core's marker colour.")]
        Color coreColor = new(1f, 0.45f, 0.35f, 1f);

        [SerializeField, Tooltip("A lair's (and a pen's) marker colour.")]
        Color lairColor = new(1f, 0.3f, 0.55f, 1f);

        [SerializeField, Tooltip("A nursery's marker colour.")]
        Color nurseryColor = new(0.6f, 1f, 0.6f, 1f);

        [SerializeField, Tooltip("A shrine's (and a derelict's, a hoard's) marker colour.")]
        Color shrineColor = new(0.9f, 0.85f, 1f, 1f);

        [SerializeField, Tooltip("A place this crew has not found yet, where the map shows one at all.")]
        Color unchartedColor = new(0.5f, 0.5f, 0.5f, 0.6f);

        [SerializeField, Tooltip("A lane's colour.")]
        Color laneColor = new(1f, 1f, 1f, 0.35f);

        public string DisplayName => displayName;
        public string Description => description;
        public Color AccentColor => accentColor;

        public ArkwaySettingsSO WithArk => withArk;
        public VoyageContentSO Content => content ? content : VoyageContentSO.Load();
        public ArkEscortSO ArkEscort => arkEscort;
        // The map's colours are the worn colour set's (ColorTrace): the Voyage window draws them on its canvas.
        public Color RouteColor => ColorTrace.Retrace(ColorTrace.Ui, routeColor);
        public Color HereColor => ColorTrace.Retrace(ColorTrace.Ui, hereColor);
        public float SeedElementLevel => seedElementLevel;
        public float WingmateLeash => wingmateLeash;
        public float FindDistance => findDistance;
        public float VaultRadius => vaultRadius;
        public int VaultPrisms => vaultPrisms;
        public Vector3 VaultPrismScale => vaultPrismScale;
        public float VaultOpenShare => vaultOpenShare;
        public float HoldRadius => holdRadius;
        public float CullRadius => cullRadius;
        public float ObserveRadius => observeRadius;
        public float RivalAbeam => rivalAbeam;
        public Color PortColor => ColorTrace.Retrace(ColorTrace.Ui, portColor);
        public Color VaultColor => ColorTrace.Retrace(ColorTrace.Ui, vaultColor);
        public Color CoreColor => ColorTrace.Retrace(ColorTrace.Ui, coreColor);
        public Color LairColor => ColorTrace.Retrace(ColorTrace.Ui, lairColor);
        public Color NurseryColor => ColorTrace.Retrace(ColorTrace.Ui, nurseryColor);
        public Color ShrineColor => ColorTrace.Retrace(ColorTrace.Ui, shrineColor);
        public Color UnchartedColor => ColorTrace.Retrace(ColorTrace.Ui, unchartedColor);
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
    }
}
