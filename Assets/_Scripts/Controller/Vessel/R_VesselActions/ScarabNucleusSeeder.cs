using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Scarab's NUCLEUS SEEDING ability (design: R_VesselActions/SCARAB.md §4.6): while a Scarab
    /// is flying, balls of its domain periodically appear embedded in the cell's nucleus, waiting for
    /// anyone to knock them loose — outward into the cytoplasm to bounce around for fun, or inward
    /// into the nucleus, which in Scarab Scramble is the court.
    ///
    /// PASSIVE by design, the Dolphin crystal-seeding shape: no input binding, no meter, no HUD slot.
    /// That is what lets it be a property of the VESSEL rather than of a mode — it needs nothing wired
    /// per scene, so it works in freestyle and the menu exactly as it does in a match. It is also why
    /// there is no ability SO here: a passive ability is bound to no input event, so
    /// <c>CollectBoundActions</c> could never resolve one (the lesson recorded on the Dolphin's own
    /// passive seeding).
    ///
    /// SERVER-SIDE ONLY. Balls are NetworkObjects, so only the server may mint one; this component
    /// runs its clock everywhere but acts only where a spawn is legal (host, or a no-network local
    /// session). A client's Scarab therefore seeds nothing of its own — the host seeds on behalf of
    /// every Scarab it simulates, which is every Scarab.
    ///
    /// Everything it does routes through <see cref="ScarabNucleusField"/>, which owns the per-cell
    /// book (embedded caps, nucleus entries, the overload). This class only answers "is it time, and
    /// which cell am I in".
    /// </summary>
    public class ScarabNucleusSeeder : MonoBehaviour
    {
        [Tooltip("Tuning for the whole ability. Leave empty to load Resources/ScarabNucleusFieldConfig.")]
        [SerializeField] ScarabNucleusFieldConfigSO config;

        IVesselStatus _status;
        float _nextSeedTime;
        Cell _cell;

        const string ConfigResourcePath = "ScarabNucleusFieldConfig";

        void Awake()
        {
            _status = GetComponent<VesselStatus>();
            if (config == null) config = Resources.Load<ScarabNucleusFieldConfigSO>(ConfigResourcePath);

            // Stagger the first seed by the full interval so a fresh match does not open with a
            // nucleus already studded before anyone has flown anywhere.
            _nextSeedTime = Time.time + (config != null ? config.seedIntervalSeconds : 14f);
        }

        void Update()
        {
            if (config == null || config.ballPrefab == null) return;   // unwired = silent, per the audio/reference convention
            if (_status == null) return;
            if (!ScarabBallForge.CanSpawnLocally) return;              // clients never mint

            if (Time.time < _nextSeedTime) return;

            // Re-arm FIRST, so a refusal (no nucleus, domain at its cap) costs one interval rather
            // than retrying every frame — the cap PAUSES the clock, it never culls anything.
            _nextSeedTime = Time.time + Mathf.Max(0.5f, config.seedIntervalSeconds);

            var cell = ResolveCell();
            if (cell == null) return;

            var field = ScarabNucleusField.ForCell(cell, config);
            field?.TrySeed(_status.Domain, ScarabBallForge.SizeScaleFor(_status));
        }

        /// <summary>
        /// The cell this Scarab is flying in. Re-resolved whenever the cached one dies (a cell swap
        /// through the freestyle selector destroys and rebuilds the world), but not every tick — the
        /// lookup is a scene search and the seed clock is measured in seconds.
        /// </summary>
        Cell ResolveCell()
        {
            if (_cell == null) _cell = Cell.FindNearestActiveCell(transform.position);
            return _cell;
        }
    }
}
