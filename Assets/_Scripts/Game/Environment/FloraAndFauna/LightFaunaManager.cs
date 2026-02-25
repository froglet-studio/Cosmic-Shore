using UnityEngine;
using System.Collections.Generic;
using CosmicShore.Game.Environment.CellModifiers;
using CosmicShore.Game.Environment.Cytoplasm;
using CosmicShore.Game.Environment.FlowField;
using CosmicShore.Game.Environment.MiniGameObjects;
using CosmicShore.Game.IO;
using CosmicShore.Game.ImpactEffects;
using CosmicShore.Game.ImpactEffects.Containers;
using CosmicShore.Game.ImpactEffects.EffectsSO;
using CosmicShore.Game.ImpactEffects.EffectsSO.AbstractEffectTypes;
using CosmicShore.Game.ImpactEffects.EffectsSO.Helpers;
using CosmicShore.Game.ImpactEffects.EffectsSO.ProjectileCrystalEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.ProjectileEndEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.ProjectileMineEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.ProjectilePrismEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.SkimmerPrismEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.VesselCrystalEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.VesselExplosionEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.VesselPrismEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.VesselProjectileEffects;
using CosmicShore.Game.ImpactEffects.EffectsSO.VesselSkimmerEffects;
using CosmicShore.Game.ImpactEffects.Impactors;
using CosmicShore.Game.Managers;
using CosmicShore.Game.Multiplayer;
using CosmicShore.Game.Player;
using CosmicShore.Game.Prisms;
using CosmicShore.Game.Projectiles;
using CosmicShore.Game.Ship;
using CosmicShore.Game.Ship.R_ShipActions.DataContainers;
using CosmicShore.Game.Ship.R_ShipActions.Executors;
using CosmicShore.Game.Ship.ShipActions;
using CosmicShore.Game.UI;
using CosmicShore.Models.Enums;
using CosmicShore.Models.ScriptableObjects;
using CosmicShore.UI.Modals;
using CosmicShore.Utility.DataContainers;
using CosmicShore.Utility.Effects;
using CosmicShore.Utility.SOAP.ScriptableClassType;
using CosmicShore.VesselHUD.Controller;
using CosmicShore.VesselHUD.Interfaces;
using CosmicShore.VesselHUD.View;
using CosmicShore.UI.Views;
using CosmicShore.Utility;
using CosmicShore.Game.Environment;
using CosmicShore.Utility.Recording;

namespace CosmicShore.Game.Environment.FloraAndFauna
{
    /// <summary>
    /// Manages a group of <see cref="LightFauna"/> creatures.
    /// Handles spawning, formation layout, and population maintenance.
    /// Extends Fauna for domain/goal propagation from the spawning system (LSP-compliant:
    /// lifecycle methods use base defaults instead of throwing NotImplementedException).
    /// </summary>
    public class LightFaunaManager : Fauna
    {
        [Header("Prefab")]
        [SerializeField] LightFauna lightFaunaPrefab;

        [Header("Data")]
        [SerializeField] LightFaunaManagerDataSO managerData;

        private readonly List<LightFauna> activeFauna = new();

        protected override void Start()
        {
            base.Start();
            SpawnGroup();
        }

        void SpawnGroup()
        {
            if (!managerData)
            {
                CSDebug.LogError($"{nameof(LightFaunaManager)} on {name} is missing {nameof(LightFaunaManagerDataSO)}.");
                return;
            }

            if (!lightFaunaPrefab)
            {
                CSDebug.LogError($"{nameof(LightFaunaManager)} on {name} is missing LightFauna prefab reference.");
                return;
            }

            int count = Mathf.Max(0, managerData.spawnCount);
            float radius = Mathf.Max(0f, managerData.spawnRadius);

            for (int i = 0; i < count; i++)
            {
                Vector3 randomOffset = Random.insideUnitSphere * radius;
                randomOffset.y = 0f;

                Vector3 spawnPosition = transform.position + randomOffset;

                LightFauna fauna = Instantiate(lightFaunaPrefab, spawnPosition, Random.rotation, transform);
                fauna.domain = domain;
                fauna.LightFaunaManager = this;
                fauna.Phase = managerData.phaseIncrease * i;
                fauna.Initialize(cell);

                activeFauna.Add(fauna);
            }

            ApplyFormation();
        }

        void ApplyFormation()
        {
            if (activeFauna.Count == 0) return;

            float spread = Mathf.Max(0f, managerData.formationSpread);

            for (int i = 0; i < activeFauna.Count; i++)
            {
                float angle = (i * 360f / activeFauna.Count) * Mathf.Deg2Rad;
                Vector3 formationOffset = new Vector3(
                    Mathf.Cos(angle) * spread,
                    0f,
                    Mathf.Sin(angle) * spread
                );

                activeFauna[i].transform.position = transform.position + formationOffset;
            }
        }

        public void RemoveFauna(LightFauna fauna)
        {
            if (activeFauna.Contains(fauna))
            {
                activeFauna.Remove(fauna);
                Destroy(fauna.gameObject);
            }

            if (managerData && activeFauna.Count < Mathf.Max(0, managerData.spawnCount / 2))
                SpawnGroup();
        }
    }
}
