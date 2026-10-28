using FMODUnity;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// The Ark's REACTOR (Garrett, 2026-10-28): the omni crystal aboard powers the Ark's prism creation. An Ark is the one
    /// body that can contain an omni crystal and harvest its power instead of morphing it into something else (every
    /// vessel's omni pickup turns the crystal into its own effect), so it is the one body whose hull REGROWS: each
    /// destroyed or taken hull prism grows back into its own socket, one at a time, nearest the reactor first, each
    /// spending one prism's worth of the reactor's charge. The Ark refuels by harvesting omni crystals it reaches (each
    /// traversal cell's core lies on its course) whenever a pilot has not taken them first.
    ///
    /// The mass law's fence (Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md, the fourth sign-off): only an Ark, only into its
    /// own empty sockets, only while charge remains, each prism growing in.
    /// </summary>
    [CreateAssetMenu(fileName = "ArkReactor", menuName = "ScriptableObjects/Toys/Ark Reactor")]
    public sealed class ArkReactorSO : ScriptableObject
    {
        [Header("Charge")]
        [SerializeField, Min(1), Tooltip("Hull prisms one omni crystal's power regrows. 36 = a third of the model's 108 sockets.")]
        int prismsPerCrystal = 36;

        [SerializeField, Min(0f), Tooltip("Omni crystals' worth of power aboard when the voyage begins: the crystal in the reactor well.")]
        float startingCrystals = 1f;

        [SerializeField, Min(1f), Tooltip("Omni crystals' worth of power the reactor holds. The Ark harvests a crystal only when " +
                                          "there is room for all of it, and leaves it for the pilots otherwise.")]
        float capacityCrystals = 2f;

        [Header("Regrowth")]
        [SerializeField, Min(0.05f), Tooltip("Seconds between regrown hull prisms (one at a time, each growing in on the prism's own clock).")]
        float regrowSeconds = 0.6f;

        [Header("Harvest")]
        [SerializeField, Min(1f), Tooltip("How close to the Ark's centre an omni crystal must be for the Ark to harvest it. Above the " +
                                          "corridor's arrival distance (140), so a core crystal no pilot took is always reached.")]
        float harvestRadius = 160f;

        [Header("Audio (shipped empty: an unwired slot is a visible TODO)")]
        [SerializeField, Tooltip("An omni crystal arriving in the reactor well.")]
        EventReference harvestSound;

        [SerializeField, Tooltip("One hull prism regrowing into its socket.")]
        EventReference regrowSound;

        public int PrismsPerCrystal => prismsPerCrystal;
        public float StartingCrystals => startingCrystals;
        public float CapacityCrystals => capacityCrystals;
        public float RegrowSeconds => regrowSeconds;
        public float HarvestRadius => harvestRadius;
        public EventReference HarvestSound => harvestSound;
        public EventReference RegrowSound => regrowSound;
    }
}
