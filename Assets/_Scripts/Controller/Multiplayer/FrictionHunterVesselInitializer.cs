using System.Collections.Generic;
using CosmicShore.Data;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Friction's hunter roster is a fixed, intensity-scaled adversary count (2/3/4/5
    /// Rhino hunters per the design doc) rather than the usual team-balancing AI
    /// backfill (which fills out human player counts). Hunters always join
    /// <see cref="Domains.Blue"/> — the "no specific team" sentinel — so they never
    /// count as an ally of any human domain, and AIPilot's same-domain skip in
    /// UpdatePlayerTarget naturally keeps them from targeting each other while still
    /// targeting every human player.
    /// </summary>
    public class FrictionHunterVesselInitializer : ServerPlayerVesselInitializerWithAI
    {
        [Header("Friction Hunter Scaling")]
        [SerializeField]
        int[] hunterCountByIntensity = { 2, 3, 4, 5 };

        [SerializeField]
        float[] hunterSkillByIntensity = { 0f, 0.34f, 0.67f, 1f };

        protected override int ResolveAICount()
        {
            int intensity = Mathf.Clamp(gameData.SelectedIntensity.Value, 1, hunterCountByIntensity.Length);
            return hunterCountByIntensity[intensity - 1];
        }

        protected override Domains ResolveAIDomain(Dictionary<Domains, int> counts) => Domains.Blue;

        protected override float ResolveAISkill()
        {
            int intensity = Mathf.Clamp(gameData.SelectedIntensity.Value, 1, hunterSkillByIntensity.Length);
            return Mathf.Clamp01(hunterSkillByIntensity[intensity - 1]);
        }

        // Tag the spawned instance only — the shared Rhino vessel prefab asset stays
        // untouched, so human Rhino players in every other mode are unaffected by
        // hunter-only impact effects like VesselLifeLossByHunterSkimmerEffectSO.
        protected override void OnAIVesselSpawned(NetworkObject aiVesselNO, Player aiPlayer)
        {
            if (!aiVesselNO.gameObject.TryGetComponent<FrictionHunterTag>(out _))
                aiVesselNO.gameObject.AddComponent<FrictionHunterTag>();
        }
    }
}
