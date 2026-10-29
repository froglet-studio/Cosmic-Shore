using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The <b>voyage harbour</b> (Docs/Voyages/SLICE.md): the voyage's settings, and the toybox entry that stands its
    /// runner in freestyle. It is NOT a toy the player sees (Garrett, 2026-10-28: the Voyage toy "is confusing" in the Toy
    /// Box): it builds no station and lists in no Toy Box. Voyages are chosen in the app shell, the Voyage window, where
    /// each job on offer is a voyage card drawn over the galaxy map (<c>VoyageJobsView</c>). Setting sail deploys the
    /// squad - you fly your seat's vessel, AI wingmates fly the others, each with its slot's pilot, artifact and element -
    /// and the Ark sails the lanes to the job. Its work done, the squad anchors there and the window offers the next jobs
    /// from that spot; the jobs chain through the news each outcome writes. Dock at home to keep what you found.
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

        [SerializeField, Tooltip("A lane's colour.")]
        Color laneColor = new(1f, 1f, 1f, 0.35f);

        public override ToyCategory Category => ToyCategory.World;

        public ArkwaySettingsSO WithArk => withArk;
        public VoyageContentSO Content => content ? content : VoyageContentSO.Load();
        public Color RouteColor => routeColor;
        public Color HereColor => hereColor;
        public float SeedElementLevel => seedElementLevel;
        public float WingmateLeash => wingmateLeash;
        public float FindDistance => findDistance;
        public float VaultRadius => vaultRadius;
        public int VaultPrisms => vaultPrisms;
        public Vector3 VaultPrismScale => vaultPrismScale;
        public float VaultOpenShare => vaultOpenShare;
        public float HoldRadius => holdRadius;
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

        /// <summary>Stands the voyage's runner (no station, no ring: nothing to fly through).</summary>
        public override void Spawn(Transform parent, ToyPlacement placement, ToyContext context) =>
            VoyageHarbour.Create(this, parent, context);
    }
}
